using System;
using Core.BeltTransport;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Statistics
{
    // 全segmentの搬出数を、5→3→3→2→2→2→2→2→2→2の層で畳む統計。層の中身はsegmentごとに約33バイトで、添字はsegment番号
    // Exit counts of every segment folded through 5→3→3→2→2→2→2→2→2→2 layers; about 33 bytes per segment, indexed by segment number
    // 最下層は5tickぶんの個数(0〜5)、次の2層は3スロットを4bitずつ詰め、45tick以上の層は2スロットのushort。窓は上の層の2スロットの和
    // The lowest layer counts a 5-tick block (0..5), the next two pack three 4-bit slots, layers of 45 ticks and up hold two ushort slots; a window is the sum of a layer's two slots
    // スロット位置はsegmentごとに持たず、リセットからの経過tickだけで全segment共通に決める。境界tickに連続配列を頭から走査して上へ押し上げる
    // Slot positions are not stored per segment; the ticks since reset decide them for all segments alike, and boundary ticks stream the arrays to push sums upward
    public sealed class BeltSegmentExitStatisticsTable : IBeltSegmentExitObserver
    {
        private const int LowestBlockTicks = 5;
        private const int NibbleSlots = 3;
        private const int PairSlots = 2;
        private const int PairsPerSegment = BeltSegmentExitWindows.LayerCount * PairSlots;

        private byte[] _block5 = Array.Empty<byte>();
        private ushort[] _block15 = Array.Empty<ushort>();
        private ushort[] _block45 = Array.Empty<ushort>();
        private ushort[] _pairs = Array.Empty<ushort>();
        private int _segmentCount;
        private readonly Subject<Unit> _onWindowAdvanced = new();

        // リセットからの完了tick数。窓の長さ以上になった窓だけが答えられる
        // Ticks completed since the reset; only windows no longer than this can be answered
        public int CompletedTicks { get; private set; }

        // 45tickブロックが1つ完成するたびに流れる。表示側は購読して出し直す
        // Fires each time a 45-tick block completes; the display subscribes and republishes
        public IObservable<Unit> OnWindowAdvanced => _onWindowAdvanced;

        // 複製の組み直しごとに呼ぶ。履歴は全部捨てる(ユーザー裁定 2026-10-20: 組み直し追従はせず全リセット)
        // Called on every replica re-assembly; all history is dropped (user ruling 2026-10-20: no rebuild tracking, full reset)
        public void Reset(int segmentCount)
        {
            if (_segmentCount != segmentCount)
            {
                _block5 = new byte[segmentCount];
                _block15 = new ushort[segmentCount];
                _block45 = new ushort[segmentCount];
                _pairs = new ushort[segmentCount * PairsPerSegment];
                _segmentCount = segmentCount;
            }
            else
            {
                Array.Clear(_block5, 0, _block5.Length);
                Array.Clear(_block15, 0, _block15.Length);
                Array.Clear(_block45, 0, _block45.Length);
                Array.Clear(_pairs, 0, _pairs.Length);
            }
            CompletedTicks = 0;
        }

        public void OnExited(int handle)
        {
            _block5[handle]++;
        }

        // 複製を1tick進めた直後に呼ぶ。境界に当たった層だけ下から順に押し上げる(上の層は同じtickに書かれた下の2スロットを読む)
        // Called right after the replica advanced one tick; only layers at a boundary push, bottom up (an upper layer reads the two lower slots written this same tick)
        public void EndTick()
        {
            CompletedTicks++;
            if (CompletedTicks % LowestBlockTicks != 0) return;
            PushBlock5();
            if (CompletedTicks % (LowestBlockTicks * NibbleSlots) != 0) return;
            PushBlock15();
            if (CompletedTicks % BeltSegmentExitWindows.PairBaseBlockTicks != 0) return;
            PushBlock45();
            for (var layer = 1; layer < BeltSegmentExitWindows.LayerCount; layer++)
            {
                if (CompletedTicks % BeltSegmentExitWindows.BlockTicksOfLayer(layer) != 0) break;
                PushPairLayer(layer);
            }
            _onWindowAdvanced.OnNext(Unit.Default);
        }

        // 窓の搬出数。窓がまだ埋まっていない間はfalse(計測中)
        // The window's exit count; false while the window is not yet filled (still measuring)
        public bool TryGetExitCount(int segmentIndex, int windowTicks, out int exitCount)
        {
            exitCount = 0;
            if (!BeltSegmentExitWindows.TryGetLayer(windowTicks, out var layer))
            {
                Debug.LogError($"[BeltSegmentExitStatistics] {windowTicks} is not a window this table holds");
                return false;
            }
            if (CompletedTicks < windowTicks) return false;
            var first = segmentIndex * PairsPerSegment + layer * PairSlots;
            exitCount = _pairs[first] + _pairs[first + 1];
            return true;
        }

        private void PushBlock5()
        {
            var shift = NibbleShift(CompletedTicks / LowestBlockTicks);
            for (var i = 0; i < _segmentCount; i++)
            {
                _block15[i] = WriteNibble(_block15[i], shift, _block5[i]);
                _block5[i] = 0;
            }
        }

        private void PushBlock15()
        {
            var shift = NibbleShift(CompletedTicks / (LowestBlockTicks * NibbleSlots));
            for (var i = 0; i < _segmentCount; i++)
            {
                _block45[i] = WriteNibble(_block45[i], shift, SumNibbles(_block15[i]));
            }
        }

        private void PushBlock45()
        {
            var slot = PairSlot(CompletedTicks / BeltSegmentExitWindows.PairBaseBlockTicks);
            for (var i = 0; i < _segmentCount; i++)
            {
                _pairs[i * PairsPerSegment + slot] = (ushort)SumNibbles(_block45[i]);
            }
        }

        private void PushPairLayer(int layer)
        {
            var slot = PairSlot(CompletedTicks / BeltSegmentExitWindows.BlockTicksOfLayer(layer));
            var lower = (layer - 1) * PairSlots;
            var upper = layer * PairSlots + slot;
            for (var i = 0; i < _segmentCount; i++)
            {
                var first = i * PairsPerSegment;
                _pairs[first + upper] = (ushort)(_pairs[first + lower] + _pairs[first + lower + 1]);
            }
        }

        // 完成したブロック番号(1始まり)から、その層の書き込み先を決める
        // The completed block number (from 1) decides the slot written in that layer
        private static int NibbleShift(int completedBlock)
        {
            return ((completedBlock - 1) % NibbleSlots) * 4;
        }

        private static int PairSlot(int completedBlock)
        {
            return (completedBlock - 1) % PairSlots;
        }

        private static ushort WriteNibble(ushort packed, int shift, int value)
        {
            return (ushort)((packed & ~(0xF << shift)) | (value << shift));
        }

        private static int SumNibbles(ushort packed)
        {
            return (packed & 0xF) + ((packed >> 4) & 0xF) + ((packed >> 8) & 0xF);
        }
    }
}
