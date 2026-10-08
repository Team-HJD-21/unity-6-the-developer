using System.Collections;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;

public class AdcBullet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    
    [Header("Attributes")] 
    [SerializeField] private float bulletSpeed = 17f;
    [SerializeField] private int bulletDamage = 30;
    [SerializeField] private Transform bulletSpawnPoint;

    private IDamageableTarget controlUnit;
    
    private Vector2 direction;
    
    // 방향을 설정하는 메서드
    public void SetDirection(Vector2 dir)
    {
        // Debug.Log("bullettargeted");
        direction = dir.normalized;
        StartCoroutine(DestroyObjectIfNotHit());
    }

    private void FixedUpdate()
    {
        if (direction == Vector2.zero) return;

        // Rigidbody2D의 속도를 방향과 속도에 맞게 설정
        rb.linearVelocity = direction * bulletSpeed;

        // 총알의 회전 설정 (방향 벡터 기준)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    private IEnumerator DestroyObjectIfNotHit()
    {
        yield return new WaitForSeconds(5f);
        Destroy(gameObject);
    }

    public void SetControlUnitTarget(IDamageableTarget target)
    {
        controlUnit = target;
    }

    // 충돌 시 총알 파괴
    private void OnCollisionEnter2D(Collision2D collision)
    {   
        PlayerInfo player = collision.gameObject.GetComponent<PlayerInfo>();
        if (player != null)
        {
            player.TakeDamage(bulletDamage);
        }
        
        TargetableComponent targetable = collision.gameObject.GetComponentInParent<TargetableComponent>();
        if (targetable != null)
        {
            targetable.TryApplyDamage(bulletDamage);
        }
        Destroy(gameObject);
    }
}
