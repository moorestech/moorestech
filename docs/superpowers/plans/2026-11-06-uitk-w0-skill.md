# UITK W0-skill（ref-uitk-design 新設と moores-code-review の UITK 観点・albof.19）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** UITK の部品・画面を作るすべてのレーンが読む知識スキル `.agents/skills/ref-uitk-design/`（SKILL.md＋references 3 本）を新設し、実装前チェックリスト `moores-reviewer-digest.md` に UITK の観点 7 つを足して、「Web 版と同じ見た目・層の向き・部品化」をレーンの実装とタスクレビューで毎回引ける状態にする。

**Architecture:** Editor を使わない文書だけのレーン（単位エージェント）。スキル本文と references 2 本（css-to-uss・compare-procedure）は、このplanに全文を載せたものを書き写す。部品カタログ（component-catalog）は設計書 `docs/uitk-migration/execution/component-architecture.md` §2 を、このplanに載せたコマンドで機械的に写す（手で写すと設計書とずれるため）。合否は create-skill のレビューモード（別コンテキストの評価役 1 体）と `quick_validate`・行数・リンク実在の検査で出し、最後は moores-code-review → `base=feature/uitk` の PR → agent がマージコミットで取り込む。

**Tech Stack:** Markdown（Agent Skills 形式の SKILL.md・YAML frontmatter）、bash・awk・sed、Python 3（`quick_validate`）、create-skill スキル、moores-code-review スキル、gh CLI、bd（Beads）

## Requirements

1. `.agents/skills/ref-uitk-design/SKILL.md` を新設する。frontmatter は `name: ref-uitk-design` と `description`（何をするか＋`Use When — …`）だけ。本文は設計書 §6 の目次 0〜13 をすべて持つ（0 は description、1〜13 は本文の `## 1.`〜`## 13.`）。受け入れ: `wc -l` が 200 以下、`quick_validate` が `Skill is valid!`、`grep -c '^## ' SKILL.md` が 13
2. SKILL.md は「見た目の正本は Web 版と参照キャプチャ」「面は Web 版のその画面と同じ枠（GamePanel だけではない）」「ref-webui-design のホワイトリストは Web に無い新しい面にだけ効く」を書く（albof.19 の題「『面はGamePanelだけ』等をWeb版に合わせて直す」と裁定 2 本）。受け入れ: `grep -n 'Web 版のその画面と同じ枠' SKILL.md` が 1 行、`grep -n '面を GamePanel に統一しない' SKILL.md` が 1 行
3. `references/component-catalog.md` を設計書 §2.1〜§2.8 の写しとして作る（見出しを `## 1`〜`## 8` に付け替え、先頭に目次・出所・直し方を置く）。受け入れ: 本文が設計書 §2 と同じ表の行を持つ（Task 1 Step 3 の差分検査が 0 行）、`## ` 見出しが 8 個
4. `references/css-to-uss.md` に plan §3.5 の変換表と、影・グラデ・マスク・図形・文字の縁・半透明・動きの写し方と前例（`GamePanelFace`・`GamePanelDecoLine`・`GamePanelBottomDeco`）を置く。受け入れ: 目次があり、§1〜§8 の 8 見出し
5. `references/compare-procedure.md` に設計書 §3.3・§3.4 の撮影・並べ比べ・合否の目安（720p で 2px 以内ほか）・例外の記帳の手順を置く。受け入れ: 目次があり、§1〜§6 の 6 見出し、参照キャプチャの撮り直しコマンドが `e2e/uitk-reference/capture.ts` を指す
6. SKILL.md から references 3 本へのリンクが、読む条件付きで 1 階層だけで張られている。受け入れ: Task 2 Step 2 のリンク検査が `MISSING` 0 件
7. `.agents/skills/moores-code-review/references/moores-reviewer-digest.md` の末尾に「## UITK」節と項目 17〜23（PickingMode の既定・全体スイッチの片側読み R2・トークンの直書き・イベントの出し方〈`Button.clicked`/`Action`〉・部品の中身を外から塗る・層の依存方向・tss の束ね漏れ）を足す。既存の 1〜16 は変えない。受け入れ: `git diff` が追記だけ（削除行 0）、`grep -c '^1[7-9]\.\|^2[0-3]\.' digest` が 7
8. 〔レーンID〕の印: まだ `feature/uitk` に無い名前・仕組み（F1・C2-b・C3-a・C4-b 等が入れるもの）には〔レーンID〕を付け、無い間の今の形を括弧で書く。受け入れ: Task 2 Step 3 の存在検査で、印の無い型名・ファイル名が `feature/uitk` に全部実在する
9. create-skill のレビューモード（Phase 5）を、別コンテキストの評価役 1 体で 1 回回し、Critical と Warning を全部直す（裁定・設計書に由来する規則への指摘は直さず理由を残す。Info は採否を `done.md` に書く）。受け入れ: レビュー報告ファイルが実在し、直した後の `quick_validate` が通り、Critical・Warning の各指摘に「直した／直さない理由」が `done.md` にある
10. moores-code-review の全ブランチレビュー → `base=feature/uitk` の PR → G1 緑 → agent が `gh pr merge --merge` で取り込む。bd `moorestech-albof.19` を閉じる
- やらないこと: C#・USS・tss・テストの変更（スキルが書く〔F1〕等の仕組みはそのレーンが作る）。moores-code-review の reviewer（`reviewers/*.md`）の新設（判断記録 D-4）。`ref-webui-design` の変更・廃止（X-5e）。設計書 `component-architecture.md` の改訂（W0-merge で入った版を写すだけ）。Web（`moorestech_web/`）の変更。参照キャプチャの撮り直し

## Global Constraints

- 作業場所: 指揮が `moores-wt new uitk-lane/w0-skill --from origin/feature/uitk --no-editor` で作った worktree。全コマンドはその worktree の root を cwd にして実行する（最初に `pwd` で確かめる）。メインワークツリーでは作業しない。Unity Editor は起動しない（`.cs` を変えないので compile は不要）
- 前提: W0-merge（albof.20）が `origin/feature/uitk` に入っていること（`docs/uitk-migration/execution/component-architecture.md` がそこにある）。無ければ書かずに止め、指揮に報告する（Task 1 Step 1）
- 裁定（`origin/master:.decisions/`）: `2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`、`2026-11-06-UITK段階2以降の画面もWeb版と同じ見た目にする.md`、`2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`、`2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`、`2026-11-06-CEF停止の記録はUITK長命ブランチから先にmasterへ切り出す.md`、`2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`
- 設計の正本: `docs/uitk-migration/execution/component-architecture.md`（設計書。§2 部品カタログ・§3 見た目の一致・§4 画面の型・§6 スキルの目次）と `origin/master:docs/uitk-migration/plan.md`（§3.3 層と全体スイッチ・§3.5 変換表・§3.6 動き・§3.8 入力・§3.12 AI 向けの作法・§4.6 見た目の確かめ方）。実行の正本は `docs/uitk-migration/execution/execution-plan.md`
- スキルの書き方（create-skill の review-checklist と利用者規約）: SKILL.md は 200 行以内。description は YAML として妥当（値に半角の「`: `」を入れず、ラベルは `Use When — `）。規則は「〜のとき〜する／しない（なぜなら〜）」と条件・パス・コマンドだけ。経緯・日付・「以前は〜」は書かない。references は 1 階層、100 行を超えるものは目次を持つ
- スキルの正本は `.agents/skills/` だけ（`.claude/skills` は symlink）。`.claude/skills/ref-uitk-design` を別に作らない
- 用語はユーザー向けの和語寄りの日本語（既存の設計書と同じ語）。部品名・API 名は設計書 §2 の綴りに合わせる
- 報告は `../moorestech_logs/harness/uitk/W0-skill/done.md`（完了条件ごとの証拠・未完了・要裁定）。`../moorestech_logs` で書く前に同 repo の `README.md` を読む。レビュー記録（`$RUNDIR`）も `../moorestech_logs/harness/` 配下
- バックグラウンドのポーリングで待たず、全部前景で実行する。タスクごとにコミットする。マージはマージコミット（Squash・rebase 禁止）。master への PR は作らない

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反7件・修正7件 / 印の持ち主を実行計画のレーン表へ合わせた（KeyCap・UiStack・Typography/Layout を C2-c、カタログ出所文も）、コード例の IconButton・IconButtonGlyph と digest の Client.Ui.Content に印、印の持ち主とコード例の印の機械検査を Task 2 Step 3 に追加、例外の判定基準を compare-procedure §6 の 1 か所へ寄せた、設計書・実行計画との食い違い 2 件（作る時期 D-1・比較の置き場 D-3）は D-11・D-12 に要裁定として記録し done.md への記帳を Task 6 Step 4 に追加
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱3・第3バケツ1 / 強=IconButtonGlyph 未定義・IconButton 無印・作る時期の食い違い・比較の置き場の食い違い、弱=印の持ち主・例外基準の二重記述・digest の無印、第3バケツ=ref-webui-design §2 の面の規則（末尾の本PR外のリファクタ提案へ）
- 全体レビュー反映（2026-11-06）の再検査（検査 5〜7・配置検査、変えた範囲: 規則表の font-size 行・文字の大きさの規則・D-6）: 実施済み / 違反0件 / Q-1 の裁定待ちを設計書 [A-39] の agent 既定へ書き換えた。Files・Interfaces は変えていない

---

## File Structure

| ファイル（repo 相対） | 新設/変更 | 責務 |
|---|---|---|
| `.agents/skills/ref-uitk-design/SKILL.md` | 新設 | UITK の作り方の規則の入口（いつ何を見るか・層・部品・USS・トークン・層と族・画面の型・動き・入力・比べ方・やらないこと・検証）。200 行以内 |
| `.agents/skills/ref-uitk-design/references/component-catalog.md` | 新設 | 部品カタログ（設計書 §2 の写し）。部品レーンが作るたびに「段階 1」列と API 列を実装どおりに直す索引 |
| `.agents/skills/ref-uitk-design/references/css-to-uss.md` | 新設 | Web の CSS を USS・描画へ写す変換表と前例 |
| `.agents/skills/ref-uitk-design/references/compare-procedure.md` | 新設 | 撮る・並べる・判定する・記帳する手順 |
| `.agents/skills/moores-code-review/references/moores-reviewer-digest.md` | 変更（末尾に追記） | 実装前・タスクレビューのチェックリストに UITK の観点 17〜23 |

