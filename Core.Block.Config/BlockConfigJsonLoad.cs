using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Core.Block.Config
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
            
            //最初に設定されたIDの連番を設定していく
            //デフォルトはnull blockの次の値
            int id = BlockConst.BlockConst.NullBlockId;

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
                BlockConfigParamBase blockParam = null;
                //TODO switch caseをやめる
                switch (type)
                {
                    case "Machine":
                        int inputSlot = block.param.inputSlot;
                        int outputSlot = block.param.outputSlot;
                        blockParam = new MachineBlockConfigParam(inputSlot,outputSlot);
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