using System;
using System.Collections.Generic;
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
        [Test]
        public void 衛星1機の成果が空の出口に入らなければ起動時に止まる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // レベル1のスタック上限より1つ多い成果は2スロット要り、出口1スロットに入らない
            // One more than the level-1 stack limit needs two slots and cannot fit the single outlet
            var element = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.TestElevatorReward);
            var stackAtLevelOne = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid).StackCounts[0];
            var oversized = new List<IItemStack>
            {
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, stackAtLevelOne),
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 1),
            };

            LogAssert.Expect(LogType.Error, new Regex("outputSlotCount 1 に入りません"));
            Assert.Throws<InvalidOperationException>(() => new SpaceElevatorOrbitalTableCheck(new FixedOrbitalArrivalService(true, oversized)).Load());
        }

        [Test]
        public void 積荷が軌道到着の表に無ければ起動時に止まる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            LogAssert.Expect(LogType.Error, new Regex("軌道到着の表"));
            Assert.Throws<InvalidOperationException>(() => new SpaceElevatorOrbitalTableCheck(new FixedOrbitalArrivalService(false, new List<IItemStack>())).Load());
        }

        // 表の判定と成果の問い合わせだけを固定で返す偽の到着サービス
        // A fake arrival service returning fixed answers for the table check and the result preview
        private class FixedOrbitalArrivalService : IOrbitalArrivalService
        {
            private readonly bool _isOrbitalPayload;
            private readonly IReadOnlyList<IItemStack> _preview;

            public FixedOrbitalArrivalService(bool isOrbitalPayload, IReadOnlyList<IItemStack> preview)
            {
                _isOrbitalPayload = isOrbitalPayload;
                _preview = preview;
            }

            public IObservable<OrbitalArrival> OnOrbitalArrival => Observable.Empty<OrbitalArrival>();
            public bool IsOrbitalPayload(ItemId itemId) { return _isOrbitalPayload; }
            public IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount) { return _preview; }
            public IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount) { throw new NotSupportedException("検査は到着させない"); }
        }
    }
}
