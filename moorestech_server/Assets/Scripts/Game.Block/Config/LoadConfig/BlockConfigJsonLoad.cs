using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Data.HashFunction.xxHash;
using Core.Const;
using Core.Item.Config;
using Game.Block.Interface.BlockConfig;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Block.Config.LoadConfig
{
    public class BlockConfigJsonLoad
    {
        private readonly Dictionary<string, IBlockConfigParamGenerator> _generators;
        private readonly IItemConfig _itemConfig;
        private readonly string _jsonPath;


        public BlockConfigJsonLoad(IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
            _generators = new VanillaBlockConfigGenerator().Generate(itemConfig);
        }

        public List<BlockConfigData> LoadFromJsons(Dictionary<string, string> blockJsons, List<string> mods)
        {
            var list = new List<BlockConfigData>();
            foreach (var mod in mods)
            {
                if (!blockJsons.TryGetValue(mod, out var json)) continue;
                list.AddRange(LoadFormOneJson(json, mod));
            }

            return list;
        }

        private List<BlockConfigData> LoadFormOneJson(string jsonText, string modId)
        {
            //JSONを動的にデシリアライズする
            dynamic person = JObject.Parse(jsonText);

            var blockDictionary = new List<BlockConfigData>();


            var xxHash = xxHashFactory.Instance.Create(new xxHashConfig
            {
                Seed = xxHashConst.DefaultSeed,
                HashSizeInBits = xxHashConst.DefaultSize,
            });


            //最初に設定されたIDの連番を設定していく
            var id = BlockConst.EmptyBlockId;


            foreach (var block in person.Blocks)
            {
                id++;

                string name = block.name;
                string type = block.type;

                string itemModId = block.itemModId;
                string itemName = block.itemName;

                var itemId = 0;
                if (itemModId == null || itemName == null)
                    //TODO ログ基盤に入れる
                    Debug.Log("[BlockJsonLoad] ブロックのアイテム設定が不正です。modId:" + modId + " ブロック名:" + name);
                else
                    itemId = _itemConfig.GetItemId(itemModId, itemName);

                if (!_generators.TryGetValue(type, out var generator))
                {
                    throw new Exception($"存在しないタイプを指定しています。type  {type} block名 {name} modId {modId}");
                }

                var blockParam = generator.Generate(block.param);

                var hash = BitConverter.ToInt64(xxHash.ComputeHash(modId + "/" + name).Hash);

                var modelTransform = GetModelTransform(block);
                var size = block.size;
                var blockSize = new Vector3Int((int)size.x, (int)size.y, (int)size.z);

                blockDictionary.Add(new BlockConfigData(modId, id, name, hash, type, blockParam, itemId, modelTransform, blockSize));
            }

            return blockDictionary;
        }


        private ModelTransform GetModelTransform(dynamic blockDynamic)
        {
            var modelTransformJson = blockDynamic.modelTransform;

            var modelTransform = new ModelTransform();
            if (modelTransformJson != null)
            {
                var posVector3 = new Vector3();
                var rotVector3 = new Vector3();
                var scaleVector3 = new Vector3();
                if (modelTransformJson.position != null)
                {
                    var pos = modelTransformJson.position;
                    posVector3 = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);
                }


                if (modelTransformJson.rotation != null)
                {
                    var rot = modelTransformJson.rotation;
                    rotVector3 = new Vector3((float)rot.x, (float)rot.y, (float)rot.z);
                }

                if (modelTransformJson.scale != null)
                {
                    var scale = modelTransformJson.scale;
                    scaleVector3 = new Vector3((float)scale.x, (float)scale.y, (float)scale.z);
                }

                modelTransform = new ModelTransform(posVector3, rotVector3, scaleVector3);
            }


            return modelTransform;
        }
    }
}