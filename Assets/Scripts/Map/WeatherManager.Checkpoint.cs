// Host의 실제 날씨·예고·추첨 상태와 다음 전환까지 남은 게임 시간을 저장합니다.
// 복원 세대에서 예약을 다시 연결하며 standalone 또는 Client 읽기 상태는 저장하지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace Map
{
    public partial class WeatherManager : ICheckpointParticipant
    {
        string ICheckpointParticipant.Key => "world/weather";
        int ICheckpointParticipant.Version => 1;
        CheckpointSectionKind ICheckpointParticipant.Kind => CheckpointSectionKind.World;
        int ICheckpointParticipant.RestoreOrder => 400;

        CheckpointSection ICheckpointParticipant.Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
        {
            RequireCheckpointScope(context);
            SavedWeather state = CaptureWeather();
            return codec.Write("world/weather", 1, CheckpointSectionKind.World, writer =>
            {
                writer.Write((int)state.Current); writer.Write(state.QueueSize); writer.Write(state.SunnyWeight);
                writer.Write(state.RandomState); writer.Write(state.Forecast.Length);
                foreach (Weather weather in state.Forecast) writer.Write((int)weather);
                writer.Write(state.Timer.ConfiguredInterval); writer.Write(state.Timer.Interval); writer.Write(state.Timer.Remaining);
            });
        }

        ICheckpointRestoreStep ICheckpointParticipant.PrepareRestore(CheckpointRestoreContext context,
            CheckpointSection section, ICheckpointPayloadCodec codec)
        {
            RequireCheckpointScope(context.Current);
            var target = codec.Read(section, 1, reader =>
            {
                Weather current = ReadWeather(reader);
                int queueSize = reader.ReadInt32(), weight = reader.ReadInt32();
                uint random = reader.ReadUInt32();
                int count = codec.ReadCount(reader, MatchEnvironmentSnapshot.MaximumForecast);
                var forecast = new Weather[count];
                for (int i = 0; i < count; i++) forecast[i] = ReadWeather(reader);
                var timer = new CountTick.TimerCheckpoint(reader.ReadSingle(), reader.ReadSingle(), reader.ReadDouble());
                if (queueSize < 1 || queueSize > MatchEnvironmentSnapshot.MaximumForecast || weight < 1 ||
                    weight > int.MaxValue - (int)Weather.WEATHER_MAX || random == 0 || count == 0)
                    throw new InvalidDataException("Invalid saved Weather configuration or RNG state.");
                return new SavedWeather(current, queueSize, weight, random, forecast, timer);
            });
            SavedWeather backup = CaptureWeather();
            return new CheckpointRestoreStep(() => ApplyWeather(target), () => ApplyWeather(backup));
        }

        private void RequireCheckpointScope(CheckpointCaptureContext context)
        {
            if (_scope == null || !_scope.Authority || context == null || context.Config.MatchId != _scope.Match || !MayAdvance)
                throw new InvalidOperationException("Use the Weather writer only in its current authoritative Match.");
            RequireMatchScope();
        }

        private SavedWeather CaptureWeather()
        {
            var tick = GetComponent<CountTick>();
            if (tick == null || weatherQueue == null || weatherQueue.Count == 0 || _matchRandom.state == 0)
                throw new InvalidOperationException("Prepare the actual Weather forecast, RNG and timer before capture.");
            return new SavedWeather(currentWeather, weatherQueueSize, sunnyWeight, _matchRandom.state,
                weatherQueue.ToArray(), tick.CaptureCheckpoint());
        }

        private void ApplyWeather(SavedWeather state)
        {
            currentWeather = state.Current; weatherQueueSize = state.QueueSize; sunnyWeight = state.SunnyWeight;
            _matchRandom.state = state.RandomState;
            weatherQueue = new Queue<Weather>(state.Forecast);
            GetComponent<CountTick>().RestoreCheckpoint(state.Timer);
            NotifyWeather();
        }

        private static Weather ReadWeather(BinaryReader reader)
        {
            int value = reader.ReadInt32();
            if (value < (int)Weather.SUNNY || value > (int)Weather.WEATHER_MAX) throw new InvalidDataException("Unknown saved Weather.");
            return (Weather)value;
        }

        private sealed class SavedWeather
        {
            public readonly Weather Current;
            public readonly int QueueSize, SunnyWeight;
            public readonly uint RandomState;
            public readonly Weather[] Forecast;
            public readonly CountTick.TimerCheckpoint Timer;
            public SavedWeather(Weather current, int queueSize, int sunnyWeight, uint randomState, Weather[] forecast, CountTick.TimerCheckpoint timer)
            { Current = current; QueueSize = queueSize; SunnyWeight = sunnyWeight; RandomState = randomState; Forecast = forecast; Timer = timer; }
        }
    }
}
