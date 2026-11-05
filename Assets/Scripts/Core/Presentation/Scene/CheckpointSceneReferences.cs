// 복원 뒤 새로 생성된 실제 개체를 논리 ID로 다시 찾습니다.
// 저장에는 Player 슬롯·제작/생성 ID만 쓰며 Unity/NGO 실행 ID를 넣지 않습니다.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Presentation.Scene
{
    public static class CheckpointSceneReferences
    {
        /// <summary>Feature writer가 타깃을 캡처할 때 호출합니다. 미등록 타깃은 누락시키지 않고 거부합니다.</summary>
        public static string Capture(Transform target)
        {
            if (target == null) return string.Empty;
            var player = target.GetComponentInParent<PlayerActorIdentity>();
            if (player != null && player.SlotIndex >= 0) return "player/" + player.SlotIndex;
            var identity = target.GetComponentInParent<SceneEntityIdentity>();
            if (identity != null && !identity.EntityId.IsEmpty) return "entity/" + identity.EntityId.Value;
            throw new InvalidOperationException("Checkpoint target needs a logical identity: " + target.name);
        }

        /// <summary>전체 Feature의 actor 적용이 끝난 참조 단계에서 호출합니다. 같은 Scene의 유일한 타깃만 반환합니다.</summary>
        public static Transform Resolve(UnityEngine.SceneManagement.Scene scene, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Transform found = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var player in root.GetComponentsInChildren<PlayerActorIdentity>())
                    if (player.gameObject.activeInHierarchy && player.enabled && key == "player/" + player.SlotIndex) Add(player.transform);
                foreach (var entity in root.GetComponentsInChildren<SceneEntityIdentity>())
                    if (entity.gameObject.activeInHierarchy && entity.enabled && key == "entity/" + entity.EntityId.Value) Add(entity.transform);
            }
            return found ?? throw new InvalidOperationException("Checkpoint target is missing: " + key);

            void Add(Transform candidate)
            {
                if (found != null && found != candidate) throw new InvalidOperationException("Checkpoint target is ambiguous: " + key);
                found = candidate;
            }
        }
    }
}
