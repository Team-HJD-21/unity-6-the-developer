using System;
using System.Collections;
using UnityEngine;

public class TowerBullet : MonoBehaviour
{
    [Header("Attributes")]
    [SerializeField] private float bulletSpeed; // 일정한 속도
    private float _bulletDamage; // 총알 피해량
    private Transform _target;

    private Vector3 _direction;
    private bool _isInitialized;

    public void Initialize(Transform target, float damage)
    {
        if (_isInitialized)
            throw new InvalidOperationException("TowerBullet has already been initialized.");
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (damage < 0f)
            throw new ArgumentOutOfRangeException(nameof(damage));

        _target = target;
        _bulletDamage = damage;
        _direction = (_target.position - transform.position).normalized; // 정규화된 방향 계산
        _isInitialized = true;
        StartCoroutine(DestroyObjectIfNotHit());
    }

    private void Update()
    {
        if (!_isInitialized) return;

        if (_target == null)
        {
            Destroy(gameObject); // 타겟이 없으면 총알 파괴
            return;
        }

        // 일정한 속도로 총알 이동
        transform.position += _direction * bulletSpeed * Time.deltaTime;

        // 총알의 회전 방향 설정
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    private IEnumerator DestroyObjectIfNotHit()
    {
        yield return new WaitForSeconds(3f);
        Destroy(gameObject); // 일정 시간이 지나면 총알 파괴
    }

    // 충돌 처리
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Monster monster = collision.gameObject.GetComponent<Monster>();
        if (monster != null)
        {
            monster.TakeDamage(_bulletDamage);
        }

        Destroy(gameObject); // 충돌 시 총알 파괴
    }
}
