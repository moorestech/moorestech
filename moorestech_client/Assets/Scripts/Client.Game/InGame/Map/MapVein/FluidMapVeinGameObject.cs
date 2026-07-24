using System;
using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    [ExecuteAlways]
    public class FluidMapVeinGameObject : MonoBehaviour
    {
        public Vector3Int MinPosition => Service.MinPosition(bounds);
        public Vector3Int MaxPosition => Service.MaxPosition(bounds);

        // mapVeinsマスタのveinGuidを参照。item/fluidの区別はマスタ側のveinTypeが持つ
        // References a veinGuid in the mapVeins master; item/fluid distinction lives in the master's veinType
        public Guid VeinGuid => Guid.Parse(veinGuid);
        [SerializeField] private string veinGuid;

        public Bounds Bounds => bounds;
        [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);
        
        private MapVeinGameObjectService _service;
        public MapVeinGameObjectService Service => _service ??= new MapVeinGameObjectService(transform);

        public void SetBounds(Bounds setBounds) => bounds = MapVeinGameObjectService.NormalizeBounds(setBounds);

        private void Update()
        {
#if UNITY_EDITOR
            bounds = MapVeinGameObjectService.NormalizeBounds(bounds);
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Service.DrawGizmo(bounds, Color.blue);
        }
    }
}
