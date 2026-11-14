// 독립 Enemy 시험 Scene에 명시적으로 붙이는 수동 네트워크 버튼입니다.
// 기존 EnemyNetworkTestLauncher를 그대로 사용하며 정식 Scene에는 자동으로 추가하지 않습니다.
// 자동 생산·예산·저장 정책은 만들지 않고 현재 Match의 공통 요청 API만 사용합니다.
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(EncounterSceneBinding))]
[AddComponentMenu("TeamHJD/Testing/Enemy Network Test Controls")]
public sealed class EnemyNetworkTestControls : MonoBehaviour
{
    private EncounterSceneBinding _binding;
    private NetworkManager _network;
    private EnemyNetworkTestLauncher _launcher;
    private Scene _ownerScene;

    private void Awake()
    {
        _binding = GetComponent<EncounterSceneBinding>();
        _network = GetComponent<NetworkManager>();
        _ownerScene = gameObject.scene;
    }

    private void OnEnable()
    {
        _launcher = new EnemyNetworkTestLauncher(_binding, _network);
        _binding.DiagnosticEmitted += OnDiagnostic;
        BindTestOwner();
    }

    private void OnGUI()
    {
        if (!_binding.IsAppNetworkOwned)
            _launcher?.Draw();
    }

    private void OnDiagnostic(GameDiagnosticEvent observation)
    {
        if (_binding.IsAppNetworkOwned)
            return;
        if (observation.Kind == GameDiagnosticKind.MatchStarted)
            BindTestOwner();
        else if (observation.Kind == GameDiagnosticKind.MatchEnded &&
                 _launcher.HasOwner && _network != null && _network.IsListening)
            _network.Shutdown();
    }

    private void BindTestOwner()
    {
        // Match 없는 버튼 표시만으로 연결 소유권을 획득하지 않습니다.
        if (!_binding.IsAppNetworkOwned && !_binding.BoundMatchId.IsEmpty)
            _launcher.BindOwner(_ownerScene);
    }

    private void OnDisable()
    {
        if (_binding != null)
            _binding.DiagnosticEmitted -= OnDiagnostic;
        if (_binding != null && !_binding.IsAppNetworkOwned && _launcher != null &&
            _launcher.HasOwner && _network != null && _network.IsListening)
            _network.Shutdown();
        _launcher?.Dispose();
        _launcher = null;
    }
}
