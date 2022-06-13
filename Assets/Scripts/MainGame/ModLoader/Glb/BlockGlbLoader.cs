using System.Collections.Generic;
using System.Linq;
using Core.Block.Config.LoadConfig;
using Cysharp.Threading.Tasks;
using Mod.Loader;
using SinglePlay;
using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGlbLoader
    {
        private const string BlockDirectory = "assets/block/";
        public static async UniTask<List<(BlockGameObject block,string name)>> GetBlockLoader(string modDirectory,SinglePlayInterface singlePlayInterface)
        {
            var blocks = new List<(BlockGameObject block,string name)>();
            
            using var mods = new ModsResource(modDirectory);

            
            foreach (var mod in mods.Mods)
            {
                var blockIds = singlePlayInterface.BlockConfig.GetBlockIds(mod.Value.ModMetaJson.ModId);
                var blockConfigs = blockIds.Select(singlePlayInterface.BlockConfig.GetBlockConfig).ToList();

                blocks.AddRange(await GetBlocks(blockConfigs,mod.Value));
            }
            
            
            return blocks;
        }

        private static async UniTask<List<(BlockGameObject block, string name)>> GetBlocks(List<BlockConfigData> blockConfigs, global::Mod.Loader.Mod mod)
        {
            var blocks = new List<(BlockGameObject block,string name)>();
            
            
            foreach (var config in blockConfigs)
            {
                var gameObject = await GlbLoader.Load(mod.ExtractedPath, BlockDirectory + config.Name + ".glb");
                if (gameObject == null)
                {
                    Debug.LogError("GlbFile Not Found  ModId:" + mod.ModMetaJson.ModId + " BlockName:" + config.Name);
                    continue;
                }
                blocks.Add((gameObject.AddComponent<BlockGameObject>(),config.Name));
            }

            return blocks;
        }
    }
}