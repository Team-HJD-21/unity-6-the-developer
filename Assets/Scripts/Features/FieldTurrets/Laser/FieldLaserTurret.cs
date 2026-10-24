using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "LaserTurret")]
public class FieldLaserTurret : DefaultFieldLaserTurret
{   
    [Header("References")]
    [SerializeField] private Transform turretRotationPoint; // 타워 회전 각도
    [SerializeField] private LayerMask enemyMask;           //raycast 감지 Layer
    [SerializeField] private Animator animator;             //타워 부분 Animator
    [SerializeField] private GameObject laserPrefab;       //총알 오브젝트 생성 위한 변수
    [SerializeField] private Transform laserSpawnPoint;    //총알 스폰 지점
    [SerializeField] private SpriteRenderer gunRenderer;    //과열시 색 변화
    //[SerializeField] private Transform bulletFirePoint;
    [SerializeField] private SpriteRenderer rangeRenderer;
    [SerializeField] private Transform rangeTransform;
    
    [Header("Attributes")]
    [SerializeField, Min(0)] private int damage = 12;
    [SerializeField] private float range;        // 타워 사거리
    [SerializeField] private float rotationSpeed;// 타워 회전 속도
    [SerializeField] private float fireRate;       // 발사 속도, 충격발 애니메이션이랑 연동시키기? ㄱㄴ?
    [SerializeField] private int power;            //타워 사용 전력량
    [SerializeField] private float overHeatTime;    //~초 격발시 과열
    [SerializeField] private float coolTime;        //~초 지나면 냉각
    [SerializeField] private bool showRange;
  
    
    [Header("Laser Settings")]
    [SerializeField] private float growDuration = 1.0f;  // 커지는 데 걸리는 시간
    [SerializeField] private float shrinkDuration = 1.0f; // 작아지는 데 걸리는 시간
    [SerializeField] private float targetYScale = 30f;    // 목표 스케일
    [SerializeField] private float initialYScale = 0.14f; // 초기 스케일
    [SerializeField] private bool _nowShooting;
    private void Start()
    {
        GunRenderer = gunRenderer;
        Animator = animator;
        TurretRotationPoint = turretRotationPoint;
        Range = range;         // 타워 사거리
        RotationSpeed = rotationSpeed;// 타워 회전 속도
        FireRate = fireRate;       // 발사 속도, 충격발 애니메이션이랑 연동시키기? ㄱㄴ?
        Power = power;            //타워 사용 전력량
        OverHeatTime = overHeatTime;    //~초 격발시 과열
        CoolTime = coolTime; //~초 지나면 냉각
        GunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
        EnemyMask = enemyMask;
        RangeRenderer = rangeRenderer;
        //Turrets Attack Range
        rangeTransform.localScale = new Vector3(Range*2.5f, Range*2.5f, 1f);
        //Info for Ui
        Level = 1;
        RPM = (int)(60 / (1 / fireRate));
        Damage = damage;
        _nowShooting = false;
    }
    override 
    protected void Shoot()//총알 객체화 후 목표로 발사(FireRateController에서 수행)
    {
        animator.enabled = true; // 발사할 때 애니메이션 시작


        if (!_nowShooting)
        {
            Debug.Log("shooting started");
            StartCoroutine(AnimateLaserScale(laserPrefab));
        }

        // GameObject bulletObj = Instantiate(laserPrefab, laserSpawnPoint.position, Quaternion.identity);
        // TowerBullet towerBulletScript = bulletObj.GetComponent<TowerBullet>();
        // // float randomX = bulletFirePoint.position.x + Random.Range(-0.5f, 0.5f);
        // // bulletFirePoint.position = new Vector3(randomX, bulletFirePoint.position.y,0f);
        // // towerBulletScript.SetTarget(bulletFirePoint);   
        // // Collider2D player = Physics2D.OverlapCircle(transform.position, 40, playerMask);
        // // Debug.Log(player);
        // // if (player != null)
        // // {
        // //     AudioManager.Instance.PlaySfx(AudioManager.Sfx.Fire);
        // // } 
    }
    private IEnumerator AnimateLaserScale(GameObject laser)
    {
        if (laser == null) yield break;
        _nowShooting = true;
        enableRotation = false;
        Transform laserTrans = laser.transform;
        Vector3 currentScale = laserTrans.localScale;

        // 1. 천천히 커지기 (30까지)
        float elapsed = 0f;
        while (elapsed < growDuration)
        {
            if (laser == null) yield break; // 파괴 체크
            elapsed += Time.deltaTime;
            float newY = Mathf.Lerp(initialYScale, targetYScale, elapsed / growDuration);
            laserTrans.localScale = new Vector3(currentScale.x, newY, currentScale.z);
            yield return null;
        }
        laserTrans.localScale = new Vector3(currentScale.x, targetYScale, currentScale.z);

        // 2. 10초 대기
        yield return new WaitForSeconds(10f);

        // 3. 다시 천천히 줄어들기 (0.14까지)
        elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            if (laser == null) yield break;
            elapsed += Time.deltaTime;
            float newY = Mathf.Lerp(targetYScale, initialYScale, elapsed / shrinkDuration);
            laserTrans.localScale = new Vector3(currentScale.x, newY, currentScale.z);
            yield return null;
        }
        laserTrans.localScale = new Vector3(currentScale.x, initialYScale, currentScale.z);
        _nowShooting = false;
        enableRotation = true;

    }
    private void OnDrawGizmosSelected()//타워의 반경 그려줌(디버깅용, 인게임에는 안나옴)
    {
#if UNITY_EDITOR
        Handles.color = Color.magenta;
        Handles.DrawWireDisc(transform.position, transform.forward, range);
#endif
    }
}
