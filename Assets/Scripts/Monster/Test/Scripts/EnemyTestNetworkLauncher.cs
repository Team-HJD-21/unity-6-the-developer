using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class EnemyTestNetworkLauncher : MonoBehaviour
{
    private NetworkManager _networkManager;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(
            new Rect(20f, 20f, 240f, 180f),
            GUI.skin.box);

        GUILayout.Label("Enemy Network Test");

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

    private void DrawConnectionStatus()
    {
        GUILayout.Label($"Mode: {GetCurrentMode()}");
        GUILayout.Label($"Client ID: {_networkManager.LocalClientId}");

        if (_networkManager.IsServer)
        {
            GUILayout.Label(
                $"Connected Clients: {_networkManager.ConnectedClientsIds.Count}");
        }

        if (GUILayout.Button("Shutdown"))
        {
            _networkManager.Shutdown();
        }
    }

    private void StartHost()
    {
        if (!_networkManager.StartHost())
        {
            Debug.LogError("Failed to start the network host.");
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