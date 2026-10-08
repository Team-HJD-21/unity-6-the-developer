using System.Collections;
using UnityEngine;

public class PlayerBomb : MonoBehaviour
{
    [Header("폭탄 기본 설정")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private int maxBombCount = 5;
    [SerializeField] private float bombRechargeTime = 5f;
    [SerializeField] private float bombCooldown = 1f;

    private int _currentBombCount;
    private float _lastBombTime = -999f;
    private Coroutine _rechargeCoroutine;
    private InGameManager _inGameManager;
    private PlayerInputHandler _inputHandler;
    private PlayerSkillController _skills;

    public int CurrentBombCount => _currentBombCount;
    public int MaxBombCount => maxBombCount;
    public bool HasPrefab => bombPrefab != null;
    public float RemainingCooldown => Mathf.Max(0f, bombCooldown - (Time.time - _lastBombTime));
    public float CooldownDuration => bombCooldown;

    private void Awake()
    {
        _currentBombCount = maxBombCount;
        _inputHandler = GetComponent<PlayerInputHandler>();
        _skills = GetComponent<PlayerSkillController>();
    }

    private void Start()
    {
        FindInGameManager();
        SyncBombUI();
    }

    private void Update()
    {
        // 선택 슬롯이 있으면 Q/E 처리는 Controller가 한 번만 수행합니다.
        if (_skills != null) return;
        // 1. PlayerInputHandler가 연결되어 있으면 WasBombPressed 확인
        bool isBombPressed = false;
        if (_inputHandler != null)
        {
            isBombPressed = _inputHandler.WasBombPressed;
        }
        else
        {
            // 인풋 핸들러가 없을 경우를 대비한 폴백 처리
            isBombPressed = Input.GetKeyDown(KeyCode.E) || GameInput.WasPressedThisFrame(GameKey.E);
        }

        // 2. 키가 눌렸을 때 폭탄 투하 시도
        if (isBombPressed)
        {
            TryDropBomb();
        }
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedSyncRoutine());
    }

    private IEnumerator DelayedSyncRoutine()
    {
        yield return null;
        FindInGameManager();
        SyncBombUI();
    }

    private void FindInGameManager()
    {
        if (_inGameManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            _inGameManager = Object.FindAnyObjectByType<InGameManager>();
#else
            _inGameManager = FindObjectOfType<InGameManager>();
#endif
        }
    }

    /// <summary>
    /// 선택된 Q/E 슬롯에서 호출되는 폭탄 투하 로직
    /// </summary>
    public void TryDropBomb()
    {
        TryUseSkill();
    }

    public bool TryUseSkill()
    {
        if (!isActiveAndEnabled || bombPrefab == null || RemainingCooldown > 0f || _currentBombCount <= 0) return false;
        if (_skills != null && (!_skills.CanUse || !_skills.Loadout.Contains(PlayerSkillId.Bomb))) return false;
        DropBomb();
        return true;
    }

    private void DropBomb()
    {
        _lastBombTime = Time.time;
        _currentBombCount--;

        if (bombPrefab != null)
        {
            Instantiate(bombPrefab, transform.position, Quaternion.identity);
        }

        // 사용 직후 UI 즉시 갱신
        SyncBombUI();

        // 폭탄 재충전 루틴 시작
        if (_rechargeCoroutine == null && _currentBombCount < maxBombCount)
        {
            _rechargeCoroutine = StartCoroutine(RechargeRoutine());
        }
    }

    private IEnumerator RechargeRoutine()
    {
        float timer = 0f;

        while (_currentBombCount < maxBombCount)
        {
            timer = 0f;
            while (timer < bombRechargeTime)
            {
                timer += Time.deltaTime;
                if (_skills == null && _inGameManager != null)
                {
                    _inGameManager.ChargeBombImage((int)(timer * 10), (int)(bombRechargeTime * 10));
                }
                yield return null;
            }

            _currentBombCount++;
            SyncBombUI();
        }

        if (_skills == null && _inGameManager != null)
        {
            _inGameManager.ChargeBombImage(1, 1);
        }

        _rechargeCoroutine = null;
    }

    /// <summary>
    /// InGameManager에 폭탄 텍스트 및 게이지 UI 동기화
    /// </summary>
    public void SyncBombUI()
    {
        if (_skills != null) return;
        FindInGameManager();

        if (_inGameManager != null)
        {
            _inGameManager.ValidateBombCount(_currentBombCount);

            if (_inGameManager.bombImage != null)
            {
                if (!_inGameManager.bombImage.activeSelf)
                {
                    _inGameManager.bombImage.SetActive(true);
                }

                if (_currentBombCount >= maxBombCount)
                {
                    _inGameManager.ChargeBombImage(1, 1);
                }
            }
        }
    }
}
