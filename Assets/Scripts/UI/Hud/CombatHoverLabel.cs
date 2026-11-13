// 아이콘의 보조 정보는 실제 hover 동안만 보입니다. 숨겨진 효과 슬롯이 입력을 가로채지 않게 부모가 제어합니다.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CombatHoverLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text Label;
    public void OnPointerEnter(PointerEventData data) { if (Label != null) Label.enabled = true; }
    public void OnPointerExit(PointerEventData data) { if (Label != null) Label.enabled = false; }
    private void OnEnable() { if (Label != null) Label.enabled = false; }
}
