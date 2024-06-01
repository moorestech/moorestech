﻿using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Common.Util;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.BlockConfig;
using Game.Context;
using Mod.Loader;
using UnityEngine;

namespace Client.Mod.Glb
{
    public class BlockGlbLoader
    {
        private const string BlockDirectory = "assets/block/";

        public static async UniTask<List<BlockData>> GetBlockLoader(string modDirectory)
        {
            var blocks = new List<BlockData>();

            var mods = new ModsResource(modDirectory);

            var blockPrefabsParent = new GameObject("Dynamic loaded block prefabs parent");
            Object.DontDestroyOnLoad(blockPrefabsParent);

            foreach (KeyValuePair<string, global::Mod.Loader.Mod> mod in mods.Mods)
            {
                List<int> blockIds = ServerContext.BlockConfig.GetBlockIds(mod.Value.ModMetaJson.ModId);
                var blockConfigs = blockIds.Select(ServerContext.BlockConfig.GetBlockConfig).ToList();

                blocks.AddRange(await GetBlocks(blockConfigs, mod.Value, blockPrefabsParent.transform));
            }

            return blocks;
        }

        private static async UniTask<List<BlockData>> GetBlocks(List<BlockConfigData> blockConfigs, global::Mod.Loader.Mod mod, Transform blockPrefabsParent)
        {
            var blocks = new List<BlockData>();

            foreach (var config in blockConfigs)
            {
                var name = config.Name;
                var type = config.Type;

                //glbからモデルのロード
                var gameObject = await GlbLoader.Load(mod.ExtractedPath, BlockDirectory + config.Name + ".glb");
                if (gameObject == null)
                {
                    //TODO Prefabコンテナのことも考慮する（モデルのパスを指定するようにする
                    Debug.LogWarning("GlbFile Not Found  ModId:" + mod.ModMetaJson.ModId + " BlockName:" + config.Name);
                    continue;
                }

                var setupObject = SetUpObject(gameObject, blockPrefabsParent, config, mod);
                blocks.Add(new BlockData(setupObject, name, type, config));
            }

            return blocks;
        }

        private static GameObject SetUpObject(GameObject blockModel, Transform blockPrefabsParent, BlockConfigData config, global::Mod.Loader.Mod mod)
        {
            blockModel.name = "model";
            //ブロックモデルの位置をリセットしてから親の設定
            blockModel.transform.position = Vector3.zero;
            blockModel.transform.localScale = Vector3.one;
            blockModel.transform.rotation = Quaternion.Euler(Vector3.zero);

            //コンフィグにあるモデルのサイズを適応
            blockModel.transform.localPosition = config.ModelTransform.Position;
            blockModel.transform.localRotation = Quaternion.Euler(config.ModelTransform.Rotation);
            blockModel.transform.localScale = config.ModelTransform.Scale;

            //マテリアルをURPに変更
            ChangeStandardToUrpMaterial(blockModel);

            var blockParent = new GameObject($"{mod.ModMetaJson.ModId} : {config.Name}");
            blockModel.transform.SetParent(blockParent.transform);

            //ヒエラルキーが散らばらないようにオブジェクトを設定
            blockParent.gameObject.transform.SetParent(blockPrefabsParent);
            blockParent.gameObject.SetActive(false);

            //すべてのレイヤーをBlockに設定
            foreach (var transform in blockModel.GetComponentsInChildren<Transform>()) transform.gameObject.layer = LayerConst.BlockLayer;

            return blockParent;
        }

        private static void ChangeStandardToUrpMaterial(GameObject gameObject)
        {
            foreach (var meshRenderer in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] materials = meshRenderer.materials;
                for (var i = 0; i < meshRenderer.sharedMaterials.Length; i++) materials[i] = meshRenderer.materials[i].StandardToUrpLit();

                meshRenderer.materials = materials;
            }
        }
    }

    public class BlockData
    {
        public readonly BlockConfigData BlockConfig;
        public readonly GameObject BlockObject;
        public readonly string Name;
        public readonly string Type;

        public BlockData(GameObject blockObject, string name, string type, BlockConfigData blockConfig)
        {
            BlockObject = blockObject;
            Name = name;
            Type = type;
            BlockConfig = blockConfig;
        }
    }
}