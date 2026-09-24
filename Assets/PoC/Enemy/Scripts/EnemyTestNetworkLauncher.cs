using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 적 AI 네트워크 PoC를 실행하기 위한 테스트 UI다.
/// Host와 Client 시작, 연결 상태 확인, 테스트 몬스터 생성을 담당한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class EnemyTestNetworkLauncher : MonoBehaviour
{
    private const int TargetFrameRate = 120;

    [Header("참조")] [SerializeField] private PoCMonsterSpawner monsterSpawner;

    private NetworkManager _networkManager;

    /// <summary>
    /// 네트워크 관리자와 테스트 실행 환경을 초기화한다.
    /// </summary>
    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();

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
            GUILayout.Label($"Connected Clients: {_networkManager.ConnectedClientsIds.Count}");

        if (GUILayout.Button("Shutdown"))
            _networkManager.Shutdown();
    }

    /// <summary>
    /// 호스트를 시작하고 테스트 몬스터를 생성한다.
    /// </summary>
    private void StartHost()
    {
        // Host 실행에 실패하면 몬스터를 생성하지 않는다.
        if (!_networkManager.StartHost())
        {
            // 테스트 실행 실패 원인을 확인하기 위해 오류 로그를 유지한다.
            Debug.LogError("Failed to start the network host.");
            return;
        }

        // 타깃 선택 동작을 비교할 테스트 몬스터 세 마리를 생성한다.
        monsterSpawner.Spawn();
        monsterSpawner.Spawn();
        monsterSpawner.Spawn();
    }

    /// <summary>
    /// 클라이언트 연결을 시작한다.
    /// </summary>
    private void StartClient()
    {
        if (!_networkManager.StartClient())
        {
            // 테스트 실행 실패 원인을 확인하기 위해 오류 로그를 유지한다.
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
