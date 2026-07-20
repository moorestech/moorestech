# Web UI E2E Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 実装済み Web UI の未担保 wire-to-UI 経路を決定論的 Playwright e2e で埋める。

**Architecture:** mock host の型付き HTTP control が可変 topic snapshot/event を駆動する。既存 action log と topic event 模倣を組み合わせ、主要操作の往復を確認する。

**Tech Stack:** TypeScript, React, Playwright, Node HTTP, ws

## Global Constraints

- `src/` は変更しない。
- `workers: 1` を維持する。
- sleep を使わない。
- skit/tutorial/tutorialAnchor spec は追加しない。
- git commit は行わない。

---

### Task 1: Mock topic controls

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/state.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/wsHandler.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/httpHandler.ts`
- Modify: `moorestech_web/webui/e2e/support/mockControl.ts`

- [ ] 全 topic 購読者と可変 payload を保持する。
- [ ] HUD、pause、localization、challenge、明示 revision の HTTP control を追加する。
- [ ] `ui_state.request` の拒否を `transition_not_allowed` に合わせる。
- [ ] e2e TypeScript compile で型を検証する。

### Task 2: Missing parity specs

**Files:**
- Modify: `e2e/tests/inventory.spec.ts`
- Modify: `e2e/tests/block/blockDetails.spec.ts`
- Modify: `e2e/tests/research.spec.ts`
- Modify: `e2e/tests/uiState.spec.ts`

- [ ] action payload と topic event 後 UI を同じテストで検証する。
- [ ] 対象 spec の Playwright 実行で確認する。

### Task 3: HUD and menu specs

**Files:**
- Create: `e2e/tests/commonHud.spec.ts`
- Create: `e2e/tests/pauseMenu.spec.ts`
- Modify: `e2e/tests/train.spec.ts`

- [ ] C2 topic 表示と context menu action を検証する。
- [ ] PauseMenu action と切断状態を検証する。
- [ ] train の既存担保と重複しない上流 assertion を補う。

### Task 4: Reconnection, i18n, challenge

**Files:**
- Modify: `e2e/tests/regression/connection.spec.ts`
- Create: `e2e/tests/i18n.spec.ts`
- Modify: `e2e/tests/challenge.spec.ts`

- [ ] restoring と全 topic snapshot 復元を検証する。
- [ ] 旧 revision snapshot が新表示を上書きしないことを検証する。
- [ ] locale別辞書再取得と challenge event 更新を検証する。

### Task 5: Verification and audit

- [ ] `pnpm test`
- [ ] `pnpm build`
- [ ] `pnpm lint`
- [ ] `pnpm exec tsc -p e2e/tsconfig.json --noEmit`
- [ ] 変更差分と監査表をレビューする。
