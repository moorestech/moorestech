using System;
using Game.Gear.Common;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class GearStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private RotationInfo[] rotationInfos;
        
        private GearStateData _gearStateData;
        
        private void Update()
        {
            if (_gearStateData == null) return;
            
            var rpm = _gearStateData.CurrentRpm;
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
                rotate *= _gearStateData.IsClockwise ? 1 : -1;
                rotationInfo.RotationTransform.Rotate(rotate);
            }
        }
        
        public void OnChangeState(ChangeBlockStateMessagePack blockState)
        {
            _gearStateData = blockState.GetStateDetail<GearStateData>(GearStateData.BlockStateDetailKey);
        }
    }
    
    [Serializable]
    public class RotationInfo
    {
        [SerializeField] private RotationAxis rotationAxis;
        [SerializeField] private Transform rotationTransform;
        public RotationAxis RotationAxis => rotationAxis;
        public Transform RotationTransform => rotationTransform;
    }
    
    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }
}