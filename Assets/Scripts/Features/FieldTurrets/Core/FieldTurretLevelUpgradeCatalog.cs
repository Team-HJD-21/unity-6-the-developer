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

        /// <summary>UI가 Host의 정의 ID를 로컬 콘텐츠로 해석합니다. 중복 ID는 임의 선택하지 않습니다.</summary>
        public bool TryGetDefinition(string definitionId, out FieldTurretDefinition definition)
        {
            FieldTurretDefinition found = null; bool ambiguous = false;
            void Consider(FieldTurretDefinition candidate)
            {
                if (candidate == null || candidate.Id != definitionId) return;
                if (found != null && found != candidate) ambiguous = true;
                else found = candidate;
            }
            foreach (Transition transition in _transitions)
            { Consider(transition.Source); Consider(transition.SourceLevelPrefab?.Definition); Consider(transition.NextLevelPrefab?.Definition); }
            definition = ambiguous ? null : found; return definition != null;
        }

        /// <summary>Feature의 복원 준비가 저장 정의 ID에 대응하는 제작 prefab을 해석합니다. 모호한 ID는 거부합니다.</summary>
        public bool TryGetPrefab(string definitionId, out FieldTurretBase prefab)
        {
            FieldTurretBase found = null; bool ambiguous = false;
            void Consider(FieldTurretBase candidate)
            {
                if (candidate == null || candidate.Definition == null || candidate.Definition.Id != definitionId) return;
                if (found != null && found != candidate) ambiguous = true;
                else found = candidate;
            }
            foreach (Transition transition in _transitions)
            { Consider(transition.SourceLevelPrefab); Consider(transition.NextLevelPrefab); }
            prefab = ambiguous ? null : found;
            if (prefab == null || !TryGetDefinition(definitionId, out var definition) || prefab.Definition != definition)
            { prefab = null; return false; }
            return true;
        }

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
