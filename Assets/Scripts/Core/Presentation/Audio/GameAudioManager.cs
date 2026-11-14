using System;
using System.Collections.Generic;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Presentation
{
    /// <summary>표준 AudioSource의 풀·분류·동시 재생 수·수명만 소유합니다. 공간 처리는 Unity가 수행합니다.</summary>
    public sealed partial class GameAudioManager : MonoBehaviour
    {
        public readonly struct VoiceHandle
        {
            internal readonly GameAudioManager Manager;
            internal readonly int Slot, Generation;
            internal VoiceHandle(GameAudioManager manager, int slot, int generation)
            { Manager = manager; Slot = slot; Generation = generation; }
            public bool IsActive => Manager != null && Manager.IsActive(this);
            public void Stop() { if (Manager != null) Manager.Stop(this); }
        }
        private sealed class Voice
        {
            public AudioSource Source;
            public GameAudioCue Cue;
            public Component Owner;
            public int OwnerId, Scene, Generation;
            public bool Paused, Releasing;
            public double EndsAt;
            public float Gain;
        }
        private readonly struct TriggerKey : IEquatable<TriggerKey>
        {
            private readonly int _cue, _owner;
            public TriggerKey(GameAudioCue cue, int owner) { _cue = cue.GetInstanceID(); _owner = owner; }
            public bool Equals(TriggerKey other) => _cue == other._cue && _owner == other._owner;
            public override bool Equals(object obj) => obj is TriggerKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_cue, _owner);
        }
        private readonly Dictionary<GameSound, GameAudioCue> _cues = new();
        private readonly Dictionary<TriggerKey, double> _retrigger = new();
        private readonly List<TriggerKey> _expired = new();
        private readonly HashSet<AudioSource> _musicSources = new(), _cinematicSources = new();
        private Voice[] _voices = Array.Empty<Voice>();
        private Transform _listener;
        private GameAudioSettings _settings;
        private System.Random _random;
        private double _lastDsp, _nextPrune;
        private bool _shutdown;
        public GameAudioConfiguration Configuration { get; private set; }
        public static GameAudioManager Current { get; private set; }
        public int ActiveVoices { get; private set; }
        public int DroppedRequests { get; private set; }
        public int PoolCapacity => _voices.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        public void Initialize(GameAudioConfiguration configuration, GameAudioSettings settings)
        {
            if (Configuration != null) throw new InvalidOperationException("Audio is already initialized.");
            if (Current != null && Current != this) throw new InvalidOperationException("One audio manager is allowed per app.");
            Configuration = configuration != null ? configuration : throw new ArgumentNullException(nameof(configuration));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _random = new System.Random(Environment.TickCount ^ GetInstanceID());
            foreach (var cue in configuration.Cues) if (cue != null) _cues[cue.Id] = cue;
            _voices = new Voice[Mathf.Clamp(configuration.PoolSize, 4, 30)];
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource source = configuration.VoicePrefab != null ? Instantiate(configuration.VoicePrefab, transform)
                    : new GameObject("Voice").AddComponent<AudioSource>();
                source.transform.SetParent(transform, false); source.name = "Voice " + i;
                source.playOnAwake = false; source.Stop(); source.dopplerLevel = 0;
                _voices[i] = new Voice { Source = source };
            }
            Current = this; _lastDsp = AudioSettings.dspTime;
            _settings.Changed += ApplyVolumes;
            CameraManager.OutputChanged += OutputChanged;
            SceneManager.sceneUnloaded += SceneUnloaded;
            SceneManager.sceneLoaded += SceneLoaded;
            FindListener(); InitializeMusic();
        }
        // Mixer exposed parameters must be applied after Awake/OnEnable, when the native mixer is ready.
        private void Start() { if (_settings != null) ApplyVolumes(_settings.Values); }
        private void ApplyVolumes(AudioVolumeState state)
        {
            if (Configuration.Mixer == null) return;
            Configuration.Mixer.SetFloat(Configuration.MasterParameter, GameAudioSettings.Decibels(state.Master));
            Configuration.Mixer.SetFloat(Configuration.MusicParameter, GameAudioSettings.Decibels(state.Music));
            Configuration.Mixer.SetFloat(Configuration.EffectsParameter, GameAudioSettings.Decibels(state.Effects));
            foreach (var voice in _voices) voice.Source.mute = state.Master <= 0 || state.Effects <= 0;
            foreach (var source in _musicSources) if (source != null) source.mute = state.Master <= 0 || state.Music <= 0;
            foreach (var source in _cinematicSources) if (source != null) source.mute = state.Master <= 0 || state.Effects <= 0;
        }
        public static VoiceHandle Play(GameSound sound, Component emitter)
        {
            if (Current == null || emitter == null || !Current._cues.TryGetValue(sound, out var cue)) return default;
            return Current.TryPlay(cue, emitter, emitter.transform.position, out var handle) ? handle : default;
        }
        public static VoiceHandle Play(GameAudioCue cue, Component emitter)
            => Current != null && emitter != null && Current.TryPlay(cue, emitter, emitter.transform.position, out var handle) ? handle : default;

        public bool TryPlay(GameAudioCue cue, Component owner, Vector3 position, out VoiceHandle handle)
        {
            handle = default;
            if (!isActiveAndEnabled || Configuration == null || cue == null || cue.Class == null || cue.Clips.Length == 0 || owner == null) return false;
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return false;
            var profile = cue.Class;
            if (cue.Loop && (cue.StartSeconds > 0 || cue.DurationSeconds > 0)) return Drop();
            if (profile.WorldSpace && (!FieldSimulationTime.CanAdvance(owner) || _listener == null ||
                Vector2.Distance(position, _listener.position) >= profile.MaximumDistance)) return Drop();
            double now = AudioSettings.dspTime;
            int ownerId = owner.GetInstanceID(); var key = new TriggerKey(cue, ownerId);
            if (_retrigger.TryGetValue(key, out double until) && now < until) return Drop();
            int instances = 0, inClass = 0, sameOwner = 0, free = -1, replace = -1, oldestForOwner = -1;
            double earliestEnd = double.PositiveInfinity;
            float worst = float.NegativeInfinity;
            int first = profile.Bus == GameAudioBus.UI ? 0 : Mathf.Clamp(Configuration.ReservedUIVoices, 1, _voices.Length - 1);
            for (int i = 0; i < _voices.Length; i++)
            {
                var voice = _voices[i];
                if (voice.Cue == null) { if (i >= first && free < 0) free = i; continue; }
                if (voice.Cue == cue) instances++;
                if (voice.Cue.Class == profile) inClass++;
                if (voice.Cue == cue && voice.OwnerId == ownerId)
                { sameOwner++; if (voice.EndsAt < earliestEnd) { earliestEnd = voice.EndsAt; oldestForOwner = i; } }
                if (i < first || voice.Cue.Class.Priority < profile.Priority || voice.Cue.Loop) continue;
                float score = voice.Cue.Class.Priority * 1000 + (_listener != null ? Vector2.Distance(voice.Source.transform.position, _listener.position) : 0);
                if (score > worst) { replace = i; worst = score; }
            }
            bool cueFull = instances >= Mathf.Max(1, cue.MaximumInstances), classFull = inClass >= Mathf.Max(1, profile.MaximumVoices);
            if (sameOwner >= Mathf.Max(1, profile.MaximumPerEmitter))
            { if (cue.Loop) return Drop(); free = oldestForOwner; }
            else if (cueFull || classFull)
            {
                free = -1; worst = float.NegativeInfinity;
                for (int i = first; i < _voices.Length; i++)
                {
                    var candidate = _voices[i];
                    if (candidate.Cue == null || candidate.Cue.Loop || candidate.Cue.Class != profile || (cueFull && candidate.Cue != cue)) continue;
                    float score = candidate.Cue.Class.Priority * 1000 + (_listener != null ? Vector2.Distance(candidate.Source.transform.position, _listener.position) : 0);
                    if (score > worst) { replace = i; worst = score; }
                }
                if (worst < profile.Priority * 1000 + (_listener != null ? Vector2.Distance(position, _listener.position) : 0)) return Drop();
            }
            else if (free < 0 && worst < profile.Priority * 1000 + (_listener != null ? Vector2.Distance(position, _listener.position) : 0)) return Drop();
            int slot = free >= 0 ? free : replace;
            if (slot < 0) return Drop();
            var clip = cue.Clips[_random.Next(cue.Clips.Length)];
            if (clip == null) return Drop();
            var v = _voices[slot]; Clear(v);
            v.Cue = cue; v.Owner = owner; v.OwnerId = ownerId; v.Scene = owner.gameObject.scene.handle;
            v.Generation++; v.Gain = 0;
            var source = v.Source; source.clip = clip; source.loop = cue.Loop; source.volume = 0;
            source.mute = _settings.Values.Master <= 0 || _settings.Values.Effects <= 0;
            source.outputAudioMixerGroup = profile.Output; source.priority = Mathf.Clamp(profile.Priority, 0, 256);
            source.spatialBlend = profile.WorldSpace ? 1 : 0; source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(.01f, profile.MinimumDistance); source.maxDistance = Mathf.Max(source.minDistance + .01f, profile.MaximumDistance);
            source.pitch = Mathf.Clamp(Mathf.Lerp(cue.Pitch.x, cue.Pitch.y, (float)_random.NextDouble()), .25f, 3);
            source.transform.position = ProjectToListeningPlane(position, profile.WorldSpace);
            float start = Mathf.Clamp(cue.StartSeconds, 0, Mathf.Max(0, clip.length - .001f));
            source.timeSamples = Mathf.Min(clip.samples - 1, Mathf.RoundToInt(start * clip.frequency));
            float duration = cue.DurationSeconds > 0 ? Mathf.Min(cue.DurationSeconds, clip.length - start) : clip.length - start;
            v.EndsAt = now + duration / source.pitch;
            _retrigger[key] = now + Mathf.Max(0, profile.RetriggerSeconds); source.Play();
            ActiveVoices++; handle = new VoiceHandle(this, slot, v.Generation); return true;
        }
        private bool Drop() { DroppedRequests++; return false; }
        private Vector3 ProjectToListeningPlane(Vector3 position, bool world)
        { if (world && _listener != null) position.z = _listener.position.z; return position; }
        private void LateUpdate()
        {
            if (Configuration == null) return;
            double now = AudioSettings.dspTime; TickMusic(now); float elapsed = (float)Math.Max(0, now - _lastDsp); _lastDsp = now;
            foreach (var v in _voices)
            {
                if (v.Cue == null) continue;
                bool pause = v.Cue.Class.WorldSpace && v.Owner != null && !FieldSimulationTime.CanAdvance(v.Owner);
                if (pause != v.Paused) { if (pause) v.Source.Pause(); else v.Source.UnPause(); v.Paused = pause; }
                if (pause) v.EndsAt += elapsed;
                if (v.Cue.Loop && (v.Owner == null || !v.Owner.gameObject.activeInHierarchy)) v.Releasing = true;
                if (!v.Cue.Loop && !pause && now >= v.EndsAt) { Clear(v); continue; }
                if (!v.Cue.Loop && !pause && now >= v.EndsAt - .04) v.Releasing = true;
                v.Gain = Mathf.MoveTowards(v.Gain, v.Releasing ? 0 : 1, elapsed / (v.Releasing ? .04f : .008f));
                v.Source.volume = Mathf.Clamp01(v.Cue.Volume) * v.Gain;
                if (v.Releasing && v.Gain <= 0) { Clear(v); continue; }
                if (v.Cue.Loop && v.Owner != null) v.Source.transform.position = ProjectToListeningPlane(v.Owner.transform.position, v.Cue.Class.WorldSpace);
            }
            if (now >= _nextPrune)
            {
                _nextPrune = now + 2; _expired.Clear();
                foreach (var pair in _retrigger) if (pair.Value <= now) _expired.Add(pair.Key);
                foreach (var key in _expired) _retrigger.Remove(key);
                _musicSources.RemoveWhere(source => source == null); _cinematicSources.RemoveWhere(source => source == null);
            }
        }
        private bool IsActive(VoiceHandle handle) => handle.Manager == this && handle.Slot >= 0 && handle.Slot < _voices.Length &&
            _voices[handle.Slot].Cue != null && _voices[handle.Slot].Generation == handle.Generation;
        private void Stop(VoiceHandle handle) { if (IsActive(handle)) _voices[handle.Slot].Releasing = true; }
        private void Clear(Voice voice)
        {
            if (voice.Cue != null) ActiveVoices--;
            voice.Source.Stop(); voice.Source.clip = null; voice.Cue = null; voice.Owner = null;
            voice.Paused = voice.Releasing = false; voice.Gain = 0;
        }
        private void SceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        { foreach (var voice in _voices) if (voice.Cue != null && voice.Scene == scene.handle) Clear(voice); FindListener(); }
        private void SceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode) => FindListener();
        private void OutputChanged(Camera camera) => _listener = camera != null ? camera.transform : null;
        private void FindListener()
        {
            _listener = CameraManager.Current != null ? CameraManager.Current.transform : null;
            if (_listener != null) return;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) { _listener = listener.transform; break; }
        }
        public static void RouteMusic(AudioSource source)
        {
            if (source == null || Current == null) return;
            source.outputAudioMixerGroup = Current.Configuration.MusicOutput; source.priority = 0;
            source.spatialBlend = 0; source.dopplerLevel = 0;
            Current._musicSources.Add(source); source.mute = Current._settings.Values.Master <= 0 || Current._settings.Values.Music <= 0;
        }
        public static void RouteCinematic(AudioSource source)
        {
            if (source == null || Current == null) return;
            source.outputAudioMixerGroup = Current.Configuration.CinematicOutput; Current._cinematicSources.Add(source);
            source.mute = Current._settings.Values.Master <= 0 || Current._settings.Values.Effects <= 0;
        }
        public void Shutdown()
        {
            if (_shutdown) return; _shutdown = true; ShutdownMusic();
            if (_settings != null) _settings.Changed -= ApplyVolumes;
            CameraManager.OutputChanged -= OutputChanged; SceneManager.sceneUnloaded -= SceneUnloaded; SceneManager.sceneLoaded -= SceneLoaded;
            foreach (var voice in _voices) if (voice.Source != null) Clear(voice);
            if (Current == this) Current = null;
            enabled = false;
        }
        private void OnDestroy() => Shutdown();
        private void OnDisable()
        { StopGameplayMusic(); foreach (var voice in _voices) if (voice.Source != null) Clear(voice); }
    }
}
