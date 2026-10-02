using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 적 스폰의 Host·Client 동기화를 확인하기 위한 PoC 테스트 UI다.
/// 네트워크 시작과 연결 상태 표시, Host의 임시 분대 생성 요청을 담당한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class EnemyNetworkTestLauncher : MonoBehaviour, IEncounterDebugCommandTarget
{
    private const int TargetFrameRate = 120;

    private NetworkManager _networkManager;
    private EncounterRuntime _encounterRuntime;

    /// <summary>
    /// 네트워크 관리자와 테스트 실행 환경을 초기화한다.
    /// Encounter가 사용할 분대 프리셋 카탈로그는 생성 시 한 번 로드한다.
    /// </summary>
    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        _encounterRuntime = null;

        // Host와 Client 창이 포커스를 잃어도 네트워크 테스트를 계속 실행한다.
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

    /// <summary>
    /// 네트워크 실행 버튼과 연결 상태를 표시한다.
    /// </summary>
    private void OnGUI()
    {
        // 네트워크 테스트 UI 영역을 생성한다.
        GUILayout.BeginArea(
            new Rect(20f, 20f, 240f, 180f),
            GUI.skin.box);

        GUILayout.Label("Enemy Network Test");

        // 실행 상태에 따라 연결 정보 또는 시작 버튼을 표시한다.
        if (_networkManager.IsListening)
            DrawConnectionStatus();
        else
            DrawLaunchButtons();

        GUILayout.EndArea();
    }

    /// <summary>
    /// 네트워크가 실행 중이 아닐 때 Host와 Client 시작 버튼을 표시한다.
    /// </summary>
    private void DrawLaunchButtons()
    {
        if (GUILayout.Button("Start Host"))
            StartHost();

        if (GUILayout.Button("Start Client"))
            StartClient();
    }

    /// <summary>
    /// 현재 네트워크 모드와 연결 정보를 표시한다.
    /// </summary>
    private void DrawConnectionStatus()
    {
        // 현재 인스턴스의 네트워크 상태를 표시한다.
        GUILayout.Label($"Mode: {GetCurrentMode()}");
        GUILayout.Label($"Client ID: {_networkManager.LocalClientId}");

        // 서버에서만 전체 접속 인원을 확인할 수 있다.
        if (_networkManager.IsServer)
        {
            GUILayout.Label($"Connected Clients: {_networkManager.ConnectedClientsIds.Count}");

            // 점령 상태를 바꾼 뒤에도 같은 Host에서 다시 계획해 확인할 수 있다.
            if (GUILayout.Button("Plan & Spawn"))
                _encounterRuntime.Spawn();
        }

        if (GUILayout.Button("Shutdown"))
            _networkManager.Shutdown();
    }

    /// <summary>
    /// 네트워크 호스트만 시작한다. Encounter 요청은 Battlefield Debug Window 명령으로 분리한다.
    /// </summary>
    private void StartHost()
    {
        _encounterRuntime ??= new EncounterRuntime();
        if (!_encounterRuntime.HasExecutor || !_encounterRuntime.HasCatalog)
        {
            Debug.LogError("Spawner or squad preset catalog is missing for the spawn test.");
            return;
        }

        // Host 시작에 실패하면 스폰 계획을 실행하지 않는다.
        if (!_networkManager.StartHost())
        {
            Debug.LogError("Failed to start the network host.");
            return;
        }
    }

    /// <summary>Debug Window가 현재 Match snapshot을 전달하고 Encounter 요청을 시작합니다.</summary>
    public bool CanRequestEncounter(out string reason)
    {
        if (_networkManager == null)
        {
            reason = "NetworkManager가 없습니다.";
            return false;
        }

        _encounterRuntime ??= new EncounterRuntime();
        if (!_encounterRuntime.HasExecutor || !_encounterRuntime.HasCatalog)
        {
            reason = "EnemySpawnExecutor 또는 EnemySquadPresetCatalog가 없습니다.";
            return false;
        }

        if (_networkManager.IsListening && !_networkManager.IsServer)
        {
            reason = "현재 Client 인스턴스에서는 Encounter를 요청할 수 없습니다.";
            return false;
        }

        reason = "준비됨. 요청 시 Host를 시작한 뒤 Encounter를 실행합니다.";
        return true;
    }

    /// <summary>Host 권한을 확보한 뒤 Match 공간 snapshot과 테스트 요청을 EncounterRuntime에 전달합니다.</summary>
    public bool TryRequestEncounter(
        BattlefieldSpatialSnapshot snapshot,
        string squadOrder,
        int maxEnemyCount,
        out string result)
    {
        if (snapshot == null)
        {
            result = "활성 Match Battlefield snapshot이 없습니다.";
            return false;
        }

        if (!CanRequestEncounter(out result)) return false;

        if (!_networkManager.IsListening && !_networkManager.StartHost())
        {
            result = "NGO Host를 시작하지 못했습니다.";
            return false;
        }

        _encounterRuntime ??= new EncounterRuntime();
        return _encounterRuntime.TrySpawn(snapshot, squadOrder, maxEnemyCount, out result);
    }

    /// <summary>
    /// 클라이언트 연결을 시작한다.
    /// </summary>
    private void StartClient()
    {
        if (!_networkManager.StartClient())
        {
            Debug.LogError("Failed to start the network client.");
        }
    }

    /// <summary>
    /// 현재 실행 중인 네트워크 모드 이름을 반환한다.
    /// </summary>
    private string GetCurrentMode()
    {
        if (_networkManager.IsHost)
            return "Host";

        if (_networkManager.IsServer)
            return "Server";

        if (_networkManager.IsClient)
            return "Client";

        return "Offline";
    }
}
