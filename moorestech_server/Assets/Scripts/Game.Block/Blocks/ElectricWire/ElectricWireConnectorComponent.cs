using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Core.Item.Interface;
using Core.Master;
using MessagePack;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.ElectricWire
{
    public class ElectricWireConnectorComponent : IElectricWireConnector, IBlockSaveState, IPostBlockLoad, IBlockStateObservable, IGetRefundItemsInfo
    {
        // 最大接続数と最大接続距離を保持する
        // Hold max connection count and max connection distance
        private readonly int _maxWireConnectionCount;

        public BlockInstanceId BlockInstanceId { get; }
        public float MaxWireLength { get; }
        public bool IsWireConnectionFull => _wireConnections.Count >= _maxWireConnectionCount;

        // このブロックが持つ電力上の役割。持たない役割はnull
        // Electric roles of this block; null when the role is absent
        public IElectricConsumer WireConsumer { get; }
        public IElectricGenerator WireGenerator { get; }
        public IElectricTransformer WireTransformer { get; }

        private readonly Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionCost Cost)> _wireConnections = new();
        public IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionCost Cost)> WireConnections => _wireConnections;

        // ブロック状態変更通知用のSubject
        // Subject for block state change notifications
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public ElectricWireConnectorComponent(int maxWireConnectionCount, float maxWireLength, BlockInstanceId blockInstanceId, IElectricConsumer consumer, IElectricGenerator generator, IElectricTransformer transformer, Dictionary<string, string> componentStates)
        {
            // 基本状態を初期化する
            // Initialize base state
            _maxWireConnectionCount = maxWireConnectionCount;
            MaxWireLength = maxWireLength;
            BlockInstanceId = blockInstanceId;
            WireConsumer = consumer;
            WireGenerator = generator;
            WireTransformer = transformer;

            _componentStates = componentStates;
            ServerContext.GetService<IElectricWireNetworkDatastore>().AddConnector(this);
        }

        public bool ContainsWireConnection(BlockInstanceId partnerId)
        {
            // 指定IDとの接続有無を確認する
            // Check whether the target id is connected
            return _wireConnections.ContainsKey(partnerId);
        }

        public bool TryAddWireConnection(BlockInstanceId partnerId, ElectricWireConnectionCost connectionCost)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_wireConnections.ContainsKey(partnerId)) return false;
            if (_wireConnections.Count >= _maxWireConnectionCount) return false;
            var connector = ResolveWireTarget(partnerId);
            if (connector == null) return false;
            _wireConnections.Add(partnerId, (connector, connectionCost));
            // 状態変更を通知する
            // Notify state change
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryRemoveWireConnection(BlockInstanceId partnerId, out ElectricWireConnectionCost cost)
        {
            if (!_wireConnections.Remove(partnerId, out var connection))
            {
                cost = default;
                return false;
            }
            cost = connection.Cost;
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        private IElectricWireConnector ResolveWireTarget(BlockInstanceId targetId)
        {
            // 接続候補をワールドから解決する
            // Resolve target connector from world
            var block = ServerContext.WorldBlockDatastore.GetBlock(targetId);
            var connector = block?.GetComponent<IElectricWireConnector>();
            if (connector == null || connector.BlockInstanceId == BlockInstanceId) return null;
            return connector;
        }

        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            // 返却すべきアイテムのリストを取得する
            // Get list of items that should be refunded
            var refundItems = new List<IItemStack>();
            foreach (var connection in _wireConnections.Values)
            {
                if (connection.Cost.Count <= 0 || connection.Cost.ItemId == ItemMaster.EmptyItemId) continue;
                var itemStack = ServerContext.ItemStackFactory.Create(connection.Cost.ItemId, connection.Cost.Count);
                refundItems.Add(itemStack);
            }
            return refundItems;
        }

        #region LoadComponent
        private readonly Dictionary<string, string> _componentStates;
        public void OnPostBlockLoad()
        {
            // 全てのブロックがロードされた後に、セーブデータから接続先を復元する
            // Restore wire connections from saved data after all blocks are loaded
            if (_componentStates == null) return;
            if (!_componentStates.TryGetValue(SaveKey, out var saved)) return;

            var data = JsonConvert.DeserializeObject<ElectricWireSaveDataJsonObject>(saved);
            if (data == null) return;

            _wireConnections.Clear();

            // 接続コスト情報を利用して復元する
            // Restore using connection cost information when available
            if (data.Connections is not { Count: > 0 }) return;

            foreach (var connection in data.Connections)
            {
                if (connection.TargetBlockInstanceId == BlockInstanceId.AsPrimitive()) continue;
                if (_wireConnections.Count >= _maxWireConnectionCount) break;
                var targetId = new BlockInstanceId(connection.TargetBlockInstanceId);
                if (_wireConnections.ContainsKey(targetId)) continue;
                var connector = ResolveWireTarget(targetId);
                if (connector == null) continue;
                var cost = new ElectricWireConnectionCost(MasterHolder.ItemMaster.GetItemId(connection.ItemGuid), connection.Count);
                _wireConnections.Add(targetId, (connector, cost));
            }

            // 復元したワイヤー接続をエネルギーネットワークへ反映する
            // Reflect restored wire connections into the energy network
            ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(this);
            _onChangeBlockState.OnNext(Unit.Default);
        }

        #endregion

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            // 接続先のブロックからも接続を削除する
            // Remove connections from connected blocks as well
            foreach (var targetId in _wireConnections.Keys.ToList())
            {
                var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(targetId);
                var targetConnector = targetBlock?.GetComponent<IElectricWireConnector>();
                if (targetConnector != null) targetConnector.TryRemoveWireConnection(BlockInstanceId, out _);
            }

            // エネルギーネットワークから除去する
            // Remove from the energy network
            ServerContext.GetService<IElectricWireNetworkDatastore>().RemoveConnector(this);

            _wireConnections.Clear();
            _onChangeBlockState.Dispose();
            IsDestroy = true;
        }

        #region IBlockStateObservable
        public BlockStateDetail[] GetBlockStateDetails()
        {
            // ワイヤー接続情報をシリアライズして返す
            // Serialize and return wire connection information
            var stateDetail = new ElectricWireStateDetail(_wireConnections.Keys);
            var bytes = MessagePackSerializer.Serialize(stateDetail);
            return new[] { new BlockStateDetail(ElectricWireStateDetail.BlockStateDetailKey, bytes) };
        }

        #endregion

        #region IBlockSaveState
        public string SaveKey => nameof(ElectricWireConnectorComponent);
        public string GetSaveState()
        {
            // 接続先と消費情報を保存する
            // Persist partner ids and consumption info
            var data = new ElectricWireSaveDataJsonObject(_wireConnections);
            return JsonConvert.SerializeObject(data);
        }

        #endregion
    }
}
