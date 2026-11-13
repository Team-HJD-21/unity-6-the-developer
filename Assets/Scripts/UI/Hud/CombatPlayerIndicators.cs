// 기존 로컬 HUD owner의 탄창/조준/재장전과 실제 XP 획득만 표시합니다. 탄환·획득 수치를 임의 갱신하지 않습니다.
using UnityEngine;
using TMPro;

[DefaultExecutionOrder(600)]
public sealed class CombatPlayerIndicators : MonoBehaviour
{
    public PlayerWeaponHud WeaponHud;
    public PlayerSkillHud SkillHud;
    public CombatHudStyle Style;
    public RectTransform CanvasRect, AmmoRoot, ReloadArrows;
    public CanvasGroup AmmoGroup;
    public RectTransform AmmoContent;
    public TMP_Text RoundCount, InfiniteLabel;
    public GameObject ReloadRoot;
    public CombatExperiencePopup PopupPrefab;
    [Min(.01f)] public float ShotFeedbackSeconds = .2f;
    public bool ReloadVisible => ReloadRoot.activeSelf;
    public int DisplayedRounds => _lastRounds;
    public float ShotPulse => 1 - Mathf.SmoothStep(0, 1, (Time.unscaledTime - _shotAt) / ShotFeedbackSeconds);
    private PlayerWeaponSelection _owner;
    private PlayerWeaponAmmo _ammo;
    private PlayerAttack _attack;
    private PlayerExperience _experience;
    private CombatExperiencePopup[] _popups;
    private int _nextPopup, _lastRounds = -1, _lastCapacity = -1;
    private float _shotAt = float.NegativeInfinity;
    private Camera _camera;

    private void Awake()
    {
        _popups = new CombatExperiencePopup[4];
        for (int i = 0; i < _popups.Length; i++)
        {
            _popups[i] = Instantiate(PopupPrefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_popups[i].gameObject, gameObject.scene);
            _popups[i].gameObject.SetActive(false);
        }
        ClearShotFeedback();
        AmmoGroup.alpha = 0; AmmoGroup.blocksRaycasts = false;
    }
    private void LateUpdate()
    {
        if (_owner != WeaponHud.Owner) Bind(WeaponHud.Owner);
        if (_owner == null || _ammo == null) { AmmoGroup.alpha = 0; return; }
        if (!SkillHud.IsFieldVisible)
        {
            AmmoGroup.alpha = 0; ReloadRoot.SetActive(false); ClearShotFeedback();
            foreach (var popup in _popups) if (popup.gameObject.activeSelf) popup.gameObject.SetActive(false);
            return;
        }
        int rounds = _ammo.CurrentRounds, capacity = _ammo.CurrentCapacity;
        bool infinite = _ammo.IsInfinite, reload = _ammo.IsReloading;
        if (rounds != _lastRounds || capacity != _lastCapacity)
        {
            _lastRounds = rounds; _lastCapacity = capacity;
            RoundCount.text = $"{rounds}<size=65%><color=#9AA39B> / {capacity}</color></size>";
        }
        AmmoGroup.alpha = 1; // The magazine belongs to the selected weapon and stays visible with its panel.
        InfiniteLabel.gameObject.SetActive(infinite);
        RoundCount.gameObject.SetActive(!infinite);
        RoundCount.rectTransform.anchoredPosition = new Vector2(0, reload ? 18 : 0);
        float pulse = ShotPulse;
        RoundCount.rectTransform.localScale = Vector3.one * (1 + .1f * pulse);
        RoundCount.color = Color.Lerp(Style.Text, new Color(1, .88f, .62f), pulse);
        AmmoContent.anchoredPosition = new Vector2(-2.5f * pulse, 0);
        ReloadRoot.SetActive(reload);
        if (reload) ReloadArrows.localRotation = Quaternion.Euler(0, 0, -720 * _ammo.ReloadProgress);
        else ReloadArrows.localRotation = Quaternion.identity;
    }
    private void Fired(WeaponType weapon)
    {
        if (_ammo == null || _ammo.IsInfinite || !SkillHud.IsFieldVisible || weapon != _owner.CurrentWeapon) return;
        _shotAt = Time.unscaledTime;
    }
    private void ChangedWeapon(WeaponType _) { _lastRounds = _lastCapacity = -1; ClearShotFeedback(); }
    private void ClearShotFeedback()
    {
        _shotAt = float.NegativeInfinity;
        if (AmmoContent != null) AmmoContent.anchoredPosition = Vector2.zero;
        if (RoundCount != null) RoundCount.rectTransform.localScale = Vector3.one;
    }
    private void Bind(PlayerWeaponSelection owner)
    {
        if (_owner != null) _owner.WeaponChanged -= ChangedWeapon;
        if (_attack != null) _attack.ShotFired -= Fired;
        if (_experience != null) _experience.ExperienceGranted -= Gained;
        foreach (var popup in _popups) if (popup != null) popup.gameObject.SetActive(false);
        _owner = owner; _ammo = owner != null ? owner.GetComponent<PlayerWeaponAmmo>() : null;
        _attack = owner != null ? owner.GetComponent<PlayerAttack>() : null;
        if (_owner != null) _owner.WeaponChanged += ChangedWeapon;
        if (_attack != null) _attack.ShotFired += Fired;
        _experience = owner != null ? owner.GetComponent<PlayerExperience>() : null;
        if (_experience != null) _experience.ExperienceGranted += Gained;
        _lastRounds = _lastCapacity = -1; ClearShotFeedback();
        ReloadRoot.SetActive(false);
    }
    private void Gained(int amount)
    {
        if (_camera == null) _camera = Camera.main;
        if (_owner == null || !SkillHud.IsFieldVisible || amount <= 0 || _camera == null) return;
        _popups[_nextPopup].Show(_owner.transform, _camera, amount, GetComponentInParent<Canvas>().scaleFactor * SkillHud.DisplayScale);
        _nextPopup = (_nextPopup + 1) % _popups.Length;
    }
    private void OnDisable() { if (_popups != null) Bind(null); if (AmmoGroup != null) AmmoGroup.alpha = 0; }
    private void OnDestroy() { if (_popups != null) foreach (var popup in _popups) if (popup != null) Destroy(popup.gameObject); }
}
