using System;

namespace Core.BeltTransport
{
    // 走行列・隙間・密着ブロックを管理するsegmentの共通部。speedは0～MaxSpeedで、tick中は固定
    // 通常segmentは段階4、合流・分岐segmentはbufferにより段階1で更新する。外部供給と配線変更はtick境界で行う
    // The shared part of a segment managing the item run, gaps and tightly-packed blocks. Speed is 0..MaxSpeed and fixed during a tick
    // Normal segments update in stage 4, merge/branch segments in stage 1 via their buffer. Supply and wiring change at tick boundaries
    public abstract class BeltConveyorSegment : IBeltReceiver
    {
        private readonly BeltItemRun _run;
        private int _tickSpeed;
        private int Length => Capacity * BeltConstants.ItemWidth;

        public abstract BeltSegmentKind Kind { get; }
        public int Capacity { get; }
        public int Count => _run.Count;

        // blockへ保存する方向の優先順。合流は搬入方向、分岐はbufferの搬出方向、通常は0
        // Direction priority order saved to the block. Input order for a merge, buffer output order for a branch, 0 otherwise
        public virtual int PriorityOrder => 0;
        internal int TickSpeed => _tickSpeed;
        public int Speed { get; private set; }

        // このtickで先頭が出口を越える距離。正数なら搬出可能。空なら0
        // Distance the head passes the exit this tick. Positive means it can be output. 0 when empty
        protected int OutputLength => _run.GetOutputLength(_tickSpeed);

        // 出口に最も近いアイテム。空でないときだけ読む
        // The item nearest the exit. Read only when not empty
        protected BeltItem HeadItem => _run.HeadItem;

        internal BeltConveyorSegment(int capacity, int speed)
        {
            // 占有長がint内に収まる容量だけ許可する
            // Allow only capacities whose occupied length fits in int
            if (capacity <= 0 || capacity > (int.MaxValue - (BeltConstants.ItemWidth - 1)) / BeltConstants.ItemWidth)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (speed < 0 || speed > BeltConstants.MaxSpeed) throw new ArgumentOutOfRangeException(nameof(speed));
            Capacity = capacity;
            Speed = speed;
            _run = new BeltItemRun(capacity);
        }

        // 段階0より前に変更する。tick中は固定。0では走行しない
        // Change before stage 0. Fixed during a tick. 0 means no movement
        public void SetSpeed(int speed)
        {
            if (speed < 0 || speed > BeltConstants.MaxSpeed) throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        // 搬入元を持たない種類は何も記録しない。合流だけが搬入元を保持する
        // Kinds without stored inputs record nothing. Only a merge keeps its inputs
        public virtual void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
        }

        // 入口側の空き距離。最後尾が入口からはみ出している間は負
        // Free length at the entrance. Negative while the last item still sticks out of the entrance
        public virtual int GetOffer(BeltDirection inputDirection)
        {
            return Length - _run.TotalLength;
        }

        // 空き以下の進入距離なら、進入距離を保って末尾へ追加する
        // When the entry length fits the offer, append at the tail keeping that entry length
        public virtual bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            var offer = GetOffer(inputDirection);
            if (length > offer) return false;
            _run.EnqueueTail(offer - length, item);
            return true;
        }

        // 段階0。このtickの速度を固定する
        // Stage 0. Fix the speed used for this tick
        internal void BeginTick()
        {
            _tickSpeed = Speed;
        }

        // tick境界で、出口に近い順のアイテムと出口までの距離を複製する
        // At a tick boundary, copy items in exit order with their distance to the exit
        public BeltItemState[] CaptureItems() => _run.CaptureItems();

        // 再生成した空のsegmentへ出口に近い順のアイテムを復元する。距離・間隔・容量は呼び出し側が確定させる
        // Restore items in exit order into a rebuilt empty segment. The caller settles distances, spacing and capacity
        public void RestoreItems(BeltItemState[] restoredItems) => _run.RestoreItems(restoredItems);

        // 進入距離lengthで入口から入ったアイテムを、現在の最後尾の後ろへ追加する
        // Append an item that entered the entrance by length behind the current last item
        protected void EnqueueAtEntry(int length, in BeltItem item) => _run.EnqueueAtEntry(length, item);

        // 先頭が出口ちょうどにあれば取り出す。残ったアイテムの位置は変えない
        // Take the head when it is exactly at the exit. Remaining items keep their positions
        protected bool TryDequeueHeadAtExit(out BeltItem item) => _run.TryDequeueHeadAtExit(out item);

        // 1tick前進する。sentは先頭の搬出が成立したか
        // Advance one tick. sent tells whether the head item's output succeeded
        protected void Advance(bool sent) => _run.Advance(sent, _tickSpeed);

        // tick境界で走行列を消費し隙間の集計も空へ戻す
        // Consume the running items at a tick boundary and reset their gap totals
        public void ClearItems() => _run.ClearItems();
    }
}
