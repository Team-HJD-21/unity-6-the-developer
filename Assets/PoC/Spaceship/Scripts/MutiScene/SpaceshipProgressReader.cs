using System;
using System.IO;
using UnityEngine;

namespace PoC.Spaceship.Scripts.MutiScene
{
    [Serializable]
    public class SpaceshipProgressMissionData
    {
        public int selectedMissionId = 1;
    }

    [Serializable]
    public class SpaceshipProgressData
    {
        public int coin = 3;
        public int progressLevel = 1;
    }

    public class SpaceshipProgressReader : MonoBehaviour
    {
        // 초기 설정 Json
        [SerializeField] private TextAsset initialMissionJson;
        [SerializeField] private TextAsset initialProgressJson;

        public const string MissionFileName = "PocMissonData.json";
        public const string ProgressFileName = "PocProgressData.json";

        [SerializeField] private SpaceshipProgressMissionData _missionData = new SpaceshipProgressMissionData();
        public SpaceshipProgressMissionData MissionData => _missionData;
        [SerializeField] private SpaceshipProgressData _progressData = new SpaceshipProgressData();
        public SpaceshipProgressData ProgressData => _progressData;

        private void Awake()
        {
            // Spaceship Load 시 바로 json읽기
            Load(initialMissionJson, MissionFileName, MissionData, SaveSession.MissionSaved);
            Load(initialProgressJson, ProgressFileName, ProgressData, SaveSession.ProgressSaved);
        }

        public static void Load<T>(TextAsset initialJson, string file, T target, bool savedThisSession) where T : class
        {
            // 경로 파일이름
            string path = Path.Combine(
                Application.persistentDataPath,
                file
            );
            // json 읽어오기 (PIE 검증용)
            string json;
            if (savedThisSession && File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
            else
            {
                if (initialJson == null)
                {
                    Debug.LogError($"Initial JSON for '{file}' is not assigned.");
                    return;
                }

                json = initialJson.text;
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                // 객체에 json 데이터 덮어쓰기
                JsonUtility.FromJsonOverwrite(json, target);
            }
        }
    }

    // 검증용 클래스 
    public static class SaveSession
    {
        public static bool MissionSaved;
        public static bool ProgressSaved;

        // Unity가 게임 실행을 시작할 때 특정 정적 메서드를 자동 호출하도록 지정
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // 이번에 저장을 했는지
            MissionSaved = false;
            ProgressSaved = false;
        }
    }
}
