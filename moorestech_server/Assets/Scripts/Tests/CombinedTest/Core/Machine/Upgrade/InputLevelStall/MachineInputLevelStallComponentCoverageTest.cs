using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine.Upgrade.InputLevelStall
{
    /// <summary>
    ///     IMachineParam を持つ全ブロックが止まった理由の部品を持つことを確かめる（クライアントは IMachineParam なら問い合わせるため）
    ///     Verifies every block whose param is IMachineParam carries the stall component, since the client queries any IMachineParam block
    /// </summary>
    public class MachineInputLevelStallComponentCoverageTest
    {
        // 大きな機械どうしが重ならない設置間隔
        // Spacing so that large machines never overlap
        private const int PlacementSpacing = 32;

        [Test]
        public void EveryMachineParamBlockHasStallComponentTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // テストマスタの IMachineParam のブロックを1つずつ置き、部品の有無を確かめる
            // Place each IMachineParam block of the test master and check it carries the component
            var placedBlockTypes = new HashSet<string>();
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                if (master.BlockParam is not IMachineParam) continue;
                var position = new Vector3Int(blockId.AsPrimitive() * PlacementSpacing, 0, 0);
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block), $"failed to place {master.Name}");
                Assert.IsTrue(block.TryGetComponent<MachineInputLevelStallComponent>(out _), $"{master.Name} ({master.BlockType}) has no MachineInputLevelStallComponent");
                placedBlockTypes.Add(master.BlockType);
            }

            // 3種の機械がそろっていることで、検査が空振りでないことを確かめる
            // All three machine kinds being present proves the check did not pass vacuously
            CollectionAssert.IsSupersetOf(placedBlockTypes, new[] { "ElectricMachine", "GearMachine", "CleanRoomMachine" });
        }
    }
}
