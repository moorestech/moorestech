using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通アイテムハッチ。入力面から受け、出力面の接続先へ毎tick中継し搬送レートを公開する
    // Wall-piercing item hatch: accepts on the input face, relays to output-side targets each tick, reports throughput
    public class CleanRoomItemHatchComponent : IBlockInventory, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomItemHatch
    {
        public string SaveKey => SaveKeyStatic;
        public static string SaveKeyStatic { get; } = typeof(CleanRoomItemHatchComponent).FullName;

        // レート窓長（tick）。1.0秒 = 20tick。RecentThroughputPerSecond の分母に使う
        // Rate-window length in ticks (1.0s = 20 ticks); denominator for RecentThroughputPerSecond
        public const int HatchRateWindowTicks = 20;

        // 中継待ちバッファの上限スタック数（0.2 確定値）。満杯時は受け取りを拒否し上流を停滞させる
        // Max in-transit stacks (fixed in §0.2); a full buffer rejects insertion so the upstream stalls
        public const int MaxInTransitStacks = 4;

        // 直近の窓で中継した個数のリングバッファ（合計を窓秒で割る）
        // Ring buffer of relayed counts over the recent window (sum divided by window seconds)
        private readonly int[] _relayedPerTick = new int[HatchRateWindowTicks];
        private int _windowCursor;

        // 中継待ちアイテム（入力面から受けてまだ出力面へ流していない分）
        // In-transit items received on the input face but not yet pushed out
        private readonly List<IItemStack> _inTransit = new();

        private readonly BlockInstanceId _blockInstanceId;
        private readonly BlockConnectorComponent<IBlockInventory> _connector;

        // 直近窓の合計搬送個数 / 窓秒。汚染計量 A_hatch = k_hatch · この値
        // Sum of relayed counts over the window / window seconds; pollution A_hatch = k_hatch * this
        public double RecentThroughputPerSecond
        {
            get
            {
                var sum = 0;
                for (var i = 0; i < _relayedPerTick.Length; i++) sum += _relayedPerTick[i];
                return sum / (HatchRateWindowTicks * GameUpdater.SecondsPerTick);
            }
        }

        public CleanRoomItemHatchComponent(BlockInstanceId blockInstanceId, BlockConnectorComponent<IBlockInventory> connector)
        {
            _blockInstanceId = blockInstanceId;
            _connector = connector;
        }

        // セーブからの復元: 中継中アイテムだけ戻す。レート窓は揮発（ロード後0から再充填）
        // Restore from save: only the in-transit items; the rate window is transient (refills from 0 after load)
        public CleanRoomItemHatchComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, BlockConnectorComponent<IBlockInventory> connector)
            : this(blockInstanceId, connector)
        {
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saved = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(raw);
            if (saved == null) return;
            foreach (var s in saved)
            {
                var stack = s?.ToItemStack();
                if (stack != null && stack.Count > 0) _inTransit.Add(stack);
            }
        }

        // 入力面から受け取り、中継バッファへ積む。満杯時は差し戻す（上限 = MaxInTransitStacks）
        // Accept into the in-transit buffer; hand the stack back when full (cap = MaxInTransitStacks)
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);
            if (itemStack == null || itemStack.Count == 0) return itemStack;
            if (_inTransit.Count >= MaxInTransitStacks) return itemStack;
            _inTransit.Add(itemStack);
            return ServerContext.ItemStackFactory.CreatEmpty();
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inTransit.Count < MaxInTransitStacks;
        }

        // 毎tick: 中継待ちを出力面ターゲットへ押し出し、押し出した個数をレート窓へ記録
        // Each tick: push in-transit items to output-side targets and record the pushed count into the rate window
        public void Update()
        {
            BlockException.CheckDestroy(this);

            var relayedThisTick = AdvanceRelay();
            RecordRate(relayedThisTick);

            #region Internal

            // 中継待ちの各アイテムを接続先へ InsertItem。受け入れられた個数を返す
            // Push each in-transit item to a connected inventory; return accepted count
            int AdvanceRelay()
            {
                var targets = _connector.ConnectedTargets;
                if (targets.Count == 0) return 0;

                var relayed = 0;
                for (var idx = _inTransit.Count - 1; idx >= 0; idx--)
                {
                    var stack = _inTransit[idx];
                    var before = stack.Count;
                    var remain = InsertToAnyTarget(stack, targets);
                    relayed += before - remain.Count;
                    if (remain.Count == 0) _inTransit.RemoveAt(idx);
                    else _inTransit[idx] = remain;
                }
                return relayed;
            }

            IItemStack InsertToAnyTarget(IItemStack stack, IReadOnlyDictionary<IBlockInventory, ConnectedInfo> targets)
            {
                var current = stack;
                foreach (var target in targets)
                {
                    if (current.Count == 0) break;
                    var ctx = new InsertItemContext(_blockInstanceId, target.Value.SelfConnector, target.Value.TargetConnector);
                    current = target.Key.InsertItem(current, ctx);
                }
                return current;
            }

            // リングバッファの現在tick枠に今回の搬送数を入れ、カーソルを進める
            // Write this tick's relayed count into the ring slot and advance the cursor
            void RecordRate(int relayed)
            {
                _relayedPerTick[_windowCursor] = relayed;
                _windowCursor = (_windowCursor + 1) % HatchRateWindowTicks;
            }

            #endregion
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return slot >= 0 && slot < _inTransit.Count ? _inTransit[slot] : ServerContext.ItemStackFactory.CreatEmpty();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            while (_inTransit.Count <= slot) _inTransit.Add(ServerContext.ItemStackFactory.CreatEmpty());
            _inTransit[slot] = itemStack;
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inTransit.Count;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var serialized = _inTransit.Select(s => new ItemStackSaveJsonObject(s)).ToList();
            return JsonConvert.SerializeObject(serialized);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
