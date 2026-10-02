using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

/// <summary>
/// [InGameManager 호환용 어댑터 및 레거시 원본 보존 스크립트]
/// InGameManager.Start()에서 player.GetComponent<Player>().maxBombCount를 직접 읽어가므로
/// 기본값을 5로 활성화하여 UI 초기값 0 버그를 원천 차단합니다.
/// </summary>
public class Player : MonoBehaviour
{
    [Header("--- Legacy Compatibility (InGameManager 연동용 활성 필드) ---")]
    [Tooltip("InGameManager.Start()에서 초기화할 때 읽어가는 폭탄 최대 개수")]
    public int maxBombCount = 5;

    [Tooltip("현재 보유 폭탄 개수")]
    public int curBombCount = 5;

    [Header("Legacy Fields (레거시 보존)")]
    public float moveSpeed = 0.3f;
    public float pushAmount = 0.02f;
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public float fireRate;
    public float attackDelay;
    public bool attackAble;
    public GameObject bombPrefab;
    public Tilemap map;
    public bool isCharging;
    public int bombElapsed;
    public int bombElapsedMax = 500;
    public UnityEvent onBombRecharge;

    // 라이프사이클(Awake, Start, Update, FixedUpdate 등)은 Player_V2와의 간섭 방지를 위해 주석 처리됨
}