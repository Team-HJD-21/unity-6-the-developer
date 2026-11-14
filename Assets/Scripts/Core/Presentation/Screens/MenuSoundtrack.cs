using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    /// <summary>Main의 도입곡 한 번과 대기곡 반복을 DSP 시각으로 연결합니다.</summary>
    public sealed class MenuSoundtrack : MonoBehaviour
    {
        public AudioSource Intro, Ambient;
        [Range(0, 1)] public float Volume = .55f;
        private GameFlowController _flow;
        private bool _active, _suspended, _introPending;
        private int _introSample, _ambientSample;
        private double _introEndsAt;
        private float _gain = 1;

        public void Bind(GameFlowController flow)
        {
            GameAudioManager.RouteMusic(Intro); GameAudioManager.RouteMusic(Ambient);
            Unbind(); _flow = flow;
            if (_flow == null) return;
            _flow.Changed += Render; Render();
        }
        private void Render()
        {
            if (_flow.Phase != GameFlowPhase.MainMenu) { Stop(); return; }
            if (_active) return;
            GameAudioManager.Current?.StopGameplayMusic();
            _active = true; _introPending = Intro != null && Intro.clip != null;
            _introSample = _ambientSample = 0; _gain = 1; Schedule();
        }
        private void Schedule()
        {
            double start = AudioSettings.dspTime + .15;
            SetGain(_gain);
            if (_introPending && Intro != null && Intro.clip != null)
            {
                Intro.loop = false; Intro.pitch = 1; Intro.timeSamples = _introSample;
                _introEndsAt = start + (Intro.clip.samples - _introSample) / (double)Intro.clip.frequency;
                Intro.PlayScheduled(start);
                if (Ambient != null && Ambient.clip != null)
                {
                    Ambient.loop = true; Ambient.pitch = 1; Ambient.timeSamples = 0;
                    Ambient.PlayScheduled(_introEndsAt);
                }
            }
            else if (Ambient != null && Ambient.clip != null)
            {
                Ambient.loop = true; Ambient.pitch = 1; Ambient.timeSamples = _ambientSample;
                Ambient.PlayScheduled(start);
            }
        }
        public void Suspend()
        {
            if (!_active || _suspended) return;
            _introPending = Intro != null && Intro.clip != null && AudioSettings.dspTime < _introEndsAt;
            _introSample = _introPending ? Intro.timeSamples : 0;
            _ambientSample = !_introPending && Ambient != null ? Ambient.timeSamples : 0;
            if (Intro != null) Intro.Stop();
            if (Ambient != null) Ambient.Stop();
            _suspended = true;
        }
        public void Resume()
        {
            if (!_active || !_suspended) return;
            _suspended = false; Schedule();
        }
        public void SetGain(float gain)
        {
            _gain = Mathf.Clamp01(gain);
            if (Intro != null) Intro.volume = Volume * _gain;
            if (Ambient != null) Ambient.volume = Volume * _gain;
        }
        private void Stop()
        {
            if (Intro != null) Intro.Stop();
            if (Ambient != null) Ambient.Stop();
            _active = _suspended = false;
        }
        public void Unbind()
        {
            if (_flow != null) _flow.Changed -= Render;
            _flow = null; Stop();
        }
        private void OnDisable() => Unbind();
    }
}
