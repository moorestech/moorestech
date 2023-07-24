using System;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.State;
using MainGame.ModLoader.Glb;
using MessagePack;
using Newtonsoft.Json;
using UnityEngine;

namespace MainGame.UnityView.Block.StateChange
{
    public class MachineBlockStateChangeProcessor : MonoBehaviour,IBlockStateChangeProcessor
    {
        #region Resources

        private static readonly AudioClip machineSoundClip = Resources.Load<AudioClip>("Machine/MachineProcess");
        private static readonly GameObject machineEffectPrefab = Resources.Load<GameObject>("Machine/MachineProcessEffect");
        #endregion
        
        private AudioSource _audioSource;
        private ParticleSystem _machineEffect;
        private float _processingRate;

        private void Start()
        {
            
            
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip = machineSoundClip;
            _audioSource.Stop();

            var effectObject = Instantiate(machineEffectPrefab, transform);
            effectObject.transform.localPosition = Vector3.zero;
            _machineEffect = effectObject.GetComponent<ParticleSystem>();
        }


        public void OnChangeState(string currentState, string previousState, string currentStateData)
        {
            var data = JsonConvert.DeserializeObject<CommonMachineBlockStateChangeData>(currentStateData);
            _processingRate = data.ProcessingRate;
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
    }
}