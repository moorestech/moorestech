using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Gear;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Core.Item.Interface;
using Core.Master;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConnectOptionModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleComponent : IGearEnergyTransformer, IBlockSaveState, IGearChainPole, IPostBlockLoad, IBlockStateObservable, IGetRefundItemsInfo
    {
        // マスターデータパラメータを保持する
        // Hold master data parameters
        private readonly GearChainPoleBlockParam _param;

        public float MaxConnectionDistance => _param.MaxConnectionDistance;
        public bool IsConnectionFull => _chainTargets.Count >= _param.MaxConnectionCount;

        // チェーン接続と、周辺ギア接続の列挙を担うserviceを保持する
        // Hold chain connections and the service that enumerates adjacent gear connections
        private readonly SimpleGearService _gearService;
        private readonly GearConnectOption _chainOption = new(false, null);

        private readonly Dictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, GearChainConnectionCost Cost)> _chainTargets = new();

        // ブロック状態変更通知用のSubject
        // Subject for block state change notifications
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public GearChainPoleComponent(GearChainPoleBlockParam param, BlockInstanceId blockInstanceId, BlockConnectorComponent<IGearEnergyTransformer, GearConnectJudge> connectorComponent, Dictionary<string, string> componentStates)
        {
            // 基本状態を初期化する
            // Initialize base state
            _param = param;
            BlockInstanceId = blockInstanceId;
            _gearService = new SimpleGearService(this, connectorComponent);
            _gearService.BlockStateChange.Subscribe(_ => _onChangeBlockState.OnNext(Unit.Default));
            
            _componentStates = componentStates;
            GearNetworkDatastore.AddGear(this);
        }


        public List<GearConnect> GetGearConnects()
        {
            // コネクタ経由の隣接接続にチェーン接続を加えて返す
            // Return adjacent connections via the connector plus chain connections
            var result = _gearService.GetGearConnects();
            foreach (var chainTarget in _chainTargets.Values) result.Add(new GearConnect(chainTarget.Transformer, _chainOption, _chainOption));

            return result;
        }

        public bool ContainsChainConnection(BlockInstanceId partnerId)
        {
            // 指定IDとの接続有無を確認する
            // Check whether the target id is connected
            return _chainTargets.ContainsKey(partnerId);
        }

        public bool TryAddChainConnection(BlockInstanceId partnerId, GearChainConnectionCost connectionCost)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_chainTargets.ContainsKey(partnerId)) return false;
            if (_chainTargets.Count >= _param.MaxConnectionCount) return false;
            var transformer = ResolveChainTarget(partnerId);
            if (transformer == null) return false;
            _chainTargets.Add(partnerId, (transformer, connectionCost));
            // 状態変更を通知する
            // Notify state change
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryRemoveChainConnection(BlockInstanceId partnerId, out GearChainConnectionCost cost)
        {
            if (!_chainTargets.Remove(partnerId, out var connection))
            {
                cost = default;
                return false;
            }

            cost = connection.Cost;
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        private IGearEnergyTransformer ResolveChainTarget(BlockInstanceId targetId)
        {
            // 接続候補をワールドから解決する
            // Resolve target transformer from world
            var block = ServerContext.WorldBlockDatastore.GetBlock(targetId);
            var transformer = block?.GetComponent<IGearEnergyTransformer>();
            if (transformer == null || transformer.BlockInstanceId == BlockInstanceId) return null;
            return transformer;
        }

        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            // 返却すべきアイテムのリストを取得する（接続ごとに複数素材を展開）
            // Get list of items that should be refunded (expand multiple materials per connection)
            var refundItems = new List<IItemStack>();
            foreach (var connection in _chainTargets.Values)
            {
                var materials = connection.Cost.Materials;
                if (materials == null) continue;
                foreach (var material in materials)
                {
                    if (material.Count <= 0 || material.ItemId == ItemMaster.EmptyItemId) continue;
                    refundItems.Add(ServerContext.ItemStackFactory.Create(material.ItemId, material.Count));
                }
            }

            return refundItems;
        }


        #region LoadComponent

        private readonly Dictionary<string, string> _componentStates;
        public void OnPostBlockLoad()
        {
            // 全てのブロックがロードされた後に、セーブデータから接続先を復元する
            // Restore chain connections from saved data after all blocks are loaded
            if (_componentStates == null) return;
            if (!_componentStates.TryGetValue(SaveKey, out var saved)) return;

            var data = JsonConvert.DeserializeObject<GearChainPoleSaveDataJsonObject>(saved);
            if (data == null) return;

            _chainTargets.Clear();
            
            // 接続コスト情報を利用して復元する
            // Restore using connection cost information when available
            if (data.Connections is not { Count: > 0 }) return;
            
            
            foreach (var connection in data.Connections)
            {
                if (connection.TargetBlockInstanceId == BlockInstanceId.AsPrimitive()) continue;
                if (_chainTargets.Count >= _param.MaxConnectionCount) break;
                var targetId = new BlockInstanceId(connection.TargetBlockInstanceId);
                if (_chainTargets.ContainsKey(targetId)) continue;
                var transformer = ResolveChainTarget(targetId);
                if (transformer == null) continue;
                var cost = connection.ToConnectionCost();
                _chainTargets.Add(targetId, (transformer, cost));
            }
            
            // 復元したチェーン接続を次tick先頭の再構築対象にする
            // Mark restored chain connections for rebuilding at the next tick head
            GearNetworkDatastore.MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
        }

        #endregion

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            // 接続先のブロックからも接続を削除する
            // Remove connections from connected blocks as well
            foreach (var targetId in _chainTargets.Keys.ToList())
            {
                var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(targetId);
                var targetPole = targetBlock?.GetComponent<IGearChainPole>();
                if (targetPole != null) targetPole.TryRemoveChainConnection(BlockInstanceId, out _);
            }

            // ギアネットワークから除去する。隣接ギアの噛み合いを次数判定に含めるため、コネクタ生存中に実行する
            // Remove from the gear network while the connector is still alive so adjacent gear meshing counts toward the degree check
            GearNetworkDatastore.RemoveGear(this);

            // コネクタはBlockComponentManagerが別コンポーネントとして破棄するため、ここでは破棄しない
            // The connector is destroyed separately by BlockComponentManager, so it must not be destroyed here
            _chainTargets.Clear();
            _gearService.Destroy();
            _onChangeBlockState.Dispose();
            IsDestroy = true;
        }


        #region IGearEnergyTransformer

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

        #endregion

        #region IBlockStateObservable


        public BlockStateDetail[] GetBlockStateDetails()
        {
            // チェーン接続情報をシリアライズして返す
            // Serialize and return chain connection information
            var partnerIds = _chainTargets.Keys;

            var stateDetail = new GearChainPoleStateDetail(partnerIds);
            var bytes = MessagePackSerializer.Serialize(stateDetail);
            return new[]
            {
                new(GearChainPoleStateDetail.BlockStateDetailKey, bytes),
                _gearService.GetBlockStateDetail(),
            };
        }

        #endregion

        #region IBlockSaveState

        public string SaveKey => nameof(GearChainPoleComponent);
        public string GetSaveState()
        {
            // 接続先と消費情報を保存する
            // Persist partner ids and consumption info
            var data = new GearChainPoleSaveDataJsonObject(_chainTargets);
            return JsonConvert.SerializeObject(data);
        }

        #endregion
    }
}
