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

            // テクスチャが無い種類は材質を共有しない(無地のまま個別に持つ)
            // A kind without a texture does not share a material (kept plain and separate)
            var newMaterial = new Material(itemMaterial) { mainTexture = texture };
            if (texture != null) MaterialCache[itemId] = newMaterial;
            meshRenderer.material = newMaterial;
        }
    }
}
