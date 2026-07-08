---
name: writing-plans
description: Use when you have a spec or requirements for a multi-step task, before touching code
---

# Writing Plans

## Overview

Write comprehensive implementation plans assuming the engineer has zero context for our codebase and questionable taste. Document everything they need to know: which files to touch for each task, code, testing, docs they might need to check, how to test it. Give them the whole plan as bite-sized tasks. DRY. YAGNI. TDD. Frequent commits.

Assume they are a skilled developer, but know almost nothing about our toolset or problem domain. Assume they don't know good test design very well.

**Announce at start:** "I'm using the writing-plans skill to create the implementation plan."

**Context:** If working in an isolated worktree, it should have been created via the `superpowers:using-git-worktrees` skill at execution time.

**Save plans to:** `docs/superpowers/plans/YYYY-MM-DD-<feature-name>.md`
- (User preferences for plan location override this default)

## Scope Check

If the spec covers multiple independent subsystems, it should have been broken into sub-project specs during brainstorming. If it wasn't, suggest breaking this into separate plans — one per subsystem. Each plan should produce working, testable software on its own.

## File Structure

Before defining tasks, map out which files will be created or modified and what each one is responsible for. This is where decomposition decisions get locked in.

- Design units with clear boundaries and well-defined interfaces. Each file should have one clear responsibility.
- You reason best about code you can hold in context at once, and your edits are more reliable when files are focused. Prefer smaller, focused files over large ones that do too much.
- Files that change together should live together. Split by responsibility, not by technical layer.
- In existing codebases, follow established patterns. If the codebase uses large files, don't unilaterally restructure - but if a file you're modifying has grown unwieldy, including a split in the plan is reasonable.

This structure informs the task decomposition. Each task should produce self-contained changes that make sense independently.

## Task Right-Sizing

A task is the smallest unit that carries its own test cycle and is worth a
fresh reviewer's gate. When drawing task boundaries: fold setup,
configuration, scaffolding, and documentation steps into the task whose
deliverable needs them; split only where a reviewer could meaningfully
reject one task while approving its neighbor. Each task ends with an
independently testable deliverable.

## Bite-Sized Task Granularity

**Each step is one action (2-5 minutes):**
- "Write the failing test" - step
- "Run it to make sure it fails" - step
- "Implement the minimal code to make the test pass" - step
- "Run the tests and make sure they pass" - step
- "Commit" - step

## Plan Document Header

**Every plan MUST start with this header:**

```markdown
# [Feature Name] Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** [One sentence describing what this builds]

**Architecture:** [2-3 sentences about approach]

**Tech Stack:** [Key technologies/libraries]

## Global Constraints

[The spec's project-wide requirements — version floors, dependency limits,
naming and copy rules, platform requirements — one line each, with exact
values copied verbatim from the spec. Every task's requirements implicitly
include this section.]

---
```

## Task Structure

````markdown
### Task N: [Component Name]

**Files:**
- Create: `exact/path/to/file.py`
- Modify: `exact/path/to/existing.py:123-145`
- Test: `tests/exact/path/to/test.py`

**Interfaces:**
- Consumes: [what this task uses from earlier tasks — exact signatures]
- Produces: [what later tasks rely on — exact function names, parameter
  and return types. A task's implementer sees only their own task; this
  block is how they learn the names and types neighboring tasks use.]

- [ ] **Step 1: Write the failing test**

```python
def test_specific_behavior():
    result = function(input)
    assert result == expected
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pytest tests/path/test.py::test_name -v`
Expected: FAIL with "function not defined"

- [ ] **Step 3: Write minimal implementation**

