using System;
using System.Collections.Generic;
using System.IO;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Model.ConnectToolsModule;

namespace Client.Mod.Texture
{
    /// <summary>
    /// connectToolのimagePathからアイコン画像を読み込み、connectToolGuid索引で保持する
    /// Loads connect-tool icon images from imagePath and indexes them by connectToolGuid
    /// </summary>
    public static class ConnectToolTextureLoader
    {
        public static Dictionary<Guid, ItemViewData> GetConnectToolTexture(string modDirectory)
        {
            var textureList = new Dictionary<Guid, ItemViewData>();

            var mods = new ModsResource(modDirectory);

            foreach (var mod in mods.Mods)
            {
                // 全connectToolに対してmodからテクスチャを取得する
                // Fetch textures for every connectTool from the mod
                foreach (var element in MasterHolder.ConnectToolMaster.All)
                {
                    textureList[element.ConnectToolGuid] = LoadViewData(element, mod.Value);
                }
            }

            return textureList;
        }

        private static ItemViewData LoadViewData(ConnectToolMasterElement element, global::Mod.Loader.Mod mod)
        {
            // imagePathが空ならassets/connectTool/{name}.pngへフォールバックする
            // Fall back to assets/connectTool/{name}.png when imagePath is empty
            var path = string.IsNullOrEmpty(element.ImagePath) ? Path.Combine("assets", "connectTool", $"{element.Name}.png") : element.ImagePath;

            // 画像が無い場合はnullテクスチャのまま生成する（ToSpriteがnull安全のため落ちない）
            // Keep a null texture when the image is missing (ToSprite is null-safe, so this does not crash)
            var texture = GetExtractedZipTexture.Get(mod.ExtractedPath, path);
            return new ItemViewData(texture, element.Name);
        }
    }
}
