using System;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    // gearの状態変化通知とステート詳細のシリアライズを担う薄いサービス。現在値は保持せず、呼び出し側が導出した値を受け取る。
    // Thin service for gear state-change notifications and state-detail serialization. Holds no current values; callers pass in derived values.
    public class SimpleGearService
    {
        public IObservable<Unit> BlockStateChange => _onBlockStateChange;
        private readonly Subject<Unit> _onBlockStateChange = new();

        public IObservable<GearUpdateType> OnGearUpdate => _onGearUpdate;
        private readonly Subject<GearUpdateType> _onGearUpdate = new();

        // tick計算後に呼ばれ、クライアントへ状態変化を通知する
        // Called after tick calculation to notify clients of the state change
        public void NotifyStateChanged()
        {
            _onBlockStateChange.OnNext(Unit.Default);
            _onGearUpdate.OnNext(GearUpdateType.SupplyPower);
        }

        // 導出済みの現在値を受け取り、クライアント送信用のステート詳細へシリアライズする
        // Receive already-derived current values and serialize them into a client-facing state detail
        public BlockStateDetail GetBlockStateDetail(RPM rpm, Torque torque, bool isClockwise)
        {
            var stateDetail = new GearStateDetail(isClockwise, rpm.AsPrimitive(), torque.AsPrimitive());
            return new BlockStateDetail(GearStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        public void Destroy()
        {
            _onBlockStateChange.Dispose();
        }
    }

    public enum GearUpdateType
    {
        SupplyPower,
        Rocked,
    }
}
