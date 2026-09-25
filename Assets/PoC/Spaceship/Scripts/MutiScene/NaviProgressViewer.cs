using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;
using UnityEngine.Serialization;

public class NaviProgressViewer : InteractableViewer
{
    [SerializeField] private SpaceshipProgressReader _reader;
    [SerializeField] private NaviProgresser naviProgresser;

    protected override string ActionText => "needed coin -> 1";

    public override void Interact()
    {
        if (naviProgresser.TryUpgrade())
        {
            Refresh();
        }
    }

    public override void Refresh()
    {
        SpaceshipProgressData progress = _reader.ProgressData;
        ShowState($"progresslevel: {progress.progressLevel} / coin: {progress.coin}");
        Debug.Log($"progress.progressLevel {progress.progressLevel} ==== progress.coin {progress.coin}");
    }
}
