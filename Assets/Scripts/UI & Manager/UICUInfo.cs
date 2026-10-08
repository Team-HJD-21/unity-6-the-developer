using System.Collections;
using TeamHJD.Game.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICUInfo : MonoBehaviour
{
    private ControlUnitHealth controlUnitHealth;
    private TurretController turretController;
    private float powerRatio = 1f;

    public TMP_Text powerText;
    public TMP_Text hpText;

    [Header("Filler")]
    public Image filledHpImage;
    public Image filledPowerImage;

    private void Awake()
    {
        GameObject controlUnit = GameObject.FindGameObjectWithTag("CU");
        if (controlUnit != null)
        {
            controlUnitHealth = controlUnit.GetComponent<ControlUnitHealth>();
        }

        turretController = TurretController.GetOrCreateForScene(gameObject.scene);

        if (hpText == null)
        {
            GameObject hpTextObject = GameObject.FindWithTag("CUHpText");
            if (hpTextObject != null)
            {
                hpText = hpTextObject.GetComponent<TMP_Text>();
            }
        }

        if (controlUnitHealth != null)
        {
            bool hasPersistentListener = false;
            for (int i = 0; i < controlUnitHealth.onCUHpChange.GetPersistentEventCount(); i++)
            {
                hasPersistentListener |= controlUnitHealth.onCUHpChange.GetPersistentTarget(i) == this &&
                                         controlUnitHealth.onCUHpChange.GetPersistentMethodName(i) == nameof(SetUICUHpInfo);
            }

            if (!hasPersistentListener)
            {
                controlUnitHealth.onCUHpChange.AddListener(SetUICUHpInfo);
            }
        }

        turretController.PowerChanged += HandlePowerChanged;
    }

    private void Start()
    {
        if (controlUnitHealth != null)
        {
            SetUICUHpInfo(controlUnitHealth.CurrentHealth, controlUnitHealth.MaxHealth, 1f);
        }

        HandlePowerChanged(turretController.CurrentPower, turretController.MaximumPower);
    }

    private void OnDestroy()
    {
        if (controlUnitHealth != null)
        {
            controlUnitHealth.onCUHpChange.RemoveListener(SetUICUHpInfo);
        }

        if (turretController != null)
        {
            turretController.PowerChanged -= HandlePowerChanged;
        }
    }

    public void SetUICUHpInfo(int curHp, int maxHp, float preRatio)
    {
        float ratio = maxHp > 0 ? curHp / (float)maxHp : 0f;
        if (filledHpImage != null)
        {
            if (preRatio > ratio)
            {
                StartCoroutine(UpdateFill(filledHpImage, preRatio, ratio));
            }
            else
            {
                filledHpImage.fillAmount = ratio;
            }
        }

        if (hpText != null)
        {
            hpText.SetText(Mathf.RoundToInt(ratio * 100f) + "%");
        }
    }

    public void SetUICUPowerInfo(int curPower, int maxPower, float preRatio)
    {
        float ratio = maxPower > 0 ? curPower / (float)maxPower : 0f;
        if (filledPowerImage != null)
        {
            if (preRatio > ratio)
            {
                StartCoroutine(UpdateFill(filledPowerImage, preRatio, ratio));
            }
            else
            {
                filledPowerImage.fillAmount = ratio;
            }
        }

        if (powerText != null)
        {
            powerText.SetText(Mathf.RoundToInt(ratio * 100f) + "%");
        }

        powerRatio = ratio;
    }

    private void HandlePowerChanged(int curPower, int maxPower)
    {
        SetUICUPowerInfo(curPower, maxPower, powerRatio);
    }

    private static IEnumerator UpdateFill(Image image, float from, float to)
    {
        float value = from;
        while (value > to)
        {
            value = Mathf.Max(to, value - 0.01f);
            image.fillAmount = value;
            yield return new WaitForSeconds(0.01f);
        }
    }
}
