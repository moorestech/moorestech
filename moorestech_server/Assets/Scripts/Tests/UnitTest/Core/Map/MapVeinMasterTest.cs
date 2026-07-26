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
                 ""veinParam"":{""itemGuid"":""99999999-9999-9999-9999-999999999999""}}]}");
            var master = new MapVeinMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("invalid ItemGuid"));
        }
    }
}
