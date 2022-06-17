using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MainGame.ModLoader.Texture
{
    public static class GetExtractedZipTexture
    {
        public static async UniTask<Texture2D> Get(string extractedModDirectory,string path)
        {
            var texture = new Texture2D(1, 1);
            texture.LoadImage(await File.ReadAllBytesAsync(Path.Combine(extractedModDirectory, path)));
            return texture;
        }
    }
}