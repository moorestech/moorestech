using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MainGame.ModLoader.Texture
{
    public static class GetExtractedZipTexture
    {
        public static Texture2D Get(string extractedModDirectory,string path)
        {
            var texture = new Texture2D(1, 1);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(extractedModDirectory, path)));
            return texture;
        }
    }
}