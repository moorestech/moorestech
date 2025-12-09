using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [RequireComponent(typeof(GearStateChangeProcessorSimulator))]
    public class GearStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public IReadOnlyList<RotationInfo> RotationInfos => rotationInfos;
        [SerializeField] private List<RotationInfo> rotationInfos;
        
        private GearStateDetail _currentGearState;
        
        public void Initialize(BlockGameObject blockGameObject) { }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }
        
        private void Update()
        {
            if (_currentGearState == null) return;
            
            Rotate(_currentGearState);
        }
        
        public void Rotate(GearStateDetail gearStateDetail)
        {
            foreach (var rotationInfo in rotationInfos)
            {
                switch (rotationInfo.RotationMode)
                {
                    case RotationMode.TransformRotate:
                        TransformRotate(rotationInfo);
                        break;
                    case RotationMode.Animator:
                        AnimationRotate(rotationInfo);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            #region Internal
            
            void TransformRotate(RotationInfo rotationInfo)
            {
                var rpm = gearStateDetail.CurrentRpm;
                var rotation = rpm / 60 * Time.deltaTime * 360;
                
                var rotate = rotationInfo.RotationAxis switch
                {
                    RotationAxis.X => new Vector3(rotation, 0, 0),
                    RotationAxis.Y => new Vector3(0, rotation, 0),
                    RotationAxis.Z => new Vector3(0, 0, rotation),
                    _ => Vector3.zero,
                };
                rotate *= rotationInfo.IsReverse ? -1 : 1;
                rotate *= rotationInfo.RotationSpeed;
                rotate *= gearStateDetail.IsClockwise ? 1 : -1;
                
                rotationInfo.RotationTransform.Rotate(rotate);
            }
            
            void AnimationRotate(RotationInfo rotationInfo)
            {
                var rpmRate = gearStateDetail.CurrentRpm / 60f;
                var speed = rotationInfo.Rpm60Speed * (rotationInfo.IsReverse ? -1 : 1) * (gearStateDetail.IsClockwise ? 1 : -1) * rpmRate;
                rotationInfo.Animator.speed = speed;
            }
            
  #endregion
        }
        
        #if UNITY_EDITOR
        public GearStateDetail DebugCurrentGearState => _currentGearState;
        #endif
    }
    
    [Serializable]
    public class RotationInfo
    {
        [SerializeField] private RotationMode rotationMode;
        
        [Header("TransformRotate Only")]
        [SerializeField] private RotationAxis rotationAxis;
        [SerializeField] private Transform rotationTransform;
        
        [SerializeField] private bool isReverse;
        [SerializeField] private float rotationSpeed = 1;
        
        [Header("Animator Only")]
        [SerializeField] private Animator animator;
        [SerializeField] private float rpm60Speed = 1;
        
        public RotationMode RotationMode => rotationMode;
        
        public RotationAxis RotationAxis => rotationAxis;
        public Transform RotationTransform => rotationTransform;
        
        public Animator Animator => animator;
        public float Rpm60Speed => rpm60Speed;
        
        public bool IsReverse => isReverse;
        public float RotationSpeed => rotationSpeed;
    }
    
    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }
    
    public enum RotationMode
    {
        TransformRotate,
        Animator,
    }
}