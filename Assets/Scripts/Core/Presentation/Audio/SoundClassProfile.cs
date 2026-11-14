using UnityEngine;
using UnityEngine.Audio;

namespace TeamHJD.Game.Presentation
{
    public enum GameAudioBus { Music, UI, Player, Enemy, Turret, Ambience, Cinematic }

    [CreateAssetMenu(menuName = "The Developer/Audio/Sound class")]
    public sealed class SoundClassProfile : ScriptableObject
    {
        public GameAudioBus Bus;
        public AudioMixerGroup Output;
        public bool WorldSpace = true;
        [Range(0, 256)] public int Priority = 128;
        [Min(1)] public int MaximumVoices = 8;
        [Min(.01f)] public float MinimumDistance = 2, MaximumDistance = 24;
        [Min(0)] public float RetriggerSeconds = .05f;
        [Min(1)] public int MaximumPerEmitter = 2;
    }
}
