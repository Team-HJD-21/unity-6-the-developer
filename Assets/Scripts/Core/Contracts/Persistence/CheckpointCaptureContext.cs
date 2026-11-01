// 한 번의 정지된 캡처가 공유하는 실행 입력과 게임 시각입니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class CheckpointCaptureContext
    {
        public MatchConfig Config { get; }
        public double GameSeconds { get; }
        public Guid Epoch { get; }
        public CheckpointCaptureContext(MatchConfig config, double gameSeconds, Guid epoch)
        { Config = config ?? throw new ArgumentNullException(nameof(config)); GameSeconds = gameSeconds; Epoch = epoch; }
    }
}
