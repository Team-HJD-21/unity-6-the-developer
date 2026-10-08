// ScriptableObject로 작성한 정의를 불변 Match 설정으로 해석합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Content.Authoring;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Content.Runtime
{
    [CreateAssetMenu(menuName = "TeamHJD/Game/Content Catalog")]
    public sealed class ContentCatalog : ScriptableObject, IContentCatalog
    {
        [SerializeField] private string _contentVersion;
        [SerializeField] private StageDefinition[] _stages = Array.Empty<StageDefinition>();
        [SerializeField] private DifficultyProfileDefinition[] _difficultyProfiles = Array.Empty<DifficultyProfileDefinition>();

        private Dictionary<string, StageDefinition> _stagesByKey;
        private Dictionary<string, DifficultyProfileDefinition> _difficultyProfilesByKey;

        public MatchConfig ResolveMatchConfig(MatchSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            EnsureIndexes();

            if (!_stagesByKey.ContainsKey(selection.StageId.Value))
                throw new KeyNotFoundException($"Stage '{selection.StageId}' is not present in this content catalog.");
            if (!_difficultyProfilesByKey.ContainsKey(selection.DifficultyProfileId.Value))
                throw new KeyNotFoundException($"Difficulty profile '{selection.DifficultyProfileId}' is not present in this content catalog.");

            return new MatchConfig(
                selection.MatchId,
                selection.Mode,
                selection.StageId,
                selection.DifficultyProfileId,
                _contentVersion,
                selection.RandomSeed,
                selection.Players);
        }

        private void OnEnable()
        {
            _stagesByKey = null;
            _difficultyProfilesByKey = null;
        }

        private void EnsureIndexes()
        {
            if (_stagesByKey != null && _difficultyProfilesByKey != null) return;
            _stagesByKey = CreateIndex(_stages, definition => definition.StageKey, "stage");
            _difficultyProfilesByKey = CreateIndex(_difficultyProfiles, definition => definition.DefinitionKey, "difficulty profile");
        }

        private static Dictionary<string, TDefinition> CreateIndex<TDefinition>(
            IEnumerable<TDefinition> definitions,
            Func<TDefinition, string> getKey,
            string category)
            where TDefinition : UnityEngine.Object
        {
            var index = new Dictionary<string, TDefinition>(StringComparer.Ordinal);
            if (definitions == null) return index;

            foreach (var definition in definitions)
            {
                if (definition == null) continue;
                var key = getKey(definition);
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException($"A {category} definition has no key.");
                if (index.ContainsKey(key)) throw new InvalidOperationException($"Duplicate {category} key '{key}'.");
                index.Add(key, definition);
            }

            return index;
        }
    }
}
