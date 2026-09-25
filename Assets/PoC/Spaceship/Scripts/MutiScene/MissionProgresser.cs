using System.IO;
using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;

public class MissionProgresser : MonoBehaviour
{
    [SerializeField] private SpaceshipProgressReader _reader;

    public void AdvanceMission()
    {
        SpaceshipProgressMissionData mission = _reader.MissionData;
        mission.selectedMissionId = mission.selectedMissionId == 1 ? 2 : 1;

        string path = Path.Combine(
            Application.persistentDataPath,
            SpaceshipProgressReader.MissionFileName
        );
        File.WriteAllText(path, JsonUtility.ToJson(mission, true));
        SaveSession.MissionSaved = true;
    }
}
