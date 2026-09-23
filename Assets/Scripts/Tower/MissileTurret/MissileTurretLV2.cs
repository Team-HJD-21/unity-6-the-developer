using System.Collections;
using System.Collections.Generic;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MissileTurretLV2 : DefaultMissileTurret
{   
    [Header("References")]
    // [SerializeField] private Transform missileSpawnPoint;    //미사일 스폰 지점
    // [SerializeField] private Transform missileSpawnPoint2;    //미사일 스폰 지점
    // [SerializeField] private Transform missileSpawnPoint3;    //미사일 스폰 지점
    [SerializeField] protected Transform []missileSpawnPoint;    //미사일 스폰 지점
    [SerializeField] protected Transform []missileTargetPoint;    //미사일 스폰 지점
    
    //[SerializeField] private GameObject towerPrefab;
    
    private GameObject[] _missileObj;
    private void Start()
    {
        gunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
        _missileObj = new GameObject[missileSpawnPoint.Length];
        Targets = new Transform[4];
        //Turrets Attack Range
        rangeTransform.localScale = new Vector3(Range*2.5f, Range*2.5f, 1f);
        //Info for UI
        Damage = 20;
    }
    protected override void Shoot()
    {
        CurMissileCount += 1;
        StartCoroutine(ShootAnimation());
        for (int i = 0; i < _missileObj.Length; i++)
        {
            if (Targets[i] != null)
            {
                _missileObj[i] = Instantiate(missilePrefab, missileSpawnPoint[i].position, turretRotationPoint.rotation);
                TowerMissile missileScript = _missileObj[i].GetComponent<TowerMissile>();
                missileScript.SetTarget(Targets[i]);
            }
        }
        for (var i = 0; i < _missileObj.Length; i++)
        {
            Targets[i] = null;
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
