# 配置種別 enum の none 番兵は撤回し PlacementEntry の ctor 必須へ移す

- 日付: 2026-08-23（PR #1232 独立レビュー D6/F26 の裁定・案A採用）
- 決定: `TerrainSurroundEffectType.none` を削除し、`PlacementEntry` を全フィールド private ctor の readonly struct にして
  `CreateTree` / `CreateObject` / `CreateVein` の種別別 static factory で `surroundEffect` を必須引数にする。
  `TilePlacementSlicer.Split` の `case none: throw` も併せて削除する
- 棄却案: (b) 現状維持し `RuntimeConvert.ToTerrainSurroundEffectType` で none を拒否してマスタ変換境界へ検出を前倒しする
- 理由: 代入漏れは「台帳へ来たら例外」ではなくコンパイルエラーで止めるのが本筋。番兵は AGENTS.md の
  「型で表せる義務を実行時チェックへ落とさない」に反する。旧裁定が棄却理由に挙げた波及コスト（配置段7ファイル＋3クラス）は、
  独立レビューで改めて論点に上がったうえで人間が案Aを選んだため、コストを払う側へ判断が更新された
- 補足: none 削除により config POCO の `terrainSurroundEffectType` 既定値は 0（`treeRootPatch`）になるが、
  スキーマ側の当該キーは必須なのでマスタ経由では未設定になりえない。台帳到達前の代入漏れは ctor 必須化で塞がれる
- supersedes: [[2026-08-23-配置種別enumにnone番兵を置いて代入漏れを例外にする.md]]
