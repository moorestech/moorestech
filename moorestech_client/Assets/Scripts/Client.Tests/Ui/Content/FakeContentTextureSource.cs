using System;
using Client.Ui.Content;
using Core.Master;
using UnityEngine;

namespace Client.Tests.Ui.Content
{
    /// <summary>
    /// テスト用のテクスチャの出所。種類ごとに決めた 1 枚（null 可）を返す
    /// Test texture source returning one fixed texture (or null) per kind
    /// </summary>
    internal sealed class FakeContentTextureSource : IGameContentTextureSource
    {
        private readonly Texture2D _item;
        private readonly Texture2D _block;
        private readonly Texture2D _fluid;

        public FakeContentTextureSource(Texture2D item, Texture2D block, Texture2D fluid)
        {
            _item = item;
            _block = block;
            _fluid = fluid;
        }

        public Texture2D ItemTexture(ItemId id)
        {
            return _item;
        }

        public Texture2D BlockTexture(BlockId id)
        {
            return _block;
        }

        public Texture2D FluidTexture(FluidId id)
        {
            return _fluid;
        }

        // 車両と接続ツールはブロックと同じ 1 枚を返す（L1 は絵の有無だけを見る）
        // Train cars and connect tools return the block texture (L1 only checks whether a picture exists)
        public Texture2D TrainCarTexture(Guid trainCarGuid)
        {
            return _block;
        }

        public Texture2D ConnectToolTexture(Guid connectToolGuid)
        {
            return _block;
        }
    }
}
