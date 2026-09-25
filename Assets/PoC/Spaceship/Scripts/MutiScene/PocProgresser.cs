using System.IO;
using PoC.Spaceship.Scripts.MutiScene;
using UnityEngine;

public class PocProgresser : MonoBehaviour
{
    [SerializeField] private PocSpaceshipProgressReader _reader;

    public bool TryUpgrade()
    {
        PocSpaceshipProgressData progress = _reader.ProgressData;
        if (progress.coin < 1)
        {
            return false;
        }

        progress.coin -= 1;
        progress.progressLevel += 1;
        string path = Path.Combine(
            Application.persistentDataPath,
            PocSpaceshipProgressReader.ProgressFileName
        );
        File.WriteAllText(path, JsonUtility.ToJson(progress, true));
        PocSaveSession.ProgressSaved = true;
        return true;
    }
}
