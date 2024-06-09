using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    public class ItemShooterTest
    {
        
        [Test]
        public void ShooterTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var world = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            //blockFactory.Create()
            
            // 上方向は速度が低下し、ゼロになると止まる
            
            
            // 下方向に加速する
            // 平行だと速度が低下する
            
            // デフォルトでインサートされる速度
            
        }
    }
}