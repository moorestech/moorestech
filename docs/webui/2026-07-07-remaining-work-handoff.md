# 申し送り: ブロック操作パリティ実装の残作業（2026-07-07 03:20）

Phase 0〜2 実行計画（`docs/superpowers/plans/2026-07-07-webui-parity-phase0-2.md`）のうち
**Task 0〜8 は完了・全コミット済み**。残るは **Task 9 の一部（PlayModeスモークと台帳クローズ）のみ**。

## 完了済みの状態（このまま信じてよい）

- Task 0: ツール副産物3ファイルの chore コミット（`7a10ba5ed`）
- Task 1: 台帳一本化 — TODO.md に「2a. 操作・表示パリティ台帳」新設、種リスト移設注記、AGENTS.md 洗い出し行削除（`a044c05d4`）
- Task 2: 状態管理適正化クローズ — grep 3本で topicStore 単一書き込み口を検証、feature側違反ゼロ、AGENTS.md タスク節削除（`38226d191`）
- Task 3: `planDirectMoves`＋InventoryPanel（`84c71209b`）/ Task 4: protocol型＋blockLogic純関数（`fb7b99244`）/
  Task 5: BlockItemGrid配線（`4c2688af3`）/ Task 6: C# `block_inventory.collect`（`da10378e4`）/
  Task 7: mock-host（`2db1d300e`）/ Task 8: e2e 5ケース（`727d6fa08`）
- **QA全green確認済み**: uloop compile Error 0 / C#テスト 57/57 / tsc 0（pnpm build）/ vitest 131 / **e2e 39**（既存34＋新規5）
- 実装は Codex CLI へ委譲し、各タスクで diff全件レビュー＋検証コマンド再実行を実施済み（計画からの逸脱なし）。
  Codex セッションID: `019f3895-71e8-7801-b5e7-0124c0cc3ed8`（修正依頼を出す場合は
  `node ~/.agents/skills/external-implement/scripts/codex-implement.mjs --session <ID> --task "..."` で同一セッション継続）

## 残作業（実行計画 Task 9 の Step 3〜5）

手順の完全版は実行計画の「Task 9: 統合QAゲート・PlayModeスモーク・台帳クローズ」を参照。Step 1〜2（web/C# QA）は上記のとおり実施済み。

### 1. PlayMode スモーク（`unity-playmode-recorded-playtest` スキルで実施）
確認する3点:
1. **PlayMode起動が正常完走すること** — InitializeScenePipeline 分割後の起動スモーク未実施分。readiness ポーリング＋`uloop get-logs --log-type Error` で確認
2. **Esc でブロックUI（SubInventory）が閉じること** — uGUI `SubInventoryState` の CloseUI 経路が webモードでも生きているかの実機確認。チェスト設置→SubInventory 遷移→`Keyboard.current` へ `QueueStateEvent(Key.Escape)` 注入
3. （可能なら）webモードのブロックパネルで右クリック半分取り・Shift移動の実機確認。**CEFオーバーレイへのマウス入力が QueueStateEvent で駆動できるかは未確認**で、困難なら descope してよい（web パネル挙動は mock-host e2e 39件で担保済み。実機 web↔host 連携検証は TODO.md 3節の INFRA-1 解消後タスクとして既に記録されている）

前提・注意（スキル本文＋メモリ `uloop-playtest-pitfalls.md` より）:
- uloop CLI Loop サーバは**起動確認済み**（`uloop list --project-path ./moorestech_client` が通る）
- PlayMode 突入前に NoSave フラグ: `SessionState.SetBool("moorestech_SkipSaveLoadPlayMode", true)`
- 入力注入は `InputSystem.QueueStateEvent` 一択。`uloop simulate-*` は絶対に使わない（注入汚染・PlayMode再起動でしか回復しない）
- EDC スニペットの API 名は書く前に実コードで実在確認（存在しないAPIが最頻出エラー）
- ポーリングは「エラー検知＞成功検知＞待機」の until ループで（`Result` 空は待たずに abort）

※前セッションで Step 0 探索（readiness条件・チェスト設置API・SubInventory強制遷移・Esc入力経路・CEF入力可否）を
Explore サブエージェントに委譲したが、**報告受領前に中断**。再開時は同じ Step 0 探索からやり直すこと
（調査項目は実行計画 Task 9 と本ドキュメントの上記3点）。

### 2. 台帳クローズ（TODO.md）
- 2a の Esc 行をスモーク結果で書き換え: 動作すれば
  `- [x] Esc でのブロックUIクローズ: uGUI SubInventoryState 経由で動作確認済み（2026-07-07 PlayModeスモーク）`、
  動かなければ `- [ ]` のまま実測結果と原因観察を追記して実装タスク化
- 「### 3. 検証」に1行追記:
  `- [x] InitializeScenePipeline 分割後の PlayMode 起動スモーク（2026-07-07、ブロック操作パリティ検証と同時実施）`
- 4ジェスチャ＋e2e の4項目は**チェック済み**（本コミットで反映済み）

### 3. 最終コミットと clean 確認
```bash
git add docs/webui/TODO.md && git commit -m "docs(webui): ブロック操作パリティ完了を台帳へ反映、PlayModeスモーク結果を記録"
git status --short   # uloop 副産物が再度 dirty なら chore コミット
```

## その後の次フェーズ（今回スコープ外・着手順）

台帳 2a の優先度2以降。Phase 3（ギア伝達系レジストリ＋ElectricToGearGenerator）から。
ロードマップは `2026-07-07-parity-implementation-plan.md`、着手時は writing-plans 形式で個別計画を作ること。
⚠ Phase 3 では「GearEnergyTransformer キー追加」は誤り — v8 blocks.json の該当 addressablesPath から blockType を再列挙（正解は Shaft / Gear / GearChainPole）。
