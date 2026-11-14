using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Presentation
{
    public enum GameplayMusicContext { None, Day, Night, Spaceship }

    /// <summary>비동기 clip 데이터 준비만 분리합니다. 게임 시간대나 실제 AudioSource 재생을 대체하지 않습니다.</summary>
    public interface IGameplayMusicData
    {
        AudioDataLoadState GetState(AudioClip clip);
        void Request(AudioClip clip);
    }

    /// <summary>GameAudioManager의 고정 Music voice 둘을 사용합니다. 전환은 마지막 요청 상태만 갱신하며 Coroutine을 남기지 않습니다.</summary>
    public sealed class GameplayMusicPlayer : IDisposable
    {
        private sealed class NativeMusicData : IGameplayMusicData
        {
            public AudioDataLoadState GetState(AudioClip clip) => clip.loadState;
            public void Request(AudioClip clip) => clip.LoadAudioData();
        }
        private sealed class Slot
        {
            public AudioSource Source;
            public GameplayMusicContext Context;
            public string Scope;
            public double StartsAt, EndsAt;
            public float Gain, From, Target;
            public double FadeAt;
            public float FadeSeconds;
        }
        private readonly GameplayMusicLibrary _library;
        private readonly IGameplayMusicData _data;
        private readonly Slot[] _slots = new Slot[2];
        private readonly AudioSource[] _sources;
        private readonly MusicShuffleBag<AudioClip> _day, _night;
        private readonly System.Collections.Generic.HashSet<AudioClip> _failed = new();
        private GameplayMusicContext _context;
        private string _scope = "";
        private int _current = -1, _queued = -1;
        private AudioClip _pendingClip;
        private GameplayMusicContext _pendingContext;
        private string _pendingScope;
        private bool _pendingContinuation;
        private double _pendingRequestedAt;
        private bool _disposed;
        public GameplayMusicContext Context => _context;
        public AudioClip CurrentClip => _current >= 0 ? _slots[_current].Source.clip : null;
        public AudioClip QueuedClip => _queued >= 0 ? _slots[_queued].Source.clip : null;
        public AudioClip PendingClip => _pendingClip;
        public double CurrentEndsAt => _current >= 0 ? _slots[_current].EndsAt : double.NaN;
        public bool IsTransitioning { get; private set; }
        public System.Collections.Generic.IReadOnlyList<AudioSource> Sources => _sources;

        public GameplayMusicPlayer(GameplayMusicLibrary library, Transform parent, AudioSource prefab, System.Random random,
            IGameplayMusicData data = null)
        {
            _library = library != null ? library : throw new ArgumentNullException(nameof(library));
            _data = data ?? new NativeMusicData();
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _day = new MusicShuffleBag<AudioClip>(library.Day, random);
            _night = new MusicShuffleBag<AudioClip>(library.Night, random);
            _sources = new AudioSource[2];
            for (int i = 0; i < _slots.Length; i++)
            {
                var source = prefab != null ? Object.Instantiate(prefab, parent) : new GameObject("Gameplay Music").AddComponent<AudioSource>();
                source.transform.SetParent(parent, false); source.name = "Gameplay Music " + (i == 0 ? "A" : "B");
                source.Stop(); source.clip = null; source.playOnAwake = false; source.loop = false;
                source.pitch = 1; source.volume = 0; source.spatialBlend = 0; source.dopplerLevel = 0; source.priority = 0;
                GameAudioManager.RouteMusic(source);
                _slots[i] = new Slot { Source = source }; _sources[i] = source;
            }
        }

        /// <summary>scope는 현재 Match ID 또는 시작 허브 인스턴스입니다. 화면 메뉴의 열림 여부를 scope로 사용하지 않습니다.</summary>
        public void SetContext(GameplayMusicContext context, string scope, double now)
        {
            if (_disposed) return;
            if (!Enum.IsDefined(typeof(GameplayMusicContext), context)) throw new ArgumentOutOfRangeException(nameof(context));
            scope ??= "";
            if (context != GameplayMusicContext.None && scope.Length == 0) context = GameplayMusicContext.None;
            if (context == GameplayMusicContext.None) scope = "";
            if (_context == context && _scope == scope) return;
            Tick(now); CancelQueued(); _pendingClip = null;
            _context = context; _scope = scope;
            // 취소된 시간대의 아직 시작하지 않은 예약이 나중에 재생되지 않게 합니다.
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Source.clip != null && _slots[i].StartsAt > now && !Matches(_slots[i], context, scope)) Clear(i);
            int next = -1;
            if (context != GameplayMusicContext.None)
            {
                for (int i = 0; i < _slots.Length; i++)
                    if (Matches(_slots[i], context, scope) && (_slots[i].Source.loop || _slots[i].EndsAt > now)) { next = i; break; }
                if (next < 0)
                {
                    AudioClip clip = NextClip(context);
                    if (clip != null)
                    {
                        if (_data.GetState(clip) == AudioDataLoadState.Loaded)
                        {
                            next = FreeSlot();
                            if (next >= 0) Start(next, clip, context, scope, now + Valid(_library.StartDelaySeconds, .1f, 0));
                            else WaitForData(clip, false, now);
                        }
                        else WaitForData(clip, false, now);
                    }
                }
            }
            _current = next; BeginTransition(now);
        }

        public void Tick(double now)
        {
            if (_disposed) return;
            IsTransitioning = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Source.clip == null) continue;
                float value = slot.FadeSeconds > 0 ? Mathf.SmoothStep(0, 1, (float)((now - slot.FadeAt) / slot.FadeSeconds)) : 1;
                slot.Gain = Mathf.Lerp(slot.From, slot.Target, value);
                slot.Source.volume = Volume * slot.Gain;
                if (slot.Target == 0 && value >= 1) Clear(i);
                else if (slot.From != slot.Target && value < 1) IsTransitioning = true;
            }
            ResolvePendingData(now);
            if (_current < 0 || _context == GameplayMusicContext.None) return;
            var active = _slots[_current];
            if (active.Source.clip == null) { _current = -1; return; }
            if (active.Source.loop) return;
            if (now >= active.EndsAt)
            {
                if (_pendingClip != null && _pendingContinuation) return;
                int ended = _current;
                if (_queued >= 0 && _slots[_queued].EndsAt > now)
                {
                    _current = _queued; _queued = -1; Clear(ended);
                    active = _slots[_current];
                }
                else
                {
                    // 긴 프레임/일시적인 로딩 실패로 예약 구간을 놓쳐도 같은 시간대만 이어갑니다.
                    CancelQueued(); Clear(ended); AudioClip clip = NextClip(_context);
                    if (clip == null) { _current = -1; return; }
                    if (_data.GetState(clip) != AudioDataLoadState.Loaded)
                    { _current = -1; WaitForData(clip, false, now); return; }
                    Start(ended, clip, _context, _scope, now + .02);
                    _current = ended; active = _slots[ended]; active.Gain = active.From = active.Target = 1;
                    active.Source.volume = Volume;
                }
            }
            if (IsTransitioning || _queued >= 0 || _pendingClip != null || now < active.EndsAt - Valid(_library.ScheduleAheadSeconds, 1.25f, .1f)) return;
            int free = 1 - _current;
            if (_slots[free].Source.clip != null) return;
            AudioClip nextClip = NextClip(_context); if (nextClip == null) return;
            if (_data.GetState(nextClip) != AudioDataLoadState.Loaded) { WaitForData(nextClip, true, now); return; }
            Start(free, nextClip, _context, _scope, active.EndsAt);
            _slots[free].Gain = _slots[free].From = _slots[free].Target = 1;
            _slots[free].Source.volume = Volume; _queued = free;
        }
        private void BeginTransition(double now)
        {
            float transition = Valid(_library.TransitionSeconds, .8f, .01f);
            if (_current >= 0 && !_slots[_current].Source.loop)
                transition = Mathf.Min(transition, (float)(Duration(_slots[_current].Source.clip) * .4));
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i]; if (slot.Source.clip == null) continue;
                float target = i == _current ? 1 : 0;
                // 로딩이 끝났다고 기존 outgoing fade를 다시 시작하지 않습니다.
                if (slot.Target == target && i != _current) continue;
                slot.From = slot.Gain; slot.Target = target;
                slot.FadeAt = i == _current ? Math.Max(now, slot.StartsAt) : now;
                slot.FadeSeconds = Mathf.Max(.001f, transition); IsTransitioning = true;
            }
        }
        private void WaitForData(AudioClip clip, bool continuation, double now)
        {
            _pendingClip = clip; _pendingContext = _context; _pendingScope = _scope; _pendingContinuation = continuation;
            _pendingRequestedAt = now;
        }
        private void ResolvePendingData(double now)
        {
            if (_pendingClip == null) return;
            if (_pendingContext != _context || _pendingScope != _scope)
            { _pendingClip = null; return; }
            if (_data.GetState(_pendingClip) == AudioDataLoadState.Failed ||
                (_data.GetState(_pendingClip) == AudioDataLoadState.Unloaded && now - _pendingRequestedAt > 2))
            {
                _failed.Add(_pendingClip); Debug.LogWarning("[GameplayMusic] Cannot load track: " + _pendingClip.name);
                _pendingClip = NextClip(_context); _pendingRequestedAt = now; if (_pendingClip == null) return;
            }
            if (_data.GetState(_pendingClip) != AudioDataLoadState.Loaded) return;
            AudioClip clip = _pendingClip;
            if (_pendingContinuation && _current >= 0)
            {
                int free = 1 - _current; if (_slots[free].Source.clip != null) return;
                double start = Math.Max(_slots[_current].EndsAt, now + .02);
                Start(free, clip, _context, _scope, start);
                _slots[free].Gain = _slots[free].From = _slots[free].Target = 1;
                _slots[free].Source.volume = Volume; _queued = free;
            }
            else
            {
                int free = FreeSlot(); if (free < 0) return;
                _current = free;
                Start(_current, clip, _context, _scope, now + Valid(_library.StartDelaySeconds, .1f, 0));
                BeginTransition(now);
            }
            _pendingClip = null;
        }
        private static bool Matches(Slot slot, GameplayMusicContext context, string scope)
            => slot.Source.clip != null && slot.Context == context && slot.Scope == scope;
        private int FreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++) if (_slots[i].Source.clip == null) return i;
            return -1;
        }
        private AudioClip NextClip(GameplayMusicContext context)
        {
            int attempts = (context == GameplayMusicContext.Day ? _day.Count : context == GameplayMusicContext.Night ? _night.Count : 1) * 2;
            for (int i = 0; i < attempts; i++)
            {
                AudioClip clip = context == GameplayMusicContext.Day ? _day.Next() : context == GameplayMusicContext.Night ? _night.Next() : _library.Spaceship;
                if (clip == null || _failed.Contains(clip)) continue;
                if (clip.samples <= 0 || clip.frequency <= 0 || _data.GetState(clip) == AudioDataLoadState.Failed)
                { _failed.Add(clip); Debug.LogWarning("[GameplayMusic] Cannot load track: " + clip.name); continue; }
                // Background 요청은 즉시 Loaded가 아닐 수 있으므로 bool 반환값만으로 실패 처리하지 않습니다.
                if (_data.GetState(clip) == AudioDataLoadState.Unloaded) _data.Request(clip);
                if (_data.GetState(clip) == AudioDataLoadState.Failed)
                { _failed.Add(clip); Debug.LogWarning("[GameplayMusic] Cannot load track: " + clip.name); continue; }
                return clip;
            }
            return null;
        }
        private void Start(int index, AudioClip clip, GameplayMusicContext context, string scope, double startsAt)
        {
            Clear(index); var slot = _slots[index];
            slot.Context = context; slot.Scope = scope; slot.StartsAt = startsAt;
            slot.EndsAt = context == GameplayMusicContext.Spaceship ? double.PositiveInfinity : startsAt + Duration(clip);
            slot.Source.clip = clip; slot.Source.loop = context == GameplayMusicContext.Spaceship;
            slot.Source.timeSamples = 0; slot.Source.PlayScheduled(startsAt);
        }
        private static double Duration(AudioClip clip) => clip.samples / (double)clip.frequency;
        private float Volume => Mathf.Clamp01(Valid(_library.Volume, .55f, 0));
        private static float Valid(float value, float fallback, float minimum) => float.IsFinite(value) ? Mathf.Max(minimum, value) : fallback;
        private void CancelQueued() { if (_queued < 0) return; Clear(_queued); _queued = -1; }
        private void Clear(int index)
        {
            var slot = _slots[index];
            slot.Source.Stop(); slot.Source.clip = null; slot.Context = GameplayMusicContext.None; slot.Scope = "";
            slot.Gain = slot.From = slot.Target = 0; slot.Source.volume = 0;
            slot.FadeAt = 0; slot.FadeSeconds = 0;
        }
        public void StopImmediately()
        {
            for (int i = 0; i < _slots.Length; i++) Clear(i);
            _current = _queued = -1; _pendingClip = null; _context = GameplayMusicContext.None; _scope = ""; IsTransitioning = false;
        }
        public void Dispose() { if (_disposed) return; StopImmediately(); _disposed = true; }
    }
}
