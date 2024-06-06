using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ItemStackMetaTest
    {
        [Test]
        // メタデータの同一性の評価
        public void MetaDataEqualityTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemsStackFactory = ServerContext.ItemStackFactory;
            
            var meta = new 
        } 
        
        [Test]
        // Addできる、できないの評価
        public void AddTest()
        {
        }
        
        [Test]
        // セーブ、ロードの評価
        public void SaveLoadTest()
        {
        }
        
        //ベルトコンベアのメタ設定
    }
}