using System.Collections.Generic;
using System.IO;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;
using Core.Block.Config.LoadConfig.Param;
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

        public Dictionary<int, BlockConfigData> LoadJsonFromPath(string jsonPath)
        {
            //JSONファイルを読み込む
            var json = File.ReadAllText(jsonPath);
            return LoadJsonFromText(json);
        }

        public Dictionary<int, BlockConfigData> LoadJsonFromText(string jsonText)
        {
            //JSONを動的にデシリアライズする
            dynamic person = JObject.Parse(jsonText);

            var blockDictionary = new Dictionary<int, BlockConfigData>();

            //最初に設定されたIDの連番を設定していく
            //デフォルトはnull blockの次の値
            int id = BlockConst.BlockConst.EmptyBlockId;

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
                blockDictionary.Add(id, new BlockConfigData(id, name, type, blockParam,itemId));
            }

            return blockDictionary;
        }
    }
}