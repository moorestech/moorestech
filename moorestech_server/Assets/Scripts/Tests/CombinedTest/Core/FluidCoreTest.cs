using System;
using Game.Fluid;
using NUnit.Framework;

namespace Tests.CombinedTest.Core
{
    public class FluidCoreTest
    {
        private static readonly Guid TestFluidId = Guid.NewGuid();
        
        [Test]
        public void FluidFillTest()
        {
            var fluidContainer = new FluidContainer(1, TestFluidId);
            var stack = new FluidStack(TestFluidId, 0.5f, FluidMoveDirection.Forward);
            
            fluidContainer.Fill(stack, out FluidStack? remain);
            
            // capacityが1、amountが0.5なので余らない
            Assert.False(remain.HasValue);
        }
        
        [Test]
        public void FluidFillRemainTest()
        
        {
            var fluidContainer = new FluidContainer(1, TestFluidId);
            
            {
                var stack = new FluidStack(TestFluidId, 0.5f, FluidMoveDirection.Forward);
                fluidContainer.Fill(stack, out _);
            }
            
            {
                var stack = new FluidStack(TestFluidId, 0.5f, FluidMoveDirection.Forward);
                fluidContainer.Fill(stack, out FluidStack? remain);
                
                // capacityが1、amountの合計値が1なので余らない
                Assert.False(remain.HasValue);
            }
            
            {
                var stack = new FluidStack(TestFluidId, 0.5f, FluidMoveDirection.Forward);
                fluidContainer.Fill(stack, out FluidStack? remain);
                
                // capacityが1、amountの合計値が1.5なので全て余る
                Assert.True(remain.HasValue);
                
                // 全て余るのでremainのamountも0.5
                Assert.AreEqual(0.5f, remain.Value.Amount);
            }
            
            // containerの値は最終的に1になるはず
            Assert.AreEqual(1f, fluidContainer.TotalAmount);
        }
    }
}