using UnityEngine;
namespace Client.Game.InGame.Entity.Factory
{
    public readonly struct BeltItemPrefab
    {
        internal readonly GameObject Prefab;
        internal readonly Texture Texture;
        internal readonly bool CustomModel;
        public BeltItemPrefab(GameObject prefab, Texture texture, bool customModel)
        { Prefab = prefab; Texture = texture; CustomModel = customModel; }
    }
}
