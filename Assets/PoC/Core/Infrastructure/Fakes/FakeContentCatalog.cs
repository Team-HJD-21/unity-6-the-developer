// Unity 에셋 없이 등록된 스테이지·난이도 ID를 Match 설정으로 해석합니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Infrastructure.Fakes
{
    public sealed class FakeContentCatalog : IContentCatalog
    {
        private readonly HashSet<StageId> _stageIds;
        private readonly HashSet<DefinitionId> _difficultyProfileIds;
        private readonly string _contentVersion;

        public FakeContentCatalog(
            IEnumerable<StageId> stageIds = null,
            IEnumerable<DefinitionId> difficultyProfileIds = null,
            string contentVersion = "fake")
        {
            _stageIds = new HashSet<StageId>(stageIds ?? new StageId[0]);
            _difficultyProfileIds = new HashSet<DefinitionId>(difficultyProfileIds ?? new DefinitionId[0]);
            _contentVersion = contentVersion ?? string.Empty;
        }

        public MatchConfig ResolveMatchConfig(MatchSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (!_stageIds.Contains(selection.StageId))
                throw new KeyNotFoundException($"Stage '{selection.StageId}' is not registered in the fake catalog.");
            if (!_difficultyProfileIds.Contains(selection.DifficultyProfileId))
                throw new KeyNotFoundException($"Difficulty profile '{selection.DifficultyProfileId}' is not registered in the fake catalog.");

            return new MatchConfig(
                selection.MatchId,
                selection.Mode,
                selection.StageId,
                selection.DifficultyProfileId,
                _contentVersion,
                selection.RandomSeed,
                selection.Players);
        }
    }
}
