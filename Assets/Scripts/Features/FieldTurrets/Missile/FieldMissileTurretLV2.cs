// 제작된 미사일 발사 위치와 레벨별 무기 구성을 공통 터렛 실행에 연결합니다.
using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "MissileTurretLV2")]
public class FieldMissileTurretLV2 : DefaultFieldMissileTurret
{   
    [Header("References")]
    // [SerializeField] private Transform missileSpawnPoint;    //미사일 스폰 지점
    // [SerializeField] private Transform missileSpawnPoint2;    //미사일 스폰 지점
    // [SerializeField] private Transform missileSpawnPoint3;    //미사일 스폰 지점
    [SerializeField] protected Transform []missileSpawnPoint;    //미사일 스폰 지점
    [SerializeField] protected Transform []missileTargetPoint;    //미사일 스폰 지점
    
    //[SerializeField] private GameObject towerPrefab;
    
    public override int ProjectilesPerShot => missileSpawnPoint != null ? missileSpawnPoint.Length : 0;
    private void Start()
    {
        if (!WasCheckpointRestored) gunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
        Targets ??= new Transform[missileSpawnPoint.Length];
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
