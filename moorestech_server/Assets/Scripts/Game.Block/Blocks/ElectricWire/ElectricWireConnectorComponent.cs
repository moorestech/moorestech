using Game.Block.Blocks.ConnectionLine;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Core.Item.Interface;
using MessagePack;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.ElectricWire
{
    public class ElectricWireConnectorComponent : IElectricWireConnector, IBlockSaveState, IPostBlockLoad, IBlockStateObservable, IGetRefundItemsInfo
    {
        private readonly int _maxWireConnectionCount;

        public BlockInstanceId BlockInstanceId { get; }
        public bool IsWireConnectionFull => _maxWireConnectionCount <= _wireConnections.Count;

        // このブロックが持つ電力上の役割。必ず消費・発電・送電のいずれかに紐づく
        // Electric role of this block; always tied to a consumer, generator or transformer
        public IElectricEnergyRole EnergyRole { get; }

        private readonly Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> _wireConnections = new();
        public IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> WireConnections => _wireConnections;

        // ブロック状態変更通知用のSubject
        // Subject for block state change notifications
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public ElectricWireConnectorComponent(int maxWireConnectionCount, BlockInstanceId blockInstanceId, IElectricEnergyRole energyRole, Dictionary<string, object> componentStates)
        {
            // 役割なしのワイヤー端点は許容しない
            // A wire endpoint without an energy role is not allowed
            if (energyRole == null) throw new ArgumentNullException(nameof(energyRole));

            // 基本状態を初期化する
            // Initialize base state
            _maxWireConnectionCount = maxWireConnectionCount;
            BlockInstanceId = blockInstanceId;
            EnergyRole = energyRole;

            _componentStates = componentStates;
            ServerContext.GetService<IElectricWireNetworkMutation>().AddConnector(this);
        }

        public bool ContainsWireConnection(BlockInstanceId partnerId)
        {
            // 指定IDとの接続有無を確認する
            // Check whether the target id is connected
            return _wireConnections.ContainsKey(partnerId);
        }

        public bool TryAddWireConnection(BlockInstanceId partnerId, ConnectionLineRecord connectionRecord)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_wireConnections.ContainsKey(partnerId)) return false;
            if (_maxWireConnectionCount <= _wireConnections.Count) return false;
            var connector = ResolveWireTarget(partnerId);
            if (connector == null) return false;
            _wireConnections.Add(partnerId, (connector, connectionRecord));
            // 接続集合の変更点自身でdirty化し、呼び出し元の再構築漏れを構造的に防ぐ
            // Mark dirty at the mutation itself so no caller can ever forget the rebuild
            ServerContext.GetService<IElectricWireNetworkMutation>().MarkTopologyDirty();
            // 状態変更を通知する
            // Notify state change
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryRemoveWireConnection(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            if (!_wireConnections.Remove(partnerId, out var connection))
            {
                record = default;
                return false;
            }
            record = connection.Record;
            ServerContext.GetService<IElectricWireNetworkMutation>().MarkTopologyDirty();
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
            // 接続ごとに払った素材を返却する
            // Refund the materials paid for each connection
            var refundItems = new List<IItemStack>();
            foreach (var connection in _wireConnections.Values) refundItems.AddRange(ConnectionLineRefundItems.Create(connection.Record.Materials));
            return refundItems;
        }

        #region LoadComponent
        private readonly Dictionary<string, object> _componentStates;
        public void OnPostBlockLoad()
        {
            // 全てのブロックがロードされた後に、セーブデータから接続先を復元する
            // Restore wire connections from saved data after all blocks are loaded
            if (_componentStates == null) return;
            if (!BlockComponentStateReader.TryRead<ElectricWireSaveDataJsonObject>(_componentStates, SaveKey, out var data)) return;

            _wireConnections.Clear();

            // 空の接続は正常、nullは破損として区別する
            // Empty connections are valid; null marks malformed save data
            if (data.Connections == null)
            {
                Debug.LogWarning($"[ElectricWire] Saved connections missing: {BlockInstanceId}");
                return;
            }
            if (data.Connections.Count == 0) return;
            ElectricWireConnectionRestorer.Restore(data, BlockInstanceId, _maxWireConnectionCount, _wireConnections);

            // 復元接続をエネルギー網へ反映
            // Reflect restored wire connections into the energy network
            ServerContext.GetService<IElectricWireNetworkMutation>().MarkTopologyDirty();
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
            ServerContext.GetService<IElectricWireNetworkMutation>().RemoveConnector(this);

            _wireConnections.Clear();
            _onChangeBlockState.Dispose();
            IsDestroy = true;
        }

        #region IBlockStateObservable
        public BlockStateDetail[] GetBlockStateDetails()
        {
            // ワイヤー接続情報をシリアライズして返す
            // Serialize and return wire connection information
            var stateDetail = new ElectricWireStateDetail(ConnectionLinePartnerMessagePack.CreateArray(_wireConnections));
            var bytes = MessagePackSerializer.Serialize(stateDetail);
            return new[] { new BlockStateDetail(ElectricWireStateDetail.BlockStateDetailKey, bytes) };
        }

        #endregion

        #region IBlockSaveState
        public string SaveKey => nameof(ElectricWireConnectorComponent);
        public object GetSaveState()
        {
            // 接続先と消費情報を保存する
            // Persist partner ids and consumption info
            var data = new ElectricWireSaveDataJsonObject(_wireConnections);
            return data;
        }

        #endregion
    }
}
