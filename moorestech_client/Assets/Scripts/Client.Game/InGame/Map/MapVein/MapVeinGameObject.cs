using System;
using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    [ExecuteAlways]
    public class MapVeinGameObject : MonoBehaviour
    {
        public Vector3Int MinPosition => new(
            Mathf.RoundToInt(transform.position.x - bounds.size.x / 2f + bounds.center.x),
            Mathf.RoundToInt(transform.position.y - bounds.size.y / 2f + bounds.center.y),
            Mathf.RoundToInt(transform.position.z - bounds.size.z / 2f + bounds.center.z));
        
        public Vector3Int MaxPosition => new(
            Mathf.RoundToInt(transform.position.x + bounds.size.x / 2f + bounds.center.x),
            Mathf.RoundToInt(transform.position.y + bounds.size.y / 2f + bounds.center.y),
            Mathf.RoundToInt(transform.position.z + bounds.size.z / 2f + bounds.center.z));
        
        public Guid VeinItemGuid => Guid.Parse(veinItemGuid);
        [SerializeField] private string veinItemGuid;
        
        public Bounds Bounds => bounds;
        [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);
        
        public void SetBounds(Bounds setBounds)
        {
            bounds = setBounds;
            
            var size = bounds.size;
            var sizeX = size.x < 1 ? 1 : Mathf.RoundToInt(size.x);
            var sizeY = size.y < 1 ? 1 : Mathf.RoundToInt(size.y);
            var sizeZ = size.z < 1 ? 1 : Mathf.RoundToInt(size.z);
            bounds.size = new Vector3(sizeX, sizeY, sizeZ);
            
            var centerX = sizeX % 2f == 0 ? 0 : 0.5f;
            var centerY = sizeY % 2f == 0 ? 0 : 0.5f;
            var centerZ = sizeZ % 2f == 0 ? 0 : 0.5f;
            bounds.center = new Vector3(centerX, centerY, centerZ);
        }
        
        private void Update()
        {
#if UNITY_EDITOR
            OnEditorUpdate();
#endif
        }
        
        private void OnEditorUpdate()
        {;
            SetBounds(bounds);
        }
        
        private void OnDrawGizmosSelected()
        {
            var gizmoBounds = new Bounds();
            gizmoBounds.SetMinMax(MinPosition, MaxPosition);
            
            var color = Color.red;
            color.a = 0.5f;
            Gizmos.color = color;
            Gizmos.DrawCube(gizmoBounds.center, gizmoBounds.size);
        }
    }
}