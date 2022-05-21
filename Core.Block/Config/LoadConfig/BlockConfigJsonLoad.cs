using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Data.HashFunction.xxHash;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;
using Core.Block.Config.LoadConfig.Param;
using Core.Const;
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
                var json = blockJsons[mod];
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
                
                blockDictionary.Add(new BlockConfigData(id, name, hash,type, blockParam,itemId));
            }

            return blockDictionary;
        }
    }
}