using System;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.State;
using MessagePack;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class MachineBlockStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        private AudioSource _audioSource;
        private ParticleSystem _machineEffect;
        private float _processingRate;

        private void Awake()
        {
            _machineSoundClip ??= Resources.Load<AudioClip>("Machine/MachineProcess");
            _machineEffectPrefab ??= Resources.Load<GameObject>("Machine/MachineProcessEffect");
        }

        private void Start()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip = _machineSoundClip;
            _audioSource.Stop();

            var effectObject = Instantiate(_machineEffectPrefab, transform);
            effectObject.transform.localPosition = Vector3.zero;
            _machineEffect = effectObject.GetComponent<ParticleSystem>();
            _machineEffect.Stop();
        }


        public void OnChangeState(string currentState, string previousState, byte[] currentStateData)
        {
            var data = MessagePackSerializer.Deserialize<CommonMachineBlockStateChangeData>(currentStateData);
            _processingRate = data.processingRate;
            switch (currentState)
            {
                case VanillaMachineBlockStateConst.ProcessingState:
                    if (previousState == VanillaMachineBlockStateConst.IdleState)
                    {
                        _machineEffect.Play();
                        _audioSource.Play();
                    }

                    break;
                case VanillaMachineBlockStateConst.IdleState:
                    _audioSource.Stop();
                    _machineEffect.Stop();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentState), currentState, null);
            }
        }

        #region Resources

        private static AudioClip _machineSoundClip;
        private static GameObject _machineEffectPrefab;

        #endregion
    }
}