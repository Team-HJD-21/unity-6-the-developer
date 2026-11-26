// 실제 계산된 경로·현재 waypoint와 감속의 남은 게임 초를 저장합니다.
// 타깃 재연결이 새 경로를 만들더라도 그 뒤 저장된 진행을 명시 적용합니다.
using System.IO;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>
/// 저장된 경로와 경유점 진행도를 복원한다.
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
        return new State(_path == null ? null : (Vector3[])_path.Clone(), _currentPath, slows.ToArray());
    }
    internal void RestoreCheckpoint(State state)
    {
        _path = state.Path == null ? null : (Vector3[])state.Path.Clone();
        _currentPath = state.Current;
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
        public readonly Vector3[] Path;
        public readonly int Current;
        public readonly SavedSlow[] Slows;
        public State(Vector3[] path, int current, SavedSlow[] slows = null)
        {
            if (current < 0 || current > (path?.Length ?? 0) || (path?.Length ?? 0) > 4096) throw new InvalidDataException("Invalid saved enemy path.");
            foreach (var p in path ?? System.Array.Empty<Vector3>())
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) throw new InvalidDataException("Non-finite enemy path.");
            Path = path; Current = current;
            Slows = slows ?? System.Array.Empty<SavedSlow>();
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
            w.Write(Path != null); w.Write(Path?.Length ?? 0);
            foreach (var p in Path ?? System.Array.Empty<Vector3>()) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
            w.Write(Current);
            // 기존 저장 형식의 정지 플래그 자리를 유지한다. 현재 이동 제어에는 사용하지 않는다.
            w.Write(false);
            w.Write(Slows.Length);
            foreach (SavedSlow effect in Slows)
            { w.Write(effect.Multiplier); w.Write(effect.RemainingSeconds); }
        }
        public static State Read(BinaryReader r, ICheckpointPayloadCodec codec, bool includesSlows)
        {
            bool exists = r.ReadBoolean(); var path = new Vector3[codec.ReadCount(r, 4096)];
            for (int i = 0; i < path.Length; i++) path[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if (!exists && path.Length != 0) throw new InvalidDataException("Invalid null enemy path.");
            int current = r.ReadInt32();
            r.ReadBoolean(); // 이전 형식의 정지 플래그를 소비해 뒤따르는 데이터 위치를 보존한다.
            var slows = includesSlows ? new SavedSlow[codec.ReadCount(r, MaximumSlowEffects)] : System.Array.Empty<SavedSlow>();
            for (int i = 0; i < slows.Length; i++)
                slows[i] = new SavedSlow(r.ReadSingle(), r.ReadDouble());
            return new(exists ? path : null, current, slows);
        }
    }
}
