using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.World
{
    /// <summary>
    ///     tick末尾の状態の束を座標でブロックへ振り分ける
    ///     Reads the tick-end block-state bundle once and routes each entry to its block by position (ADR 0088)
    /// </summary>
    public class BlockStateEventHandler : IInitializable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        public BlockStateEventHandler(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;

            // WorldDataHandlerと同じくctorで購読する。InitializeDispatchまでの受信はVanillaApiEventがバッファする
            // Subscribe in the ctor like WorldDataHandler; VanillaApiEvent buffers anything received before InitializeDispatch
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.EventTag, OnBlockStatesTickBundle);
        }

        public void Initialize()
        {
        }

        private void OnBlockStatesTickBundle(byte[] payload)
        {
            BlockStatesTickBundleMessagePack bundle;
            using (BlockStateProfilerMarkers.DeserializeBlockState.Auto()) bundle = MessagePackSerializer.Deserialize<BlockStatesTickBundleMessagePack>(payload);

            foreach (var state in bundle.States)
            {
                // 座標にブロックが無い件は読み飛ばす
                // Skip entries whose position has no block, e.g. right after removal
                Vector3Int position = state.Position;
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(position, out var block))
                {
                    Debug.Log($"[BlockStateEventHandler] 座標{position}にブロックが無いため状態を読み飛ばしました");
                    continue;
                }
                block.ApplyBlockState(state);
            }
        }
    }
}
