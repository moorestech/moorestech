using System;
using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    [ExecuteAlways]
    public class ItemMapVeinGameObject : MonoBehaviour
    {
        public Guid VeinItemGuid => Guid.Parse(veinItemGuid);
        [SerializeField] private string veinItemGuid;

        public Bounds Bounds => bounds;
        [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);

    }
}