```python
def function(input):
    return expected
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pytest tests/path/test.py::test_name -v`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/path/test.py src/path/file.py
git commit -m "feat: add specific feature"
```
````

## No Placeholders

Every step must contain the actual content an engineer needs. These are **plan failures** — never write them:
- "TBD", "TODO", "implement later", "fill in details"
- "Add appropriate error handling" / "add validation" / "handle edge cases"
- "Write tests for the above" (without actual test code)
- "Similar to Task N" (repeat the code — the engineer may be reading tasks out of order)
- Steps that describe what to do without showing how (code blocks required for code steps)
- References to types, functions, or methods not defined in any task

## Remember
- Exact file paths always
- Complete code in every step — if a step changes code, show the code
- Exact commands with expected output
- DRY, YAGNI, TDD, frequent commits

## Self-Review

After writing the complete plan, look at the spec with fresh eyes and check the plan against it. This is a checklist you run yourself — not a subagent dispatch.

**1. Spec coverage:** Skim each section/requirement in the spec. Can you point to a task that implements it? List any gaps.

**2. Placeholder scan:** Search your plan for red flags — any of the patterns from the "No Placeholders" section above. Fix them.

**3. Type consistency:** Do the types, method signatures, and property names you used in later tasks match what you defined in earlier tasks? A function called `clearLayers()` in Task 3 but `clearFullLayers()` in Task 7 is a bug.

If you find issues, fix them inline. No need to re-review — just fix and move on. If you find a spec requirement with no task, add the task.

## Execution Handoff

After saving the plan, offer execution choice:

**"Plan complete and saved to `docs/superpowers/plans/<filename>.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?"**

**If Subagent-Driven chosen:**
- **REQUIRED SUB-SKILL:** Use superpowers:subagent-driven-development
- Fresh subagent per task + two-stage review

**If Inline Execution chosen:**
- **REQUIRED SUB-SKILL:** Use superpowers:executing-plans
- Batch execution with checkpoints for review

# 追加SKILL:spec-architecture-review

---
name: spec-architecture-review
description: |
  設計書（spec）・実装計画（plan）をユーザーレビューやコミットに出す前に、そこに書かれた「配置決定」（どの型・メンバーをどのアセンブリ/層に置くか、どの機構を使うか）を全件抽出し、層責務・既存前例・プロジェクトイディオムと突合して違反を自己修正するレビュースキル。質問トリアージ（design-question-triage）が「質問前」を守るのに対し、このスキルは「質問にならず設計書へ静かに書き込まれた誤配置」を捕まえる。
  Use when:
  1. 設計書・スペック・実装計画を書き終えて、ユーザーレビュー依頼やコミットに出す直前（毎回必須）
  2. brainstorming 系スキルの「設計提示」「spec self-review」フェーズに入る時
  3. writing-plans 系スキルの「plan self-review」フェーズに入る時
  4. 設計レビューで「その層に置くのはおかしい」「その機構はプロジェクト標準と違う」という指摘を受けた後の再発防止として
---

# spec-architecture-review — 設計書の配置決定を前例と突合する

## なぜこのスキルが必要か

設計書レビューで最も信頼を損なうのは、**実装の都合で層責務を破る配置**をユーザーに指摘されることである。
「変更箇所が最小になるから既存クラスに足す」「データの出所がマスタだからマスタクラスに置く」という判断は、
書いた瞬間は合理的に見えるが、コードベースの所有権モデルを壊す。
この種の誤りは質問の形を取らないため質問トリアージでは捕まらない。**書かれた設計書自体を検査する**必要がある。

内容の正しさ（プレースホルダ・内部整合・スコープ）は既存の spec self-review が見る。
このスキルが見るのは**構造の正しさ**: 「どこに置くか」「何の機構を使うか」がこのコードベースの流儀に合っているか。

## 発火タイミング

設計書・実装計画を「書き終えた」と思った直後、ユーザーに見せる・コミットする**前**。
spec と plan の両方に対して実行する（plan は spec に無かった配置詳細が増えるため、spec で済ませたから plan は不要、とはならない）。

## 検査スコープ — 何を見て、何を見ないか

このスキルの findings に載せてよいのは**構造違反**（層責務・前例逸脱・イディオム逸脱）だけである。

見ないもの（findingsに混ぜたら誤り。気づいた場合は findings 外の備考1行に留める）:
- **実現可能性・実在性**: 参照クラスが実在するか、既存APIで実装が成立するか（実装フェーズとコンパイラの責務）
- **内容の正しさ・網羅性**: 要件漏れ、テスト不足、曖昧さ（既存の spec self-review の責務）
- **改善余地**: 前例が肯定している形に対する「より良くできる」提案。前例通りの配置は verdict: ok。
  違反と断定できるのは**規約表・前例と矛盾する場合のみ**であり、判断が割れる配置（例: どのマスタymlに置くか）は
  violation ではなく「新規パターン/判断点」としてユーザー注目点に回す

