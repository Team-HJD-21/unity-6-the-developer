using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어 폭탄 설치(E키), 소지량 관리, 500 카운트 충전 루프 및 InGameManager UI 동기화 전담 컴포넌트
/// 
/// [채택 이유 및 대안 비교]
/// 1. 웨이브 시작 트리거 자동 감지 및 강제 완충 (포탑 단계 이후 대응):
///    - 대안: UI 시작 버튼(Button OnClick)에 매번 함수를 수동 등록.
///    - 채택 이유: 버튼 인스펙터 연결이 누락되거나 씬 전환 시 참조가 깨지는 문제를 방지하기 위해,
///      준비 단계(isWave=false)에서 버튼 클릭 후 웨이브가 시작되는(isWave=true) 순간을 컴포넌트 내부에서 감지하여
///      폭탄 개수를 즉시 최대치(5)로 완충하고 UI를 갱신합니다.
/// 
/// 2. 코루틴 중복 실행 방지 및 안전한 재충전:
///    - Coroutine 레퍼런스를 추적하여 중복 실행을 차단하고, 포탑 설치 중이더라도
///      웨이브 진입 즉시 정상적인 충전 사이클이 작동하도록 보장합니다.
/// </summary>
public class PlayerBomb : MonoBehaviour
{
    [Header("Bomb Settings")]
    [Tooltip("설치할 폭탄 프리팹")]
    [SerializeField] private GameObject bombPrefab;

    [Tooltip("폭탄 최대 소지 개수")]
    [SerializeField] private int maxBombCount = 5;

    [Tooltip("현재 보유 중인 폭탄 개수")]
    [SerializeField] private int curBombCount = 5;

    [Header("Recharge Timing")]
    [Tooltip("폭탄 1개 충전에 필요한 카운트 (레거시 기본값 500)")]
    [SerializeField] private int bombElapsedMax = 500;

    [Tooltip("현재 충전 누적 카운트")]
    [SerializeField] private int bombElapsed = 0;

    [Tooltip("현재 자동 충전 루프가 실행 중인지 여부")]
    [SerializeField] private bool isCharging = false;

    [Header("Events")]
    [Tooltip("폭탄 충전 진행도가 오를 때마다 발생하는 이벤트 (UI 게이지 갱신용)")]
    public UnityEvent onBombRecharge;

    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;

    private Coroutine rechargeCoroutine;
    private bool prevWaveState = false;

    public int CurBombCount => curBombCount;
    public int MaxBombCount => maxBombCount;

    private void Reset()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();

        curBombCount = maxBombCount;
        isCharging = false;
        bombElapsed = 0;
    }

    private void Start()
    {
        // 1. InGameManager UI 충전 게이지 연동 이벤트 등록
        onBombRecharge.RemoveAllListeners();
        onBombRecharge.AddListener(() =>
        {
            if (GeneralManager.Instance?.inGameManager != null)
            {
                GeneralManager.Instance.inGameManager.ChargeBombImage(bombElapsed, bombElapsedMax);
            }
        });

        // 2. 시작 시 즉시 UI 폭탄 개수 및 게이지 동기화
        ResetBombsToMax();
    }

    private void Update()
    {
        // 1. 웨이브 시작 상태 변화 감지 (포탑 준비 단계 -> 버튼 클릭 후 웨이브 돌입)
        CheckWaveTransition();

        if (inputHandler == null) return;

        // 2. 현재 상태(대화 중, 일시정지, 웨이브 비활성 등) 검사
        if (!CanUseBombInCurrentState()) return;

        // 3. E키 입력 시 폭탄 설치
        if (inputHandler.WasBombPressed)
        {
            PlaceBomb();
        }

        // 4. 폭탄이 최대치 미만이고 충전 중이 아니면 충전 루프 개시
        if (!isCharging && curBombCount < maxBombCount)
        {
            if (rechargeCoroutine != null) StopCoroutine(rechargeCoroutine);
            rechargeCoroutine = StartCoroutine(RechargeBombCoroutine());
        }
    }

    /// <summary>
    /// 포탑 설치 단계가 끝나고 유저가 버튼을 눌러 웨이브(isWave=true)가 시작되는 순간을 감지
    /// </summary>
    private void CheckWaveTransition()
    {
        if (GeneralManager.Instance?.inGameManager == null) return;

        bool currentWaveState = GeneralManager.Instance.inGameManager.isWave;

        // 준비 단계(false)였다가 버튼을 눌러 웨이브(true)로 진입한 첫 프레임
        if (!prevWaveState && currentWaveState)
        {
            ResetBombsToMax();
        }

        prevWaveState = currentWaveState;
    }

    /// <summary>
    /// 포탑 설치 후 또는 게임 시작 시 폭탄 개수를 최대치로 리셋하고 UI를 완충 상태로 갱신
    /// </summary>
    public void ResetBombsToMax()
    {
        curBombCount = maxBombCount;
        bombElapsed = 0;
        isCharging = false;

        if (rechargeCoroutine != null)
        {
            StopCoroutine(rechargeCoroutine);
            rechargeCoroutine = null;
        }

        UpdateUIBombCount();
        onBombRecharge.Invoke(); // 게이지 원형 이미지 0으로 초기화
    }

    private bool CanUseBombInCurrentState()
    {
        if (GeneralManager.Instance?.inGameManager == null) return true;

        var igm = GeneralManager.Instance.inGameManager;
        if (igm.isTalking) return false;
        if (igm.pauseVisible) return false;
        if (!igm.isWave) return false; // 웨이브가 아닐 때는 폭탄 사용 불가

        return true;
    }

    private void PlaceBomb()
    {
        if (bombPrefab == null)
        {
            Debug.LogWarning("[PlayerBomb] Bomb Prefab이 연결되지 않았습니다.");
            return;
        }

        if (curBombCount > 0)
        {
            Instantiate(bombPrefab, transform.position, Quaternion.identity);
            curBombCount--;
            UpdateUIBombCount();

            // 사용 후 바로 충전 코루틴이 없으면 실행되도록 유도
            if (!isCharging && curBombCount < maxBombCount)
            {
                if (rechargeCoroutine != null) StopCoroutine(rechargeCoroutine);
                rechargeCoroutine = StartCoroutine(RechargeBombCoroutine());
            }
        }
    }

    private IEnumerator RechargeBombCoroutine()
    {
        isCharging = true;

        while (curBombCount < maxBombCount)
        {
            // 웨이브 중이 아니거나 일시정지인 경우 충전 타이머 일시 정지 대기
            if (!CanUseBombInCurrentState())
            {
                yield return null;
                continue;
            }

            bombElapsed++;
            onBombRecharge.Invoke();

            if (bombElapsed >= bombElapsedMax)
            {
                bombElapsed = 0;
                curBombCount++;
                UpdateUIBombCount();
                onBombRecharge.Invoke();
            }

            yield return new WaitForSeconds(0.01f);
        }

        isCharging = false;
        rechargeCoroutine = null;
    }

    private void UpdateUIBombCount()
    {
        if (GeneralManager.Instance?.inGameManager != null)
        {
            GeneralManager.Instance.inGameManager.ValidateBombCount(curBombCount);
        }
    }
}