using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Random = Unity.Mathematics.Random;

public class Player : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 0.3f;
    [SerializeField] private float pushAmount = 0.02f; // 충돌 시 밀어내는 크기
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private float fireRate;
    [SerializeField] private GameObject bombPrefab; // 폭탄 프리팹 추가
    public Tilemap map;
    public float attackDelay;
    public bool attackAble;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;
    private Vector2 _mouse;
    private Vector3 sumVector;
    private Transform _mouseTransform;
    private float _timeTilFire;
    private int _bulletsPerShot; // 한 번에 발사할 총알 수
    private float _spreadAngle; // 총알 퍼짐 각도

    [Header("Bomb")] 
    public int maxBombCount;
    public int curBombCount;
    public bool isCharging;
    public int bombElapsed;
    public int bombElapsedMax = 500;
    public UnityEvent onBombRecharge;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _bulletsPerShot = DataManager.GetAttributeData(AttributeType.PlayerBullet);
        _spreadAngle = _bulletsPerShot * 5;

        //  최대 소지 개수
        maxBombCount = 5;
        curBombCount = maxBombCount;
        isCharging = false;
    }

    private void Start()
    {
        if (_rb)
        {
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Continuous로 설정
        }
        
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        _mouse = Camera.main.ScreenToWorldPoint(GameInput.PointerPosition);

        if (map == null)
        {
            map = FindFirstObjectByType<Tilemap>();
        }

        attackAble = true;
        
        onBombRecharge.AddListener(()=>GeneralManager.Instance.inGameManager.ChargeBombImage(bombElapsed, bombElapsedMax));
    }

    private void FixedUpdate()
    {
        if (!GeneralManager.Instance.inGameManager.isTalking)
        {
            if (GeneralManager.Instance.inGameManager.isWave)
            {
                PlayerMove();
            }
        }

        if (!isCharging)
        {
            if (curBombCount < maxBombCount)
            {
                StartCoroutine(RechargeBombCoroutine());
            }
        }
    }

    private void PlayerMove()
    {
        sumVector = Vector3.zero;

        // 키 입력 처리
        if (GameInput.IsPressed(GameKey.UpArrow)) sumVector += Vector3.up * moveSpeed;
        if (GameInput.IsPressed(GameKey.LeftArrow)) sumVector += Vector3.left * moveSpeed;
        if (GameInput.IsPressed(GameKey.DownArrow)) sumVector += Vector3.down * moveSpeed;
        if (GameInput.IsPressed(GameKey.RightArrow)) sumVector += Vector3.right * moveSpeed;
        if (GameInput.IsPressed(GameKey.W)) sumVector += Vector3.up * moveSpeed;
        if (GameInput.IsPressed(GameKey.A)) sumVector += Vector3.left * moveSpeed;
        if (GameInput.IsPressed(GameKey.S)) sumVector += Vector3.down * moveSpeed;
        if (GameInput.IsPressed(GameKey.D)) sumVector += Vector3.right * moveSpeed;

        // 이동 처리
        transform.position += sumVector * Time.fixedDeltaTime;
        // _rb.velocity = sumVector.normalized * moveSpeed;
        // 경계 내에서 위치 클램프
        Bounds tilemapBounds = map.GetComponent<Renderer>().bounds;
        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, tilemapBounds.min.x, tilemapBounds.max.x),
            Mathf.Clamp(transform.position.y, tilemapBounds.min.y, tilemapBounds.max.y),
            transform.position.z
        );
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 충돌 방향 계산
            Vector3 pushDirection = collision.contacts[0].normal;

            // 플레이어를 충돌 방향 반대쪽으로 밀어냄
            transform.position += pushDirection * pushAmount;

            // Debug.Log($"Collision Enter: Push Direction = {pushDirection}");
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 충돌 방향 계산
            Vector3 pushDirection = collision.contacts[0].normal;

            // 충돌 지속 시에도 플레이어를 충돌 반대쪽으로 계속 밀어냄
            transform.position += pushDirection * pushAmount;

            // Debug.Log($"Collision Stay: Push Direction = {pushDirection}");
        }
    }

    private void Update()
    {
        // 마우스의 월드 좌표를 가져옴
        _mouse = Camera.main.ScreenToWorldPoint(GameInput.PointerPosition);

        // 방향에 따라 애니메이션 파라미터를 설정
        if (_animator != null)
        {

            if (!GeneralManager.Instance.inGameManager.pauseVisible)
            {
                if (GeneralManager.Instance.inGameManager.isWave)
                {
                    CheckDirectionToMouse();
                }
            }
        }


        if (!GeneralManager.Instance.inGameManager.isTalking)
        {
            if (GeneralManager.Instance.inGameManager.isWave)
            {
                PlayerAttack();    
            }
        }
    }

    private void PlayerAttack()
    {
        _timeTilFire += Time.deltaTime;
        if (_timeTilFire >= (1f / fireRate))
        {
            if (GameInput.IsPressed(GameKey.Space))
            {
                AudioManager.Instance.PlaySfx(AudioManager.Sfx.PlayerBullet);
                Vector2 baseDirection = (_mouse - (Vector2)bulletSpawnPoint.position).normalized;
                float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;

                int bulletsToFire = _bulletsPerShot;
                float angleStep = _spreadAngle / (bulletsToFire - 1);
                float startAngle = baseAngle - _spreadAngle / 2;

                for (int i = 0; i < bulletsToFire; i++)
                {
                    // Debug.Log("Shot!");
                    float currentAngle = startAngle + angleStep * i;
                    float radianAngle = currentAngle * Mathf.Deg2Rad;

                    Vector2 bulletDirection = new Vector2(Mathf.Cos(radianAngle), Mathf.Sin(radianAngle));

                    GameObject bulletObj = Instantiate(bulletPrefab, bulletSpawnPoint.position, Quaternion.identity);
                    PlayerBullet playerBulletScript = bulletObj.GetComponent<PlayerBullet>();
                    // Debug.Log(_rb.velocity);
                    playerBulletScript.SetDirection(bulletDirection,sumVector);
                }
                _timeTilFire = 0f;
            }
            
            //  똑같이 쿨타임 존재
            if (GameInput.WasPressedThisFrame(GameKey.E)) PlaceBomb();
        }
    }
    // StartCoroutine(PlayerAttackCoroutine());
        
    
    private void PlaceBomb()//폭탄 설치
    {
        // 플레이어의 현재 위치에 폭탄을 생성합니다.
        if (bombPrefab != null)
        {
            if (curBombCount > 0)
            {
                Instantiate(bombPrefab, transform.position, Quaternion.identity);
                curBombCount--;
                
                GeneralManager.Instance.inGameManager.ValidateBombCount(curBombCount);
            }
        }
    }

    private IEnumerator RechargeBombCoroutine()
    {
        isCharging = true;
        bombElapsed = 0;
        
        while (true)
        {
            if (bombElapsed > bombElapsedMax)
            {
                isCharging = false;
                curBombCount++;
                
                GeneralManager.Instance.inGameManager.ValidateBombCount(curBombCount);
                
                yield break;
            }
            
            bombElapsed++;
            onBombRecharge.Invoke();
            
            yield return new WaitForSeconds(0.01f);
        }
    }
    
    private IEnumerator PlayerAttackCoroutine()
    {
        attackAble = false;
        yield return new WaitForSeconds(attackDelay);
        attackAble = true;
    }

    private void CheckDirectionToMouse()
    {
        Vector2 direction = _mouse - (Vector2)transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (angle > -45 && angle <= 45)
        {
            SetAnimationState("isSide", true, false, false);
            _spriteRenderer.flipX = true;
        }
        else if (angle > 45 && angle <= 135)
        {
            SetAnimationState("isUp", false, false, true);
            _spriteRenderer.flipX = false;
        }
        else if (angle > -135 && angle <= -45)
        {
            SetAnimationState("isIdle", false, true, false);
            _spriteRenderer.flipX = false;
        }
        else
        {
            SetAnimationState("isSide", true, false, false);
            _spriteRenderer.flipX = false;
        }
    }

    private void SetAnimationState(string activeState, bool side, bool idle, bool up)
    {
        _animator.SetBool("isSide", side);
        _animator.SetBool("isIdle", idle);
        _animator.SetBool("isUp", up);
    }
}
