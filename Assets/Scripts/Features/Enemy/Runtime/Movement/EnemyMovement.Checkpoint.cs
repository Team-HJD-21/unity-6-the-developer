// 감속의 남은 게임 초를 저장합니다.
// 추적 경로는 복원한 위치와 타깃에서 Agent가 다시 계산합니다.
using System.IO;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;

/// <summary>
/// 저장된 감속을 복원한다.
/// </summary>
public partial class EnemyMovement
{
    internal State CaptureCheckpoint()
    {
        var slows = new List<SavedSlow>();
        if (_slowEffects != null && _slowEffects.Count > 0)
        {
            double now = FieldSimulationTime.Seconds(this);
            foreach (SlowEffect effect in _slowEffects)
                if (effect.EndsAt > now)
                    slows.Add(new SavedSlow(effect.Multiplier, effect.EndsAt - now));
        }
        return new State(slows.ToArray());
    }
    internal void RestoreCheckpoint(State state)
    {
        _slowEffects?.Clear();
        if (state.Slows.Length == 0) return;
        _slowEffects ??= new List<SlowEffect>();
        double now = FieldSimulationTime.Seconds(this);
        foreach (SavedSlow effect in state.Slows)
            _slowEffects.Add(new SlowEffect(effect.Multiplier, now + effect.RemainingSeconds));
    }

    internal readonly struct SavedSlow
    {
        public readonly float Multiplier;
        public readonly double RemainingSeconds;
        public SavedSlow(float multiplier, double remainingSeconds)
        {
            if (!float.IsFinite(multiplier) || multiplier <= 0f || multiplier >= 1f ||
                !double.IsFinite(remainingSeconds) || remainingSeconds <= 0d)
                throw new InvalidDataException("Invalid saved enemy slow effect.");
            Multiplier = multiplier;
            RemainingSeconds = remainingSeconds;
        }
    }

    internal sealed class State
    {
        public readonly SavedSlow[] Slows;
        public State(SavedSlow[] slows)
        {
            Slows = slows;
            if (Slows.Length > MaximumSlowEffects) throw new InvalidDataException("Too many enemy slow effects.");
            var multipliers = new HashSet<float>();
            foreach (SavedSlow effect in Slows)
            {
                _ = new SavedSlow(effect.Multiplier, effect.RemainingSeconds);
                if (!multipliers.Add(effect.Multiplier)) throw new InvalidDataException("Duplicate enemy slow multiplier.");
            }
        }
        public void Write(BinaryWriter w)
        {
            // 이전 형식에서 경로 존재 여부, 경유점 수, 진행 인덱스, 정지 여부를 기록하던 자리다.
            w.Write(false);
            w.Write(0);
            w.Write(0);
            w.Write(false);
            w.Write(Slows.Length);
            foreach (SavedSlow effect in Slows)
            { w.Write(effect.Multiplier); w.Write(effect.RemainingSeconds); }
        }
        public static State Read(BinaryReader r, ICheckpointPayloadCodec codec, bool includesSlows)
        {
            bool exists = r.ReadBoolean();
            int cornerCount = codec.ReadCount(r, 4096);
            if (!exists && cornerCount != 0) throw new InvalidDataException("Invalid null enemy path.");
            for (int i = 0; i < cornerCount; i++)
            {
                float x = r.ReadSingle();
                float y = r.ReadSingle();
                float z = r.ReadSingle();
                if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
                    throw new InvalidDataException("Non-finite enemy path.");
            }
            int current = r.ReadInt32();
            if (current < 0 || current > cornerCount) throw new InvalidDataException("Invalid saved enemy path.");
            r.ReadBoolean(); // 이전 형식의 정지 플래그를 소비해 뒤따르는 데이터 위치를 보존한다.
            var slows = includesSlows ? new SavedSlow[codec.ReadCount(r, MaximumSlowEffects)] : System.Array.Empty<SavedSlow>();
            for (int i = 0; i < slows.Length; i++)
                slows[i] = new SavedSlow(r.ReadSingle(), r.ReadDouble());
            return new(slows);
        }
    }
}
