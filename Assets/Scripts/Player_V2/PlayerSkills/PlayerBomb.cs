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

    public int CurrentBombCount => _currentBombCount;
    public int MaxBombCount => maxBombCount;

    private void Awake()
    {
        _currentBombCount = maxBombCount;
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Start()
    {
        FindInGameManager();
        SyncBombUI();
    }

    private void Update()
    {
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
    /// E키 입력 시 호출되는 폭탄 투하 로직
    /// </summary>
    public void TryDropBomb()
    {
        // 쿨타임 검사
        if (Time.time - _lastBombTime < bombCooldown) return;

        // 폭탄 개수 검사
        if (_currentBombCount <= 0) return;

        DropBomb();
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
                if (_inGameManager != null)
                {
                    _inGameManager.ChargeBombImage((int)(timer * 10), (int)(bombRechargeTime * 10));
                }
                yield return null;
            }

            _currentBombCount++;
            SyncBombUI();
        }

        if (_inGameManager != null)
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