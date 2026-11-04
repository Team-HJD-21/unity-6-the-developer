// 한 Match의 저장 실행기·명시 요청 채널·관찰자와 writer 등록 수명을 조립합니다.
// 자동 저장 사건과 Feature 원본은 소유하지 않습니다. 생성한 조립부가 Match 종료 때 해제합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchCheckpointScope : IDisposable
    {
        private bool _disposed;
        public CheckpointService Service { get; }
        public CheckpointRequestChannel Requests { get; }
        public CheckpointTrigger Trigger { get; }
        public MatchId MatchId => Service.MatchId;

        /// <summary>Scene 조립부가 현재 권위 runtime, App 저장소와 실제 writer를 주입합니다. 인자 목록은 복사합니다.</summary>
        /// <remarks>requiredParticipantKeys는 콘텐츠 조립 계약입니다. 필수 writer가 빠지면 저장/복원과 이벤트 요청 모두 거부합니다.</remarks>
        public MatchCheckpointScope(ICheckpointRuntime runtime, ICheckpointStore store, ICheckpointPayloadCodec codec,
            IEnumerable<ICheckpointParticipant> participants, IEnumerable<string> requiredParticipantKeys)
        {
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (requiredParticipantKeys == null) throw new ArgumentNullException(nameof(requiredParticipantKeys));
            var keys = requiredParticipantKeys.ToArray();
            if (keys.Length == 0) throw new ArgumentException("Declare at least one required writer for a Match checkpoint scope.", nameof(requiredParticipantKeys));
            Service = new CheckpointService(runtime, store, codec, requiredParticipantKeys: keys);
            try
            {
                foreach (var participant in participants) Service.Register(participant);
                Requests = new CheckpointRequestChannel();
                Trigger = new CheckpointTrigger(Requests, Service);
            }
            catch { Service.Dispose(); throw; }
        }

        /// <summary>Feature 생성/교체 조립부가 writer를 추가하고 해제 lease를 보관합니다. scope가 Feature 객체를 파괴하지 않습니다.</summary>
        public IDisposable Register(ICheckpointParticipant participant)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchCheckpointScope));
            return Service.Register(participant);
        }

        /// <summary>Match 종료 전에 사건 구독·진행 요청·writer 대응을 닫습니다. App의 저장소는 계속 사용할 수 있습니다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Trigger?.Dispose(); }
            finally { Service.Dispose(); }
        }
    }
}
