using System.Collections.Generic;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Train.Unit;

namespace Client.Tests.Common
{
    /// <summary>
    ///     本番はsnapshotからfactory経由でしか登録しないため、テストで組んだ車両viewを直接載せる登録簿
    ///     Production registers only from snapshots via the factory, so this registry takes hand-built car views directly
    /// </summary>
    public class TrainCarViewRegistryFake : ITrainCarViewRegistry
    {
        private readonly Dictionary<TrainCarInstanceId, TrainCarEntityObject> _entities = new();

        // 同じIDへ別のviewを載せれば、再同期による作り直しを再現できる
        // Putting another view under the same ID reproduces a rebuild by resync
        public void Register(TrainCarEntityObject trainCarEntityObject)
        {
            _entities[trainCarEntityObject.TrainCarInstanceId] = trainCarEntityObject;
        }

        public void Register(TrainCarInstanceId trainCarInstanceId, TrainCarEntityObject trainCarEntityObject)
        {
            _entities[trainCarInstanceId] = trainCarEntityObject;
        }

        public void Unregister(TrainCarInstanceId trainCarInstanceId)
        {
            _entities.Remove(trainCarInstanceId);
        }

        public bool TryGetEntity(TrainCarInstanceId id, out TrainCarEntityObject entity)
        {
            if (!_entities.TryGetValue(id, out entity))
            {
                entity = null;
                return false;
            }

            return entity != null;
        }
    }
}
