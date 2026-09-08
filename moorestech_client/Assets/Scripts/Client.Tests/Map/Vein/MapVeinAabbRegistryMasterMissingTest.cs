using System.Text.RegularExpressions;
using Client.Game.InGame.Map.MapVein;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Map.Vein.MapVeinAabbRegistryFixture;

namespace Client.Tests.Map.Vein
{
    /// <summary>
    ///     マスタに無い鉱脈をサーバー(FluidMapVeinDatastore)と同じくログ＋スキップで扱うことを固定する
    ///     Pins that veins missing from the master are logged and skipped, exactly as the server's FluidMapVeinDatastore does
    /// </summary>
    public class MapVeinAabbRegistryMasterMissingTest
    {
        private const string ItemVeinGuid = "11111111-0000-0000-0000-000000000001";
        private const string FluidVeinGuid = "11111111-0000-0000-0000-000000000002";
        private const string UnknownVeinGuid = "11111111-0000-0000-0000-0000000000ff";

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void マスタに無いveinGuidの鉱脈だけをスキップし残りは登録する()
        {
            LogAssert.Expect(LogType.Error, new Regex($"veinGuid:{UnknownVeinGuid}"));

            var registry = Create(
                new VeinLayoutMessagePack(UnknownVeinGuid, 0, 0, 0, 1, 0, 1),
                new VeinLayoutMessagePack(ItemVeinGuid, 10, 0, 10, 11, 0, 11),
                new VeinLayoutMessagePack(FluidVeinGuid, 20, 0, 20, 21, 0, 21));

            // 未登録分だけが欠け、他の鉱脈は種別と産出物つきで残る（例外でワールドごと落ちない）
            // Only the unregistered entry is missing; the others survive with their kind and yield (the world never dies on an exception)
            Assert.AreEqual(2, registry.Veins.Count);
            var itemVein = SelectVeinsOfKind(registry, MapVeinKind.Item)[0];
            var fluidVein = SelectVeinsOfKind(registry, MapVeinKind.Fluid)[0];
            Assert.IsNotNull(itemVein.VeinItemId);
            Assert.IsNotNull(fluidVein.VeinFluidId);
            Assert.AreEqual(1, SelectVeinsOfKind(registry, MapVeinKind.Item).Count);
            Assert.AreEqual(1, SelectVeinsOfKind(registry, MapVeinKind.Fluid).Count);
        }
    }
}
