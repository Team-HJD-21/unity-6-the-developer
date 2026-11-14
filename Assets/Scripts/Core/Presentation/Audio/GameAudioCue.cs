using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    public enum GameSound { PlayerShot, TurretShot, MissileLaunch, Explosion, TurretOn, TurretOff, WolfAttack }

    [CreateAssetMenu(menuName = "The Developer/Audio/Sound cue")]
    public sealed class GameAudioCue : ScriptableObject
    {
        public GameSound Id;
        public SoundClassProfile Class;
        public AudioClip[] Clips = System.Array.Empty<AudioClip>();
        [Range(0, 1)] public float Volume = .5f;
        public Vector2 Pitch = Vector2.one;
        public bool Loop;
        [Min(0)] public float StartSeconds;
        [Min(0)] public float DurationSeconds;
        [Min(1)] public int MaximumInstances = 6;
    }
}
