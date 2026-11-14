// Level이 최종 Scene의 실제 참가자 도착점·보스 원본·선택적 도입 연출을 지정합니다.
// Match를 새로 만들지 않으며 지정되지 않은 보스/위치를 임의 콘텐츠로 대체하지 않습니다.
using UnityEngine;
using UnityEngine.Playables;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    [DisallowMultipleComponent]
    public sealed class FinalBattleSceneLayout : MonoBehaviour
    {
        [Tooltip("PlayerId 오름차순으로 사용하는 실제 NavMesh/이동 가능 도착점입니다.")]
        public Transform[] playerStarts;
        [Tooltip("같은 Scene의 IFinalBattleBossSource 구현입니다. 실제 보스·Executor를 지정합니다.")]
        public MonoBehaviour bossSource;
        [Tooltip("없으면 도입 연출 없이 진입합니다. 준비 정지와 별개인 실시간으로 한 번 재생합니다.")]
        public PlayableDirector intro;
        [Tooltip("도입 연출 중 사용할 선택적 로컬 시점입니다. 출력 Camera를 새로 만들지 않습니다.")]
        public Transform introView;
        [Min(0)] public float cameraBlendSeconds = .25f;
    }
}
