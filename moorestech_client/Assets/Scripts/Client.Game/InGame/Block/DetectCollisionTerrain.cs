using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class DetectCollisionTerrain : MonoBehaviour
    {
        public bool isCollisionTerrain;
        private void FixedUpdate()
        {
            isCollisionTerrain = false;
        }
        
        private void OnCollisionStay(Collision other)
        {
            isCollisionTerrain = true;
        }
        private void OnTriggerStay(Collider other)
        {
            isCollisionTerrain = true;
        }
    }
}