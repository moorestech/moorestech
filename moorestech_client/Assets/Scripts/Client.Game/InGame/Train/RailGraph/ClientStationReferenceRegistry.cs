using Client.Game.InGame.Block;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Mooresmaster.Model.BlocksModule;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using Game.Train.SaveLoad;

namespace Client.Game.InGame.Train.RailGraph
{
    public sealed class ClientStationReferenceRegistry : IInitializable, IDisposable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly RailGraphClientCache _cache;
        private readonly Dictionary<ConnectionDestination, StationReference> _stationByDestination = new();
        private readonly CompositeDisposable _subscriptions = new();

        public ClientStationReferenceRegistry(BlockGameObjectDataStore blockGameObjectDataStore, RailGraphClientCache cache)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _cache = cache;
        }

        public void Initialize()
        {
            // 駅ブロックのイベントを購読して参照を同期する
            // Subscribe to station block events and keep references in sync.
            _blockGameObjectDataStore.OnBlockPlaced.Subscribe(HandleBlockPlaced).AddTo(_subscriptions);
            _blockGameObjectDataStore.OnBlockRemoved.Subscribe(HandleBlockRemoved).AddTo(_subscriptions);

            // 既存ブロックから駅参照を構築してキャッシュへ反映する
            // Build station references from existing blocks and apply to cache.
            foreach (var block in _blockGameObjectDataStore.BlockGameObjectDictionary.Values) RegisterStationReferencesIfNeeded(block);
            ApplyStationReferences();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public void ApplyStationReferences()
        {
            // キャッシュ内ノードへ駅参照を反映する
            // Apply station references to nodes in cache.
            foreach (var node in _cache.Nodes)
            {
                if (node == null) continue;
                ApplyStationReference(node);
            }
        }

        public void ApplyStationReference(ConnectionDestination destination)
        {
            // ConnectionDestinationから該当ノードに駅参照を反映する
            // Apply station reference to the node identified by ConnectionDestination.
            if (!_cache.TryGetNodeId(destination, out var nodeId)) return;
            var node = _cache.Nodes[nodeId];
            if (node == null) return;
            ApplyStationReference(node);
        }

        private void HandleBlockPlaced(BlockGameObject block)
        {
            // 駅ブロックを登録して関連ノードを更新する
            // Register station block and update related nodes.
            if (!RegisterStationReferencesIfNeeded(block)) return;
            ApplyStationReferencesForPosition(block.BlockPosInfo.OriginalPos);
        }

        private void HandleBlockRemoved(Vector3Int position)
        {
            // 駅参照を削除して関連ノードを更新する
            // Remove station references and update related nodes.
            RemoveStationReferences(position);
            ApplyStationReferencesForPosition(position);
        }

        private bool RegisterStationReferencesIfNeeded(BlockGameObject block)
        {
            // 駅系ブロックのみ参照を登録する
            // Register references only for station-type blocks.
            var blockParam = block.BlockMasterElement.BlockParam;
            if (blockParam is not TrainStationBlockParam && blockParam is not TrainCargoPlatformBlockParam) return false;
            RegisterStationReferences(block.BlockInstanceId, block.BlockPosInfo.OriginalPos);
            return true;
        }

        private void RegisterStationReferences(BlockInstanceId instanceId, Vector3Int position)
        {
            // 駅の4ノードを登録する
            // Register four station nodes.
            RegisterStationReference(instanceId, position, 0, true);
            RegisterStationReference(instanceId, position, 1, true);
            RegisterStationReference(instanceId, position, 1, false);
            RegisterStationReference(instanceId, position, 0, false);
        }

        private void RemoveStationReferences(Vector3Int position)
        {
            // 駅の4ノード参照を削除する
            // Remove four station node references.
            RemoveStationReference(position, 0, true);
            RemoveStationReference(position, 1, true);
            RemoveStationReference(position, 1, false);
            RemoveStationReference(position, 0, false);
        }

        private void ApplyStationReferencesForPosition(Vector3Int position)
        {
            // 駅の4ノードへ参照を反映する
            // Apply references to four station nodes.
            ApplyStationReference(CreateDestination(position, 0, true));
            ApplyStationReference(CreateDestination(position, 1, true));
            ApplyStationReference(CreateDestination(position, 1, false));
            ApplyStationReference(CreateDestination(position, 0, false));
        }

        private void RegisterStationReference(BlockInstanceId instanceId, Vector3Int position, int componentIndex, bool isFront)
        {
            // 駅ノード参照を作成して登録する
            // Create and register a station node reference.
            var destination = CreateDestination(position, componentIndex, isFront);
            var nodeRole = ResolveNodeRole(componentIndex, isFront);
            var nodeSide = isFront ? StationNodeSide.Front : StationNodeSide.Back;
            var stationRef = new StationReference();
            stationRef.SetStationReference(instanceId, position, nodeRole, nodeSide);
            _stationByDestination[destination] = stationRef;
        }

        private void RemoveStationReference(Vector3Int position, int componentIndex, bool isFront)
        {
            // 駅ノード参照を削除する
            // Remove a station node reference.
            var destination = CreateDestination(position, componentIndex, isFront);
            _stationByDestination.Remove(destination);
        }

        private void ApplyStationReference(ClientRailNode node)
        {
            // キャッシュノードに駅参照を適用する
            // Apply station reference to a cache node.
            var destination = node.ConnectionDestination;
            var hasStationRef = _stationByDestination.TryGetValue(destination, out var stationRef);
            node.UpdateStationReference(hasStationRef ? stationRef : new StationReference());
        }

        private static ConnectionDestination CreateDestination(Vector3Int position, int componentIndex, bool isFront)
        {
            return new ConnectionDestination(position, componentIndex, isFront);
        }

        private static StationNodeRole ResolveNodeRole(int componentIndex, bool isFront)
        {
            return (componentIndex == 0 && isFront) || (componentIndex == 1 && !isFront) ? StationNodeRole.Entry : StationNodeRole.Exit;
        }
    }
}
