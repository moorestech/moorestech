using System;
using Game.Fluid;
using NUnit.Framework;

namespace Tests.UnitTest.Game.Fluid
{
    /// <summary>
    ///     液体の容器が非有限・負の容量を作る時点で止めることを確かめる
    ///     Checks that a fluid container stops non-finite or negative capacities at construction
    /// </summary>
    public class FluidContainerCapacityTest
    {
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void 非有限や負の容量の容器は作れない(double capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FluidContainer(capacity));
        }
    }
}