レビューの信頼はfalse positiveで最も速く壊れる。迷ったら ok に倒し、注目点として書く。

## Phase 1: 配置決定の全件抽出（インベントリ化）

設計書から以下を**表に書き出す**（頭の中の確認は抽出ではない。書き出さないと漏れる）:

| # | 項目（型/メンバー/ファイル） | 配置先アセンブリ・層 | 使用する機構 |
|---|---|---|---|

抽出対象:
- 新規作成・変更するすべてのファイルと、その所属アセンブリ（asmdef 単位）
- 新規の型・public メンバーと、その所属層
- 採用する機構: イベント/通知、永続化、通信、DI 登録、マスタデータアクセス、非同期

**既存クラスへのメンバー追加も1行として抽出する。** 新規ファイルより既存クラスへの「ちょい足し」の方が誤配置が起きやすい。

## Phase 1.5: データフロー地図（既存パイプラインに参加する機能は必須）

対象機能が既存の一方向連鎖（例: 入力→共有モデル書き込み→下流がモデル変化から挙動を導出）に相乗りするなら、配置検査の前に矢印列を1本書き、新規コンポーネントの立ち位置を1語で宣言する:

```
（駆動元）→（既存の書き手たち）→［共有モデル/状態］→（下流の読み手たち）→（挙動）
```

- **書き手**（共有モデルへ書くだけ）← パイプライン型機能の既定。自由度は「誰が・いつ書くか」だけ
- **読み手**（モデル変化を観測して表示・遷移等を足す）
- **交差点**（❌）＝既存フローに分岐・逆流・並行経路を足すもの: 下流へ制御を返す `bool` 戻り値／共有モデルを迂回する第2の書き込み経路（下流への直接セッター）／フレーム駆動へのイベント型混入。**不可避の理由（既存のどの駅でも表現できない情報・タイミング）を書けないなら書き手／読み手へ畳む**

これで前例は「同じ矢印位置の既存コンポーネント」に一意化し（検査2）、交差点の是非は機構選択（検査4）で判定される。根拠: スポイト機能で `bool` 戻り＋各設置システムへの直接セッターを提示し「データフローを一貫（共有選択モデルへ書く一本に）」と全面修正された。矢印列を書けば「PlacementSelection への2人目の書き手」に一意化し両者を交差点として弾けた。

## Phase 2: 各行に4つの検査

### 検査1: 層責務（layer ownership）

各行について次の2つを1行ずつ書いて突合する:
- この項目が属するドメインは何か（例: プレイヤーインベントリ）
- 配置先アセンブリの責務は何か（例: Core.Master = マスタデータの生ロード・保持）

**判定質問: 「この機能が存在しなかったとしても、この変更はこの層にとって意味を持つか？」**
No なら、それはドメイン層に置くべきものである。

共有層（`Core.*` や複数ドメインから参照される基盤）への追加は挙証責任が逆転する:
「ドメイン非依存であること」を示せない限り追加禁止。ドメインの言葉（プレイヤー、インベントリ、研究…）が
型名・メソッド名に現れる時点でドメイン依存であり、共有層には置けない。

### 検査2: 前例（precedent）

同カテゴリの既存実装を Grep で探し、配置・命名・機構を比較する:
- 新規 store → 既存の store はどのアセンブリ・どんな形か
- マスタ値の解釈ロジック → 既存はどの層のどんな util か
- 新規イベント → 既存イベントの型・公開方法
- DI 登録・asmdef 参照追加 → 同種の登録・参照の前例

**前例は「機構」ではなく「役割」で選ぶ。** 「この機構（購読・静的アクセス等）を使っている前例があるか」と探すのは確証バイアス
（選んだ機構の前例は大抵1件見つかる）。正しい問いは「**同じ役割のコンポーネントは既存でどう駆動・配置されているか**」。
役割が違う前例を機構が同じという理由で引用してはならない（例: 受動的な表示オブザーバの購読前例は、
制御に参加するコンポーネントの駆動方式の前例にならない）。

**置換・吸収ゲート**: 設計が既存コンポーネントを置換・吸収する場合、**その置換対象自身の駆動機構が第一の前例**である。
機構を変える（駆動→購読、明示呼び出し→イベント等）なら、それは「新規パターン」であり、置換対象の機構との比較付きで
ユーザー注目点に載せる。無言の機構変更は禁止。

