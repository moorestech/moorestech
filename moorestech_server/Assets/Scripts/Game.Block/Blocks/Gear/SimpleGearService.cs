using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlockConnectInfoModule;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    // gearの現在値導出・接続列挙・状態変化通知・ステート詳細シリアライズを担うサービス。現在値は保持せず、毎回所属networkから導出する。
    // Service for deriving gear current values, enumerating connections, state-change notifications, and state-detail serialization. Holds no current values; derives them from the owning network each call.
    public class SimpleGearService
    {
        public IObservable<Unit> BlockStateChange => _onBlockStateChange;
        private readonly Subject<Unit> _onBlockStateChange = new();

        public IObservable<GearUpdateType> OnGearUpdate => _onGearUpdate;
        private readonly Subject<GearUpdateType> _onGearUpdate = new();

        // 現在値導出に必要なIDと要求トルク計算をownerへ委譲する
        // Delegate the id and required-torque calculation needed for derivation to the owner
        private readonly IGearEnergyTransformer _owner;
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;

        public SimpleGearService(IGearEnergyTransformer owner, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            _owner = owner;
            _connectorComponent = connectorComponent;
        }

        // コネクタ経由の隣接gear接続を列挙する
        // Enumerate adjacent gear connections via the connector
        public List<GearConnect> GetGearConnects()
        {
            var result = new List<GearConnect>();
            foreach (var target in _connectorComponent.ConnectedTargets)
            {
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.SelfConnector?.ConnectOption, (GearConnectOption)target.Value.TargetConnector?.ConnectOption));
            }
            return result;
        }

        public RPM CurrentRpm
        {
            get
            {
                TryResolveRotation(out var rpm, out _);
                return rpm;
            }
        }

        public Torque CurrentTorque
        {
            get
            {
                if (!TryResolveRotation(out var rpm, out var isClockwise)) return new Torque(0);
                if (rpm.AsPrimitive() <= 0f) return new Torque(0);

                // generatorは自身の発電トルク、消費側は現在RPMでの要求トルクを現在トルクとみなす
                // A generator reports generated torque; a consumer reports required torque at the current RPM
                if (_owner is IGearGenerator generator) return generator.GenerateTorque;
                return _owner.GetRequiredTorque(rpm, isClockwise);
            }
        }

        public bool IsCurrentClockwise
        {
            get
            {
                TryResolveRotation(out _, out var isClockwise);
                return isClockwise;
            }
        }

        // tick計算後に呼ばれ、クライアントへ状態変化を通知する
        // Called after tick calculation to notify clients of the state change
        public void NotifyStateChanged()
        {
            _onBlockStateChange.OnNext(Unit.Default);
            _onGearUpdate.OnNext(GearUpdateType.SupplyPower);
        }

        // 導出した現在値をクライアント送信用のステート詳細へシリアライズする
        // Serialize the derived current values into a client-facing state detail
        public BlockStateDetail GetBlockStateDetail()
        {
            var stateDetail = new GearStateDetail(IsCurrentClockwise, CurrentRpm.AsPrimitive(), CurrentTorque.AsPrimitive());
            return new BlockStateDetail(GearStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        public void Destroy()
        {
            _onBlockStateChange.Dispose();
        }

        // 所属networkを引き、符号付き原点RPM比から実RPMと絶対回転方向を導出する
        // Look up the owning network and derive actual RPM and absolute direction from the signed origin RPM ratio
        private bool TryResolveRotation(out RPM rpm, out bool isClockwise)
        {
            rpm = new RPM(0);
            isClockwise = true;
            if (!GearNetworkDatastore.TryGetGearNetwork(_owner.BlockInstanceId, out var network)) return false;
            return network.TryResolveRotation(_owner.BlockInstanceId, out rpm, out isClockwise);
        }
    }

    public enum GearUpdateType
    {
        SupplyPower,
        Rocked,
    }
}
