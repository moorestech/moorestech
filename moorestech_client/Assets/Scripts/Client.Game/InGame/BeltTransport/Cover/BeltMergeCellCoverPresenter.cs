using System.Collections.Generic;
using Core.BeltTransport;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マスの直線ベルコンを分岐器モデルで覆う。複製の版が進んだ回に合流マス集合を取り直し、未作成と死んだ覆いは毎回見直す
    // Covers the straight belt on each merge cell with the splitter model; re-reads the merge cells when the replica advances and revisits pending and dead covers every call
    // 呼び出しは購読でなく TrainUnitClientSimulator から毎フレーム明示的に行う(BeltItemObjectPresenter と同じ)
    // Not subscription-driven: TrainUnitClientSimulator calls this every frame explicitly, like BeltItemObjectPresenter
    public sealed class BeltMergeCellCoverPresenter
    {
        private readonly BeltTransportClientCache _cache;
        private readonly IBeltMergeCellCoverFactory _factory;
        private readonly HashSet<Vector3Int> _mergeCells = new();
        private readonly Dictionary<Vector3Int, IBeltMergeCellCover> _covers = new();
        private readonly List<Vector3Int> _work = new();
        private int _shownReplicaVersion;

        public BeltMergeCellCoverPresenter(BeltTransportClientCache cache, IBeltMergeCellCoverFactory factory)
        {
            _cache = cache;
            _factory = factory;
        }

        public void UpdateAll()
        {
            if (_cache.Replica == null) return;
            if (_cache.ReplicaVersion != _shownReplicaVersion)
            {
                _shownReplicaVersion = _cache.ReplicaVersion;
                CollectMergeCells();
                RemoveCoversOutsideMergeCells();
            }
            RemoveDeadCovers();
            CreateMissingCovers();
            return;

            #region Internal

            // 合流segmentは1マスなので先頭マスの位置が合流マス
            // A merge segment is a single cell, so its first cell is the merge cell
            void CollectMergeCells()
            {
                _mergeCells.Clear();
                foreach (var shape in _cache.Replica.Shapes)
                {
                    if (shape.Kind != BeltSegmentKind.Merge || shape.Cells.Length == 0) continue;
                    _mergeCells.Add(shape.Cells[0].Position);
                }
            }

            void RemoveCoversOutsideMergeCells()
            {
                _work.Clear();
                foreach (var position in _covers.Keys)
                {
                    if (!_mergeCells.Contains(position)) _work.Add(position);
                }
                foreach (var position in _work) RemoveCover(position);
            }

            // ベルコンが撤去された覆いは落とす。まだ合流マスなら下の CreateMissingCovers が作り直す
            // Drop covers whose belt was removed; CreateMissingCovers below re-creates them while the cell is still a merge
            void RemoveDeadCovers()
            {
                _work.Clear();
                foreach (var pair in _covers)
                {
                    if (!pair.Value.IsAlive) _work.Add(pair.Key);
                }
                foreach (var position in _work) RemoveCover(position);
            }

            // ブロック設置イベントが複製より遅れた合流マスは、BlockGameObject が現れるまで毎回試す
            // Merge cells whose block placement arrives after the replica are retried every call until the BlockGameObject appears
            void CreateMissingCovers()
            {
                foreach (var position in _mergeCells)
                {
                    if (_covers.ContainsKey(position)) continue;
                    if (_factory.TryCreate(position, out var cover)) _covers.Add(position, cover);
                }
            }

            void RemoveCover(Vector3Int position)
            {
                _covers[position].Remove();
                _covers.Remove(position);
            }

            #endregion
        }
    }
}
