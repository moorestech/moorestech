using System;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class ConnectorShapeMasterTest
    {
        private static readonly Guid GearTeethShape = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid GearShaftShape = Guid.Parse("22222222-2222-2222-2222-222222222222");

        [Test]
        // 形状互換表の判定（登録ペア・未登録ペア・ワイルドカード）をテスト
        // Test shape compatibility table (registered pairs, unregistered pairs, wildcard)
        public void CanConnectConnectorShapesTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 登録済みペアは順序に関わらず接続可能
            // Registered pairs connect regardless of argument order
            Assert.IsTrue(MasterHolder.BlockMaster.CanConnectConnectorShapes(GearTeethShape, GearTeethShape));
            Assert.IsTrue(MasterHolder.BlockMaster.CanConnectConnectorShapes(GearShaftShape, GearShaftShape));

            // 未登録ペア（歯×シャフト）は接続不可
            // Unregistered pair (teeth x shaft) cannot connect
            Assert.IsFalse(MasterHolder.BlockMaster.CanConnectConnectorShapes(GearTeethShape, GearShaftShape));
            Assert.IsFalse(MasterHolder.BlockMaster.CanConnectConnectorShapes(GearShaftShape, GearTeethShape));

            // 形状未設定（null）はワイルドカード
            // Unset shape (null) is a wildcard
            Assert.IsTrue(MasterHolder.BlockMaster.CanConnectConnectorShapes(null, GearTeethShape));
            Assert.IsTrue(MasterHolder.BlockMaster.CanConnectConnectorShapes(GearTeethShape, null));
            Assert.IsTrue(MasterHolder.BlockMaster.CanConnectConnectorShapes(null, null));
        }
    }
}
