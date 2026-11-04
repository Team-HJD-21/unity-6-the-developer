// 저장된 논리 ID를 Awake 전에 지정하고 제작 prefab을 소유 Scene에 재생성합니다.
// Feature는 반환된 실제 개체에 검증한 게임 상태를 적용하며 이 Factory는 규칙 상태를 소유하지 않습니다.
using System;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Presentation.Scene
{
    public static class CheckpointActorFactory
    {
        /// <summary>권위 Feature가 복원 중 새 actor를 만들 때 사용합니다. 논리 ID를 먼저 연결하고 Awake를 실행합니다.</summary>
        public static GameObject Instantiate(GameObject prefab, UnityScene ownerScene, CheckpointTransformState pose, EntityId identity)
        {
            ValidatePrefab(prefab, identity);
            if (!ownerScene.IsValid() || !ownerScene.isLoaded || identity.IsEmpty) throw new ArgumentException("A loaded owner Scene and saved logical identity are required.");
            GameObject staging = new GameObject("Checkpoint Staging") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
            staging.SetActive(false); SceneManager.MoveGameObjectToScene(staging, ownerScene);
            GameObject actor = null;
            try
            {
                actor = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                actor.SetActive(false);
                var component = actor.GetComponent<SceneEntityIdentity>() ?? actor.AddComponent<SceneEntityIdentity>();
                component.Initialize(identity);
                actor.transform.SetParent(null, false);
                // 부모 해제의 TRS 재계산 뒤 최종 값을 적용해 반복 복원의 scale 반올림 누적을 피합니다.
                pose.Apply(actor.transform);
                actor.SetActive(true);
                return actor;
            }
            catch { if (actor != null) Retire(actor); throw; }
            finally { Retire(staging); }
        }

        /// <summary>Feature의 PrepareRestore 단계가 제작 prefab과 저장 ID의 충돌을 Native 개체 생성 전에 확인합니다.</summary>
        public static void ValidatePrefab(GameObject prefab, EntityId identity)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (identity.IsEmpty) throw new ArgumentException("A saved logical identity is required.", nameof(identity));
            var authored = prefab.GetComponent<SceneEntityIdentity>();
            if (authored != null && !authored.EntityId.IsEmpty && authored.EntityId != identity)
                throw new ArgumentException("The prefab already belongs to a different logical entity.", nameof(prefab));
        }

        /// <summary>실제 생성자가 초기화하는 동안 호출합니다. 복원 Factory가 먼저 지정한 ID는 유지합니다.</summary>
        public static EntityId EnsureIdentity(GameObject actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            var component = actor.GetComponent<SceneEntityIdentity>() ?? actor.AddComponent<SceneEntityIdentity>();
            if (component.EntityId.IsEmpty) component.Initialize(new EntityId("player-field/" + Guid.NewGuid().ToString("N")));
            return component.EntityId;
        }

        /// <summary>소유 Feature가 교체할 이전 actor를 즉시 비활성화하고 해당 실행 모드의 정상 파괴를 요청합니다.</summary>
        public static void Retire(GameObject actor)
        {
            if (actor == null) return;
            actor.SetActive(false);
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(actor); else UnityEngine.Object.DestroyImmediate(actor);
        }
    }
}
