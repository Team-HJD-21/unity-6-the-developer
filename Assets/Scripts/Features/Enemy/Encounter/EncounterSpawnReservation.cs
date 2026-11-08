// Enemy Feature의 지연 요청 핸들입니다. 대기 수량은 아직 생존 Enemy 집계에 포함하지 않습니다.
// 취소와 최종 요청 결과를 구분하며 실제 실행은 공통 Encounter 요청 서비스를 통합니다.
using System;
using TeamHJD.Game.Contracts;
public sealed class EncounterSpawnReservation
{
    public Guid RequestId { get; }
    public double DueGameSeconds { get; }
    public bool IsPending { get; private set; } = true;
    public bool IsCancelled { get; private set; }
    public string Reason { get; private set; } = "";
    public EncounterRequestResult Result { get; private set; }
    internal EncounterSpawnReservation(Guid id, double due) { RequestId = id; DueGameSeconds = due; }
    public void Cancel() => Cancel("Caller cancelled the pending request.");
    internal void Cancel(string reason)
    {
        if (!IsPending) return;
        IsPending = false; IsCancelled = true; Reason = reason;
    }
    internal void Complete(EncounterRequestResult result)
    {
        if (!IsPending) return;
        Result = result ?? throw new ArgumentNullException(nameof(result));
        IsPending = false; Reason = result.Reason;
    }
}
