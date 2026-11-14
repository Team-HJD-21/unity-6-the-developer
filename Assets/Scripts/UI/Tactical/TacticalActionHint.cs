using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>도상 버튼의 이름은 필요할 때만 공통 Hover 카드로 표시합니다.</summary>
public sealed class TacticalActionHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public RectTransform Card;
    public TMP_Text Label;
    public string Caption;
    private bool _inside;
    private float _entered;
    public void OnPointerEnter(PointerEventData e){_inside=true;_entered=Time.unscaledTime;}
    public void OnPointerExit(PointerEventData e)=>Hide();
    public void OnPointerDown(PointerEventData e)=>Hide();
    private void Update(){if(Card!=null){bool show=_inside && Time.unscaledTime-_entered>.35f;Card.gameObject.SetActive(show);if(show)Label.text=Caption;}}
    private void OnDisable()=>Hide();
    private void Hide(){_inside=false;if(Card!=null)Card.gameObject.SetActive(false);}
}
