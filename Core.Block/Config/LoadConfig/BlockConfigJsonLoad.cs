using System;
using System.Collections.Generic;
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

        public List<BlockConfigData> LoadFromJsons(List<string> jsons)
        {
            return jsons.SelectMany(LoadFormOneJson).ToList();
        }

        public List<BlockConfigData> LoadFormOneJson(string jsonText)
        {
            //JSONを動的にデシリアライズする
            dynamic person = JObject.Parse(jsonText);

            var blockDictionary = new List<BlockConfigData>();

            //最初に設定されたIDの連番を設定していく
            //デフォルトはnull blockの次の値
            int id = BlockConst.EmptyBlockId;

            foreach (var block in person.Blocks)
            {
                //IDがなければ加算
                //IDがあればその値を設定
                if (block.id == null)
                {
                    id++;
                }
                else
                {
                    id = block.id;
                }

                string name = block.name;
                string type = block.type;
                int itemId = block.itemId;
                IBlockConfigParam blockParam = _generators[type].Generate(block.param);
                blockDictionary.Add(new BlockConfigData(id, name, type, blockParam,itemId));
            }

            return blockDictionary;
        }
    }
}