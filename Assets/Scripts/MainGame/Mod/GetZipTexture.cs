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
            
            using var textureBinary = new BinaryReader(zipArchiveEntry.Open());
            
            
            var bytes = textureBinary.ReadBytes((int)textureBinary.BaseStream.Length);
            
            Texture2D texture = new Texture2D(1, 1);
            texture.LoadImage(bytes);
            
            return texture;
        }
    }
}