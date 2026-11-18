using System;
using Core.Master;
using UnityEngine;

namespace Client.Ui.Content
{
    /// <summary>
    /// ID からテクスチャを引く口（欠けは null）
    /// Looks up textures by ID (missing returns null)
    /// </summary>
    public interface IGameContentTextureSource
    {
        Texture2D ItemTexture(ItemId id);
        Texture2D BlockTexture(BlockId id);
        Texture2D FluidTexture(FluidId id);
        Texture2D TrainCarTexture(Guid trainCarGuid);
        Texture2D ConnectToolTexture(Guid connectToolGuid);
    }
}
