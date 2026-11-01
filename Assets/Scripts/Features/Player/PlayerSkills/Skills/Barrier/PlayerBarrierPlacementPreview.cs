using UnityEngine;

/// <summary>방벽의 첫 펄스와 연결 미리보기 외형을 관리합니다.</summary>
public sealed class PlayerBarrierPlacementPreview
{
    private readonly PlayerFieldDevice _prefab;
    private readonly Vector3 _first;
    private GameObject _preview;
    private Material _previewMaterial;
    private LineRenderer _previewLine, _firstPulse, _secondPulse;
    private SpriteRenderer _secondEmitterPreview;
    public PlayerBarrierPlacementPreview(PlayerFieldDevice prefab, Vector3 start, float width, UnityEngine.SceneManagement.Scene ownerScene)
    {
        _prefab = prefab; _first = start;
            _preview = new GameObject("Barrier Placement Preview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_preview, ownerScene);
            _previewMaterial = new Material(Shader.Find("Sprites/Default"));
            _previewLine = PlayerFieldLineFactory.Line("Connection", _previewMaterial, width, Color.cyan, _preview.transform);
            _firstPulse = PlayerFieldLineFactory.Line("First Pulse", _previewMaterial, .07f, Color.cyan, _preview.transform);
            _secondPulse = PlayerFieldLineFactory.Line("Second Pulse Preview", _previewMaterial, .07f, Color.cyan, _preview.transform);
            if (prefab.EmitterSprite != null)
            {
                CreateEmitterPreview("First Emitter", start, Color.white);
                _secondEmitterPreview = CreateEmitterPreview("Second Emitter Preview", start, new Color(1, 1, 1, .5f));
            }
            PlayerFieldLineFactory.Circle(_firstPulse, start, .23f, 6);
            PlayerFieldLineFactory.Circle(_secondPulse, start, .23f, 6);
            PlayerFieldLineFactory.SetLine(_previewLine, start, start);
    }
    public void Refresh(Vector3 end, bool valid)
    {
        Color color = valid ? new Color(.15f, .95f, 1, .65f) : new Color(1, .25f, .2f, .65f);
        _previewLine.startColor = _previewLine.endColor = color;
        _secondPulse.startColor = _secondPulse.endColor = color;
        if (_secondEmitterPreview != null)
        {
            _secondEmitterPreview.transform.position = end;
            _secondEmitterPreview.color = new Color(color.r, color.g, color.b, .6f);
        }
        PlayerFieldLineFactory.SetLine(_previewLine, _first, end);
        PlayerFieldLineFactory.Circle(_secondPulse, end, .23f, 6);
    }

    private SpriteRenderer CreateEmitterPreview(string name, Vector3 position, Color color)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(_preview.transform, false);
        renderer.transform.position = position;
        renderer.sprite = _prefab.EmitterSprite;
        renderer.transform.localScale = _prefab.EmitterScale;
        renderer.color = color;
        renderer.sortingOrder = 110;
        return renderer;
    }
    public void Dispose()
    {
        if (_preview != null) _preview.SetActive(false);
        PlayerFieldDevice.Dispose(_preview);
        PlayerFieldDevice.Dispose(_previewMaterial);
        _preview = null;
        _previewMaterial = null;
        _secondEmitterPreview = null;
    }
}
