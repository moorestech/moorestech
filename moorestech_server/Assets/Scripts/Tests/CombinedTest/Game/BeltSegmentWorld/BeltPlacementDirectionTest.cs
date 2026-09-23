using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    public sealed class BeltPlacementDirectionTest
    {
        [Test]
        public void EveryFamilyVariantAcceptsOnlyFourHorizontalFacings()
        {
            var f = new BeltWorldFixture();
            int x = 0;
            foreach (var family in MasterHolder.BlockMaster.Blocks.BeltConveyorFamilies)
            foreach (var guid in new Guid?[] {family.StraightBlockGuid,family.UpBlockGuid,family.DownBlockGuid})
            {
                if (!guid.HasValue) continue;
                var id = MasterHolder.BlockMaster.GetBlockId(guid.Value);
                foreach (BlockDirection direction in Enum.GetValues(typeof(BlockDirection)))
                {
                    bool horizontal = direction >= BlockDirection.North && direction <= BlockDirection.West;
                    Assert.AreEqual(horizontal,BeltConveyorPlaceFamilyUtil.IsPlacementDirectionAllowed(id,direction));
                    Assert.AreEqual(horizontal,ServerContext.WorldBlockDatastore.TryAddBlock(id,new Vector3Int(x++ * 4,0,0),direction,Array.Empty<BlockCreateParam>(),out _));
                }
            }
        }
        [Test]
        public void NonBeltKeepsVerticalFacing()
        {
            var f = new BeltWorldFixture();
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId,Vector3Int.zero,BlockDirection.UpEast,Array.Empty<BlockCreateParam>(),out _));
        }
    }
}
