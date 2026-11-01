// 체크포인트의 영속 입출력 경계입니다. 캡처·복원·게임 이벤트 규칙을 소유하지 않습니다.
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointStore
    {
        /// <summary>실행 서비스가 캡처한 불변 자료를 저장합니다. 원자 교체가 끝난 뒤의 취소를 실패로 보고하지 않습니다.</summary>
        Task SaveAsync(CheckpointSlot slot, CheckpointData data, CancellationToken cancellationToken = default);
        /// <summary>명시적으로 선택한 슬롯을 읽습니다. 없으면 null, 손상/미지원 버전/IO 실패는 예외이며 새 게임으로 덮어쓰지 않습니다.</summary>
        Task<CheckpointData> LoadAsync(CheckpointSlot slot, CancellationToken cancellationToken = default);
    }
}
