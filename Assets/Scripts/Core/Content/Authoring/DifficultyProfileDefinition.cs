// 난이도 프로필의 제작 식별자와 화면 표시 정보를 저장합니다.

using UnityEngine;

namespace TeamHJD.Game.Content.Authoring
{
    [CreateAssetMenu(menuName = "TeamHJD/Game/Difficulty Profile Definition")]
    public sealed class DifficultyProfileDefinition : ScriptableObject
    {
        [SerializeField] private string _definitionKey;
        [SerializeField] private string _displayName;

        public string DefinitionKey => _definitionKey;
        public string DisplayName => _displayName;
    }
}
