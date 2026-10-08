// Runtime의 읽기 전용 진단·최근 기록 계약입니다. Editor 타입을 참조하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IGameDiagnosticsSource
    {
        event Action<GameDiagnosticEvent> DiagnosticEmitted;
        IReadOnlyList<GameDiagnosticEvent> RecentDiagnostics { get; }
        long DroppedDiagnosticCount { get; }
        MatchId BoundMatchId { get; }
        double GameSeconds { get; }
        double RealSeconds { get; }
        int ActiveEnemyCount { get; }
        int UnattributedEnemyCount { get; }
        bool IsServer { get; }
        bool IsClient { get; }
        bool IsListening { get; }
    }
}
