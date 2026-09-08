# Webセクションの表示分岐はJSX直判定にし共有行はdetails/rowsへ置く

- 日付: 2026-09-06 / 出所: AskUserQuestion（PR #1323 レビュー D4・D7）
- 決定: `pumpSectionDisplay` ヘルパと型と専用テストを削除し、PumpSection は `pumpingFluids.length > 0` の1変数で1分岐にする（姉妹 MinerSection/GearSection と同形）。Machine/Miner/Pump で写しになっている「稼働ラベル＋充足率」行と「アイコン＋分間レート」行は `details/rows/` を新設して `MachineStateRow` / `PerMinuteRateRow`（＋既存 PowerRateText）として共有する
- 棄却案: D4 (B) 判別 union にして網羅分岐 / D7 (A) 既存 PowerRateText.tsx に追記 / (C) 共有化は別PR
- 理由: 相補2boolの型は排他を保証せず、行の写しは片側更新で油井だけ表記がずれる
