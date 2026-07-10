using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom
{
    public delegate bool TryGetCleanRoomDelegate(IBlock block, out CleanRoom room);

    public class CleanRoomMachineEffectService
    {
        private readonly Dictionary<BlockInstanceId, (IBlock Block, ICleanRoomMachine Machine)> _machines = new();

        public void OnBlockPlaced(WorldBlockData blockData)
        {
            if (!blockData.Block.TryGetComponent<ICleanRoomMachine>(out var machine)) return;
            _machines[blockData.Block.BlockInstanceId] = (blockData.Block, machine);
        }

        public void OnBlockRemoved(WorldBlockData blockData)
        {
            _machines.Remove(blockData.Block.BlockInstanceId);
        }

        public void PushEffects(TryGetCleanRoomDelegate tryGetRoom)
        {
            foreach (var entry in _machines.Values)
            {
                if (entry.Machine.IsDestroy) continue;

                // 部屋未検出またはOut行なら、機械へ最悪効果を押し込んで停止させる
                // If no room is found or the room is Out, push the worst effect so the machine halts
                if (!tryGetRoom(entry.Block, out var room) || MasterHolder.CleanRoomMaster.OutThresholdIndex <= room.ThresholdIndex)
                {
                    entry.Machine.SetCleanRoomEffect(new CleanRoomEffect(false, 0, 0));
                    continue;
                }

                // 非Out行のマスタ効果だけを稼働許可として機械へ渡す
                // Only non-Out threshold master effects grant machine operation
                var row = MasterHolder.CleanRoomMaster.Thresholds[room.ThresholdIndex];
                entry.Machine.SetCleanRoomEffect(new CleanRoomEffect(true, row.MaxChipLevel, row.DownBinRate));
            }
        }
    }
}
