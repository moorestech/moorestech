using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;

namespace Client.UitkPoc.TextAndInput
{
    // modから実行時に読むアイコンを、文中に<sprite name="アイテム名">で埋め込めるSpriteAssetへまとめる
    // Packs icons loaded from mods at runtime into a SpriteAsset usable inline as <sprite name="item">
    public static class RuntimeSpriteAssetBuilder
    {
        private const int IconPixels = 64;
        private const BindingFlags InternalField = BindingFlags.NonPublic | BindingFlags.Instance;

        public static SpriteAsset Build(List<(string name, Texture2D icon)> icons)
        {
            // GPUで縮小してから1枚のアトラスへ詰める。PackTexturesは必要最小の大きさへアトラスを縮めるので、矩形は縮んだ後の寸法で計算する
            // Downscale on GPU then pack; PackTextures shrinks the atlas, so rects use the post-pack size
            var scaled = new Texture2D[icons.Count];
            for (var i = 0; i < icons.Count; i++) scaled[i] = Downscale(icons[i].icon);
            var atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
            var rects = atlas.PackTextures(scaled, 2, 2048, false);

            // 公開APIではアトラス・寸法・字面情報を設定できないため、内部フィールドへ直接書く（Unity更新で壊れうる）
            // No public API sets the atlas, size or face info, so internal fields are written directly (may break on upgrade)
            var asset = ScriptableObject.CreateInstance<SpriteAsset>();
            asset.name = "UitkPocItemIcons";
            SetInternal(asset, "m_SpriteAtlasTexture", atlas);
            SetInternal(asset, "<width>k__BackingField", (float)atlas.width);
            SetInternal(asset, "<height>k__BackingField", (float)atlas.height);
            SetInternal(asset, "m_FaceInfo", new FaceInfo { pointSize = IconPixels, scale = 1, lineHeight = IconPixels, ascentLine = IconPixels * 0.8f, descentLine = -IconPixels * 0.2f });
            asset.material = new Material(Shader.Find("Hidden/TextCore/Sprite")) { mainTexture = atlas };

            for (var i = 0; i < icons.Count; i++)
            {
                var rect = new GlyphRect((int)(rects[i].x * atlas.width), (int)(rects[i].y * atlas.height), IconPixels, IconPixels);
                var glyph = new SpriteGlyph((uint)i, new GlyphMetrics(IconPixels, IconPixels, 0, IconPixels * 0.8f, IconPixels * 1.08f), rect, 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new SpriteCharacter(0xE000u + (uint)i, asset, glyph) { name = icons[i].name });
            }
            asset.UpdateLookupTables();
            return asset;
        }

        private static Texture2D Downscale(Texture2D source)
        {
            var target = RenderTexture.GetTemporary(IconPixels, IconPixels, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, target);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var result = new Texture2D(IconPixels, IconPixels, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, IconPixels, IconPixels), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return result;
        }

        private static void SetInternal(SpriteAsset asset, string fieldName, object value)
        {
            // フィールドが消えたらUnity更新で内部構造が変わったということなので、即座に例外で止める
            // A missing field means Unity's internals changed, so fail loudly right away
            var field = typeof(SpriteAsset).GetField(fieldName, InternalField);
            if (field == null) throw new System.MissingFieldException(nameof(SpriteAsset), fieldName);
            field.SetValue(asset, value);
        }
    }
}
