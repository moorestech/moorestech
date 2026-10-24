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

                // 部屋未検出なら未密閉として止める
                // Without a detected room, halt the machine as not enclosed
                if (!tryGetRoom(entry.Block, out var room))
                {
                    entry.Machine.SetCleanRoomEffect(CleanRoomEffect.NotEnclosed());
                    continue;
                }

                // 部屋はあるがOut行なら、クラス不足として止める
                // A room on the Out row halts the machine for an insufficient class
                if (!room.TryGetClassRow(out var row))
                {
                    entry.Machine.SetCleanRoomEffect(CleanRoomEffect.ClassOut());
                    continue;
                }

                // 非Out行のクラス別の歩留まり値だけを稼働許可と一緒に機械へ渡す
                // Only a non-Out row's per-class yield values are handed to the machine along with the permission to operate
                entry.Machine.SetCleanRoomEffect(CleanRoomEffect.Operable(new CleanRoomClassYield(row.QualityMultiplier, row.LayerFailRate, row.DieLossRate)));
            }
        }
    }
}
