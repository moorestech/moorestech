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
        
        public GearStateData CurrentGearState { get; private set; }
        
        public void OnChangeState(ChangeBlockStateMessagePack blockState)
        {
            CurrentGearState = blockState.GetStateDetail<GearStateData>(GearStateData.BlockStateDetailKey);
        }
        
        private void Update()
        {
            if (CurrentGearState == null) return;
            
            Rotate(CurrentGearState);
        }
        
        public void Rotate(GearStateData gearStateData)
        {
            var rpm = gearStateData.CurrentRpm;
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
                rotate *= gearStateData.IsClockwise ? 1 : -1;
                
                rotationInfo.RotationTransform.Rotate(rotate);
            }
        }
    }
    
    [Serializable]
    public class RotationInfo
    {
        [SerializeField] private RotationAxis rotationAxis;
        [SerializeField] private Transform rotationTransform;
        [SerializeField] private bool isReverse;
        
        public RotationAxis RotationAxis => rotationAxis;
        public Transform RotationTransform => rotationTransform;
        public bool IsReverse => isReverse;
    }
    
    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }
}