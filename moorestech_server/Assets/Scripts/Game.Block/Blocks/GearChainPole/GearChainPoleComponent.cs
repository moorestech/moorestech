using System.Collections.Generic;
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
        public bool HasChainConnection => _chainTargetId.HasValue;
        public BlockInstanceId? PartnerId => _chainTargetId;
        public string SaveKey => ChainConstants.SaveKey;
        public bool IsDestroy { get; private set; }

        // チェーン接続と周辺ギアのコネクターを保持する
        // Hold chain connection and adjacent gear connectors
        private readonly BlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        private readonly SimpleGearService _gearService;
        private readonly GearConnectOption _chainOption = new(false);

        private BlockInstanceId? _chainTargetId;
        private IGearEnergyTransformer _chainTarget;
        private int? _savedTargetId;

        public GearChainPoleComponent(float maxConnectionDistance, BlockInstanceId blockInstanceId, BlockConnectorComponent<IGearEnergyTransformer> connectorComponent, Dictionary<string, string> componentStates)
        {
            // 基本状態を初期化する
            // Initialize base state
            MaxConnectionDistance = maxConnectionDistance;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _gearService = new SimpleGearService(blockInstanceId);
            GearNetworkDatastore.AddGear(this);

            // セーブデータから接続先を復元する
            // Restore connection target from saved state
            LoadSavedState(componentStates);
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
            RefreshChainTarget();

            // ギアコネクター経由の接続を列挙する
            // Enumerate adjacent gear connections
            var result = new List<GearConnect>();
            foreach (var target in _connectorComponent.ConnectedTargets)
            {
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.SelfOption, (GearConnectOption)target.Value.TargetOption));
            }

            // チェーン接続があれば追加する
            // Append chain connection when present
            if (_chainTarget != null) result.Add(new GearConnect(_chainTarget, _chainOption, _chainOption));

            return result;
        }

        public void SetChainConnection(BlockInstanceId partnerId)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            _chainTargetId = partnerId;
            _savedTargetId = partnerId.AsPrimitive();
            RefreshChainTarget();
        }

        public void ClearChainConnection()
        {
            // 接続情報を消去する
            // Clear current connection info
            _chainTargetId = null;
            _chainTarget = null;
            _savedTargetId = null;
        }

        public string GetSaveState()
        {
            // 接続先のIDだけを保存する
            // Persist only partner id
            var data = new GearChainPoleSaveData(_savedTargetId);
            return JsonConvert.SerializeObject(data);
        }

        public void Destroy()
        {
            // コンポーネントのリソースを解放する
            // Release component resources
            _connectorComponent.Destroy();
            if (GearNetworkDatastore.Contains(this)) GearNetworkDatastore.RemoveGear(this);
            ClearChainConnection();
            IsDestroy = true;
        }

        private void LoadSavedState(Dictionary<string, string> componentStates)
        {
            // セーブデータが存在する場合だけ復元する
            // Restore when saved state exists
            if (componentStates == null) return;
            if (!componentStates.TryGetValue(SaveKey, out var saved)) return;
            var data = JsonConvert.DeserializeObject<GearChainPoleSaveData>(saved);
            _savedTargetId = data?.TargetBlockInstanceId;
        }

        private void RefreshChainTarget()
        {
            // セーブ済みのIDから接続先を再設定する
            // Rehydrate target from saved id
            if (!_chainTargetId.HasValue && _savedTargetId.HasValue) _chainTargetId = new BlockInstanceId(_savedTargetId.Value);
            if (!_chainTargetId.HasValue) return;

            // ワールドに存在しなければ接続を破棄する
            // Drop connection when target is missing
            var block = ServerContext.WorldBlockDatastore.GetBlock(_chainTargetId.Value);
            if (block == null)
            {
                ClearChainConnection();
                return;
            }

            // 対象のトランスフォーマーを取得する
            // Resolve partner transformer
            var transformer = block.GetComponent<IGearEnergyTransformer>();
            if (transformer == null || transformer.BlockInstanceId == BlockInstanceId)
            {
                ClearChainConnection();
                return;
            }

            _chainTarget = transformer;
        }

        private class GearChainPoleSaveData
        {
            [JsonProperty("targetBlockInstanceId")]
            public int? TargetBlockInstanceId { get; }

            public GearChainPoleSaveData(int? targetBlockInstanceId)
            {
                TargetBlockInstanceId = targetBlockInstanceId;
            }
        }
    }
}
