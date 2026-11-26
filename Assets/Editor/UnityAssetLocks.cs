using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
internal sealed class UnityAssetLocks : AssetModificationProcessor
{
    [Serializable]
    private sealed class LockStatus
    {
        public string[] tracked = Array.Empty<string>();
        public EditableAsset[] editable = Array.Empty<EditableAsset>();
        public RemoteLock[] remoteLocks = null;
    }

    [Serializable]
    private sealed class RemoteLock
    {
        public string path = "";
        public string id = "";
        public string owner = "";
        public bool owned = false;
    }

    [Serializable]
    private sealed class EditableAsset
    {
        public string path = "";
        public string id = "";
        public string record = "";
    }

    private sealed class ProjectSettingsEdit
    {
        public readonly string Path;
        private UnityEngine.Object _asset;
        private string _acceptedJson;
        private int _dirtyCount;
        private bool _acceptedDirty;
        private bool _restoring;

        public ProjectSettingsEdit(string path)
        {
            Path = path;
        }

        public bool Check(bool saving)
        {
            if (_restoring)
                return false;

            if (_asset == null)
            {
                _asset = AssetDatabase.LoadAllAssetsAtPath(Path).FirstOrDefault();
                if (_asset != null)
                    Remember(EditorJsonUtility.ToJson(_asset), EditorUtility.IsDirty(_asset));
                return !saving || TryOpen(Path);
            }

            bool dirty = EditorUtility.IsDirty(_asset);
            if (!dirty)
                _acceptedDirty = false;
            if (!saving && _dirtyCount == EditorUtility.GetDirtyCount(_asset))
                return true;

            string json = EditorJsonUtility.ToJson(_asset);
            if (!dirty || string.Equals(json, _acceptedJson, StringComparison.Ordinal))
            {
                Remember(json, dirty);
                return !saving || CanWrite(Path);
            }

            if (TryOpen(Path))
            {
                Remember(json, true);
                return true;
            }

            _restoring = true;
            try
            {
                EditorJsonUtility.FromJsonOverwrite(_acceptedJson, _asset);
                if (!_acceptedDirty)
                    EditorUtility.ClearDirty(_asset);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Unity asset locks: could not restore {Path}\n{exception.Message}");
            }
            finally
            {
                _dirtyCount = EditorUtility.GetDirtyCount(_asset);
                _restoring = false;
            }
            return false;
        }

        private void Remember(string json, bool dirty)
        {
            _acceptedJson = json;
            _acceptedDirty = dirty;
            _dirtyCount = EditorUtility.GetDirtyCount(_asset);
        }
    }

    private static readonly string ProjectRoot = Path.GetDirectoryName(Application.dataPath);
    private const string Physics2DSettingsPath = "ProjectSettings/Physics2DSettings.asset";
    private const string TagManagerPath = "ProjectSettings/TagManager.asset";
    private static readonly ProjectSettingsEdit[] ProjectSettingsEdits =
    {
        new ProjectSettingsEdit(Physics2DSettingsPath),
        new ProjectSettingsEdit(TagManagerPath)
    };
    private static readonly bool Windows = Application.platform == RuntimePlatform.WindowsEditor;
    private static readonly bool Mac = Application.platform == RuntimePlatform.OSXEditor;
    private static HashSet<string> _tracked = new HashSet<string>(StringComparer.Ordinal);
    private static Dictionary<string, EditableAsset> _editable = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, RemoteLock> RemoteLocks = new(StringComparer.Ordinal);
    private static readonly Color OtherLockColor = new(1f, .58f, .22f);
    private static readonly Color EditableLockColor = new(.35f, .85f, .48f);
    private static readonly Color ReadOnlyLockColor = new(.72f, .74f, .77f);
    private static Texture2D _closedLockIcon;
    private static Texture2D _openLockIcon;
    private static bool _remoteLocksCurrent;
    private const double LockRefreshSeconds = 15;
    private const double FullSyncSeconds = 45;
    private static Task<string> _refresh;
    private static double _nextRefresh;
    private static double _nextFullSync;
    private static bool _refreshIncludesFiles;
    private static int _refreshGeneration;
    private static int _editGeneration;
    private static bool _refreshFailed;

    static UnityAssetLocks()
    {
        if (Application.isBatchMode)
            return;

        EditorApplication.update += Update;
        EditorApplication.focusChanged += OnFocusChanged;
        EditorApplication.projectWindowItemOnGUI += DrawProjectLock;
    }

    private static void OnFocusChanged(bool hasFocus)
    {
        if (hasFocus && (_refresh == null || _refreshIncludesFiles || _refresh.IsCompleted))
            _nextRefresh = 0;
    }

