using Client.Game.InGame.BlockSystem;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class GroundCollisionDetector : MonoBehaviour
    {
        public bool IsCollision { get; private set; }
        
        
        //TODO グラウンド検知
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.TryGetComponent<GroundGameObject>(out _)) IsCollision = true;
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.TryGetComponent<GroundGameObject>(out _)) IsCollision = false;
        }
    }
}