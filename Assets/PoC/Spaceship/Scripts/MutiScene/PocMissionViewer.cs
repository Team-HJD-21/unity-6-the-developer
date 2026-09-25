using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;

public class PocMissionViewer : PocInteractableViewer
{
    [SerializeField] private PocSpaceshipProgressReader _reader;
    [SerializeField] private PocMissionProgresser _progresser;

    protected override string ActionText => "mission level";

    public override void Interact()
    {
        _progresser.AdvanceMission();
        Refresh();
    }

    public override void Refresh()
    {
        ShowState($"mission: {_reader.MissionData.selectedMissionId}");
        Debug.Log("mission level" +  _reader.MissionData.selectedMissionId);
    }
}
