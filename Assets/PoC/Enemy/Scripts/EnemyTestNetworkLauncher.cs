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

    [SerializeField] private PoCMonsterSpawner monsterSpawner;

    private NetworkManager _networkManager;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();

        // Host와 Client 창의 포커스가 바뀌어도 테스트가 계속 실행되도록 한다.
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

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

    private void StartHost()
    {
        if (!_networkManager.StartHost())
        {
            Debug.LogError("Failed to start the network host.");
            return;
        }

        monsterSpawner.Spawn();
    }

    private void StartClient()
    {
        if (!_networkManager.StartClient())
            Debug.LogError("Failed to start the network client.");
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
