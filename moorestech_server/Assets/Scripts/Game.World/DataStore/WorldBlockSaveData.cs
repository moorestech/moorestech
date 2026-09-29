using System.Collections.Generic;
using Game.Block.Interface;
using Game.World.Interface.DataStore;

namespace Game.World.DataStore
{
    internal static class WorldBlockSaveData
    {
        internal static List<BlockJsonObject> Capture(IReadOnlyDictionary<BlockInstanceId, WorldBlockData> blocks)
        {
            var list = new List<BlockJsonObject>();
            foreach (KeyValuePair<BlockInstanceId, WorldBlockData> block in blocks)
                list.Add(new BlockJsonObject(
                    block.Value.BlockPositionInfo.OriginalPos,
                    block.Value.Block.BlockGuid.ToString(),
                    block.Value.Block.BlockInstanceId.AsPrimitive(),
                    block.Value.Block.GetSaveState(),
                    (int)block.Value.BlockPositionInfo.BlockDirection));

            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            list.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            return list;
        }
    }
}
