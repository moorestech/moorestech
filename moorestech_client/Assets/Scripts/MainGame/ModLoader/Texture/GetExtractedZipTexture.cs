using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MainGame.ModLoader.Texture
{
    //TODO 成功か失敗かと理由を返すようにする　ログ出力は使う側が行う
    public static class GetExtractedZipTexture
    {
        public static Texture2D Get(string extractedModDirectory,string path)
        {
            //TODO ログ基盤
            var imgPath = Path.Combine(extractedModDirectory, path);
            
            //そのパスにファイルがあるかを確認
            if (!File.Exists(imgPath))
            {
                Debug.LogError($"画像ファイルが存在しません パス : {imgPath}");
                return null;
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
        }
    }
}