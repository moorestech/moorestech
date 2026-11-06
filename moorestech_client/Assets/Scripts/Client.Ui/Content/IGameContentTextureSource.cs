using Core.Master;
using UnityEngine;

namespace Client.Ui.Content
{
    /// <summary>
    /// ID からアイコンのテクスチャを引く口。欠けた ID は null を返し、欠けのログは素材の側が出す。空のアイテム・空の液体は渡さない
    /// Looks up icon textures by ID; a missing ID returns null and the source logs the gap; never pass the empty item or empty fluid
    /// </summary>
    public interface IGameContentTextureSource
    {
        Texture2D ItemTexture(ItemId id);
        Texture2D BlockTexture(BlockId id);
        Texture2D FluidTexture(FluidId id);
    }
}
