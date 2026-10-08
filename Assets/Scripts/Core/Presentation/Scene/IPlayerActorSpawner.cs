// Match composition이 Player actor 생성 구현과 수명 정리에 의존하는 경계입니다.

using System;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public interface IPlayerActorSpawner : IDisposable
    {
        GameObject Spawn(PlayerId playerId, PlayerStart playerStart);
        bool Despawn(PlayerId playerId);
    }
}
