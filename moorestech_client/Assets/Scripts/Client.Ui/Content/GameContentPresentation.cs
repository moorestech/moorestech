using System;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Content
{
    /// <summary>
    /// ID → 表示素材（UiIcon・名前）を 1 か所で解く Content 層（設計書 A-4）
    /// The Content layer resolving IDs into display material (UiIcon and names) in one place (design A-4)
    /// </summary>
    public sealed class GameContentPresentation
    {
        private readonly IGameContentTextureSource _textures;

        public GameContentPresentation(IGameContentTextureSource textures)
        {
            _textures = textures;
        }

        public UiIcon Item(ItemId id)
        {
            // 名前を先に解き、空の ID はテクスチャを引く前に止める（素材の側の LogError を先に出さない）
            // Resolve the name first so an empty ID stops before the texture lookup logs anything
            var name = ItemName(id);
            return new UiIcon(_textures.ItemTexture(id), id.AsPrimitive().ToString(), name);
        }

        public UiIcon Block(BlockId id)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(id).BlockGuid;
            return new UiIcon(_textures.BlockTexture(id), id.AsPrimitive().ToString(), UiText.FromContent(ContentLocalizationKeys.BlockName(blockGuid)));
        }

        public UiIcon Fluid(FluidId id)
        {
            // 空の液体（空タンク）も表示素材にしない。マスタは Guid.Empty を黙って返すのでここで止める
            // An empty fluid (empty tank) is never display material; the master silently returns Guid.Empty, so stop here
            if (id == FluidMaster.EmptyFluidId) throw new ArgumentException("The empty fluid has no display material; the screen must not ask for it", nameof(id));
            var fluidGuid = MasterHolder.FluidMaster.GetFluidGuid(id);
            return new UiIcon(_textures.FluidTexture(id), id.AsPrimitive().ToString(), UiText.FromContent(ContentLocalizationKeys.FluidName(fluidGuid)));
        }

        public UiText ItemName(ItemId id)
        {
            // 空きは表示素材にしない。画面が ItemSlotContent の Icon を null にして表す
            // An empty ID is never display material; the screen expresses it with a null ItemSlotContent icon
            if (id == ItemMaster.EmptyItemId) throw new ArgumentException("The empty item has no display material; pass a null icon to ItemSlotContent instead", nameof(id));
            return UiText.FromContent(ContentLocalizationKeys.ItemName(MasterHolder.ItemMaster.GetItemGuid(id)));
        }
    }
}