`.claude/skills` は `.agents/skills` への symlink なので、`.claude/skills/ref-uitk-design/` は自動で見える。SDD の実装者・タスクレビュアーは `.claude/skills/moores-code-review/references/moores-reviewer-digest.md` を毎タスク読む（`moores-subagent-driven-development/implementer-contract.md`・`task-reviewer-contract.md`）。

---

### Task 1: references 3 本を作る

**Files:**
- Create: `.agents/skills/ref-uitk-design/references/component-catalog.md`
- Create: `.agents/skills/ref-uitk-design/references/css-to-uss.md`
- Create: `.agents/skills/ref-uitk-design/references/compare-procedure.md`

**Interfaces:**
- Consumes: `docs/uitk-migration/execution/component-architecture.md` §2（W0-merge で `feature/uitk` に入った版）
- Produces: SKILL.md（Task 2）がリンクする 3 ファイル。パスは `references/component-catalog.md`・`references/css-to-uss.md`・`references/compare-procedure.md`。カタログの節番号は `## 1 読み方と共通の約束`〜`## 8 Web の非 UI 共有モジュールの行き先`、css-to-uss の「半透明」は `## 6. 半透明`

- [ ] **Step 1: 前提を確かめる**

Run:
```bash
pwd
git fetch origin
git log -1 --format='%h %s' HEAD
test -f docs/uitk-migration/execution/component-architecture.md && echo OK-design || echo MISSING-design
test -e .agents/skills/ref-uitk-design && echo EXISTS-skill || echo OK-no-skill
grep -c '^### 2\.[1-8] ' docs/uitk-migration/execution/component-architecture.md
```
Expected: `pwd` が `uitk-lane/w0-skill` の worktree、`OK-design`、`OK-no-skill`、`8`。`MISSING-design` なら W0-merge が未取り込みなので、何も書かずに止めて指揮へ報告する。`EXISTS-skill` なら先行の作業があるので、中身を読んで指揮へ報告して止める

- [ ] **Step 2: css-to-uss.md と compare-procedure.md を書く**

`.agents/skills/ref-uitk-design/references/css-to-uss.md` を、次の内容そのままで作る（4 つのバッククォートの囲みの内側だけ）:

````markdown
# Web の CSS を USS へ写す

部品・画面の USS を Web の CSS module から写すときに開く。USS に無い表現（影・グラデ・マスク・grid・data 属性など）を前にしたら、自分で工夫する前にこの表の前例を使う。表に無い表現が出たら、写し方を決めたレーンがここへ 1 行足す。

目次: 1 単位と式 / 2 選択子と状態 / 3 配置と重なり / 4 文字 / 5 影・グラデ・マスク・図形 / 6 半透明 / 7 動き / 8 描画する子要素の型

出所: 変換表は `origin/master:docs/uitk-migration/plan.md` §3.5、前例は `S/Client.Ui/Components/GamePanel/`（`S/` = `moorestech_client/Assets/Scripts/`）。

## 1. 単位と式

| CSS | USS での扱い |
|---|---|
| `rem`・`em` | px にする（1rem = 16px）。元の値をコメントに残す |
| 定数だけの `calc()` | 計算済みの px にし、導出式をコメントに残す（`CalcConstantRecheckTest`〔F1〕が検算する）。例 `GamePanel.uss` の `top: -11.913894px;`（`calc(var(--panel-top, -8px) - 3.913894px)`） |
| 要素・画面の寸法に依存する `calc()`・`%`・`vw`/`vh` | USS の `%`・中央寄せか、C# の幾何計算（`GeometryChangedEvent`）に置き換える。舞台の倍率は `UiStageFrame`〔F1〕が持つ |
| `color-mix`・`rgb(from …)` | 事前に計算した色を `DerivedTokens.uss`〔F1〕（今は `ControlTokens.uss`）の 1 行にし、`/* web: 元の式 */` を付ける |
| `var(--x, 既定値)` の既定値 | 部品の USS で既定値を明示して受け直す（例 `--game-panel-edge-fade: var(--panel-edge-fade);`） |

## 2. 選択子と状態

| CSS | USS での扱い |
|---|---|
| `[data-selected]` などの data 属性 | `.<部品>--selected` のクラス。付け外しは部品の Set メソッドの `EnableInClassList` だけ |
| `:hover`・`:active`・`:focus` | そのまま USS の擬似クラス |
| `:first-child`・`:last-child`・`:nth-child` | 使えない（`UiStyleConventionTest` が落とす）。C# で `Add` するときにクラスを付ける（例 Layout の `UiStack`〔C2-c〕が先頭以外へ gap のクラスを付ける） |
| `::before`・`::after` | 子要素にし、`pickingMode = PickingMode.Ignore` を付ける。例 Web の GamePanel の `::before` → `GamePanelFace` |
| `:where(.keyHintText)` のような共有クラス | 共有部品（`KeyCap`〔C2-c〕）にする。同じ宣言を部品ごとに複製しない |
| feature の CSS が部品の変数を上書き | 同じ名前のカスタムプロパティを画面のルートで上書きする（USS のカスタムプロパティは子孫へ継承される） |

## 3. 配置と重なり

| CSS | USS での扱い |
|---|---|
| `display: grid`・`gap` | `flex-wrap` と固定幅・`margin`。スロットの格子は `SlotGrid`〔C3-a〕（列数から幅を C# で決める） |
| `width: fit-content` | 子の数と固定幅から C# で幅を決める（`SlotGrid`〔C3-a〕） |
| `z-index`・`--z-*` | `UiLayer` の層に載せる。同じ層の中は子の順で決める |
| `position: fixed` | 該当する層のルートに `position: absolute` で置く |
| コンテナクエリ（`cqw`・`container-type`） | `GeometryChangedEvent` で幅を読み、つまみ（`--slot-size` 等）を C# から書く |
| `pointer-events: none` | `pickingMode = PickingMode.Ignore` |
| `overflow: hidden` での切り抜き | `overflow: hidden` は USS でも効く。角丸の切り抜きは `-unity-overflow-clip-box` を確かめてから使う |

## 4. 文字

| CSS | USS での扱い |
|---|---|
| `-webkit-text-stroke`（アイコン上の数の縁、ADR 0033） | `-unity-text-outline-width`・`-unity-text-outline-color`。値は `--icon-text-stroke-*` |
| `text-shadow` | USS に無い。`-unity-text-outline` で近似し、見た目の例外に記帳する（前例 `PanelActionButton`。段階 1 の差として F1 が `LX-n` に記帳する） |
| `font-weight: bold`・`font-synthesis` | 合成の太字は使わない。Web が単一ウェイトなので同じ |
| `font-size`（Mantine の `size`） | Web と同じ px。13px 下限は外す（設計書 [A-39]。F1 が下限テストを外す〔F1〕） |
| `text-overflow: ellipsis` | `overflow: hidden`・`white-space: nowrap`・`text-overflow: ellipsis` |
| `font-family` | 書かない。`Typography.uss` が SDF のフォールバック列を当てる |

## 5. 影・グラデ・マスク・図形

USS に無い表現は `generateVisualContent`（頂点カラーのメッシュ）か Painter2D で描く。描く子は `PickingMode.Ignore`、色と長さは部品の USS で受け直したトークンを `UssCustomStyleReader` で読む（§8）。

| CSS | 描き方 | 前例 |
|---|---|---|
| `linear-gradient`（面の縦フェード）と `mask`（4 辺のフェード） | 頂点カラーの格子（4×4 頂点・3×3 セル）。端の頂点の α を 0 にする | `GamePanelFace` |
| 横の `linear-gradient`（罫線） | Painter2D の線に `Gradient`（停止位置は Web の % を逐語で） | `GamePanelDecoLine` |
| 三角・矢印・SVG の `path` | Painter2D の `MoveTo`/`LineTo`/`Fill` | `GamePanelBottomDeco`・`ProgressArrowGlyph`〔C4-b〕 |
| `box-shadow`（外側のにじみ） | 面の外周に、内側の α から外側の 0 へ落ちる頂点カラーの帯を張る | `GamePanelShadow`〔C2-a〕 |
| `box-shadow: inset` 2 層（ベベル） | border の入れ子（子要素 2 つに border を 1 本ずつ） | `SlotFrame`〔C3-a〕 |
| `outline`・ring | border の入れ子で近似し、差があれば記帳する | `ModeSwitch`（段階 1 の差として F1 が `LX-n` に記帳する） |
| `filter: drop-shadow` | 図形の輪郭を少し太らせた α の帯を先に描く | `ProgressArrowGlyph`〔C4-b〕 |
| `repeating-linear-gradient`（斜線帯） | Painter2D で平行四辺形を繰り返し塗る | 削除モードの帯（modeHud の画面に置く。S2-mode-tooltip が作る） |
| `background-image`（UI 装飾の画像） | 使わない。図形で描く | — |

## 6. 半透明

- Web の α をそのまま使わない。UITK は Linear 色空間で混ぜるので、同じ指定が Chrome より薄く出る（実測: 背景 `#9c7b42` に黒 α0.6 が UITK `#664f29`、Chrome `#3e311a`）
- 生成されるトークン（`Tokens.uss` 等）は変換スクリプト `tools/uitk/webTokens/translucency.mjs` が近似補正する（暗い色は `1-(1-α)^2.2`、明るい色は `α^2.2`）。結果の表は `docs/uitk/translucent-tokens.md`（生成物）
- 手で書く半透明（`DerivedTokens.uss`〔F1〕・今は `ControlTokens.uss`）は、同じ近似補正か、背景が面の色に決まるなら「面色を背景にした厳密式」（前例 `--game-panel-rule-*`・`--game-panel-triangle-face`）で決め、行のコメントに Web の値と決め方を書く
- 最後は実機の撮影で決める。決め直したら決め方のコメントを「実機で据え置き（何を撮って決めたか）」へ書き換える（前例 `--surface-navy`・`--panel-action-hover-face`）

