using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CanonTurretLv1 : DefaultCanonTurret
{   
    [Header("References")]
    [SerializeField] private Transform bulletSpawnPoint;    //총알 스폰 지점
    [SerializeField] private Transform bulletFirePoint;
    
    private void Start()
    {
        GunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
        //Turrets Attack Range
        rangeTransform.localScale = new Vector3(Range*2.5f, Range*2.5f, 1f);
    }
    override 
    protected void Shoot()//총알 객체화 후 목표로 발사(FireRateController에서 수행)
    {
        animator.enabled = true; // 발사할 때 애니메이션 시작
        GameObject bulletObj = Instantiate(bulletPrefab, bulletSpawnPoint.position, Quaternion.identity);
        TowerBullet towerBulletScript = bulletObj.GetComponent<TowerBullet>();
        float randomX = bulletFirePoint.position.x + Random.Range(-0.5f, 0.5f);
        bulletFirePoint.position = new Vector3(randomX, bulletFirePoint.position.y,0f);
        towerBulletScript.Initialize(bulletFirePoint, Damage);
        // Collider2D player = Physics2D.OverlapCircle(transform.position, 40, playerMask);
        // Debug.Log(player);
        // if (player != null)
        // {
        //     AudioManager.Instance.PlaySfx(AudioManager.Sfx.Fire);
        // } 
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
