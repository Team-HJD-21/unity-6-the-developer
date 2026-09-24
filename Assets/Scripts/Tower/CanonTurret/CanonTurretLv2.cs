using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CanonTurretLv2 : DefaultCanonTurret
{   
    [Header("References")]
    [SerializeField] private Transform[] bulletSpawnPoint;    //총알 스폰 지점
    [SerializeField] private Transform []bulletFireDirection;    //총 격발 방향
    

    private GameObject []_bulletObj;
    
    private void Start()
    {
        _bulletObj = new GameObject[bulletSpawnPoint.Length];
        GunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
        //Turrets Attack Range
        rangeTransform.localScale = new Vector3(Range*2.5f, Range*2.5f, 1f);
    }
    override 
    protected void Shoot()//총알 객체화 후 목표로 발사(FireRateController에서 수행)
    {
        animator.enabled = true; // 발사할 때 애니메이션 시작
        for (int i = 0; i < _bulletObj.Length; i++)
        {
            _bulletObj[i] = Instantiate(bulletPrefab, bulletSpawnPoint[i].position, Quaternion.identity);
            TowerBullet towerBulletScript = _bulletObj[i].GetComponent<TowerBullet>();
            towerBulletScript.SetDamage(Damage);
            float randomValue = Random.Range(-0.5f, 0.5f);
            bulletFireDirection[i].position = new Vector3(bulletFireDirection[i].position.x+randomValue, bulletFireDirection[i].position.y,0f);
            towerBulletScript.SetTarget(bulletFireDirection[i]);
            // Collider2D player = Physics2D.OverlapCircle(transform.position, 40, playerMask);
            // Debug.Log(player);
            // if (player != null)
            // {
            //     AudioManager.Instance.PlaySfx(AudioManager.Sfx.Fire);
            // } 
        }
    }
    
    private void OnDrawGizmosSelected()//타워의 반경 그려줌(디버깅용, 인게임에는 안나옴)
    {
#if UNITY_EDITOR
        Handles.color = Color.magenta;
        if (Definition != null)
            Handles.DrawWireDisc(transform.position, transform.forward, Range);
#endif
    }
}
