using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorOrbitalTableCheckTest
    {
        [TearDown]
        public void RestoreMaster() { new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)); }

        [Test]
        public void 衛星1機の成果が空の出口に入らなければ起動時に止まる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // レベル1のスタック上限より1つ多い成果は2スロット要り、出口1スロットに入らない
            // One more than the level-1 stack limit needs two slots and cannot fit the single outlet
            var element = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.TestElevatorReward);
            var stackAtLevelOne = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid).StackCounts[0];
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var key = new JsonFileName("items");
            var items = JObject.Parse(configs[0].JsonContents[key]);
            var satellite = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.TestElevatorSatellite);
            items["orbitalPayloads"].Single(p => (Guid)p["payloadItemGuid"] == satellite)["arrivalOutputs"][0]["count"] = stackAtLevelOne + 1;
            configs[0].JsonContents[key] = items.ToString();
            MasterHolder.Load(new MasterJsonFileContainer(configs));

            LogAssert.Expect(LogType.Error, new Regex("outputSlotCount 1 に入りません"));
            Assert.Throws<InvalidOperationException>(() => new SpaceElevatorOrbitalTableCheck(new FixedOrbitalArrivalService(true)).Load());
        }

        [Test]
        public void 積荷が軌道到着の表に無ければ起動時に止まる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            LogAssert.Expect(LogType.Error, new Regex("軌道到着の表"));
            Assert.Throws<InvalidOperationException>(() => new SpaceElevatorOrbitalTableCheck(new FixedOrbitalArrivalService(false)).Load());
        }

        // 表の判定と成果の問い合わせだけを固定で返す偽の到着サービス
        // A fake arrival service returning fixed answers for the table check and the result preview
        private class FixedOrbitalArrivalService : IOrbitalArrivalService
        {
            private readonly bool _isOrbitalPayload;

            public FixedOrbitalArrivalService(bool isOrbitalPayload)
            {
                _isOrbitalPayload = isOrbitalPayload;
            }

            public IObservable<OrbitalArrival> OnOrbitalArrival => Observable.Empty<OrbitalArrival>();
            public bool IsOrbitalPayload(ItemId itemId) { return _isOrbitalPayload; }
            public IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount) { throw new AssertionException("起動検査でスタックを生成してはいけません"); }
            public IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount) { throw new NotSupportedException("検査は到着させない"); }
        }
    }
}
