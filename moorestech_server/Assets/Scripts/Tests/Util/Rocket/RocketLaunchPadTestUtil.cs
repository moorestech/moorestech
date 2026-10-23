using System.Linq;
using MessagePack;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Tests.Module.TestMod;
using Tests.Util.EnergySystem;
using UnityEngine;

namespace Tests.Util.Rocket
{
    // 発射台のテストで使う設置・搬入・tick送り
    // Placement, loading and tick helpers shared by launch pad tests
    public static class RocketLaunchPadTestUtil
    {
        // 本番の状態配信と同じ観測口で検証する
        // Observe through the same state contract used in production
        public static RocketLaunchPadStateDetail ReadState(IBlockStateObservable processor)
        {
            var detail = processor.GetBlockStateDetails().Single(value => value.Key == RocketLaunchPadStateDetail.BlockStateDetailKey);
            return MessagePackSerializer.Deserialize<RocketLaunchPadStateDetail>(detail.Value);
        }

        public static IBlock PlacePoweredPad(Vector3Int position, float generatePower)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.RocketLaunchPadId, position, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var pad);
            ElectricWireTestUtil.WirePower(position, position + new Vector3Int(3, 0, 0), generatePower);
            return pad;
        }

        public static void FillMaterials(IBlock pad)
        {
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.InsertItem(ForUnitTestItemId.RocketCaseId, 2);
            inventory.InsertItem(ForUnitTestItemId.RocketFuelId, 3);
            inventory.InsertItem(ForUnitTestItemId.RocketComputerId, 1);
        }

        public static void FillOneLaunch(IBlock pad, ItemId payloadItemId, int payloadCount)
        {
            FillMaterials(pad);
            pad.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(payloadItemId, payloadCount);
        }

        // 出力口を計算データで埋める。出力口は取り出し専用なので、置き場の制限を通らない SetItem で書く
        // Fill the output slots with compute data; they are take-out only, so write with SetItem which skips the placement check
        public static void FillOutputSlots(IBlock pad, int countPerSlot)
        {
            var layout = new RocketLaunchPadSlotLayout((RocketLaunchPadBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId).BlockParam);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            foreach (var slot in layout.OutputSlots) inventory.SetItem(slot, ForUnitTestItemId.ComputeDataId, countPerSlot);
        }

        // 指定の段階になるまで tick を回し、なったかを返す
        // Run ticks until the given phase and report whether it was reached
        public static bool RunUntilPhase(IBlock pad, RocketLaunchPadPhase until, int maxTicks)
        {
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            for (var i = 0; i < maxTicks; i++)
            {
                if (RocketLaunchPadTestUtil.ReadState(processor).Phase == until) return true;
                GameUpdater.RunFrames(1);
            }
            return RocketLaunchPadTestUtil.ReadState(processor).Phase == until;
        }
    }
}
