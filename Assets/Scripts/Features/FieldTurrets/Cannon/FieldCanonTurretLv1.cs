// 발사 직후 생성 탄환에 현재 터렛의 Match 시간과 종료 수명을 전달합니다.
using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "CanonTurretLv1")]
public class FieldCanonTurretLv1 : DefaultFieldCanonTurret
{   
    [Header("References")]
    [SerializeField] private Transform bulletSpawnPoint;    //총알 스폰 지점
    [SerializeField] private Transform bulletFirePoint;
    public override int ProjectilesPerShot => bulletSpawnPoint != null ? 1 : 0;
    
    private void Start()
    {
        if (!WasCheckpointRestored) GunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
    }
    override 
    protected void Shoot()//총알 객체화 후 목표로 발사(FireRateController에서 수행)
    {
        animator.enabled = true; // 발사할 때 애니메이션 시작
        GameObject bulletObj = Instantiate(bulletPrefab, bulletSpawnPoint.position, Quaternion.identity);
        BindProjectileSimulation(bulletObj);
        FieldTurretBullet fieldTurretBullet = bulletObj.GetComponent<FieldTurretBullet>();
        float randomX = bulletFirePoint.position.x + NextScatterOffset(-0.5f, 0.5f);
        Vector3 aimPosition = new Vector3(randomX, bulletFirePoint.position.y, 0f);
        fieldTurretBullet.Initialize(bulletFirePoint, Damage, aimPosition);
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
