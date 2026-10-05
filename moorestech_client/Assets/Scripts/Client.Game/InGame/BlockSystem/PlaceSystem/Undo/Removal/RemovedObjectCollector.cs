using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去直前に各削除対象から撤去物を集める。記録できなかった物は理由をログへ出して件数だけ数える
    ///     Collects removed objects from each delete target right before removal; unrecordable things are logged with a reason and only counted
    /// </summary>
    public class RemovedObjectCollector
    {
        private readonly List<IRemovedObject> _objects = new();
        private readonly List<UnrecordableBlock> _unrecordableBlocks = new();
        public IReadOnlyList<IRemovedObject> Objects => _objects;
        public IReadOnlyList<UnrecordableBlock> GetUnrecordableBlocks()
        {
            return _unrecordableBlocks;
        }
        public int UnrecordableCount { get; private set; }

        public void Add(IRemovedObject removed)
        {
            _objects.Add(removed);
        }

        // 復元先を持てない物。Undo時にプレイヤーへ件数で知らせる
        // Something with no restore target; reported to the player by count on undo
        public void AddUnrecordable(string reason)
        {
            Debug.LogWarning($"[RemovalRestore] unrecordable: {reason}");
            UnrecordableCount++;
        }

        public void AddUnrecordable(Vector3Int position, BlockDirection direction, BlockId blockId, string reason)
        {
            Debug.LogWarning($"[RemovalRestore] unrecordable: {reason}");
            _unrecordableBlocks.Add(new UnrecordableBlock(position, direction, blockId));
        }

        public readonly struct UnrecordableBlock
        {
            public readonly Vector3Int Position;
            public readonly BlockDirection Direction;
            public readonly BlockId BlockId;

            public UnrecordableBlock(Vector3Int position, BlockDirection direction, BlockId blockId)
            {
                Position = position;
                Direction = direction;
                BlockId = blockId;
            }
        }
    }
}
