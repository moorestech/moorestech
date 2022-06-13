using System.IO;
using UnityEngine;

namespace MainGame.ModLoader.Texture
{
    public static class GetZipTexture
    {
        public static Texture2D Get(string extractedModDirectory,string path)
        {
            var texture = new Texture2D(1, 1);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(extractedModDirectory, path)));
            return texture;
        }
    }
}