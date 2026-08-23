using System;
using Core.Master;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Map
{
    /// <summary>
    ///     mapVeinsマスタのveinGuid索引・veinType振り分け・foreignKey違反検出を検証するテスト
    ///     Tests verifying mapVeins master's veinGuid lookup, veinType dispatch, and foreignKey violation detection
    /// </summary>
    public class MapVeinMasterTest
    {
        // ForUnitTest map.json に定義済みのテスト用鉱脈GUID
        // Test vein GUIDs defined in ForUnitTest map.json
        private static readonly Guid ItemVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        private static readonly Guid FluidVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000002");
        private static readonly Guid VeinItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid VeinFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [SetUp]
        public void Setup()
        {
            // DIコンテナ生成でMasterHolderをForUnitTest modからロードする
            // Load MasterHolder from ForUnitTest mod via DI container generation
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void veinGuidで正しい要素を引ける()
        {
            var element = MasterHolder.MapVeinMaster.GetElementOrNull(ItemVeinGuid);
            Assert.NotNull(element);
            Assert.AreEqual("test:IronVein", element.VeinName);
            Assert.IsNull(MasterHolder.MapVeinMaster.GetElementOrNull(Guid.NewGuid()));
        }

        [Test]
        public void veinTypeでitemとfluidに振り分けられる()
        {
            // item鉱脈はItemVeinParamでitemGuidを持つ
            // Item vein resolves to ItemVeinParam holding itemGuid
            var itemElement = MasterHolder.MapVeinMaster.GetElementOrNull(ItemVeinGuid);
            var itemParam = itemElement.VeinParam as ItemVeinParam;
            Assert.NotNull(itemParam);
            Assert.AreEqual(VeinItemGuid, itemParam.ItemGuid);

            // fluid鉱脈はFluidVeinParamでfluidGuidを持つ
            // Fluid vein resolves to FluidVeinParam holding fluidGuid
            var fluidElement = MasterHolder.MapVeinMaster.GetElementOrNull(FluidVeinGuid);
            var fluidParam = fluidElement.VeinParam as FluidVeinParam;
            Assert.NotNull(fluidParam);
            Assert.AreEqual(VeinFluidGuid, fluidParam.FluidGuid);
        }

        [Test]
        public void 実在しないitemGuidの鉱脈はバリデーションで失敗する()
        {
            // 存在しないitemGuidを参照するmapVeinsを構築しValidateがfalseを返すことを確認
            // Build mapVeins referencing a non-existent itemGuid and assert Validate returns false
            var json = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000001"",""veinName"":""bad"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""99999999-9999-9999-9999-999999999999""},
                 ""outcropAddressablePath"":""Vanilla/Environment/Vein/Item/VeinPrefab_Stone"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",
                 ""handMiningType"":""none"",""handMiningParam"":null}]}");
            var master = new MapVeinMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("invalid ItemGuid"));
        }

        [Test]
        public void 実在しないtoolItemGuidの手掘り鉱脈はバリデーションで失敗する()
        {
            // 不正ツールGUIDを検出
            // Detect invalid tool GUID
            var json = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000002"",""veinName"":""badTool"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",
                 ""handMiningType"":""minable"",
                 ""handMiningParam"":{""handMiningTools"":[{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":1},{""toolItemGuid"":""99999999-9999-9999-9999-999999999999"",""attackSpeed"":1}],
                 ""minCount"":1,""maxCount"":1}}]}");
            var master = new MapVeinMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("invalid ToolItemGuid"));
        }

        [Test]
        public void fluid鉱脈をminableにするとバリデーションで失敗する()
        {
            // fluid minableを拒否
            // Reject fluid minable
            var json = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000003"",""veinName"":""badFluid"",""veinType"":""fluid"",
                 ""veinParam"":{""fluidGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/WaterVein"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",
                 ""handMiningType"":""minable"",
                 ""handMiningParam"":{""handMiningTools"":[{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":1}],""minCount"":1,""maxCount"":1}}]}");
            Assert.IsFalse(new MapVeinMaster(json).Validate(out var logs));
            Assert.IsTrue(logs.Contains("badFluid"));
        }

        [Test]
        public void handMiningToolsが空の鉱脈はバリデーションで失敗する()
        {
            // 空ツール配列を拒否
            // Reject empty tool array
            var json = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000004"",""veinName"":""noTools"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",
                 ""handMiningType"":""minable"",""handMiningParam"":{""handMiningTools"":[],""minCount"":1,""maxCount"":1}}]}");
            Assert.IsFalse(new MapVeinMaster(json).Validate(out var logs));
            Assert.IsTrue(logs.Contains("noTools"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void attackSpeedが正でない手掘り鉱脈はバリデーションで失敗する(float attackSpeed)
        {
            var json = JToken.Parse($@"{{""mapObjects"":[],""mapVeins"":[
                {{""veinGuid"":""33333333-0000-0000-0000-000000000007"",""veinName"":""badSpeed"",""veinType"":""item"",
                 ""veinParam"":{{""itemGuid"":""00000000-0000-0000-1234-000000000001""}},""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",""handMiningType"":""minable"",
                 ""handMiningParam"":{{""handMiningTools"":[{{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":{attackSpeed}}}],""minCount"":1,""maxCount"":1}}}}]}}");
            Assert.IsFalse(new MapVeinMaster(json).Validate(out var logs));
            Assert.IsTrue(logs.Contains("attackSpeed"));
        }

        [Test]
        public void toolItemGuidが重複する手掘り鉱脈はバリデーションで失敗する()
        {
            var json = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000008"",""veinName"":""duplicateTool"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""00000000-0000-0000-1234-000000000001""},""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",""handMiningType"":""minable"",
                 ""handMiningParam"":{""handMiningTools"":[{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":1},{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":2}],""minCount"":1,""maxCount"":1}}]}" );
            Assert.IsFalse(new MapVeinMaster(json).Validate(out var logs));
            Assert.IsTrue(logs.Contains("duplicate ToolItemGuid"));
        }

        [Test]
        public void minCountが1未満またはmaxCountより大きい鉱脈はバリデーションで失敗する()
        {
            // 個数範囲を個別検証
            // Verify count range separately
            var zeroMinJson = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000005"",""veinName"":""zeroMin"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",
                 ""handMiningType"":""minable"",""handMiningParam"":{""handMiningTools"":[{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":1}],""minCount"":0,""maxCount"":1}}]}");
            Assert.IsFalse(new MapVeinMaster(zeroMinJson).Validate(out var zeroMinLogs));
            Assert.IsTrue(zeroMinLogs.Contains("zeroMin"));

            var reversedCountJson = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000006"",""veinName"":""reversedCount"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",""terrainSurroundEffectType"":""rockNoBareGround"",
                 ""handMiningType"":""minable"",""handMiningParam"":{""handMiningTools"":[{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":1}],""minCount"":3,""maxCount"":1}}]}");
            Assert.IsFalse(new MapVeinMaster(reversedCountJson).Validate(out var reversedCountLogs));
            Assert.IsTrue(reversedCountLogs.Contains("reversedCount"));
        }

        [Test]
        public void minableな鉱脈はHandMiningParamがMinableHandMiningParamとして解決される()
        {
            // IronVein手掘り設定を検証
            // Verify IronVein hand-mining settings
            var element = MasterHolder.MapVeinMaster.GetElementOrNull(ItemVeinGuid);
            var handMiningParam = element.HandMiningParam as MinableHandMiningParam;
            Assert.NotNull(handMiningParam);
            Assert.AreEqual(1, handMiningParam.HandMiningTools.Length);
            Assert.AreEqual(ToolItemGuid, handMiningParam.HandMiningTools[0].ToolItemGuid);
        }
    }
}
