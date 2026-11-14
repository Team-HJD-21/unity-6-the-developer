using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD와 독립된 제작 Canvas입니다. 입력과 게임 시간을 소유하지 않습니다.</summary>
public sealed class FieldEntryAnnouncement : MonoBehaviour
{
    public CanvasGroup Group;
    public Image MaskGraphic, ScanGraphic;
    public Material RevealMaterial;
    [Range(.001f, .05f)] public float ScanWidth = .014f;
    private Material _mask, _scan;
    private void Awake()
    {
        _mask = new Material(RevealMaterial); _scan = new Material(RevealMaterial);
        _mask.SetFloat("_ScanOnly", 0); _scan.SetFloat("_ScanOnly", 1);
        _scan.SetFloat("_ScanWidth", ScanWidth);
        MaskGraphic.material = _mask; ScanGraphic.material = _scan;
        Group.alpha = 0; Group.blocksRaycasts = Group.interactable = false;
        Apply(1); ScanGraphic.enabled = false;
    }
    public void Render(float elapsed, float fade, float hold, float erase)
    {
        bool erasing = elapsed >= fade + hold;
        float reveal = erasing ? 1 - Mathf.Clamp01((elapsed - fade - hold) / Mathf.Max(.001f, erase)) : 1;
        Group.alpha = reveal <= 0 ? 0 : Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.001f, fade));
        ScanGraphic.enabled = erasing && reveal > 0;
        Apply(reveal);
    }
    private void Apply(float reveal)
    {
        _mask.SetFloat("_Reveal", reveal); _scan.SetFloat("_Reveal", reveal);
        MaskGraphic.materialForRendering.SetFloat("_Reveal", reveal);
        ScanGraphic.materialForRendering.SetFloat("_Reveal", reveal);
        ScanGraphic.materialForRendering.SetFloat("_ScanOnly", 1);
    }
    private void OnDestroy()
    { if (_mask != null) Destroy(_mask); if (_scan != null) Destroy(_scan); }
}
