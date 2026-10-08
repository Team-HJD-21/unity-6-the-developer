using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class UICUHpHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public GameObject uiInfoWrapper;
    public TMP_Text uiInfo;

    private ControlUnitHealth controlUnitHealth;
    private bool isHover;

    private void Start()
    {
        uiInfoWrapper.SetActive(false);
        GameObject controlUnit = GameObject.FindGameObjectWithTag("CU");
        if (controlUnit != null)
        {
            controlUnitHealth = controlUnit.GetComponent<ControlUnitHealth>();
        }
    }

    private void Update()
    {
        if (isHover) UpdateUIInfo();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHover = true;
        uiInfoWrapper.SetActive(true);
        UpdateUIInfo();
        UpdateUIPosition(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHover = false;
        uiInfoWrapper.SetActive(false);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isHover) return;
        UpdateUIInfo();
        UpdateUIPosition(eventData.position);
    }

    private void UpdateUIInfo()
    {
        if (controlUnitHealth != null)
        {
            uiInfo.SetText("HP : " + controlUnitHealth.CurrentHealth + " / " + controlUnitHealth.MaxHealth);
        }
    }

    private void UpdateUIPosition(Vector2 pointerPosition)
    {
        RectTransform rectTransform = uiInfoWrapper.GetComponent<RectTransform>();
        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);
        float width = worldCorners[2].x - worldCorners[0].x;
        float height = worldCorners[2].y - worldCorners[0].y;
        rectTransform.position = pointerPosition + new Vector2(-width / 2 - 1, height / 2 + 1);
    }
}
