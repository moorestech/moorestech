# 0039 採掘機の鉱脈判定は底面フットプリントのXZ重なりで共有ロジック1本にする

## Context
自動生成マップの鉱脈AABBは生成点中心の3×3×3（ADR 0023）。採掘機の設置可否はクライアント`MinerVeinPlacementReporter`が、採掘対象はサーバー`VanillaMinerProcessorComponent.SetMiningItem`が、それぞれ`drillLocalPosition`（全機種[0,0,0]）を向きで換算した1セルでAABB inclusive判定していた。2×2採掘機は`floor(hit+0.5)-size/2`でカーソル中心に置かれ、ドリルセルは向きで角が変わるため、表示ボックス3セルのうち2辺の0.5m帯（面積約31%）が「見た目は中なのに不可」になる。加えてQ/Eの`HeightOffset`が残留し、斜面では`floor(hit.y)`がAABBのY±1から外れる。

## Decision
- 共有判定 `MinerVeinFootprintJudge`（`Game.Block.Interface`）を新設: `BlockPositionInfo`（MinPos/MaxPos）と鉱脈のmin/maxセルを受け、XZのみのinclusive重なりを返す。Yは見ない
- サーバー`IItemMapVeinDatastore`に`GetVeinsOverlappingFootprint(BlockPositionInfo)`を追加し`SetMiningItem`はこれを使う（重なった全veinが採掘対象。順序は既存どおり先頭で採掘時間）。`GetOverVeins(Vector3Int)`は手掘り用途に残す
- クライアント`MapVeinAabbRegistry.IsOverlappingFootprint(BlockPositionInfo, MapVeinKind)`を追加し`MinerVeinPlacementReporter`はセル毎に`BlockPositionInfo`を組んでこれを呼ぶ
- `CommonBlockPlaceDragState.SyncSelectedBlock`で選択ブロックが変わったら`HeightOffset = 0`
- `drillLocalPosition`をblocks.yml（3箇所）・blocks.json（本番/テストMod）・`BlockMasterUtil.MinerDrillLocalPositionValidation`・関連テストから削除。moorestech_master側はPRを出しピンを更新

出所: ユーザー裁定 2026-08-28（`.decisions/2026-08-28-採掘機の設置可否は底面フットプリントのXZ重なりで決めYは見ない.md`）。判定クラスの配置先・API名・「重なった全veinを採掘対象」は agent前提（既存SetMiningItemが複数veinを全て追加していた前例に従う）

## Considered Options
`.decisions/` 同ファイルの棄却案を参照（ドリル1セル維持＋表示合わせ／判定のみ+1緩和／Y判定維持／HeightOffset採掘機限定・据え置き／drillLocalPosition残置）

## Consequences
- 表示ボックスと設置可能範囲が一致する。フットプリントが2つのAABBに跨ると両方の鉱石を掘る
- 地下・空中に置いた採掘機もXZが重なれば掘れる（現状用途では影響なし）
- ADR 0023のAABB Y±1は露頭・手掘り用としては維持
- 既存セーブの採掘機はロード時にフットプリント基準で採掘対象を引き直す（8/25裁定の割り切りと同性質）
