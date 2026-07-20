# WebUI ブロック詳細×5 + 研究ツリー 進捗メモ

**最終更新**: 2026-07-07 01:30 JST
**ブランチ**: feature/webui-block-research-ui
**worktree**: /Users/katsumi/moorestech-worktrees/tree2/.claude/worktrees/webui-blocks
**元計画**: docs/superpowers/plans/2026-07-06-webui-block-research-ui.md

---

## 状態: 全タスク完了（Task 1〜14 + web-ui 統合）

### Task 1〜13 — コミット済み（前セッションまで）
詳細は git log 参照。Task 13（研究ツリー）は `38147474a`。

### Task 14（e2e + ドキュメント）— ✅ 完了・コミット `c3763349b`
- e2e 3本新規: blockDetails / filterSplitter / research（+ ItemSlot に testId prop、フィルタスロットへ `filter-slot-<dir>-<slot>` 付与）
- QAで発見・修正した実バグ1件: research.spec の afterEach が `state=GameScreen` に戻していたため、mock 既定（PlayerInventory）を期待する後続 uiState.spec に状態が漏れて fail。`PlayerInventory` リセットへ修正
- docs/webui/TODO.md: BLK-2/3/4/5/8・RES-1 を実装済みへ消し込み、payload 拡充済み（BlockDetailDtoBuilder）の実態を反映
- 設計仕様書の research.tree 記述は既に「ui_state.current から導出」になっており修正不要だった

### web-ui 並行セッションの統合 — ✅ 完了（マージ3回: `66a3648ef`, `dde67d60b`, `055dc951a`）
3回目の要点: web-ui の `8ea8c9f18`（BlockInventoryTopic 流体/進捗実配信）は、本ブランチの `BlockDetailDtoBuilder` が同一 StateDetail から既に配信済みのため**完全な機能重複**。二重充填を避けるため BlockInventoryTopic.cs と asmdef は ours（本ブランチ版）で解決し、web-ui 側実装は破棄（`BlockSubInventorySource` の新アクセサ3件は未使用のまま残置・コンパイル無害）。web-ui が並行で作業を続けているため、**合流時は再度 `git log HEAD..web-ui` を確認すること**
web-ui 側の大規模リファクタ（bridge/ の transport/store/contract 3層分割、SubInventoryState の UniRx 化、mock-host server.ts のモジュール分割）を取り込み:
- 衝突解決6ファイル（BlockInventoryTopic.cs は UniRx Subscribe + networkCache 追跡を合成）
- 本ブランチ新規ファイル17+3件の旧 bridge パス import を新パスへ一括追従
- `src/bridge/validators.test.ts` を `contract/` へ移動
- mock-host 分割構造（httpHandler/wsHandler/state）へ研究・ブロック詳細5種の拡張を移植

### 検証（2026-07-07 マージ3回目後に全て再実行）
- `pnpm vitest run` = **122 pass** / `pnpm exec tsc --noEmit` = clean / `tsc -p e2e/tsconfig.json` = clean
- `pnpm exec playwright test` = **34 pass**（新規3 spec 含む全 suite）
- **未実施**: C# コンパイル（`uloop compile`）。Unity Editor 未起動で接続不可。BlockInventoryTopic.cs のマージ解決は API 照合済み（OnSubInventoryUpdated=IObservable, OnStateChanged=event, binder 登録残存）だが、**Unity 起動後に要コンパイル確認**

---

## 保留・残タスク
- **報酬アイテムの個数表示**（研究ツリー）: ワイヤ型 `rewardItemIds: number[]` が個数を落としている。Task 2/4 相当 + C# 配信側 + WireFixtures の一式変更が必要
- **C# コンパイル確認**: Unity Editor 起動後に `uloop compile --project-path ./moorestech_client`
- 全体タスク（AGENTS.md「WebUIでやること」）: all-code-review / 状態管理適正化 / 実装漏れ洗い出し（→ web-ui セッションが `docs/webui/2026-07-06-all-code-review-progress.md` で進行中）

## 未コミットの無関係変更
`moorestech_client/.uloop/tools.json`（M）— uloop バージョン管理ファイル。コミット対象外
