using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TacticalHudView : MonoBehaviour
{
    public Canvas Canvas;
    public CanvasGroup CommandGroup;
    public RectTransform CommandRoot, Dock, UpgradeDrawer, Overview, Tooltip, Prompt, Goal;
    public RectTransform WorldClip;
    public RectTransform[] ViewportMatte;
    public Button ToggleMap, Close, ToggleTurret, Upgrade, LevelUp, LevelDown, LowPlus, LowMinus, FirePlus, FireMinus;
    public Button AutoActivate, BatchActivate, BatchDeactivate, SelectMultiple, FocusPlayer, FocusCU, Back;
    public Button[] Incidents;
    public TMP_Text[] IncidentLabels, EnemyLabels;
    public TMP_Text Title, Stats, Restriction, ToggleLabel, SelectionLabel, LowLabel, FireLabel, TooltipLabel, PromptLabel, GoalLabel;
    public Image TurretIcon, TooltipIcon, PlayerMarker;
    public Image ControlMarker, GoalPin, PromptAction;
    public Image[] TurretGlyphs, EnemyIcons;
    public Graphic GoalArrow;
    public RawImage OverviewFill;
    public RawImage OverviewTerrain;
    public RectTransform SelectionBox;
    public TacticalMapGraphic WorldGraphic, OverviewGraphic;
    public TacticalCommandController Owner { get; private set; }
    public void Bind(TacticalCommandController owner) => Owner = owner;
    public void ShowCommand(bool show)
    {
        CommandRoot.gameObject.SetActive(show);
        if (!show) UpgradeDrawer.gameObject.SetActive(false);
        Close.gameObject.SetActive(show);
    }
}