## 7. 動き

| CSS | USS・C# での扱い |
|---|---|
| `transition` | USS の `transition`。付けた直後のクラスの付け外しは補間されないので `GeometryChangedEvent` を契機にする |
| `height: auto` への遷移 | px に固定してから動かす |
| `@keyframes`・`animation`（脈動・往復） | `UiMotionPlayer` の Step（`S/Client.Ui/Foundation/Motion/Steps/`）。尺はトークン |
| `scroll-behavior: smooth` | `ScrollOffsetTweenStep` |
| 慣性（パン） | `UiInertiaAnimation` |

## 8. 描画する子要素の型

```csharp
// Web の <元のセレクタ> を描く子。色と長さは部品の USS で受け直したトークンから読む
// Child that draws the Web <source selector>; colors and lengths come from tokens re-declared in the component USS
public sealed class ExampleDecoration : VisualElement
{
    private Color _color;
    private bool _hasTokens;

    public ExampleDecoration()
    {
        AddToClassList("example__decoration");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
    }

    private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
    {
        // 読めなくなったときも描き直し、前の絵を残さない
        // Repaint even when unreadable so the previous drawing never lingers
        _hasTokens = UssCustomStyleReader.TryReadColor(customStyle, "--example-decoration-color", out _color);
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext context)
    {
        // silent-ok: トークンが読めない間は描かない / UssCustomStyleReader が読めなかったトークン名を LogError で出す
        if (!_hasTokens) return;
        var painter = context.painter2D;
        painter.fillColor = _color;
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, 0));
        painter.LineTo(new Vector2(contentRect.width, 0));
        painter.LineTo(new Vector2(contentRect.width / 2f, contentRect.height));
        painter.ClosePath();
        painter.Fill();
    }
}
```

- 名前（`ExampleDecoration`・`--example-decoration-color`）は写す部品のものに置き換える
- 実在の型は `S/Client.Ui/Components/GamePanel/GamePanelBottomDeco.cs`（三角）・`GamePanelDecoLine.cs`（横グラデ）・`GamePanelFace.cs`（頂点カラーの格子）を開いて合わせる
````

`.agents/skills/ref-uitk-design/references/compare-procedure.md` を、次の内容そのままで作る:

````markdown
# 見た目の比べ方（撮る・並べる・判定する・記帳する）

部品レーンが部品を作り終えたとき、画面レーンが画面の L1 を通して L3/L4 に入る前、指揮が波の取り込みを判定するときに開く。画素の一致は目標にしない。比べるのは位置・寸法・色・装飾の有無・文字の段組。

目次: 1 誰が・いつ・何を / 2 Web の参照を用意する / 3 UITK を撮る / 4 並べ比べの載せ方 / 5 合否の目安 / 6 差の直し方と例外の記帳

出所: `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4、`origin/master:docs/uitk-migration/plan.md` §4.6。

## 1. 誰が・いつ・何を

| 何を | 誰が | いつ |
|---|---|---|
| Web の参照キャプチャ | integrator | 段の頭（その段の画面を撮り直す）と、master の取り込みでその段の画面に効く Web の変更が入ったとき |
| UITK の部品の見本 | 部品レーン | 部品の L1 が通った後 |
| UITK の画面 | 画面レーン | 画面の L1 が通り、L3/L4 に入る前 |
| 並べ比べ（自己確認） | 作ったレーン | 完了報告の前 |
| 並べ比べ（判定） | 指揮 | 波の取り込みの前。波ごとに codex-audit（画像付き）にかける |
| ユーザーの目視 | ユーザー | 切替 PR の検証ビルドで 1 回だけ |

## 2. Web の参照を用意する

- 置き場は `moorestech_logs/uitk/reference-captures/web-<撮影元の短い sha>/`。撮影元は台帳 `docs/uitk/TODO.md` の「## 参照キャプチャ」に書かれたもの。画面と状態の一覧は `index.md`、機械向けは `manifest.json`
- まず既にある撮影 id（例 `3-build-menu.png`・`2-tooltip-multiline.png`）を使う。部品だけの参照は、その部品が写っている画面の参照から切り抜く（Web に部品の見本ページは無く、作ると Web の変更になる）
- 撮り直しは integrator が master の worktree で行う（`feature/uitk` の Web には手を入れない）:

```bash
cd <master の worktree>/moorestech_web/webui && pnpm install
SHA=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT=/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/uitk/reference-captures/web-$SHA \
UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
```

- 出力先は空のディレクトリでないと止まる。撮り終えたら台帳の撮影元と置き場を新しい方へ書き換える
- mock-host で撮れない状態（実マスタのアイコン・ドラッグの最中など）は台帳の「## 未撮影」にある。それらは PlayMode で撮って同じ置き場へ足す

## 3. UITK を撮る

- **部品**: 部品ギャラリー〔F1〕（`Client.Ui.Editor` の EditorWindow。本番の tss を当て、部品ごとに全状態を並べる。部品レーンが `IUiGalleryEntry`〔F1〕を 1 つ足す）を開き、`uloop screenshot --project-path ./moorestech_client --capture-mode window --window-name <ギャラリーの題> --output-directory <worktree の絶対パス>/moorestech_client/.uloop/outputs/compare/<レーンID>` で撮る
- **画面**: PlayMode・全体スイッチ UITK で、参照キャプチャと同じ状態を DSL のシナリオで作り、ScreenCapture で 1280×720 と 2560×1080 の 2 種を撮る。フレーム番号の透かしを入れて古いフレームを見ないようにする
- `--capture-mode rendering`（PlayMode の既定）には UITK が写らない。撮れた画像は結果の `ImagePath` を開く（ディレクトリの新しい順で拾わない）
- 画面の ScreenCapture も同じ `moorestech_client/.uloop/outputs/compare/<レーンID>/` に置く

## 4. 並べ比べの載せ方

- 並べ比べは PR の成果物の載せ先（環境ローカル規約が決める。Mac mini では PR 報告ページ `https://review.moores.tech/pr/<番号>/report/`）に載せる。ページの書式は載せ先の生成器が決めるので、独自の HTML を書かない
- 1 状態につき Web と UITK の画像を同じ `group`（状態名）に入れ、Web を先に置く。caption は `Web <撮影 id>` と `UITK <状態名> 1280×720`（画面は `2560×1080` の組も足す）
- 差の表は作業報告 `moorestech_logs/harness/uitk/<レーンID>/done.md` に状態ごとに書く:

```markdown
### <状態名>（Web: <撮影 id> / UITK: <ファイル名>）

| 観点 | Web | UITK | 判定 | 対応 |
|---|---|---|---|---|
| 位置・寸法 | | | 同じ / 差 | 直した / LX-n |
| 色 | | | | |
| 装飾の有無 | | | | |
| 文字の段組 | | | | |
```

- 画像の原本は worktree の gitignore 下（`moorestech_client/.uloop/outputs/compare/<レーンID>/`）に置き、`moorestech_logs` には入れない（`moorestech_logs` に置くのは報告の md とスクショ 3 枚程度まで）

## 5. 合否の目安

- 1280×720 で位置・寸法の差が 2px 以内
- 色はトークン経由で同じ値（α 補正の行は補正後の値）
- 装飾（罫線・三角・影・ベベル・グラデ）の有無が同じ
- 文字の段組（折り返し・行数）が同じ
- 字形の差（Chrome と TextCore の描き方）は差に数えない

## 6. 差の直し方と例外の記帳

1. 差はまず Web へ寄せて直す（USS の値を Web の CSS と突き合わせる。つまみの上書き漏れ・トークンの取り違え・半透明の補正漏れが多い）
2. 例外にしてよいのは、同じにするための対応が大きい（部品の作り直し・数百行の描画・性能の劣化）か、ブラウザの癖の再現で、かつ差が一目で分からないときだけ。配置・色の系統・装飾の有無・文字の大きさの差は例外にしない。直せないときは報告に「要裁定」と書いて止める
3. 例外は台帳 `docs/uitk/TODO.md` の「## 見た目の例外」〔F1〕に 1 行足す。列: ID（`LX-n`）／画面・部品／差／同じにしない理由／推奨案／撮影（報告ページの URL）／出所（agent 既定・裁定）
4. 例外にした USS・C# の行に `/* look-exception: LX-n */` を付ける。印と台帳の照合は `LookExceptionLedgerTest`〔F1〕が行う
````

- [ ] **Step 3: component-catalog.md を設計書 §2 から写す**

先頭の文を一時ファイルに書き、設計書 §2.1〜§2.8 を見出しだけ付け替えて続ける。

