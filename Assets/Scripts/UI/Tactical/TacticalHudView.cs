using TMPro;
using TeamHJD.Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TacticalHudView : MonoBehaviour
{
    // 월드 표식·지도 조작은 전투 HUD(20/30) 아래, 전환막은 자체 Canvas 순서를 유지합니다.
    public const int SortingOrder = 10;
    public Canvas Canvas;
    public TacticalModeEffect ModeEffect;
    public CanvasGroup CommandGroup;
    public RectTransform CommandRoot, Dock, UpgradeDrawer, Overview, Tooltip, Prompt, Goal;
    public RectTransform WorldClip;
    public RectTransform[] ViewportMatte;
    public Button ToggleMap, Close, ToggleTurret, Upgrade, LevelUp, LevelDown, LowPlus, LowMinus, FirePlus, FireMinus;
    public Button AutoActivate, BatchActivate, BatchDeactivate, SelectMultiple, FocusPlayer, FocusCU, Back;
    public Button MapSettings, Ranges, Scenery, ClearGoal;
    public RectTransform SettingsDrawer;
    public TacticalHealthGraphic DockHealth, TooltipHealth;
    public TacticalHealthGraphic[] IncidentHealth;
    public Image[] IncidentIcons;
    public TacticalSymbolGraphic[] IncidentStates;
    public TacticalSymbolGraphic DockState, TooltipState, RangeState, SceneryState;
    public TMP_Text PowerValue, ThreatValue, TooltipPower;
    public Image ThreatIcon;
    public TacticalActionHint DockStateHint;
    public TMP_Text RangeLabel, SceneryLabel;
    public Button[] Incidents;
    public TMP_Text[] IncidentLabels, EnemyLabels;
    public TMP_Text Title, Stats, Restriction, ToggleLabel, SelectionLabel, LowLabel, FireLabel, TooltipLabel, PromptLabel, GoalLabel;
    public Image TurretIcon, TooltipIcon, PlayerMarker;
    public Image ControlMarker, GoalPin, PromptAction;
    public Image[] TurretGlyphs, EnemyIcons;
    public Graphic GoalArrow;
    public RawImage OverviewFill, TerritoryFill;
    public CanvasGroup FieldStatusGroup;
    public RectTransform FieldStatusContent;
    public TMP_Text FieldStatusLabel;
    public TacticalSymbolGraphic FieldStatusIcon;
    public RawImage OverviewTerrain;
    public RectTransform SelectionBox;
    public TacticalMapGraphic WorldGraphic, OverviewGraphic;
    public TacticalCommandController Owner { get; private set; }
    private void Awake()
    {
        if(Canvas==null)Canvas=GetComponent<Canvas>();
        if(Canvas!=null){Canvas.overrideSorting=true;Canvas.sortingOrder=SortingOrder;}
    }
    public void Bind(TacticalCommandController owner) => Owner = owner;
    private void LateUpdate()
    {
        // EventSystem의 클릭이 Controller.Update 뒤에 와도 첫 전환 프레임에 필드 UI를 남기지 않습니다.
        if(!HudPresentationVisibility.IsSuppressed)return;
        ToggleMap.gameObject.SetActive(false);
        Tooltip.gameObject.SetActive(false);Prompt.gameObject.SetActive(false);Goal.gameObject.SetActive(false);
        FieldStatusGroup.alpha=0;FieldStatusGroup.gameObject.SetActive(false);
    }
    public void ShowCommand(bool show)
    {
        CommandRoot.gameObject.SetActive(show);
        if (!show) UpgradeDrawer.gameObject.SetActive(false);
        if (!show && SettingsDrawer!=null) SettingsDrawer.gameObject.SetActive(false);
        Close.gameObject.SetActive(show);
    }
}
