using MainGame.Network.Event;
using MainGame.UnityView.Chunk;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Block
{
    public class BlockStateChangePresenter : IInitializable
    {
        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;
        private readonly ReceiveBlockStateChangeEvent _receiveBlockStateChangeEvent;


        public BlockStateChangePresenter(ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore, ReceiveBlockStateChangeEvent receiveBlockStateChangeEvent)
        {
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            _receiveBlockStateChangeEvent = receiveBlockStateChangeEvent;
            _receiveBlockStateChangeEvent.OnStateChange += OnStateChange;
        }

        private void OnStateChange(BlockStateChangeProperties stateChangeProperties)
        {
            var pos = stateChangeProperties.Position;
            if (!_chunkBlockGameObjectDataStore.BlockGameObjectDictionary.TryGetValue(pos,out var _))
            {
                Debug.Log("ブロックがない : " + pos);
            }
            else
            {
                var blockObject = _chunkBlockGameObjectDataStore.BlockGameObjectDictionary[pos];
                blockObject.BlockStateChangeProcessor.OnChangeState(stateChangeProperties.CurrentState,stateChangeProperties.PreviousState,stateChangeProperties.CurrentStateData);
            }
        }

        public void Initialize() { }
    }
}