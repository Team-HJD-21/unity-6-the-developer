// App 조립부가 지정한 디렉터리에 체크포인트를 원자적으로 저장합니다.
// Unity 상태를 작업 스레드에서 읽지 않으며 파일 핸들은 각 요청 안에서만 소유합니다.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Infrastructure
{
    public sealed class BinaryCheckpointStore : ICheckpointStore, IDisposable
    {
        private readonly string _directory, _prefix;
        private readonly object _gate = new();
        private readonly StringComparison _pathComparison;
        private readonly bool _deleteOnDispose;
        private bool _disposed;

        /// <summary>Editor 임시 저장만 종료 시 삭제합니다. 기본값은 빌드와 같은 영구 저장입니다.</summary>
        public BinaryCheckpointStore(string directory, bool deleteOnDispose = false)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A storage directory is required.", nameof(directory));
            _directory = Path.GetFullPath(directory);
            _prefix = _directory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? _directory : _directory + Path.DirectorySeparatorChar;
            _pathComparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            _deleteOnDispose = deleteOnDispose;
            // 비정상 Editor 종료로 남은 임시 파일도 다음 Play의 성장/안전장치에 섞지 않습니다.
            if (_deleteOnDispose) DeleteSessionFiles();
        }
        /// <inheritdoc />
        public Task SaveAsync(CheckpointSlot slot, CheckpointData data, CancellationToken cancellationToken = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string path = Resolve(slot);
            return Task.Run(() => Save(path, data, cancellationToken), cancellationToken);
        }
        /// <inheritdoc />
        public Task<CheckpointData> LoadAsync(CheckpointSlot slot, CancellationToken cancellationToken = default)
        {
            string path = Resolve(slot);
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] bytes;
                    try
                    {
                        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                        if (file.Length < CheckpointBinaryCodec.HeaderBytes || file.Length > CheckpointBinaryCodec.MaximumFileBytes)
                            throw new InvalidDataException("Checkpoint file size exceeds the supported bounds.");
                        bytes = new byte[(int)file.Length]; int offset = 0;
                        while (offset < bytes.Length)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            int count = file.Read(bytes, offset, Math.Min(65536, bytes.Length - offset));
                            if (count == 0) throw new InvalidDataException("Checkpoint file was truncated.");
                            offset += count;
                        }
                    }
                    catch (FileNotFoundException) { return null; }
                    catch (DirectoryNotFoundException) { return null; }
                    var data = CheckpointBinaryCodec.Decode(bytes); cancellationToken.ThrowIfCancellationRequested(); return data;
                }
            }, cancellationToken);
        }
        private void Save(string path, CheckpointData data, CancellationToken cancellation)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellation.ThrowIfCancellationRequested(); byte[] bytes = CheckpointBinaryCodec.Encode(data);
                cancellation.ThrowIfCancellationRequested(); Directory.CreateDirectory(_directory);
                string temporary = Path.Combine(_directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        for (int offset = 0; offset < bytes.Length;)
                        {
                            cancellation.ThrowIfCancellationRequested(); int count = Math.Min(65536, bytes.Length - offset);
                            file.Write(bytes, offset, count); offset += count;
                        }
                        file.Flush(true);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    // 같은 디렉터리에서 완성본만 교체합니다. 이 commit 지점 뒤에는 취소를 실패로 바꾸지 않습니다.
                    if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        private string Resolve(CheckpointSlot slot)
        {
            if (slot.IsEmpty) throw new ArgumentException("A valid slot is required.", nameof(slot));
            string path = Path.GetFullPath(Path.Combine(_directory, slot.Value + ".checkpoint"));
            if (!path.StartsWith(_prefix, _pathComparison)) throw new ArgumentException("The slot resolves outside its storage directory.", nameof(slot));
            return path;
        }

        /// <summary>진행 중인 파일 교체가 끝난 뒤 닫습니다. 종료 뒤 대기 쓰기가 파일을 다시 만들지 못합니다.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                if (_deleteOnDispose) DeleteSessionFiles();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BinaryCheckpointStore));
        }

        // 지정 폴더의 직접 자식인 저장 파일/이 Store의 임시 파일만 지웁니다. 재귀 삭제는 하지 않습니다.
        private void DeleteSessionFiles()
        {
            if (!Directory.Exists(_directory)) return;
            foreach (string path in Directory.EnumerateFiles(_directory, "*.checkpoint", SearchOption.TopDirectoryOnly))
                File.Delete(path);
            foreach (string path in Directory.EnumerateFiles(_directory, ".*.tmp", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(path).Substring(1);
                if (Guid.TryParseExact(name, "N", out _)) File.Delete(path);
            }
        }
    }
}
