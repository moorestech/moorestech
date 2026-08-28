# 0039 採掘機の鉱脈判定は底面フットプリントのXZ重なりで共有ロジック1本にする

## Context
自動生成マップの鉱脈AABBは生成点中心の3×3×3（ADR 0023）。採掘機の設置可否はクライアント`MinerVeinPlacementReporter`が、採掘対象はサーバー`VanillaMinerProcessorComponent.SetMiningItem`が、それぞれ`drillLocalPosition`（全機種[0,0,0]）を向きで換算した1セルでAABB inclusive判定していた。2×2採掘機は`floor(hit+0.5)-size/2`でカーソル中心に置かれ、ドリルセルは向きで角が変わるため、表示ボックス3セルのうち2辺の0.5m帯（面積約31%）が「見た目は中なのに不可」になる。加えてQ/Eの`HeightOffset`が残留し、斜面では`floor(hit.y)`がAABBのY±1から外れる。

## Decision
- 共有判定 `MinerVeinFootprintJudge`（`Game.Block.Interface`）を新設: `OverlapsXz(BlockPositionInfo, veinMin, veinMax)`（XZのみinclusive・Yは見ない）と `CanMine(MineSettings, ItemId)`（mineSettings一致のみ掘れる）の2本
- 鉱脈層（`Game.Map.Interface` / `Game.Map`）は採掘機を知らない: `IItemMapVeinDatastore.Veins` で全veinを公開するだけで、絞り込みは呼び出し側（`VanillaMinerProcessorComponent.SetMiningItem`）が判定クラスで行う。`GetOverVeins`は`GetVeinsContainingCell`へ改名し手掘り・ポンプ用途に残す
- 採掘対象は「XZ重なり ∧ mineSettings一致」のvein。同一アイテムは1種1個、採掘時間は一致veinの最遅値（順序非依存）。一致0件なら採掘しない
- クライアント`MapVeinAabb`が`VeinItemId`を持ち、`MinerVeinPlacementReporter`は`Registry.Veins`を同じ判定クラスで絞る（置けるのに掘らない採掘機を作らない）
- `CommonBlockPlaceDragState.SyncSelectedBlock`で選択ブロックが変わったら`HeightOffset = 0`
- `drillLocalPosition`をblocks.yml（3箇所）・blocks.json（本番/テストMod）・`BlockMasterUtil.MinerDrillLocalPositionValidation`・関連テストから削除。moorestech_master側はPRを出しピンを更新

出所: ユーザー裁定 2026-08-28（`.decisions/2026-08-28-採掘機の設置可否は底面フットプリントのXZ重なりで決めYは見ない.md`、`.decisions/2026-08-28-採掘機はmineSettings一致の鉱脈だけ採掘し設置可否も同基準にする.md`）。判定クラスの配置先・API名・「鉱脈層にBlock層依存を張らずVeinsを公開して呼び出し側で絞る」は agent前提（AGENTS.md「汎用基盤にドメイン語彙を持ち込まない」・moores-code-review D2 案A）

## Considered Options
`.decisions/` 同ファイルの棄却案を参照（ドリル1セル維持＋表示合わせ／判定のみ+1緩和／Y判定維持／HeightOffset採掘機限定・据え置き／drillLocalPosition残置）

## Consequences
- 表示ボックスと設置可能範囲が一致する。フットプリントが2つの掘れるAABBに跨ると両方の鉱石を掘る（1種1個）。掘れない鉱脈に跨っても無視される
- 地下・空中に置いた採掘機もXZが重なれば掘れる（現状用途では影響なし）
- ADR 0023のAABB Y±1は露頭・手掘り用としては維持
- 既存セーブの採掘機はロード時にフットプリント基準で採掘対象を引き直す（8/25裁定の割り切りと同性質）
