using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Client.UitkPoc.Shell
{
    // 全デモが共有する資源（日本語フォント・masterの場所・アイコン）
    // Resources shared by every demo (Japanese font, master location, icons)
    public class UitkPocContext
    {
        public readonly FontAsset JapaneseFont;
        public readonly string ModDirectory;
        private readonly Dictionary<string, Texture2D> _iconCache = new();

        public UitkPocContext(FontAsset japaneseFont, string modDirectory)
        {
            JapaneseFont = japaneseFont;
            ModDirectory = modDirectory;
        }

        public Texture2D LoadItemIcon(string itemName)
        {
            if (_iconCache.TryGetValue(itemName, out var cached)) return cached;

            // png優先、無ければjpegを読む。どちらも無いアイテムはnullのまま描画側でプレースホルダにする
            // Prefer png then jpeg; missing icons stay null and the view draws a placeholder
            var texture = LoadFirstExisting(Path.Combine(ModDirectory, "assets", "item", itemName + ".png"), Path.Combine(ModDirectory, "assets", "item", itemName + ".jpeg"));
            if (texture == null) Debug.LogWarning($"[UitkPoc] アイコンが無い: {itemName}");
            _iconCache[itemName] = texture;
            return texture;

            #region Internal

            Texture2D LoadFirstExisting(string pngPath, string jpegPath)
            {
                var path = File.Exists(pngPath) ? pngPath : File.Exists(jpegPath) ? jpegPath : null;
                if (path == null) return null;
                var result = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                result.LoadImage(File.ReadAllBytes(path));
                return result;
            }

            #endregion
        }
    }
}
