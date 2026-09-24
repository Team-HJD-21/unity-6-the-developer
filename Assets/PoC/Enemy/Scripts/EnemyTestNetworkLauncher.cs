using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 적 AI 네트워크 PoC에서 Host와 Client를 실행하고 연결 상태를 표시한다.
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

        // 백그라운드 테스트 유지
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

    /// <summary>
    /// 네트워크 실행 버튼과 연결 상태를 표시한다.
    /// </summary>
    private void OnGUI()
    {
        GUILayout.BeginArea(
            new Rect(20f, 20f, 240f, 180f),
            GUI.skin.box);

        GUILayout.Label("Enemy Network Test");

        if (_networkManager.IsListening)
            DrawConnectionStatus();
        else
            DrawLaunchButtons();

        GUILayout.EndArea();
    }

    private void DrawLaunchButtons()
    {
        if (GUILayout.Button("Start Host"))
            StartHost();

        if (GUILayout.Button("Start Client"))
            StartClient();
    }

    private void DrawConnectionStatus()
    {
        GUILayout.Label($"Mode: {GetCurrentMode()}");
        GUILayout.Label($"Client ID: {_networkManager.LocalClientId}");

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
        if (!_networkManager.StartHost())
        {
            // 테스트 실행 실패 원인을 확인하기 위해 오류 로그를 유지한다.
            Debug.LogError("Failed to start the network host.");
            return;
        }

        // 테스트 용 스폰
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