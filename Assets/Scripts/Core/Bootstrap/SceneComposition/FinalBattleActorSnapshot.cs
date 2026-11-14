// 최종 Scene 준비 전의 실제 참가자 Scene/부모/위치를 보존하고 실패·안전장치 복귀 때 되돌립니다.
// HP·성장·전투 상태의 원본은 저장 writer가 맡으며 이 임시 값은 체크포인트로 저장하지 않습니다.
using System;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    internal sealed class FinalBattleActorSnapshot
    {
        private readonly GameObject _actor;
        private readonly Transform _parent;
        private readonly Scene _scene;
        private readonly CheckpointTransformState _pose;
        public FinalBattleActorSnapshot(GameObject actor)
        {
            _actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            _parent = actor.transform.parent; _scene = actor.scene; _pose = CheckpointTransformState.Capture(actor.transform);
        }
        public void Restore()
        {
            if (_actor == null) return; // 사망/Match 종료로 제거된 이전 객체를 다시 만들지 않습니다.
            if (!_scene.IsValid() || !_scene.isLoaded) throw new InvalidOperationException("참가자가 돌아갈 원래 Scene이 없습니다.");
            _actor.transform.SetParent(null, true);
            if (_actor.scene != _scene) SceneManager.MoveGameObjectToScene(_actor, _scene);
            if (_parent != null && _parent.gameObject.scene == _scene) _actor.transform.SetParent(_parent, true);
            _pose.Apply(_actor.transform);
            if (_actor.TryGetComponent<Rigidbody2D>(out var body)) body.position = _pose.Position;
        }
        public bool IsInScene(Scene scene) => _actor != null && _actor.scene == scene;
    }
}
