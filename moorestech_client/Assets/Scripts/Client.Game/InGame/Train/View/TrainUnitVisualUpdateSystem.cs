using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Train.Unit;

namespace Client.Game.InGame.Train.View
{
    public sealed class TrainUnitVisualUpdateSystem
    {
        private readonly TrainUnitClientCache _trainCache;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly Dictionary<TrainUnitInstanceId, TrainUnitVisualUpdater> _updatersByUnit = new();

        public TrainUnitVisualUpdateSystem(TrainUnitClientCache trainCache, TrainCarObjectDatastore trainCarDatastore, RailGraphClientCache railGraphCache)
        {
            // 論理 unit と car object registry を受け取り、描画更新時だけ接続する
            // Receive logical units and the car object registry, then connect them only during visual updates
            _trainCache = trainCache;
            _trainCarDatastore = trainCarDatastore;
            _railGraphCache = railGraphCache;
        }

        public void UpdateAll(double renderTick, double currentSimulationTick)
        {
            foreach (var pair in _trainCache.Units)
            {
                // active な unit には専用 updater を持たせ、履歴を unit 単位で閉じ込める
                // Give each active unit its own updater and keep history scoped to that unit
                var unit = pair.Value;
                var updater = ResolveUpdater(unit.TrainUnitInstanceId);
                updater.Update(unit, renderTick, currentSimulationTick);
            }

            // cache から消えた unit の updater だけを破棄し、car object の削除責務には触れない
            // Dispose only updaters for units removed from the cache and leave car object deletion elsewhere
            RemoveStaleUpdaters();
        }

        private TrainUnitVisualUpdater ResolveUpdater(TrainUnitInstanceId trainUnitInstanceId)
        {
            if (_updatersByUnit.TryGetValue(trainUnitInstanceId, out var updater))
            {
                return updater;
            }

            // 新規 unit はこの時点で描画履歴 container を作る
            // Create the render history container when a new unit becomes visible to the update system
            updater = new TrainUnitVisualUpdater(_trainCarDatastore, _railGraphCache, _railGraphCache);
            _updatersByUnit[trainUnitInstanceId] = updater;
            return updater;
        }

        private void RemoveStaleUpdaters()
        {
            var staleUnitIds = new List<TrainUnitInstanceId>();
            foreach (var pair in _updatersByUnit)
            {
                if (!_trainCache.Units.ContainsKey(pair.Key))
                {
                    staleUnitIds.Add(pair.Key);
                }
            }

            // dictionary を列挙した後で削除し、更新中の collection 変更を避ける
            // Remove after dictionary enumeration to avoid mutating the collection while iterating
            for (var i = 0; i < staleUnitIds.Count; i++)
            {
                _updatersByUnit.Remove(staleUnitIds[i]);
            }
        }
    }
}
