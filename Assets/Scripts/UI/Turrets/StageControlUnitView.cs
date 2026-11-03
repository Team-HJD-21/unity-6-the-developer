// CU 자동 활성화 버튼과 정적·동적 지역 상태를 표시합니다.
// 전력·대기시간·터렛 활성화 판단은 D의 Controller가 소유하며 View는 입력 의도만 보냅니다.
using System;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.FieldTurrets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[DisallowMultipleComponent]
public sealed class StageControlUnitView : MonoBehaviour
{
    [SerializeField] private TMP_Text regionText;
    [SerializeField] private TMP_Text autoStatusText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button autoActivationButton;
    public event Action AutoActivationRequested;
    public bool IsConfigured => regionText != null && autoStatusText != null && feedbackText != null && autoActivationButton != null;
    public string RegionSummary => regionText != null ? regionText.text : "";
    private void OnEnable()
    {
        if (autoActivationButton != null) autoActivationButton.onClick.AddListener(RequestAutoActivation);
    }
    private void OnDisable()
    {
        if (autoActivationButton != null) autoActivationButton.onClick.RemoveListener(RequestAutoActivation);
    }
    public void Refresh(FieldTurretController controller, bool canInteractAtControlUnit)
    {
        if (!IsConfigured) return;
        RegionGridSnapshot snapshot = controller?.Regions?.Snapshot;
        regionText.text = snapshot == null ? "Region: waiting for Match" :
            (snapshot.Mode == RegionPolicyMode.Dynamic ? "DYNAMIC - no region locks\nActivate turrets to capture" :
                "STATIC - unlock regions to activate") +
            $" | {snapshot.CapturedCellCount}/{snapshot.CapturedCells.Count} cells";
        bool running = controller != null && controller.IsAutoActivationRunning;
        float remaining = controller != null ? controller.AutoActivationCooldownRemaining : 0;
        autoStatusText.text = controller == null ? "Waiting for CU power" : running ? "AUTO ACTIVATION - RUNNING" :
            remaining > 0 ? $"Auto activation cooldown: {remaining:0.0}s" :
            !canInteractAtControlUnit ? "Approach the CU to auto activate" : "CU auto activation is ready";
        autoActivationButton.interactable = controller != null && canInteractAtControlUnit && !running && remaining <= 0;
    }
    public void SetFeedback(string message) { if (feedbackText != null) feedbackText.text = message; }
    public void RefreshAuthority(TurretControllerPresentationSnapshot controller, RegionGridSnapshot region,
        bool canInteractAtControlUnit, bool pending)
    {
        if (!IsConfigured) return;
        if (controller == null)
        {
            regionText.text = "Region: waiting for Host state"; autoStatusText.text = "Waiting for Host state";
            autoActivationButton.interactable = false; return;
        }
        if (region != null && (region.Mode != controller.RegionMode || region.GridId != controller.RegionGridId ||
            region.Revision != controller.RegionRevision)) region = null;
        regionText.text = (controller.RegionMode == RegionPolicyMode.Dynamic ? "DYNAMIC - no region locks\nActivate turrets to capture" :
            "STATIC - unlock regions to activate") + (region == null ? " | region snapshot pending" : $" | {region.CapturedCellCount}/{region.CapturedCells.Count} cells");
        autoStatusText.text = !controller.IsControlUnitAvailable || controller.ControlUnitHealth <= 0 ? "Control unit unavailable" :
            pending ? "Waiting for Host confirmation/state" : controller.IsAutoActivationRunning ? "AUTO ACTIVATION - RUNNING" :
            controller.AutoActivationCooldown > 0 ? $"Auto activation cooldown: {controller.AutoActivationCooldown:0.0}s" :
            !canInteractAtControlUnit ? "Approach the CU to auto activate" : "CU auto activation is ready";
        autoActivationButton.interactable = canInteractAtControlUnit && !pending && !controller.IsAutoActivationRunning && controller.AutoActivationCooldown <= 0;
    }
    private void RequestAutoActivation() => AutoActivationRequested?.Invoke();
}