結果は二択:
- **前例に合わせる**（原則こちら。合わせない積極的理由がなければ従う）
- **新規パターンとして設計書に明記**（前例が存在しない/割れている場合のみ。理由と一緒に書き、ユーザーレビューの注目点として提示する）

### 検査3: イディオム（idiom）

プロジェクト規約表（`references/` 配下。moorestech なら `references/moorestech-layer-map.md` を必ず読む）と突合する。
最低限の共通項目:
- イベント・通知の標準機構（このプロジェクトの標準は何か。C# 標準機能で書いていないか）
- 永続化の形式・キー（揮発 ID を保存していないか、マスタ由来値を保存していないか）
- マスタ生成物へのアクセス方法（読み取り専用か、生成クラスへの手出しをしていないか）
- 依存の増やし方（asmdef / パッケージ参照の追加は前例のある形式か）

### 検査4: 機構選択（leverage-over-replace）

設計が「動作中の既存機構」（状態機械・入力処理・ライフサイクル・既存の同期経路など）に対して
**抑止・凍結・迂回・許可リスト制・並行複製**のいずれかを導入する場合、それは配置以前の**機構選択の分岐点**である。
次を必須とする:

- **受動的統合案を必ず併記する**: 既存機構を正のまま無傷で動かし続け、新レイヤーは購読・ミラー・ビュー差し替えに徹する案を、
  能動介入案と**名前付きで並べて** head-to-head 比較する（「既存の凍結を温存」「一部だけ許可」も能動介入側に分類する。
  片方をコスト過大に描いて棚上げする藁人形比較は比較と認めない）。
- **デフォルトは受動的統合**。能動介入を選ぶには「既存機構を無傷で活かすと成立しない具体的理由」の明記が必要。
  差分の小ささ・実装の速さ・「既存挙動と不変だから安全」は理由にならない。
- 比較の結論と根拠を設計書のアーキテクチャ節に残す（後続レビュアーが分岐を再検証できる形で）。

根拠となった実障害: 動作中の状態機械に許可リスト制の遷移抑止を導入した計画が、全タスク実装完了後に
「既存機構をそのまま活かすべきだった」とユーザー指摘で全面やり直しになった。比較さえ書いていれば設計段階で気づけた。

## Phase 2.5: 機能パリティ検査（移行・統合・置換の計画では必須）

計画が触れる機構で**現在ユーザーが実際に使える操作**（入力キー・画面遷移・常時表示UI・保存等の重要操作）を列挙した
**死活表**（操作 → 計画後も生きるか → 根拠1行）を設計書に含める。列挙は「触るコードの操作」でなく
「同じ機構にぶら下がる全操作」を対象にする（自分が変えない操作が巻き添えで死ぬのが典型事故）。

- 1つでも死ぬ・退化する操作がある場合、それを「既知の制限」として計画内で確定するのは**禁止**。
  それは制限ではなく**裁定事項**であり、実装開始前にユーザーへ選択肢付きの質問として提示し、裁定を得るまで当該部分の実装に進まない。
- 「新規パターン」「レビュー注目点」として設計書に書くだけで実装を開始するのも同じ違反（**フラグ記載は裁定の代替にならない**）。

根拠となった実障害: 「モード切替キーが効かなくなる」「常時HUDが消える」を既知の制限として計画内で独断確定した結果、
実装完了後にユーザー指摘で全面やり直しになった。死活表と裁定ゲートがあれば1問の質問で防げた。

## Phase 3: 修正と記録

- **違反は質問せず修正する。** 前例が明確なら、それに合わせるのはユーザーの判断を要しない
- 修正した設計書に**「レイヤリング制約」または「配置と前例」セクション**を残し、主要な配置決定に前例（ファイルパス）を引用する。これが次のレビュアー（人間・AI とも）の検証コストを下げる
- 前例のない「新規パターン」だけを、ユーザーレビュー依頼文で注目点として列挙する
- 検査で層マップ・規約表に載っていない規約違反を指摘されたら、`references/` の規約表に追記する（このスキル自体を成長させる）

## Red Flags — この思考が出たら検査に戻る