    private static void Update()
    {
        foreach (ProjectSettingsEdit setting in ProjectSettingsEdits)
            setting.Check(saving: false);

        if (_refresh != null)
        {
            if (!_refresh.IsCompleted)
                return;

            try
            {
                string json = _refresh.GetAwaiter().GetResult();
                LockStatus status = JsonUtility.FromJson<LockStatus>(json);
                if (_refreshIncludesFiles)
                    _tracked = new HashSet<string>(status.tracked, StringComparer.Ordinal);

                // An acquisition can finish while a background snapshot is waiting to be applied.
                if (_refreshGeneration == _editGeneration)
                {
                    if (_refreshIncludesFiles)
                        _editable = status.editable.ToDictionary(asset => asset.path, StringComparer.Ordinal);

                    ApplyRemoteLocks(status.remoteLocks);
                    _refreshFailed = false;
                }
            }
            catch (Exception exception)
            {
                if (!_refreshFailed)
                    Debug.LogWarning($"Unity asset locks: {exception.Message}");
                _refreshFailed = true;
                if (_refreshGeneration == _editGeneration)
                    _remoteLocksCurrent = false;
            }
            _refresh = null;
            EditorApplication.RepaintProjectWindow();
        }

        double now = EditorApplication.timeSinceStartup;
        bool fullSync = now >= _nextFullSync;
        if (!fullSync && now < _nextRefresh)
            return;

        if (fullSync)
            _nextFullSync = now + FullSyncSeconds;
        _nextRefresh = now + LockRefreshSeconds;
        _refreshIncludesFiles = fullSync;
        _refreshGeneration = _editGeneration;
        _refresh = Task.Run(() => Run(fullSync ? "Sync" : "ReadLocks"));
    }

    private static void ApplyRemoteLocks(RemoteLock[] locks)
    {
        _remoteLocksCurrent = locks != null;
        if (!_remoteLocksCurrent)
            return;

        RemoteLocks.Clear();
        foreach (RemoteLock entry in locks)
            RemoteLocks[entry.path] = entry;

        foreach (EditableAsset asset in _editable.Values.ToArray())
        {
            if (string.IsNullOrEmpty(asset.id))
                continue;

            if (!RemoteLocks.TryGetValue(asset.path, out RemoteLock entry) || !entry.owned || entry.id != asset.id)
                _editable.Remove(asset.path);
        }
    }

    private static void DrawProjectLock(string guid, Rect row)
    {
        if (Event.current.type != EventType.Repaint || RemoteLocks.Count == 0)
            return;

        string path = GetLockPath(AssetDatabase.GUIDToAssetPath(guid));
        if (path == null || !RemoteLocks.TryGetValue(path, out RemoteLock entry))
            return;

        bool editable = entry.owned && CanWrite(path);
        Texture2D icon = editable
            ? (_openLockIcon ??= EditorGUIUtility.FindTexture("LockIcon"))
            : (_closedLockIcon ??= EditorGUIUtility.FindTexture("LockIcon-On"));
        if (icon == null)
            return;

        Color color = editable ? EditableLockColor : entry.owned ? ReadOnlyLockColor : OtherLockColor;
        string owner = string.IsNullOrEmpty(entry.owner) ? "확인 불가" : entry.owner;
        string status = editable ? "현재 작업 폴더에서 편집 가능" : "현재 작업 폴더에서 읽기 전용";
        string tooltip = $"잠금 소유자: {owner}\n{status}";
        if (!_remoteLocksCurrent)
            tooltip += "\n마지막 GitHub 조회 기준";

        const float size = 14f;
        float y = row.y + (row.height > 24f ? 2f : (row.height - size) * .5f);
        var badge = new Rect(row.xMax - size - 2f, y, size, size);
        GUI.DrawTexture(badge, icon, ScaleMode.ScaleToFit, true, 0f, color, 0f, 0f);
        GUI.Label(badge, new GUIContent(string.Empty, tooltip), GUIStyle.none);
    }

    private static bool IsOpenForEdit(string[] paths, List<string> notEditable, StatusQueryOptions options)
    {
        if (Application.isBatchMode)
            return true;

        foreach (string path in paths)
        {
            if (IsProjectSettingsPath(path))
                continue; // The settings UI stays interactive while edits and writes require a lock.

            string lockPath = GetLockPath(path);
            if (lockPath != null && _tracked.Contains(lockPath) && !CanWrite(lockPath))
                notEditable.Add(path);
        }

        return notEditable.Count == 0;
    }

    private static bool MakeEditable(string[] paths, string prompt, List<string> notEditable)
    {
        if (Application.isBatchMode)
            return true;

        foreach (string path in paths)
        {
            if (!TryOpen(path))
                notEditable.Add(path);
        }
        return notEditable.Count == 0;
    }

    private static string[] OnWillSaveAssets(string[] paths)
    {
        return Application.isBatchMode ? paths : paths.Where(CanSave).ToArray();
    }

