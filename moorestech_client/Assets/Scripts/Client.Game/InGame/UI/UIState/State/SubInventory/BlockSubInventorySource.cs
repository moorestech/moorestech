using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.Inventory;
using Client.Network.API;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class BlockSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }

        public Vector3Int BlockPosition => _originalPos;

        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly Vector3Int _originalPos;

        public BlockSubInventorySource(BlockGameObjectDataStore blockGameObjectDataStore, Vector3Int originalPos)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _originalPos = originalPos;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateBlockMessage(originalPos);
        }

        public bool TryGetReachTarget(out IInteractable reachTarget)
        {
            if (!TryResolveBlockGameObject(out var blockGameObject))
            {
                reachTarget = null;
                return false;
            }

            reachTarget = blockGameObject.Interactable;
            return reachTarget != null;
        }

        // 表示名を運ばず、表示側が辞書解決できる識別子を渡す。撤去済みなら引けないことを戻り値で表に出す
        // Hands over identity instead of a source name, and reports through the return value that a removed block has none
        public bool TryGetBlockIdentity(out Guid blockGuid, out string blockTypeName)
        {
            if (!TryResolveBlockGameObject(out var blockGameObject))
            {
                blockGuid = Guid.Empty;
                blockTypeName = null;
                return false;
            }

            var masterElement = blockGameObject.BlockMasterElement;
            blockGuid = masterElement.BlockGuid;
            blockTypeName = masterElement.BlockType;
            return true;
        }

        // 「開いた位置の今の表示は何か」を答える唯一の口。撤去済みの墓標はfake-null比較で落とす
        // The single answer to what the opened position's current view is, dropping a stale tombstone by fake-null compare
        private bool TryResolveBlockGameObject(out BlockGameObject blockGameObject)
        {
            if (_blockGameObjectDataStore.TryGetBlockGameObject(_originalPos, out blockGameObject) && blockGameObject != null) return true;

            blockGameObject = null;
            Debug.Log($"BlockSubInventorySource could not resolve the block view at {_originalPos}: it is no longer in the datastore");
            return false;
        }

        public SubInventoryModel CreateModel(InventoryResponse inventoryResponse)
        {
            var model = new SubInventoryModel(new BlockInventorySubInventoryIdentifier(_originalPos));
            if (inventoryResponse.Result != InventoryRequestResult.Success)
            {
                Debug.Log($"ブロックインベントリの取得に失敗しました。結果:{inventoryResponse.Result} 位置:{InventoryIdentifier.BlockPosition.Vector3Int}");
                return model;
            }

            model.SetItems(inventoryResponse.Items);
            return model;
        }
    }

}
