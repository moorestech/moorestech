using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class GearStateChangeProcessor : MonoBehaviour,IBlockStateChangeProcessor
    {
        [SerializeField] private RotationInfo[] rotationInfos;
        
        private GearStateData _gearStateData;
        
        public void OnChangeState(string currentState, string previousState, byte[] currentStateData)
        {
            _gearStateData = MessagePack.MessagePackSerializer.Deserialize<GearStateData>(currentStateData);
        }

        private void Update()
        {
            if (_gearStateData == null) return;
            
            var rpm = _gearStateData.CurrentRpm;
            var rotation = (rpm / 60) * Time.deltaTime * 360;
            foreach (var rotationInfo in rotationInfos)
            {
                var rotate = rotationInfo.RotationAxis switch
                {
                    RotationAxis.X => new Vector3(rotation, 0, 0),
                    RotationAxis.Y => new Vector3(0, rotation, 0),
                    RotationAxis.Z => new Vector3(0, 0, rotation),
                    _ => Vector3.zero
                };
                rotationInfo.RotationTransform.Rotate(rotate);
            }
            
        }
    }

    [Serializable]
    public class RotationInfo
    {
        public RotationAxis RotationAxis => rotationAxis;
        [SerializeField] private RotationAxis rotationAxis;
        public Transform RotationTransform => rotationTransform;
        [SerializeField] private Transform rotationTransform;
    }
    
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }
}