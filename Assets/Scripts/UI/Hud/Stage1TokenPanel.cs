using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>필드·허브 상태창에서 로컬 잔액을 표시합니다. 구매 버튼이 배치된 허브 화면만 요청을 전달합니다.</summary>
[DisallowMultipleComponent]
public sealed class Stage1TokenPanel : MonoBehaviour
{
    private static readonly string ArmorPrice = $"토큰 {Stage1TokenProgress.CuArmorCost}";
    private const string RetryFailureMessage = "토큰을 확인할 수 없습니다.";

    [SerializeField] private TMP_Text _balance;
    [SerializeField] private Button _purchase;
    [SerializeField] private TMP_Text _purchaseLabel;

    private AppRoot _app;
    private bool _purchasing;
    private bool _retryFailed;
    private int? _shownBalance;
    private bool _shownStorageError;

    private void OnEnable()
    {
        _app = FindAnyObjectByType<AppRoot>();
        _retryFailed = false;
        _shownBalance = int.MinValue;
        if (_purchase != null) _purchase.onClick.AddListener(HandlePress);
        Refresh();
    }

    private void OnDisable()
    {
        if (_purchase != null) _purchase.onClick.RemoveListener(HandlePress);
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        int? balance = _app != null ? _app.Stage1TokenBalance : null;
        bool storageError = _app != null && _app.HasStage1TokenStorageError;
        if (_balance != null && (_shownBalance != balance || _shownStorageError != storageError))
        {
            _shownBalance = balance;
            _shownStorageError = storageError;
            _balance.text = storageError ? "토큰 확인 실패" : balance.HasValue ? $"토큰 {balance.Value:N0}" : "토큰 —";
        }
        if (_purchase == null) return;

        bool owned = _app != null && _app.HasStage1CuArmor;
        _purchase.interactable = !_purchasing && _app != null && (_app.CanRetryStage1TokenStorage || _app.CanBuyStage1CuArmor);
        if (_purchaseLabel != null)
        {
            string label = _purchasing ? "처리 중" : storageError
                ? (_retryFailed ? "재시도 실패" : "다시 시도")
                : owned ? "구매 완료" : ArmorPrice;
            if (_purchaseLabel.text != label) _purchaseLabel.text = label;
        }
    }

    private async void HandlePress()
    {
        if (_purchasing || _app == null) return;
        bool retry = _app.CanRetryStage1TokenStorage;
        if (!retry && !_app.CanBuyStage1CuArmor) return;
        _purchasing = true;
        _retryFailed = false;
        Refresh();
        try
        {
            if (retry)
            {
                if (await _app.RetryStage1TokenStorageAsync())
                    GameAlerts.Show($"토큰 +{Stage1TokenProgress.FirstBossReward}");
                else if (_app != null && _app.HasStage1TokenStorageError)
                {
                    _retryFailed = true;
                    GameAlerts.Show(RetryFailureMessage);
                }
            }
            else if (await _app.BuyStage1CuArmorAsync())
                GameAlerts.Show("CU 장갑 I 구매 완료 · 다음 출격부터 최대 체력 +10%");
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (retry) _retryFailed = true;
            Debug.LogException(error, this);
            GameAlerts.Show(retry ? RetryFailureMessage : "연구를 저장하지 못했습니다. 다시 시도해 주세요.");
        }
        finally
        {
            _purchasing = false;
            if (this != null && isActiveAndEnabled) Refresh();
        }
    }
}
