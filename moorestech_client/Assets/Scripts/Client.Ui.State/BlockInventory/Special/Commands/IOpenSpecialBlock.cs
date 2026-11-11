using Game.Block.Interface;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    // 開いたブロックの位置・種別・インスタンス
    // Position, type and instance of the open block
    public readonly struct OpenSpecialBlock
    {
        public Vector3Int Position { get; }
        public string BlockType { get; }
        public BlockInstanceId InstanceId { get; }

        public OpenSpecialBlock(Vector3Int position, string blockType, BlockInstanceId instanceId)
        {
            Position = position;
            BlockType = blockType;
            InstanceId = instanceId;
        }
    }

    // 開いたブロックを読む口（開いていない・識別が未取得なら false）
    // Port that reads the open block; false when nothing is open or its identity is not fetched yet
    public interface IOpenSpecialBlock
    {
        bool TryGet(out OpenSpecialBlock block);
    }
}
