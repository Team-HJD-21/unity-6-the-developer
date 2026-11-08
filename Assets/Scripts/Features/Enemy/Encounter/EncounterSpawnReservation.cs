// Enemy Feature의 지연 요청 핸들입니다. 대기 수량은 아직 생존 Enemy 집계에 포함하지 않습니다.
// 취소와 최종 요청 결과를 구분하며 실제 실행은 공통 Encounter 요청 서비스를 통합니다.
using System;
using TeamHJD.Game.Contracts;
/// <summary>
/// 지연 생성 요청의 대기·취소 상태와 최종 실행 결과를 보관한다.
/// </summary>
public sealed class EncounterSpawnReservation
{
    public Guid RequestId { get; }
    public double DueGameSeconds { get; }
    public bool IsPending { get; private set; } = true;
    public bool IsCancelled { get; private set; }
    public string Reason { get; private set; } = "";
    public EncounterRequestResult Result { get; private set; }
    internal EncounterSpawnReservation(Guid id, double due) { RequestId = id; DueGameSeconds = due; }
    /// <summary>
    /// 아직 실행되지 않은 요청을 호출자 사유로 취소한다.
    /// </summary>
    public void Cancel() => Cancel("Caller cancelled the pending request.");
    internal void Cancel(string reason)
    {
        if (!IsPending) return;
        IsPending = false; IsCancelled = true; Reason = reason;
    }
    /// <summary>
    /// 대기 중인 요청에 최종 실행 결과를 기록하고 대기를 종료한다.
    /// </summary>
    internal void Complete(EncounterRequestResult result)
    {
        if (!IsPending) return;
        Result = result ?? throw new ArgumentNullException(nameof(result));
        IsPending = false; Reason = result.Reason;
    }
}
