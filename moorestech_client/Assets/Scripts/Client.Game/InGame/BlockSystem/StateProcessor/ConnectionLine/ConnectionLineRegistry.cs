using System;
using System.Collections.Generic;
using Game.Block.Interface;
using UniRx;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     表示中の接続線を両端ブロックから引ける索引。線は小さいId側の子に1本だけ生成されるため、相手側からはここで引く
    ///     Index resolving displayed lines from either endpoint; each line lives once under the smaller-id block, so the other side looks it up here
    /// </summary>
    public class ConnectionLineRegistry
    {
        private readonly Dictionary<BlockInstanceId, List<ConnectionLineDeleteTarget>> _linesByBlock = new();

        // 線の端点ブロックに付く線が増減したとき、そのブロックIdを流す
        // Emits the block id whose attached lines were added or removed
        private readonly Subject<BlockInstanceId> _lineAttachmentChanged = new();
        public IObservable<BlockInstanceId> OnLineAttachmentChanged => _lineAttachmentChanged;

        public void Register(ConnectionLineDeleteTarget line)
        {
            Add(line.FromId);
            Add(line.ToId);
            _lineAttachmentChanged.OnNext(line.FromId);
            _lineAttachmentChanged.OnNext(line.ToId);

            #region Internal

            void Add(BlockInstanceId blockId)
            {
                if (!_linesByBlock.TryGetValue(blockId, out var lines))
                {
                    lines = new List<ConnectionLineDeleteTarget>();
                    _linesByBlock[blockId] = lines;
                }
                lines.Add(line);
            }

            #endregion
        }

        public void Unregister(ConnectionLineDeleteTarget line)
        {
            Remove(line.FromId);
            Remove(line.ToId);
            _lineAttachmentChanged.OnNext(line.FromId);
            _lineAttachmentChanged.OnNext(line.ToId);

            #region Internal

            void Remove(BlockInstanceId blockId)
            {
                if (!_linesByBlock.TryGetValue(blockId, out var lines)) return;
                lines.Remove(line);
                if (lines.Count == 0) _linesByBlock.Remove(blockId);
            }

            #endregion
        }

        public IReadOnlyList<ConnectionLineDeleteTarget> GetLinesAttachedTo(BlockInstanceId blockId)
        {
            return _linesByBlock.TryGetValue(blockId, out var lines) ? lines : Array.Empty<ConnectionLineDeleteTarget>();
        }

        public bool HasLineBetween(BlockInstanceId fromId, BlockInstanceId toId, ConnectionLineKind kind)
        {
            foreach (var line in GetLinesAttachedTo(fromId))
            {
                // 破棄待ちの表示体は現在の接続として扱わない
                // A destroyed view is not a current connection
                if (line == null || line.Kind != kind) continue;
                if (line.FromId.Equals(fromId) && line.ToId.Equals(toId) ||
                    line.FromId.Equals(toId) && line.ToId.Equals(fromId)) return true;
            }
            return false;
        }
    }
}
