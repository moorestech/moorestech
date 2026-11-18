using System;
using Client.Game.InGame.FluidNetwork;
using Client.Ui.State.BlockInventory.Fluid;
using Core.Master;
using Game.Fluid.Sync.State;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 流体の複製の節を 10^-9 単位から量・容量へ換算して読む本物の読み手を確かめる（L0）
    /// Verifies the production reader that converts replica nodes from 10^-9 units into amount and capacity (L0)
    /// </summary>
    public class FluidNetworkReplicaReaderTest
    {
        private static readonly Guid WaterGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 複製の節を量と容量へ換算して読み届く前は読めない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var cache = new FluidNetworkClientCache();
            var reader = new FluidNetworkReplicaReader(cache);

            // 複製が届く前はパイプの節が無い
            // Before the replica arrives there is no pipe node
            Assert.IsFalse(reader.TryReadNode(Vector3Int.zero, out _, out _, out _));

            // 複製の量は 10 億分の 1 単位なので、容量 100・量 40 は 100 × 10^9 / 40 × 10^9
            // Replica amounts are 10^-9 units, so capacity 100 and amount 40 are 100 × 10^9 / 40 × 10^9
            cache.ApplyFullState(new FluidNetworkFullState(new[] { new FluidNodeState(Vector3Int.zero, 100_000_000_000, 40_000_000_000, waterId) }, Array.Empty<FluidFaceState>(), Array.Empty<FluidPortState>()));

            Assert.IsTrue(reader.TryReadNode(Vector3Int.zero, out var fluidId, out var amount, out var capacity));
            Assert.AreEqual(waterId.AsPrimitive(), fluidId);
            Assert.AreEqual(40.0, amount, 0.001);
            Assert.AreEqual(100.0, capacity, 0.001);
        }
    }
}
