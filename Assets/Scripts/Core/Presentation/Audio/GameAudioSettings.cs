using System;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    public readonly struct AudioVolumeState
    {
        public readonly float Master, Music, Effects;
        public AudioVolumeState(float master, float music, float effects)
        { Master = GameAudioSettings.Sanitize(master); Music = GameAudioSettings.Sanitize(music); Effects = GameAudioSettings.Sanitize(effects); }
    }

    /// <summary>로컬 출력 설정. 미리듣기와 저장을 분리하고 게임 Save/네트워크 상태를 변경하지 않습니다.</summary>
    public sealed class GameAudioSettings
    {
        public const string MasterKey = "CBC.Audio.Master", MusicKey = "CBC.Audio.Music", EffectsKey = "CBC.Audio.Effects";
        public AudioVolumeState Values { get; private set; }
        public event Action<AudioVolumeState> Changed;
        public GameAudioSettings(bool loadSaved = true) => Values = loadSaved
            ? new AudioVolumeState(PlayerPrefs.GetFloat(MasterKey, 1), PlayerPrefs.GetFloat(MusicKey, 1), PlayerPrefs.GetFloat(EffectsKey, 1))
            : new AudioVolumeState(1, 1, 1);
        public static float Sanitize(float value) => float.IsFinite(value) ? Mathf.Round(Mathf.Clamp01(value) * 100) / 100 : 1;
        public static float Decibels(float value) => value <= 0 ? -80 : Mathf.Clamp(20 * Mathf.Log10(Sanitize(value)), -80, 0);
        public void Set(float master, float music, float effects)
        {
            var next = new AudioVolumeState(master, music, effects);
            if (Values.Master == next.Master && Values.Music == next.Music && Values.Effects == next.Effects) return;
            Values = next; Changed?.Invoke(next);
        }
        public void Restore(AudioVolumeState state) => Set(state.Master, state.Music, state.Effects);
        public void Save()
        {
            PlayerPrefs.SetFloat(MasterKey, Values.Master); PlayerPrefs.SetFloat(MusicKey, Values.Music);
            PlayerPrefs.SetFloat(EffectsKey, Values.Effects); PlayerPrefs.Save();
        }
    }
}
