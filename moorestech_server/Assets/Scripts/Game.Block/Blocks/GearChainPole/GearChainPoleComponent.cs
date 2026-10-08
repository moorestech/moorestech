using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Component;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Gear;
using Game.Block.Interface.Extension;
using UnityEngine;
using Game.Context;
using Game.Gear.Common;
using Core.Item.Interface;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConnectOptionModule;
using UniRx;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleComponent : IGearEnergyTransformer, IBlockSaveState, IGearChainPole, IPostBlockLoad, IBlockStateObservable, IGetRefundItemsInfo
    {
        // マスターデータパラメータを保持する
        // Hold master data parameters
        private readonly GearChainPoleBlockParam _param;

        public float MaxConnectionDistance => _param.MaxConnectionDistance;
        public bool IsConnectionFull => _param.MaxConnectionCount <= _chainLookup.Count;

        // チェーン接続と、周辺ギア接続の列挙を担うserviceを保持する
        // Hold chain connections and the service that enumerates adjacent gear connections
        private readonly SimpleGearService _gearService;
        private readonly GearConnectOption _chainOption = new(false, null);

        // 台帳の読み取り面と変更面を分離する
        // Separate the ledger's read and mutation surfaces
        private readonly IGearChainConnectionLookup _chainLookup;
        private readonly IGearChainConnectionMutation _chainMutation;

        // ブロック状態変更通知用のSubject
        // Subject for block state change notifications
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public GearChainPoleComponent(GearChainPoleBlockParam param, BlockInstanceId blockInstanceId, BlockConnectorComponent<IGearEnergyTransformer, GearContext> connectorComponent, Dictionary<string, object> componentStates)
        {
            // 基本状態を初期化する
            // Initialize base state
            var chainConnections = new GearChainConnectionSet();
            _chainLookup = chainConnections;
            _chainMutation = chainConnections;
            _param = param;
            BlockInstanceId = blockInstanceId;
            _gearService = new SimpleGearService(this, connectorComponent);
            _gearService.BlockStateChange.Subscribe(_ => _onChangeBlockState.OnNext(Unit.Default));
            
            _componentStates = componentStates;
            ServerContext.GetService<IGearNetworkDatastore>().AddGear(this);
        }

        public List<GearConnect> GetGearConnects()
        {
            // コネクタ経由の隣接接続にチェーン接続を加えて返す
            // Return adjacent connections via the connector plus chain connections
            var result = _gearService.GetGearConnects();
            foreach (var chainTarget in _chainLookup.Targets.Values) result.Add(new GearConnect(chainTarget.Transformer, _chainOption, _chainOption));

            return result;
        }

        public bool ContainsChainConnection(BlockInstanceId partnerId)
        {
            return _chainLookup.Contains(partnerId);
        }

        public bool TryAddChainConnection(BlockInstanceId partnerId, ConnectionLineRecord connectionRecord)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_chainLookup.Contains(partnerId) || IsConnectionFull)
            {
                Debug.LogWarning($"[GearChain] Connection already exists or limit reached: {BlockInstanceId} -> {partnerId}");
                return false;
            }
            var transformer = GearChainConnectionRestorer.ResolveTarget(BlockInstanceId, partnerId);
            if (transformer == null) return false;
            _chainMutation.Add(partnerId, transformer, connectionRecord);
            // 接続集合の変更点自身でdirty化し、呼び出し元の再構築漏れを構造的に防ぐ
            // Mark dirty at the mutation itself so no caller can ever forget the rebuild
            ServerContext.GetService<IGearNetworkDatastore>().MarkTopologyDirty();
            // 状態変更を通知する
            // Notify state change
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryRemoveChainConnection(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            if (!_chainMutation.TryRemove(partnerId, out record))
            {
                Debug.LogWarning($"[GearChain] Connection to remove does not exist: {BlockInstanceId} -> {partnerId}");
                return false;
            }
            ServerContext.GetService<IGearNetworkDatastore>().MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            return _chainLookup.TryGetRecord(partnerId, out record);
        }

        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            return _chainLookup.CreateRefundItems();
        }

        private readonly Dictionary<string, object> _componentStates;
        public void OnPostBlockLoad()
        {
            // 全ブロック生成後に保存台帳を復元
            // Restore the saved ledger after all blocks are created
            if (_componentStates == null) return;
            if (!BlockComponentStateReader.TryRead<GearChainPoleSaveDataJsonObject>(_componentStates, SaveKey, out var data)) return;
            GearChainConnectionRestorer.Restore(data, BlockInstanceId, _param.MaxConnectionCount, _chainLookup, _chainMutation);

            // 復元した接続を次tick先頭で反映
            // Rebuild the network from restored connections at the next tick head
            ServerContext.GetService<IGearNetworkDatastore>().MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            // 接続先のブロックからも接続を削除する
            // Remove connections from connected blocks as well
            foreach (var targetId in _chainLookup.PartnerIds.ToList())
            {
                var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(targetId);
                var targetPole = targetBlock?.GetComponent<IGearChainPole>();
                if (targetPole != null) targetPole.TryRemoveChainConnection(BlockInstanceId, out _);
            }

            // ギアネットワークから除去する。隣接ギアの噛み合いを次数判定に含めるため、コネクタ生存中に実行する
            // Remove from the gear network while the connector is still alive so adjacent gear meshing counts toward the degree check
            ServerContext.GetService<IGearNetworkDatastore>().RemoveGear(this);

            // コネクタはBlockComponentManagerが別コンポーネントとして破棄するため、ここでは破棄しない
            // The connector is destroyed separately by BlockComponentManager, so it must not be destroyed here
            _chainMutation.Clear();
            _gearService.Destroy();
            _onChangeBlockState.Dispose();
            IsDestroy = true;
        }

        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            // マスタ設定のgearConsumptionに従って必要トルクを算出（baseTorque=0で消費ゼロ維持可能）
            // Calculate required torque from gearConsumption master (baseTorque=0 keeps zero consumption)
            return GearConsumptionCalculator.CalcRequiredTorque(_param.GearConsumption, rpm);
        }

        public BlockInstanceId BlockInstanceId { get; }

        // 現在値の導出はserviceへ委譲。serviceも値を保持せず毎回networkから導出する
        // Current-value derivation is delegated to the service, which also holds nothing and derives from the network each call
        public RPM CurrentRpm => _gearService.CurrentRpm;
        public Torque CurrentTorque => _gearService.CurrentTorque;
        public bool IsCurrentClockwise => _gearService.IsCurrentClockwise;

        public void NotifyStateChanged()
        {
            _gearService.NotifyStateChanged();
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            // チェーン接続情報をシリアライズして返す
            // Serialize and return chain connection information
            var stateDetail = new GearChainPoleStateDetail(ConnectionLinePartnerMessagePack.CreateArray(_chainLookup.Targets));
            var bytes = MessagePackSerializer.Serialize(stateDetail);
            return new[]
            {
                new(GearChainPoleStateDetail.BlockStateDetailKey, bytes),
                _gearService.GetBlockStateDetail(),
            };
        }

        public string SaveKey => nameof(GearChainPoleComponent);
        public object GetSaveState()
        {
            return _chainLookup.CreateSaveData();
        }

    }
}
