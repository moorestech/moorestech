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
        private readonly HashSet<object> _unrecordableKeys = new();
        public IReadOnlyList<IRemovedObject> Objects => _objects;
        public IReadOnlyList<UnrecordableBlock> GetUnrecordableBlocks()
        {
            return _unrecordableBlocks;
        }
        public int UnrecordableCount => _unrecordableKeys.Count;

        public void Add(IRemovedObject removed)
        {
            _objects.Add(removed);
        }

        // 復元先を持てない物。Undo時にプレイヤーへ件数で知らせる。両端から採取された同じ線は論理キーで1件にまとめる
        // Something with no restore target, reported by count on undo; the same line captured from both ends collapses to one by its logical key
        public void AddUnrecordable(object restoreKey, string reason)
        {
            if (!_unrecordableKeys.Add(restoreKey)) return;
            Debug.LogWarning($"[RemovalRestore] unrecordable: {reason}");
        }

        public void AddUnrecordableBlock(Vector3Int position, BlockDirection direction, BlockId blockId, string reason)
        {
            foreach (var recorded in _unrecordableBlocks)
            {
                if (recorded.Position == position) return;
            }
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
