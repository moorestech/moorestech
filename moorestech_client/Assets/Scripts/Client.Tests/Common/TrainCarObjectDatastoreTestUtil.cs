using System.Collections.Generic;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Train.Unit;

namespace Client.Tests.Common
{
    /// <summary>
    ///     本番はsnapshotからfactory経由でしか登録しないため、テストで組んだ車両viewをdatastoreへ直接載せる
    ///     Production registers only from snapshots via the factory, so tests put hand-built car views straight into the datastore
    /// </summary>
    internal static class TrainCarObjectDatastoreTestUtil
    {
        // 車両view自身に付けたdatastoreへ、その車両を自分のIDで登録する
        // Attach a datastore to the car view itself and register the car under its own ID
        public static TrainCarObjectDatastore AttachRegistered(TrainCarEntityObject trainCarEntityObject)
        {
            var datastore = trainCarEntityObject.gameObject.AddComponent<TrainCarObjectDatastore>();
            Register(datastore, trainCarEntityObject.TrainCarInstanceId, trainCarEntityObject);
            return datastore;
        }

        // 同じIDへ別のviewを載せれば、再同期による作り直しを再現できる
        // Putting another view under the same ID reproduces a rebuild by resync
        public static void Register(TrainCarObjectDatastore datastore, TrainCarInstanceId trainCarInstanceId, TrainCarEntityObject trainCarEntityObject)
        {
            var entities = TestReflection.GetField<Dictionary<TrainCarInstanceId, TrainCarEntityObject>>(datastore, "_entities");
            entities[trainCarInstanceId] = trainCarEntityObject;
        }

        // RemoveTrainEntityは描画マテリアルの解放を伴うため、テストでは登録だけを外す
        // RemoveTrainEntity also releases render materials, so tests drop only the registration
        public static void Unregister(TrainCarObjectDatastore datastore, TrainCarInstanceId trainCarInstanceId)
        {
            TestReflection.GetField<Dictionary<TrainCarInstanceId, TrainCarEntityObject>>(datastore, "_entities").Remove(trainCarInstanceId);
        }
    }
}
