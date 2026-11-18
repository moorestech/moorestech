using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Tests.Ui.Content;
using Client.Ui.Content;
using Client.Ui.State.Hotbar;
using Client.Ui.State.PlacementTarget;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// テスト用の絵の解き手と、絵のある割当（撮影済みの BP。マスタに依らない）
    /// Icon resolver for tests and an assignment with a picture (a captured blueprint; independent of master data)
    /// </summary>
    /// <remarks>
    /// サムネイルは本物の BlueprintThumbnailContainer を使い、撮り直しは同じ Guid への 2 度目の Add で表す（前例 BuildMenuSamples.Icons）
    /// Thumbnails use the real BlueprintThumbnailContainer; a recapture is a second Add for the same Guid (precedent BuildMenuSamples.Icons)
    /// </remarks>
    internal static class HotbarTestPresentation
    {
        public static readonly Guid PicturedBlueprintId = new("5f0b9a8e-1c2d-4e3f-8a9b-0c1d2e3f4a5b");

        public static PlacementTargetIcons Create()
        {
            return Create(CreateThumbnails());
        }

        public static PlacementTargetIcons Create(BlueprintThumbnailContainer thumbnails)
        {
            var textures = new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture);
            return new PlacementTargetIcons(new GameContentPresentation(textures), textures, thumbnails);
        }

        // Web の fixture の枠 0（正方の赤い SVG の絵）にあたる撮影を 1 枚持つ
        // Holds one capture standing in for the Web fixture's slot 0 (a red square SVG)
        public static BlueprintThumbnailContainer CreateThumbnails()
        {
            var thumbnails = new BlueprintThumbnailContainer();
            thumbnails.Add(PicturedBlueprintId, CreateSquare(new Color32(204, 51, 51, 255)));
            return thumbnails;
        }

        // 呼ぶたびに新しい絵の値（参照の等価）
        // A new icon value (reference equality) per call
        public static HotbarAssignedSlot PicturedBlueprint()
        {
            return new HotbarAssignedSlot(PicturedBlueprintId, "参照拠点", new PlacementTargetBlueprintIcon(PicturedBlueprintId));
        }

        // 正方なので枠の中央の主張（R-7）がそのまま効く
        // Square, so the centring claim (R-7) applies directly
        public static Texture2D CreateSquare(Color32 color)
        {
            var texture = new Texture2D(256, 256) { hideFlags = HideFlags.DontSave };
            var pixels = new Color32[256 * 256];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
