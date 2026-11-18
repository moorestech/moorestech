using System;
using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;

namespace Client.Ui.Content
{
    /// <summary>
    /// 本番のテクスチャの出所
    /// The production texture source
    /// </summary>
    public sealed class ClientContextContentTextureSource : IGameContentTextureSource
    {
        // 空の ID はコンテナが LogError 無しで null を返すので、引く前に止める（GameContentPresentation と同じ扱い）
        // Containers return null for an empty ID without logging, so stop before the lookup (same as GameContentPresentation)
        // silent-ok: 空でない欠けた ID は各コンテナが LogError を出して null を返す / 画面では GameIcon が #id を描き、開発者は Console の LogError で気づく
        // silent-ok: each container logs an error and returns null for a missing non-empty ID / on screen GameIcon draws #id and the developer sees the LogError
        public Texture2D ItemTexture(ItemId id)
        {
            if (id == ItemMaster.EmptyItemId) throw new ArgumentException("The empty item has no texture; pass a null icon to ItemSlotContent instead", nameof(id));
            return ClientContext.ItemImageContainer.GetItemView(id)?.ItemTexture as Texture2D;
        }

        public Texture2D BlockTexture(BlockId id)
        {
            return ClientContext.BlockImageContainer.GetBlockView(id)?.ItemTexture as Texture2D;
        }

        public Texture2D FluidTexture(FluidId id)
        {
            if (id == FluidMaster.EmptyFluidId) throw new ArgumentException("The empty fluid has no texture; the screen must not ask for it", nameof(id));
            return ClientContext.FluidImageContainer.GetItemView(id)?.FluidTexture as Texture2D;
        }

        public Texture2D TrainCarTexture(Guid trainCarGuid)
        {
            return ClientContext.TrainCarImageContainer.GetTrainCarView(trainCarGuid)?.ItemTexture as Texture2D;
        }

        public Texture2D ConnectToolTexture(Guid connectToolGuid)
        {
            return ClientContext.ConnectToolImageContainer.GetConnectToolView(connectToolGuid)?.ItemTexture as Texture2D;
        }
    }
}
