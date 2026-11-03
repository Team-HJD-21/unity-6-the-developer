// Host가 소유한 actor에서 조회한 조작 가능 여부와 XY 위치입니다.
// Client 명령 payload의 위치/생존 주장은 이 값의 원본으로 사용할 수 없습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public readonly struct PlayerInteractionSnapshot
    {
        public BattlefieldPoint Position { get; }
        public bool IsAlive { get; }
        public bool CanInteract { get; }
        public PlayerInteractionSnapshot(BattlefieldPoint position, bool isAlive, bool canInteract)
        { Position = position; IsAlive = isAlive; CanInteract = canInteract; }
    }
}
