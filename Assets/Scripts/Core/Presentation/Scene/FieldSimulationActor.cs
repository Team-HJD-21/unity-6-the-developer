// 필드 개체에 주입된 Match 시계와 물리·애니메이션 정지를 연결합니다.
// Behaviour/NetworkObject를 끄지 않아 네트워크 수명과 별도의 상태효과 활성 상태를 보존합니다.
using System;
using System.Threading;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.AI;

namespace TeamHJD.Game.Presentation.Scene
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class FieldSimulationActor : MonoBehaviour
    {
        private IMatchTime _time;
        private CancellationToken _gameplay;
        private bool _destroyOnCompletion;
        private double _lastSeconds;
        private readonly List<(Rigidbody2D body, bool simulated, Vector2 velocity, float angular, bool sleeping)> _bodies = new();
        private readonly List<(Animator animator, float speed)> _animators = new();
        private bool _held;
        private readonly List<ParticleSystem> _particles = new();
        private readonly List<(NavMeshAgent agent, bool stopped)> _agents = new();
        public IMatchTime TimeSource => _time;
        public CancellationToken GameplayCancellation => _gameplay;
        public bool CanAdvance => !_gameplay.IsCancellationRequested && (_time == null || (_time.IsAvailable && !_time.Snapshot.IsPaused && _time.Snapshot.Rate > 0));
        public double Seconds
        {
            get
            {
                if (_time == null) return Time.timeAsDouble;
                if (_time.IsAvailable) _lastSeconds = _time.ElapsedSeconds;
                return _lastSeconds;
            }
        }

        /// <summary>생성자가 적·터렛·탄환을 만든 직후 같은 Match의 시계를 전달합니다.</summary>
        /// <remarks>전역 Scene 검색을 하지 않습니다. 자식 투사체에는 생성 주체가 TimeSource를 명시적으로 전달합니다.</remarks>
        public static FieldSimulationActor Attach(GameObject actor, IMatchTime time, CancellationToken gameplay = default, bool destroyOnCompletion = false)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (time == null) throw new ArgumentNullException(nameof(time));
            var binding = actor.GetComponent<FieldSimulationActor>() ?? actor.AddComponent<FieldSimulationActor>();
            if (binding._time != null && !ReferenceEquals(binding._time, time))
                throw new InvalidOperationException("A field actor belongs to a different Match clock.");
            binding._time = time; binding._gameplay = gameplay; binding._destroyOnCompletion = destroyOnCompletion;
            binding._lastSeconds = time.IsAvailable ? time.ElapsedSeconds : 0;
            binding.Synchronize(); return binding;
        }
        private void Update()
        {
            if (_gameplay.IsCancellationRequested && _destroyOnCompletion) { Destroy(gameObject); return; }
            Synchronize();
        }
        private void FixedUpdate() => Synchronize();
        /// <summary>정지 전환 직후 즉시 반영하거나 프레임 시작에 현재 시계 상태를 반영합니다.</summary>
        public void Synchronize()
        {
            bool hold = !CanAdvance;
            if (hold == _held)
            {
                if (hold)
                {
                    // NGO 초기화가 정지 이후 실행돼도 물리/Animator를 다시 켜지 못하게 합니다.
                    foreach (var item in _bodies) if (item.body != null) item.body.simulated = false;
                    foreach (var item in _animators) if (item.animator != null) item.animator.speed = 0;
                    HoldNavigation();
                }
                return;
            }
            _held = hold;
            if (hold)
            {
                foreach (var body in GetComponentsInChildren<Rigidbody2D>(true))
                {
                    _bodies.Add((body, body.simulated, body.linearVelocity, body.angularVelocity, body.IsSleeping()));
                    body.simulated = false;
                }
                foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
                    if (particles.isPlaying) { _particles.Add(particles); particles.Pause(false); }
                foreach (var animator in GetComponentsInChildren<Animator>(true))
                { _animators.Add((animator, animator.speed)); animator.speed = 0; }
                foreach (var agent in GetComponentsInChildren<NavMeshAgent>(true))
                {
                    if (!agent.enabled) continue;
                    _agents.Add((agent, agent.isOnNavMesh && agent.isStopped));
                }
                HoldNavigation();
            }
            else ReleaseHold();
        }
        private void ReleaseHold()
        {
            foreach (var item in _agents)
                if (item.agent != null && item.agent.enabled && item.agent.isOnNavMesh)
                    item.agent.isStopped = item.stopped;
            _agents.Clear();
            foreach (var item in _bodies)
                if (item.body != null)
                {
                    item.body.simulated = item.simulated; item.body.linearVelocity = item.velocity; item.body.angularVelocity = item.angular;
                    if (item.sleeping) item.body.Sleep(); else item.body.WakeUp();
                }
            foreach (var item in _animators) if (item.animator != null) item.animator.speed = item.speed;
            foreach (var particles in _particles) if (particles != null) particles.Play(false);
            _particles.Clear(); _bodies.Clear(); _animators.Clear(); _held = false;
        }

        private void HoldNavigation()
        {
            foreach (var item in _agents)
                if (item.agent != null && item.agent.enabled && item.agent.isOnNavMesh)
                    item.agent.isStopped = true;
        }

        /// <summary>Feature writer가 정지 중에도 실제 복귀 예정 속도와 simulated 상태를 캡처합니다.</summary>
        public CheckpointRigidbody2DState CaptureBody(Rigidbody2D body)
        {
            RequireOwnedBody(body);
            Synchronize();
            foreach (var item in _bodies)
                if (item.body == body)
                    return new CheckpointRigidbody2DState(body.position, body.rotation,
                        item.velocity, item.angular, item.simulated, item.sleeping);
            return CheckpointRigidbody2DState.Capture(body);
        }

        /// <summary>정지된 캡처/복원 단계가 적용합니다. 재개 시 과거 보관 속도로 덮어쓰지 않도록 복귀 값도 갱신합니다.</summary>
        public void RestoreBody(Rigidbody2D body, CheckpointRigidbody2DState state)
        {
            RequireOwnedBody(body);
            Synchronize();
            if (!_held) { state.Apply(body); return; }
            int found = _bodies.FindIndex(item => item.body == body);
            var entry = (body, state.Simulated, state.Velocity, state.AngularVelocity, state.Sleeping);
            if (found >= 0) _bodies[found] = entry;
            else _bodies.Add(entry);
            state.ApplyPose(body);
            body.linearVelocity = state.Velocity; body.angularVelocity = state.AngularVelocity;
            body.simulated = false;
        }

        private void RequireOwnedBody(Rigidbody2D body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (!body.transform.IsChildOf(transform)) throw new InvalidOperationException("The body belongs to a different field actor.");
        }
        /// <summary>Scene/Feature 소유자가 lease 종료 때 호출합니다. 다른 Match의 바인딩을 해제하지 않습니다.</summary>
        public void Unbind(IMatchTime time)
        { if (!ReferenceEquals(_time, time)) return; _time = null; _gameplay = default; _destroyOnCompletion = false; ReleaseHold(); }
        private void OnDestroy() => ReleaseHold();
    }
}
