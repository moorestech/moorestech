using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Context
{
    public class ChainSystem : IChainSystem
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IChainInventoryService _inventoryService;
        private readonly ItemId _chainItemId;

        public ChainSystem(IWorldBlockDatastore worldBlockDatastore, IChainInventoryService inventoryService)
        {
            // 依存関係を初期化する
            // Initialize dependencies
            _worldBlockDatastore = worldBlockDatastore;
            _inventoryService = inventoryService;
            _chainItemId = MasterHolder.ItemMaster.ExistItemId(ChainConstants.ChainItemGuid)
                ? MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid)
                : ItemMaster.EmptyItemId;
        }

        public bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            if (!TryGetGearChainPole(posA, out var poleA, out var transformerA) || !TryGetGearChainPole(posB, out var poleB, out var transformerB))
            {
                error = "InvalidTarget";
                return false;
            }

            // 同一ターゲットや距離超過を弾く
            // Reject same target or over distance
            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                error = "InvalidTarget";
                return false;
            }

            var maxDistance = Math.Min(poleA.MaxConnectionDistance, poleB.MaxConnectionDistance);
            if (Vector3Int.Distance(posA, posB) > maxDistance)
            {
                error = "TooFar";
                return false;
            }

            // 既存接続がある場合は失敗させる
            // Fail when already connected
            if (poleA.HasChainConnection || poleB.HasChainConnection)
            {
                error = "AlreadyConnected";
                return false;
            }

            // チェーンアイテムを消費する
            // Consume chain item
            if (!ConsumeChainItem(playerId))
            {
                error = "NoItem";
                return false;
            }

            // 接続を確定させる
            // Finalize connection
            poleA.SetChainConnection(poleB.BlockInstanceId);
            poleB.SetChainConnection(poleA.BlockInstanceId);
            RebuildNetworks(transformerA, transformerB);
            return true;
        }

        public bool TryDisconnect(Vector3Int posA, Vector3Int posB, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            if (!TryGetGearChainPole(posA, out var poleA, out var transformerA) || !TryGetGearChainPole(posB, out var poleB, out var transformerB))
            {
                error = "InvalidTarget";
                return false;
            }

            // 相互接続でない場合は失敗
            // Fail when not connected to each other
            if (!poleA.HasChainConnection || !poleB.HasChainConnection || poleA.PartnerId != poleB.BlockInstanceId || poleB.PartnerId != poleA.BlockInstanceId)
            {
                error = "NotConnected";
                return false;
            }

            poleA.ClearChainConnection();
            poleB.ClearChainConnection();
            RebuildNetworks(transformerA, transformerB);
            return true;
        }

        private bool TryGetGearChainPole(Vector3Int position, out IGearChainPole chainPole, out IGearEnergyTransformer transformer)
        {
            // 指定座標からコンポーネントを解決する
            // Resolve component from position
            chainPole = null;
            transformer = null;
            if (!_worldBlockDatastore.TryGetBlock(position, out IBlock block)) return false;
            chainPole = block.GetComponent<IGearChainPole>();
            transformer = block.GetComponent<IGearEnergyTransformer>();
            return chainPole != null && transformer != null;
        }

        private bool ConsumeChainItem(int playerId)
        {
            // プレイヤーのメインインベントリから1つ消費する
            // Spend one chain item from player inventory
            if (_chainItemId == ItemMaster.EmptyItemId) return true;
            return _inventoryService.TryConsumeChainItem(playerId, _chainItemId);
        }

        private void RebuildNetworks(params IGearEnergyTransformer[] transformers)
        {
            // ネットワークを再構築して回転を再計算する
            // Rebuild gear networks to recalc rotation
            foreach (var transformer in transformers)
            {
                if (GearNetworkDatastore.Contains(transformer)) GearNetworkDatastore.RemoveGear(transformer);
            }

            foreach (var transformer in transformers) GearNetworkDatastore.AddGear(transformer);
        }
    }
}
