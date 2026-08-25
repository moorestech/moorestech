using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 建設コスト素材1種の不足。所持数と今回の設置に必要な総数
    /// One short construction material: held count and the total required for this placement
    /// </summary>
    public readonly struct ConstructionMaterialShortage
    {
        public readonly ItemId ItemId;
        public readonly int Held;
        public readonly int Required;

        public ConstructionMaterialShortage(ItemId itemId, int held, int required)
        {
            ItemId = itemId;
            Held = held;
            Required = required;
        }
    }
}
