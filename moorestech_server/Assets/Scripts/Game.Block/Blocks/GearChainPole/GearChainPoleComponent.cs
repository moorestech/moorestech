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
using MessagePack;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleComponent : IGearEnergyTransformer, IBlockSaveState, IGearChainPole, IPostBlockLoad, IBlockStateObservable
    {
        // マスターデータパラメータを保持する
        // Hold master data parameters
        private readonly GearChainPoleBlockParam _param;
        
        public float MaxConnectionDistance => _param.MaxConnectionDistance;
        public bool IsConnectionFull => _chainTargets.Count >= _param.MaxConnectionCount;

        // チェーン接続と周辺ギアのコネクターを保持する
        // Hold chain connection and adjacent gear connectors
        private readonly BlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        private readonly SimpleGearService _gearService;
        private readonly GearConnectOption _chainOption = new(false);

        private readonly Dictionary<BlockInstanceId, IGearEnergyTransformer> _chainTargets = new();
        
        // ブロック状態変更通知用のSubject
        // Subject for block state change notifications
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public GearChainPoleComponent(GearChainPoleBlockParam param, BlockInstanceId blockInstanceId, BlockConnectorComponent<IGearEnergyTransformer> connectorComponent, Dictionary<string, string> componentStates)
        {
            // 基本状態を初期化する
            // Initialize base state
            _param = param;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _gearService = new SimpleGearService(blockInstanceId);
            _componentStates = componentStates;
            GearNetworkDatastore.AddGear(this);
        }
        

        public List<GearConnect> GetGearConnects()
        {
            // ギアコネクター経由の接続を列挙する
            // Enumerate adjacent gear connections
            var result = new List<GearConnect>();
            foreach (var target in _connectorComponent.ConnectedTargets)
            {
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.SelfOption, (GearConnectOption)target.Value.TargetOption));
            }

            // チェーン接続があれば追加する
            // Append chain connection when present
            foreach (var chainTarget in _chainTargets.Values) result.Add(new GearConnect(chainTarget, _chainOption, _chainOption));

            return result;
        }

        public bool ContainsChainConnection(BlockInstanceId partnerId)
        {
            // 指定IDとの接続有無を確認する
            // Check whether the target id is connected
            return _chainTargets.ContainsKey(partnerId);
        }

        public bool TryAddChainConnection(BlockInstanceId partnerId)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_chainTargets.ContainsKey(partnerId)) return false;
            if (_chainTargets.Count >= _param.MaxConnectionCount) return false;
            var transformer = ResolveChainTarget(partnerId);
            if (transformer == null) return false;
            _chainTargets.Add(partnerId, transformer);
            // 状態変更を通知する
            // Notify state change
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool RemoveChainConnection(BlockInstanceId partnerId)
        {
            // 指定した接続を解除する
            // Remove a specific partner connection
            var removed = _chainTargets.Remove(partnerId);
            if (removed)
            {
                // 状態変更を通知する
                // Notify state change
                _onChangeBlockState.OnNext(Unit.Default);
            }
            return removed;
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
        
        
        #region LoadComponent
        
        private readonly Dictionary<string, string> _componentStates;
        public void OnPostBlockLoad()
        {
            // 全てのブロックがロードされた後に、セーブデータから接続先を復元する
            // Restore chain connections from saved data after all blocks are loaded
            if (_componentStates == null) return;
            if (!_componentStates.TryGetValue(SaveKey, out var saved)) return;
            
            var data = JsonConvert.DeserializeObject<GearChainPoleSaveData>(saved);
            if (data?.TargetBlockInstanceIds == null) return;
            
            _chainTargets.Clear();
            foreach (var targetIdInt in data.TargetBlockInstanceIds)
            {
                var targetId = new BlockInstanceId(targetIdInt);
                if (targetId == BlockInstanceId) continue;
                if (_chainTargets.ContainsKey(targetId)) continue;
                if (_chainTargets.Count >= _param.MaxConnectionCount) break;
                var transformer = ResolveChainTarget(targetId);
                if (transformer == null) continue;
                _chainTargets.Add(targetId, transformer);
            }
        }
        
        #endregion
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            // コンポーネントのリソースを解放する
            // Release component resources
            _connectorComponent.Destroy();
            GearNetworkDatastore.RemoveGear(this);
            _chainTargets.Clear();
            _onChangeBlockState.Dispose();
            IsDestroy = true;
        }
        
        
        #region IGearEnergyTransformer
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            // チェーンポール自体は負荷を持たない
            // Chain pole itself does not consume torque
            return new Torque(0);
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _gearService.CurrentRpm;
        public Torque CurrentTorque => _gearService.CurrentTorque;
        public bool IsCurrentClockwise => _gearService.IsCurrentClockwise;
        
        
        public void StopNetwork()
        {
            // ネットワーク停止をサービスに委譲する
            // Delegate network stop to service
            _gearService.StopNetwork();
        }
        
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            // 入力された回転をサービスへ転送する
            // Forward supplied rotation to service
            _gearService.SupplyPower(rpm, torque, isClockwise);
        }
        
        #endregion
        
        #region IBlockStateObservable
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            // チェーン接続情報をシリアライズして返す
            // Serialize and return chain connection information
            var stateDetail = new GearChainPoleStateDetail(_chainTargets.Keys);
            var bytes = MessagePackSerializer.Serialize(stateDetail);
            return new BlockStateDetail[] { new(GearChainPoleStateDetail.BlockStateDetailKey, bytes) };
        }
        
        #endregion
        
        #region IBlockSaveState
        
        public string SaveKey => nameof(GearChainPoleComponent);
        public string GetSaveState()
        {
            // 接続先のIDリストを保存する
            // Persist partner id list
            var targetIds = _chainTargets.Keys.Select(id => id.AsPrimitive()).ToList();
            var data = new GearChainPoleSaveData(targetIds);
            return JsonConvert.SerializeObject(data);
        }
        
        #endregion
    }
}
