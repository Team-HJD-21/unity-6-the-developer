// 실제 HUD 버튼이 Host 명령을 정확히 한 번 실행하고 확정 투영을 표시하는지 검사합니다.
// 마우스/해상도 인수와 원격 B actor 이동 복제 검증을 대신하지 않습니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;
public sealed class StageTurretCommandHudTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>(); if (app != null) app.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator MapLocksNearbyUnoccupiedTurretsUntilTheirCellIsCaptured()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();yield return EnterFieldThroughTheSpaceshipExit();
        var hud=Object.FindAnyObjectByType<InitialStagePlayerHUD>();var t=hud.Tactical;
        hud.LocalPlayer.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        var turret=FindInactive(hud.Turrets,default);turret.TryGetEntityId(out var id);
        hud.LocalPlayer.transform.position=turret.transform.position;yield return new WaitForSecondsRealtime(.2f);
        Assert.That(t.Open(),Is.True);yield return FinishTransition(t);Assert.That(t.Select(id),Is.True);
        Assert.That(t.CanOperate(t.Selected),Is.False);Assert.That(t.View.ToggleLabel.text,Is.EqualTo("잠김"));
        Assert.That(t.View.ToggleTurret.interactable,Is.False);Assert.That(t.View.DockState.Shape,Is.EqualTo(TacticalSymbolGraphic.Symbol.Off),"원격 잠금이 실제 해금/전원 상태를 덮지 않습니다.");
        Assert.That(t.View.ToggleTurret.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>().Icon,Is.InstanceOf<TacticalSymbolGraphic>());
        Assert.That(((TacticalSymbolGraphic)t.View.ToggleTurret.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>().Icon).Shape,Is.EqualTo(TacticalSymbolGraphic.Symbol.Locked));
        t.View.ToggleTurret.onClick.Invoke();Assert.That(turret.IsActivated,Is.False,"오래된 UI 이벤트도 미점유 조작을 통과시키지 않습니다.");
        CaptureCellForMap(Object.FindAnyObjectByType<RegionSceneBinding>(),turret);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(t.CanOperate(t.Selected),Is.True);Assert.That(t.View.ToggleLabel.text,Is.EqualTo("켜기"));
        Click(t.View.ToggleTurret);yield return new WaitForSecondsRealtime(.2f);
        Assert.That(turret.IsActivated,Is.True);Assert.That(t.View.ToggleLabel.text,Is.EqualTo("끄기"));
        t.Close();yield return FinishTransition(t);yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator FullMembraneHandsOffUiOnlyAfterTheCenterWaveAndCancelsWithoutLeakingInput()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();yield return EnterFieldThroughTheSpaceshipExit();
        var hud=Object.FindAnyObjectByType<InitialStagePlayerHUD>();var t=hud.Tactical;
        hud.LocalPlayer.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        var control=hud.LocalPlayer.GetComponent<PlayerControlState>();
        Assert.That(t.Open(),Is.True);Assert.That(t.IsTransitioning,Is.True);
        Assert.That(t.View.CommandGroup.alpha,Is.Zero);Assert.That(t.IsMapPresented,Is.False);
        Assert.That(control.CanMove||control.CanAct,Is.False);
        bool covered=false;float deadline=Time.realtimeSinceStartup+2;
        while(t.IsTransitioning && Time.realtimeSinceStartup<deadline)
        {
            if(t.View.ModeEffect.Cover>.99f && t.View.ModeEffect.Reveal<.01f)covered=true;
            Assert.That(t.View.CommandGroup.alpha,Is.Zero);Assert.That(t.IsMapPresented,Is.False);
            yield return null;
        }
        Assert.That(covered,Is.True);Assert.That(t.IsMapPresented,Is.True);Assert.That(t.View.CommandGroup.alpha,Is.EqualTo(1));
        t.Close();yield return new WaitForSecondsRealtime(.04f);
        Assert.That(t.Open(),Is.True,"닫는 도중 재요청은 현재 막에서 이어집니다.");
        yield return FinishTransition(t);Assert.That(t.IsOpen,Is.True);
        control.SetInputBlocked(true);yield return null;yield return null;
        Assert.That(t.IsOpen||t.IsTransitioning,Is.False);Assert.That(t.View.ModeEffect.Surface.enabled,Is.False);
        control.SetInputBlocked(false);yield return new WaitForSecondsRealtime(.06f);
        Assert.That(control.CanMove,Is.True,$"종료 프레임 뒤 이동 복구: tactical={control.IsInTacticalView}, blocked={control.IsBlocked}, act={control.CanAct}, frame={Time.frameCount}");
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator RealTurretDamageShakesIconsAndLeavesAHealthTrailWithoutChangingTheCamera()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();yield return EnterFieldThroughTheSpaceshipExit();
        var tactical=Object.FindAnyObjectByType<InitialStagePlayerHUD>().Tactical;
        tactical.Source.LocalPlayer.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        var turret=FindInactive(Object.FindAnyObjectByType<FieldTurretController>(),default);
        tactical.Source.LocalPlayer.transform.position=turret.transform.position+Vector3.down*2;
        yield return new WaitForSecondsRealtime(.2f);Assert.That(tactical.Open(),Is.True);yield return FinishTransition(tactical);
        turret.TryGetEntityId(out var id);tactical.Select(id);
        yield return new WaitForSecondsRealtime(.25f);
        var center=tactical.CameraPosition;int before=turret.CurrentHealth;
        Assert.That(turret.ApplyDamage(System.Math.Max(1,before/4)),Is.True);
        float deadline=Time.realtimeSinceStartup+1;
        while(!tactical.WasHit(id) && Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That(tactical.WasHit(id),Is.True,"실제 Host 피해가 presentation까지 전달되어야 합니다.");
        yield return null;tactical.RenderVisuals();
        Assert.That(tactical.View.DockHealth.Fraction,Is.LessThan(1));
        Assert.That(tactical.View.DockHealth.Trail,Is.GreaterThan(tactical.View.DockHealth.Fraction));
        Assert.That(tactical.View.Incidents.Any(b=>b.gameObject.activeSelf),Is.True);
        bool moved=false;deadline=Time.realtimeSinceStartup+.2f;
        while(Time.realtimeSinceStartup<deadline){yield return null;if(tactical.HitOffset(id).sqrMagnitude>.01f)moved=true;}
        Assert.That(moved,Is.True);Assert.That(Vector2.Distance(center,tactical.CameraPosition),Is.LessThan(.01f));
        yield return new WaitForSecondsRealtime(.75f);
        Assert.That(tactical.View.DockHealth.Trail,Is.EqualTo(tactical.View.DockHealth.Fraction).Within(.001f));
        Assert.That(tactical.HitOffset(id),Is.EqualTo(Vector2.zero));
        Canvas.ForceUpdateCanvases();
        Rect ScreenRect(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
        var navigator=ScreenRect((RectTransform)tactical.View.Overview.parent);
        Assert.That(navigator.Overlaps(ScreenRect((RectTransform)tactical.View.Close.transform)),Is.False);
        Assert.That(navigator.Overlaps(ScreenRect((RectTransform)tactical.View.MapSettings.transform)),Is.False);
        Assert.That(ScreenRect((RectTransform)tactical.View.FocusPlayer.transform).Overlaps(ScreenRect(tactical.View.Overview)),Is.False);
        tactical.Close();yield return FinishTransition(tactical);yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator AuthoredSceneCornersAndSmoothCameraNeverExposeOutsideTheMap()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();yield return EnterFieldThroughTheSpaceshipExit();
        var tactical=Object.FindAnyObjectByType<InitialStagePlayerHUD>().Tactical;
        tactical.Source.LocalPlayer.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Assert.That(tactical.CameraLeftTop,Is.Not.Null);Assert.That(tactical.CameraRightBottom,Is.Not.Null);
        Assert.That(tactical.CameraLeftTop.name,Is.EqualTo("LeftTop"));
        Assert.That(tactical.CameraRightBottom.name,Is.EqualTo("RightBottom"));
        Assert.That(tactical.TryGetAuthoredCameraBounds(out var expectedBounds),Is.True);
        Assert.That(tactical.Open(),Is.True);yield return FinishTransition(tactical);yield return null;
        Assert.That(tactical.WorldBounds,Is.EqualTo(expectedBounds));
        Assert.That(tactical.View.ModeEffect.Surface.raycastTarget,Is.False);
        float firstReveal=tactical.View.ModeEffect.Progress;
        Assert.That(firstReveal,Is.EqualTo(1));
        yield return null;
        Assert.That(tactical.View.ModeEffect.Progress,Is.EqualTo(1).Within(.001f));
        var definition=tactical.Regions.Definition;
        tactical.Zoom(1);yield return new WaitForSecondsRealtime(tactical.Style.CameraZoomSeconds+.12f);
        var initial=tactical.CameraPosition;
        var desired=initial.y>=expectedBounds.center.y?expectedBounds.min:expectedBounds.max;
        var local=TacticalMapMath.Project(desired,expectedBounds,tactical.View.Overview.rect);
        var screen=RectTransformUtility.WorldToScreenPoint(null,tactical.View.Overview.TransformPoint(local));
        tactical.PointerClick(screen,true,false);
        Assert.That(Vector2.Distance(tactical.CameraPosition,initial),Is.LessThan(.001f),"선택한 위치로 같은 프레임에 순간이동하지 않습니다.");
        int intermediates=0;var samples=new System.Collections.Generic.List<Vector2>();
        float deadline=Time.realtimeSinceStartup+tactical.Style.CameraMoveSeconds+.12f;
        while(Time.realtimeSinceStartup<deadline)
        {
            yield return null;samples.Add(tactical.CameraPosition);AssertInside();
        }
        var final=tactical.CameraPosition;
        Assert.That(Vector2.Distance(initial,final),Is.GreaterThan(5));
        foreach(var sample in samples)if(Vector2.Distance(sample,initial)>.01f&&Vector2.Distance(sample,final)>.01f)intermediates++;
        Assert.That(intermediates,Is.GreaterThan(0),"정상 렌더 프레임에서 보간 중간 위치를 실제 관측합니다.");
        float remaining=Vector2.Distance(initial,final);
        foreach(var sample in samples)
        {
            float distance=Vector2.Distance(sample,final);
            Assert.That(distance,Is.LessThanOrEqualTo(remaining+.02f),"한 번의 이동은 목적지를 넘어 튀거나 뒤로 되돌아가지 않습니다.");remaining=distance;
        }
        var zoomStart=tactical.CameraPosition;var zoomPositions=new System.Collections.Generic.List<Vector2>();
        tactical.Zoom(-1);deadline=Time.realtimeSinceStartup+Mathf.Max(tactical.Style.CameraMoveSeconds,tactical.Style.CameraZoomSeconds)+.12f;
        while(Time.realtimeSinceStartup<deadline){yield return null;zoomPositions.Add(tactical.CameraPosition);AssertInside();}
        var zoomEnd=tactical.CameraPosition;
        Assert.That(Vector2.Distance(zoomStart,zoomEnd),Is.GreaterThan(1),"모서리에서 축소하면 viewport를 유지할 새 중심이 필요합니다.");
        Assert.That(zoomPositions.Any(p=>Vector2.Distance(p,zoomStart)>.01f&&Vector2.Distance(p,zoomEnd)>.01f),Is.True,
            "zoom의 FOV뿐 아니라 모서리 재중앙화도 중간 위치를 거칩니다.");
        for(int i=0;i<24;i++)tactical.Zoom(-1);
        deadline=Time.realtimeSinceStartup+tactical.Style.CameraZoomSeconds+.12f;
        while(Time.realtimeSinceStartup<deadline){yield return null;AssertInside();}
        Assert.That(tactical.Regions.Definition,Is.SameAs(definition),"카메라 marker가 점유/명령 권한 원본을 변경하지 않습니다.");
        tactical.Close();yield return FinishTransition(tactical);
        Assert.That(tactical.IsOpen,Is.False);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(tactical.View.ModeEffect.Surface.enabled,Is.False);
        Assert.That(tactical.View.ModeEffect.Progress,Is.Zero);
        yield return new ExitPlayMode();
        void AssertInside()
        {
            var view=tactical.CurrentCameraRect;
            Assert.That(view.xMin,Is.GreaterThanOrEqualTo(expectedBounds.xMin-.02f));
            Assert.That(view.xMax,Is.LessThanOrEqualTo(expectedBounds.xMax+.02f));
            Assert.That(view.yMin,Is.GreaterThanOrEqualTo(expectedBounds.yMin-.02f));
            Assert.That(view.yMax,Is.LessThanOrEqualTo(expectedBounds.yMax+.02f));
        }
    }
    [UnityTest]
    public IEnumerator FieldHoverWaitsForPlayerAttackQuietAndFUsesTheHostCommand()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();yield return EnterFieldThroughTheSpaceshipExit();
        var hud=Object.FindAnyObjectByType<InitialStagePlayerHUD>();
        var tactical=hud.Tactical;
        var turret=FindInactive(Object.FindAnyObjectByType<FieldTurretController>(),default);
        var player=hud.LocalPlayer;
        player.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        player.transform.position=turret.transform.position+Vector3.down*2;
        yield return new WaitForSecondsRealtime(.3f);
        var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        var attack=player.GetComponent<PlayerAttack>();int fired=0;
        System.Action shot=()=>fired++;
        attack.AttackPerformed+=shot;
        void Point(bool down=false)=>UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
            new UnityEngine.InputSystem.LowLevel.MouseState{position=tactical.OutputCamera.WorldToScreenPoint(turret.transform.position),buttons=(ushort)(down?1:0)});
        IEnumerator HoverFor(float seconds,bool down=false)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<end){Point(down);yield return null;}
        }
        try
        {
            yield return HoverFor(.8f);
            Assert.That(tactical.View.Tooltip.gameObject.activeSelf,Is.True,"호버 포인터는 이동 중인 실제 카메라의 터렛 화면 위치를 계속 따라갑니다.");
            Assert.That(tactical.View.Prompt.gameObject.activeSelf,Is.True);
            yield return HoverFor(.06f,true);
            yield return HoverFor(.06f);
            Assert.That(fired,Is.GreaterThan(0),"실제 PlayerAttack 성공 이벤트를 기준으로 호버 대기를 검사합니다.");
            Assert.That(hud.IsTurretPanelOpen,Is.False,"필드의 일반 클릭으로 폐기한 터렛 패널을 열지 않습니다.");
            yield return HoverFor(1);
            Assert.That(tactical.View.Tooltip.gameObject.activeSelf,Is.False,"플레이어 공격 뒤 2초 동안 정보 카드를 감춥니다.");
            yield return HoverFor(1.1f);
            Assert.That(tactical.View.Tooltip.gameObject.activeSelf,Is.True);
            bool before=turret.IsActivated;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F));
            yield return new WaitForSecondsRealtime(.06f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(turret.IsActivated,Is.EqualTo(!before),"근접 F도 실제 Host 명령 경로로 실행합니다.");
            Assert.That(tactical.IsOpen||hud.IsTurretPanelOpen,Is.False);
        }
        finally
        {
            attack.AttackPerformed-=shot;
            if(mouse.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            if(keyboard.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
        }
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator SkillDragThroughRealUiModuleCommitsTheDisplayedDropTarget()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();yield return EnterFieldThroughTheSpaceshipExit();
        var hud=Object.FindAnyObjectByType<PlayerSkillHud>();
        var picker=hud.CombatPicker;
        var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        void Send(Vector2 position,bool down=false)=>UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
            new UnityEngine.InputSystem.LowLevel.MouseState{position=position,buttons=(ushort)(down?1:0)});
        try
        {
            Send(new Vector2(Screen.width*.5f,2));yield return new WaitForSecondsRealtime(.35f);
            Assert.That(picker.IsOpen,Is.True);
            var wedge=picker.Wedges.Single(w=>w.Skill==PlayerSkillId.PlayerTurret);
            Vector2 local=CombatHudGeometry.Radial(262,CombatHudGeometry.SkillAngle(wedge.Skill));
            Vector2 start=RectTransformUtility.WorldToScreenPoint(null,wedge.transform.TransformPoint(local));
            Vector2 target=new(Screen.width*.3f,Screen.height*.55f);
            Send(start);yield return new WaitForSecondsRealtime(.06f);
            Send(start,true);yield return new WaitForSecondsRealtime(.06f);
            Send(Vector2.Lerp(start,target,.4f),true);yield return new WaitForSecondsRealtime(.06f);
            Assert.That(picker.IsDragging,Is.True,"이 검사는 Picker.BeginDrag를 직접 호출하지 않고 UI InputModule의 down/move를 통과합니다.");
            Send(target,true);yield return new WaitForSecondsRealtime(.06f);
            Assert.That(picker.Destination,Is.EqualTo(0));
            Send(target);yield return new WaitForSecondsRealtime(.06f);
            Assert.That(hud.Owner.Loadout.Q,Is.EqualTo(PlayerSkillId.PlayerTurret));
            Assert.That(hud.Owner.Loadout.E,Is.EqualTo(PlayerSkillId.None));
        }
        finally{if(mouse.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);}
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator TacticalCameraPanZoomGoalAndClosePreservePlayerAndWorldTime()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return EnterFieldThroughTheSpaceshipExit();
        var hud=Object.FindAnyObjectByType<InitialStagePlayerHUD>(); var t=hud.Tactical;
        hud.LocalPlayer.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        var control=hud.LocalPlayer.GetComponent<PlayerControlState>();
        var player=t.PlayerPosition; float original=t.OutputCamera.orthographicSize;
        var clock=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IMatchDayNightCycle>().First();
        double time=clock.RemainingSeconds;
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        try
        {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.M));
        yield return new WaitForSecondsRealtime(.05f);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
        yield return new WaitForSecondsRealtime(.05f);
        Assert.That(t.IsOpen,Is.True,$"Field={hud.HasFieldActor}, HP={hud.LocalPlayer.curHp}, commands={control.CanIssueTurretCommands}, blocked={control.IsBlocked}");
        yield return FinishTransition(t);
        var status=hud.GetComponent<PlayerStatusPanel>();
        Assert.That(status.TryOpen(),Is.False,"전술 화면 위에 상태/스킬 트리 패널을 겹쳐 열지 않습니다.");
        Assert.That(t.CurrentCameraRect.Contains(player),Is.True,"맵 경계에서는 중심을 보정하되 플레이어를 계속 보여줍니다.");
        Assert.That(TeamHJD.Game.Presentation.Scene.CameraManager.Current.ActiveTarget.parent,Is.Null,"월드 카메라 target은 Canvas 배율과 독립적입니다.");
        Assert.That(control.CanMove||control.CanAct,Is.False); Assert.That(control.CanIssueTurretCommands,Is.True);
        var skills=Object.FindAnyObjectByType<PlayerSkillHud>();
        Assert.That(skills.IsFieldVisible && skills.GetComponent<Canvas>().enabled,Is.True,"전술 중에도 같은 Canvas의 타이머는 유지합니다.");
        Assert.That(skills.AreCombatControlsVisible,Is.False); Assert.That(skills.CombatPicker.CompactGroup.alpha,Is.Zero);
        var header=skills.GetComponentInChildren<CombatHudHeader>(true);
        float headerDeadline=Time.realtimeSinceStartup+1.5f;
        while(!header.OccupancyLabel.gameObject.activeSelf && Time.realtimeSinceStartup<headerDeadline)yield return null;
        Assert.That(header.OccupancyLabel.gameObject.activeSelf,Is.True,"확정된 점유 가능 분모가 있으면 점유율을 표시합니다.");
        double expectedPercent=100.0*t.Regions.CapturedCellCount/t.Regions.Definition.CapturableCellCount;
        string number=header.OccupancyLabel.text.TrimEnd('%');
        if(number.StartsWith("<"))Assert.That(expectedPercent,Is.GreaterThan(0).And.LessThan(.1));
        else
        {
            double displayed=double.Parse(number,System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(displayed,Is.EqualTo(expectedPercent).Within(expectedPercent>0&&expectedPercent<1?.051:.501));
        }
        header.OccupancyLabel.ForceMeshUpdate(true);
        Assert.That(header.OccupancyLabel.isTextOverflowing,Is.False);
        Assert.That(((RectTransform)t.View.transform).rect.center.magnitude,Is.LessThan(.001f));
        float fit=t.OutputCamera.orthographicSize; t.Zoom(1); yield return new WaitForSecondsRealtime(.05f);
        Assert.That(t.OutputCamera.orthographicSize,Is.LessThan(fit));
        var corners=new Vector3[4]; t.View.Overview.GetWorldCorners(corners);
        var a=RectTransformUtility.WorldToScreenPoint(null,Vector3.Lerp(corners[0],corners[2],.45f));
        var b=RectTransformUtility.WorldToScreenPoint(null,Vector3.Lerp(corners[0],corners[2],.55f));
        var camera=t.CameraPosition; t.BeginPointerDrag(a,true); t.PointerDrag(a,b,b-a,true,true); t.EndPointerDrag(a,b,true);
        yield return new WaitForSecondsRealtime(t.Style.CameraMoveSeconds+.08f);
        Assert.That(Vector2.Distance(t.CameraPosition,camera),Is.GreaterThan(1));
        t.PointerClick(b,true,true); Assert.That(t.HasGoal,Is.True);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(Vector2.Distance(t.PlayerPosition,player),Is.LessThan(.02f)); Assert.That(clock.RemainingSeconds,Is.LessThan(time));
        for(int zoom=0;zoom<24;zoom++)t.Zoom(-1);
        yield return new WaitForSecondsRealtime(t.Style.CameraZoomSeconds+.08f);
        var mapRect=t.CurrentCameraRect;
        Assert.That(mapRect.xMin,Is.GreaterThanOrEqualTo(t.WorldBounds.xMin-.02f));
        Assert.That(mapRect.xMax,Is.LessThanOrEqualTo(t.WorldBounds.xMax+.02f));
        Assert.That(mapRect.yMin,Is.GreaterThanOrEqualTo(t.WorldBounds.yMin-.02f));
        Assert.That(mapRect.yMax,Is.LessThanOrEqualTo(t.WorldBounds.yMax+.02f));
        Assert.That(t.View.WorldClip.GetComponent<RectMask2D>(),Is.Not.Null);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
        yield return new WaitForSecondsRealtime(.05f);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
        yield return new WaitForSecondsRealtime(.05f);
        yield return FinishTransition(t);
        Assert.That(t.IsOpen,Is.False); Assert.That(Object.FindAnyObjectByType<AppRoot>().Screens.Top,Is.Null,"전술 닫기의 Esc는 메뉴 열기로 다시 처리하지 않습니다.");
        Assert.That(t.OutputCamera.orthographicSize,Is.EqualTo(original)); Assert.That(control.CanMove&&control.CanAct,Is.True,$"move={control.CanMove}, act={control.CanAct}, tactical={control.IsInTacticalView}, blocked={control.IsBlocked}, frame={Time.frameCount}");
        Assert.That(t.OutputCamera.rect,Is.EqualTo(new Rect(0,0,1,1)));
        Assert.That(t.View.Goal.gameObject.activeInHierarchy,Is.True);
        Assert.That(hud.IsTurretPanelOpen,Is.False);
        Assert.That(status.TryOpen(),Is.True);
        Assert.That(t.Open(),Is.False,"상태창을 확인 중에는 전술 화면을 겹쳐 열지 않습니다.");
        status.Close();
        }
        finally { if(keyboard.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); }
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator RightDragSettingsAndFieldWaypointPreserveWorldAndRendererState()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();yield return EnterFieldThroughTheSpaceshipExit();
        var t=Object.FindAnyObjectByType<InitialStagePlayerHUD>().Tactical;
        t.Source.LocalPlayer.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Assert.That(t.Open(),Is.True);yield return FinishTransition(t);yield return null;
        var player=t.PlayerPosition;var original=t.SceneryRenderers.ToDictionary(r=>r,r=>r.forceRenderingOff);
        Assert.That(original.Count,Is.EqualTo(1));Assert.That(original.Keys.First().name,Is.EqualTo("Environment_Obj"));
        var physics=original.Keys.First().GetComponent<Collider2D>();bool collider=physics!=null && physics.enabled;
        bool ranges=t.ShowAllRanges,scenery=t.ShowScenery;
        try
        {
            t.View.MapSettings.onClick.Invoke();Assert.That(t.View.SettingsDrawer.gameObject.activeSelf,Is.True);
            t.SetMapVisibility(false,false,false);
            Assert.That(t.ShowAllRanges||t.ShowScenery,Is.False);
            Assert.That(original.Keys.All(r=>r.forceRenderingOff),Is.True);
            Assert.That(physics!=null && physics.enabled,Is.EqualTo(collider));
            t.View.MapSettings.onClick.Invoke();
            var start=(Vector2)t.OutputCamera.pixelRect.center;var delta=new Vector2(100,-50);var before=t.CameraPosition;
            var e=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {button=UnityEngine.EventSystems.PointerEventData.InputButton.Right,position=start,pressPosition=start};
            var target=t.View.WorldGraphic.gameObject;
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
            e.position=start+delta;e.delta=delta;
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.dragHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(t.Style.CameraMoveSeconds+.15f);
            Assert.That(t.HasGoal,Is.False,"우클릭 드래그가 핑으로도 처리되지 않습니다.");
            Assert.That(Vector2.Distance(t.CameraPosition,before),Is.GreaterThan(.5f));
            Assert.That(Vector2.Distance(t.PlayerPosition,player),Is.LessThan(.01f));
            t.SetGoal(t.WorldBounds.max);t.Close();yield return FinishTransition(t);
            float goalDeadline=Time.realtimeSinceStartup+1;
            while(!t.View.Goal.gameObject.activeSelf && Time.realtimeSinceStartup<goalDeadline)yield return null;
            Assert.That(t.HasGoal,Is.True,$"field={t.Source.HasFieldActor}, goal={t.GoalPosition}, player={t.PlayerPosition}");
            Assert.That(t.View.Goal.gameObject.activeSelf,Is.True,$"field={t.Source.HasFieldActor}, open={t.IsOpen}");
            Assert.That(t.View.GoalPin.enabled||t.View.GoalArrow.enabled,Is.True);
            Assert.That(original.All(pair=>pair.Key.forceRenderingOff==pair.Value),Is.True,"필드 복귀 시 원래 장식 렌더 상태로 복원합니다.");
            Assert.That(t.Open(),Is.True);yield return FinishTransition(t);t.ClearGoal();Assert.That(t.HasGoal,Is.False);
        }
        finally{t.SetMapVisibility(ranges,scenery,false);t.Close();}
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator ActualButtonsUseTypedCommandsHostStateAndOneSubscriptionAfterReenable()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return EnterFieldThroughTheSpaceshipExit();
        var app = Object.FindAnyObjectByType<AppRoot>();
        var network = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        var hud = Object.FindAnyObjectByType<InitialStagePlayerHUD>();
        var player = Object.FindAnyObjectByType<PlayerInfo>();
        var controller = Object.FindAnyObjectByType<FieldTurretController>();
        var entities = Object.FindAnyObjectByType<TurretMatchBinding>();
        var regions = Object.FindAnyObjectByType<RegionSceneBinding>();
        var tactical = hud.Tactical;
        var view = tactical.View;
        player.gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Object.FindAnyObjectByType<ControlUnitHealth>().gameObject.AddComponent<TeamHJD.Game.Presentation.Scene.DebugDamageProtection>();
        Assert.That(tactical.Open(), Is.True);yield return FinishTransition(tactical);
        Assert.That(player.GetComponent<PlayerControlState>().CanAct, Is.False);
        Assert.That(hud.HasTurretCommandBinding, Is.True);
        hud.BindTurretCommands(hud.BoundMatchId, hud.BoundPlayerId, network.TurretCommands, network.TurretPresentation);
        Assert.That(network.TurretPresentation.Snapshot, Is.Not.Null);
        Assert.That(regions.ResetPolicy(RegionPolicyMode.Static, true), Is.True);
        FieldTurretBase turret = FindInactive(controller, default);
        CaptureCellForMap(regions,turret);
        Assert.That(turret.TryGetEntityId(out EntityId firstId), Is.True);
        player.transform.position = turret.transform.position;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(hud.TrySelectTurret(firstId), Is.True);
        yield return AssertActivationColor(view, tactical.Style.Active);
        Click(view.ToggleTurret);
        Assert.That(turret.IsActivated, Is.True);
        Assert.That(network.TurretPresentation.Snapshot.TryGet(firstId, out var state), Is.True); Assert.That(state.IsActivated, Is.True);
        Assert.That(view.Restriction.text, Is.Not.EqualTo("적용했습니다."), "확정 결과는 공통 Alert로 전달합니다.");
        yield return new WaitForSecondsRealtime(.05f);
        var alert = Object.FindAnyObjectByType<TeamHJD.Game.Presentation.AlertStripView>();
        Assert.That(alert, Is.Not.Null);
        Assert.That(alert.Message.text, Is.EqualTo("적용했습니다."));
        yield return AssertActivationColor(view, tactical.Style.Common.Health);
        Click(view.ToggleTurret); Assert.That(turret.IsActivated, Is.False); yield return new WaitForSecondsRealtime(.2f);

        turret = FindInactive(controller, firstId); Assert.That(turret.TryGetEntityId(out EntityId id), Is.True);
        CaptureCellForMap(regions,turret);
        player.transform.position = turret.transform.position;
        Assert.That(hud.TrySelectTurret(id), Is.True);
        yield return new WaitForSecondsRealtime(.2f);
        int level = turret.Definition.Level;
        Click(view.Upgrade); Assert.That(view.UpgradeDrawer.gameObject.activeInHierarchy, Is.True);
        Click(view.LevelUp); yield return new WaitForSecondsRealtime(.2f); turret = Resolve(entities, id);
        Assert.That(turret.Definition.Level, Is.EqualTo(level + 1));
        Assert.That(network.TurretPresentation.Snapshot.TryGet(id, out state), Is.True); Assert.That(state.Level, Is.EqualTo(level + 1));
        Click(view.LevelDown); yield return new WaitForSecondsRealtime(.2f); turret = Resolve(entities, id); Assert.That(turret.Definition.Level, Is.EqualTo(level));
        Click(view.LowPlus); yield return new WaitForSecondsRealtime(.2f); Assert.That(turret.RuntimeState.UpgradeLevel, Is.EqualTo(1));
        Click(view.LowMinus); yield return new WaitForSecondsRealtime(.2f); Assert.That(turret.RuntimeState.UpgradeLevel, Is.Zero);

        var controls = player.GetComponent<PlayerControlState>(); controls.SetInputBlocked(true);
        long sequence = network.TurretPresentation.Snapshot.ConfirmedCommandSequence;
        view.ToggleTurret.onClick.Invoke(); controls.SetInputBlocked(false);
        Assert.That(network.TurretPresentation.Snapshot.ConfirmedCommandSequence, Is.EqualTo(sequence));
        Assert.That(turret.IsActivated, Is.False);
        hud.gameObject.SetActive(false); hud.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(hud.HasTurretCommandBinding, Is.True); Assert.That(tactical.Open(), Is.True);yield return FinishTransition(tactical); Assert.That(hud.TrySelectTurret(id), Is.True);
        sequence = network.TurretPresentation.Snapshot.ConfirmedCommandSequence;
        Click(view.ToggleTurret); Assert.That(turret.IsActivated, Is.True);
        Assert.That(network.TurretPresentation.Snapshot.ConfirmedCommandSequence, Is.EqualTo(sequence + 1));
        var controlUnit = Object.FindAnyObjectByType<ControlUnitHealth>();
        controlUnit.enabled = false;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(network.TurretPresentation.Snapshot.Controller.IsControlUnitAvailable, Is.False);
        controlUnit.enabled = true;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(network.TurretPresentation.Snapshot.Controller.IsControlUnitAvailable, Is.True);
        app.EndCurrentMatch(); Assert.That(hud.HasTurretCommandBinding, Is.False);
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator CombatMenuKeepsWorldTimeAndOtherLeasesAndShowsTheFinalHostAutoReply()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Stage_1.unity");
        yield return new EnterPlayMode();
        yield return ExerciseCombatMenu();
    }

    private static IEnumerator ExerciseCombatMenu()
    {
        var app = Object.FindAnyObjectByType<AppRoot>();
        var hud = Object.FindAnyObjectByType<InitialStagePlayerHUD>();
        var vitals = Object.FindAnyObjectByType<StagePlayerVitalsView>();
        var debug = Object.FindAnyObjectByType<CombatHudDebugPanel>();
        var network = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        float deadline = Time.realtimeSinceStartup + 3;
        while ((!hud.HasLocalActor || network.TurretPresentation.Snapshot?.Controller?.IsControlUnitAvailable != true)
            && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(hud.HasLocalActor, Is.True);
        var controlUnit = network.TurretPresentation.Snapshot.Controller;
        Assert.That(controlUnit.IsControlUnitAvailable, Is.True);
        // 직접 Stage 진입 시 시작 문에 닿아 우주선 방문이 먼저 진행될 수 있습니다.
        yield return new WaitForSecondsRealtime(.5f);
        deadline = Time.realtimeSinceStartup + 3;
        while (app.LocalInput.IsBlocked && Time.realtimeSinceStartup < deadline) yield return null;
        var skillHud = Object.FindAnyObjectByType<PlayerSkillHud>();
        var presence = vitals.Player.GetComponent<TeamHJD.Game.Presentation.Scene.PlayerMatchPresence>();
        if (presence.Location == MatchPlayerLocation.Spaceship)
        {
            Assert.That(skillHud.IsFieldVisible, Is.False);
            Assert.That(skillHud.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(skillHud.Owner, Is.Not.Null, "Visibility must not detach the local actor.");
            foreach (var group in hud.GetComponentsInChildren<CombatVitalsGroup>(true)) Assert.That(group.Group.alpha, Is.Zero);
            Assert.That(debug.Panel.alpha + debug.Toast.alpha, Is.Zero);
            var exit = Object.FindAnyObjectByType<SpaceshipExit>();
            var body = vitals.Player.GetComponentsInChildren<Collider2D>().First(c => c.enabled && c.gameObject.activeInHierarchy);
            var travel = exit.ExitAsync(body);
            deadline = Time.realtimeSinceStartup + 3;
            while (!travel.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(travel.IsCompleted, Is.True);
            travel.GetAwaiter().GetResult();
            yield return null;
        }
        Assert.That(presence.Location, Is.EqualTo(MatchPlayerLocation.Field));
        Assert.That(skillHud.IsFieldVisible && skillHud.GetComponent<Canvas>().enabled, Is.True);
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        System.IDisposable otherLease = null;
        string feedback = null;
        System.Action<string> received = message => feedback = message;
        hud.AutoActivationFeedback += received;
        try
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
                { position = new Vector2(Screen.width * .5f, Screen.height * .5f) });
            UnityEngine.InputSystem.InputSystem.Update();
            // 정식 Match의 CU는 로컬 호환 참조가 아니라 Host 확정 presentation에서 읽습니다.
            vitals.Player.transform.position = new Vector3((float)controlUnit.ControlUnitPosition.X + 5, (float)controlUnit.ControlUnitPosition.Y);
            var clock = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IMatchDayNightCycle>().First();
            var menu = app.Screens.ShowMenu();
            Assert.That(menu, Is.Not.Null);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(debug.Panel.alpha, Is.EqualTo(1).Within(.001f));
            double time = clock.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(clock.RemainingSeconds, Is.LessThan(time), "The personal menu must not pause shared world time.");

            long sequence = network.TurretPresentation.Snapshot.ConfirmedCommandSequence;
            otherLease = app.LocalInput.Acquire("HUD v6 unrelated test lease");
            debug.Auto.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(app.Screens.Top, Is.Null);
            Assert.That(app.LocalInput.IsBlocked, Is.True, "Closing this menu must preserve another owner's lease.");
            Assert.That(network.TurretPresentation.Snapshot.ConfirmedCommandSequence, Is.EqualTo(sequence));
            Assert.That(feedback, Does.Contain("현재는 요청할 수 없습니다"));
            otherLease.Dispose(); otherLease = null;
            yield return null;

            Assert.That(app.Screens.ShowMenu(), Is.Not.Null);
            yield return new WaitForSecondsRealtime(.35f);
            feedback = null;
            debug.Auto.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.Screens.Top, Is.Null);
            Assert.That(app.LocalInput.IsBlocked, Is.False);
            Assert.That(feedback == "자동 활성화가 시작되었습니다." || feedback == "자동 활성화 요청이 거절되었습니다.",
                Is.True, "The synchronous Host reply must survive the submit handler: " + feedback);
            Assert.That(debug.ToastLabel.text, Is.Empty, "공통 Alert와 별도 디버그 토스트를 중복 표시하지 않습니다.");
            var previousSelection = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            app.Screens.ShowMenu();
            var menuView = Object.FindObjectsByType<TeamHJD.Game.Presentation.AppPanelView>(FindObjectsSortMode.None).First(v => v.name == "Screen:Menu");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(menuView.Settings.gameObject);
            app.Screens.ShowSettings();
            // TestRunner can resume before this frame's LateUpdate, which projects the screen stack.
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(debug.Panel.alpha + debug.Toast.alpha, Is.Zero, "Settings covers both debug controls and their toast.");
            Assert.That(app.Screens.Back(), Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(menuView.Settings.gameObject), "Back restores the selected menu action.");
            Assert.That(app.Screens.Back(), Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(previousSelection), "A covered menu must retain its original external selection.");
        }
        finally
        {
            hud.AutoActivationFeedback -= received;
            otherLease?.Dispose();
            if (mouse.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
        }
    }

    private static IEnumerator FinishTransition(TacticalCommandController t)
    {
        float deadline=Time.realtimeSinceStartup+2;
        while(t.IsTransitioning && Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That(t.IsTransitioning,Is.False,"전체 막과 중앙 파동 뒤 UI 전환이 완료되어야 합니다.");
        // 닫기를 확정한 프레임의 입력 누출 방지까지 지난 정상 Player 업데이트를 기다립니다.
        yield return new WaitForSecondsRealtime(.06f);
    }

    private static void CaptureCellForMap(RegionSceneBinding regions,FieldTurretBase turret)
    {
        var snapshot=regions.Source.Snapshot;var p=turret.transform.position;
        Assert.That(snapshot.Definition.TryGetCellIndex(new BattlefieldPoint(p.x,p.y),out int cell),Is.True);
        // 점유 완료 뒤의 명령 경로를 검사하는 명시적 Host 복원 fixture입니다.
        regions.RestoreCheckpoint(snapshot.Definition,RegionPolicyMode.Static,
            snapshot.CapturedCells.Select((c,i)=>(c,i)).Where(x=>x.c).Select(x=>x.i).Append(cell).Distinct().ToArray(),
            snapshot.Definition.RegionIds.Where(x=>x>0).Distinct().ToArray());
    }

    private static IEnumerator EnterFieldThroughTheSpaceshipExit()
    {
        var vitals = Object.FindAnyObjectByType<StagePlayerVitalsView>();
        float deadline = Time.realtimeSinceStartup + 4;
        while ((vitals.Player == null || Object.FindAnyObjectByType<SpaceshipExit>() == null) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(vitals.Player, Is.Not.Null);
        yield return new WaitForSecondsRealtime(.5f);
        var app = Object.FindAnyObjectByType<AppRoot>();
        deadline = Time.realtimeSinceStartup + 4;
        while (app.LocalInput.IsBlocked && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(app.LocalInput.IsBlocked, Is.False, "Finish the automatic entrance fade before requesting the normal exit.");
        var presence = vitals.Player.GetComponent<TeamHJD.Game.Presentation.Scene.PlayerMatchPresence>();
        if (presence.Location == MatchPlayerLocation.Spaceship)
        {
            var exit = Object.FindAnyObjectByType<SpaceshipExit>();
            var body = vitals.Player.GetComponentsInChildren<Collider2D>().First(c => c.enabled && c.gameObject.activeInHierarchy);
            var travel = exit.ExitAsync(body);
            deadline = Time.realtimeSinceStartup + 4;
            while (!travel.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(travel.IsCompleted, Is.True); travel.GetAwaiter().GetResult();
            yield return null;
        }
        Assert.That(presence.Location, Is.EqualTo(MatchPlayerLocation.Field));
        // 앞선 검사/에디터의 포인터가 하단 스킬 열기 영역에 남으면 준비 UI가 정상적으로 조작을 막습니다.
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null) UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.position, new Vector2(Screen.width * .5f, Screen.height * .8f));
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(Object.FindAnyObjectByType<PlayerSkillHud>().IsOpen, Is.False);
    }
    private static FieldTurretBase FindInactive(FieldTurretController controller, EntityId exclude)
    {
        foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.GetAll())
            if (controller.OwnsTurret(turret) && !turret.IsDestroyed && !turret.IsLocked && !turret.IsActivated && !turret.IsCoolingDown && turret.TryGetEntityId(out var id) && id != exclude && turret.Definition.Level == 1)
                return turret;
        Assert.Fail("No inactive level-one test turret."); return null;
    }
    private static FieldTurretBase Resolve(TurretMatchBinding entities, EntityId id)
    {
        Assert.That(entities.TryResolveLocalId(id, out int localId), Is.True);
        Assert.That(FieldTurretInstanceRegistry.TryGet(localId, out var turret), Is.True); return turret;
    }
    private static Button ButtonOf(Object view, string property) => (Button)new SerializedObject(view).FindProperty(property).objectReferenceValue;
    private static IEnumerator AssertActivationColor(TacticalHudView view, Color expected)
    {
        var label = view.ToggleLabel;
        var feedback = view.ToggleTurret.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>();
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        feedback.OnPointerEnter(pointer);
        for (int i = 0; i < 8; i++) { yield return null; Assert.That(label.color, Is.EqualTo(expected), "Hover must preserve the activation state's text color."); }
        feedback.OnPointerExit(pointer);
        for (int i = 0; i < 4; i++) { yield return null; Assert.That(label.color, Is.EqualTo(expected)); }
    }
    private static void Click(Button button)
    { Assert.That(button.interactable, Is.True, button.name); button.onClick.Invoke(); }
    private static string Text(Object view, string property) => ((TMP_Text)new SerializedObject(view).FindProperty(property).objectReferenceValue).text;
}
