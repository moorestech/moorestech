using System;
using Game.Fluid;
using NUnit.Framework;

namespace Tests.CombinedTest.Core
{
    public class FluidCoreTest
    {
        private const float Delta = 0.0001f;
        private static readonly Guid TestFluidId = Guid.NewGuid();
        
        [Test]
        public void FluidFillTest()
        {
            var fluidContainer = new FluidContainer(1, TestFluidId);
            var stack = new FluidStack(TestFluidId, 0.5f, fluidContainer);
            
            fluidContainer.Fill(stack, out FluidStack? remain);
            
            // capacityが1、amountが0.5なので余らない
            Assert.False(remain.HasValue);
        }
        
        [Test]
        public void FluidFillRemainTest()
        {
            var fluidContainer = new FluidContainer(1, TestFluidId);
            
            {
                var stack = new FluidStack(TestFluidId, 0.5f, fluidContainer);
                fluidContainer.Fill(stack, out _);
            }
            
            {
                var stack = new FluidStack(TestFluidId, 0.5f, fluidContainer);
                fluidContainer.Fill(stack, out FluidStack? remain);
                
                // capacityが1、amountの合計値が1なので余らない
                Assert.False(remain.HasValue);
            }
            
            {
                var stack = new FluidStack(TestFluidId, 0.5f, fluidContainer);
                fluidContainer.Fill(stack, out FluidStack? remain);
                
                // capacityが1、amountの合計値が1.5なので全て余る
                Assert.True(remain.HasValue);
                
                // 全て余るのでremainのamountも0.5
                Assert.AreEqual(0.5f, remain.Value.Amount);
            }
            
            // containerの値は最終的に1になるはず
            Assert.AreEqual(1f, fluidContainer.TotalAmount);
        }
        
        [Test]
        public void FluidDrainTest()
        {
            // 初期化
            var fluidContainer = new FluidContainer(1, TestFluidId);
            {
                var stack = new FluidStack(TestFluidId, 0.5f, FluidContainer.Empty);
                fluidContainer.Fill(stack, out _);
            }
            
            // 現在のamountより少ない量をdrainする
            {
                var stack = fluidContainer.Drain(0.3f, FluidContainer.Empty);
                Assert.AreEqual(0.3f, stack.Amount);
                Assert.AreEqual(0.2f, fluidContainer.TotalAmount, Delta);
            }
            
            // 現在のamountより多い量をdrainする
            {
                var stack = fluidContainer.Drain(0.3f, FluidContainer.Empty);
                Assert.AreEqual(0.2f, stack.Amount, Delta);
                Assert.AreEqual(0f, fluidContainer.TotalAmount);
            }
        }
    }
}