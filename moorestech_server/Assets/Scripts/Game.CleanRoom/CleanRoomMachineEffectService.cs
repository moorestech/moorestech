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
                if (!room.TryGetClassRow(out _))
                {
                    entry.Machine.SetCleanRoomEffect(CleanRoomEffect.ClassOut());
                    continue;
                }

                // 非Out行の歩留まり値を閾値行の添字つきで機械へ渡す
                // Pass non-Out yield values to the machine with their threshold row index
                entry.Machine.SetCleanRoomEffect(CleanRoomEffect.Operable(CleanRoomClassYield.FromThresholdIndex(room.ThresholdIndex)));
            }
        }
    }
}