    private static bool CanSave(string path)
    {
        if (!IsProjectSettingsPath(path))
            return TryOpen(path);

        return Array.Find(ProjectSettingsEdits, setting => setting.Path == path).Check(saving: true);
    }

    private static AssetMoveResult OnWillMoveAsset(string from, string to)
    {
        return Application.isBatchMode || TryOpenForPathChange(from)
            ? AssetMoveResult.DidNotMove
            : AssetMoveResult.FailedMove;
    }

    private static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
    {
        return Application.isBatchMode || TryOpenForPathChange(path)
            ? AssetDeleteResult.DidNotDelete
            : AssetDeleteResult.FailedDelete;
    }

    [MenuItem("Assets/Open for Edit (Git LFS)")]
    private static void OpenSelected()
    {
        foreach (UnityEngine.Object asset in Selection.objects)
            TryOpen(AssetDatabase.GetAssetPath(asset));
    }

    [MenuItem("Assets/Open for Edit (Git LFS)", true)]
    private static bool CanOpenSelected()
    {
        return Selection.objects.Any(asset => GetLockPath(AssetDatabase.GetAssetPath(asset)) != null);
    }

    private static bool TryOpen(string path)
    {
        string lockPath = GetLockPath(path);
        if (lockPath == null || CanWrite(lockPath))
            return true;

        try
        {
            EditableAsset asset = JsonUtility.FromJson<EditableAsset>(Run("Acquire", lockPath));
            _editable[lockPath] = asset;
            ++_editGeneration;
            _nextRefresh = 0;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Unity asset locks: {lockPath}\n{exception.Message}");
            return false;
        }
    }

    private static bool TryOpenForPathChange(string path)
    {
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(path))
            return TryOpen(path);

        EditorUtility.DisplayProgressBar("Git LFS", $"Checking asset locks in {path}", 0f);
        try
        {
            LockStatus status = JsonUtility.FromJson<LockStatus>(Run("AcquireFolder", path));
            foreach (EditableAsset asset in status.editable)
                _editable[asset.path] = asset;
            ++_editGeneration;
            _nextRefresh = 0;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Unity asset locks: {path}\n{exception.Message}");
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static bool CanWrite(string path)
    {
        if (!_editable.TryGetValue(path, out EditableAsset asset))
            return false;

        try
        {
            var file = new FileInfo(Path.Combine(ProjectRoot, path));
            if (file.Exists && file.IsReadOnly)
                return false;

            return string.IsNullOrEmpty(asset.record) || File.Exists(asset.record);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string GetLockPath(string path)
    {
        if (IsProjectSettingsPath(path))
            return path;

        if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            return null;

        if (path.EndsWith(".png", StringComparison.Ordinal))
            return path + ".meta";
        if (path.EndsWith(".unity", StringComparison.Ordinal) ||
            path.EndsWith(".prefab", StringComparison.Ordinal) ||
            path.EndsWith(".png.meta", StringComparison.Ordinal))
            return path;
        return null;
    }

    private static bool IsProjectSettingsPath(string path)
    {
        return path == Physics2DSettingsPath || path == TagManagerPath;
    }

    private static string Run(string command, string path = null)
    {
        var start = new ProcessStartInfo
        {
            FileName = Windows ? "powershell.exe" : "pwsh",
            WorkingDirectory = ProjectRoot,
            Arguments = "-NoProfile -NonInteractive " + (Windows ? "-ExecutionPolicy RemoteSigned " : "") +
                        $"-File tools/UnityAssetLocks/AssetLocks.ps1 {command}",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (path != null)
            start.Arguments += " -AssetPath \"" + path.Replace("\"", "\\\"") + "\"";
        if (Mac)
            ConfigureMacToolPath(start);

        using (Process process = Process.Start(start))
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            if (command == "Acquire" || command == "AcquireFolder")
            {
                process.WaitForExit(); // Let lock acquisition roll back if any path fails.
            }
            else if (!process.WaitForExit(25000))
            {
                process.Kill();
                throw new TimeoutException("Git LFS did not respond in time.");
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException(errors.GetAwaiter().GetResult().Trim());
            return output.GetAwaiter().GetResult();
        }
    }

    private static void ConfigureMacToolPath(ProcessStartInfo start)
    {
        char separator = Path.PathSeparator;
        string[] directories = (start.EnvironmentVariables["PATH"] ?? "")
            .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
            .Concat(new[] { "/opt/homebrew/bin", "/usr/local/bin" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        start.EnvironmentVariables["PATH"] = string.Join(separator.ToString(), directories);
        start.FileName = directories
            .Select(directory => Path.Combine(directory, "pwsh"))
            .FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("PowerShell 7 (pwsh) is required. Install it with 'brew install powershell'.");
    }
}
