using System;
using Core.BeltTransport;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Statistics
{
    // 全segmentの搬出数の統計。5tickブロック36個のリング(180tick)と、その上に180tickブロックを2つずつ束ねる5層。segmentあたり約53バイト、添字はsegment番号
    // Exit counts of every segment: a ring of 36 five-tick blocks (180 ticks) plus five layers pairing 180-tick blocks above it; about 53 bytes per segment, indexed by segment number
    // 90・180の窓はリングの直近スロットの和を読む時に足す(開いているsegmentだけ)。360以上は層の2スロットの和で、窓の半分ごとに更新される
    // The 90 and 180 windows add up the ring's latest slots at read time (only for the open segment); 360 and up are a layer's two slots, refreshed every half window
    // リングの添字も層の境界も経過tickだけで全segment共通に決める。5tick境界にリングへ1バイト書き、180tick境界にだけ層を押し上げる
    // The ring index and the layer boundaries are decided for all segments alike by the elapsed ticks; a 5-tick boundary writes one byte into the ring, and only a 180-tick boundary pushes the layers
    public sealed class BeltSegmentExitStatisticsTable : IBeltSegmentExitObserver
    {
        private const int RingBlockTicks = BeltSegmentExitWindows.RingBlockTicks;
        private const int RingSlots = BeltSegmentExitWindows.RingSlots;
        private const int PairSlots = 2;
        private const int PairsPerSegment = BeltSegmentExitWindows.PairLayerCount * PairSlots;

        private byte[] _currentBlock = Array.Empty<byte>();
        private byte[] _ring = Array.Empty<byte>();
        // 層ごとに[古い, 新しい]の順
        // Per layer, [older, newer]
        private ushort[] _pairs = Array.Empty<ushort>();
        private int _segmentCount;
        private readonly Subject<Unit> _onWindowAdvanced = new();

        // リセットからの完了tick数。窓の長さ以上になった窓だけが答えられる
        // Ticks completed since the reset; only windows no longer than this can be answered
        public int CompletedTicks { get; private set; }

        // 5tickブロックが1つ完成するたびに流れる。表示側は購読して出し直す
        // Fires each time a 5-tick block completes; the display subscribes and republishes
        public IObservable<Unit> OnWindowAdvanced => _onWindowAdvanced;

        // 複製の組み直しごとに呼ぶ。履歴は全部捨てる(ユーザー裁定 2026-10-20: 組み直し追従はせず全リセット)
        // Called on every replica re-assembly; all history is dropped (user ruling 2026-10-20: no rebuild tracking, full reset)
        public void Reset(int segmentCount)
        {
            if (_segmentCount != segmentCount)
            {
                _currentBlock = new byte[segmentCount];
                _ring = new byte[segmentCount * RingSlots];
                _pairs = new ushort[segmentCount * PairsPerSegment];
                _segmentCount = segmentCount;
            }
            else
            {
                Array.Clear(_currentBlock, 0, _currentBlock.Length);
                Array.Clear(_ring, 0, _ring.Length);
                Array.Clear(_pairs, 0, _pairs.Length);
            }
            CompletedTicks = 0;
        }

        public void OnExited(int handle)
        {
            _currentBlock[handle]++;
        }

        // 複製を1tick進めた直後に呼ぶ。5tick境界でリングへ書き、180tick境界で層を下から順に押し上げる(上の層は同じtickに書かれた下の2スロットを読む)
        // Called right after the replica advanced one tick; a 5-tick boundary writes the ring, a 180-tick boundary pushes the layers bottom up (an upper layer reads the two lower slots written this same tick)
        public void EndTick()
        {
            CompletedTicks++;
            if (CompletedTicks % RingBlockTicks != 0) return;
            WriteRing();
            if (CompletedTicks % BeltSegmentExitWindows.RingTicks == 0)
            {
                PushRingTotal();
                for (var layer = 1; layer < BeltSegmentExitWindows.PairLayerCount; layer++)
                {
                    if (CompletedTicks % BeltSegmentExitWindows.PairBlockTicksOfLayer(layer) != 0) break;
                    PushPairLayer(layer);
                }
            }
            _onWindowAdvanced.OnNext(Unit.Default);
        }

        // 窓の搬出数。窓がまだ埋まっていない間はfalse(計測中)
        // The window's exit count; false while the window is not yet filled (still measuring)
        public bool TryGetExitCount(int segmentIndex, int windowTicks, out int exitCount)
        {
            exitCount = 0;
            if (BeltSegmentExitWindows.IsRingWindow(windowTicks))
            {
                if (windowTicks % RingBlockTicks != 0 || windowTicks <= 0) return UnknownWindow(windowTicks);
                if (CompletedTicks < windowTicks) return false;
                exitCount = SumLatestRingSlots(segmentIndex, windowTicks / RingBlockTicks);
                return true;
            }
            if (!BeltSegmentExitWindows.TryGetPairLayer(windowTicks, out var layer)) return UnknownWindow(windowTicks);
            if (CompletedTicks < windowTicks) return false;
            var first = segmentIndex * PairsPerSegment + layer * PairSlots;
            exitCount = _pairs[first] + _pairs[first + 1];
            return true;
        }

        // リングの直近を slotsPerBucket 個ずつ束ねて古い順に buckets へ写し、埋まった束の数を返す。最新の束は最新スロットで終わる
        // Copies the ring's latest slots bundled slotsPerBucket at a time into buckets, oldest first, and returns how many bundles are filled; the newest bundle ends at the newest slot
        public int ReadRecentExitCounts(int segmentIndex, int slotsPerBucket, int[] buckets)
        {
            var completedBlocks = CompletedTicks / RingBlockTicks;
            var available = Math.Min(buckets.Length, Math.Min(completedBlocks, RingSlots) / slotsPerBucket);
            var first = segmentIndex * RingSlots;
            var slot = LatestRingSlot();
            for (var bucket = available - 1; bucket >= 0; bucket--)
            {
                var sum = 0;
                for (var n = 0; n < slotsPerBucket; n++)
                {
                    sum += _ring[first + slot];
                    slot = slot == 0 ? RingSlots - 1 : slot - 1;
                }
                buckets[bucket] = sum;
            }
            return available;
        }

        private static bool UnknownWindow(int windowTicks)
        {
            Debug.LogError($"[BeltSegmentExitStatistics] {windowTicks} is not a window this table holds");
            return false;
        }

        // 完成した5tickブロック番号(1始まり)が指すリングのスロット
        // The ring slot addressed by the completed 5-tick block number (from 1)
        private int LatestRingSlot()
        {
            return (CompletedTicks / RingBlockTicks - 1) % RingSlots;
        }

        private void WriteRing()
        {
            var slot = LatestRingSlot();
            for (var i = 0; i < _segmentCount; i++)
            {
                _ring[i * RingSlots + slot] = _currentBlock[i];
                _currentBlock[i] = 0;
            }
        }

        // 直近slotCount個のスロットを、最新から逆向きに足す
        // Add the latest slotCount slots, walking backward from the newest
        private int SumLatestRingSlots(int segmentIndex, int slotCount)
        {
            var first = segmentIndex * RingSlots;
            var slot = LatestRingSlot();
            var sum = 0;
            for (var n = 0; n < slotCount; n++)
            {
                sum += _ring[first + slot];
                slot = slot == 0 ? RingSlots - 1 : slot - 1;
            }
            return sum;
        }

        // リング全体(180tick)の和を層0の新しい側へ入れ、古い側へ押し出す
        // Put the whole ring's sum (180 ticks) into layer 0's newer slot, pushing the previous one to the older slot
        private void PushRingTotal()
        {
            for (var i = 0; i < _segmentCount; i++)
            {
                var total = 0;
                var first = i * RingSlots;
                for (var slot = 0; slot < RingSlots; slot++) total += _ring[first + slot];
                var pair = i * PairsPerSegment;
                _pairs[pair] = _pairs[pair + 1];
                _pairs[pair + 1] = (ushort)total;
            }
        }

        private void PushPairLayer(int layer)
        {
            var lower = (layer - 1) * PairSlots;
            var upper = layer * PairSlots;
            for (var i = 0; i < _segmentCount; i++)
            {
                var first = i * PairsPerSegment;
                _pairs[first + upper] = _pairs[first + upper + 1];
                _pairs[first + upper + 1] = (ushort)(_pairs[first + lower] + _pairs[first + lower + 1]);
            }
        }
    }
}
