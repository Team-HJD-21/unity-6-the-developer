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

    [SerializeField] private TMP_Text _balance;
    [SerializeField] private Button _purchase;
    [SerializeField] private TMP_Text _purchaseLabel;

    private AppRoot _app;
    private bool _purchasing;
    private int? _shownBalance;

    private void OnEnable()
    {
        _app = FindAnyObjectByType<AppRoot>();
        _shownBalance = int.MinValue;
        if (_purchase != null) _purchase.onClick.AddListener(BuyArmor);
        Refresh();
    }

    private void OnDisable()
    {
        if (_purchase != null) _purchase.onClick.RemoveListener(BuyArmor);
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        int? balance = _app != null ? _app.Stage1TokenBalance : null;
        if (_balance != null && _shownBalance != balance)
        {
            _shownBalance = balance;
            _balance.text = balance.HasValue ? $"토큰 {balance.Value:N0}" : "토큰 —";
        }
        if (_purchase == null) return;

        bool owned = _app != null && _app.HasStage1CuArmor;
        _purchase.interactable = !_purchasing && _app != null && _app.CanBuyStage1CuArmor;
        if (_purchaseLabel != null)
        {
            string label = _purchasing ? "저장 중" : owned ? "구매 완료" : ArmorPrice;
            if (_purchaseLabel.text != label) _purchaseLabel.text = label;
        }
    }

    private async void BuyArmor()
    {
        if (_purchasing || _app == null || !_app.CanBuyStage1CuArmor) return;
        _purchasing = true;
        Refresh();
        try
        {
            if (await _app.BuyStage1CuArmorAsync())
                GameAlerts.Show("CU 장갑 I 구매 완료 · 다음 출격부터 최대 체력 +10%");
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            GameAlerts.Show("연구를 저장하지 못했습니다. 다시 시도해 주세요.");
        }
        finally
        {
            _purchasing = false;
            if (this != null && isActiveAndEnabled) Refresh();
        }
    }
}
