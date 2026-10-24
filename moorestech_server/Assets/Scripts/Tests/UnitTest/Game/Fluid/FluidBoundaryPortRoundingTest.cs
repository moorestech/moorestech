using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface.Component;
using Game.Fluid;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.Fluid
{
    /// <summary>
    ///     パイプ→機械の境界で、機械が受け入れた double をノードから引く単位へ戻すときの丸めを確認する(ADR 0090)。
    ///     ノードが引く量は機械が得た量を切り上げた単位で、試みた量を超えない。1 単位が 10^-9 なので、機械側の double 誤差が単位を超えると数単位ずれ得る(ADR 0090 追記)。ここでは 2 の冪の端数で誤差を避けて切り上げだけを見る。
    ///     Verifies the rounding at the pipe→machine boundary when the machine's accepted double is converted back to the units the node loses (ADR 0090):
    ///     the node loses the machine's gain rounded up to units and never more than attempted. With 10^-9 units the machine's double noise can shift this by a few units (ADR 0090 addendum); power-of-two fractions keep this test on the ceil alone.
    /// </summary>
    public class FluidBoundaryPortRoundingTest
    {
        private static readonly FluidId Water = new(1);

        [Test]
        public void 機械が端数を受け入れたらノードは切り上げた単位を失うTest()
        {
            // 機械は 0.3125 + 2^-40 だけ受け入れる。ノードは 312,500,001 単位を失い、機械が得た量より多い
            // The machine accepts only 0.3125 + 2^-40; the node loses 312,500,001 units, more than the machine gained
            var limit = 0.3125 + System.Math.Pow(2, -40);
            var inventory = new PartialInventory(limit);
            var port = new FluidBoundaryPort(0, inventory, default, 5 * FluidUnits.UnitsPerAmount, Vector3Int.zero);

            var accepted = port.Deliver(FluidUnits.UnitsPerAmount, Water);

            Assert.AreEqual(312_500_001, accepted);
            Assert.AreEqual(limit, inventory.Received, 1e-15);
        }

        [Test]
        public void 全部受け入れたら試みた量と同じ単位を失い全部拒否したら0Test()
        {
            var all = new PartialInventory(double.MaxValue);
            Assert.AreEqual(FluidUnits.UnitsPerAmount, new FluidBoundaryPort(0, all, default, 5 * FluidUnits.UnitsPerAmount, Vector3Int.zero).Deliver(FluidUnits.UnitsPerAmount, Water));

            var none = new PartialInventory(0);
            Assert.AreEqual(0, new FluidBoundaryPort(0, none, default, 5 * FluidUnits.UnitsPerAmount, Vector3Int.zero).Deliver(FluidUnits.UnitsPerAmount, Water));
        }

        // 受け入れ上限までだけ受け取る機械の代役
        // A stand-in machine that accepts only up to its limit
        private sealed class PartialInventory : IFluidInventory
        {
            private readonly double _acceptLimit;
            public double Received { get; private set; }
            public bool IsDestroy { get; private set; }

            public PartialInventory(double acceptLimit)
            {
                _acceptLimit = acceptLimit;
            }

            public List<FluidStack> GetFluidInventory()
            {
                return new List<FluidStack>();
            }

            public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
            {
                var accepted = System.Math.Min(fluidStack.Amount, _acceptLimit);
                Received += accepted;
                return new FluidStack(fluidStack.Amount - accepted, fluidStack.FluidId);
            }

            public void Destroy()
            {
                IsDestroy = true;
            }
        }
    }
}
