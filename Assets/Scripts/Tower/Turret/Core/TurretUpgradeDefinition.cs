using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.Content
{
    [CreateAssetMenu(
        fileName = "TurretUpgradeDefinition",
        menuName = "The Developer/Turret Upgrade Definition")]
    public sealed class TurretUpgradeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;

        [Header("Compatibility")]
        [Tooltip("An empty list allows this upgrade on every turret definition.")]
        [SerializeField] private List<string> _compatibleDefinitionIds = new();

        [Header("Runtime Modifiers")]
        [SerializeField] private int _damageModifier;
        [SerializeField] private int _powerModifier;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int DamageModifier => _damageModifier;
        public int PowerModifier => _powerModifier;

        public bool IsCompatibleWith(string definitionId)
        {
            if (_compatibleDefinitionIds.Count == 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(definitionId) &&
                   _compatibleDefinitionIds.Contains(definitionId);
        }

        private void OnValidate()
        {
            _id = _id?.Trim();
            _displayName = _displayName?.Trim();
            _compatibleDefinitionIds ??= new List<string>();

            for (int index = _compatibleDefinitionIds.Count - 1; index >= 0; index--)
            {
                string definitionId = _compatibleDefinitionIds[index]?.Trim();
                if (string.IsNullOrEmpty(definitionId))
                {
                    _compatibleDefinitionIds.RemoveAt(index);
                    continue;
                }

                _compatibleDefinitionIds[index] = definitionId;
            }
        }
    }
}
