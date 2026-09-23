// Stores authoring identity and presentation metadata for a difficulty profile.

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
