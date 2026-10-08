// Scene의 적 생성 지점과 점령 상태를 제공합니다.
// 복제된 지점도 등록 시 고유 ID를 부여받으며 종류·수량 결정은 Planner가 담당합니다.
using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 적 스폰을 위한 씬상의 고정 위치와 생성 가능 상태를 제공한다.
/// 생성할 적의 종류와 수량은 결정하지 않는다.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("식별 정보")]
    [SerializeField] private string spawnPointId;
    [SerializeField] private string areaId;
    // 런타임 복제는 authoring 값을 저장하지 않고 이 객체의 수명 동안 ID를 유지합니다.
    private string _runtimeSpawnPointId;
    private string _runtimeAuthoredId;

    [Header("생성 영역")]
    [SerializeField, Min(0f)] private float spawnRadius = 1f;

    [Header("점령 상태")]
    [SerializeField] private bool canSpawnEnemies = true;

    /// <summary>
    /// 스폰 지점을 식별하는 고유 값을 반환한다.
    /// </summary>
    public string SpawnPointId => _runtimeSpawnPointId != null && _runtimeAuthoredId == spawnPointId
        ? _runtimeSpawnPointId : spawnPointId;

    /// <summary>레지스트리 안에서 빈 ID/복제 ID를 구분하고, 기존의 유효한 ID는 유지합니다.</summary>
    internal void EnsureUniqueRegistrationId(HashSet<string> registeredIds)
    {
        if (!string.IsNullOrWhiteSpace(SpawnPointId) && registeredIds.Add(SpawnPointId)) return;
        _runtimeAuthoredId = spawnPointId;
        do { _runtimeSpawnPointId = "SpawnPoint-" + Guid.NewGuid().ToString("N"); }
        while (!registeredIds.Add(_runtimeSpawnPointId));
    }

    private void Reset()
    {
        spawnPointId = "SpawnPoint-" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 스폰 지점이 속한 전장 구역의 식별 값을 반환한다.
    /// </summary>
    public string AreaId => areaId;

    /// <summary>
    /// 몬스터를 생성할 월드 위치를 반환한다.
    /// </summary>
    public Vector3 Position => transform.position;

    /// <summary>
    /// 몬스터 생성 시 적용할 회전값을 반환한다.
    /// </summary>
    public Quaternion Rotation => transform.rotation;

    /// <summary>
    /// 스폰 위치를 무작위로 분산할 반지름을 반환한다.
    /// </summary>
    public float SpawnRadius => spawnRadius;

    /// <summary>
    /// 이 지점이 활성화되어 있고 적에게 점령되어 스폰 후보가 될 수 있는지 반환한다.
    /// </summary>
    public bool CanSpawnEnemies => isActiveAndEnabled && canSpawnEnemies;

    /// <summary>
    /// 점령 상태가 바뀌면 서버의 점령 관리자가 적 스폰 가능 여부를 갱신한다.
    /// 플레이어 점령 시 <see langword="false"/>, 적 재점령 시 <see langword="true"/>를 전달한다.
    /// </summary>
    /// <param name="enabled">이 지점에서 적 생성을 허용할지 여부.</param>
    public void SetEnemySpawnEnabled(bool enabled)
    {
        canSpawnEnemies = enabled;
    }

    /// <summary>
    /// Scene 뷰에 스폰 가능 영역을 원형 Gizmo로 표시한다.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
    
    // TODO: Ground, Air 등 이 SpawnPoint에서 생성 가능한 Enemy 타입을 추가한다.
}
