// Stores authoring identity and presentation metadata for a playable stage.

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
