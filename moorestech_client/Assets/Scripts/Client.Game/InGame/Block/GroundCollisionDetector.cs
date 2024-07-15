using Client.Game.InGame.BlockSystem;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class GroundCollisionDetector : MonoBehaviour
    {
        public bool IsCollision { get; private set; }
        
        private void FixedUpdate()
        {
            // なぜかExitが呼ばれないのでこの方法でリセットを行う
            IsCollision = false;
        }
        
        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.TryGetComponent<GroundGameObject>(out _))
            {
                IsCollision = true;
            }
        }
    }
}