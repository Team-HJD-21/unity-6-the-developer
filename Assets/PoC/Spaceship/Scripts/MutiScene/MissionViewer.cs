using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;

public class MissionViewer : InteractableViewer
{
    [SerializeField] private SpaceshipProgressReader _reader;
    [SerializeField] private MissionProgresser _progresser;

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