Run:
```bash
mkdir -p "${TMPDIR:-/tmp}/uitk-w0-skill"
cat > "${TMPDIR:-/tmp}/uitk-w0-skill/catalog-header.md" <<'EOF'
# 部品カタログ（UITK の部品と Web の正本の対応）

部品を作る・使う・名前を決める前に開く。ここに有る部品を画面の中に作り直さず、無い部品を足すときは SKILL.md §3 の昇格規則に従う。

目次: 1 読み方と共通の約束 / 2 枠・面族 / 3 スロット族 / 4 操作部品族 / 5 表示・ゲージ族 / 6 文字・装飾族 / 7 事実上の共有部品と、画面に留めるもの / 8 Web の非 UI 共有モジュールの行き先

- **出所**: `docs/uitk-migration/execution/component-architecture.md`（設計書）§2 の写し。設計書の §2.n がこの文書の §n にあたる。本文の `§n.n`・`[A-n]`・`[Q-n]` は設計書の節と判断 ID、`S/` は `moorestech_client/Assets/Scripts/`、`W/` は `moorestech_web/webui/src/`、「段階・レーン」列は設計書の提案。持ち主は `docs/uitk-migration/execution/execution-plan.md` のレーン表が正本で、食い違うときはそちらに従う（`KeyCap`・`ShortcutHintLabel`・`GameScrollView`・Typography・Layout は C2-c）
- **直し方**: 部品を作ったレーンは、その行の「段階 1」列を `済み（<レーンID>）` に、公開 API の列を実装の公開 API どおりに直す。設計書の案から API を変えたときは、そのレーンの計画の判断記録に理由を書く。作られた部品は実コードが正本で、この表は引くための索引
- **列**: 部品（UITK）／責務／公開 API 案（C#）／USS・状態クラス／使う画面（Web の feature）／作る段階・レーン／Web の見た目の正本／段階 1 との関係（再利用・改修・新規）

EOF
SRC=docs/uitk-migration/execution/component-architecture.md
DST=.agents/skills/ref-uitk-design/references/component-catalog.md
{ cat "${TMPDIR:-/tmp}/uitk-w0-skill/catalog-header.md"
  awk '/^### 2\.1 読み方と共通の約束/{p=1} /^## 3\. 見た目を Web と一致させる仕組み/{p=0} p' "$SRC" \
    | sed -e '/^---$/d' -e 's/^### 2\./## /' -e '/^- \*\*列\*\*: 部品（UITK）/d'
} > "$DST"
wc -l "$DST"
grep -n '^## ' "$DST"
# 設計書 §2 の表の行がすべて写っていること（差分 0 行）
diff <(awk '/^### 2\.1 /{p=1} /^## 3\. /{p=0} p' "$SRC" | grep '^|') <(grep '^|' "$DST") && echo OK-rows
```
Expected: 行数が 130〜150、`## 1 読み方と共通の約束`・`## 2 枠・面族（…）`・`## 3 スロット族（…）`・`## 4 操作部品族（…）`・`## 5 表示・ゲージ族（…）`・`## 6 文字・装飾族（…）`・`## 7 事実上の共有部品と、画面に留めるもの`・`## 8 Web の非 UI 共有モジュールの行き先` の 8 見出し、`OK-rows`。2026-11-06 の設計書（`docs/uitk-migration/execution/component-architecture.md` の 472 行版）で回した実測は 140 行

- [ ] **Step 4: 目次と見出しを検査する**

Run:
```bash
R=.agents/skills/ref-uitk-design/references
for f in $R/*.md; do echo "$f $(wc -l < "$f") toc=$(grep -c '^目次: ' "$f")"; done
grep -c '^## [1-8]\. ' $R/css-to-uss.md
grep -c '^## [1-6]\. ' $R/compare-procedure.md
```
Expected: 3 ファイルとも `toc=1`、css-to-uss が `8`、compare-procedure が `6`

- [ ] **Step 5: コミットする**

```bash
git add .agents/skills/ref-uitk-design/references
git commit -m "docs(skill): ref-uitk-design の references（部品カタログ・CSS から USS への写し方・見た目の比べ方）を足す

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: SKILL.md を作る

**Files:**
- Create: `.agents/skills/ref-uitk-design/SKILL.md`

**Interfaces:**
- Consumes: Task 1 の references 3 本（パスと節名は Task 1 の Produces）
- Produces: スキル `ref-uitk-design`（description のトリガで全レーンの実装者が読む）。節番号 `## 1.`〜`## 13.` は設計書 §6 の目次 1〜13 と同じ並び。印〔レーンID〕の規則（冒頭の「印の読み方」）は、F1・C2-b・C3-a・C4-b などのレーンが自分の印を外すときの約束になる

- [ ] **Step 1: SKILL.md を書く**

`.agents/skills/ref-uitk-design/SKILL.md` を、次の内容そのままで作る:

````markdown
---
name: ref-uitk-design
description: moorestech のゲーム内 UI を UI Toolkit で作る規則と見た目の正本の引き方。Web 版と同じ見た目にするための層・部品カタログ・USS とトークン・画面の型・比べ方・例外の記帳を持つ。Use When — Client.Ui・Client.Ui.State・USS・tss のコードを読む・書く・レビューする時、部品・画面・HUD を足す時、色・寸法・配置・装飾・動き・入力を変える時、Web 版との見た目の差を直す・記帳する時。
---

# moorestech UITK の作り方と見た目

対象は `moorestech_client/Assets/Scripts/Client.Ui`（描画）と `Client.Ui.State`（画面状態）。略記: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/src/`。

**印の読み方**: 〔レーンID〕の付いた名前・仕組みは、そのレーン（`docs/uitk-migration/execution/execution-plan.md` のレーン表）が `feature/uitk` に入るまで存在しない。無い間は自分で作らず（そのレーンの持ち物）、括弧の中の今の形に従う。そのレーンは自分を取り込む PR でこの文書と references の自分の印を外す。

## 1. 見た目の正本は Web 版

- 全画面を Web 版と同じ見た目にする（ユーザーの裁定。移行で見た目を変えない）。例外はユーザー原文「無理に同じにする必要はない、影響が小さく非本質的な対応が入るのであれば、推奨でも良い」だけ（§11）
- 見る順: ① その画面・部品の Web の CSS module（`W/shared/ui/<部品>/`・`W/features/<機能>/`）② `moorestech_web/webui/docs/design/components.md` の該当節 ③ 参照キャプチャ `moorestech_logs/uitk/reference-captures/web-<sha>/`（撮影元は台帳 `docs/uitk/TODO.md` の「## 参照キャプチャ」。画面と状態の一覧は `index.md`）
- 数値は Web の CSS から逐語で写す（rem・em は ×16 の px）。目で合わせた値を足さない
- 面は Web 版のその画面と同じ枠にする。Web が GamePanel で描く画面は `GamePanel`、Mantine の枠で描く画面（ポーズ・設定・バグ報告・トースト・モーダル・チャレンジ）はその枠を写す。面を GamePanel に統一しない
- `ref-webui-design` のホワイトリスト（装飾 6 語彙・スロットは共有部品だけ・全画面の不透明面の禁止・stage 族と viewport 族）は、Web 版に無い新しい面を作るときにだけ効く。Web 版にある見た目はそのまま写す

## 2. 層と依存の向き

| 層 | 置き場 | 参照してよい | 参照しない |
|---|---|---|---|
| Foundation | `S/Client.Ui/Foundation/<領域>/` | Client.Input・Client.Localization・UniRx・UniTask・Client.Game の入力・UIState・アンカー ID | Components・Content・Screens・Client.Ui.State の機能別の型 |
| Components | `S/Client.Ui/Components/<族>/<部品>/` | Foundation・`Client.Ui.State` の `Text`（`UiText`）・`LocalizationKey` | Content・Screens・Client.Game・`MasterHolder`・Client.Ui.State の機能別の型 |
| Content〔C3-a〕 | `S/Client.Ui/Content/` | Foundation・Components の値型・Client.Game（`ClientContext`）・Core.Master | Screens |
| Screens | `S/Client.Ui/Screens/<機能>/` | 上のすべて・Client.Ui.State | 他の `Screens/<機能>` |
| State | `S/Client.Ui.State/<機能>/`（別 asmdef） | Client.Game・Client.Network・Core.Master | Client.Ui・UnityEngine.UIElements |

- 部品はドメインを知らない。ItemId・BlockId・Model を受けず、`UiIcon`〔C3-a〕・`UiText`・数・値型を受ける。ID から表示素材への解決は Content の `GameContentPresentation`〔C3-a〕の 1 か所に置く
- asmdef は `Client.Ui`・`Client.Ui.State` の 2 つ。層の向きは L2 の `UiLayerDependencyDirectionTest`〔F1〕が using で止める。破れたら asmdef の分割を裁定に上げる

## 3. 部品カタログの引き方

- 部品を作る・使う前に `references/component-catalog.md` で名前・置き場・公開 API・作るレーンを確かめる。カタログに有る部品を画面の中に作り直さない
- 名前は Web の `shared/ui/<名>` と同じ。Web に名前が無い Mantine 由来の見た目は役割の名前（`FormButton`・`DialogFrame`）にし、技術名を入れない
- Web で 2 feature 以上が使う部品は、その段の部品レーンが Components に直接作る。Web に無い新しい共有は 1 機能目を `Screens/<機能>/` に置き、2 機能目のレーンは自分で作らず指揮に申告する（integrator が波の境で Components へ移す）。1 画面の中の複数ファイルで使うだけの部品は `Screens/<機能>/Parts/`
- 部品を作ったレーンは、カタログの行の「段階 1」列をその部品の実装どおりに直す

## 4. 部品の書き方

```csharp
// web: W/shared/ui/IconButton（components.md §8.6）。形の例で、IconButton と IconButtonGlyph（例 enum IconButtonGlyph { Close, … }）は〔C2-b〕が作るまで実在しない
// web: W/shared/ui/IconButton (components.md §8.6). Shape example only; IconButton and IconButtonGlyph do not exist until C2-b
public sealed class IconButton : VisualElement
{
    private readonly Subject<Unit> _onClicked = new();
    public IObservable<Unit> OnClicked => _onClicked;

