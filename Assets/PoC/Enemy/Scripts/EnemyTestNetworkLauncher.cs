using Unity.Netcode;
using UnityEngine;


// 적 AI 네트워크 PoC에서 Host와 Client를 실행하고
// 현재 네트워크 연결 상태를 확인하기 위한 테스트용 런처
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class EnemyTestNetworkLauncher : MonoBehaviour
{
    // 현재 네트워크 관리자
    private NetworkManager _networkManager;

    // 임시 스폰너
    public PoCMonsterSpawner  monsterSpawner;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();

        Application.runInBackground = true;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
    }

    private void OnGUI()
    {
        // 네트워크 테스트 UI가 표시될 영역
        GUILayout.BeginArea(
            new Rect(20f, 20f, 240f, 180f),
            GUI.skin.box);

        GUILayout.Label("Enemy Network Test");

        // 네트워크가 실행 중이면 연결 정보를 표시하고,
        // 실행 전이면 Host 및 Client 시작 버튼을 표시한다
        if (_networkManager.IsListening)
        {
            DrawConnectionStatus();
        }
        else
        {
            DrawLaunchButtons();
        }

        GUILayout.EndArea();
    }

    /// <summary>
    /// Host 또는 Client로 네트워크를 시작하는 버튼을 표시한다.
    /// </summary>
    private void DrawLaunchButtons()
    {
        if (GUILayout.Button("Start Host"))
        {
            StartHost();
        }

        if (GUILayout.Button("Start Client"))
        {
            StartClient();
        }
    }

    /// <summary>
    /// 현재 네트워크 모드와 연결 상태를 표시한다.
    /// </summary>
    private void DrawConnectionStatus()
    {
        GUILayout.Label($"Mode: {GetCurrentMode()}");
        GUILayout.Label($"Client ID: {_networkManager.LocalClientId}");

        // 서버 또는 Host인 경우 현재 연결된 클라이언트 수를 표시한다.
        if (_networkManager.IsServer)
        {
            GUILayout.Label(
                $"Connected Clients: {_networkManager.ConnectedClientsIds.Count}");
        }

        // 현재 네트워크 세션을 종료한다.
        if (GUILayout.Button("Shutdown"))
        {
            _networkManager.Shutdown();
        }
    }

    /// <summary>
    /// 이 애플리케이션을 Host로 시작한다.
    /// </summary>
    private void StartHost()
    {
        if (!_networkManager.StartHost())
        {
            Debug.LogError("Failed to start the network host.");
        }
        monsterSpawner.Spawn();
    }

    /// <summary>
    /// 이 애플리케이션을 Client로 시작하여 Host에 접속을 시도한다.
    /// </summary>
    private void StartClient()
    {
        if (!_networkManager.StartClient())
        {
            Debug.LogError("Failed to start the network client.");
        }
    }

    /// <summary>
    /// 현재 실행 중인 네트워크 모드를 반환한다.
    /// </summary>
    private string GetCurrentMode()
    {
        if (_networkManager.IsHost)
        {
            return "Host";
        }

        if (_networkManager.IsServer)
        {
            return "Server";
        }

        if (_networkManager.IsClient)
        {
            return "Client";
        }

        return "Offline";
    }
}
