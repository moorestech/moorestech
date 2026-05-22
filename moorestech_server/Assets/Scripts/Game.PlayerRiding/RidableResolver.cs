using Game.PlayerRiding.Interface;
using Game.Train.Unit;

namespace Game.PlayerRiding
{
    // IRidableIdentifier から乗り物実体 IRidable を解決する。
    // PlayerRidingDatastore からのみ使われる（仕様書セクション4.0・4.2）。
    // Resolves an IRidable from an IRidableIdentifier. Used only by PlayerRidingDatastore.
    public class RidableResolver
    {
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public RidableResolver(ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
        }

        // 解決できない（乗り物が存在しない）場合は null を返す。
        // Returns null when the ridable does not exist.
        public IRidable Resolve(IRidableIdentifier identifier)
        {
            // RidableType は string ベースの UnitOf 型なので case ラベルにできず if で分岐する。
            // RidableType is a string-based UnitOf type, so dispatch with if instead of a switch case.
            if (identifier.Type == RidableType.TrainCar)
            {
                var trainCarId = new TrainCarInstanceId(((TrainCarRidableIdentifier)identifier).TrainCarInstanceId);
                if (_trainUnitLookupDatastore.TryGetTrainCar(trainCarId, out var trainCar))
                {
                    return trainCar;
                }
                return null;
            }
            return null;
        }
    }
}
