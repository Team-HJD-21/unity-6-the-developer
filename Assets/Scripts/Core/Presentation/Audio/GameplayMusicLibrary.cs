using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    /// <summary>게임 시간대별 음악 원본입니다. 재생 길이가 Match의 낮밤 길이를 결정하지 않습니다.</summary>
    [CreateAssetMenu(menuName = "The Developer/Audio/Gameplay music library")]
    public sealed class GameplayMusicLibrary : ScriptableObject
    {
        public AudioClip[] Day = System.Array.Empty<AudioClip>();
        public AudioClip[] Night = System.Array.Empty<AudioClip>();
        public AudioClip Spaceship;
        [Range(0, 1)] public float Volume = .55f;
        [Min(.01f)] public float TransitionSeconds = .8f;
        [Min(.1f)] public float ScheduleAheadSeconds = 1.25f;
        [Min(0)] public float StartDelaySeconds = .1f;
    }
}
