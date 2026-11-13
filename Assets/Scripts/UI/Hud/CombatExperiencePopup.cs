// 실제 경험치 획득 이벤트가 재사용하는 World Space UI입니다. 게임 수치나 별도 XP 원본을 소유하지 않습니다.
using TMPro;
using UnityEngine;

public sealed class CombatExperiencePopup : MonoBehaviour
{
    public TMP_Text Label;
    public CanvasGroup Group;
    private Transform _target;
    private Camera _camera;
    private float _started;
    private float _pixelScale = 1;
    public void Show(Transform target, Camera camera, int amount, float pixelScale = 1)
    {
        _target = target; _camera = camera; _started = Time.unscaledTime;
        _pixelScale = Mathf.Max(.1f, pixelScale);
        Label.text = $"+{amount} XP"; gameObject.SetActive(true); Update();
    }
    private void Update()
    {
        if (_target == null || _camera == null) { gameObject.SetActive(false); return; }
        float t = Time.unscaledTime - _started;
        if (t >= 1.1f) { gameObject.SetActive(false); return; }
        float units = _camera.orthographic ? _camera.orthographicSize * 2 / Mathf.Max(1, _camera.pixelHeight) :
            2 * Vector3.Distance(_camera.transform.position, _target.position) * Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Max(1, _camera.pixelHeight);
        units *= _pixelScale;
        transform.localScale = Vector3.one * units;
        transform.rotation = _camera.transform.rotation;
        transform.position = _target.position + _camera.transform.right * (-65 * units) + _camera.transform.up * ((85 + 30 * CombatHudMotion.Sigmoid(t / 1.1f)) * units);
        Group.alpha = t < .12f ? CombatHudMotion.Sigmoid(t / .12f) : t < .65f ? 1 : 1 - CombatHudMotion.Sigmoid((t - .65f) / .45f);
    }
}
