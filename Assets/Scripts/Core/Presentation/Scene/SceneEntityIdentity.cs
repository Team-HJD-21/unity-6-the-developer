// 배치 개체의 논리 ID를 Scene에 직렬화합니다. 런타임에서 임의 ID를 자동 생성하지 않습니다.
// 복제된 배치의 중복 ID는 제작 도구에서 명시적으로 고치며 개체 교체 때 같은 ID를 이전합니다.
using System;
using TeamHJD.Game.Contracts;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;
namespace TeamHJD.Game.Presentation.Scene
{
    [DisallowMultipleComponent]
    public sealed class SceneEntityIdentity : MonoBehaviour, IEntityIdentity
    {
        [SerializeField] private string _entityId;
        public EntityId EntityId => string.IsNullOrWhiteSpace(_entityId) ? default : new EntityId(_entityId);
        public void Initialize(EntityId entityId)
        {
            if (entityId.IsEmpty) throw new ArgumentException("A logical entity ID is required.", nameof(entityId));
            if (!EntityId.IsEmpty && EntityId != entityId) throw new InvalidOperationException("An actor identity cannot be reassigned.");
            _entityId = entityId.Value;
        }
    }
}
