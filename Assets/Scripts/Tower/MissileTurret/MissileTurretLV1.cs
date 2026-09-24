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
    private GameObject []_missileObj;
    private void Start()
    {
        Targets = new Transform[missileSpawnPoint.Length];
        gunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
        _missileObj = new GameObject[missileSpawnPoint.Length];
        //Turrets Attack Range
        rangeTransform.localScale = new Vector3(Range*2.5f, Range*2.5f, 1f);
    }
    protected override void Shoot()
    {
        // Debug.Log("shooting now");
        RecordMissileLaunch();
        StartCoroutine(ShootAnimation());
        for (int i = 0; i < _missileObj.Length; i++)
        {
            if (Targets[i] != null)
            {
                _missileObj[i] = Instantiate(missilePrefab, missileSpawnPoint[i].position, turretRotationPoint.rotation);
                TowerMissile missileScript = _missileObj[i].GetComponent<TowerMissile>();
                missileScript.Initialize(Targets[i], Damage);
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
