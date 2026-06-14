using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using System;

namespace Tests.UnitTest.Game
{
    public class MultiSizeBlockTest
    {
        public static readonly BlockId Block_1x4_Id = (BlockId)9;
        public static readonly BlockId Block_3x2_Id = (BlockId)10;
        public static readonly BlockId Block_1x2x3_Id = (BlockId)11;
        
        private IWorldBlockDatastore worldDatastore;
        
        [Test]
        public void BlockPlaceAndGetTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            worldDatastore = ServerContext.WorldBlockDatastore;
            
            //平面設置の検証
            worldDatastore.TryAddBlock(Block_1x4_Id, new Vector3Int(10, 0, 10), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            RetrieveBlock(Block_1x4_Id, new Vector3Int(10, 0, 10));
            RetrieveBlock(Block_1x4_Id, new Vector3Int(10, 0, 13));
            RetrieveNonExistentBlock(new Vector3Int(9, 0, 10));
            RetrieveNonExistentBlock(new Vector3Int(10, 0, 15));
            
            worldDatastore.TryAddBlock(Block_1x4_Id, new Vector3Int(10, 0, -10), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
            RetrieveBlock(Block_1x4_Id, new Vector3Int(10, 0, -10));
            RetrieveBlock(Block_1x4_Id, new Vector3Int(13, 0, -10));
            RetrieveNonExistentBlock(new Vector3Int(9, 0, -10));
            RetrieveNonExistentBlock(new Vector3Int(15, 0, -10));
            
            worldDatastore.TryAddBlock(Block_3x2_Id, new Vector3Int(-10, 0, -10), BlockDirection.South, Array.Empty<BlockCreateParam>(), out _);
            RetrieveBlock(Block_3x2_Id, new Vector3Int(-10, 0, -10));
            RetrieveBlock(Block_3x2_Id, new Vector3Int(-8, 0, -9));
            RetrieveNonExistentBlock(new Vector3Int(-10, 0, -11));
            RetrieveNonExistentBlock(new Vector3Int(-7, 0, -9));
            
            //立体設置の検証
            worldDatastore.TryAddBlock(Block_1x2x3_Id, new Vector3Int(20, 0, 20), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(20, 0, 20));
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(20, 1, 22));
            RetrieveNonExistentBlock(new Vector3Int(21, 1, 22));
            RetrieveNonExistentBlock(new Vector3Int(20, 2, 22));
            RetrieveNonExistentBlock(new Vector3Int(20, 1, 23));
            
            worldDatastore.TryAddBlock(Block_1x2x3_Id, new Vector3Int(40, 0, 40), BlockDirection.UpEast, Array.Empty<BlockCreateParam>(), out _);
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(40, 0, 40));
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(41, 2, 40));
            RetrieveNonExistentBlock(new Vector3Int(42, 2, 40));
            RetrieveNonExistentBlock(new Vector3Int(41, 3, 40));
            RetrieveNonExistentBlock(new Vector3Int(41, 2, 41));
        }
        
        
        [Test]
        public void OverlappingBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            worldDatastore = ServerContext.WorldBlockDatastore;
            
            worldDatastore.TryAddBlock(Block_1x4_Id, new Vector3Int(10, 0, 10), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var placed = worldDatastore.TryAddBlock(Block_3x2_Id, new Vector3Int(10, 0, 12), BlockDirection.South, Array.Empty<BlockCreateParam>(), out _);

            //重なる設置は拒否される
            //Overlapping placement must be rejected
            Assert.IsFalse(placed);
            //3x2が設置されてないことをチェックする
            RetrieveNonExistentBlock(new Vector3Int(11, 0, 12));
            //元の1x4はそのまま残っていることをチェックする
            RetrieveBlock(Block_1x4_Id, new Vector3Int(10, 0, 12));
        }

        [Test]
        public void OverlappingFootprintWithFreeOriginTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            worldDatastore = ServerContext.WorldBlockDatastore;

            //1x4をZ方向に設置（占有範囲 (0,0,0)〜(0,0,3)）
            //Place a 1x4 block occupying (0,0,0)..(0,0,3)
            worldDatastore.TryAddBlock(Block_1x4_Id, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            //原点(-2,0,3)は空きだが占有範囲が(0,0,3)で1x4と重なる3x2を設置する
            //Place a 3x2 whose origin cell is free but whose footprint overlaps the 1x4 at (0,0,3)
            var placed = worldDatastore.TryAddBlock(Block_3x2_Id, new Vector3Int(-2, 0, 3), BlockDirection.South, Array.Empty<BlockCreateParam>(), out _);

            //占有範囲が重なるため設置は拒否される
            //Placement must be rejected because the footprint overlaps
            Assert.IsFalse(placed);
            RetrieveNonExistentBlock(new Vector3Int(-2, 0, 3));
            //元の1x4はそのまま残っていることをチェックする
            RetrieveBlock(Block_1x4_Id, new Vector3Int(0, 0, 3));
        }
        
        [Test]
        public void BoundaryBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            worldDatastore = ServerContext.WorldBlockDatastore;
            
            worldDatastore.TryAddBlock(Block_1x4_Id, new Vector3Int(10, 0, 10), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldDatastore.TryAddBlock(Block_1x4_Id, new Vector3Int(11, 0, 11), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
            
            //1x4が設置されてないことをチェックする
            RetrieveBlock(Block_1x4_Id, new Vector3Int(11, 0, 11));
        }
        
        /// <summary>
        ///     ブロックが設置されていることを確認する
        /// </summary>
        private void RetrieveBlock(BlockId expectedBlockId, Vector3Int position)
        {
            var block = worldDatastore.GetBlock(position);
            Assert.IsNotNull(block);
            Assert.AreEqual(expectedBlockId, block.BlockId);
        }
        
        /// <summary>
        ///     ブロックが設置されていないことを確認する
        /// </summary>
        private void RetrieveNonExistentBlock(Vector3Int position)
        {
            Assert.False(worldDatastore.Exists(position));
        }
    }
}
