using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;

public class PocProgressViewer : PocInteractableViewer
{
    [SerializeField] private PocSpaceshipProgressReader _reader;
    [SerializeField] private PocProgresser _progresser;

    protected override string ActionText => "needed coin -> 1";

    public override void Interact()
    {
        if (_progresser.TryUpgrade())
        {
            Refresh();
        }
    }

    public override void Refresh()
    {
        PocSpaceshipProgressData progress = _reader.ProgressData;
        ShowState($"progresslevel: {progress.progressLevel} / coin: {progress.coin}");
        Debug.Log($"progress.progressLevel {progress.progressLevel} ==== progress.coin {progress.coin}");
    }
}
