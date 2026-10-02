// Scene에 생성된 Player GameObject를 Match의 PlayerId와 연결합니다.

using System;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class PlayerActorIdentity : MonoBehaviour
    {
        private string _playerId;

        public PlayerId PlayerId => new PlayerId(_playerId ?? string.Empty);

        public void Bind(PlayerId playerId)
        {
            if (playerId.IsEmpty) throw new ArgumentException("A spawned player requires a valid ID.", nameof(playerId));
            if (!string.IsNullOrEmpty(_playerId)) throw new InvalidOperationException("Player identity is already bound.");
            _playerId = playerId.Value;
        }
    }
}
