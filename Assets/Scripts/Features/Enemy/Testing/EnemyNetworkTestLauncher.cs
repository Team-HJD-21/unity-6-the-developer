// 독립 Enemy 시험의 수동 Host/Client 버튼을 제공하는 얇은 도구입니다.
// 실제 생성은 EncounterSceneBinding의 공통 요청으로 전달하며 게임의 생산 정책은 소유하지 않습니다.
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
internal sealed class EnemyNetworkTestLauncher : IDisposable
{
    private readonly EncounterSceneBinding _binding;
    private readonly NetworkManager _networkManager;
    private Scene _ownerScene;
    public bool HasOwner => _ownerScene.IsValid();
    public EnemyNetworkTestLauncher(EncounterSceneBinding binding, NetworkManager network)
    { _binding = binding; _networkManager = network; }
    public void BindOwner(Scene owner)
    {
        if (!owner.IsValid() || !owner.isLoaded) return;
        _ownerScene = owner;
        SceneManager.sceneUnloaded -= OnOwnerUnloaded;
        SceneManager.sceneUnloaded += OnOwnerUnloaded;
    }
    private void OnOwnerUnloaded(Scene scene)
    {
        if (scene.handle != _ownerScene.handle) return;
        Dispose();
        if (_networkManager != null && _networkManager.IsListening) _networkManager.Shutdown();
        if (_binding != null) UnityEngine.Object.Destroy(_binding.gameObject);
    }
    public void Dispose()
    {
        SceneManager.sceneUnloaded -= OnOwnerUnloaded;
        _ownerScene = default;
    }

    public void Draw()
    {
        if (_networkManager == null) return;
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

    private void DrawLaunchButtons()
    {
        if (GUILayout.Button("Start Host"))
            StartHost();

        if (GUILayout.Button("Start Client"))
            StartClient();
    }

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
            {
                if (!_binding.RequestSpawn(out string result))
                    Debug.LogWarning(result, _binding);
            }
        }

        if (GUILayout.Button("Shutdown"))
            _networkManager.Shutdown();
    }

    public void StartHost()
    {
        if (!_binding.ResolveSpawnSetup())
        {
            Debug.LogError("Spawner or squad preset catalog is missing for the spawn test.");
            return;
        }

        Application.runInBackground = true;
        // Host 시작과 스폰 요청은 독립된 테스트 조작입니다.
        if (!_networkManager.StartHost())
        {
            Debug.LogError("Failed to start the network host.");
            return;
        }
    }

    private void StartClient()
    {
        if (!_networkManager.StartClient())
        {
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
