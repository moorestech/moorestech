using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using UnityEngine;

namespace Client.Ui.Content
{
    /// <summary>
    /// 配置対象の絵の出どころ → UiIcon（Block は表示素材、車両・接続ツールは素材の口、BP は撮影の口から引く）
    /// Placement-target picture source to UiIcon (blocks via presentation, cars and connect tools via the texture source, blueprints via thumbnails)
    /// </summary>
    public sealed class PlacementTargetIcons
    {
        private readonly GameContentPresentation _content;
        private readonly IGameContentTextureSource _textures;
        private readonly IBlueprintThumbnailLookup _thumbnails;

        public PlacementTargetIcons(GameContentPresentation content, IGameContentTextureSource textures, IBlueprintThumbnailLookup thumbnails)
        {
            _content = content;
            _textures = textures;
            _thumbnails = thumbnails;
        }

        // 名前で描く対象（NameOnly）・撮影が消えた BP・テクスチャの欠けは false（面は名前で描き、詳細の列は絵を足さない。[A-100]）
        // False for NameOnly, a blueprint whose capture vanished, and a missing texture (the face draws the name, the detail adds no picture; [A-100])
        // silent-ok: 撮影が消えた BP は次の組み直しで NameOnly になる過渡、テクスチャの欠けは各容器が LogError を出す / 遊ぶ人は名前の面で、開発者は Console の LogError で気づく
        // silent-ok: a vanished capture is a transient until the next rebuild turns it NameOnly, and each container logs a missing texture / players see the name face, developers see the LogError
        public bool TryToUiIcon(PlacementTargetIcon icon, out UiIcon uiIcon)
        {
            uiIcon = icon switch
            {
                PlacementTargetBlockIcon block => _content.Block(block.BlockId),
                PlacementTargetTrainCarIcon car => Textured(_textures.TrainCarTexture(car.TrainCarGuid), car.TrainCarGuid),
                PlacementTargetConnectToolIcon tool => Textured(_textures.ConnectToolTexture(tool.ConnectToolGuid), tool.ConnectToolGuid),
                PlacementTargetBlueprintIcon blueprint => _thumbnails.TryGet(blueprint.BlueprintGuid, out var thumbnail) ? Textured(thumbnail, blueprint.BlueprintGuid) : null,
                PlacementTargetNameOnlyIcon => null,
                _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null),
            };
            if (uiIcon?.Texture != null) return true;
            uiIcon = null;
            return false;
        }

        // 名前は面と詳細が別に描くので、絵の代わりの文字は持たせない
        // The face and detail draw the name themselves, so no fallback text is carried
        private static UiIcon Textured(Texture2D texture, Guid id)
        {
            return new UiIcon(texture, id.ToString("D"), UiText.FromVerbatim(string.Empty));
        }
    }
}
