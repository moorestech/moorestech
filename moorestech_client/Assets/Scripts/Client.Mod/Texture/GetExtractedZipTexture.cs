using System;
using System.IO;
using UnityEngine;

namespace Client.Mod.Texture
{
    //TODO 成功か失敗かと理由を返すようにする　ログ出力は使う側が行う
    public static class GetExtractedZipTexture
    {
        // 対応する画像拡張子の一覧
        // Supported image extensions
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

        public static Texture2D Get(string extractedModDirectory, string path)
        {
            //TODO ログ基盤
            var imgPath = Path.Combine(extractedModDirectory, path);

            // 指定パスにファイルがなければ、別の画像拡張子で探す
            // If file not found at specified path, try alternative image extensions
            if (!File.Exists(imgPath))
            {
                imgPath = FindWithAlternativeExtension(imgPath);
                if (imgPath == null) return null;
            }

            try
            {
                var texture = new Texture2D(1, 1);
                texture.LoadImage(File.ReadAllBytes(imgPath));
                return texture;
            }
            catch (Exception e)
            {
                Debug.Log($"画像のロード中にエラーが発生しました。パス {imgPath} \nMessage {e.Message} \nStackTrace {e.StackTrace}");
                return null;
            }

            #region Internal

            string FindWithAlternativeExtension(string originalPath)
            {
                var withoutExt = Path.ChangeExtension(originalPath, null);
                foreach (var ext in SupportedExtensions)
                {
                    var candidate = withoutExt + ext;
                    if (File.Exists(candidate)) return candidate;
                }
                return null;
            }

            #endregion
        }
    }
}