# writing-plans 型閉包検査（Phase 2.6）Implementation Plan

> **For the controller session:** 本planはスキル文書（Markdown）のみの変更で、コンパイル・テスト対象コードを含まない。作成セッション内で直接実施済み。

**Goal:** writing-plans の spec-architecture-review 部に「型閉包・重複・ADR矛盾」の検査（検査5〜7）を足し、実装レビューで差し戻されている型未閉包の指摘を plan 段階の1問へ前倒しする。

**Architecture:** 発火条件は `references/type-closure-patterns.md` に字面パターンの表として置き、SKILL.md 本文には手順（fresh-context subagent への委譲・6節限定・問い1つ・第3バケツ）と WHY 一句だけを書く。事案と数値は `references/incidents.md #T1` に集める。

**Tech Stack:** Markdown（`.agents/skills/writing-plans/`）、`.decisions/`

## Requirements

裁定: `.decisions/2026-09-13-writing-plans型閉包検査は構造化節だけを字面で見て問い1つを出しplanは書き換えない.md`

1. 発火条件表が存在し、A〜I（型閉包）・検査6（重複）・検査7（ADR矛盾）の各行が「発火条件（字面）」と「問い」の2列を持ち、出所の verdict id を引ける
2. SKILL.md に Phase 2.6 があり、fresh-context subagent への派遣物4点・検査範囲（構造化6節）・出力形式（1発火1行→AskUserQuestion 1問）・第3バケツの扱い・射程外（impl-deviated）が書かれている
3. Phase 3 に第3バケツのレビュー依頼文への出力行がある
4. Red Flags に型閉包由来の行が足されている（null/-1 合図・前提の転写・範囲外の既存重複）
5. incidents.md に #T1 として出所（239件分類→63件照合）が記録され、本文に日付・数値が焼き込まれていない
6. やらないこと: task-reviewer-contract の変更（別PR、bd 別issue）、グローバル `~/.agents` 側への移植（別途）、検査の自動化スクリプト（まず人手＋subagent で回して発火率を見る）

## Global Constraints

- 規則本文は WHY 一句、事案は incidents.md（剪定監査 2026-09-04 の横断パターン）
- 「迷ったら ok」を継承。表に無い形は発火させない
- plan は書き換えない。裁定は `## 判断記録（ADR）` に残す

---

### Task 1: 発火条件表

**Files:**
- Create: `.agents/skills/writing-plans/references/type-closure-patterns.md`

- [x] x-pass2 の A〜K を検査5（A〜I）・検査6（J＋Self-Review 6 相当＋既存重複の Grep 行）・検査7（K）に再編し、各行に「問い」を付ける
- [x] 検査6 最終行を第3バケツの唯一の入口として明記する

### Task 2: SKILL.md 本文

**Files:**
- Modify: `.agents/skills/writing-plans/SKILL.md`（検査スコープ・Phase 2 見出し・Phase 2.6 新設・Phase 3・Red Flags）

- [x] Phase 2.6 を Phase 2.5 と Phase 3 の間に置く
- [x] 検査1〜4（修正まで行う）と検査5〜7（問いを出すだけ）の区別を検査スコープ節に書く

### Task 3: 事案と裁定

**Files:**
- Modify: `.agents/skills/writing-plans/references/incidents.md`
- Create: `.decisions/2026-09-13-writing-plans型閉包検査は構造化節だけを字面で見て問い1つを出しplanは書き換えない.md`

- [x] #T1 を追記。本文からは数値を外す
- [x] 6論点の裁定と棄却案を .decisions に残す

### Task 4: 後続の積み残し

- [x] bd: task-reviewer-contract への「plan の Interfaces と実装の型差分」1行追加を別 issue として起票
- [ ] グローバル `~/.agents/skills/writing-plans` の事実誤り（`git pull` 必須・「センスが怪しい」・pytest 例・不在スキル参照2件）の移植は別途。Phase 2.6 は moorestech 固有の層マップに依存するため移植対象外

## 判断記録（ADR）

- 6論点の裁定 → `.decisions/2026-09-13-writing-plans型閉包検査は…md`（出所: 引き継ぎdoc 論点1〜3 ＋ 第2パスで生じた3点 → ユーザー承認 2026-09-13）
- 検査5〜7を spec-architecture-review 内の Phase として置き、別スキルにしなかった理由: 発火点が同じ構造化節で、検査1（層配置）との重なり（G）を1つの派遣で処理できる。別スキル化は subagent 派遣が2回になるだけ
- 自動化スクリプト（発火条件の grep 化）は今回見送り: 字面パターンは書けたが、6節の切り出しが plan の書式に依存する。まず subagent で数回回し、発火率と誤検知を `moorestech_logs/harness/writing-plans-extension/` に記録してから判断する
- unityプレイ録画テスト: 対象外（スキル文書のみ）
