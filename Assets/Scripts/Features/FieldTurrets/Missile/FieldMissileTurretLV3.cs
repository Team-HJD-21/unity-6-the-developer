using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "MissileTurretLV3")]
public class FieldMissileTurretLV3 : DefaultFieldMissileTurret
{
    [Header("References")]
    [SerializeField] private Transform[] missileSpawnPoint; //미사일 스폰 지점
    [SerializeField] private Transform[] missileTargetPoint; //미사일 스폰 지점
    public override int ProjectilesPerShot => missileSpawnPoint != null ? missileSpawnPoint.Length : 0;

    private void Start()
    {
        gunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
        Targets = new Transform[missileSpawnPoint.Length];
        
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
