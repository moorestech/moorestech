using Constant;
using MainGame.Network.Event;
using MainGame.UnityView.WorldMapTile;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Block
{
    public class WorldMapTilePresenter : IInitializable
    {
        private readonly WorldMapTileGameObjectDataStore _worldMapTileGameObjectDataStore;

        public WorldMapTilePresenter(ReceiveChunkDataEvent receiveChunkDataEvent, WorldMapTileGameObjectDataStore worldMapTileGameObjectDataStore)
        {
            _worldMapTileGameObjectDataStore = worldMapTileGameObjectDataStore;
            receiveChunkDataEvent.OnChunkUpdateEvent += OnChunkUpdate;
        }

        public void Initialize()
        {
        }


        private void OnChunkUpdate(ChunkUpdateEventProperties properties)
        {
            var chunkPos = properties.ChunkPos;

            for (var i = 0; i < ChunkConstant.ChunkSize; i++)
            for (var j = 0; j < ChunkConstant.ChunkSize; j++)
            {
                var pos = chunkPos + new Vector2Int(i, j);
                var id = properties.MapTileIds[i, j];
                _worldMapTileGameObjectDataStore.GameObjectBlockPlace(pos, id);
            }
        }
    }
}