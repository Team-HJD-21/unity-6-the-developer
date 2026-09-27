using System;
using System.Collections.Generic;
using TeamHJD.Game.Content;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    [CreateAssetMenu(fileName = "TurretLevelUpgradeCatalog",
        menuName = "The Developer/Turret Level Upgrade Catalog")]
    public sealed class TurretLevelUpgradeCatalog : ScriptableObject
    {
        [Serializable]
        private struct Transition
        {
            [SerializeField] private TurretDefinition _source;
            [SerializeField] private TurretBase _nextLevelPrefab;

            public TurretDefinition Source => _source;
            public TurretBase NextLevelPrefab => _nextLevelPrefab;
        }

        [SerializeField] private List<Transition> _transitions = new();

        public bool TryGetNext(TurretDefinition source, out TurretBase nextLevelPrefab)
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
    }
}
