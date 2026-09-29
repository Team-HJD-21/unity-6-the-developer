using UnityEngine;


/// <summary>
/// 적을 생성할 수 있는 씬상의 고정 위치를 나타낸다.
/// 생성 대상과 수량은 판단하지 않고 위치 정보만 제공한다.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("식별 정보")]
    [SerializeField] private string spawnPointId;
    [SerializeField] private string areaId;

    /// <summary>
    /// 스폰 지점을 식별하는 고유 값을 반환한다.
    /// </summary>
    public string SpawnPointId => spawnPointId;

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

    // TODO: Ground, Air 등 이 SpawnPoint에서 생성 가능한 Enemy 타입을 추가한다.
}
