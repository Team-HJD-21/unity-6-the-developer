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
    }

    [Serializable]
    private sealed class EditableAsset
    {
        public string path = "";
        public string record = "";
    }

    [Serializable]
    private sealed class LockRecord
    {
        public string submitted = "";
    }

    private static readonly string ProjectRoot = Path.GetDirectoryName(Application.dataPath);
    private static readonly bool Windows = Application.platform == RuntimePlatform.WindowsEditor;
    private static readonly bool Mac = Application.platform == RuntimePlatform.OSXEditor;
    private static HashSet<string> _tracked = new HashSet<string>(StringComparer.Ordinal);
    private static Dictionary<string, string> _editable = new Dictionary<string, string>(StringComparer.Ordinal);
    private static Task<string> _refresh;
    private static double _nextRefresh;
    private static bool _refreshFailed;

    static UnityAssetLocks()
    {
        if (!Application.isBatchMode)
            EditorApplication.update += Update;
    }

    private static void Update()
    {
        if (_refresh != null)
        {
            if (!_refresh.IsCompleted)
                return;

            try
            {
                string json = _refresh.GetAwaiter().GetResult();
                LockStatus status = JsonUtility.FromJson<LockStatus>(json);
                _tracked = new HashSet<string>(status.tracked, StringComparer.Ordinal);
                _editable = status.editable.ToDictionary(asset => asset.path, asset => asset.record, StringComparer.Ordinal);
                _refreshFailed = false;
            }
            catch (Exception exception)
            {
                if (!_refreshFailed)
                    Debug.LogWarning($"Unity asset locks: {exception.Message}");
                _refreshFailed = true;
            }
            _refresh = null;
        }

        if (EditorApplication.timeSinceStartup >= _nextRefresh)
        {
            _nextRefresh = EditorApplication.timeSinceStartup + 45;
            _refresh = Task.Run(() => Run("Sync"));
        }
    }

    private static void IsOpenForEdit(string[] paths, List<string> notEditable, StatusQueryOptions options)
    {
        if (Application.isBatchMode)
            return;

        foreach (string path in paths)
        {
            string lockPath = GetLockPath(path);
            if (lockPath != null && _tracked.Contains(lockPath) && !CanWrite(lockPath))
                notEditable.Add(path);
        }
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
        return Application.isBatchMode ? paths : paths.Where(TryOpen).ToArray();
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
            _editable[lockPath] = asset.record;
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
                _editable[asset.path] = asset.record;
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
        if (!_editable.TryGetValue(path, out string record))
            return false;

        try
        {
            var file = new FileInfo(Path.Combine(ProjectRoot, path));
            if (file.Exists && file.IsReadOnly)
                return false;

            return string.IsNullOrEmpty(record) ||
                   string.IsNullOrEmpty(JsonUtility.FromJson<LockRecord>(File.ReadAllText(record)).submitted);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string GetLockPath(string path)
    {
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
            if (command != "Sync")
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
