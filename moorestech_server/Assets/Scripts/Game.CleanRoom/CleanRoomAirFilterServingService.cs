using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.CleanRoom.Util;

namespace Game.CleanRoom
{
    /// <summary>
    ///     成立した部屋に属する清浄機の集合を前tickと比べ、出入りした清浄機へだけ在室をプッシュする
    ///     Diffs the purifiers serving detected rooms against the previous tick and pushes membership only to those that changed
    /// </summary>
    /// <remarks>
    ///     部屋は判定段が毎tick導き直し、清浄機ごとの所属変化を知らせるイベントは無い。そのため送る側で前tickの集合と比べ、変化した清浄機にだけ送る。
    ///     Rooms are re-derived by the detection pass every tick with no per-purifier membership event, so the sender diffs against the previous tick's set and pushes only to purifiers whose membership changed.
    /// </remarks>
    public class CleanRoomAirFilterServingService
    {
        // 前tickにtrueを送った清浄機と、今tick部屋に入っている清浄機
        // Purifiers sent true last tick, and purifiers inside a room this tick
        private HashSet<ICleanRoomAirFilter> _previousServingFilters = new();
        private HashSet<ICleanRoomAirFilter> _currentServingFilters = new();

        public void MarkServing(IReadOnlyList<CleanRoomAirFilterEntry> entries)
        {
            foreach (var entry in entries) _currentServingFilters.Add(entry.Filter);
        }

        // 清浄機が保持する在室から前tick集合を作り直す。trueを送った清浄機だけがtrueを持つので、稼働中の世界で呼んでも集合は変わらない
        // Rebuild the previous-tick set from the purifiers' held serving state; only purifiers sent true hold true, so calling this on a live world leaves the set unchanged
        public void SeedPreviousServing(IReadOnlyList<ICleanRoomAirFilter> filters)
        {
            _previousServingFilters.Clear();
            foreach (var filter in filters)
            {
                if (filter.IsServingRoom) _previousServingFilters.Add(filter);
            }
        }

        public void PushChanges()
        {
            // 新たに部屋へ入った清浄機へだけtrueを送る
            // Send true only to purifiers that newly entered a room
            foreach (var filter in _currentServingFilters)
            {
                if (_previousServingFilters.Contains(filter)) continue;
                filter.SetServingRoom(true);
            }

            // 部屋から外れた清浄機へだけfalseを送る。撤去済みは届け先が無いので送らない
            // Send false only to purifiers that left every room; removed ones have no receiver, so skip them
            foreach (var filter in _previousServingFilters)
            {
                if (_currentServingFilters.Contains(filter) || filter.IsDestroy) continue;
                filter.SetServingRoom(false);
            }

            // 今tickの集合を次tickの比較元にする
            // This tick's set becomes the baseline for the next tick
            (_previousServingFilters, _currentServingFilters) = (_currentServingFilters, _previousServingFilters);
            _currentServingFilters.Clear();
        }
    }
}
