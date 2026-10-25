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
            [SerializeField] private TurretBase _sourceLevelPrefab;
            [SerializeField] private TurretBase _nextLevelPrefab;

            public TurretDefinition Source => _source;
            public TurretBase SourceLevelPrefab => _sourceLevelPrefab;
            public TurretBase NextLevelPrefab => _nextLevelPrefab;
        }

        [SerializeField] private List<Transition> _transitions = new();

        /// <summary>UI가 Host의 정의 ID를 로컬 콘텐츠로 해석합니다. 중복 ID는 임의 선택하지 않습니다.</summary>
        public bool TryGetDefinition(string definitionId, out TurretDefinition definition)
        {
            TurretDefinition found = null; bool ambiguous = false;
            void Consider(TurretDefinition candidate)
            {
                if (candidate == null || candidate.Id != definitionId) return;
                if (found != null && found != candidate) ambiguous = true;
                else found = candidate;
            }
            foreach (Transition transition in _transitions)
            { Consider(transition.Source); Consider(transition.SourceLevelPrefab?.Definition); Consider(transition.NextLevelPrefab?.Definition); }
            definition = ambiguous ? null : found; return definition != null;
        }

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

        public bool TryGetPrevious(TurretDefinition source, out TurretBase previousLevelPrefab)
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
