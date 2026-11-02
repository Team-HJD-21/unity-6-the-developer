// Scene 제작 또는 권한 생성자가 부여한 논리 ID를 읽는 Port입니다.
// Definition ID·로컬 InstanceId·NetworkObjectId와 혼용하지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEntityIdentity { EntityId EntityId { get; } }
}
