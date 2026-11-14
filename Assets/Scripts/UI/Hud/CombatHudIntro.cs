using TeamHJD.Game.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>첫 필드 표시 때만 수평 스캔으로 켭니다. 입력 lease나 게임 시간은 변경하지 않습니다.</summary>
[DefaultExecutionOrder(900)]
public sealed class CombatHudIntro : MonoBehaviour
{
    public CanvasGroup Group;
    public Mask RevealMask;
    public Image MaskGraphic, ScanGraphic;
    public Material MaterialTemplate;
    public PlayerSkillHud SkillHud;
    public InitialStagePlayerHUD Stage;
    public FieldEntryAnnouncement AnnouncementPrefab;
    [Min(0)] public float MessageFadeSeconds = .22f;
    [Min(0)] public float MessageHoldSeconds = 3f;
    [Min(.1f)] public float MessageEraseSeconds = .45f;
    [Min(.1f)] public float Duration = .6f;
    public bool Completed { get; private set; }
    public float Progress { get; private set; }
    private Material _maskMaterial, _scanMaterial;
    private AppRoot _app;
    private float _readyAt = -1, _elapsed;
    private bool _started;
    private sealed class Entry { public int Users; public float Start = -1; }
    private static readonly Dictionary<int, Entry> Entries = new();
    private Entry _entry;
    private int _scene;
    private FieldEntryAnnouncement _announcement;
    public float AnnouncementSeconds => MessageFadeSeconds + MessageHoldSeconds + MessageEraseSeconds;
    private void Awake()
    {
        _scene = gameObject.scene.handle;
        if (!Entries.TryGetValue(_scene, out _entry)) Entries.Add(_scene, _entry = new Entry());
        _entry.Users++;
        if (AnnouncementPrefab != null)
        {
            _announcement = Instantiate(AnnouncementPrefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_announcement.gameObject, gameObject.scene);
        }
        _maskMaterial = new Material(MaterialTemplate); _scanMaterial = new Material(MaterialTemplate);
        _maskMaterial.SetFloat("_ScanOnly", 0); _scanMaterial.SetFloat("_ScanOnly", 1);
        MaskGraphic.material = _maskMaterial; ScanGraphic.material = _scanMaterial;
        Group.alpha = 0; Group.blocksRaycasts = Group.interactable = false;
        MaskGraphic.enabled = RevealMask.enabled = true; ScanGraphic.enabled = false;
        SetProgress(0);
    }
    private void LateUpdate()
    {
        if (Completed) return;
        if (_app == null) _app = FindFirstObjectByType<AppRoot>();
        bool field = SkillHud != null ? SkillHud.IsFieldVisible : Stage != null && Stage.HasFieldActor;
        bool ready = field && (_app == null || (_app.IsInitialized && !_app.LocalInput.IsBlocked));
        if (!field)
        {
            _readyAt = -1; _elapsed = 0; _started = false; Group.alpha = 0; ScanGraphic.enabled = false; SetProgress(0); return;
        }
        if (!_started)
        {
            if (!ready) { _readyAt = -1; return; }
            if (_readyAt < 0) _readyAt = Time.unscaledTime;
            if (Time.unscaledTime - _readyAt < .1f) return;
            _started = true;
            if (_entry.Start < 0) _entry.Start = Time.unscaledTime;
        }
        _elapsed = Mathf.Max(0, Time.unscaledTime - _entry.Start);
        if (_announcement != null) _announcement.Render(_elapsed, MessageFadeSeconds, MessageHoldSeconds, MessageEraseSeconds);
        if (_elapsed < AnnouncementSeconds) { Group.alpha = 0; return; }
        ScanGraphic.enabled = true;
        SetProgress(Mathf.Clamp01((_elapsed - AnnouncementSeconds) / Duration));
        Group.alpha = Mathf.Lerp(.45f, 1, CombatHudMotion.Sigmoid(Progress));
        if (Progress < 1) return;
        Completed = true; Group.alpha = 1; Group.blocksRaycasts = Group.interactable = true;
        RevealMask.enabled = MaskGraphic.enabled = ScanGraphic.enabled = false;
    }
    private void SetProgress(float progress)
    {
        Progress = progress;
        _maskMaterial.SetFloat("_Reveal", progress); _scanMaterial.SetFloat("_Reveal", progress);
        if (MaskGraphic.enabled && MaskGraphic.materialForRendering != null) MaskGraphic.materialForRendering.SetFloat("_Reveal", progress);
        if (ScanGraphic.enabled && ScanGraphic.materialForRendering != null)
        { ScanGraphic.materialForRendering.SetFloat("_Reveal", progress); ScanGraphic.materialForRendering.SetFloat("_ScanOnly", 1); }
    }
    private void OnDestroy()
    {
        if (_entry != null && --_entry.Users == 0) Entries.Remove(_scene);
        if (_announcement != null) Destroy(_announcement.gameObject);
        if (_maskMaterial != null) Destroy(_maskMaterial);
        if (_scanMaterial != null) Destroy(_scanMaterial);
    }
}
