using System;
using System.IO;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class ConnectorSettingsAbsentValidationTest
    {
        [Test]
        // connectorSettingsセクションが丸ごと欠落したマスタでもロードとワイルドカード接続判定が成立する
        // Loading succeeds and wildcard connection judging still holds even with connectorSettings entirely absent
        public void MasterWithoutConnectorSettingsLoadsAndTreatsShapesAsWildcardTest()
        {
            PrepareMasterDependencies();
            var blockMaster = CreateBlockMasterWithoutConnectorSettings();

            blockMaster.Initialize();

            // 形状未設定同士は常にワイルドカードとして接続可能
            // Unset shapes on both sides are always wildcard-connectable
            Assert.IsTrue(blockMaster.CanConnectConnectorShapes(null, null));

            // テーブル欠落時も既知の形状ペアは接続不可のまま(全許容にはならない)
            // Even without a table, known shape pairs remain non-connectable (not silently allow-all)
            var teethShape = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var shaftShape = Guid.Parse("22222222-2222-2222-2222-222222222222");
            Assert.IsFalse(blockMaster.CanConnectConnectorShapes(teethShape, shaftShape));
        }

        private static void PrepareMasterDependencies()
        {
            // ItemMasterなどBlockMaster初期化の依存マスタを既存の有効Modで準備する
            // Prepare dependency masters such as ItemMaster from the existing valid mod
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static BlockMaster CreateBlockMasterWithoutConnectorSettings()
        {
            // blocks.jsonは変更せず、テスト内のJTokenからconnectorSettingsキーだけを除去する
            // Keep blocks.json unchanged and strip only the connectorSettings key from the in-test JToken
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(blocksJsonPath));
            ((JObject)blocksJToken).Remove("connectorSettings");
            return new BlockMaster(blocksJToken);
        }
    }
}
