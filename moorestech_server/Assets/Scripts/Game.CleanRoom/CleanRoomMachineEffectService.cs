using System.Collections.Generic;
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

                // 部屋未検出なら未密閉として最悪効果を押し込み停止させる
                // Without a detected room, push the worst effect as not enclosed so the machine halts
                if (!tryGetRoom(entry.Block, out var room))
                {
                    entry.Machine.SetCleanRoomEffect(new CleanRoomEffect(CleanRoomOperability.NotEnclosed, 0, 0));
                    continue;
                }

                // 部屋はあるがOut行なら、クラス不足として停止させる
                // A room on the Out row halts the machine for an insufficient class
                if (!room.TryGetClassRow(out var row))
                {
                    entry.Machine.SetCleanRoomEffect(new CleanRoomEffect(CleanRoomOperability.ClassOut, 0, 0));
                    continue;
                }

                // 非Out行のマスタ効果だけを稼働許可として機械へ渡す
                // Only non-Out threshold master effects grant machine operation
                entry.Machine.SetCleanRoomEffect(new CleanRoomEffect(CleanRoomOperability.Operable, row.MaxChipLevel, row.DownBinRate));
            }
        }
    }
}
