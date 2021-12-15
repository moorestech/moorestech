using System;
using System.Collections.Generic;
using System.IO;
using Core.Block.Config;

namespace Test.TestConfig
{
    public class AllMachineBlockConfig : IBlockConfig
    {
        private readonly Dictionary<int, BlockConfigData> _blockConfigDictionary;

        //TODO 機械レシピ用のテストコンフィグも作る
        public AllMachineBlockConfig()
        {
            _blockConfigDictionary = new BlockConfigJsonLoad(GetConfigPath("All Machine Block Config.json")).LoadJson();
        }

        public BlockConfigData GetBlockConfig(int id)
        {
            return _blockConfigDictionary[id];
        }
        
        
        private static string GetConfigPath(string fileName)
        {
            DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory); 
            DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
            return Path.Combine(diParent.FullName, "Test","TestConfig","Json",fileName);
        }
    }
}