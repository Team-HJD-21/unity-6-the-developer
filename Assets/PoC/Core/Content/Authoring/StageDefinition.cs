// 플레이 가능한 스테이지의 제작 식별자와 화면 표시 정보를 저장합니다.

using UnityEngine;

namespace TeamHJD.Game.Content.Authoring
{
    [CreateAssetMenu(menuName = "TeamHJD/Game/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] private string _stageKey;
        [SerializeField] private string _displayName;

        public string StageKey => _stageKey;
        public string DisplayName => _displayName;
    }
}
