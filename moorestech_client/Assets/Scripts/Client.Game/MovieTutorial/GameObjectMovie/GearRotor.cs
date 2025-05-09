using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;

namespace Client.Game.MovieTutorial.GameObjectMovie
{
    public class GearRotor : MonoBehaviour
    {
        [SerializeField] private bool isClockwise;
        [SerializeField] private float rpm;
        
        [SerializeField] private List<RotationInfo> rotationInfos;
        
        private void Update()
        {
            Rotate();
        }
        
        
        private void Rotate()
        {
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
                rotate *= isClockwise ? 1 : -1;
                
                rotationInfo.RotationTransform.Rotate(rotate);
            }
        }
    }
}