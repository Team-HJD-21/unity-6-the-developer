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
    
    public string SpawnPointId => spawnPointId;
    public string AreaId => areaId;
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

    // TODO: Ground, Air 등 이 SpawnPoint에서 생성 가능한 Enemy 타입을 추가한다.
    
}
