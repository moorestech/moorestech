using System.Collections.Generic;
using System.Linq;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Gear;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleComponent : IGearEnergyTransformer, IBlockSaveState, IGearChainPole
    {
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _gearService.CurrentRpm;
        public Torque CurrentTorque => _gearService.CurrentTorque;
        public bool IsCurrentClockwise => _gearService.IsCurrentClockwise;
        public float MaxConnectionDistance { get; }
        public bool IsConnectionFull
        {
            get
            {
                RefreshChainTargets();
                return _chainTargets.Count >= _maxConnectionCount;
            }
        }
        public IReadOnlyCollection<BlockInstanceId> PartnerIds => _chainTargets.Keys;
        public string SaveKey => ChainConstants.SaveKey;
        public bool IsDestroy { get; private set; }

        // チェーン接続と周辺ギアのコネクターを保持する
        // Hold chain connection and adjacent gear connectors
        private readonly BlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        private readonly SimpleGearService _gearService;
        private readonly GearConnectOption _chainOption = new(false);
        private readonly int _maxConnectionCount;

        private readonly Dictionary<BlockInstanceId, IGearEnergyTransformer> _chainTargets = new();
        private readonly List<int> _savedTargetIds = new();

        public GearChainPoleComponent(float maxConnectionDistance, int maxConnectionCount, BlockInstanceId blockInstanceId, BlockConnectorComponent<IGearEnergyTransformer> connectorComponent, Dictionary<string, string> componentStates)
        {
            // 基本状態を初期化する
            // Initialize base state
            MaxConnectionDistance = maxConnectionDistance;
            _maxConnectionCount = maxConnectionCount;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _gearService = new SimpleGearService(blockInstanceId);
            GearNetworkDatastore.AddGear(this);

            // セーブデータから接続先を復元する
            // Restore connection target from saved state
            LoadSavedState(componentStates);
            RefreshChainTargets();
        }

        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            // チェーンポール自体は負荷を持たない
            // Chain pole itself does not consume torque
            return new Torque(0);
        }

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

        public List<GearConnect> GetGearConnects()
        {
            // 接続先を最新化する
            // Refresh connection target
            RefreshChainTargets();

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
            RefreshChainTargets();
            return _chainTargets.ContainsKey(partnerId);
        }

        public bool TryAddChainConnection(BlockInstanceId partnerId)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            RefreshChainTargets();
            if (_chainTargets.ContainsKey(partnerId)) return false;
            if (_chainTargets.Count >= _maxConnectionCount) return false;
            var transformer = ResolveChainTarget(partnerId);
            if (transformer == null) return false;
            _chainTargets.Add(partnerId, transformer);
            SyncSavedTargetIds();
            return true;
        }

        public bool RemoveChainConnection(BlockInstanceId partnerId)
        {
            // 指定した接続を解除する
            // Remove a specific partner connection
            RefreshChainTargets();
            var removed = _chainTargets.Remove(partnerId);
            if (!removed) return false;
            SyncSavedTargetIds();
            return true;
        }

        public void ClearChainConnections()
        {
            // 全ての接続情報を消去する
            // Clear every chain connection info
            _chainTargets.Clear();
            _savedTargetIds.Clear();
        }

        public string GetSaveState()
        {
            // 接続先のIDリストを保存する
            // Persist partner id list
            var data = new GearChainPoleSaveData(new List<int>(_savedTargetIds));
            return JsonConvert.SerializeObject(data);
        }

        public void Destroy()
        {
            // コンポーネントのリソースを解放する
            // Release component resources
            _connectorComponent.Destroy();
            if (GearNetworkDatastore.Contains(this)) GearNetworkDatastore.RemoveGear(this);
            ClearChainConnections();
            IsDestroy = true;
        }

        private void LoadSavedState(Dictionary<string, string> componentStates)
        {
            // セーブデータが存在する場合だけ復元する
            // Restore when saved state exists
            if (componentStates == null) return;
            if (!componentStates.TryGetValue(SaveKey, out var saved)) return;
            var data = JsonConvert.DeserializeObject<GearChainPoleSaveData>(saved);
            if (data?.TargetBlockInstanceIds == null) return;
            _savedTargetIds.Clear();
            _savedTargetIds.AddRange(data.TargetBlockInstanceIds.Where(id => id != BlockInstanceId.AsPrimitive()));
        }

        private void RefreshChainTargets()
        {
            // セーブ済みと現在の接続を同期する
            // Sync saved ids with current world connections
            var candidates = _savedTargetIds.Select(id => new BlockInstanceId(id)).ToList();
            foreach (var targetId in _chainTargets.Keys)
            {
                if (!candidates.Contains(targetId)) candidates.Add(targetId);
            }

            var refreshed = new Dictionary<BlockInstanceId, IGearEnergyTransformer>();
            foreach (var targetId in candidates)
            {
                if (refreshed.ContainsKey(targetId)) continue;
                if (refreshed.Count >= _maxConnectionCount) break;
                var transformer = ResolveChainTarget(targetId);
                if (transformer == null) continue;
                refreshed.Add(targetId, transformer);
            }

            _chainTargets.Clear();
            foreach (var pair in refreshed) _chainTargets.Add(pair.Key, pair.Value);
            SyncSavedTargetIds();
        }

        private IGearEnergyTransformer ResolveChainTarget(BlockInstanceId targetId)
        {
            // 接続候補をワールドから解決する
            // Resolve target transformer from world
            var block = ServerContext.WorldBlockDatastore.GetBlock(targetId);
            if (block == null) return null;
            var transformer = block.GetComponent<IGearEnergyTransformer>();
            if (transformer == null || transformer.BlockInstanceId == BlockInstanceId) return null;
            return transformer;
        }

        private void SyncSavedTargetIds()
        {
            // 保存用のIDを最新状態に合わせる
            // Sync saved ids to current targets
            _savedTargetIds.Clear();
            foreach (var key in _chainTargets.Keys) _savedTargetIds.Add(key.AsPrimitive());
        }

        private class GearChainPoleSaveData
        {
            [JsonProperty("targetBlockInstanceIds")]
            public IReadOnlyCollection<int> TargetBlockInstanceIds { get; }

            public GearChainPoleSaveData(IReadOnlyCollection<int> targetBlockInstanceIds)
            {
                TargetBlockInstanceIds = targetBlockInstanceIds ?? new List<int>();
            }
        }
    }
}
