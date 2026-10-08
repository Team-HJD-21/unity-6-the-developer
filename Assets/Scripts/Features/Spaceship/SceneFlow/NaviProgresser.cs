using System.IO;
using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;

public class NaviProgresser : MonoBehaviour
{
    [SerializeField] private SpaceshipProgressReader _reader;

    public bool TryUpgrade()
    {
        SpaceshipProgressData progress = _reader.ProgressData;
        if (progress.coin < 1)
        {
            return false;
        }

        progress.coin -= 1;
        progress.progressLevel += 1;
        string path = Path.Combine(
            Application.persistentDataPath,
            SpaceshipProgressReader.ProgressFileName
        );
        File.WriteAllText(path, JsonUtility.ToJson(progress, true));
        SaveSession.ProgressSaved = true;
        return true;
    }
}
