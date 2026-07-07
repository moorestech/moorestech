using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Connector;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     アイテムを中継しつつ搬送レートを汚染計算へ公開するハッチ
    ///     Hatch that relays items and exposes its throughput to pollution
    /// </summary>
    public class CleanRoomItemHatchComponent : IBlockInventory, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomItemHatch
    {
        public const int TransitSlotCount = 4;
        public const int ThroughputWindowTicks = 20;

        // リング窓の合計を窓の実時間で割って毎秒レートにする
        // Divide the ring-window sum by the window's wall time for a per-second rate
        public double RecentThroughputPerSecond => _windowSum / (ThroughputWindowTicks * GameUpdater.SecondsPerTick);

        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly IItemStack[] _transitSlots = new IItemStack[TransitSlotCount];
        private readonly int[] _pushedPerTick = new int[ThroughputWindowTicks];
        private int _windowIndex;
        private int _windowSum;

        public CleanRoomItemHatchComponent(IBlockInventoryInserter blockInventoryInserter)
        {
            _blockInventoryInserter = blockInventoryInserter;
            for (var i = 0; i < TransitSlotCount; i++) _transitSlots[i] = ServerContext.ItemStackFactory.CreatEmpty();
        }

        public CleanRoomItemHatchComponent(Dictionary<string, string> componentStates, IBlockInventoryInserter blockInventoryInserter) : this(blockInventoryInserter)
        {
            // セーブ済みの中継スタックをスロット順に復元する
            // Restore saved in-transit stacks in slot order
            var itemJsons = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(componentStates[SaveKey]);
            for (var i = 0; i < TransitSlotCount && i < itemJsons.Count; i++) _transitSlots[i] = itemJsons[i].ToItemStack();
        }

        public void Update()
        {
            CheckDestroy(this);

            // 中継バッファ全スロットを接続先へ押し出し、押し出せた個数を数える
            // Push every transit slot to connected targets, counting items actually moved
            var pushedThisTick = 0;
            for (var i = 0; i < TransitSlotCount; i++)
            {
                var before = _transitSlots[i];
                if (before.Id == ItemMaster.EmptyItemId) continue;
                var after = _blockInventoryInserter.InsertItem(before);
                pushedThisTick += before.Count - after.Count;
                _transitSlots[i] = after;
            }

            // リング窓を1tick進め、最古の記録を今tickの実績で置き換える
            // Advance the ring window one tick, replacing the oldest record with this tick's count
            _windowIndex = (_windowIndex + 1) % ThroughputWindowTicks;
            _windowSum -= _pushedPerTick[_windowIndex];
            _pushedPerTick[_windowIndex] = pushedThisTick;
            _windowSum += pushedThisTick;
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

        public string GetSaveState()
        {
            CheckDestroy(this);

            var itemJsons = new List<ItemStackSaveJsonObject>();
            foreach (var itemStack in _transitSlots) itemJsons.Add(new ItemStackSaveJsonObject(itemStack));
            return JsonConvert.SerializeObject(itemJsons);
        }

        public IItemStack GetItem(int slot) { CheckDestroy(this); return _transitSlots[slot]; }
        public void SetItem(int slot, IItemStack itemStack) { CheckDestroy(this); _transitSlots[slot] = itemStack; }
        public int GetSlotSize() { CheckDestroy(this); return TransitSlotCount; }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
