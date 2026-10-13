using System.Collections.Generic;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 既定のアイテムprefab(Vanilla/Game/ItemEntity)の部品。種類のテクスチャを貼った材質を種類ごとに1つ作って使い回す
    // The component on the default item prefab (Vanilla/Game/ItemEntity); builds one material per item kind with the kind's texture and reuses it
    public class BeltItemTexturedObject : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Material itemMaterial;

        private static readonly Dictionary<ItemId, Material> MaterialCache = new();

        public void SetTexture(Texture texture, ItemId itemId)
        {
            if (MaterialCache.TryGetValue(itemId, out var cachedMaterial))
            {
                meshRenderer.material = cachedMaterial;
                return;
            }

            // テクスチャが無い種類も無地の材質を種類ごとに1つ共有し、個体ごとに増やさない
            // A kind without a texture also shares one plain material per kind instead of growing one per object
            var newMaterial = new Material(itemMaterial) { mainTexture = texture };
            MaterialCache[itemId] = newMaterial;
            meshRenderer.material = newMaterial;
        }
    }
}
