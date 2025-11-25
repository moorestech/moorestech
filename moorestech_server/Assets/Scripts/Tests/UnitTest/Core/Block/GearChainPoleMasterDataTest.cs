using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using Mooresmaster.Model.BlocksModule;

namespace Tests.UnitTest.Core.Block
{
    public class GearChainPoleMasterDataTest
    {
        [SetUp]
        public void SetUp()
        {
            // テスト用のサーバーコンテキストを構築する
            // Build server context for tests
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void GearChainPoleMasterDataIsRegistered()
        {
            // マスターデータからチェーンポールを取得する
            // Fetch chain pole block from master data
            var chainPole = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearChainPole);
            Assert.AreEqual("GearChainPole", chainPole.BlockType);

            // 最大接続距離のパラメーターが設定されていることを確認する
            // Verify max connection distance parameter exists
            var param = chainPole.BlockParam as GearChainPoleBlockParam;
            Assert.NotNull(param);
            Assert.Greater(param.MaxConnectionDistance, 0f);
            Assert.Greater(param.MaxConnectionCount, 1);

            // ギア接続情報が存在することを確認する
            // Confirm gear connection configuration exists
            Assert.IsNotNull(param.Gear);
        }

        [Test]
        public void GearChainPoleProvidesGearTransformerComponent()
        {
            // チェーンポールブロックを生成する
            // Create chain pole block instance
            var block = ServerContext.BlockFactory.Create(ForUnitTestModBlockId.GearChainPole, new BlockInstanceId(12), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));

            // ギアエネルギートランスフォーマーを取得する
            // Retrieve gear energy transformer component
            var transformer = block.GetComponent<IGearEnergyTransformer>();
            Assert.IsNotNull(transformer);

            // 接続が未設定の状態ではコネクトが存在しないことを確認する
            // Ensure no gear connections exist before chain linking
            Assert.IsEmpty(transformer.GetGearConnects());
        }
    }
}
