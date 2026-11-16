using System;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.Content
{
    /// <summary>
    /// ID → 表示素材を 1 か所で解く Content 層（A-4）
    /// Content layer resolving IDs into display material in one place (A-4)
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

        // マスタに無い ID なら false（未知の ID を受けうる口だけが使う。他は Item を使い、無ければ例外で開発者が気づく）
        // False for an id absent from the master (only for paths that may receive unknown ids; others use Item and throw so developers notice)
        public bool TryItem(ItemId id, out UiIcon icon)
        {
            icon = MasterHolder.ItemMaster.ExistItemId(id) ? Item(id) : null;
            return icon != null;
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
            // 空きは表示素材にしない（画面側が Icon を null にする）
            // An empty ID is not display material (the screen nulls the icon)
            if (id == ItemMaster.EmptyItemId) throw new ArgumentException("The empty item has no display material; pass a null icon to ItemSlotContent instead", nameof(id));
            return UiText.FromContent(ContentLocalizationKeys.ItemName(MasterHolder.ItemMaster.GetItemGuid(id)));
        }

        public Color FluidFace(FluidId id)
        {
            // 空タンクは色なし。Fluid と同じく止める
            // An empty fluid (empty tank) has no color; stop here as Fluid does
            if (id == FluidMaster.EmptyFluidId) throw new ArgumentException("The empty fluid has no fill color; the screen must not ask for it", nameof(id));
            var master = MasterHolder.FluidMaster.GetFluidMaster(id);
            // 色の形式はロード時に FluidMasterUtil.Validate が検証済みなので、解けない色には到達しない（来たら検証の穴として止める）
            // FluidMasterUtil.Validate checked the format at load, so an unparsable color is unreachable (if it comes, stop on the validation hole)
            if (!ColorUtility.TryParseHtmlString(master.Color, out var face)) throw new InvalidOperationException($"Fluid {master.Name} has color '{master.Color}' that passed master validation but cannot be parsed");
            return face;
        }
    }
}
