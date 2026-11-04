// Scene의 필드 Feature가 게임 시간 읽기와 정지 수명을 임대하는 접점입니다.
using System;
using System.Threading;
namespace TeamHJD.Game.Contracts
{
    public interface IFieldSimulationConsumer
    {
        /// <summary>권위 조립부가 Match 생성 시 호출합니다. 반환 lease를 Feature 해제 전에 Dispose합니다.</summary>
        /// <remarks>Feature는 주입된 시계로 공격·수명·쿨다운을 처리하고 생성한 자식 개체에도 전달합니다.</remarks>
        IDisposable BindFieldSimulation(IMatchTime time, CancellationToken gameplay);
    }
}
