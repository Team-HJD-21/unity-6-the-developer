using System;
using System.Collections.Generic;
using TeamHJD.Game.Content;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    [CreateAssetMenu(fileName = "FieldTurretLevelUpgradeCatalog",
        menuName = "The Developer/Field Turret Level Upgrade Catalog")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TeamHJD.Game.Turrets", sourceAssembly: "TeamHJD.Game.Turrets", sourceClassName: "TurretLevelUpgradeCatalog")]
    public sealed class FieldTurretLevelUpgradeCatalog : ScriptableObject
    {
        [Serializable]
        private struct Transition
        {
            [SerializeField] private FieldTurretDefinition _source;
            [SerializeField] private FieldTurretBase _sourceLevelPrefab;
            [SerializeField] private FieldTurretBase _nextLevelPrefab;

            public FieldTurretDefinition Source => _source;
            public FieldTurretBase SourceLevelPrefab => _sourceLevelPrefab;
            public FieldTurretBase NextLevelPrefab => _nextLevelPrefab;
        }

        [SerializeField] private List<Transition> _transitions = new();

        public bool TryGetNext(FieldTurretDefinition source, out FieldTurretBase nextLevelPrefab)
        {
            foreach (Transition transition in _transitions)
            {
                if (transition.Source != source)
                    continue;

                nextLevelPrefab = transition.NextLevelPrefab;
                return nextLevelPrefab != null;
            }

            nextLevelPrefab = null;
            return false;
        }

        public bool TryGetPrevious(FieldTurretDefinition source, out FieldTurretBase previousLevelPrefab)
        {
            foreach (Transition transition in _transitions)
            {
                if (transition.NextLevelPrefab == null ||
                    transition.NextLevelPrefab.Definition != source)
                {
                    continue;
                }

                previousLevelPrefab = transition.SourceLevelPrefab;
                return previousLevelPrefab != null;
            }

            previousLevelPrefab = null;
            return false;
        }
    }
}
