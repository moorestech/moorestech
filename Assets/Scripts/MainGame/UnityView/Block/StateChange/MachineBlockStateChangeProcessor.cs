using System;
using Core.Block.Blocks.Machine;
using MainGame.ModLoader.Glb;
using UnityEngine;

namespace MainGame.UnityView.Block.StateChange
{
    public class MachineBlockStateChangeProcessor : MonoBehaviour,IBlockStateChangeProcessor
    {
        #region Resources
        private static AudioClip _machineSound;
        #endregion
        
        private AudioSource _audioSource;

        private void Start()
        {
            _machineSound ??= Resources.Load<AudioClip>("Machine/MachineProcess");
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip = _machineSound;
            _audioSource.Stop();
        }


        public void SetState(string currentState, string previousState, byte[] currentStateData)
        {
            switch (currentState)
            {
                case VanillaMachineBlockStateConst.ProcessingState:
                    _audioSource.Play();
                    break;
                case VanillaMachineBlockStateConst.IdleState:
                    _audioSource.Stop();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentState), currentState, null);
            }
        }
    }
}