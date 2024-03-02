using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.BlockConfig;
using Constant;
using Constant.Util;
using Mod.Loader;
using ServerServiceProvider;
using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGlbLoader
    {
        private const string BlockDirectory = "assets/block/";


        public static async UniTask<List<BlockData>> GetBlockLoader(string modDirectory, MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            var blocks = new List<BlockData>();

            var mods = new ModsResource(modDirectory);

            var blockPrefabsParent = new GameObject("BlockPrefabsParent");

            foreach (var mod in mods.Mods)
            {
                var blockIds = moorestechServerServiceProvider.BlockConfig.GetBlockIds(mod.Value.ModMetaJson.ModId);
                var blockConfigs = blockIds.Select(moorestechServerServiceProvider.BlockConfig.GetBlockConfig).ToList();

                blocks.AddRange(await GetBlocks(blockConfigs, mod.Value, blockPrefabsParent.transform));
            }


            return blocks;
        }

        private static async UniTask<List<BlockData>> GetBlocks(List<BlockConfigData> blockConfigs, Mod.Loader.Mod mod, Transform blockPrefabsParent)
        {
            var blocks = new List<BlockData>();


            foreach (var config in blockConfigs)
            {
                //glbからモデルのロード
                var gameObject = await GlbLoader.Load(mod.ExtractedPath, BlockDirectory + config.Name + ".glb");
                if (gameObject == null)
                {
                    Debug.LogError("GlbFile Not Found  ModId:" + mod.ModMetaJson.ModId + " BlockName:" + config.Name);
                    continue;
                }

                blocks.Add(new BlockData(SetUpObject(gameObject, blockPrefabsParent, config, mod), config.Name, config.Type));
            }

            return blocks;
        }

        private static BlockGameObject SetUpObject(GameObject blockModel, Transform blockPrefabsParent, BlockConfigData config, Mod.Loader.Mod mod)
        {
            blockModel.name = "model";
            //ブロックモデルの位置をリセットしてから親の設定
            blockModel.transform.position = Vector3.zero;
            blockModel.transform.localScale = Vector3.one;
            blockModel.transform.rotation = Quaternion.Euler(Vector3.zero);
            var blockParent = new GameObject($"{mod.ModMetaJson.ModId} : {config.Name}");
            blockModel.transform.SetParent(blockParent.transform);

            //コンフィグにあるモデルのサイズを適応
            blockModel.transform.localPosition = config.ModelTransform.Position;
            blockModel.transform.localRotation = Quaternion.Euler(config.ModelTransform.Rotation);
            blockModel.transform.localScale = config.ModelTransform.Scale;

            //マテリアルをURPに変更
            ChangeStandardToUrpMaterial(blockModel);

            //コンポーネントの設定
            var blockObj = blockParent.AddComponent<BlockGameObject>();
            //子要素のコンポーネントの設定
            foreach (var mesh in blockObj.GetComponentsInChildren<MeshRenderer>())
            {
                mesh.gameObject.AddComponent<BlockGameObjectChild>();
                mesh.gameObject.AddComponent<MeshCollider>();
            }

            //ヒエラルキーが散らばらないようにオブジェクトを設定
            blockObj.gameObject.transform.SetParent(blockPrefabsParent);

            blockObj.gameObject.SetActive(false);


            //すべてのレイヤーをBlockに設定
            foreach (var trnasform in blockObj.GetComponentsInChildren<Transform>()) trnasform.gameObject.layer = LayerConst.BlockLayer;

            return blockObj;
        }

        private static void ChangeStandardToUrpMaterial(GameObject gameObject)
        {
            foreach (var meshRenderer in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = meshRenderer.materials;
                for (var i = 0; i < meshRenderer.sharedMaterials.Length; i++) materials[i] = meshRenderer.materials[i].StandardToUrpLit();

                meshRenderer.materials = materials;
            }
        }
    }

    public class BlockData
    {
        public readonly BlockGameObject BlockObject;
        public readonly string Name;
        public readonly string Type;

        public BlockData(BlockGameObject blockObject, string name, string type)
        {
            BlockObject = blockObject;
            Name = name;
            Type = type;
        }
    }
}