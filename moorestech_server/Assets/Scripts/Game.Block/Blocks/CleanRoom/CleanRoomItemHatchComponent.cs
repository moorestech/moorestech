using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Connector;
using Game.Block.Interface.Component;
using Game.Context;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     アイテムを毎秒の上限まで中継しつつ搬送レートを汚染計算へ公開するハッチ
    ///     Hatch that relays items up to a per-second cap and exposes its throughput to pollution
    /// </summary>
    public class CleanRoomItemHatchComponent : IBlockInventory, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomItemHatch, IGetRefundItemsInfo
    {
        public const int TransitSlotCount = 4;
        public const int ThroughputWindowTicks = 20;

        // 1tick分ずつ足す枠の浮動小数誤差で1個を取りこぼさないための余裕
        // Margin so floating-point error in the per-tick accrual never drops a whole item
        private const double AllowanceFloorTolerance = 1e-9;

        // リング窓の合計を窓の実時間で割って毎秒レートにする
        // Divide the ring-window sum by the window's wall time for a per-second rate
        public double RecentThroughputPerSecond => _windowSum / (ThroughputWindowTicks * GameUpdater.SecondsPerTick);

        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly IItemStack[] _transitSlots = new IItemStack[TransitSlotCount];
        private readonly int[] _pushedPerTick = new int[ThroughputWindowTicks];
        private readonly double _maxItemsPerSecond;
        private readonly double _allowanceCapacity;
        private double _pushAllowance;
        private int _windowIndex;
        private int _windowSum;

        public CleanRoomItemHatchComponent(double maxItemsPerSecond, IBlockInventoryInserter blockInventoryInserter)
        {
            _blockInventoryInserter = blockInventoryInserter;
            _maxItemsPerSecond = maxItemsPerSecond;

            // 上限が毎秒1個未満でも1個は貯まるよう、貯められる量は1秒分と1個の大きい方にする
            // Store the larger of one second's worth and one item so a sub-one cap still lets an item through
            _allowanceCapacity = Math.Max(maxItemsPerSecond, 1d);
            _pushAllowance = _allowanceCapacity;
            for (var i = 0; i < TransitSlotCount; i++) _transitSlots[i] = ServerContext.ItemStackFactory.CreatEmpty();
        }

        public CleanRoomItemHatchComponent(Dictionary<string, object> componentStates, double maxItemsPerSecond, IBlockInventoryInserter blockInventoryInserter) : this(maxItemsPerSecond, blockInventoryInserter)
        {
            // セーブ済みの中継スタックをスロット順に復元する
            // Restore saved in-transit stacks in slot order
            var itemJsons = BlockComponentStateReader.Read<List<ItemStackSaveJsonObject>>(componentStates, SaveKey);
            for (var i = 0; i < TransitSlotCount && i < itemJsons.Count; i++) _transitSlots[i] = itemJsons[i].ToItemStack();
        }

        public void Update()
        {
            CheckDestroy(this);

            // 搬送枠へ1tick分を足し、貯められる量を超えた分は捨てる
            // Add one tick of allowance, discarding whatever exceeds the storable amount
            _pushAllowance = Math.Min(_allowanceCapacity, _pushAllowance + _maxItemsPerSecond * GameUpdater.SecondsPerTick);

            // 枠の範囲で中継スロットを接続先へ押し出し、押し出せた個数を数える
            // Push transit slots to connected targets within the allowance, counting items actually moved
            var pushedThisTick = 0;
            for (var i = 0; i < TransitSlotCount; i++)
            {
                var allowedCount = (int)Math.Floor(_pushAllowance + AllowanceFloorTolerance);
                // silent-ok: 枠を超えた分は中継スロットに残り、満杯なら InsertItem が差し戻してベルトの出口で待つ（搬送量の上限そのもの） / プレイヤーがハッチ手前の詰まりと搬送の頭打ちで気づく
                if (allowedCount <= 0) break;
                var before = _transitSlots[i];
                if (before.Id == ItemMaster.EmptyItemId) continue;

                var pushedCount = PushUpTo(before, allowedCount);
                _transitSlots[i] = before.SubItem(pushedCount);
                _pushAllowance -= pushedCount;
                pushedThisTick += pushedCount;
            }

            // リング窓を1tick進め、最古の記録を今tickの実績で置き換える
            // Advance the ring window one tick, replacing the oldest record with this tick's count
            _windowIndex = (_windowIndex + 1) % ThroughputWindowTicks;
            _windowSum -= _pushedPerTick[_windowIndex];
            _pushedPerTick[_windowIndex] = pushedThisTick;
            _windowSum += pushedThisTick;

            #region Internal

            int PushUpTo(IItemStack stack, int allowedCount)
            {
                // 枠を超える分は渡さず、渡した分から受け取られなかった残りを引く
                // Offer no more than the allowance and subtract whatever the targets handed back
                var offered = stack.Count <= allowedCount ? stack : stack.SubItem(stack.Count - allowedCount);
                var rest = _blockInventoryInserter.InsertItem(offered);
                return offered.Count - rest.Count;
            }

            #endregion
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            CheckDestroy(this);

            // 空きスロットが無ければ受け取らず、そのまま差し戻す
            // With no free slot the stack is rejected and returned unchanged
            for (var i = 0; i < TransitSlotCount; i++)
            {
                if (_transitSlots[i].Id != ItemMaster.EmptyItemId) continue;
                _transitSlots[i] = itemStack;
                return ServerContext.ItemStackFactory.CreatEmpty();
            }

            return itemStack;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);

            var emptySlotCount = 0;
            for (var i = 0; i < TransitSlotCount; i++)
                if (_transitSlots[i].Id == ItemMaster.EmptyItemId)
                    emptySlotCount++;

            return itemStacks.Count <= emptySlotCount;
        }

        public string SaveKey { get; } = typeof(CleanRoomItemHatchComponent).FullName;

        public object GetSaveState()
        {
            CheckDestroy(this);

            var itemJsons = new List<ItemStackSaveJsonObject>();
            foreach (var itemStack in _transitSlots) itemJsons.Add(new ItemStackSaveJsonObject(itemStack));
            return itemJsons;
        }

        // 撤去時は中継中のスタックをそのまま返す
        // On removal the in-transit stacks are refunded as they are
        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            CheckDestroy(this);
            var items = new List<IItemStack>();
            foreach (var itemStack in _transitSlots)
                if (itemStack.Id != ItemMaster.EmptyItemId) items.Add(itemStack);
            return items;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
