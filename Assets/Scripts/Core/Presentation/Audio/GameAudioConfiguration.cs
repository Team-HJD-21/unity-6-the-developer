using UnityEngine;
using UnityEngine.Audio;

namespace TeamHJD.Game.Presentation
{
    [CreateAssetMenu(menuName = "The Developer/Audio/Configuration")]
    public sealed class GameAudioConfiguration : ScriptableObject
    {
        public AudioMixer Mixer;
        public AudioMixerGroup MusicOutput, CinematicOutput;
        public AudioSource VoicePrefab;
        public GameplayMusicLibrary GameplayMusic;
        public GameAudioCue[] Cues = System.Array.Empty<GameAudioCue>();
        [Range(4, 30)] public int PoolSize = 24;
        [Range(1, 4)] public int ReservedUIVoices = 2;
        public string MasterParameter = "MasterVolume", MusicParameter = "MusicVolume", EffectsParameter = "EffectsVolume";
    }
}
