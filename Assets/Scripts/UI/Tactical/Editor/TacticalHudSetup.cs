using System;
using System.Linq;
using TeamHJD.Game.Content;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>전술 UI의 편집 가능한 원본을 작성합니다. 실행 중에는 이 프리팹만 복제합니다.</summary>
public static class TacticalHudSetup
{
    public const string ViewPath = "Assets/Prefabs/UI/Tactical/TacticalHud.prefab";
    public const string StylePath = "Assets/Resources/Core/TacticalHudStyle.asset";
    private const string HudPath = "Assets/Prefabs/UI/InitialStagePlayerHUD.prefab";
    private static CombatHudStyle _common;
    [MenuItem("Tools/TeamHJD/The Developer/Tactical/Bind active scene camera bounds")]
    public static void BindActiveSceneCameraBounds()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Bind camera markers in Edit Mode.");
        var roots = scene.GetRootGameObjects();
        var left = roots.Single(g => g.name == "LeftTop").transform;
        var right = roots.Single(g => g.name == "RightBottom").transform;
        var controller = roots.SelectMany(g => g.GetComponentsInChildren<TacticalCommandController>(true)).Single();
        if (!TacticalMapMath.TryBounds(left.position, right.position, out _))
            throw new InvalidOperationException("LeftTop/RightBottom must form a finite positive camera area.");
        Undo.RecordObject(controller, "Bind tactical camera markers");
        controller.CameraLeftTop = left; controller.CameraRightBottom = right;
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        EditorUtility.SetDirty(controller); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        // Scene 저장은 호출자가 diff를 확인한 뒤 수행합니다. HUD/맵/다른 Prefab은 다시 만들지 않습니다.
    }

    [MenuItem("Tools/TeamHJD/The Developer/Configure Tactical HUD")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring UI.");
        _common = AssetDatabase.LoadAssetAtPath<CombatHudStyle>("Assets/Art/UI/HudV6/CombatHudStyle.asset");
        if (_common == null) throw new InvalidOperationException("Combat HUD style is missing.");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI/Tactical")) AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Tactical");
        const string labelPath="Assets/Prefabs/UI/Tactical/TacticalWorldLabel.mat";
        var worldLabel=AssetDatabase.LoadAssetAtPath<Material>(labelPath);
        if(worldLabel==null) { worldLabel=new Material(_common.LabelFont.material){name="TacticalWorldLabel"}; AssetDatabase.CreateAsset(worldLabel,labelPath); }
        worldLabel.shader=_common.LabelFont.material.shader; worldLabel.CopyPropertiesFromMaterial(_common.LabelFont.material);
        worldLabel.SetFloat("_OutlineWidth",.12f); worldLabel.SetColor("_OutlineColor",new Color(.025f,.035f,.03f,1));
        EditorUtility.SetDirty(worldLabel); AssetDatabase.SaveAssetIfDirty(worldLabel);
        var style = AssetDatabase.LoadAssetAtPath<TacticalHudStyle>(StylePath);
        if (style == null) { style = ScriptableObject.CreateInstance<TacticalHudStyle>(); AssetDatabase.CreateAsset(style, StylePath); }
        style.Common = _common;
        var root = Rect("Tactical HUD", null, Vector2.zero, Vector2.zero, new Vector2(.5f,.5f));
        try
        {
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 600;
            var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var v = root.gameObject.AddComponent<TacticalHudView>(); v.Canvas = canvas;
            var field = Stretch("Field hover marker", root); var fg = field.gameObject.AddComponent<TacticalMapGraphic>(); fg.View = v; fg.Field = true; fg.raycastTarget = false;
            var command = Stretch("Command view", root); v.CommandRoot = command;
            v.CommandGroup = command.gameObject.AddComponent<CanvasGroup>();
            var matte = Stretch("Map margins",command); var matteCanvas=matte.gameObject.AddComponent<Canvas>(); matteCanvas.overrideSorting=true; matteCanvas.sortingOrder=-1000;
            v.ViewportMatte=new RectTransform[4];
            for(int i=0;i<4;i++) { var pane=Rect("Margin "+i,matte,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f)); Img(pane,null,new Color(.055f,.07f,.065f,1)); v.ViewportMatte[i]=pane; }
            v.WorldClip=Rect("Map viewport",command,new Vector2(1920,1080),Vector2.zero,new Vector2(.5f,.5f)); v.WorldClip.gameObject.AddComponent<RectMask2D>();
            var world = Stretch("World commands", v.WorldClip); v.WorldGraphic = world.gameObject.AddComponent<TacticalMapGraphic>(); v.WorldGraphic.View = v;
            var wp = world.gameObject.AddComponent<TacticalMapPointer>(); wp.View = v;
            v.TurretGlyphs=new Image[64];
            for(int i=0;i<v.TurretGlyphs.Length;i++)
            { var glyph=Img(Rect("Turret glyph "+i,v.WorldClip,Vector2.one*style.GlyphSize,Vector2.zero,new Vector2(.5f,.5f)),null,_common.Text); glyph.preserveAspect=true; glyph.gameObject.SetActive(false); v.TurretGlyphs[i]=glyph; }
            v.SelectionBox = Rect("Selection", command, new Vector2(1,1), Vector2.zero, new Vector2(.5f,.5f));
            Img(v.SelectionBox, "panel_edge_hover", new Color(1,1,1,.7f));
            v.PlayerMarker = Img(Rect("Player", v.WorldClip, new Vector2(24,24), Vector2.zero, new Vector2(.5f,.5f)), "owner_player", _common.Text);
            v.ControlMarker = Img(Rect("Control unit",v.WorldClip,new Vector2(24,24),Vector2.zero,new Vector2(.5f,.5f)),"owner_cu",_common.Power);
            v.EnemyLabels = new TMP_Text[24];
            v.EnemyIcons=new Image[24];
            for (int i=0; i<v.EnemyLabels.Length; i++)
            {
                v.EnemyLabels[i] = Text("Enemy count " + i, v.WorldClip, Vector2.zero, new Vector2(40,26), 16, new Vector2(.5f,.5f));
                v.EnemyLabels[i].font=_common.LabelFont; v.EnemyLabels[i].fontSharedMaterial=worldLabel; v.EnemyLabels[i].color=new Color(.94f,.76f,.65f);
                var icon=Img(Rect("Enemy glyph "+i,v.WorldClip,new Vector2(20,20),Vector2.zero,new Vector2(.5f,.5f)),null,new Color(.94f,.76f,.65f)); icon.sprite=style.EnemyGlyph; icon.preserveAspect=true; v.EnemyIcons[i]=icon;
            }
            v.Incidents = new Button[3]; v.IncidentLabels = new TMP_Text[3];
            for (int i=0;i<3;i++) { v.Incidents[i] = Button("Incident " + i, command, new Vector2(24,-140-i*39), new Vector2(240,33), "", out var label, new Vector2(0,1)); v.IncidentLabels[i] = label; }

            v.Dock = Panel("Facility dock", command, new Vector2(24,24), new Vector2(576,100), Vector2.zero, "weapon_surface");
            v.TurretIcon = Img(Rect("Facility sprite",v.Dock,new Vector2(76,76),new Vector2(12,-12),new Vector2(0,1)),null,Color.white); v.TurretIcon.preserveAspect=true;
            v.Title = Text("Facility",v.Dock,new Vector2(104,-12),new Vector2(256,28),20,new Vector2(0,1));
            v.Stats = Text("State",v.Dock,new Vector2(104,-49),new Vector2(256,38),16,new Vector2(0,1));
            v.ToggleTurret = Button("Toggle",v.Dock,new Vector2(-12,-12),new Vector2(94,32),"켜기",out var toggle,new Vector2(1,1)); v.ToggleLabel=toggle;
            v.ToggleTurret.GetComponent<UiButtonFeedback>().PreserveLabelColor = true;
            v.Upgrade = Button("Upgrade",v.Dock,new Vector2(-12,-54),new Vector2(94,32),"강화",out _,new Vector2(1,1));
            ButtonIcon(v.ToggleTurret,style.PowerGlyph); ButtonIcon(v.Upgrade,style.UpgradeGlyph);
            v.Restriction = Text("Restriction",v.Dock,new Vector2(0,108),new Vector2(576,24),15,new Vector2(0,0)); v.Restriction.color = _common.Power;
            v.UpgradeDrawer = Panel("Upgrade drawer",command,new Vector2(24,152),new Vector2(576,94),Vector2.zero);
            v.LowLabel=Text("Low power",v.UpgradeDrawer,new Vector2(16,-12),new Vector2(138,26),16,new Vector2(0,1));
            v.FireLabel=Text("Firepower",v.UpgradeDrawer,new Vector2(16,-53),new Vector2(138,26),16,new Vector2(0,1));
            v.LowMinus=Button("Low minus",v.UpgradeDrawer,new Vector2(162,-10),new Vector2(34,30),"−",out _,new Vector2(0,1));
            v.LowPlus=Button("Low plus",v.UpgradeDrawer,new Vector2(206,-10),new Vector2(34,30),"+",out _,new Vector2(0,1));
            v.FireMinus=Button("Fire minus",v.UpgradeDrawer,new Vector2(162,-51),new Vector2(34,30),"−",out _,new Vector2(0,1));
            v.FirePlus=Button("Fire plus",v.UpgradeDrawer,new Vector2(206,-51),new Vector2(34,30),"+",out _,new Vector2(0,1));
            v.LevelUp=Button("Level up",v.UpgradeDrawer,new Vector2(-16,-10),new Vector2(140,30),"레벨 상승",out _,new Vector2(1,1));
            v.LevelDown=Button("Level down",v.UpgradeDrawer,new Vector2(-16,-51),new Vector2(140,30),"레벨 하강",out _,new Vector2(1,1));

            var nav = Panel("Navigator",command,new Vector2(-98,24),new Vector2(290,184),new Vector2(1,0));
            v.Overview=Rect("Overview",nav,new Vector2(270,164),new Vector2(10,-10),new Vector2(0,1)); v.Overview.pivot=new Vector2(.5f,.5f); v.Overview.anchoredPosition=new Vector2(145,-92);
            v.Overview.gameObject.AddComponent<RectMask2D>();
            Img(v.Overview,null,new Color(.045f,.055f,.05f,.95f)).raycastTarget=true;
            var terrain=Rect("Authored terrain",v.Overview,new Vector2(270,164),Vector2.zero,new Vector2(.5f,.5f)); v.OverviewTerrain=terrain.gameObject.AddComponent<RawImage>(); v.OverviewTerrain.raycastTarget=false; v.OverviewTerrain.color=new Color(.75f,.78f,.73f,.85f);
            var raster=Rect("Captured cells",v.Overview,new Vector2(270,164),Vector2.zero,new Vector2(.5f,.5f)); v.OverviewFill=raster.gameObject.AddComponent<RawImage>(); v.OverviewFill.raycastTarget=false; v.OverviewFill.color=new Color(1,1,1,.4f);
            var map=Stretch("Entities and viewport",v.Overview); v.OverviewGraphic=map.gameObject.AddComponent<TacticalMapGraphic>(); v.OverviewGraphic.View=v; v.OverviewGraphic.Overview=true; v.OverviewGraphic.raycastTarget=false;
            var pointer=v.Overview.gameObject.AddComponent<TacticalMapPointer>(); pointer.View=v; pointer.IsOverview=true;
            v.FocusPlayer=Button("Focus player",command,new Vector2(-292,216),new Vector2(96,32),"플레이어",out _,new Vector2(1,0));
            v.FocusCU=Button("Focus CU",command,new Vector2(-190,216),new Vector2(88,32),"CU",out _,new Vector2(1,0));
            v.Back=Button("Previous view",command,new Vector2(-98,216),new Vector2(84,32),"이전",out _,new Vector2(1,0));
            v.AutoActivate=Button("Auto activation",command,new Vector2(-98,268),new Vector2(190,34),"자동 활성화",out _,new Vector2(1,0));
            v.BatchActivate=Button("Batch activation",command,new Vector2(-420,268),new Vector2(116,34),"선택 켜기",out _,new Vector2(1,0));
            v.BatchDeactivate=Button("Batch deactivation",command,new Vector2(-296,268),new Vector2(116,34),"선택 끄기",out _,new Vector2(1,0));
            v.SelectMultiple=Button("Multiple selection",command,new Vector2(-544,268),new Vector2(130,34),"다중 선택",out var selection,new Vector2(1,0));
            v.SelectionLabel=selection; v.SelectMultiple.GetComponent<UiButtonFeedback>().PreserveLabelColor=true;
            ButtonIcon(v.AutoActivate,style.AutomaticGlyph); ButtonIcon(v.BatchActivate,style.PowerGlyph); ButtonIcon(v.BatchDeactivate,style.PowerGlyph); ButtonIcon(v.SelectMultiple,style.SelectionGlyph);
            v.Close=Button("Close command",command,new Vector2(24,-92),new Vector2(156,34),"전술 닫기  M",out _,new Vector2(0,1));
            v.ToggleMap=Button("Open command",root,new Vector2(24,-92),new Vector2(118,34),"전술  M",out _,new Vector2(0,1));
            v.Tooltip=Panel("Turret hover",root,Vector2.zero,new Vector2(320,88),new Vector2(.5f,.5f));
            v.TooltipLabel=Text("Information",v.Tooltip,new Vector2(80,-10),new Vector2(226,66),17,new Vector2(0,1));
            v.TooltipIcon=Img(Rect("Icon",v.Tooltip,new Vector2(54,64),new Vector2(14,-12),new Vector2(0,1)),null,Color.white); v.TooltipIcon.preserveAspect=true;
            v.Prompt=Panel("Quick interaction",root,Vector2.zero,new Vector2(68,32),new Vector2(.5f,.5f));
            v.PromptLabel=Text("Key",v.Prompt,new Vector2(-16,0),new Vector2(28,30),18,new Vector2(.5f,.5f)); v.PromptLabel.font=_common.LabelFont; v.PromptLabel.alignment=TextAlignmentOptions.Center;
            v.PromptAction=Img(Rect("Power action",v.Prompt,new Vector2(22,22),new Vector2(16,0),new Vector2(.5f,.5f)),null,_common.Text); v.PromptAction.sprite=style.PowerGlyph;
            v.Goal=Rect("Pinned goal",root,new Vector2(80,54),Vector2.zero,new Vector2(.5f,.5f));
            v.GoalArrow=Rect("Direction",v.Goal,new Vector2(14,18),new Vector2(0,10),new Vector2(.5f,.5f)).gameObject.AddComponent<TacticalGoalArrow>(); v.GoalArrow.color=_common.Accent; v.GoalArrow.raycastTarget=false;
            v.GoalPin=Img(Rect("Beacon",v.Goal,new Vector2(28,28),new Vector2(0,10),new Vector2(.5f,.5f)),null,_common.Accent); v.GoalPin.sprite=style.GoalGlyph; v.GoalPin.preserveAspect=true;
            v.GoalLabel=Text("Distance",v.Goal,new Vector2(0,-15),new Vector2(80,24),16,new Vector2(.5f,.5f)); v.GoalLabel.alignment=TextAlignmentOptions.Center;
            v.SelectionBox.gameObject.SetActive(false); v.ShowCommand(false); v.Tooltip.gameObject.SetActive(false); v.Prompt.gameObject.SetActive(false); v.Goal.gameObject.SetActive(false); v.ToggleMap.gameObject.SetActive(false);
            var scale=root.gameObject.AddComponent<CombatHudScaleBinding>();
            scale.MinimumScreenScale=.8f;
            var parts=new System.Collections.Generic.List<CombatHudScaleBinding.Element>();
            void Scale(RectTransform rect,bool offset=true,float minimum=.85f) => parts.Add(new CombatHudScaleBinding.Element{Target=rect,ScaleOffset=offset,BaseOffset=rect.anchoredPosition,MinimumScale=minimum});
            Scale(v.Dock); Scale(v.UpgradeDrawer); Scale(nav); Scale(v.Tooltip,false); Scale(v.Prompt,false);
            foreach(var button in new[]{v.ToggleMap,v.Close,v.FocusPlayer,v.FocusCU,v.Back,v.AutoActivate,v.BatchActivate,v.BatchDeactivate,v.SelectMultiple}) Scale((RectTransform)button.transform);
            foreach(var button in v.Incidents) Scale((RectTransform)button.transform);
            scale.Elements=parts.ToArray();
            root.localScale=Vector3.one;
            style.ViewPrefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,ViewPath).GetComponent<TacticalHudView>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        var hudRoot=PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var hud=hudRoot.GetComponent<InitialStagePlayerHUD>();
            var old= hudRoot.GetComponentInChildren<StageTurretUpgradeView>(true);
            if(old!=null) { var so=new SerializedObject(old); style.LowPower=Definitions(so,"lowPowerUpgrades"); style.Firepower=Definitions(so,"highFirepowerUpgrades"); }
            var tactical=hudRoot.GetComponent<TacticalCommandController>() ?? hudRoot.AddComponent<TacticalCommandController>();
            tactical.Source=hud; tactical.Style=style; hud.Tactical=tactical;
            var hudSo=new SerializedObject(hud); var oldView=hudSo.FindProperty("turretView").objectReferenceValue as Component;
            hudSo.FindProperty("turretView").objectReferenceValue=null; hudSo.ApplyModifiedPropertiesWithoutUndo();
            if(oldView!=null) UnityEngine.Object.DestroyImmediate(oldView.gameObject);
            var cu=hudRoot.GetComponentInChildren<StageControlUnitView>(true); if(cu!=null) cu.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(hudRoot,HudPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(hudRoot); }
        EditorUtility.SetDirty(style); AssetDatabase.SaveAssetIfDirty(style);
        Debug.Log("TACTICAL_HUD_AUTHORED");
    }
    private static FieldTurretUpgradeDefinition[] Definitions(SerializedObject so,string name)
    { var p=so.FindProperty(name); return Enumerable.Range(0,p.arraySize).Select(i=>p.GetArrayElementAtIndex(i).objectReferenceValue as FieldTurretUpgradeDefinition).ToArray(); }
    private static void ButtonIcon(Button button,Sprite sprite)
    {
        var r=(RectTransform)button.transform;
        var image=Img(Rect("Command glyph",r,new Vector2(20,20),new Vector2(-r.sizeDelta.x*.5f+17,0),new Vector2(.5f,.5f)),null,_common.Text);
        image.sprite=sprite; image.preserveAspect=true;
        var feedback=button.GetComponent<UiButtonFeedback>(); feedback.Icon=image;
        var label=feedback.Label; label.rectTransform.sizeDelta=new Vector2(r.sizeDelta.x-30,r.sizeDelta.y); label.rectTransform.anchoredPosition=new Vector2(12,0);
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos,Vector2 anchor)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.gameObject.layer=5;
        if(parent!=null)r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=anchor; r.sizeDelta=size; r.anchoredPosition=pos; return r;
    }
    private static RectTransform Stretch(string name,Transform parent)
    { var r=Rect(name,parent,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f)); r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; return r; }
    private static Image Img(RectTransform r,string sprite,Color color)
    { var i=r.gameObject.AddComponent<Image>(); i.sprite=sprite!=null?_common.Get(sprite):null; i.type=i.sprite!=null?Image.Type.Sliced:Image.Type.Simple; i.color=color; i.raycastTarget=false; return i; }
    private static RectTransform Panel(string name,Transform parent,Vector2 pos,Vector2 size,Vector2 anchor,string sprite="panel_surface")
    { var r=Rect(name,parent,size,pos,anchor); Img(r,sprite,Color.white); Img(Stretch("Frame",r),"panel_edge_idle",Color.white); return r; }
    private static TMP_Text Text(string name,Transform parent,Vector2 pos,Vector2 size,float font,Vector2 anchor)
    {
        var r=Rect(name,parent,size,pos,anchor); var t=r.gameObject.AddComponent<TextMeshProUGUI>(); t.font=_common.BodyFont;
        t.fontSize=font; t.color=_common.Text; t.text=""; t.raycastTarget=false; t.textWrappingMode=TextWrappingModes.NoWrap; t.overflowMode=TextOverflowModes.Ellipsis; t.verticalAlignment=VerticalAlignmentOptions.Middle; return t;
    }
    private static Button Button(string name,Transform parent,Vector2 pos,Vector2 size,string text,out TMP_Text label,Vector2 anchor)
    {
        var r=Rect(name,parent,size,pos,anchor); var surface=Img(r,"button_surface",Color.white); surface.raycastTarget=true;
        var edge=Img(Stretch("Edge",r),"button_edge_idle",Color.white);
        var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=surface; b.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=b.colors; colors.highlightedColor=Color.white; colors.pressedColor=new Color(.65f,.65f,.65f); colors.selectedColor=Color.white; colors.disabledColor=new Color(.4f,.4f,.4f,.6f); b.colors=colors;
        label=Text("Label",r,Vector2.zero,size,16,new Vector2(.5f,.5f)); label.text=text; label.alignment=TextAlignmentOptions.Center;
        var feedback=r.gameObject.AddComponent<UiButtonFeedback>(); feedback.Label=label; feedback.Edge=edge; feedback.IdleEdge=_common.Get("button_edge_idle"); feedback.HoverEdge=_common.Get("button_edge_hover"); feedback.Text=_common.Text; feedback.Accent=_common.Accent;
        return b;
    }
}
