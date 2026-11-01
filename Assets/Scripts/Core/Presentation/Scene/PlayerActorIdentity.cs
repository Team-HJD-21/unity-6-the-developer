// Scene에 생성된 Player GameObject를 Match의 PlayerId와 연결합니다.

using System;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class PlayerActorIdentity : MonoBehaviour
    {
        private string _playerId;
        private int _slotIndex = -1;

        public PlayerId PlayerId => new PlayerId(_playerId ?? string.Empty);
        /// <summary>현재 Match의 roster 순서입니다. 저장 키에 접속 ID를 사용하지 않으며 미등록은 -1입니다.</summary>
        public int SlotIndex => _slotIndex;
        public string CheckpointKeyPrefix => _slotIndex >= 0 && !PlayerId.IsEmpty
            ? "world/player/" + _slotIndex : throw new InvalidOperationException("Bind a roster slot before registering Player checkpoint writers.");

        public void Bind(PlayerId playerId)
        {
            if (playerId.IsEmpty) throw new ArgumentException("A spawned player requires a valid ID.", nameof(playerId));
            if (!string.IsNullOrEmpty(_playerId)) throw new InvalidOperationException("Player identity is already bound.");
            _playerId = playerId.Value;
        }

        /// <summary>로컬 생성자가 PlayerStart의 슬롯을 함께 바인딩합니다.</summary>
        public void Bind(PlayerId playerId, int slotIndex)
        {
            if (slotIndex < 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            Bind(playerId); _slotIndex = slotIndex;
        }

        /// <summary>Host actor 등록 조립부가 검증한 roster 슬롯을 지정합니다. 현재 writer 등록 중에는 바꾸지 않습니다.</summary>
        public void BindCheckpointSlot(int slotIndex)
        {
            if (PlayerId.IsEmpty) throw new InvalidOperationException("Bind the logical Player ID first.");
            if (slotIndex < 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            _slotIndex = slotIndex;
        }
    }
}
