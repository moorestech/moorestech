using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Miner;
using Core.Block.Config;
using Core.Item;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;


using Test.Module.TestMod;

namespace Test.UnitTest.Core.Block
{
    public class MinerSaveLoadTest
    {
        private const int MinerId = UnitTestMod.MinerId;
        
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            ulong minerHash = serviceProvider.GetService<IBlockConfig>().GetBlockConfig(MinerId).BlockHash;
            
            var originalMiner = blockFactory.Create(MinerId, 1);
            int originalRemainingMillSecond = 350;
            
            var outputSlot = 
                (List<IItemStack>)typeof(VanillaMiner).
                    GetField("_outputSlot", BindingFlags.Instance | BindingFlags.NonPublic).
                    GetValue(originalMiner);
            outputSlot[0] = itemStackFactory.Create(1,1);
            outputSlot[2] = itemStackFactory.Create(4,1);
            typeof(VanillaMiner).
                GetField("_remainingMillSecond", BindingFlags.Instance | BindingFlags.NonPublic).
                SetValue(originalMiner,originalRemainingMillSecond);

            
            
            var json = originalMiner.GetSaveState();
            Console.WriteLine(json);
            
            
            var loadedMiner = blockFactory.Load(minerHash, 1,json);
            var loadedOutputSlot = 
                (List<IItemStack>)typeof(VanillaMiner).
                    GetField("_outputSlot", BindingFlags.Instance | BindingFlags.NonPublic).
                    GetValue(loadedMiner);
            var loadedRemainingMillSecond = 
                (int)typeof(VanillaMiner).
                    GetField("_remainingMillSecond", BindingFlags.Instance | BindingFlags.NonPublic).
                    GetValue(loadedMiner);
            
            Assert.AreEqual(outputSlot[0],loadedOutputSlot[0]);
            Assert.AreEqual(outputSlot[1],loadedOutputSlot[1]);
            Assert.AreEqual(outputSlot[2],loadedOutputSlot[2]);
            Assert.AreEqual(originalRemainingMillSecond,loadedRemainingMillSecond);
        }
    }
}