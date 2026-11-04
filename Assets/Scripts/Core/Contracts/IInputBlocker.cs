// 로컬 화면·전환이 소유한 입력 잠금을 token 수명으로 관리합니다.
// 사망·Host gameplay 권한이나 게임 시간 pause를 대신하지 않습니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface IInputBlocker
    {
        bool IsBlocked { get; }
        IDisposable Acquire(string owner);
    }
}