    public IconButton(string testid, LocalizationKey accessibleLabel, IconButtonGlyph glyph)
    {
        // testid は name へ、ボタンでもフォーカスは持たない
        // testid goes to name; buttons never take focus
        name = testid;
        focusable = false;
        AddToClassList("icon-button");
        // 押下は Subject で外へ出す（ラベルと × の描画は省略）
        // Presses leave through the Subject (label and the x glyph omitted here)
        RegisterCallback<ClickEvent>(_ => _onClicked.OnNext(Unit.Default));
    }
}
```

- ctor は `(string testid, …)` で `name = testid`（Web の `data-testid` と同じ綴り。DSL のシナリオがそのまま動く）。装飾だけの部品は testid を取らない。デフォルト引数を使わない
- 表示する値は `SetXxx(値)` で受ける。値を返す素通しのプロパティを作らない（`{ get; private set; }` は可）
- 操作は `IObservable<T> On<変化対象><過去分詞>`（中は UniRx の `Subject`）で出す。`Button.clicked`・`Action`・`event`・コールバック引数で外へ出さない。素の `Button` を中で使うなら `clicked` は部品の中で `Subject` へ流すだけにする
- `focusable = false`。フォーカスを持つのは入力欄だけ
- 描くだけの子（Painter2D・頂点カラー・装飾）と文字だけの子は `pickingMode = PickingMode.Ignore`。押せる要素だけが Pick を受ける
- 子を受ける部品（枠・スクロール）は `contentContainer` を返し、外は `Add` で入れる
- 色・寸法を C# に書かない。描画で値が要るときは部品の USS で `--<部品>-<名>: var(--トークン);` と受け直し、`CustomStyleResolvedEvent` の中で `UssCustomStyleReader.TryReadColor`・`TryReadPixels` で読む（前例 `S/Client.Ui/Components/GamePanel/GamePanelFace.cs`）
- `Update()` を使わない。変化は Set の呼び出しか Model の購読で受ける
- AGENTS.md のとおり 1 ファイル 200 行・1 ディレクトリ 10 ファイル・`partial` と `Func<>` の禁止・日英 2 行のコメント

## 5. USS の書き方

1. 1 部品 1 USS を `.cs` の横に置く（`Components/<族>/<部品>/<部品>.uss`）。画面の USS（`Screens/<機能>/<機能>.uss`）は配置とつまみの上書きだけを持つ
2. 新しい USS は `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の自分の族の区画に `@import url("project://database/Assets/Scripts/Client.Ui/<…>.uss");` を 1 行足す〔F1〕（今の形: ctor が `StyleSheet` を受け、`UiAssetCatalog` に登録する）。束ね漏れと余りは `UiThemeImportBundleTest`〔F1〕が落とす
3. 規則はルートのクラスで始める（`.item-slot …`）。クラスは BEM（ルートは部品名のケバブ、子は `__`、状態は `--`）。ルートのクラス名は全 USS で一意
4. 他の部品のクラスを書かない。画面が部品の見た目を変えてよいのは、つまみ（Web の局所上書き変数と同じ名前のカスタムプロパティ）を画面のルートで上書きするときだけ。例 `.hotbar-hud { --slot-size: var(--hotbar-slot-size); }`
5. 状態はクラス `--<状態>` にし、付け外しは部品の Set メソッドの `EnableInClassList` だけが行う。`:hover`・`:active`・`:focus` は使ってよい。構造擬似クラス（`:first-child` 等）と z-index は使わず、C# でクラスを付ける・層で決める
6. 先頭に出所を日英 1 行ずつ書く: `/* web: W/shared/ui/SlotFrame/style.module.css（components.md §8.6） */`
7. CSS を写すとき（rem・calc・color-mix・data 属性・`::before`・grid・コンテナクエリ・影・グラデ・マスク・文字の縁・半透明）は `references/css-to-uss.md` を開く。USS に無い表現を自分で工夫する前に前例を見る

## 6. トークン

| ファイル（`S/Client.Ui/Foundation/Theme/`） | 出所 | 編集 |
|---|---|---|
| `Tokens.uss`・`SlotTokens.uss`・`SkitTokens.uss` | `W/app/tokens.css`・`slotTokens.css`・`skitTokens.css` | 生成物。手で直さず、リポジトリ直下で `node tools/uitk/convert-web-tokens.mjs`。名前の一致は `WebTokenParityTest` |
| `MantineTokens.uss`〔F1〕 | Mantine の暗色テーマの `--mantine-*` | 生成物。手で直さない |
| `DerivedTokens.uss`〔F1〕（今は `ControlTokens.uss`） | Web の `color-mix`・`rgb(from …)`・半透明・直書きの色 | 手で書く。各行に `/* web: 元の式 決め方 */` |
| `Theme.uss` | Mantine の既定コントロール | 既定コントロールを `--mantine-*` で塗る〔F1〕（今は `--control-*`） |
| `Typography.uss`（クラスの拡充は〔C2-c〕）・`Layout.uss`〔C2-c〕 | Mantine の Text/Title・Stack/Group | カタログの「文字・装飾族」 |

- 部品・画面の USS の色は `var(--…)` だけ。`#hex`・`rgb()` は `UiStyleConventionTest` が落とす。新しい色が要るときは `DerivedTokens.uss`〔F1〕に元の式と決め方を付けて足す
- 寸法は Web がトークンで書く値はトークン、Web が直書きする値は同じ直書きで写す
- 半透明は Web の α をそのまま使わない（UITK は Linear 色空間で混ぜるので薄く出る）。決め方は `references/css-to-uss.md` の「半透明」
- 文字の大きさは Web と同じ px にする。13px 下限は外す（設計書 [A-39]。`TokenFloorAndTranslucencyTest` の下限は F1 が外す〔F1〕。F1 の取り込み前は下限テストが残る）
- フォントは `Typography.uss` が当てる SDF だけを使う。合成の太字（`-unity-font-style: bold`）は使わない

## 7. 層と族

- 層は `UiLayer`（`S/Client.Ui/Foundation/Layers/UiLayer.cs`）の固定順の兄弟コンテナ。順の正本は `W/app/tokens.css` の `--z-*`。層を足すのは integrator で、enum と `UiLayerOrderTest` を同時に直す。画面・部品のレーンは足さない
- 載せ方は `root.GetLayer(UiLayer.Screen, UiFamily.Stage)`〔F1〕（今は `GetLayer(UiLayer)`）。stage 族（1280×720 の舞台で一様に拡縮。パネル・グリッド）か viewport 族（実画面の辺に付く。常時 HUD）かは Web の所属（`ref-webui-design` §1.5）と同じにする
- 層コンテナ・ルート・幕・描くだけの子は `PickingMode.Ignore`。忘れるとワールドのクリックとパネル外の右短押しが効かなくなる
- 全体スイッチ（Web／UITK）を Presenter・View・部品が読まない。UITK のときだけ composition root（`S/Client.Starter/Registration/MainGameUiRegistration.cs`）が Presenter を登録する。入力の裁き（`UiPointerHitTest`・`UiInputExclusivity`）・DSL のロケータ・アンカーの解決はスイッチの系の値だけを読み、Web と UITK の値を論理和にしない

## 8. 画面の型

| ファイル | 持つもの |
|---|---|
| `S/Client.Ui.State/<機能>/<機能>ViewModel.cs` | 不変 sealed。判別 union は抽象基底＋sealed 派生 |
| `S/Client.Ui.State/<機能>/<機能>Model.cs` | `IReadOnlyReactiveProperty<<機能>ViewModel> Current`（源を購読して作る） |
| `S/Client.Ui.State/<機能>/I<機能>Commands.cs`・`<機能>Commands.cs` | 型付きコマンド。`UiCommandResult` を返し、拒否は理由 enum とログ |
| `S/Client.Ui/Screens/<機能>/<機能>ScreenPresenter.cs`（HUD は `<名>HudPresenter`） | 起動時に 1 回マウント・ルータで表示を切替・表示中だけ Model を購読。判断を持たない |
| `S/Client.Ui/Screens/<機能>/<機能>View.cs`（HUD は `<名>HudView`） | 部品を組む。`Render(viewModel)` で値を受け、操作は Commands を直接呼ぶ。結果は `UiCommandResultToaster.Report`〔F1〕（今は `GameUiToastFeed.ShowRejected`） |
| `S/Client.Ui/Screens/<機能>/<機能>.uss` | 配置とつまみの上書きだけ |

- 前例は `S/Client.Ui/Screens/PauseMenu/`（`PauseMenuScreenPresenter`・`PauseMenuView`）
- View は C# で組む。UXML と UI Builder は使わない
- 出し分けは `UiRoutingState.Current.Screen` と `UiScreenTraitsTable` だけで決める
- 毎フレームの値（採掘の進捗など）は Model がフレーム末に畳んで流す。View は `Update()` を持たない

## 9. 動き

- 入退場とホバーは USS の `transition`。追加した直後のクラスの付け外しは補間されないので `GeometryChangedEvent` を契機にする。`height: auto` は補間できないので px に固定してから動かす
- ループ・キーフレーム・smooth scroll・慣性は `UiMotionPlayer`（`S/Client.Ui/Foundation/Motion/`）の Step で書く。足りない Step は使う段のレーンが足す
- 尺と曲線はトークンに置き、既存の値を参照する（通知の生存 7000ms・入場 160ms・退場 200ms・脈動 1200ms）
- テストのために尺を 0 にする抜け道を作らない。テストは時計（`IUiMotionClock`）を進める

## 10. 入力

- フォーカスを持つのは入力欄（`GameTextField`）だけ。Space・Enter でボタンが押されず、Tab でフォーカスが動かないことを L1 で固定する
- ESC は UIState が持つ。入力欄での ESC はフォーカスを外すだけにし、値を戻す既定の処理を止める
- 文字入力中の移動の停止と IME のモードは `UiInputExclusivity`・`ImeCompositionModeApplier` の 1 か所で決める。画面で `imeCompositionMode` を触らない
- 画面のキー処理（ESC・←→・Enter）は Presenter か UIState に集める
- 入力の規則の根拠は `origin/master:docs/uitk-migration/plan.md` の §3.8

## 11. 比べ方と例外の記帳

- 部品は部品ギャラリー〔F1〕で全状態を並べて撮り、画面は PlayMode・全体スイッチ UITK で 720p と 2560×1080 を撮る。Web の参照と状態ごとに並べて PR の成果物の載せ先に載せ、差を作業報告に表で書く。手順・置き場・合否の目安は `references/compare-procedure.md`
- `uloop screenshot` は `--capture-mode window` で撮る。`rendering` には UITK が写らない
- 例外にしてよい条件は `references/compare-procedure.md` §6 だけが決める（ここに書き写さない）
- 例外は台帳 `docs/uitk/TODO.md` の「## 見た目の例外」〔F1〕に `LX-n` で書き、その USS・C# の行に `/* look-exception: LX-n */` を付ける

