using System.IO;
using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;

public class PocMissionProgresser : MonoBehaviour
{
    [SerializeField] private PocSpaceshipProgressReader _reader;

    public void AdvanceMission()
    {
        PocSpaceshipProgressMissionData mission = _reader.MissionData;
        mission.selectedMissionId = mission.selectedMissionId == 1 ? 2 : 1;

        string path = Path.Combine(
            Application.persistentDataPath,
            PocSpaceshipProgressReader.MissionFileName
        );
        File.WriteAllText(path, JsonUtility.ToJson(mission, true));
        PocSaveSession.MissionSaved = true;
    }
}
