# earnItem ピン解決は MapObjectMaster の lookup API へ寄せ、0件をマスタ検証で弾く

日付: 2026-08-22 / 文脈: ADR 0029 実装 Task 1 のレビュー指摘裁定

## 決定

1. `MapObjectPinTargetResolver` は `MasterHolder.MapObjectMaster.Map.MapObjects` を直接LINQ走査しない。
   `MapObjectMaster` に `GetMapObjectGuidsByEarnItem(Guid itemGuid)` を新設し、Resolver はそこへ委譲する。
2. `ChallengeMasterUtil` の earnItem 検証は itemGuid の実在だけでなく
   「そのアイテムを earnItems に持つ mapObject が1件以上あるか」も検証してログを出す。

## 棄却した案

- plan（2026-08-22-tutorial-equip-challenge-pin-target-and-hints.md）Step 7 の逐語どおり Resolver 内で直接走査を維持する案。
- plan Step 6 の「参照先の実在だけを検証する」を正として 0件チェックを足さない案。

## 理由

- 直接走査は repo 全体で唯一の生データ直参照であり、`MapObjectMaster` が lookup API を所有する前例から無言で乖離する。
- 0件を通すと実行時に毎フレーム `未破壊のMapObject` LogError ＋ピン不表示という無言故障になる。
  マスタデータ防御レンズに従い、起動時のマスタ検証で前倒しする。

## リンク

- ADR: docs/adr/0029-tutorial-equip-challenge-pin-target-and-hints.md
- レビュー報告: .superpowers/sdd/task-1-review.md
