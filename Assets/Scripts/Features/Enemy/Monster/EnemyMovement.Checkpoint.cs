// 실제 계산된 경로와 현재 waypoint·정지 상태를 저장합니다.
// 타깃 재연결이 새 경로를 만들더라도 그 뒤 저장된 진행을 명시 적용합니다.
using System.IO;
using TeamHJD.Game.Contracts;
using UnityEngine;

public partial class EnemyMovement
{
    internal State CaptureCheckpoint() => new(_path == null ? null : (Vector3[])_path.Clone(), _currentPath, _isStopped);
    internal void RestoreCheckpoint(State state)
    { _path = state.Path == null ? null : (Vector3[])state.Path.Clone(); _currentPath = state.Current; _isStopped = state.Stopped; }

    internal sealed class State
    {
        public readonly Vector3[] Path;
        public readonly int Current;
        public readonly bool Stopped;
        public State(Vector3[] path, int current, bool stopped)
        {
            if (current < 0 || current > (path?.Length ?? 0) || (path?.Length ?? 0) > 4096) throw new InvalidDataException("Invalid saved enemy path.");
            foreach (var p in path ?? System.Array.Empty<Vector3>())
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) throw new InvalidDataException("Non-finite enemy path.");
            Path = path; Current = current; Stopped = stopped;
        }
        public void Write(BinaryWriter w)
        {
            w.Write(Path != null); w.Write(Path?.Length ?? 0);
            foreach (var p in Path ?? System.Array.Empty<Vector3>()) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
            w.Write(Current); w.Write(Stopped);
        }
        public static State Read(BinaryReader r, ICheckpointPayloadCodec codec)
        {
            bool exists = r.ReadBoolean(); var path = new Vector3[codec.ReadCount(r, 4096)];
            for (int i = 0; i < path.Length; i++) path[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if (!exists && path.Length != 0) throw new InvalidDataException("Invalid null enemy path.");
            return new(exists ? path : null, r.ReadInt32(), r.ReadBoolean());
        }
    }
}
