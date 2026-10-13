using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 新設駅の既定名を、全駅名と重ならない最小の番号で決める（1は番号なし）
    // Decide a new station's default name with the smallest number unused by any station (1 has no number)
    public class TrainStationDefaultNameAllocator
    {
        private readonly TrainStationNameFeatureScanner _scanner;

        public TrainStationDefaultNameAllocator(TrainStationNameFeatureScanner scanner)
        {
            _scanner = scanner;
        }

        internal string Allocate(BlockPositionInfo position)
        {
            var baseName = _scanner.ScanBaseName(position);
            var existingNames = CollectExistingNames();
            if (!existingNames.Contains(baseName)) return baseName;
            var number = 2;
            while (existingNames.Contains($"{baseName} {number}")) number++;
            return $"{baseName} {number}";

            #region Internal

            HashSet<string> CollectExistingNames()
            {
                // 設置中の駅はまだdatastoreに居ないので含まれない
                // The station being placed is not in the datastore yet, so it is excluded
                var names = new HashSet<string>();
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    if (data.Block.TryGetComponent<TrainStationComponent>(out var station)) names.Add(station.StationName);
                }
                return names;
            }

            #endregion
        }
    }
}
