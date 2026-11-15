// 제작된 미사일 발사 위치와 레벨별 무기 구성을 공통 터렛 실행에 연결합니다.
using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "MissileTurretLV1")]
public class FieldMissileTurretLV1 : DefaultFieldMissileTurret
{   
    [Header("References")]
    // [SerializeField] private Transform missileSpawnPoint;   //미사일 스폰 지점
    // [SerializeField] private Transform missileSpawnPoint2;  //미사일 스폰 지점
    [SerializeField] private Transform []missileSpawnPoint;
    public override int ProjectilesPerShot => missileSpawnPoint != null ? missileSpawnPoint.Length : 0;
    private void Start()
    {
        Targets ??= new Transform[missileSpawnPoint.Length];
        if (!WasCheckpointRestored) gunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
    }
    protected override void Shoot() => LaunchMissiles(missileSpawnPoint);
}
