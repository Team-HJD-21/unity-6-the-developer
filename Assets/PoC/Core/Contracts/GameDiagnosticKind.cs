// 게임 정책과 구분되는 관측 사실의 종류입니다. 제거는 처치를 뜻하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public enum GameDiagnosticKind { MatchStarted, MatchEnded, RequestStarted, RequestCompleted, RequestFailed, EnemySpawned, EnemyRemoved, EnemyDied }
}
