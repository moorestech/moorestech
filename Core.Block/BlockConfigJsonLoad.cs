using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace Core.Block
{
    public class BlockConfigJsonLoad
    {
        string _jsonPath;
        public BlockConfigJsonLoad(string jsonPath)
        {
            _jsonPath = jsonPath;
        }

        public Dictionary<int, BlockConfigData> LoadJson()
        {
            //JSONファイルを読み込む
            var json = File.ReadAllText(_jsonPath);
            //JSONを動的にデシリアライズする
            dynamic person = JObject.Parse(json);
            
            var blockDictionary = new Dictionary<int, BlockConfigData>();
            var random = new Random();
            foreach (var block in person.Blocks)
            {
                int id = block.id;
                string name = block.name;
                string type = block.type;
                BlockConfigParamBase blockParam = null;
                //TODO switch caseをやめる
                switch (type)
                {
                    case "Machine":
                        blockParam = new MachineBlockConfigParam(block.param.inputSlot,block.param.outputSlot);
                        break;
                    default:
                        throw new System.NotImplementedException();
                        break;
                }
                blockDictionary.Add(id,new BlockConfigData(id,name,type,blockParam));
            }

            return blockDictionary;
        }
    }
}