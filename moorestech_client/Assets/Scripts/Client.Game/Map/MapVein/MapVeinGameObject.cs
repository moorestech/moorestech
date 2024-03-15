using UnityEngine;

namespace Client.Game.Map.MapVein
{
    public class MapVeinGameObject : MonoBehaviour
    {
        public Vector2Int VeinRangeMinPos => new((int)transform.position.x, (int)transform.position.z);
        
        public Vector2Int VeinRangeMaxPos => veinRangeMax + VeinRangeMinPos;
        [SerializeField] private Vector2Int veinRangeMax;

        public string VeinItemModId => veinItemModId;
        [SerializeField] private string veinItemModId;
        public string VeinItemId => veinItemId;
        [SerializeField] private string veinItemId;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            
            var min = new Vector3(VeinRangeMinPos.x, 0, VeinRangeMinPos.y);
            var max = new Vector3(VeinRangeMaxPos.x, 10, VeinRangeMaxPos.y);
            
            Gizmos.DrawWireCube(transform.position, max - min);
        }
    }
}