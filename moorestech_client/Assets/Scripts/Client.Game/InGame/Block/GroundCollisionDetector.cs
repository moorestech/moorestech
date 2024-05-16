using Client.Game.InGame.Chunk;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class GroundCollisionDetector : MonoBehaviour
    {
        public bool IsCollision { get; private set; }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent<GroundGameObject>(out _))
            {
                IsCollision = true;
            }
        }
        
        private void OnCollisionExit(Collision other)
        {
            if (other.gameObject.TryGetComponent<GroundGameObject>(out _))
            {
                IsCollision = false;
            }
        }
    }
}