## 12. やらないこと

- UXML・UI Builder にしか無い状態。インラインの style は計算で決まる位置・寸法だけ
- 部品・画面の USS での色の直書き・構造擬似クラス・z-index・合成の太字・ビットマップの参照・UI 装飾の画像化
- 他の部品の中身（子のクラス・状態クラス・`style`）を外から塗る
- `Func<>`・`partial`・デフォルト引数・`Action`/`event`/`Button.clicked` で外へ出すイベント
- Components から Client.Game・`MasterHolder`・機能別の State を参照する
- `feature/uitk` で `moorestech_web/` を書き換える（Web の変更は master の取り込みでだけ入る）
- 記帳なしに Web と違う見た目にする

## 13. 検証

| 層 | 何を | 土台 | 置き場 | 最小 |
|---|---|---|---|---|
| L0 | 純ロジック・ViewModel の導出・コマンドの前提 | EditMode NUnit | `S/Client.Tests/Ui/State/<機能>/` | 写す Web の vitest の全件。元のテスト名を XML コメントに書く |
| L1 部品 | 状態クラス・つまみの効き（`resolvedStyle`）・イベント・`PickingMode`・`focusable` | `UiTestHost`（本番のテーマ・1280×720） | `S/Client.Tests/Ui/Components/<族>/` | 状態ごと 1・イベントごと 1・つまみ上書き 1 |
| L1 画面 | View＋Model＋Commands の結線 | `ScreenTestHost`・`FakeModel<T>`・`CommandRecorder<T>` | `S/Client.Tests/Ui/Screens/<機能>/` | Web のコンポーネントテストと e2e のうちブラウザ固有でない全件 |
| L2 | 規約 | `UiStyleConventionTest` ほか | `S/Client.Tests/Ui/Conventions/` | 規約を足したら違反の文字列のケースを 1 つ |
| L3 | 実ゲームの結線 | EditModeInPlayingTest | `S/Client.Tests/EditModeInPlayingTest/Ui/` | 段ごとに 1 本（スイッチ UITK で開く・閉じる） |
| L4 | 録画のプレイテスト | `unity-playmode-recorded-playtest` | `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/` | plan §4.4 のラダー |

- 回し方: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\."`
- 写したテストと Web の元テストの対応は `docs/uitk/disposition.md` に書く
````

- [ ] **Step 2: 形式・行数・リンクを検査する**

Run:
```bash
S=.agents/skills/ref-uitk-design
wc -l < $S/SKILL.md
grep -c '^## ' $S/SKILL.md
for f in $(grep -o 'references/[a-z-]*\.md' $S/SKILL.md | sort -u); do test -f "$S/$f" && echo "OK $f" || echo "MISSING $f"; done
(cd ~/.agents/skills/create-skill && uv run --with pyyaml python3 -m scripts.quick_validate "$OLDPWD/$S")
grep -n 'Web 版のその画面と同じ枠\|面を GamePanel に統一しない' $S/SKILL.md
```
Expected: 行数 200 以下（2026-11-06 の下書きで 167）、`13`、`OK references/compare-procedure.md`・`OK references/component-catalog.md`・`OK references/css-to-uss.md`（`MISSING` 0 件）、`Skill is valid!`、最後の grep は 1 行（§1 の「面は Web 版のその画面と同じ枠にする。…面を GamePanel に統一しない」の行）。`uv` が無い環境では `python3 -m pip install --user pyyaml` の後に `python3 -m scripts.quick_validate` で同じ検査をする

- [ ] **Step 3: 印の無い名前が実在することを確かめる**

SKILL.md と references が印〔…〕を付けずに書いた型名・ファイルは、今の `feature/uitk` に実在しなければならない（実在しない名前に印が付いていないと、実装者が探して迷う）。

