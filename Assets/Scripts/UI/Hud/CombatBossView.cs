// Encounter 표시 원본이 실제 보스와 이름을 연결할 때만 표시합니다. 일반 적을 보스로 자동 추정하지 않습니다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatBossView : MonoBehaviour
{
    public CombatHudStyle Style;
    public CanvasGroup Group;
    public Image Health;
    public TMP_Text Name;
    public EnemyHealth Source { get; private set; }
    private readonly CombatHudTransition _visible = new();
    public void Bind(EnemyHealth source, string displayName)
    {
        Source = source != null && source.gameObject.scene == gameObject.scene && !string.IsNullOrWhiteSpace(displayName) ? source : null;
        Name.text = Source != null ? displayName : "";
    }
    private void LateUpdate()
    {
        bool available = Source != null && Source.isActiveAndEnabled && Source.CurrentHealth > 0 && Source.MaxHealth > 0;
        _visible.Set(available ? 1 : 0, Style.SlideSeconds);
        Group.alpha = _visible.Tick(Time.unscaledDeltaTime); Group.blocksRaycasts = false;
        Health.fillAmount = available ? Mathf.Clamp01((float)Source.CurrentHealth / Source.MaxHealth) : 0;
    }
    private void OnDisable() { Source = null; _visible.Snap(0); if (Group != null) Group.alpha = 0; }
}
