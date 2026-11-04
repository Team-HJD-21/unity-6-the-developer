// Core 난수 전체 흐름을 기존 체크포인트 실행기에 연결합니다.
// Feature의 날씨·스폰 상태와 구분해 seed/알고리즘/난수 진행 상태만 저장합니다.

using System;
using System.IO;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class RandomCheckpointParticipant : ICheckpointParticipant
    {
        private readonly MatchId _match;
        private readonly RandomService _random;

        public string Key => "core/random";
        public int Version => 1;
        public CheckpointSectionKind Kind => CheckpointSectionKind.World;
        public int RestoreOrder => 0;

        /// <summary>Core 조립부가 현재 Match와 해당 Match가 소유한 난수 서비스를 연결합니다.</summary>
        public RandomCheckpointParticipant(MatchId match, RandomService random)
        {
            if (match.IsEmpty)
                throw new ArgumentException("A Match ID is required.", nameof(match));
            _match = match;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <inheritdoc />
        public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            RequireMatch(context);
            var state = _random.Capture();
            return codec.Write(Key, Version, Kind, writer =>
            {
                writer.Write(state.AlgorithmVersion);
                writer.Write(state.Seed);
                writer.Write(state.Streams.Count);
                foreach (var stream in state.Streams)
                {
                    codec.WriteText(writer, stream.StreamId, 128);
                    writer.Write(stream.State);
                }
            });
        }

        /// <inheritdoc />
        public ICheckpointRestoreStep PrepareRestore(
            CheckpointRestoreContext context,
            CheckpointSection section,
            ICheckpointPayloadCodec codec)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            RequireMatch(context.Current);
            if (section == null || section.Key != Key || section.Kind != Kind)
                throw new InvalidDataException("Unexpected random checkpoint section.");
            var target = codec.Read(section, Version, reader =>
            {
                int version = reader.ReadInt32(),
                    seed = reader.ReadInt32(),
                    count = codec.ReadCount(reader, RandomStateSnapshot.MaximumStreams);
                try
                {
                    var streams = new RandomStreamState[count];
                    for (int i = 0; i < count; i++)
                        streams[i] = new RandomStreamState(codec.ReadText(reader, 128), reader.ReadUInt64());
                    return new RandomStateSnapshot(version, seed, streams);
                }
                catch (ArgumentException error)
                {
                    throw new InvalidDataException("Invalid random state.", error);
                }
            });
            if (target.Seed != _random.Seed)
                throw new InvalidDataException("Random checkpoint seed does not match the current Match.");
            var backup = _random.Capture();
            return new CheckpointRestoreStep(() => _random.Restore(target), () => _random.Restore(backup));
        }

        private void RequireMatch(CheckpointCaptureContext context)
        {
            if (context == null ||
                context.Config.MatchId != _match ||
                context.Config.RandomSeed != _random.Seed ||
                !_random.IsAvailable)
                throw new InvalidOperationException("Use the random writer only in its current authority Match.");
        }
    }
}
