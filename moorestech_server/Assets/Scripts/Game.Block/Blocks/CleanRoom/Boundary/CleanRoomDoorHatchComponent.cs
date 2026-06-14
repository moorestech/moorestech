using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // クリーンルームのドアハッチ。通過バーストを溜め、自前tickのlatchで1tick分だけ公開する（peekは非破壊）
    // Clean-room door hatch: accumulates passage bursts and latches them for exactly one tick (peek is non-destructive)
    public class CleanRoomDoorHatchComponent : IUpdatableBlockComponent, ICleanRoomDoorHatch
    {
        // プレイヤー1通過あたりの瞬間加算量（balance §2: burst_door。個/通過＝A_totalに混ぜない）
        // Per-passage instantaneous addition (balance §2: burst_door; per-passage units, never folded into A_total)
        public const double DoorPassageBurst = 15.0;

        // tick間に発生した通過の累積（次の latch で pending へ移る）
        // Passages accumulated between ticks (moved into pending at the next latch)
        private double _incomingBurst;

        // 今tickに各部屋へ計上されるべきバースト（peek は非破壊）
        // Burst visible to room evaluation this tick (peek is non-destructive)
        private double _pendingBurst;

        // 統合seam: 将来の座標watcher/クライアント通知がプレイヤー通過時に呼ぶ。多重通過は合算
        // Integration seam: a future coordinate watcher / client notification calls this; multiple passages accumulate
        public void NotifyPlayerPassage()
        {
            _incomingBurst += DoorPassageBurst;
        }

        // データストアが部屋ごとに読む。非破壊なので面する全部屋が全額を計上できる（0.5 の共有境界規則）
        // Read per room by the datastore; non-destructive so every facing room books the full amount (§0.5)
        public double PeekPendingBurst()
        {
            return _pendingBurst;
        }

        // 自前tickの latch（=advance）。評価順に依存せず、公開は正確に1tick分・二重計上なし
        // Self-ticked latch (=advance): order-independent, visible for exactly one tick, never double-booked
        public void Update()
        {
            _pendingBurst = _incomingBurst;
            _incomingBurst = 0;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
