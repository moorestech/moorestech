using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    public class MapVeinGameObject : MonoBehaviour
    {
        [SerializeField] private Vector2Int veinRangeMax;
        [SerializeField] private string veinItemModId;
        [SerializeField] private string veinItemId;
        public Vector2Int VeinRangeMinPos => new((int)transform.position.x, (int)transform.position.z);

        public Vector2Int VeinRangeMaxPos => veinRangeMax + VeinRangeMinPos;

        public string VeinItemModId => veinItemModId;
        public string VeinItemId => veinItemId;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            var min = new Vector3(VeinRangeMinPos.x, 0, VeinRangeMinPos.y);
            var max = new Vector3(VeinRangeMaxPos.x, 10, VeinRangeMaxPos.y);

            Gizmos.DrawWireCube(transform.position, max - min);
        }
    }
}