using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Tests.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の見本。ICチップ 2 種（成果は計算データ）とケーブル 1 種（成果はケーブルプール）
    /// Cargo samples: two IC chip kinds (compute data out) and one cable kind (cable pool)
    /// </summary>
    public static class OrbitalCargoSamples
    {
        // L1 で名前と絵を引くので、テスト用マスタにある小さな ID を使う（既存の画面テストと同じ 1〜4）
        // L1 resolves names and icons, so use small IDs present in the test master (1-4, as existing screen tests do)
        public static readonly ItemId ChipLv1 = new(1);
        public static readonly ItemId ChipLv2 = new(2);
        public static readonly ItemId Cable = new(3);
        public static readonly ItemId ComputeData = new(4);

        public static OrbitalCargoChoice ChipLv1Choice => new(ChipLv1, 20, new[] { new ItemAmount(ComputeData, 20) }, false);
        public static OrbitalCargoChoice ChipLv2Choice => new(ChipLv2, 20, new[] { new ItemAmount(ComputeData, 40) }, false);
        public static OrbitalCargoChoice CableChoice => new(Cable, 1000, Array.Empty<ItemAmount>(), true);

        public static IReadOnlyList<OrbitalCargoChoice> PadCarried => new[] { ChipLv1Choice, ChipLv2Choice, CableChoice };

        // 発射台の詳細（3 種を運び、unlocked だけ解放済み）
        // A launch pad cargo detail carrying three kinds with only `unlocked` released
        public static OrbitalCargoDetail Pad(ItemId? selected, ItemId? currentFlight, params ItemId[] unlocked)
        {
            return new OrbitalCargoDetail(selected, currentFlight, PadCarried, new HashSet<ItemId>(unlocked), 120);
        }
    }
}
