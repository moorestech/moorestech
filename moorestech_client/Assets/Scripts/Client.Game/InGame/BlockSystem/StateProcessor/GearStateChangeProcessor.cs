using System;
using System.Collections.Generic;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class GearStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public IReadOnlyList<RotationInfo> RotationInfos => rotationInfos;
        [SerializeField] private List<RotationInfo> rotationInfos;
        
        private GearStateDetail _currentGearState;
        
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
            var rpm = gearStateDetail.CurrentRpm;
            var rotation = rpm / 60 * Time.deltaTime * 360;
            foreach (var rotationInfo in rotationInfos)
            {
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
        }
        
        #if UNITY_EDITOR
        public GearStateDetail DebugCurrentGearState => _currentGearState;
        #endif
    }
    
    [Serializable]
    public class RotationInfo
    {
        [SerializeField] private RotationAxis rotationAxis;
        [SerializeField] private Transform rotationTransform;
        [SerializeField] private bool isReverse;
        [SerializeField] private float rotationSpeed = 1;
        
        public RotationAxis RotationAxis => rotationAxis;
        public Transform RotationTransform => rotationTransform;
        public bool IsReverse => isReverse;
        public float RotationSpeed => rotationSpeed;
    }
    
    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }
}