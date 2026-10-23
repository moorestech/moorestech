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
    ///     ノードが引く量は機械が得た量以上(切り上げ)で、試みた量を超えず、増える側へは丸めない。
    ///     Verifies the rounding at the pipe→machine boundary when the machine's accepted double is converted back to the units the node loses (ADR 0090):
    ///     the node loses at least what the machine gained (ceil), never more than attempted, and never rounds toward creating fluid.
    /// </summary>
    public class FluidBoundaryPortRoundingTest
    {
        private static readonly FluidId Water = new(1);

        [Test]
        public void 機械が端数を受け入れたらノードは切り上げた単位を失うTest()
        {
            // 機械は 0.3335 だけ受け入れる。ノードは 334 単位(0.334)を失い、機械が得た 0.3335 より多い
            // The machine accepts only 0.3335; the node loses 334 units (0.334), more than the 0.3335 the machine gained
            var inventory = new PartialInventory(0.3335);
            var port = new FluidBoundaryPort(0, inventory, default, 5000, Vector3Int.zero);

            var accepted = port.Deliver(1000, Water);

            Assert.AreEqual(334, accepted);
            Assert.AreEqual(0.3335, inventory.Received, 1e-12);
        }

        [Test]
        public void 全部受け入れたら試みた量と同じ単位を失い全部拒否したら0Test()
        {
            var all = new PartialInventory(double.MaxValue);
            Assert.AreEqual(1000, new FluidBoundaryPort(0, all, default, 5000, Vector3Int.zero).Deliver(1000, Water));

            var none = new PartialInventory(0);
            Assert.AreEqual(0, new FluidBoundaryPort(0, none, default, 5000, Vector3Int.zero).Deliver(1000, Water));
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
