using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MissileTurretLV1 : DefaultMissileTurret
{   
    [Header("References")]
    // [SerializeField] private Transform missileSpawnPoint;   //미사일 스폰 지점
    // [SerializeField] private Transform missileSpawnPoint2;  //미사일 스폰 지점
    [SerializeField] private Transform []missileSpawnPoint;
    public override int ProjectilesPerShot => missileSpawnPoint != null ? missileSpawnPoint.Length : 0;
    private void Start()
    {
        Targets = new Transform[missileSpawnPoint.Length];
        gunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
    }
    protected override void Shoot() => LaunchMissiles(missileSpawnPoint);
    private void OnDrawGizmosSelected()//타워의 반경 그려줌(디버깅용, 인게임에는 안나옴)
    {
#if UNITY_EDITOR
        Handles.color = Color.magenta;
        if (Definition != null)
            Handles.DrawWireDisc(transform.position, transform.forward, Range);
#endif
    }
}
