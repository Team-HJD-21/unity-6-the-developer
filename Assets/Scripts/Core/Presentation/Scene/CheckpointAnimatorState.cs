// 실제 Animator의 레이어 진행과 연속 파라미터를 값으로 보존합니다.
// 복원은 공격 함수를 호출하지 않으며 Animator 상태만 같은 위치로 옮깁니다.
using System;
using System.IO;
using TeamHJD.Game.Contracts;
using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class CheckpointAnimatorState
    {
        private readonly (int hash, float time)[] _layers;
        private readonly (int hash, AnimatorControllerParameterType type, double value)[] _parameters;
        private CheckpointAnimatorState((int, float)[] layers, (int, AnimatorControllerParameterType, double)[] parameters)
        { _layers = layers; _parameters = parameters; }

        public static CheckpointAnimatorState Capture(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return new(Array.Empty<(int, float)>(), Array.Empty<(int, AnimatorControllerParameterType, double)>());
            var layers = new (int, float)[animator.layerCount];
            for (int i = 0; i < layers.Length; i++)
            {
                if (animator.IsInTransition(i)) throw new InvalidOperationException("Retry the checkpoint after the Animator transition completes.");
                var state = animator.GetCurrentAnimatorStateInfo(i); layers[i] = (state.fullPathHash, state.normalizedTime);
            }
            var parameters = animator.parameters; var values = new (int, AnimatorControllerParameterType, double)[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                values[i] = (p.nameHash, p.type, p.type == AnimatorControllerParameterType.Float ? animator.GetFloat(p.nameHash) :
                    p.type == AnimatorControllerParameterType.Int ? animator.GetInteger(p.nameHash) : p.type == AnimatorControllerParameterType.Bool && animator.GetBool(p.nameHash) ? 1 : 0);
            }
            return new(layers, values);
        }

        public void Apply(Animator animator)
        {
            if (_layers.Length == 0) return;
            if (animator == null || animator.layerCount != _layers.Length) throw new InvalidDataException("Animator layers changed.");
            var authored = animator.parameters;
            foreach (var p in _parameters)
            {
                if (!Array.Exists(authored, a => a.nameHash == p.hash && a.type == p.type)) throw new InvalidDataException("Animator parameters changed.");
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float: animator.SetFloat(p.hash, (float)p.value); break;
                    case AnimatorControllerParameterType.Int: animator.SetInteger(p.hash, (int)p.value); break;
                    case AnimatorControllerParameterType.Bool: animator.SetBool(p.hash, p.value != 0); break;
                    case AnimatorControllerParameterType.Trigger: animator.ResetTrigger(p.hash); break;
                }
            }
            for (int i = 0; i < _layers.Length; i++)
            {
                if (!animator.HasState(i, _layers[i].hash)) throw new InvalidDataException("Animator state changed.");
                animator.Play(_layers[i].hash, i, _layers[i].time);
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(_layers.Length); foreach (var layer in _layers) { writer.Write(layer.hash); writer.Write(layer.time); }
            writer.Write(_parameters.Length);
            foreach (var p in _parameters) { writer.Write(p.hash); writer.Write((int)p.type); writer.Write(p.value); }
        }

        public static CheckpointAnimatorState Read(BinaryReader reader, ICheckpointPayloadCodec codec)
        {
            var layers = new (int, float)[codec.ReadCount(reader, 16)];
            for (int i = 0; i < layers.Length; i++)
            {
                int hash = reader.ReadInt32(); float time = reader.ReadSingle();
                if (!float.IsFinite(time) || time < 0) throw new InvalidDataException("Invalid Animator progress.");
                layers[i] = (hash, time);
            }
            var parameters = new (int, AnimatorControllerParameterType, double)[codec.ReadCount(reader, 256)];
            for (int i = 0; i < parameters.Length; i++)
            {
                int hash = reader.ReadInt32(); var type = (AnimatorControllerParameterType)reader.ReadInt32(); double value = reader.ReadDouble();
                if (!Enum.IsDefined(typeof(AnimatorControllerParameterType), type) || !double.IsFinite(value)) throw new InvalidDataException("Invalid Animator parameter.");
                parameters[i] = (hash, type, value);
            }
            return new(layers, parameters);
        }
    }
}
