// 같은 Match에서 일정 간격으로 Encounter 명령을 하나씩 실행하는 Editor용 상태 머신입니다.
// 호출자가 전달한 clock delta만 사용하며 일시정지·지연 이후 몰아서 요청하지 않습니다.
// Match 변경, 실행 불가, 요청 실패 또는 유한 횟수 도달 시 명확한 이유로 중지합니다.
using System;

namespace TeamHJD.Game.Editor
{
    public sealed class EncounterRepeatScheduler
    {
        public delegate bool Request(out string result);
        public const double MinimumIntervalSeconds = 0.1;
        public const int MaximumFiniteRequests = 10000;
        private bool _isRequesting;
        public bool IsRunning { get; private set; }
        public string MatchId { get; private set; }
        public double IntervalSeconds { get; private set; }
        public double RemainingSeconds { get; private set; }
        public int MaximumRequests { get; private set; }
        public int AttemptedRequests { get; private set; }
        public int CompletedRequests { get; private set; }
        public string LastResult { get; private set; } = "연속 요청이 꺼져 있습니다.";
        public string StopReason { get; private set; }

        public bool Start(string matchId, double intervalSeconds, int maximumRequests)
        {
            if (IsRunning || _isRequesting) return false;
            if (string.IsNullOrWhiteSpace(matchId) || double.IsNaN(intervalSeconds) ||
                double.IsInfinity(intervalSeconds) || intervalSeconds < MinimumIntervalSeconds ||
                maximumRequests < 0 || maximumRequests > MaximumFiniteRequests)
            {
                StopReason = "Match와 유효한 간격·요청 횟수가 필요합니다.";
                return false;
            }
            MatchId = matchId;
            IntervalSeconds = RemainingSeconds = intervalSeconds;
            MaximumRequests = maximumRequests;
            AttemptedRequests = CompletedRequests = 0;
            LastResult = "첫 요청을 기다립니다.";
            StopReason = null;
            IsRunning = true;
            return true;
        }

        public void Stop(string reason)
        {
            IsRunning = false;
            StopReason = reason;
        }

        public void Tick(double deltaSeconds, string currentMatchId, bool isPlaying,
            bool isPaused, bool canRequest, string unavailableReason, Request request)
        {
            if (!IsRunning || _isRequesting) return;
            if (!isPlaying) { Stop("Play Mode 종료로 중지했습니다."); return; }
            if (!string.Equals(MatchId, currentMatchId, StringComparison.Ordinal))
            { Stop("Match 종료 또는 변경으로 중지했습니다."); return; }
            if (!canRequest || request == null)
            { Stop(string.IsNullOrEmpty(unavailableReason) ? "Encounter 실행 대상이 준비되지 않았습니다." : unavailableReason); return; }
            if (isPaused || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds <= 0) return;
            RemainingSeconds = Math.Max(0, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds > 0) return;

            // 여러 간격을 건너뛰어도 catch-up loop 대신 현재 시점의 최신 snapshot으로 한 번만 요청합니다.
            _isRequesting = true;
            AttemptedRequests++;
            try
            {
                bool succeeded = request(out string result);
                LastResult = result ?? (succeeded ? "Encounter 요청 완료." : "Encounter 요청 실패.");
                if (succeeded) CompletedRequests++;
                else { Stop("요청 실패: " + LastResult); return; }
            }
            catch (Exception exception)
            {
                LastResult = exception.GetType().Name + ": " + exception.Message;
                Stop("요청 예외: " + LastResult);
                return;
            }
            finally { _isRequesting = false; }

            if (!IsRunning) return;
            if (MaximumRequests > 0 && AttemptedRequests >= MaximumRequests)
            { Stop("지정한 요청 횟수를 완료했습니다."); return; }
            RemainingSeconds = IntervalSeconds;
        }
    }
}
