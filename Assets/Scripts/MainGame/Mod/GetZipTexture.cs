using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace MainGame.Mod
{
    public static class GetZipTexture
    {
        public static Texture2D Get(ZipArchive zipArchive, string path)
        {
            var zipArchiveEntry = zipArchive.GetEntry(path);
            if (zipArchiveEntry == null) return null;

            using var ms = new MemoryStream();
            zipArchiveEntry.Open().CopyTo(ms);
            
            var texture = new Texture2D(1, 1);
            texture.LoadImage(ms.ToArray());
            
            return texture;
        }
    }
}