using Client.Game.Common;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObject近傍生成だけを初期化待機境界へ公開する
    ///     Exposes only near-field map-object instantiation to the startup wait boundary
    /// </summary>
    public sealed class MapObjectNearFieldWaitTarget : IInitialEventApplyWaitTarget
    {
        private readonly MapObjectGameObjectDatastore _datastore;

        public MapObjectNearFieldWaitTarget(MapObjectGameObjectDatastore datastore)
        {
            _datastore = datastore;
        }

        public UniTask WaitForInitialApplyAsync()
        {
            return _datastore.WaitForNearFieldInstantiationAsync();
        }
    }
}