| 思考 | 現実 |
|---|---|
| 「変更が最小だから既存クラスに足す」 | 差分最小は層違反の正当化にならない。診断: 判定質問（Phase 2 検査1）を通せ |
| 「データの出所がマスタだからマスタクラスへ」 | データの出所と解釈ロジックの所有者は別物。解釈はドメイン層が持つ |
| 「新機能だから新しい制御フロー（bool戻り・専用セッター・イベント）を足す」 | 既存パイプライン参加機能の実体は「書き手が1人増える」だけ。Phase 1.5 で矢印列を書き、交差点を足していないか確認せよ |
| 「C# 標準の event/Action で十分」 | 機構選定の基準は十分性ではなく統一性。プロジェクト標準に合わせる |
| 「specに書くほどの詳細ではない」 | 配置とイディオムは設計の一部。書かなければ実装時に都合で決まる |
| 「後で直せる」 | 設計書に書いた配置はそのまま実装・レビューされ、手戻りが最大化する |
| 「既存機構を凍結/抑止/許可リストで黙らせるのが手っ取り早い」 | それは機構選択の分岐点。検査4で受動的統合案（無傷で動かし購読・ミラー）と名前付き比較せよ |
| 「動いている機能が死ぬのは既知の制限と書けばよい」 | 動作中機能の喪失は制限でなく裁定事項。Phase 2.5 の死活表に載せ、実装前に選択肢付きで質問する |
| 「セルフレビューはもうやった」 | 既存 self-review は内容検査。構造検査（このスキル）は別物で、両方やる |
| 「既存イベントを購読すれば呼び出し側を触らずに済む」 | 制御に参加するコンポーネントは呼び出し側（ステート等）から駆動されるのが原則。購読で済ませてよいのは受動的な表示オブザーバだけ |
| 「発火順・タイミングの都合はこう回避する」 | 選んだ機構のせいで回避策（発火順の妥協、二重管理等）が必要になったら、それは機構選定ミスのサイン。回避策を書く前に駆動方向を反転した案と比較する |

## アンチパターン実例（実際にあった指摘）

プレイヤーインベントリのスロット数レベル機能の設計・計画で、同一セッション内に2件の構造違反を書き込んだ:

1. **共有層へのドメインロジック混入**: レベル→スロット数の解決アクセサ
   `GetPlayerInventorySlotCount(level)` を `ItemMaster`（Core.Master）へ追加する計画を書いた。
   「マスタデータ（items.yml）が出所だから」という連想で配置したが、プレイヤーインベントリは Game 層の
   ドメインであり、Core.Master の責務はマスタの生ロード・保持のみ。
   → 正解: Game.PlayerInventory.Interface に static util を新設し、`MasterHolder.ItemMaster.Items`
   （public readonly 生成物）を**読むだけ**にする。Core 側への追加はゼロ。
2. **イディオム逸脱**: 新 store の通知を `event Action<int>` で設計した。プロジェクト標準は UniRx
   （`Subject<T>` + `IObservable<T>`、csharp-event-pattern スキル・Game.UnlockState に前例）。
   依存追加の不確実性（asmdef に UniRx があるか未確認）を理由に標準から逃げた。
   → 正解: 前例の asmdef（Game.UnlockState.asmdef の `"UniRx"` 参照）を確認してから標準に乗る。

どちらも spec self-review（プレースホルダ・整合性・スコープ・曖昧さ）は通過していた。
構造検査が独立したフェーズとして存在しなかったことが根本原因である。

3. **機構先行の前例引用（役割不一致）**: FPS建設モードの設計で、建設系UIステートのカメラ・カーソル制御を担う
   `BuildViewModeController`（制御参加者）のライフサイクルを `UIStateControl.OnStateChanged` の購読で作り、
   前例として `DisplayEnergizedRange`（受動的な表示オブザーバ）を引用して検査2を通過させた。
   実際には置換対象の `ScreenClickableCameraController` と同役割の `PlaceSystemStateController` がともに
   「ステートから明示駆動」であり、役割同型の前例はすべて駆動側だった。さらに購読方式の発火タイミング
   （OnEnterより後）を回避するためカーソル制御を各ステートに残す妥協まで書いていた（機構ミスのサインの見逃し）。
   → 正解: ステートから `OnEnterBuildState`/`OnLeaveBuildState` で明示駆動。本スキルに「役割で前例を選ぶ」
   「置換・吸収ゲート」「回避策は機構ミスのサイン」の3項が追加されたのはこの指摘が由来。
