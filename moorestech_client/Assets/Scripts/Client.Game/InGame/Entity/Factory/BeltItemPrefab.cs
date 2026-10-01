using UnityEngine;
namespace Client.Game.InGame.Entity.Factory
{
    public readonly struct BeltItemPrefab
    {
        public readonly GameObject Prefab;
        public readonly Texture Texture;
        public readonly bool CustomModel;
        public BeltItemPrefab(GameObject prefab, Texture texture, bool customModel)
        { Prefab = prefab; Texture = texture; CustomModel = customModel; }
    }
}
