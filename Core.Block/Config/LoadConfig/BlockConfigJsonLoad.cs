using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Data.HashFunction.xxHash;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;
using Core.Block.Config.LoadConfig.Param;
using Core.Const;
using Core.Util;
using Newtonsoft.Json.Linq;

namespace Core.Block.Config.LoadConfig
{
    public class BlockConfigJsonLoad
    {
        readonly string _jsonPath;
        private readonly Dictionary<string, IBlockConfigParamGenerator> _generators;


        public BlockConfigJsonLoad()
        {
            _generators = new VanillaBlockConfigGenerator().Generate();
        }

        public List<BlockConfigData> LoadFromJsons(Dictionary<string,string> blockJsons,List<string> mods)
        {
            var list = new List<BlockConfigData>();
            foreach (var mod in mods)
            {
                if (!blockJsons.TryGetValue(mod,out var json))
                {
                    continue;
                }
                list.AddRange(LoadFormOneJson(json,mod));
            }
            return list;
        }

        private List<BlockConfigData> LoadFormOneJson(string jsonText,string modId)
        {
            //JSONを動的にデシリアライズする
            dynamic person = JObject.Parse(jsonText);

            var blockDictionary = new List<BlockConfigData>();

            
            var xxHash = xxHashFactory.Instance.Create(new xxHashConfig()
            {
                Seed = xxHashConst.DefaultSeed,
                HashSizeInBits = xxHashConst.DefaultSize
            });

            
            //最初に設定されたIDの連番を設定していく
            int id = BlockConst.EmptyBlockId;
            

            foreach (var block in person.Blocks)
            {
                id++;
                
                string name = block.name;
                string type = block.type;
                int itemId = block.itemId;
                IBlockConfigParam blockParam = _generators[type].Generate(block.param);

                ulong hash = BitConverter.ToUInt64(xxHash.ComputeHash(modId + "/" + name).Hash);
                
                var modelTransform = GetModelTransform(block);

                blockDictionary.Add(new BlockConfigData(modId,id, name, hash,type, blockParam,itemId,modelTransform));
            }

            return blockDictionary;
        }


        private ModelTransform GetModelTransform(dynamic blockDynamic)
        {
            var modelTransformJson = blockDynamic.modelTransform;

            var modelTransform = new ModelTransform();
            if (modelTransformJson != null)
            {
                var posVector3 = new CoreVector3();
                var rotVector3 = new CoreVector3();
                var scaleVector3 = new CoreVector3();
                if (modelTransformJson.pos != null)
                {
                    var pos = modelTransformJson.pos;
                    posVector3 = new CoreVector3((float)pos[0], (float)pos[1],(float) pos[2]);
                }
                
                
                if (modelTransformJson.rot != null)
                {
                    var rot = modelTransformJson.rot;
                    rotVector3 = new CoreVector3((float)rot[0],(float) rot[1], (float)rot[2]);
                }
                if (modelTransformJson.scale != null)
                {
                    var scale = modelTransformJson.scale;
                    scaleVector3 = new CoreVector3((float)scale[0], (float)scale[1], (float)scale[2]);
                }
                
                modelTransform = new ModelTransform(posVector3, rotVector3, scaleVector3);
            }


            return modelTransform;
        }
        
    }
}