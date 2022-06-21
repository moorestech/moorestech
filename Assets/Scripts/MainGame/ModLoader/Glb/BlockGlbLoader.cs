using System.Collections.Generic;
using System.Linq;
using Core.Block.Config.LoadConfig;
using Cysharp.Threading.Tasks;
using MainGame.Basic.Util;
using Mod.Loader;
using Server.Event;
using SinglePlay;
using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGlbLoader
    {
        private const string BlockDirectory = "assets/block/";
        private const string UrpMaterialPath = "URPLit";
        
        
        public static async UniTask<List<BlockData>> GetBlockLoader(string modDirectory,SinglePlayInterface singlePlayInterface)
        {
            var blocks = new List<BlockData>();
            
            using var mods = new ModsResource(modDirectory);

            var blockPrefabsParent = new GameObject("BlockPrefabsParent");
            
            foreach (var mod in mods.Mods)
            {
                var blockIds = singlePlayInterface.BlockConfig.GetBlockIds(mod.Value.ModMetaJson.ModId);
                var blockConfigs = blockIds.Select(singlePlayInterface.BlockConfig.GetBlockConfig).ToList();

                blocks.AddRange(await GetBlocks(blockConfigs,mod.Value,blockPrefabsParent));
            }
            
            
            return blocks;
        }

        private static async UniTask<List<BlockData>> GetBlocks(List<BlockConfigData> blockConfigs, global::Mod.Loader.Mod mod,GameObject blockPrefabsParent)
        {
            var blocks = new List<BlockData>();
            
            
            foreach (var config in blockConfigs)
            {
                var gameObject = await GlbLoader.Load(mod.ExtractedPath, BlockDirectory + config.Name + ".glb");
                if (gameObject == null)
                {
                    Debug.LogWarning("GlbFile Not Found  ModId:" + mod.ModMetaJson.ModId + " BlockName:" + config.Name);
                    continue;
                }
                
                blockPrefabsParent.SetActive(false);
                gameObject.transform.SetParent(blockPrefabsParent.transform);
                ChangeStandardToUrpMaterial(gameObject);

                blocks.Add(new BlockData(gameObject.AddComponent<BlockGameObject>(),config.Name));
            }

            return blocks;
        }

        private static readonly Material urpMaterial = (Material)Resources.Load(UrpMaterialPath);
        private static void ChangeStandardToUrpMaterial(GameObject gameObject)
        {        
            Debug.Log("Shader " + urpMaterial.name);
            foreach (var meshRenderer in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                for (int i = 0; i < meshRenderer.materials.Length; i++)
                {
                    meshRenderer.sharedMaterials[i] = meshRenderer.materials[i].CopyMaterial(urpMaterial);
                    Debug.Log("SharedMaterial:" + meshRenderer.sharedMaterials[i].shader.name);
                }
            }
        }
    }
    
    public class BlockData{
    
        public readonly BlockGameObject BlockObject;
        public readonly string Name;

        public BlockData(BlockGameObject blockObject, string name)
        {
            BlockObject = blockObject;
            Name = name;
        }
    }
}