using System;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Block;

namespace Tests.UnitTest.Game.Block
{
    public class BlockRemoverTest
    {
        [Test]
        public void Remove_RemovesBlockFromDatastore()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var datastore = ServerContext.WorldBlockDatastore;
            // Place a block
            var blockId = ForUnitTestModBlockId.MachineId; 
            var pos = new Vector3Int(10, 0, 10);
            datastore.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            
            Assert.IsTrue(datastore.Exists(pos));
            
            // Create BlockRemover
            IBlockRemover remover = new BlockRemover(datastore);
            
            // Act
            remover.Remove(block.BlockInstanceId, BlockRemoveReason.Broken);
            
            // Assert
            Assert.IsFalse(datastore.Exists(pos));
        }
    }
}