Run:
```bash
for name in UiLayer.cs UiLayerStack.cs GameUiRoot.cs UssCustomStyleReader.cs GamePanelFace.cs GamePanelDecoLine.cs GamePanelBottomDeco.cs \
  PauseMenuScreenPresenter.cs PauseMenuView.cs UiMotionPlayer.cs IUiMotionClock.cs ScrollOffsetTweenStep.cs UiInertiaAnimation.cs \
  ImeCompositionModeApplier.cs GameTextField.cs UiRoutingState.cs UiScreenTraits.cs UiCommandResult.cs GameUiToastFeed.cs UiText.cs \
  UiTestHost.cs ScreenTestHost.cs FakeModel.cs CommandRecorder.cs UiStyleConventionTest.cs UiLayerOrderTest.cs WebTokenParityTest.cs \
  TokenFloorAndTranslucencyTest.cs MainGameUiRegistration.cs UiAssetCatalog.cs ControlTokens.uss Tokens.uss SlotTokens.uss SkitTokens.uss \
  Theme.uss Typography.uss GameUiTheme.tss convert-web-tokens.mjs translucency.mjs capture.ts translucent-tokens.md disposition.md; do
  hit=$(git ls-files | grep -c "/$name\$")
  [ "$hit" -ge 1 ] && echo "OK $name" || echo "ABSENT $name"
done
for sym in UiPointerHitTest UiInputExclusivity UiScreenTraitsTable; do
  git grep -q "class $sym\|interface $sym" -- moorestech_client/Assets/Scripts && echo "OK $sym" || echo "ABSENT $sym"
done
# SKILL.md のコード例の型（実在しない）は、例の見出しコメントに印が要る
S=.agents/skills/ref-uitk-design
awk '/^```csharp/{c=1;next} /^```/{c=0} c' $S/SKILL.md | grep '〔C2-b〕' | grep -q 'IconButtonGlyph' && echo "OK-marked IconButton IconButtonGlyph" || echo "UNMARKED IconButton IconButtonGlyph"
# 印のレーン ID が、実行計画のレーン表でその名前を中身に含む行の ID と一致すること
EP=docs/uitk-migration/execution/execution-plan.md
grep -oh '`[^`]*`〔[A-Za-z0-9-]*〕' $S/SKILL.md $S/references/*.md .agents/skills/moores-code-review/references/moores-reviewer-digest.md | sort -u | while IFS= read -r m; do
  name=$(printf '%s' "$m" | sed -e 's/^`\([^`(]*\).*/\1/' -e 's/\.[a-z]*$//'); lane=$(printf '%s' "$m" | sed 's/.*〔\(.*\)〕$/\1/')
  owners=$(grep '^| [A-Z][A-Za-z0-9-]* |' "$EP" | grep -F -- "$name" | awk -F'|' '{gsub(/ /,"",$2); print $2}' | sort -u | tr '\n' ' ')
  if [ -z "$owners" ]; then echo "UNLISTED $name〔$lane〕"; elif printf ' %s' "$owners" | grep -q " $lane "; then echo "OK $name〔$lane〕"; else echo "NG $name〔$lane〕 owners: $owners"; fi
done
```
Expected: `ABSENT` 0 件、`OK-marked`、`NG` 0 件。`ABSENT` が出たら、その名前を使う行に〔その名前を入れるレーンID〕を付けるか（`docs/uitk-migration/execution/execution-plan.md` のレーン表で持ち主を引く）、実在する名前へ直してから Step 2 を回し直す。`NG` が出たら印をレーン表の持ち主（`owners` のうち部品・基盤を作る行。画面レーンは使う側）に直す。`UNLISTED`（レーン表の行に名前が出ない。例 `GetLayer`・`Layout`）は、設計書 §5 と実行計画の該当行を読んで持ち主を手で確かめ、`done.md` に名前と確かめた行を書く

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/ref-uitk-design/SKILL.md
git commit -m "docs(skill): UITK の作り方と見た目の正本を引く ref-uitk-design を新設する

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: moores-reviewer-digest に UITK の観点を足す

**Files:**
- Modify: `.agents/skills/moores-code-review/references/moores-reviewer-digest.md`（末尾に追記。既存の 1〜16 は変えない）

**Interfaces:**
- Consumes: Task 2 の `.agents/skills/ref-uitk-design/SKILL.md`（digest から `.claude/skills/ref-uitk-design/SKILL.md` として指す）
- Produces: 項目 17〜23。SDD の実装者・タスクレビュアー（`implementer-contract.md`・`task-reviewer-contract.md`）が毎タスク読む

- [ ] **Step 1: 末尾に追記する**

Run（ファイル末尾が改行で終わっていることを先に確かめてから足す）:
```bash
D=.agents/skills/moores-code-review/references/moores-reviewer-digest.md
tail -c1 "$D" | od -c | head -1
cat >> "$D" <<'EOF'

## UITK（`Client.Ui`・`Client.Ui.State`・USS・tss を触るときだけ。作り方の正本は `.claude/skills/ref-uitk-design/SKILL.md`）

17. **PickingMode の既定** — 層コンテナ・ルート・幕・描くだけの子（Painter2D・頂点カラー・装飾・文字だけの Label）が `PickingMode.Ignore` か。押せない要素が Pick を受けると、ワールドのクリックとパネル外の右短押しが効かなくなる。逆に押せる要素を Ignore にしていないか。
18. **全体スイッチの片側読み（R2）** — 入力の裁き（`UiPointerHitTest`・`UiInputExclusivity`）・DSL のロケータ・アンカーの解決が、全体スイッチの系の値だけを読んでいるか。Web と UITK の値を論理和にしていないか。Presenter・View・部品がスイッチを読んでいないか（分岐は composition root の 1 か所）。
19. **トークンの直書き** — 部品・画面の USS と C# に色（`#hex`・`rgb()`・`new Color(`）を直に書いていないか。色は `var(--…)`、C# が色・寸法を要るときは部品の USS で受け直して `UssCustomStyleReader` で読む。Web の値を写した半透明に α 補正が付いているか。Web がトークンで書く寸法を数値で書いていないか。
20. **イベントの出し方** — 部品・View が `Button.clicked`・`Action`・`event`・コールバック引数で操作を外へ出していないか。`IObservable<T> On<変化対象><過去分詞>`（UniRx の `Subject`）へ。`clicked` は部品の内側で `Subject` へ流すときだけ使う。
21. **部品の中身を外から塗る** — 画面の USS や別の部品の USS が他部品の子クラス（`__`）・状態クラス（`--`）を書いていないか、C# から他部品の子へ `style.*`・`AddToClassList` していないか。外から変えてよいのはつまみ（Web の局所上書き変数と同じ名前のカスタムプロパティ）だけ。
22. **層の依存方向** — `Components/` が `Client.Game`・`MasterHolder`・`Client.Ui.State` の機能別の型・`Client.Ui.Content`〔C3-a〕・`Client.Ui.Screens` を参照していないか。`Content/` が `Screens` を、`Screens/<機能>` が別の `Screens/<機能>` を、`Client.Ui.State` が `UnityEngine.UIElements` を参照していないか。部品が ItemId などの ID を受けていたら、解決を Content へ移す。
23. **tss の束ね漏れ** — 新しい USS が `GameUiTheme.tss` の自分の族の区画に `@import` されているか、消した USS の `@import` が残っていないか（tss 束ねの導入前は `UiAssetCatalog` への登録と ctor の `StyleSheet`）。漏れると見た目が無言で崩れる。
EOF
```
Expected: `od` の出力が `\n` を含む（含まなければ先に `printf '\n' >> "$D"` を実行してから `cat >>` する）

- [ ] **Step 2: 追記だけであることを確かめる**

Run:
```bash
D=.agents/skills/moores-code-review/references/moores-reviewer-digest.md
git diff --numstat -- "$D"
grep -c '^1[7-9]\. \|^2[0-3]\. ' "$D"
grep -n '^## UITK' "$D"
```
Expected: `--numstat` の削除数が `0`（例 `10	0	…`）、`7`、`## UITK（…）` が 1 行

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/moores-code-review/references/moores-reviewer-digest.md
git commit -m "docs(skill): moores-reviewer-digest に UITK の観点（PickingMode・片側読み・トークン・イベント・外から塗る・層の向き・tss）を足す

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: create-skill のレビューモードで合否を出す

**Files:**
- Modify: `.agents/skills/ref-uitk-design/SKILL.md`・`.agents/skills/ref-uitk-design/references/*.md`（指摘の反映だけ）
- Create（コミットしない）: `../moorestech_logs/harness/uitk/W0-skill/skill-review.final.md`（評価役の報告）

**Interfaces:**
- Consumes: Task 1〜2 のスキル一式
- Produces: 反映済みのスキル、`done.md` の「create-skill レビュー」節（指摘ごとの「直した／直さない理由」）

- [ ] **Step 1: 評価役 1 体に別コンテキストでレビューさせる**

create-skill の Phase 5（既存スキルのレビューだけの依頼と同じ）。評価役には作成の経緯を渡さず、チェックリストと対象だけを渡す。依頼文を書く:
```bash
mkdir -p ../moorestech_logs/harness/uitk/W0-skill
cat > "${TMPDIR:-/tmp}/uitk-w0-skill/review-prompt.md" <<EOF
あなたはスキルのレビュアーです。ファイルは編集しないでください。
1. $HOME/.agents/skills/create-skill/references/review-checklist.md を読む
2. 対象スキル $(pwd)/.agents/skills/ref-uitk-design/ の SKILL.md と references/ をすべて読む
3. チェックリストの全項目で評価し、チェックリスト末尾のレポートテンプレートで返す（Critical / Warning / Info）
前提: このスキルは moorestech の UI Toolkit 移行の各レーンが読む知識スキル（ref- 接頭辞は「読むだけで何も変えない」契約）。
〔レーンID〕の印は、そのレーンが入るまで存在しない名前を示す約束で、本文冒頭に定義がある。
EOF
```
- Claude Code のホスト: Agent ツールで subagent を 1 体（model は opus）起動し、上の依頼文の中身をそのまま渡す。返答の全文を `../moorestech_logs/harness/uitk/W0-skill/skill-review.final.md` に保存する
- Codex のホスト（Agent ツールが無い）: 前景で次を実行する
```bash
codex exec -m gpt-6-astra -s read-only -C "$(pwd)" -o ../moorestech_logs/harness/uitk/W0-skill/skill-review.final.md - < "${TMPDIR:-/tmp}/uitk-w0-skill/review-prompt.md"
```
Expected: `skill-review.final.md` が実在し、`Critical`・`Warning`・`Info` の節を持つ。空・途中で切れているときは同じコマンドを 1 回だけ回し直し、それでも駄目なら `done.md` の「未完了」に生のエラーを書いて止める

- [ ] **Step 2: Critical と Warning を全部直す**

指摘ごとに SKILL.md・references を直す。直すときの約束:
- SKILL.md は 200 行以内を保つ（超えるなら詳細を references へ移す）
- 裁定・設計書に由来する規則（Web 版と同じ見た目・面は Web 版の枠・層の向き・つまみ・印〔レーンID〕）を、指摘を理由に消したり緩めたりしない。そうした指摘は「直さない理由: 裁定 <ファイル名>」として残す
- component-catalog.md の §2〜§8 の表は設計書の写しなので中身を書き換えない（先頭の目次・出所・直し方の文だけ直してよい）。表の中身への指摘は「直さない理由: 設計書が正本」として残す
- Info は採るかどうかを判断し、採らないものは理由を 1 行書く

- [ ] **Step 3: 直した後の検査を回す**

Run: Task 1 Step 4、Task 2 Step 2・Step 3 と同じコマンド
Expected: 同じ期待値（行数 200 以下・`Skill is valid!`・`MISSING` 0・`ABSENT` 0・目次あり）。再レビューはしない（create-skill の Phase 5 の規定）

- [ ] **Step 4: done.md に記録してコミットする**

`../moorestech_logs/harness/uitk/W0-skill/done.md` に「## create-skill レビュー」節を作り、`skill-review.final.md` のパスと、指摘ごとに `- [Critical|Warning|Info] <指摘の要約> → 直した（<ファイル:節>）／直さない（<理由>）` を書く。`../moorestech_logs` の README の規約に従ってそちらの repo にコミットする。スキル側は:
```bash
git add .agents/skills/ref-uitk-design
git commit -m "docs(skill): ref-uitk-design に create-skill レビューの指摘を反映する

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（スキル側に変更が無ければコミットしない）

---

### Task 5: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜4 のコミット一式（`origin/feature/uitk..HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。Codex ホストでは `.agents/skills/moores-code-review/references/codex-host.md` の手順で `scripts/codex_workflow_runner/run.mjs` を使う。レビューが裁定・設計書と食い違う直し（「面は GamePanel に統一」「つまみでなく画面 USS から子クラスを塗る」等）を提案したら採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば検査を回し直してコミットする**

Run: Task 1 Step 4、Task 2 Step 2・Step 3、Task 3 Step 2 と同じコマンド
Expected: 同じ期待値。続けて:
```bash
git add .agents/skills/ref-uitk-design .agents/skills/moores-code-review/references/moores-reviewer-digest.md
git commit -m "fix(skill): W0-skill の moores-code-review 反映

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（自動適用が無ければコミットしない）

---

### Task 6: feature/uitk への PR と取り込み（必須）

**Files:**
- なし（PR・マージ・後片付け）

**Interfaces:**
- Consumes: Task 5 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後のレーン（F1・C2-*・S2-* …）の開始プロンプトが指す `.agents/skills/ref-uitk-design/SKILL.md` が `feature/uitk` に実在する

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（文書だけの変更なので `media` は空）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": ["UITK の作り方と見た目の正本を引く知識スキル ref-uitk-design（SKILL.md と references 3 本）を新設した", "moores-reviewer-digest に UITK の観点 17〜23 を足した"],
  "media": [],
  "no_media_reason": "スキル文書だけの変更で、画面・挙動の変更が無い",
  "verification": ["quick_validate: Skill is valid!", "SKILL.md <行数> 行（200 以下）・リンク MISSING 0・印の無い名前 ABSENT 0", "create-skill レビュー: Critical <n>・Warning <n> を反映（done.md）"]
}
```
`python3 /Users/sakastudio/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない

- [ ] **Step 2: feature/uitk の進みを取り込み、G1 緑を見る**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
git push
gh pr checks <PR番号>
```
Expected: 進んでいなければ `Already up to date.`。進んでいて衝突したら、`moores-reviewer-digest.md` は両方の追記を採って項目番号を通し番号に振り直し、ほかは止めて指揮に報告する。進んでいたら Task 2 Step 3（印の無い名前の実在）を回し直す（F1 等が先に入っていれば、その印を外してよいかもここで分かる。外すのは印の持ち主のレーンの仕事なので、ここでは外さない）。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`。checks が終わっていなければ前景で `gh pr checks <PR番号> --watch` を実行して待つ

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ

- [ ] **Step 4: 後片付けと記録**

Run:
```bash
bd close moorestech-albof.19 --reason "W0-skill: PR #<番号> で ref-uitk-design を新設（面は Web 版の枠・見た目の正本は Web 版と参照キャプチャ）し、moores-reviewer-digest に UITK の観点 17〜23 を足して feature/uitk へ取り込んだ"
```
`done.md` の証拠に PR 番号・マージコミット・G1 のジョブ URL・各検査の出力を足す。続けて `grep -n 'F1 で \*\*新設\|比較 HTML\|uitk/compare/' docs/uitk-migration/execution/component-architecture.md docs/uitk-migration/execution/execution-plan.md` を回し、当たりが残っていれば判断記録 D-11・D-12 のとおり `done.md` の「要裁定」に当たった行を並べて書く。未コミット・未 push が無いことを `git status --short`（空）と `git log origin/uitk-lane/w0-skill..HEAD`（空）で確かめ、指揮が `moores-wt rm <この worktree の絶対パス>` を実行できる状態（全作業がコミット・push 済みで PR がマージ済み）を確認して終える

---

## Self-Review

1. Requirements coverage: R1 → Task 2 Step 1・2、R2 → Task 2 Step 1・2（grep）、R3 → Task 1 Step 3、R4・R5 → Task 1 Step 2・4、R6 → Task 2 Step 2、R7 → Task 3、R8 → Task 2 Step 3、R9 → Task 4、R10 → Task 5・6。漏れなし。設計書 §6 の目次 0〜13 と本文の対応: 0 → description、1 見た目の正本 → §1、2 層と依存 → §2、3 カタログの引き方 → §3、4 部品の書き方 → §4、5 USS → §5、6 トークン → §6、7 層と族 → §7、8 画面の型 → §8、9 動き → §9、10 入力 → §10、11 比べ方と例外 → §11、12 やらないこと → §12、13 検証 → §13
2. Placeholder scan: `<番号>`・`<HEAD 10 桁>`・`<行数>`・`<n>` は実行時にしか決まらない値で、取り方を同じステップに書いた。スキル本文の `<部品>`・`<機能>`・`<レーンID>` は規則の変数で、プレースホルダではない。「TBD」「適切に」は無い。SKILL.md §4 と css-to-uss §8 のコードは形の例で、実在の前例のパスを同じ節に書いた
3. Type consistency: SKILL.md の部品名・API（`IconButton(string testid, LocalizationKey accessibleLabel, IconButtonGlyph glyph)`・`IObservable<Unit> OnClicked`・`GetLayer(UiLayer, UiFamily)`・`UiCommandResultToaster.Report`・`GameContentPresentation`・`UiIcon`）は設計書 §1.2・§2.4・§4.2 と同じ綴り。L2 テスト名（`UiThemeImportBundleTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`・`CalcConstantRecheckTest`）は実行計画の F1 行と同じ綴り。印の無い既存名は Task 2 Step 3 で実在を機械検査する
4. 保留・縮退経路: 前提欠け（Task 1 Step 1 の `MISSING-design`・`EXISTS-skill`）と評価役の失敗（Task 4 Step 1）は、書き換えずに止めて指揮へ理由を報告する。解消の条件は「W0-merge が `feature/uitk` に入る」「評価役が報告を返す」で、どちらも指揮がレーンを再起動すれば到達できる。印〔レーンID〕の「無い間は作らない」は、そのレーンが入ると解消する（印の持ち主が外す。持ち主のレーンが計画から消えた場合は、指揮が波の境で印を外すか持ち主を付け替える）
5. 「決定的」を根拠にした選択: カタログを機械的に写すのは決定性のためではなく、設計書と食い違わないため（Task 1 Step 3 の行差分 0 で確かめる）
6. 呼び出し側への責務漏出: 新設サービス・API は無い（該当なし）

2026-11-06 に、SKILL.md と references 3 本の下書きを scratch に書き、`quick_validate`（`Skill is valid!`）・行数（SKILL.md 167・catalog 140・css-to-uss 129・compare-procedure 75）・Task 1 Step 3 のコマンドを `docs/uitk-migration/execution/component-architecture.md`（472 行版）に対して実際に回した。

---

## 判断記録（ADR）

従う裁定・設計: Global Constraints の裁定 6 本、設計書 `docs/uitk-migration/execution/component-architecture.md` の §6（スキルの目次）・[A-17]（新設する）、実行計画 `docs/uitk-migration/execution/execution-plan.md` の W0-skill 行。

| ID | 判断 | 棄却した案と理由 | 出所 |
|---|---|---|---|
| D-1 | スキルを W0 で作り（設計書 §6・[A-17] の「F1 で新設」は実行計画の W0-skill 行に合わせて上書き。設計書の文言の直しは D-11）、まだ無い名前・仕組みには〔レーンID〕を付けて「無い間の今の形」を括弧で書く。印は持ち主のレーンが自分を取り込む PR で外す | 設計書 [A-17] どおり F1 で作る: 実行計画が W0-skill を W0 に置いており、F1 の前に走る W0-tools・L0-* と、F1 の実装者自身が引く正本が無くなる。今の形だけを書く: F1 の直後に全文が古くなり、C2 以降のレーンが段階 1 の形（`StyleSheet` 引数・`--control-*`）を前例として写す | agent 既定 |
| D-2 | 部品カタログは設計書 §2 をコマンドで機械的に写し、表の中身は手で変えない。部品を作ったレーンが「段階 1」列と API 列を実装どおりに直す（作られた部品は実コードが正本、カタログは索引） | 要約して写す: 設計書と食い違い、どちらが正か迷う。スキルからは設計書を指すだけにする: 設計書は段階ごとの改訂の記録で、実装済みの状態を持たない | agent 既定 |
| D-3 | 並べ比べは PR の成果物の載せ先（Mac mini では PR 報告ページ）に状態ごとの画像の組で載せ、差の表は作業報告 `done.md` に書く。画像の原本は worktree の gitignore 下 | 設計書 §3.3 の `moorestech_logs/uitk/compare/<レーン>/` に比較 HTML を置く: AGENTS.md が `moorestech_logs` に置く画像をスクショ 3 枚程度までに限っており、報告ページの書式は `report_html.py` だけが決める規約（PR ごとに独自の HTML を書かない）とも食い違う | agent 既定（AGENTS.md・環境ローカル規約に合わせて設計書 §3.3 の置き場を読み替えた。設計書・実行計画の文言の直しは D-12） |
| D-4 | moores-code-review の UITK 観点は digest（実装前・タスクレビューのチェックリスト）に足すだけにし、`reviewers/` に UITK の reviewer を新設しない。機械で止められる観点（束ね漏れ・層の向き・色の直書き・例外の照合）は F1 の L2 テストが止める | reviewer `moores-any-uitk-conventions` を新設する: `scripts/model_map.json` の方針「reviewers/ には採用実績のある観点のみ」に反し、`orchestrator-steps.md` の本数・配線テストの改修も要る。レーンのレビューで同じ UITK の指摘が繰り返し出たら、その実績で新設を提案する | agent 既定 |
| D-5 | このレーンに unityプレイ録画テストも EditModeInPlayingTest も入れない | — : スキル文書と digest だけで、ランタイム挙動に触れない | agent 既定（moores-writing-plans の規定どおり 1 行で記録） |
| D-6 | 文字の大きさは「Web と同じ px。13px 下限は外す（設計書 [A-39]。F1 が下限テストを外す〔F1〕）」と書く | 「Q-1 の裁定どおり」と書いて答えを先取りしない（旧版）: Q-1 は全体レビューで上位の裁定から答えが決まる agent 既定へ下げた（実行計画 E-11）ので、待つ相手がいない | agent 既定（設計書 [A-39]・実行計画 E-11） |
| D-7 | create-skill のレビューモードは、Claude Code のホストでは Agent ツールの subagent 1 体、Codex のホストでは `codex exec -m gpt-6-astra -s read-only` 1 本で回す。再レビューはしない | 実装者が自分で読み直す: create-skill の Phase 5 が「書いた本人のコンテキストでは同じ所を見落とす」として別コンテキストを求めている | agent 既定（create-skill の規定に合わせた） |
| D-8 | SKILL.md §4 の形の例は、設計書 §2.4 の `IconButton` の ctor をそのまま使う（中身の描画は省略と明記） | 架空の部品名で例を書く: 実装者が実在の部品と取り違える。完全な実装を載せる: C2-b の持ち物を先取りする | agent 既定 |
| D-9 | `ref-webui-design` のホワイトリストは「Web 版に無い新しい面を作るときだけ効く」と書き、Web 版にある見た目はホワイトリストに無くても写す | ホワイトリストを UITK にもそのまま当てる: Mantine 由来の枠（ポーズ・トースト等）が写せず、裁定「Web 版と同じ見た目」と食い違う | plan §3.12（agent 既定）と裁定 2026-11-06 の 2 本 |
| D-10 | digest の UITK 節の指し先は `.claude/skills/ref-uitk-design/SKILL.md`（symlink 側の綴り） | `.agents/skills/…` と書く: digest の既存の行と SDD の契約文書が `.claude/skills/…` で指しており、綴りがそろわない | agent 既定 |
| D-11 | 要裁定（指揮）: 設計書 §6・[A-17] の「F1 で新設」を「W0-skill で新設」に直すのは W0-merge（設計書と実行計画一式を同梱する PR）。本レーンは設計書を改訂しない。取り込み時に直っていなければ `done.md` の「要裁定」に「設計書 §6・[A-17] は D-1 で上書き済み」と書いて指揮に整合を任せる（Task 6 Step 4） | 本レーンで設計書を直す: やらないこと（設計書は W0-merge の版を写すだけ）と所有がぶつかる。done.md に書くだけで直さない: 正本が 2 つ食い違ったまま残る | agent 既定（推奨: W0-merge で直す。ゴールからは外れない文書の整合） |
| D-12 | 要裁定（指揮）: 並べ比べの成果物は D-3 にそろえ、設計書 §3.3 の表・§3.4 の撮影列・§5 の部品レーンの完了条件と、実行計画 E-2・最終タスク対の「比較 HTML」「`moorestech_logs/uitk/compare/`」を W0-merge で「報告ページに Web と UITK の画像の組・差の表は done.md」に直す。取り込み時に直っていなければ `done.md` の「要裁定」に食い違いを並べて書く（Task 6 Step 4） | 設計書どおり比較 HTML を `moorestech_logs` に置き、画像枚数の制限を例外にする: AGENTS.md と報告ページの規約に反する | agent 既定（推奨: D-3 にそろえる） |

## 本PR外のリファクタ提案

- `.agents/skills/ref-webui-design/SKILL.md` §2 の「パネル背景はすべて shared/ui/GamePanel」と ref-uitk-design §1 の「面を GamePanel に統一しない」が逆向きに読める。§2 の冒頭に「UITK の画面は ref-uitk-design §1 に従う」と 1 行足すか、X-5e（ref-webui-design の撤去）まで据え置くかを指揮が波の境で決める（据え置くと 2 つのスキルが逆のことを書いた状態が切替まで残る）
