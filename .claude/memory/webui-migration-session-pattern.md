---
name: webui-migration-session-pattern
description: How to implement moorestech uGUI→Web(React) feature migration with parallel subagents; CEF is broken so videos = Playwright browser recordings
metadata: 
  node_type: memory
  type: project
  originSessionId: d39fe96a-a703-40d0-ad12-1553b5c766da
---

moorestech の uGUI→Web UI 移行 (CEF + React/Tailwind/TS) を subagent-driven + 並列で進めるときの確立パターン (2026-06-14 セッションで 5 機能 INV-2/COM-2/COM-3/INV-6/INV-4+BLK-1 を実装)。

**実行モデル (競合回避が肝)**: 全機能が共有レジストリ(`moorestech_web/webui/src/bridge/protocol.ts`/`payloadTypes.ts`/`src/app/App.tsx`/`e2e/mock-host/*`)を通るため、**コントローラ(メイン)が共有契約+wiring+C# を直列所有**し、**サブエージェントは各機能の新規ファイル(component/css/logic/単体テスト/e2e spec)のみ**作成。各 agent は `pnpm vitest run <file>` で自己検証(node-env, RTL無し→純ロジックのみ単体、component は e2e で担保)。build/playwright/uloop は 1 worktree で競合するのでコントローラが統合時に一括実行。依存(例: INV-6 fluid ↔ INV-4 block registry)は wave 分割。

**動画 = Playwright ブラウザ録画**: CEF(INFRA-1)は `libcef*.dylib` が 131B の Git LFS ポインタで破損 → in-Unity 録画不可。`playwright.config.ts` に `video:"on"` + `workers:1`(mock host のグローバル状態共有のため直列)を入れ、`test-results/*/video.webm` を各機能ごとに収録。

**mock-host の落とし穴**: 全面 backdrop を持つ overlay (modal の `fixed inset-0 z-50`) や開いた panel を **接続時に常時配信すると他テストのクリックを intercept** して大量失敗。→ overlay 系は **既定 OFF + `/__block`・`/__modal` 等のテスト専用 HTTP 制御で opt-in**、各 spec は afterEach で OFF に戻す。`received` action ログはテスト横断で蓄積されるので、同一 action type を複数テストが出すなら `.find` でなく **最新(`.at(-1)`)** を見る。

**C# host 追加 (`Client.WebUiHost`)**: Topic=`ITopicHandler.GetSnapshotJsonAsync`+`hub.Publish`、Action=`IActionHandler`、登録は `WebUiGameBinder.Bind()`、DI は `ClientDIContext...Resolve<T>()`。uGUI からデータを取るには **additive な getter/setter/event のみ追加**(凍結方針 D6 維持): 例 `HotBarView.SetSelectIndex`/`ProgressBarView.IsShown/CurrentProgress/OnProgressChanged`/`SubInventoryState.CurrentSubInventory/OnSubInventoryUpdated`。modal は pull-based なので push 用 `WebUiModalService`(UniTaskCompletionSource で respond 解決)を新設。`.cs` 変更後は worktree 用 Unity を `uloop launch` してから `uloop compile`(別 worktree は専用 Unity が要る)。

関連: [[ui-completeness-off-state-overlays]] [[worktree-needs-own-unity]]
