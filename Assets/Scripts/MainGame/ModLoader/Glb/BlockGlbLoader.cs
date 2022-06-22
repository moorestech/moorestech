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
                //GameObjectの取得
                var gameObject = await GlbLoader.Load(mod.ExtractedPath, BlockDirectory + config.Name + ".glb");
                if (gameObject == null)
                {
                    Debug.LogWarning("GlbFile Not Found  ModId:" + mod.ModMetaJson.ModId + " BlockName:" + config.Name);
                    continue;
                }

                blocks.Add(new BlockData(,config.Name));
            }

            return blocks;
        }

        private static BlockData SetUpObject(GameObject blockModel,Transform blockPrefabParent,BlockConfigData config,global::Mod.Loader.Mod mod)
        {
            blockModel.name = "model";
            //ブロックモデルの位置をリセットしてから親の設定
            blockModel.transform.position = Vector3.zero;
            blockModel.transform.localScale = Vector3.one;
            blockModel.transform.rotation = Quaternion.Euler(Vector3.zero);
            var blockParent = new GameObject($"{mod.ModMetaJson.ModId} : {config.Name}");
            blockModel.transform.SetParent(blockParent.transform);
            
            //コンフィグにあるモデルのサイズを適応
            blockModel.transform.localPosition = config.ModelTransform.Position.ToUniVector3();
            blockModel.transform.localRotation = config.ModelTransform.Rotation.ToQuotation();
            blockModel.transform.localScale = config.ModelTransform.Scale.ToUniVector3();
            
            //マテリアルをURPに変更
            ChangeStandardToUrpMaterial(blockModel);
            
            //コンポーネントの設定
            var blockObj = blockParent.AddComponent<BlockGameObject>();
            
        }

        private static void ChangeStandardToUrpMaterial(GameObject gameObject)
        {        
            foreach (var meshRenderer in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = meshRenderer.materials;
                for (int i = 0; i < meshRenderer.sharedMaterials.Length; i++)
                {
                    materials[i] = meshRenderer.materials[i].StandardToUrpLit();
                }

                meshRenderer.materials = materials;
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