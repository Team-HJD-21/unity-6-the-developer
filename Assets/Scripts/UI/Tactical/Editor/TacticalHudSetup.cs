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
    [MenuItem("Tools/TeamHJD/The Developer/Tactical/Bind active scene scenery")]
    public static void BindActiveSceneScenery()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Bind authored scenery in Edit Mode.");
        var all=scene.GetRootGameObjects();
        var controller=all.SelectMany(g=>g.GetComponentsInChildren<TacticalCommandController>(true)).Single();
        // Stage의 실제 장식 Tilemap을 authoring 때 한 번 명시 연결합니다. 지면/물/충돌/터렛은 바꾸지 않습니다.
        var scenery=all.SelectMany(g=>g.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>(true))
            .Where(r=>r.name=="Environment_Obj" && r.sortingLayerName=="Entities").Cast<Renderer>().ToArray();
        if(scenery.Length==0)throw new InvalidOperationException("Select the authored Environment_Obj scenery renderer for this Scene.");
        Undo.RecordObject(controller,"Bind tactical scenery");controller.SceneryRenderers=scenery;
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);EditorUtility.SetDirty(controller);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
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
        const string modePath="Assets/Art/UI/Tactical/TacticalModeFrame.mat";
        var modeMaterial=AssetDatabase.LoadAssetAtPath<Material>(modePath);
        var modeShader=Shader.Find("The Developer/UI/TacticalModeFrame");
        if(modeShader==null)throw new InvalidOperationException("Tactical frame shader is missing.");
        if(modeMaterial==null){modeMaterial=new Material(modeShader){name="TacticalModeFrame"};AssetDatabase.CreateAsset(modeMaterial,modePath);}
        modeMaterial.shader=modeShader;modeMaterial.SetColor("_Color",new Color(.63f,.75f,.71f,1));modeMaterial.SetFloat("_CellSize",48);modeMaterial.SetFloat("_Cover",0);modeMaterial.SetFloat("_Reveal",0);
        EditorUtility.SetDirty(modeMaterial);AssetDatabase.SaveAssetIfDirty(modeMaterial);
        const string territoryPath="Assets/Art/UI/Tactical/TacticalTerritory.mat";
        var territory=AssetDatabase.LoadAssetAtPath<Material>(territoryPath);
        var territoryShader=Shader.Find("The Developer/UI/TacticalTerritory");
        if(territoryShader==null)throw new InvalidOperationException("Territory shader is missing.");
        if(territory==null){territory=new Material(territoryShader){name="TacticalTerritory"};AssetDatabase.CreateAsset(territory,territoryPath);}
        territory.shader=territoryShader;EditorUtility.SetDirty(territory);AssetDatabase.SaveAssetIfDirty(territory);
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
            var mode=Stretch("Tactical mode frame",root);mode.SetSiblingIndex(1);
            var modeCanvas=mode.gameObject.AddComponent<Canvas>();modeCanvas.overrideSorting=true;modeCanvas.sortingOrder=-900;mode.gameObject.AddComponent<GraphicRaycaster>();
            var surface=Img(Stretch("Hex edge and transition",mode),null,Color.white);surface.material=modeMaterial;
            v.ModeEffect=mode.gameObject.AddComponent<TacticalModeEffect>();v.ModeEffect.Surface=surface;v.ModeEffect.Commands=v.CommandGroup;
            var matte = Stretch("Map margins",command); var matteCanvas=matte.gameObject.AddComponent<Canvas>(); matteCanvas.overrideSorting=true; matteCanvas.sortingOrder=-1000;
            v.ViewportMatte=new RectTransform[4];
            for(int i=0;i<4;i++) { var pane=Rect("Margin "+i,matte,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f)); Img(pane,null,new Color(.055f,.07f,.065f,1)); v.ViewportMatte[i]=pane; }
            v.WorldClip=Rect("Map viewport",command,new Vector2(1920,1080),Vector2.zero,new Vector2(.5f,.5f)); v.WorldClip.gameObject.AddComponent<RectMask2D>();
            var area=Rect("Continuous territory",v.WorldClip,new Vector2(1,1),Vector2.zero,new Vector2(.5f,.5f));
            v.TerritoryFill=area.gameObject.AddComponent<RawImage>();v.TerritoryFill.material=territory;v.TerritoryFill.raycastTarget=false;
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
            v.IncidentIcons=new Image[3];v.IncidentHealth=new TacticalHealthGraphic[3];v.IncidentStates=new TacticalSymbolGraphic[3];
            for (int i=0;i<3;i++)
            {
                var button=Button("Incident "+i,command,new Vector2(24,-108-i*66),new Vector2(104,58),"",out var label,new Vector2(0,1));
                v.Incidents[i]=button;v.IncidentLabels[i]=label;label.gameObject.SetActive(false);
                var icon=Img(Rect("Facility",button.transform,new Vector2(38,34),new Vector2(10,-5),new Vector2(0,1)),null,Color.white);icon.preserveAspect=true;v.IncidentIcons[i]=icon;
                v.IncidentStates[i]=Symbol("Threat",button.transform,new Vector2(26,26),new Vector2(-12,-9),new Vector2(1,1),TacticalSymbolGraphic.Symbol.Impact);
                v.IncidentHealth[i]=Health("Health",button.transform,new Vector2(84,6),new Vector2(10,-44));
            }

            v.Dock = Panel("Facility dock", command, new Vector2(24,24), new Vector2(576,118), Vector2.zero, "weapon_surface");
            v.Dock.GetComponent<Image>().raycastTarget=true;
            v.TurretIcon = Img(Rect("Facility sprite",v.Dock,new Vector2(82,86),new Vector2(8,-12),new Vector2(0,1)),null,Color.white); v.TurretIcon.preserveAspect=true;
            v.Title = Text("Facility",v.Dock,new Vector2(104,-10),new Vector2(256,28),18,new Vector2(0,1));
            v.Stats = Text("Legacy state",v.Dock,Vector2.zero,Vector2.zero,16,new Vector2(0,1));v.Stats.gameObject.SetActive(false);
            v.DockHealth=Health("Health",v.Dock,new Vector2(254,11),new Vector2(104,-47));
            var hpIcon=Img(Rect("Health glyph",v.Dock,new Vector2(18,18),new Vector2(78,-45),new Vector2(0,1)),"stat_health",_common.Text);
            v.DockState=Symbol("Facility state",v.Dock,new Vector2(26,26),new Vector2(104,-75),new Vector2(0,1),TacticalSymbolGraphic.Symbol.Off);
            v.DockState.raycastTarget=true;
            v.DockStateHint=v.DockState.gameObject.AddComponent<TacticalActionHint>();
            v.DockStateHint.Card=Panel("State hover",v.Dock,new Vector2(104,126),new Vector2(260,30),new Vector2(0,0));
            v.DockStateHint.Label=Text("Meaning",v.DockStateHint.Card,Vector2.zero,new Vector2(250,28),16,new Vector2(.5f,.5f));v.DockStateHint.Label.alignment=TextAlignmentOptions.Center;
            v.DockStateHint.Card.gameObject.SetActive(false);
            Img(Rect("Power glyph",v.Dock,new Vector2(20,20),new Vector2(148,-77),new Vector2(0,1)),"stat_power",_common.Power);
            v.PowerValue=Text("Power value",v.Dock,new Vector2(176,-74),new Vector2(66,26),18,new Vector2(0,1));
            v.ThreatIcon=Img(Rect("Enemy glyph",v.Dock,new Vector2(22,22),new Vector2(264,-76),new Vector2(0,1)),null,style.Enemy);v.ThreatIcon.sprite=style.EnemyGlyph;v.ThreatIcon.preserveAspect=true;
            v.ThreatValue=Text("Threat count",v.Dock,new Vector2(294,-74),new Vector2(60,26),18,new Vector2(0,1));v.ThreatValue.color=style.Enemy;
            v.ToggleTurret = Button("Toggle",v.Dock,new Vector2(-12,-12),new Vector2(124,32),"켜기",out var toggle,new Vector2(1,1)); v.ToggleLabel=toggle;
            v.ToggleTurret.GetComponent<UiButtonFeedback>().PreserveLabelColor = true;
            v.Upgrade = Button("Upgrade",v.Dock,new Vector2(-12,-68),new Vector2(124,32),"강화",out _,new Vector2(1,1));
            ButtonIcon(v.ToggleTurret,style.PowerGlyph,true); ButtonIcon(v.Upgrade,style.UpgradeGlyph);
            v.Restriction = Text("Restriction",v.Dock,new Vector2(0,126),new Vector2(576,24),15,new Vector2(0,0)); v.Restriction.color = _common.Power;v.Restriction.gameObject.SetActive(false);
            v.UpgradeDrawer = Panel("Upgrade drawer",command,new Vector2(24,166),new Vector2(576,94),Vector2.zero);
            v.UpgradeDrawer.GetComponent<Image>().raycastTarget=true;
            v.LowLabel=Text("Low power",v.UpgradeDrawer,new Vector2(16,-12),new Vector2(138,26),16,new Vector2(0,1));
            v.FireLabel=Text("Firepower",v.UpgradeDrawer,new Vector2(16,-53),new Vector2(138,26),16,new Vector2(0,1));
            v.LowMinus=Button("Low minus",v.UpgradeDrawer,new Vector2(162,-10),new Vector2(34,30),"−",out _,new Vector2(0,1));
            v.LowPlus=Button("Low plus",v.UpgradeDrawer,new Vector2(206,-10),new Vector2(34,30),"+",out _,new Vector2(0,1));
            v.FireMinus=Button("Fire minus",v.UpgradeDrawer,new Vector2(162,-51),new Vector2(34,30),"−",out _,new Vector2(0,1));
            v.FirePlus=Button("Fire plus",v.UpgradeDrawer,new Vector2(206,-51),new Vector2(34,30),"+",out _,new Vector2(0,1));
            v.LevelUp=Button("Level up",v.UpgradeDrawer,new Vector2(-16,-10),new Vector2(140,30),"레벨 상승",out _,new Vector2(1,1));
            v.LevelDown=Button("Level down",v.UpgradeDrawer,new Vector2(-16,-51),new Vector2(140,30),"레벨 하강",out _,new Vector2(1,1));

            var nav = Panel("Navigator",command,new Vector2(-98,24),new Vector2(290,230),new Vector2(1,0));
            v.Overview=Rect("Overview",nav,new Vector2(270,164),new Vector2(10,-10),new Vector2(0,1)); v.Overview.pivot=new Vector2(.5f,.5f); v.Overview.anchoredPosition=new Vector2(145,-138);
            v.Overview.gameObject.AddComponent<RectMask2D>();
            Img(v.Overview,null,new Color(.045f,.055f,.05f,.95f)).raycastTarget=true;
            var terrain=Rect("Authored terrain",v.Overview,new Vector2(270,164),Vector2.zero,new Vector2(.5f,.5f)); v.OverviewTerrain=terrain.gameObject.AddComponent<RawImage>(); v.OverviewTerrain.raycastTarget=false; v.OverviewTerrain.color=new Color(.75f,.78f,.73f,.85f);
            var raster=Rect("Captured cells",v.Overview,new Vector2(270,164),Vector2.zero,new Vector2(.5f,.5f)); v.OverviewFill=raster.gameObject.AddComponent<RawImage>(); v.OverviewFill.raycastTarget=false; v.OverviewFill.color=Color.white;v.OverviewFill.material=territory;
            var map=Stretch("Entities and viewport",v.Overview); v.OverviewGraphic=map.gameObject.AddComponent<TacticalMapGraphic>(); v.OverviewGraphic.View=v; v.OverviewGraphic.Overview=true; v.OverviewGraphic.raycastTarget=false;
            var pointer=v.Overview.gameObject.AddComponent<TacticalMapPointer>(); pointer.View=v; pointer.IsOverview=true;
            v.FocusPlayer=IconButton("Focus player",nav,new Vector2(12,-8),new Vector2(38,34),"플레이어",_common.Get("owner_player"));
            v.FocusCU=IconButton("Focus CU",nav,new Vector2(58,-8),new Vector2(38,34),"CU",_common.Get("owner_cu"));
            v.Back=IconButton("Previous view",nav,new Vector2(104,-8),new Vector2(38,34),"이전 시야",null,TacticalSymbolGraphic.Symbol.Previous);
            v.ClearGoal=IconButton("Clear waypoint",nav,new Vector2(240,-8),new Vector2(38,34),"목표 해제",null,TacticalSymbolGraphic.Symbol.ClearGoal);
            v.AutoActivate=IconButton("Auto activation",command,new Vector2(-98,268),new Vector2(48,40),"자동 활성화",style.AutomaticGlyph,anchor:new Vector2(1,0));
            v.BatchActivate=IconButton("Batch activation",command,new Vector2(-154,268),new Vector2(48,40),"선택 켜기",null,TacticalSymbolGraphic.Symbol.Active,new Vector2(1,0));
            v.BatchDeactivate=IconButton("Batch deactivation",command,new Vector2(-210,268),new Vector2(48,40),"선택 끄기",null,TacticalSymbolGraphic.Symbol.Off,new Vector2(1,0));
            v.SelectMultiple=IconButton("Multiple selection",command,new Vector2(-266,268),new Vector2(48,40),"다중 선택",style.SelectionGlyph,anchor:new Vector2(1,0));
            v.SelectionLabel=v.SelectMultiple.GetComponent<UiButtonFeedback>().Label;v.SelectMultiple.GetComponent<UiButtonFeedback>().PreserveLabelColor=true;
            v.Close=IconButton("Close command",command,new Vector2(-24,24),new Vector2(48,40),"지도 닫기",null,TacticalSymbolGraphic.Symbol.Close,new Vector2(1,0));
            v.ToggleMap=Button("Open command",root,new Vector2(-24,24),new Vector2(192,42),"전술 지도",out _,new Vector2(1,0));
            SymbolButton(v.ToggleMap,TacticalSymbolGraphic.Symbol.Map);
            v.MapSettings=IconButton("Map settings",command,new Vector2(-24,78),new Vector2(48,40),"지도 설정",null,TacticalSymbolGraphic.Symbol.Settings,new Vector2(1,0));
            v.SettingsDrawer=Rect("Map settings drawer",nav,new Vector2(270,164),new Vector2(10,-56),new Vector2(0,1));
            Img(v.SettingsDrawer,null,new Color(.045f,.055f,.05f,.98f)).raycastTarget=true;
            var settingsTitle=Text("Settings heading",v.SettingsDrawer,new Vector2(12,-4),new Vector2(246,26),18,new Vector2(0,1));settingsTitle.text="지도 설정";
            v.Ranges=Button("Range visibility",v.SettingsDrawer,new Vector2(8,-38),new Vector2(254,34),"",out var rangeLabel,new Vector2(0,1));v.RangeLabel=rangeLabel;
            v.Scenery=Button("Scenery visibility",v.SettingsDrawer,new Vector2(8,-80),new Vector2(254,34),"",out var sceneryLabel,new Vector2(0,1));v.SceneryLabel=sceneryLabel;
            v.Ranges.GetComponent<UiButtonFeedback>().PreserveLabelColor=true;v.Scenery.GetComponent<UiButtonFeedback>().PreserveLabelColor=true;
            foreach(var button in new[]{v.Ranges,v.Scenery})
            {
                var label=button.GetComponent<UiButtonFeedback>().Label;
                label.rectTransform.sizeDelta=new Vector2(224,34);label.rectTransform.anchoredPosition=new Vector2(12,0);
            }
            v.RangeState=Symbol("Range mode",v.Ranges.transform,new Vector2(24,24),new Vector2(-106,0),new Vector2(.5f,.5f),TacticalSymbolGraphic.Symbol.RangeAll);
            v.SceneryState=Symbol("Scenery mode",v.Scenery.transform,new Vector2(24,24),new Vector2(-106,0),new Vector2(.5f,.5f),TacticalSymbolGraphic.Symbol.TerrainVisible);
            v.Ranges.GetComponent<UiButtonFeedback>().Icon=v.RangeState;v.Scenery.GetComponent<UiButtonFeedback>().Icon=v.SceneryState;
            v.SettingsDrawer.gameObject.SetActive(false);
            v.Tooltip=Panel("Turret hover",root,Vector2.zero,new Vector2(320,106),new Vector2(.5f,.5f));
            v.TooltipLabel=Text("Information",v.Tooltip,new Vector2(80,-8),new Vector2(226,27),17,new Vector2(0,1));
            v.TooltipHealth=Health("Health",v.Tooltip,new Vector2(212,9),new Vector2(80,-46));
            v.TooltipState=Symbol("State",v.Tooltip,new Vector2(24,24),new Vector2(80,-67),new Vector2(0,1),TacticalSymbolGraphic.Symbol.Off);
            Img(Rect("Power",v.Tooltip,new Vector2(20,20),new Vector2(130,-69),new Vector2(0,1)),"stat_power",_common.Power);
            v.TooltipPower=Text("Power value",v.Tooltip,new Vector2(158,-67),new Vector2(132,26),17,new Vector2(0,1));
            v.TooltipIcon=Img(Rect("Icon",v.Tooltip,new Vector2(54,64),new Vector2(14,-12),new Vector2(0,1)),null,Color.white);v.TooltipIcon.preserveAspect=true;
            var fieldStatus=Rect("Field region status",root,new Vector2(440,42),new Vector2(0,-82),new Vector2(.5f,1));fieldStatus.pivot=new Vector2(.5f,.5f);
            v.FieldStatusGroup=fieldStatus.gameObject.AddComponent<CanvasGroup>();v.FieldStatusGroup.alpha=0;v.FieldStatusGroup.blocksRaycasts=false;
            v.FieldStatusContent=Stretch("State pulse",fieldStatus);
            v.FieldStatusLabel=Text("State",v.FieldStatusContent,new Vector2(16,0),new Vector2(400,36),22,new Vector2(.5f,.5f));v.FieldStatusLabel.alignment=TextAlignmentOptions.Center;
            v.FieldStatusLabel.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/UI/Common/AlertText.mat");
            v.FieldStatusIcon=Symbol("Region symbol",v.FieldStatusContent,new Vector2(26,26),new Vector2(-120,0),new Vector2(.5f,.5f),TacticalSymbolGraphic.Symbol.OutsideTerritory);
            v.Prompt=Panel("Quick interaction",root,Vector2.zero,new Vector2(68,32),new Vector2(.5f,.5f));
            v.PromptLabel=Text("Key",v.Prompt,new Vector2(-16,0),new Vector2(28,30),18,new Vector2(.5f,.5f)); v.PromptLabel.font=_common.LabelFont; v.PromptLabel.alignment=TextAlignmentOptions.Center;
            v.PromptAction=Img(Rect("Power action",v.Prompt,new Vector2(22,22),new Vector2(16,0),new Vector2(.5f,.5f)),null,_common.Text); v.PromptAction.sprite=style.PowerGlyph;
            v.Goal=Rect("Pinned goal",root,new Vector2(80,54),Vector2.zero,new Vector2(.5f,.5f));
            v.GoalArrow=Rect("Direction",v.Goal,new Vector2(14,18),new Vector2(0,10),new Vector2(.5f,.5f)).gameObject.AddComponent<TacticalGoalArrow>(); v.GoalArrow.color=_common.Accent; v.GoalArrow.raycastTarget=false;
            v.GoalPin=Img(Rect("Beacon",v.Goal,new Vector2(28,28),new Vector2(0,10),new Vector2(.5f,.5f)),null,_common.Accent); v.GoalPin.sprite=style.GoalGlyph; v.GoalPin.preserveAspect=true;
            v.GoalLabel=Text("Distance",v.Goal,new Vector2(0,-15),new Vector2(90,26),18,new Vector2(.5f,.5f)); v.GoalLabel.alignment=TextAlignmentOptions.Center;
            v.GoalLabel.font=_common.LabelFont;v.GoalLabel.fontSharedMaterial=worldLabel;
            v.SelectionBox.gameObject.SetActive(false); v.ShowCommand(false); v.Tooltip.gameObject.SetActive(false); v.Prompt.gameObject.SetActive(false); v.Goal.gameObject.SetActive(false); v.ToggleMap.gameObject.SetActive(false);
            var scale=root.gameObject.AddComponent<CombatHudScaleBinding>();
            scale.MinimumScreenScale=.8f;
            var parts=new System.Collections.Generic.List<CombatHudScaleBinding.Element>();
            void Scale(RectTransform rect,bool offset=true,float minimum=.85f) => parts.Add(new CombatHudScaleBinding.Element{Target=rect,ScaleOffset=offset,BaseOffset=rect.anchoredPosition,MinimumScale=minimum});
            Scale(v.Dock); Scale(v.UpgradeDrawer); Scale(nav);Scale(fieldStatus); Scale(v.Tooltip,false); Scale(v.Prompt,false);
            foreach(var button in new[]{v.ToggleMap,v.Close,v.MapSettings,v.AutoActivate,v.BatchActivate,v.BatchDeactivate,v.SelectMultiple}) Scale((RectTransform)button.transform);
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
    private static TacticalHealthGraphic Health(string name,Transform parent,Vector2 size,Vector2 pos)
    {var h=Rect(name,parent,size,pos,new Vector2(0,1)).gameObject.AddComponent<TacticalHealthGraphic>();h.color=_common.Text;h.raycastTarget=false;return h;}
    private static TacticalSymbolGraphic Symbol(string name,Transform parent,Vector2 size,Vector2 pos,Vector2 anchor,TacticalSymbolGraphic.Symbol shape)
    {var g=Rect(name,parent,size,pos,anchor).gameObject.AddComponent<TacticalSymbolGraphic>();g.Set(shape,_common.Text);g.raycastTarget=false;return g;}
    private static Button IconButton(string name,Transform parent,Vector2 pos,Vector2 size,string caption,Sprite sprite,TacticalSymbolGraphic.Symbol shape=TacticalSymbolGraphic.Symbol.Map,Vector2? anchor=null)
    {
        var button=Button(name,parent,pos,size,"",out _,anchor??new Vector2(0,1));var feedback=button.GetComponent<UiButtonFeedback>();
        if(sprite!=null){var icon=Img(Rect("Action",button.transform,new Vector2(24,24),Vector2.zero,new Vector2(.5f,.5f)),null,_common.Text);icon.sprite=sprite;icon.preserveAspect=true;feedback.Icon=icon;}
        else feedback.Icon=Symbol("Action",button.transform,new Vector2(24,24),Vector2.zero,new Vector2(.5f,.5f),shape);
        var hint=button.gameObject.AddComponent<TacticalActionHint>();hint.Caption=caption;
        hint.Card=Panel("Hover name",button.transform,new Vector2(0,8),new Vector2(144,30),new Vector2(1,1));hint.Card.pivot=new Vector2(1,0);
        hint.Label=Text("Name",hint.Card,Vector2.zero,new Vector2(134,28),16,new Vector2(.5f,.5f));hint.Label.alignment=TextAlignmentOptions.Center;
        hint.Card.gameObject.SetActive(false);return button;
    }
    private static void ButtonIcon(Button button,Sprite sprite,bool toggleState=false)
    {
        var r=(RectTransform)button.transform;
        var rect=Rect("Command glyph",r,new Vector2(20,20),new Vector2(-r.sizeDelta.x*.5f+17,0),new Vector2(.5f,.5f));
        Graphic icon;
        if(toggleState){var symbol=rect.gameObject.AddComponent<TacticalSymbolGraphic>();symbol.Set(TacticalSymbolGraphic.Symbol.Active,_common.Text);symbol.raycastTarget=false;icon=symbol;}
        else{var image=Img(rect,null,_common.Text);image.sprite=sprite;image.preserveAspect=true;icon=image;}
        var feedback=button.GetComponent<UiButtonFeedback>(); feedback.Icon=icon;
        var label=feedback.Label; label.rectTransform.sizeDelta=new Vector2(r.sizeDelta.x-30,r.sizeDelta.y); label.rectTransform.anchoredPosition=new Vector2(12,0);
    }
    private static void SymbolButton(Button button,TacticalSymbolGraphic.Symbol shape)
    {
        var r=Rect("Map symbol",button.transform,new Vector2(26,26),new Vector2(-66,0),new Vector2(.5f,.5f));
        var g=r.gameObject.AddComponent<TacticalSymbolGraphic>();g.Shape=shape;g.color=_common.Text;g.raycastTarget=false;
        var f=button.GetComponent<UiButtonFeedback>();f.Icon=g;f.Label.fontSize=18;
        f.Label.rectTransform.anchoredPosition=new Vector2(14,0);f.Label.rectTransform.sizeDelta=new Vector2(142,42);
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
