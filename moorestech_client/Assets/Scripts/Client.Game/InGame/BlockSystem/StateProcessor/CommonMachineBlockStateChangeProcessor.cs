using System;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.State;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// TODO マシーン系は自動でつけるみたいなシステムが欲しいな、、、
    /// </summary>
    public class CommonMachineBlockStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public CommonMachineBlockStateDetail CurrentMachineState { get; private set; }
        
        private AudioSource _audioSource;
        private ParticleSystem _machineEffect;
        
        private void Awake()
        {
            _machineSoundClip ??= Resources.Load<AudioClip>("Machine/MachineProcess"); // TODO ここ消したいなぁ
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
        
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            CurrentMachineState = blockState.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var currentState = CurrentMachineState.CurrentStateType;
            var previousState = CurrentMachineState.PreviousStateType;
            
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
            }
        }
        
        #region Resources
        
        private static AudioClip _machineSoundClip;
        private static GameObject _machineEffectPrefab;
        
        #endregion
    }
}