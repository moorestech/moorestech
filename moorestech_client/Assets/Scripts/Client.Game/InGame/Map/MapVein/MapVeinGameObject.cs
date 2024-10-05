using System;
using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    public class MapVeinGameObject : MonoBehaviour
    {
        public Vector2Int VeinRangeMinPos => new((int)transform.position.x, (int)transform.position.z);
        public Vector2Int VeinRangeMaxPos => veinRangeMax + VeinRangeMinPos;
        public Guid VeinItemGuid => Guid.Parse(veinItemGuid);
        
        [SerializeField] private string veinItemGuid;
        [SerializeField] private Vector2Int veinRangeMax;
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            
            var min = new Vector3(VeinRangeMinPos.x, 0, VeinRangeMinPos.y);
            var max = new Vector3(VeinRangeMaxPos.x, 10, VeinRangeMaxPos.y);
            
            Gizmos.DrawWireCube(transform.position, max - min);
        }
    }
}