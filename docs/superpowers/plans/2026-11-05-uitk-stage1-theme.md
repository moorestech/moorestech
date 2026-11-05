# UITK 段階1 theme レーン Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** UITK の見た目の基盤（トークン USS の仕上げ・暗色テーマ・SDF フォントとフォールバック・トークンの C# 読み出し・GamePanel・UiMotion・LocalizedLabel と数値書式・AnchorIds と TutorialAnchor の登録簿）を段階1の統合ブランチの上に作る。

**Architecture:** 正本は `docs/uitk-migration/plan.md` §2.2・§3.4〜3.6・§3.9・§3.10・§4.5 と §7.2。テーマは 1 枚の TSS（`GameUiTheme.tss`）に既定ランタイムテーマ・トークン USS・`Theme.uss`・`Typography.uss` を束ね、PanelSettings と `UiTestHost` の両方がそれを当てる。描画部品は `Client.Ui/Foundation/*`・`Client.Ui/Components/*` に置き、アンカーの語彙 `AnchorIds` は Client.Game（`TutorialAnchorIdMapper` の置き場）に置いて Web の `anchorIds.ts` と L0 で突き合わせる。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（USS・TSS・TextCore FontAsset・Painter2D・generateVisualContent）、UniRx、VContainer、NUnit＋UI Test Framework（`UITestFixture`）、Node（`tools/uitk` の変換スクリプト）

## Requirements

- R1 トークン: Web の `tokens.css`・`skitTokens.css`・`slotTokens.css` の全トークンが、USS へ移るか、行き先と理由付きで除外表に載る。受け入れ: `WebTokenParityTest` が緑、除外表の各行の理由が「層 enum」「実行時の寸法」「UiMotion」「フォントアセット」のいずれか
- R2 半透明（S7 の結論）: α が 0 と 1 の間の色トークンは、`1-(1-α)^2.2`（暗い色）か `α^2.2`（明るい色）で補正した α を USS に書き、Web の値をコメントに残す。`docs/uitk/translucent-tokens.md` に「トークン・Web の値・UITK の値・決め方」の表を生成する。受け入れ: 表の行数が USS の半透明トークン数と一致（L2）
- R3 文字の下限（S7 の結論）: 文字寸法のトークンは 13px 未満を 13px へ上げる。例外はアイコン上の個数（`--fluid-amount-font-size`・`--inventory-count-base-font-size`）。受け入れ: L2 で `Client.Ui` 配下の USS に 13px 未満の `font-size` と文字寸法トークンが無い（例外 2 つを除く）
- R4 テーマ: `Theme.uss` を PoC の `PocControls.uss` から作り（`.poc-root` を外す・色は `var()` だけ）、`GameUiTheme.tss` に束ね、`GameUiPanelSettings.asset` の themeStyleSheet を差し替える。受け入れ: L1 で Button と TextField の背景色がトークンの値に解決される
- R5 フォント（S10 の結論）: NotoSans-Regular・NotoSansJP-Medium・NotoSansKR-Medium の TextCore SDF FontAsset を Editor で作ってコミットする。フォールバックは Latin→JP→KR、CJK のロケール（japanese・korean・chinese-simplified・chinese-traditional）では JP→KR。受け入れ: L1 で Label の解決後フォントが既定は Latin、ルートに CJK クラスを付けると JP。日本語・韓国語・英数の見本の字形がどれかのアセットで解決される
- R6 トークンの C# 読み出し（骨組みの持ち越し）: 部品 USS で `--x: var(--token)` と受け直し、C# は `CustomStyleResolvedEvent` で読む。長さ（px）と時間（ms・s）は文字列で読んで解釈する。受け入れ: L1 で px・ms・色を読める
- R7 GamePanel（default のみ）: Web の `GamePanel`（default variant）の構造（タイトル行・上下の罫線・本文・下部の三角 3 つ）と面（半透明ネイビー・上端のフェード・左右と下端のフェード）を UITK で描く。面は `generateVisualContent` の頂点カラー、三角は Painter2D。受け入れ: L1 で構造と、トークン由来の寸法が読めていること。720p と 21:9（2560×1080）の撮影が 1 回ある
- R8 UiMotion: 時計の注入・頭からの再生し直し・予約の取り消し・完了の通知（UniRx）・パネルから外れたら停止。tween と慣性（減衰）の 2 種を同じ走行器で動かす。時間源はパネルのスケジューラ（実時間）。受け入れ: L0（時計を手で進める）と L1（`simulate.FrameUpdate(ms)`）が緑
- R9 ローカライズ: `LocalizedLabel`（キーと引数を持ち、言語変更で解決し直す）、`UiNumberFormat`（InvariantCulture を明示）、UXML の `text=` の生文字の禁止（L2）。受け入れ: L1 で言語変更後に文言が変わる。de-DE の現在カルチャでも小数点が「.」
- R10 アンカー: `AnchorIds`（静的 19 個・動的 prefix 6 種・生成関数）を C# の唯一の正本にし、`TutorialAnchorIdMapper` を吸収する。`TutorialAnchor.Mark(VisualElement, params string[])` と、パネルごとの登録簿（Attach で登録・Detach で同期解除・解決は missing／unique／duplicate を返し missing と duplicate は開発者ログへ）。受け入れ: L0 で `anchorIds.ts` と一致、L1 で登録と解除と解決
- R11 ゲーム起動の回帰: テーマ差し替えとロケールのクラス付けを入れた状態で、骨組みの `GameUiSkeletonTest`（EditModeInPlayingTest）が緑のまま

**やらないこと**: 入力・ESC・フォーカス・DSL・L2 の色や z-index の規約テスト（input レーン）。ポーズ等の画面（screens レーン）。GamePanel の craft・skit・hud variant（段階 2・4）。アンカーの枠の描画・非表示と画面外の判定・祖先のクリップ（段階 2）。`ref-uitk-design` スキル。moores-code-review と pr-create（このレーンでは行わない。指揮セッションが統合後に行う）。

## Global Constraints

- 作業 worktree: theme レーンの worktree（`moores-wt` で作成）、ブランチ `feature/uitk-s1-theme`。統合ブランチ `feature/uitk-stage1` へ push しない。Unity Editor はこの worktree の 1 本だけ使う
- AGENTS.md: 1 ファイル 200 行以下・1 ディレクトリ 10 コードファイルまで・partial 禁止・`Func<>` 禁止・try-catch 原則禁止・イベントは UniRx・日英 2 行コメント・`#region Internal` はメソッド内ローカル関数だけ・単純な getter/setter プロパティ禁止（`{ get; private set; }` は可）・デフォルト引数禁止・`[SerializeField]` は小文字キャメル・fail-closed の経路はログを出す
- .cs を変えたら `uloop compile --project-path ./moorestech_client`（ErrorCount 0）。テストは `--filter-type regex` か `class` で絞る。`Client.Tests.Playtest` 系を選ぶ広い正規表現は禁止
- Prefab・PanelSettings・TSS 以外の Unity YAML（.asset）は Write/Edit で触らない。PanelSettings の差し替えと FontAsset の作成は `uloop execute-dynamic-code`。`.meta` は手で作らない
- USS・TSS・UXML はテキストで書いてよい（Unity がインポートする）。USS の色は `Tokens*.uss`・`ControlTokens.uss` の外では `var()` だけを使う
- 共有ファイル（`UiLayer`・所有表・`MainGameUiRegistration`・`Tokens*.uss`・`AnchorIds`）は拡張点の範囲で足すだけ。`GameUiRoot` へはメソッド 1 つ（Task 3）だけ足す
- 各タスクの終わりにコミットし、`git push origin feature/uitk-s1-theme` する

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| パス | 責務 | タスク |
|---|---|---|
| `tools/uitk/webTokens/convertTokenValue.mjs`（変更）・`tools/uitk/webTokens/translucency.mjs`（新規）・`tools/uitk/webTokens/fontSizeFloor.mjs`（新規）・`tools/uitk/convert-web-tokens.mjs`（変更） | 変換の追加規則（α 補正・文字の下限・em と複合値・分割） | 1 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/{Tokens,SkitTokens,SlotTokens}.uss`・`TokenConversionExclusions.txt`（再生成） | 生成物 | 1 |
| `docs/uitk/translucent-tokens.md`（生成） | 半透明の 3 列表 | 1 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/WebTokenParityTest.cs`（変更）・`TokenFloorAndTranslucencyTest.cs`（新規） | L2 | 1 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/ControlTokens.uss`・`Theme.uss`（新規） | ランタイム既定コントロールの暗色化 | 2 |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（新規）・`GameUiPanelSettings.asset`（uloop で変更） | テーマの束 | 2 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/GameUiThemeTest.cs`（新規） | L1 | 2 |
| `moorestech_client/Assets/UI Toolkit/GameUi/Fonts/GameUi{Latin,Japanese,Korean} SDF.asset`（uloop で作成）・`tools/uitk/create-game-ui-fonts.dynamic.cs`（新規。uloop へ渡す手順の正本） | フォント | 3 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Typography.uss`（新規） | 文字の既定とロケールのクラス | 3 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/GameUiLocaleClass.cs`（新規）・`GameUiLocaleClassBinder.cs`（新規） | ロケール → ルートのクラス | 3 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Root/GameUiRoot.cs`（`SetRootClassEnabled` を足す）・`moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（1 行） | 配線 | 3 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/GameUiFontTest.cs`（新規） | L1 | 3 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/UssTokenValue.cs`・`UssCustomStyleReader.cs`（新規） | トークンの C# 読み出し | 4 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/UssCustomStyleReaderTest.cs`・`UssTokenValueTest.cs`（新規）・`UssCustomStyleProbe.uss`（新規・テスト用） | L0・L1 | 4 |
| `moorestech_client/Assets/Scripts/Client.Ui/Components/GamePanel/GamePanel.cs`・`GamePanelFace.cs`・`GamePanelBottomDeco.cs`・`GamePanel.uss`（新規） | GamePanel（default） | 5 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/GamePanelTest.cs`（新規） | L1 | 5 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/{IUiMotionClock,IUiAnimation,UiEasing,UiMotionSpec,UiTweenAnimation,UiInertiaAnimation,UiMotionTimeline,UiMotionPlayer,UiMotionHandle,UiMotionSteps}.cs`（新規） | UiMotion | 6 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/{UiMotionTimelineTest,UiMotionPlayerTest,UiInertiaAnimationTest}.cs`（新規） | L0・L1 | 6 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/LocalizedLabel.cs`・`UiNumberFormat.cs`（新規） | ローカライズ | 7 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/{LocalizedLabelTest,UiNumberFormatTest,UxmlLiteralTextTest}.cs`（新規） | L0・L1・L2 | 7 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/AnchorIds.cs`（新規）・`TutorialAnchorIdMapper.cs`（削除）・`ItemViewHighLightTutorialManager.cs`（変更） | アンカーの語彙 | 8 |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Anchor/{TutorialAnchor,TutorialAnchorRegistry,TutorialAnchorResolution}.cs`（新規）・`moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef`（`Client.Game`・`Client.Localization` を参照に足す） | Mark と登録簿 | 7・8 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/{AnchorIdsWebParityTest,TutorialAnchorRegistryTest}.cs`（新規）・`Client.Tests/WebUi/TutorialAnchorContractTest.cs`・`Client.Tests/UnitTest/Tutorial/ItemViewHighLightTutorialManagerTest.cs`（変更） | L0・L1 | 8 |

配置と前例:
- 描画の基盤は `Client.Ui/Foundation/<Theme|Text|Motion|Anchor>`、部品は `Client.Ui/Components/<部品>`（plan.md §3.1 のディレクトリ規約）
- DI のエントリポイントは骨組みの `WebUiSuspension`（`IInitializable, IDisposable`、`MainGameUiRegistration` に `RegisterEntryPoint` 1 行）に合わせる（Task 3 の `GameUiLocaleClassBinder`）
- L1 は骨組みの `UiTestHost` を継承する（`Client.Tests/Ui/Layers/UiLayerStackTest.cs` が前例）。L2 のテキスト検査は `WebTokenParityTest` の形（リポジトリ直下からの相対パスで読む）
- `AnchorIds` は `TutorialAnchorIdMapper` と同じ Client.Game の `InGame/Tutorial/UIHighlight/` に置く。`ItemViewHighLightTutorialManager`（Client.Game）が使うため Client.Ui.State（Client.Game を参照する側）には置けない
- 言語変更の購読は `Localize.OnLanguageChanged`（`Client.Localization/Localize.cs:22`、UniRx）

---

### Task 1: トークン USS の仕上げ（半透明の補正・文字の下限・残りの除外の解消）

**Files:**
- Create: `tools/uitk/webTokens/translucency.mjs`
- Create: `tools/uitk/webTokens/fontSizeFloor.mjs`
- Modify: `tools/uitk/webTokens/convertTokenValue.mjs`
- Modify: `tools/uitk/convert-web-tokens.mjs`
- Regenerate: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss`・`SkitTokens.uss`・`SlotTokens.uss`・`TokenConversionExclusions.txt`
- Create（生成）: `docs/uitk/translucent-tokens.md`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/WebTokenParityTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/TokenFloorAndTranslucencyTest.cs`

**Interfaces:**
- Consumes: 骨組みの `tools/uitk/convert-web-tokens.mjs`（`createTokenConverter(rawByName).convert(name)` が `{ value, comment }` か `{ excluded: true, reason }` を返す）
- Produces: USS の変数名は Web と同じ `--x`。分割で生まれた派生名は `--<元の名前>-<部位>`（例 `--tooltip-border-width`・`--tooltip-border-color`・`--recipe-action-background-start`・`--recipe-action-background-middle`・`--recipe-action-background-end`）。除外表の行は `--name<TAB>理由`、分割した元の名前は `--name<TAB>分割: --a, --b` の形で除外表に残す。`docs/uitk/translucent-tokens.md` の表の列は `| トークン | Web の値 | UITK の値 | 決め方 |`

- [ ] **Step 1: 半透明の補正を書く**（`tools/uitk/webTokens/translucency.mjs`）

```js
// Linear 色空間の UITK は sRGB で混ぜる Chrome より半透明が薄く出るため、α を近似補正する（S7。docs/uitk/TODO.md）
// UITK in Linear color space blends lighter than Chrome's sRGB blend, so approximate-correct the alpha (S7, docs/uitk/TODO.md)
const gamma = 2.2;

// 暗い色は 1-(1-α)^2.2、明るい色は α^2.2。暗い背景の上で Chrome の見え方に寄る初期値（部品の段で実機で決める）
// Dark colors use 1-(1-α)^2.2 and light colors α^2.2; a starting value close to Chrome on dark backdrops (finalized per component on device)
export function correctAlphaForLinearBlend(red, green, blue, alpha) {
  if (alpha <= 0 || alpha >= 1) return alpha;
  const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255;
  const corrected = luminance < 0.5 ? 1 - Math.pow(1 - alpha, gamma) : Math.pow(alpha, gamma);
  return Math.round(corrected * 10000) / 10000;
}

export const translucencyMethod = "α 近似補正（S7）。部品の段で実機で決める";
```

- [ ] **Step 2: 文字の下限を書く**（`tools/uitk/webTokens/fontSizeFloor.mjs`）

```js
// 720p で 12px 以下の和文が潰れるため、文字寸法トークンは 13px 未満を 13px へ上げる（S7・plan.md §3.10）
// Japanese at 12px or less crushes at 720p, so text-size tokens under 13px are raised to 13px (S7, plan.md §3.10)
export const minimumFontSizePx = 13;

// アイコン上の個数は縁取りで読ませるので下限の外に置く（plan.md §3.10）
// Counts drawn over icons rely on an outline, so they stay outside the floor (plan.md §3.10)
const exemptNames = new Set(["--fluid-amount-font-size", "--inventory-count-base-font-size"]);

export function isFontSizeToken(name) {
  return name.endsWith("-font-size") || name.endsWith("-text-size");
}

export function applyFontSizeFloor(name, pxValue) {
  if (!isFontSizeToken(name) || exemptNames.has(name) || pxValue >= minimumFontSizePx) return { px: pxValue, raised: false };
  return { px: minimumFontSizePx, raised: true };
}
```

- [ ] **Step 3: 変換器に規則を足す**（`tools/uitk/webTokens/convertTokenValue.mjs`）

既存の `convert(name)` の値の確定直後に、次の順で後処理を足す。各規則は 1 関数にし、`convert` から呼ぶ。
1. 確定値が `rgba(r, g, b, a)` で 0<a<1 なら `correctAlphaForLinearBlend` で a を差し替え、`comment` に `web: rgba(r, g, b, a) α補正` を足す（既存の `calc:` コメントがあれば `; ` でつなぐ）。変換器は半透明の行を `translucentRows`（`{ name, webValue, ussValue }` の配列）に積み、`createTokenConverter` の戻り値から読めるようにする
2. 確定値が `Npx` で `isFontSizeToken(name)` なら `applyFontSizeFloor`。上げたら `comment` に `web: Npx 下限13px` を足す
3. `--key-hint-letter-spacing`（`0.055em`）: em は同じ部品の文字寸法トークンに掛けて px にする。対応は表で持つ: `{ "--key-hint-letter-spacing": "--key-hint-font-size" }`。値は `0.055 * 25 = 1.375px`、`comment` に `em: 0.055em × var(--key-hint-font-size)`
4. 複合値: `--key-hint-text-shadow`・`--challenge-hud-text-shadow`（`0.35px 0.35px 0 rgb(0 0 0 / 80%)`）は色部分を `rgba()` に直した複合値のまま出す（USS の `text-shadow` は「x y blur color」を受ける）。`--label-face-padding`（`4px 10px`）はそのまま出す
5. 分割: `--tooltip-border`（`1px solid rgb(255 255 255 / 22%)`）→ `--tooltip-border-width: 1px` と `--tooltip-border-color: rgba(255, 255, 255, <補正α>)`。`--recipe-action-background`（`linear-gradient(90deg, #0072d8 0%, #008fed 50%, #04d8ec 100%)`）→ `--recipe-action-background-start: #0072d8`・`--recipe-action-background-middle: #008fed`・`--recipe-action-background-end: #04d8ec`。`--skit-tool-icon-shadow`（`0 1px 1px var(--skit-window-face)`）は使い先（`moorestech_web/webui/src` を grep）が `text-shadow` なら 4 と同じ扱い、`box-shadow` なら USS に無いので除外のまま理由を「box-shadow は USS に無い。部品の段でメッシュで描く」にする。分割した元の名前は除外表へ `分割: --a, --b` で書く
6. 除外理由の書き直し: `--font-ui` は「フォントアセット: Typography.uss の -unity-font-definition（GameUi{Latin,Japanese,Korean} SDF）」、`--tutorial-pulse-*` は「UiMotion: @keyframes 名は UiMotion の尺トークンで置き換える」、`--z-*` は「層 enum: UiLayer」、ビューポート依存は「実行時の寸法: USS の配置か C# の幾何」

- [ ] **Step 4: 生成スクリプトに表の出力を足す**（`tools/uitk/convert-web-tokens.mjs`）

USS を書いた後に `docs/uitk/translucent-tokens.md` を書く。先頭 2 行は生成元と再生成コマンドのコメント（`<!-- Generated by node tools/uitk/convert-web-tokens.mjs — do not edit by hand. -->`）、見出し `# 半透明トークン（Web の値と UITK の値）`、1 段落「UITK は Linear 色空間で混ぜるため Web の α をそのまま使わない（plan.md §3.5・S7）。UITK の値は補正式の初期値で、部品を作る段で実機で決め直し、その時は決め方の列を書き換える」、続けて表 `| トークン | Web の値 | UITK の値 | 決め方 |` に `translucentRows` を 1 行ずつ（決め方は `translucencyMethod`）。

- [ ] **Step 5: 再生成する**

Run: `node tools/uitk/convert-web-tokens.mjs`
Expected: `wrote …/Tokens.uss (N lines)` など 4 行と `docs/uitk/translucent-tokens.md` の 1 行。どの USS も 200 行以下（超えたらスクリプトが止まる）

- [ ] **Step 6: パリティテストを派生名に対応させる**（`WebTokenParityTest.cs`）

`ReadExcludedNames` の隣に `ReadDerivedNames` を足す: 除外表の理由が `分割: ` で始まる行の右辺（`, ` 区切り）を派生名の集合として返す。`CssTokenNamesEqualUssTokenNamesPlusExclusions` では `ussNames` から派生名を除いてから比べ、さらに「派生名はすべて USS にある」を Assert する。`UssTokensContainNoUnconvertedCssSyntax` の禁止語に `"em;"`・`"linear-gradient"`・`"solid"` を足す。

- [ ] **Step 7: 下限と半透明の L2 を書く**（`TokenFloorAndTranslucencyTest.cs`）

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 文字寸法の下限（13px）と半透明トークンの表が USS と食い違わないことを確かめる（L2・S7）
    /// Verifies the 13px text-size floor and that the translucent-token table matches the USS (L2, S7)
    /// </summary>
    public class TokenFloorAndTranslucencyTest
    {
        private const string UiScriptDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private const string TranslucentTablePath = "docs/uitk/translucent-tokens.md";
        private const float MinimumFontSizePx = 13f;
        private static readonly HashSet<string> ExemptTokenNames = new() { "--fluid-amount-font-size", "--inventory-count-base-font-size" };
        private static readonly Regex FontSizeTokenPattern = new(@"(--[\w-]+-(?:font|text)-size)\s*:\s*([\d.]+)px");
        private static readonly Regex FontSizePropertyPattern = new(@"(?<![\w-])font-size\s*:\s*([\d.]+)px");
        private static readonly Regex TranslucentRgbaPattern = new(@"(--[\w-]+)\s*:\s*rgba\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*(0?\.\d+)\s*\)");

        [Test]
        public void NoUssTextSizeBelowThirteenPixels()
        {
            foreach (var path in UssFiles())
            {
                var text = File.ReadAllText(path);
                foreach (Match match in FontSizeTokenPattern.Matches(text))
                {
                    if (ExemptTokenNames.Contains(match.Groups[1].Value)) continue;
                    Assert.GreaterOrEqual(ParsePx(match.Groups[2].Value), MinimumFontSizePx, $"{Path.GetFileName(path)}: {match.Value}");
                }
                foreach (Match match in FontSizePropertyPattern.Matches(text))
                {
                    Assert.GreaterOrEqual(ParsePx(match.Groups[1].Value), MinimumFontSizePx, $"{Path.GetFileName(path)}: {match.Value}");
                }
            }
        }

        [Test]
        public void TranslucentTableListsEveryTranslucentToken()
        {
            // USS の半透明トークンと表の行が名前で一致する
            // The USS translucent tokens and the table rows match by name
            var ussNames = UssFiles().SelectMany(path => TranslucentRgbaPattern.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value)).ToHashSet();
            var tableText = File.ReadAllText(Path.Combine(RepositoryRoot(), TranslucentTablePath));
            var tableNames = Regex.Matches(tableText, @"^\| `(--[\w-]+)` \|", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToHashSet();
            CollectionAssert.AreEquivalent(ussNames, tableNames);
        }

        private static IEnumerable<string> UssFiles()
        {
            return Directory.GetFiles(Path.Combine(RepositoryRoot(), UiScriptDirectory), "*.uss", SearchOption.AllDirectories);
        }

        private static float ParsePx(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
```

表の行の書式は `| \`--name\` | web | uss | 決め方 |` にする（Step 4 の出力をこれに合わせる）。

- [ ] **Step 8: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Theme\."`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 9: コミットして push する**

```bash
git add tools/uitk moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme docs/uitk/translucent-tokens.md moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme
git commit -m "feat(uitk): トークン変換に半透明の α 補正・文字の下限・複合値の分割を足し全トークンの行き先を確定する"
git push origin feature/uitk-s1-theme
```

### Task 2: 暗色テーマ（Theme.uss・ControlTokens.uss・GameUiTheme.tss）と PanelSettings の差し替え

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/ControlTokens.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Theme.uss`
- Create: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`
- Modify（uloop）: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset`（themeStyleSheet）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/GameUiThemeTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/TestHost/UiTestHost.cs`（コメントの拡張点だけ。テーマは PanelSettings から読むので処理は変えない）

**Interfaces:**
- Consumes: Task 1 の `Tokens*.uss`
- Produces: `GameUiTheme.tss`（パス `Assets/UI Toolkit/GameUi/GameUiTheme.tss`）。中身の順は `@import url("unity-theme://default");` → `Tokens.uss`・`SkitTokens.uss`・`SlotTokens.uss`・`ControlTokens.uss` → `Theme.uss`。Task 3 がこの末尾に `Typography.uss` を 1 行足す。`--control-*` のトークン名（下の Step 1 の 13 個）

- [ ] **Step 1: ControlTokens.uss を書く**（手書き。Web に対応トークンが無い、ランタイム既定コントロール用の色。値は PoC `PocControls.uss`）

```css
/* ランタイム既定コントロール（Button・TextField・Slider・Toggle・Scroller・Dropdown）の暗色。Web の Mantine テーマ相当で Web に対応トークンが無い */
/* Dark colors for runtime default controls (Button, TextField, Slider, Toggle, Scroller, Dropdown); Mantine-theme equivalents with no Web token */
:root {
    --control-text: #e6e8eb;
    --control-label: #9aa3ad;
    --control-border: #4a515c;
    --control-border-hover: #6b7380;
    --control-accent: #f2a640;
    --control-face: #353b45;
    --control-face-hover: #424955;
    --control-face-active: #2b3038;
    --control-input-face: #1f2329;
    --control-dragger-rim: #1b1d22;
    --control-scroller-track: rgba(255, 255, 255, 0.0008);
    --control-popup-face: #262a31;
    --control-popup-item-hover: #3a414c;
}
```

`--control-scroller-track` は PoC の `rgba(255, 255, 255, 0.04)` に Task 1 の明色の補正（`0.04^2.2`）を当てた値。

- [ ] **Step 2: Theme.uss を書く**

`git show origin/poc/uitk-hard-cases:moorestech_client/Assets/Scripts/Client.UitkPoc/Resources/UitkPoc/PocControls.uss` を写し、(1) 全セレクタの `.poc-root ` 接頭辞を外す、(2) 16 進と rgba の色をすべて Step 1 の `var(--control-*)` に置き換える（対応は値の一致で機械的に決まる。`rgba(0, 0, 0, 0)` は `transparent` の代わりに `rgba(0, 0, 0, 0)` のまま残してよい＝完全透明は混ぜ方に依らない）、(3) 先頭のコメントを「既定ランタイムテーマの部品を暗色へ塗り替える。スライダーの入力部の高さが 0 になる癖もここで直す（plan.md §3.5）」の日英 2 行にする、(4) `transition-duration: 0.12s` はそのまま。200 行以下。

- [ ] **Step 3: GameUiTheme.tss を書く**

```css
@import url("unity-theme://default");
@import url("project://database/Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Foundation/Theme/SkitTokens.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Foundation/Theme/SlotTokens.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Foundation/Theme/ControlTokens.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Foundation/Theme/Theme.uss");
```

`unity-theme://default` で既定ランタイムテーマが解決しない場合（インポート時の警告・L1 で Button の既定スタイルが無い）は、現行の `GameUiPanelSettings.asset` の themeStyleSheet が指す `UnityDefaultRuntimeTheme.tss` のパスを uloop で読み（`AssetDatabase.GetAssetPath(panelSettings.themeStyleSheet)`）、`@import url("project://database/<そのパス>")` に置き換える。

- [ ] **Step 4: PanelSettings の themeStyleSheet を差し替える**（uloop）

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset");
var tss = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>("Assets/UI Toolkit/GameUi/GameUiTheme.tss");
if (tss == null) return "tss missing";
ps.themeStyleSheet = tss;
UnityEditor.EditorUtility.SetDirty(ps);
UnityEditor.AssetDatabase.SaveAssets();
return UnityEditor.AssetDatabase.GetAssetPath(ps.themeStyleSheet);'
```

Expected: `Assets/UI Toolkit/GameUi/GameUiTheme.tss`。`git diff` で `.asset` の差分が themeStyleSheet の参照 1 行だけであることを見る（他の差分が出たら `git checkout` で戻してやり直す）。

- [ ] **Step 5: L1 を書く**（`GameUiThemeTest.cs`）

```csharp
using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 本番テーマで既定コントロールが暗色トークンに解決されることを確かめる（L1）
    /// Verifies default controls resolve to the dark control tokens under the production theme (L1)
    /// </summary>
    public class GameUiThemeTest : UiTestHost
    {
        [Test]
        public void ButtonAndTextFieldUseControlTokens()
        {
            var button = new Button { text = "x" };
            var field = new TextField();
            rootVisualElement.Add(button);
            rootVisualElement.Add(field);
            simulate.FrameUpdate();

            // ControlTokens.uss の --control-face と --control-input-face
            // --control-face and --control-input-face from ControlTokens.uss
            AssertColor("#353b45", button.resolvedStyle.backgroundColor);
            var input = field.Q(className: "unity-base-text-field__input");
            AssertColor("#1f2329", input.resolvedStyle.backgroundColor);
        }

        [Test]
        public void SliderInputKeepsNonZeroHeight()
        {
            // 既定テーマの入力部の高さ 0 の癖を Theme.uss が直している
            // Theme.uss fixes the default theme's zero-height slider input
            var slider = new Slider(0, 1);
            rootVisualElement.Add(slider);
            simulate.FrameUpdate();
            Assert.Greater(slider.Q(className: "unity-base-slider__input").resolvedStyle.height, 0f);
        }

        private static void AssertColor(string expectedHex, Color actual)
        {
            ColorUtility.TryParseHtmlString(expectedHex, out var expected);
            Assert.AreEqual(expected.r, actual.r, 0.01f);
            Assert.AreEqual(expected.g, actual.g, 0.01f);
            Assert.AreEqual(expected.b, actual.b, 0.01f);
        }
    }
}
```

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\."`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 7: コミットして push する**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme "moorestech_client/Assets/UI Toolkit/GameUi" moorestech_client/Assets/Scripts/Client.Tests/Ui
git commit -m "feat(uitk): PoC の暗色コントロールを Theme.uss にして GameUiTheme.tss へ束ね PanelSettings に当てる"
git push origin feature/uitk-s1-theme
```

### Task 3: SDF フォントとフォールバック・Typography.uss・ロケールのクラス

**Files:**
- Create: `tools/uitk/create-game-ui-fonts.dynamic.cs`（uloop へ渡すコード。Assets 外なのでコンパイルされない）
- Create（uloop）: `moorestech_client/Assets/UI Toolkit/GameUi/Fonts/GameUiLatin SDF.asset`・`GameUiJapanese SDF.asset`・`GameUiKorean SDF.asset`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Typography.uss`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（末尾に Typography.uss の import を 1 行）
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/GameUiLocaleClass.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/GameUiLocaleClassBinder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Root/GameUiRoot.cs`（`SetRootClassEnabled` を足す）
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef`（references に `Client.Localization` を足す）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（1 行）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/GameUiFontTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/TestHost/UiTestHost.cs`（`<remarks>` の「フォントはフォントレーンが足す」を「フォントは GameUiTheme.tss の Typography.uss から当たる」に直す）

**Interfaces:**
- Consumes: Task 2 の `GameUiTheme.tss`
- Produces:
  - `public static class GameUiLocaleClass { public const string CjkClassName = "game-ui-locale--cjk"; public static bool IsCjk(string languageCode); }`（`japanese`・`korean`・`chinese-simplified`・`chinese-traditional` で true）
  - `public class GameUiLocaleClassBinder : IInitializable, IDisposable`（ctor `(GameUiRoot gameUiRoot)`）
  - `GameUiRoot.SetRootClassEnabled(string className, bool enabled)`
  - FontAsset 3 つのパスは上の Files 節のとおり

- [ ] **Step 1: フォント作成のコードを書く**（`tools/uitk/create-game-ui-fonts.dynamic.cs`）

```csharp
// GameUi の SDF FontAsset 3 つを作り直す。uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-fonts.dynamic.cs)" で実行する（S10）
// Recreates the three GameUi SDF FontAssets; run with uloop execute-dynamic-code --code "$(cat tools/uitk/create-game-ui-fonts.dynamic.cs)" (S10)
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
const string Folder = "Assets/UI Toolkit/GameUi/Fonts";
if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/UI Toolkit/GameUi", "Fonts");
FontAsset Create(string sourcePath, string assetName)
{
    var path = $"{Folder}/{assetName}.asset";
    AssetDatabase.DeleteAsset(path);
    var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
    var asset = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
    asset.name = assetName;
    asset.clearDynamicDataOnBuild = true;
    AssetDatabase.CreateAsset(asset, path);
    foreach (var texture in asset.atlasTextures) { texture.name = assetName + " Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
    asset.material.name = assetName + " Material";
    AssetDatabase.AddObjectToAsset(asset.material, asset);
    return asset;
}
var korean = Create("Assets/Dependencies/Font/NotoSansKR-Medium.otf", "GameUiKorean SDF");
var japanese = Create("Assets/Dependencies/Font/NotoSansJP-Medium.otf", "GameUiJapanese SDF");
var latin = Create("Assets/Dependencies/Font/NotoSans-Regular.ttf", "GameUiLatin SDF");
japanese.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { korean };
latin.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { japanese, korean };
foreach (var asset in new[] { korean, japanese, latin }) { asset.ClearFontAssetData(true); EditorUtility.SetDirty(asset); }
AssetDatabase.SaveAssets();
return $"latin={AssetDatabase.GetAssetPath(latin)} japanese={AssetDatabase.GetAssetPath(japanese)} korean={AssetDatabase.GetAssetPath(korean)}";
```

フォールバックは循環させない（Latin→JP→KR、KR は終端）。CJK のロケールは JP を先頭にし、JP の英数字形を使う（Web の `:root[data-locale]` が CJK で `MooresUI JP` を先に置くのと同じ）。`ClearFontAssetData(true)` の引数の意味が違う・存在しない場合は `ClearFontAssetData()` の公開シグネチャに合わせ、字形の無い状態でコミットする（Dynamic なので実行時に足される）。

- [ ] **Step 2: 実行してアセットを確かめる**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat tools/uitk/create-game-ui-fonts.dynamic.cs)"`
Expected: 3 つのパス。続けて `git status` で `Fonts/` に 3 つの `.asset` と Unity が作った `.meta` があること

- [ ] **Step 3: Typography.uss を書き、TSS に足す**

```css
/* 文字の既定。フォントは Latin→JP→KR のフォールバック、CJK のロケールは JP を先頭にする（plan.md §3.10）。合成の太字は使わない */
/* Text defaults; fonts fall back Latin→JP→KR and CJK locales lead with JP (plan.md §3.10); no synthetic bold */
:root {
    -unity-font-definition: url("project://database/Assets/UI%20Toolkit/GameUi/Fonts/GameUiLatin%20SDF.asset");
    font-size: 14px;
    color: var(--control-text);
}

.game-ui-locale--cjk {
    -unity-font-definition: url("project://database/Assets/UI%20Toolkit/GameUi/Fonts/GameUiJapanese%20SDF.asset");
}
```

`GameUiTheme.tss` の末尾に `@import url("project://database/Assets/Scripts/Client.Ui/Foundation/Theme/Typography.uss");` を足す。URL の空白は `%20`。インポートで解決しない場合は空白のまま引用符で囲った形を試し、通った方を使う。

- [ ] **Step 4: ロケールのクラスを書く**（`GameUiLocaleClass.cs`）

```csharp
namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 言語コードから、ルートに付けるロケールのクラスを決める
    /// Decides the locale class put on the root from a language code
    /// </summary>
    public static class GameUiLocaleClass
    {
        public const string CjkClassName = "game-ui-locale--cjk";

        // Web の :root[data-locale] と同じ 4 言語で JP フォントを先頭にする
        // Lead with the JP font for the same four languages as the Web's :root[data-locale]
        public static bool IsCjk(string languageCode)
        {
            return languageCode == "japanese" || languageCode == "korean" || languageCode == "chinese-simplified" || languageCode == "chinese-traditional";
        }
    }
}
```

- [ ] **Step 5: ルートのクラスの口を GameUiRoot に足す**（`GameUiRoot.cs` の `SetCefVisible` の下）

```csharp
        // ロケールなど、パネル全体に効くクラスをルートへ付け外しする
        // Toggles panel-wide classes such as the locale on the root
        public void SetRootClassEnabled(string className, bool enabled)
        {
            uiDocument.rootVisualElement.EnableInClassList(className, enabled);
        }
```

- [ ] **Step 6: 言語変更を購読するエントリポイントを書く**（`GameUiLocaleClassBinder.cs`。前例 `WebUiSuspension`）

```csharp
using System;
using Client.Localization;
using Client.Ui.Foundation.Root;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 言語変更に合わせてルートのロケールのクラスを付け替え、フォントの先頭を切り替える
    /// Re-applies the root locale class on language change to switch the leading font
    /// </summary>
    public class GameUiLocaleClassBinder : IInitializable, IDisposable
    {
        private readonly GameUiRoot _gameUiRoot;
        private IDisposable _subscription;

        public GameUiLocaleClassBinder(GameUiRoot gameUiRoot)
        {
            _gameUiRoot = gameUiRoot;
        }

        public void Initialize()
        {
            Apply();
            _subscription = Localize.OnLanguageChanged.Subscribe(_ => Apply());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void Apply()
        {
            _gameUiRoot.SetRootClassEnabled(GameUiLocaleClass.CjkClassName, GameUiLocaleClass.IsCjk(Localize.GetCurrentLanguageCode()));
        }
    }
}
```

`MainGameUiRegistration.Register` の `builder.RegisterEntryPoint<WebUiSuspension>();` の下に `builder.RegisterEntryPoint<GameUiLocaleClassBinder>();` を足し、`using Client.Ui.Foundation.Text;` を足す。`Client.Ui.asmdef` の references に `"Client.Localization"` を足す。

- [ ] **Step 7: L1 を書く**（`GameUiFontTest.cs`）

```csharp
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// 本番テーマのフォントとフォールバックが日本語・韓国語・英数を描けることを確かめる（L1・S10）
    /// Verifies the production theme's fonts and fallbacks cover Japanese, Korean and Latin (L1, S10)
    /// </summary>
    public class GameUiFontTest : UiTestHost
    {
        private const string FontFolder = "Assets/UI Toolkit/GameUi/Fonts";

        [Test]
        public void LabelUsesLatinByDefaultAndJapaneseUnderCjkClass()
        {
            var label = new Label("x");
            rootVisualElement.Add(label);
            simulate.FrameUpdate();
            Assert.AreEqual(Load("GameUiLatin SDF"), label.resolvedStyle.unityFontDefinition.fontAsset);

            rootVisualElement.AddToClassList(GameUiLocaleClass.CjkClassName);
            simulate.FrameUpdate();
            Assert.AreEqual(Load("GameUiJapanese SDF"), label.resolvedStyle.unityFontDefinition.fontAsset);
        }

        [TestCase("電力・鉄鉱石の粉・歯車ベルトコンベア")]
        [TestCase("설정 저장")]
        [TestCase("Settings 1,234.5/min")]
        public void FallbackChainCoversSample(string sample)
        {
            // Latin を先頭にしたフォールバックの連鎖のどこかで全字形が見つかる
            // Every glyph is found somewhere along the Latin-led fallback chain
            var latin = Load("GameUiLatin SDF");
            foreach (var character in sample)
            {
                if (char.IsWhiteSpace(character)) continue;
                Assert.IsTrue(HasInChain(latin, character), $"missing glyph '{character}' (U+{(int)character:X4})");
            }
        }

        private static bool HasInChain(FontAsset asset, char character)
        {
            if (asset.HasCharacter(character, false, true)) return true;
            foreach (var fallback in asset.fallbackFontAssetTable) if (HasInChain(fallback, character)) return true;
            return false;
        }

        private static FontAsset Load(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FontAsset>($"{FontFolder}/{name}.asset");
            Assert.IsNotNull(asset, $"{FontFolder}/{name}.asset");
            return asset;
        }
    }
}
```

`HasCharacter(char, bool searchFallbacks, bool tryAddCharacter)` の公開シグネチャが違えば、`TryAddCharacters(string, out string missing)` で 1 文字ずつ試す形にする。

- [ ] **Step 8: テスト後にフォントの動的データを空に戻す**

テストで字形が足されると `.asset` が汚れる。テスト後に `git status` を見て `Fonts/*.asset` に差分があれば Step 2 のコードを再実行せず、次で空に戻す:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FontAsset", new[]{"Assets/UI Toolkit/GameUi/Fonts"})) { var a = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)); a.ClearFontAssetData(true); UnityEditor.EditorUtility.SetDirty(a);} UnityEditor.AssetDatabase.SaveAssets(); return "cleared";'
```

（Step 1 で使えた `ClearFontAssetData` のシグネチャに合わせる）

- [ ] **Step 9: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\."`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 10: コミットして push する**

```bash
git add tools/uitk/create-game-ui-fonts.dynamic.cs "moorestech_client/Assets/UI Toolkit/GameUi" moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/Ui
git commit -m "feat(uitk): GameUi の SDF フォント 3 つとフォールバックを Editor で作り Typography.uss とロケールのクラスで当てる"
git push origin feature/uitk-s1-theme
```

### Task 4: トークンの C# 読み出し（UssTokenValue・UssCustomStyleReader）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/UssTokenValue.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/UssCustomStyleReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/UssTokenValueTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/UssCustomStyleReaderTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/UssCustomStyleProbe.uss`

**Interfaces:**
- Produces:
  - `public static class UssTokenValue { public static bool TryParsePixels(string text, out float pixels); public static bool TryParseMilliseconds(string text, out float milliseconds); }`（`"12px"`→12、`"160ms"`→160、`"0.2s"`→200、単位なしの数は px・ms とも受ける。解釈は InvariantCulture）
  - `public static class UssCustomStyleReader { public static float ReadPixels(ICustomStyle style, string propertyName); public static float ReadMilliseconds(ICustomStyle style, string propertyName); public static Color ReadColor(ICustomStyle style, string propertyName); }` 値が無い・解釈できないときは `Debug.LogError($"[UssCustomStyleReader] {propertyName} …")` を出して `float.NaN`（色は `Color.magenta`）を返す（部品が未解決のトークンを描いても目に見えて、ログで気づける）
  - 規約（クラスの `<remarks>` に書く）: `:root` のトークンは当てた要素でしか customStyle に出ず、子へ継承されない。部品は自分のクラスのルールで `--<部品>-<名前>: var(--token)` と受け直し、`CustomStyleResolvedEvent` で読む

- [ ] **Step 1: L0 を書く**（`UssTokenValueTest.cs`）

```csharp
using Client.Ui.Foundation.Theme;
using NUnit.Framework;

namespace Client.Tests.Ui.Theme
{
    public class UssTokenValueTest
    {
        [TestCase("12px", 12f)]
        [TestCase("11.742px", 11.742f)]
        [TestCase("8", 8f)]
        public void ParsesPixels(string text, float expected)
        {
            Assert.IsTrue(UssTokenValue.TryParsePixels(text, out var pixels));
            Assert.AreEqual(expected, pixels, 0.0001f);
        }

        [TestCase("160ms", 160f)]
        [TestCase("0.2s", 200f)]
        [TestCase("1200", 1200f)]
        public void ParsesMilliseconds(string text, float expected)
        {
            Assert.IsTrue(UssTokenValue.TryParseMilliseconds(text, out var milliseconds));
            Assert.AreEqual(expected, milliseconds, 0.0001f);
        }

        [TestCase("12%")]
        [TestCase("auto")]
        [TestCase("")]
        public void RejectsNonLengths(string text)
        {
            Assert.IsFalse(UssTokenValue.TryParsePixels(text, out _));
        }
    }
}
```

- [ ] **Step 2: 実装を書く**（`UssTokenValue.cs`）

```csharp
using System.Globalization;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// customStyle から文字列で読んだ USS の長さと時間を数に直す
    /// Converts USS lengths and durations read as strings from customStyle into numbers
    /// </summary>
    public static class UssTokenValue
    {
        public static bool TryParsePixels(string text, out float pixels)
        {
            var body = text.EndsWith("px") ? text.Substring(0, text.Length - 2) : text;
            return TryParseNumber(body, out pixels);
        }

        public static bool TryParseMilliseconds(string text, out float milliseconds)
        {
            // ms を先に見ないと "160ms" を秒として読んでしまう
            // Check ms first or "160ms" would be read as seconds
            if (text.EndsWith("ms")) return TryParseNumber(text.Substring(0, text.Length - 2), out milliseconds);
            if (text.EndsWith("s") && TryParseNumber(text.Substring(0, text.Length - 1), out var seconds))
            {
                milliseconds = seconds * 1000f;
                return true;
            }
            return TryParseNumber(text, out milliseconds);
        }

        private static bool TryParseNumber(string text, out float value)
        {
            return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
```

- [ ] **Step 3: L1 のプローブ USS とテストを書く**

`UssCustomStyleProbe.uss`:

```css
.uss-custom-style-probe {
    --probe-edge-fade: var(--panel-edge-fade);
    --probe-enter: var(--notification-enter-duration);
    --probe-face: var(--surface-navy);
}
```

`UssCustomStyleReaderTest.cs`（`UiTestHost` 継承）: プローブ USS を `AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Client.Tests/Ui/Theme/UssCustomStyleProbe.uss")` で読み `rootVisualElement.styleSheets.Add`、クラス `uss-custom-style-probe` の要素を足して `simulate.FrameUpdate()`、`ReadPixels(element.customStyle, "--probe-edge-fade")` が `Tokens.uss` の `--panel-edge-fade`（12）と一致、`ReadMilliseconds(…, "--probe-enter")` が 160、`ReadColor(…, "--probe-face")` の rgb が `--surface-navy` の 10/14/27（/255）で α が Task 1 の補正値（`Tokens.uss` から読む）と一致することを確かめる。期待値は `Tokens.uss` の本文を正規表現で読んで比べる（数を二重に書かない）。もう 1 件、`:root` のトークンを子要素の customStyle で直接読むと取れないこと（`TryGetValue(new CustomStyleProperty<string>("--panel-edge-fade"), out _)` が false）を固定し、規約の根拠を残す。

- [ ] **Step 4: 実装を書く**（`UssCustomStyleReader.cs`）

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// 部品の USS が var() で受け直したトークンを、解決後の値で C# へ読む
    /// Reads tokens that a component's USS re-declared through var() as resolved values in C#
    /// </summary>
    /// <remarks>
    /// :root のトークンは当てた要素の customStyle にしか出ず子へ継承されない。部品は自クラスのルールで --部品-名前: var(--token) と受け直し CustomStyleResolvedEvent で読む。px・ms 付きの値は float では読めないので文字列で読む（docs/uitk/TODO.md の customStyle）
    /// :root tokens appear only in the customStyle of the element they apply to and are not inherited; components re-declare --component-name: var(--token) in their own class rule and read it in CustomStyleResolvedEvent; values with px or ms cannot be read as float, so they are read as strings (customStyle in docs/uitk/TODO.md)
    /// </remarks>
    public static class UssCustomStyleReader
    {
        public static float ReadPixels(ICustomStyle style, string propertyName)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParsePixels(text, out var pixels)) return pixels;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved length (got '{text}')");
            return float.NaN;
        }

        public static float ReadMilliseconds(ICustomStyle style, string propertyName)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParseMilliseconds(text, out var milliseconds)) return milliseconds;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved duration (got '{text}')");
            return float.NaN;
        }

        public static Color ReadColor(ICustomStyle style, string propertyName)
        {
            if (style.TryGetValue(new CustomStyleProperty<Color>(propertyName), out var color)) return color;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved color");
            return Color.magenta;
        }
    }
}
```

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Theme\.Uss"`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 6: コミットして push する**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme
git commit -m "feat(uitk): 部品 USS が受け直したトークンを C# で読む UssCustomStyleReader と px・ms の解釈を足す"
git push origin feature/uitk-s1-theme
```

### Task 5: GamePanel（default）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Components/GamePanel/GamePanel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Components/GamePanel/GamePanelFace.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Components/GamePanel/GamePanelBottomDeco.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Components/GamePanel/GamePanel.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/GamePanelTest.cs`

**Interfaces:**
- Consumes: Task 4 の `UssCustomStyleReader.ReadPixels/ReadColor`、Task 7 の `LocalizedLabel`（Task 7 が後なので、タイトルは `Label` を受け取る形にして依存を切る）
- Produces:
  - `public class GamePanel : VisualElement`。ctor `GamePanel(StyleSheet panelStyleSheet)`（USS は呼び出し側が渡す。`UiAssetCatalog` ができたらそこから渡す）。`public override VisualElement contentContainer` は本文の要素。`public void SetTitle(Label titleLabel)`（タイトル行と上下の罫線を出す。呼ばなければタイトル行は無い）。`public void SetTitleAction(VisualElement action)`（タイトル行の右端）
  - USS のクラス: `game-panel`・`game-panel__face`・`game-panel__header`・`game-panel__title`・`game-panel__title-action`・`game-panel__deco-line`・`game-panel__deco-line--top`・`game-panel__deco-line--bottom`・`game-panel__body`・`game-panel__bottom-deco`
  - 受け直すトークン: `--game-panel-face: var(--surface-navy)`・`--game-panel-edge-fade: var(--panel-edge-fade)`

- [ ] **Step 1: Web の default variant を読む**

`moorestech_web/webui/src/shared/ui/GamePanel/style.module.css` の `.panel`・`.panel[data-variant="default"]::before`・`.header`・`.title`・`.titleAction`・`.decoLine*`・`.body`・`.bottomDeco`（と `span`）と `index.tsx` を読み、寸法・色・余白を写す。色は直書きせず `Tokens.uss` の同値のトークンを使う。同値のトークンが無い色（例 `#e8eaf0`）は `GamePanel.uss` の `.game-panel` ルールで `--game-panel-<名前>` として宣言し値を 1 か所に置く（Tokens.uss は生成物なので触らない）。`::before` は `GamePanelFace`（子要素・`position: absolute`・Web の `top/right/bottom/left` のオフセットどおり・`picking-mode` は Ignore）にする。CSS の `calc(var(--panel-top, -8px) - 3.913894px)` は -11.913894px のリテラルにして導出式をコメントに残す。`box-shadow` は USS に無いので描かない（判断記録 D-6）。

- [ ] **Step 2: 面を書く**（`GamePanelFace.cs`）

`generateVisualContent` で頂点カラーのメッシュを描く。面は矩形を縦横に分割した格子（x: 0, fade, w-fade, w の 4 本、y: 0, topFade(11.742px), h-12px, h の 4 本）で、各頂点の色は `face`（`--game-panel-face`）に α の係数を掛けたもの。係数は「縦: 上端 0→topFade で 1、h-12→h で 1→0」と「横: 0→fade で 0→1、w-fade→w で 1→0」の積（Web の mask-composite: intersect と同じ）。`CustomStyleResolvedEvent` で `UssCustomStyleReader.ReadColor(customStyle, "--game-panel-face")` と `ReadPixels(customStyle, "--game-panel-edge-fade")` を読み `MarkDirtyRepaint()`。`--game-panel-*` は `.game-panel` のルールで宣言するので、`GamePanelFace` 自身にも `game-panel__face` のルールで同じ受け直しを書く（customStyle は継承されないため。Task 4 の規約）。格子は 3×3 のセル＝16 頂点・54 インデックス。`MeshGenerationContext.Allocate(16, 54)` → `SetNextVertex`・`SetNextIndex`。値が NaN の間は描かない。

- [ ] **Step 3: 下部の三角を書く**（`GamePanelBottomDeco.cs`）

Web の `.bottomDeco span` 3 個の寸法・間隔・色を写し、`generateVisualContent` の `ctx.painter2D` で下向きの三角を 3 つ塗る（`BeginPath`→`MoveTo`→`LineTo`×2→`ClosePath`→`Fill`）。色は `--game-panel-deco` のように受け直したトークンを読む。

- [ ] **Step 4: 本体を書く**（`GamePanel.cs`）

```csharp
using UnityEngine.UIElements;

namespace Client.Ui.Components.GamePanel
{
    /// <summary>
    /// ゲームの窓枠（default）。半透明ネイビーの面・タイトル行と罫線・本文・下部の三角を持つ（plan.md §3.4）
    /// Game window frame (default) with a translucent navy face, title row with rules, body and bottom triangles (plan.md §3.4)
    /// </summary>
    public class GamePanel : VisualElement
    {
        private readonly VisualElement _header;
        private readonly VisualElement _titleAction;
        private readonly VisualElement _body;

        public override VisualElement contentContainer => _body;

        public GamePanel(StyleSheet panelStyleSheet)
        {
            styleSheets.Add(panelStyleSheet);
            AddToClassList("game-panel");
            hierarchy.Add(new GamePanelFace());

            // タイトル行は SetTitle まで隠し、罫線もそれに合わせる
            // Keep the title row and its rules hidden until SetTitle
            _header = new VisualElement();
            _header.AddToClassList("game-panel__header");
            _header.style.display = DisplayStyle.None;
            _titleAction = new VisualElement();
            _titleAction.AddToClassList("game-panel__title-action");
            _header.Add(_titleAction);
            hierarchy.Add(_header);

            _body = new VisualElement();
            _body.AddToClassList("game-panel__body");
            hierarchy.Add(_body);
            hierarchy.Add(new GamePanelBottomDeco());
        }

        public void SetTitle(Label titleLabel)
        {
            titleLabel.AddToClassList("game-panel__title");
            _header.Insert(0, titleLabel);
            _header.style.display = DisplayStyle.Flex;
        }

        public void SetTitleAction(VisualElement action)
        {
            _titleAction.Add(action);
        }
    }
}
```

上下の罫線（`game-panel__deco-line--top`・`--bottom`）は `_header` の前後に置き、`_header` と同じく `SetTitle` で表示する（上の雛形に足す）。

- [ ] **Step 5: L1 を書く**（`GamePanelTest.cs`、`UiTestHost` 継承）

確かめること: (1) `Add` した子が `game-panel__body` の中に入る、(2) `SetTitle` 前はヘッダと罫線が `DisplayStyle.None`、後は Flex でタイトルのテキストが一致、(3) `simulate.FrameUpdate()` 後に面の要素の customStyle から `--game-panel-edge-fade` が `Tokens.uss` の `--panel-edge-fade` と同じ値で読める、(4) 面と下部の三角の要素の `pickingMode` が Ignore、(5) 1280×720 のパネルで 400×300 にしたパネルのレイアウトが崩れない（本文の `layout.width` が 0 より大きく、パネル幅から Web の左右 padding を引いた値と 1px 以内で一致）。描画（generateVisualContent）が例外を出さないことは `simulate.FrameUpdate()` を 2 回回して `LogAssert.NoUnexpectedReceived()` で確かめる。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Components\."`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 7: 720p と 21:9 を撮る**（plan.md §4.6。`uloop screenshot` の rendering には UITK が写らないので RenderTexture へ描いて読む）

空のシーンで PlayMode に入り（`uloop control-play-mode --action Play`。シーンが MainGame なら先に `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene)`）、`uloop execute-dynamic-code` で次を行う: `GameUiPanelSettings.asset` を `Object.Instantiate` した PanelSettings に `targetTexture`（1280×720 と 2560×1080、sRGB）を付けた UIDocument を 2 つ作り、ルートの背景を `#9c7b42`、中央に 420×320 の `GamePanel`（タイトル「ポーズ」の Label・本文に Label 2 行）を置く。`uloop focus-window` の後に 2 回目の execute-dynamic-code で RT を `ReadPixels` して PNG にし、`.superpowers/sdd/screenshots/gamepanel-720p.png`・`gamepanel-2560x1080.png` に保存する。PNG を目で見て、面の上端と左右・下端のフェード、タイトル行・罫線・三角があることを確かめ、所見をタスク報告に書く。終わったら PlayMode を止める。

- [ ] **Step 8: コミットして push する**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Components moorestech_client/Assets/Scripts/Client.Tests/Ui/Components
git commit -m "feat(uitk): GamePanel の default を頂点カラーの面と Painter2D の三角で描く"
git push origin feature/uitk-s1-theme
```

### Task 6: UiMotion

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/IUiMotionClock.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/IUiAnimation.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiEasing.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiMotionSpec.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiTweenAnimation.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiInertiaAnimation.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiMotionTimeline.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiMotionPlayer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiMotionHandle.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/UiMotionSteps.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/UiMotionTimelineTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/UiInertiaAnimationTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/UiMotionPlayerTest.cs`

**Interfaces:**
- Produces:
  - `public interface IUiMotionClock { long GetNowMilliseconds(); }`
  - `public interface IUiAnimation { void Begin(); bool Advance(float elapsedMilliseconds); }`（`Begin` は頭からの状態に戻す。`Advance` は開始からの経過で状態を書き、終わったら true）
  - `public enum UiEasing { Linear, EaseOutCubic, EaseInOutSine }`、`public static class UiEasingFunctions { public static float Evaluate(UiEasing easing, float t); }`（`UiEasing.cs` に同居）
  - `public enum UiMotionLoop { Once, Repeat, PingPong }`
  - `public sealed class UiMotionSpec`（ctor `(float durationMilliseconds, float delayMilliseconds, UiEasing easing, UiMotionLoop loop)`、`readonly` フィールド 4 つ）
  - `public interface IUiTweenStep { void Apply(float easedProgress); }`
  - `public sealed class UiTweenAnimation : IUiAnimation`（ctor `(UiMotionSpec spec, IUiTweenStep step)`）。`delay` 中は何もしない。`Once` は尺で終わる。`Repeat`・`PingPong` は終わらない（取り消しで止める）
  - `public sealed class UiInertiaAnimation : IUiAnimation`（ctor `(float initialVelocityPerMillisecond, float decayPerMillisecond, float stopVelocityPerMillisecond, IUiInertiaStep step)`、`public interface IUiInertiaStep { void Apply(float offset); }`）。速度 `v(t) = v0 * exp(-k t)`、位置 `v0 (1 - exp(-k t)) / k`、`|v(t)| < stop` で終わる
  - `public sealed class UiMotionTimeline`（L0 用の純粋な走行器。ctor `(IUiMotionClock clock, IUiAnimation animation)`、`void Start()`・`bool Tick()`（終わったら true）・`void Restart()`・`void Cancel()`・`bool IsRunning`（`{ get; private set; }`）・`IObservable<Unit> Completed`）
  - `public static class UiMotionPlayer { public static UiMotionHandle Play(VisualElement target, IUiAnimation animation); }`。パネルのスケジューラ（`target.schedule.Execute(...).Every(0)`、時刻は `TimerState.now`）を時計にして `UiMotionTimeline` を回す。`DetachFromPanelEvent` で取り消す
  - `public sealed class UiMotionHandle`（`void Restart()`・`void Cancel()`・`IObservable<Unit> Completed`）。取り消したら `Completed` は発火しない。終わったら 1 回だけ発火して `OnCompleted`
  - `UiMotionSteps.cs`: `public sealed class OpacityTweenStep : IUiTweenStep`（ctor `(VisualElement target, float from, float to)`）、`TranslateYTweenStep`（ctor `(VisualElement target, float fromPixels, float toPixels)`）、`ScrollOffsetTweenStep`（ctor `(ScrollView scrollView, Vector2 from, Vector2 to)`。段階 3 の smooth scroll 用）

- [ ] **Step 1: L0 を書く**（`UiMotionTimelineTest.cs`）

手で進める時計 `ManualClock : IUiMotionClock`（テストファイル内の private クラス。`Advance(long ms)`）と、Apply の値を記録する `RecordingStep : IUiTweenStep` で、次を 1 テストずつ書く: (1) `Once` 200ms・Linear で 0/100/200ms の Apply が 0/0.5/1 で 200ms の Tick が true、`Completed` が 1 回、(2) delay 100ms の間は Apply されない、(3) 途中で `Restart` すると Apply が 0 から始まり、終わりの時刻が張り直される（通知の集約で寿命を張り直す用途）、(4) delay 中に `Cancel` すると以後 Apply されず `Completed` も来ない（予約した退場の取り消し）、(5) `Repeat` は尺を過ぎても Tick が false で 250ms で 0.25 に戻る、(6) `PingPong` は 300ms で 0.5（復路）、(7) `EaseOutCubic` の 0.5 が `1-(0.5)^3=0.875`。

- [ ] **Step 2: 慣性の L0 を書く**（`UiInertiaAnimationTest.cs`）

v0=2px/ms・k=0.005/ms・stop=0.01px/ms で、位置が単調に増え `v0/k=400px` を超えないこと、`ln(v0/stop)/k ≈ 1059.7ms` で終わること（1059ms では false、1060ms では true）。

- [ ] **Step 3: 実装を書く**（Interfaces の型をこの順で: `IUiMotionClock`・`IUiAnimation`・`UiEasing`・`UiMotionSpec`・`UiTweenAnimation`・`UiInertiaAnimation`・`UiMotionTimeline`）

`UiTweenAnimation.Advance(elapsed)` の核:

```csharp
        public bool Advance(float elapsedMilliseconds)
        {
            // 予約の遅延中は何も書かない
            // Write nothing while the scheduled delay runs
            var active = elapsedMilliseconds - _spec.DelayMilliseconds;
            if (active < 0f) return false;

            var cycles = _spec.DurationMilliseconds <= 0f ? 1f : active / _spec.DurationMilliseconds;
            if (_spec.Loop == UiMotionLoop.Once && cycles >= 1f)
            {
                _step.Apply(UiEasingFunctions.Evaluate(_spec.Easing, 1f));
                return true;
            }

            // Repeat は周の端数、PingPong は偶数周で往路・奇数周で復路
            // Repeat uses the cycle fraction; PingPong goes forward on even cycles and back on odd ones
            var fraction = cycles - Mathf.Floor(cycles);
            var progress = _spec.Loop == UiMotionLoop.PingPong && ((int)Mathf.Floor(cycles) & 1) == 1 ? 1f - fraction : fraction;
            if (_spec.Loop == UiMotionLoop.Once) progress = cycles;
            _step.Apply(UiEasingFunctions.Evaluate(_spec.Easing, progress));
            return false;
        }
```

`UiMotionTimeline` は `Start` で `_startMilliseconds = clock.GetNowMilliseconds()` と `animation.Begin()`、`Tick` で `animation.Advance(now - start)`、終わったら `IsRunning=false` にして `Subject<Unit>` に `OnNext`→`OnCompleted`。`Restart` は `Start` と同じ（走っていなくても走らせ直す）。`Cancel` は `IsRunning=false` にするだけで通知しない。`Tick` は `IsRunning` が false なら何もせず false。尺 0 の抜け道は作らない（plan.md §3.6。テストは時計を進める）。

- [ ] **Step 4: パネルの走行器を書く**（`UiMotionPlayer.cs`・`UiMotionHandle.cs`・`UiMotionSteps.cs`）

`UiMotionPlayer.Play` は、パネルのスケジューラの時刻を読む `IUiMotionClock`（`UiMotionPlayer` 内の private sealed class。直近の `TimerState.now` を持つ）で `UiMotionTimeline` を作り、`target.schedule.Execute(state => { clock.Set(state.now); if (timeline.Tick()) item.Pause(); }).Every(0)` で回す。`target.RegisterCallback<DetachFromPanelEvent>` で `Cancel` と `item.Pause()`。`UiMotionHandle.Restart` は item を `Resume` してから `timeline.Restart`。要素がパネルに無いときに `Play` されたら `Debug.LogError("[UiMotionPlayer] target is not attached to a panel")` を出して取り消し済みのハンドルを返す（スケジューラが無く動かないため。気づく経路はログ）。

- [ ] **Step 5: L1 を書く**（`UiMotionPlayerTest.cs`、`UiTestHost` 継承）

`OpacityTweenStep` で 0→1 を 200ms の `Once`。`simulate.FrameUpdate(0)`→`FrameUpdate(100)` で `style.opacity` が 0.5 付近（±0.05）、`FrameUpdate(250)` で 1 と `Completed` 1 回。2 件目: 再生中に要素を `RemoveFromHierarchy` すると、その後 `FrameUpdate` しても opacity が変わらず `Completed` が来ない。`simulate.FrameUpdate(long ms)` が無い版なら UI Test Framework の時刻を進める API（`PanelSimulator` の公開メンバー）を調べて合わせ、報告に書く。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Motion\."`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 7: コミットして push する**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion
git commit -m "feat(uitk): 時計を注入できる UiMotion（tween・慣性・再生し直し・取り消し・完了通知）を足す"
git push origin feature/uitk-s1-theme
```

### Task 7: LocalizedLabel・UiNumberFormat・UXML の生文字の禁止

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/LocalizedLabel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/UiNumberFormat.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/LocalizedLabelTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/UiNumberFormatTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/UxmlLiteralTextTest.cs`

**Interfaces:**
- Consumes: `Client.Localization.Localize.Get(LocalizationKey)`・`GetFormatted(LocalizationKey, IReadOnlyList<LocalizationTextParam>)`・`OnLanguageChanged`（Task 3 で `Client.Ui.asmdef` に `Client.Localization` を参照済み）
- Produces:
  - `public class LocalizedLabel : Label`。`public void SetKey(LocalizationKey key)`・`public void SetKey(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)`。パネルにいる間だけ `Localize.OnLanguageChanged` を購読し（`AttachToPanelEvent` で購読して解決、`DetachFromPanelEvent` で破棄）、キーが未設定のまま Attach したら `Debug.LogError` を出す
  - `public static class UiNumberFormat { public static string Fixed(double value, int decimals); public static string Integer(long value); }`（どちらも `CultureInfo.InvariantCulture`。桁区切りは入れない。Web の `toFixed` と同じ）

- [ ] **Step 1: 数値書式の L0 を書く**（`UiNumberFormatTest.cs`）

`CultureInfo.CurrentCulture` を `de-DE` にして（`[SetUp]` で退避、`[TearDown]` で戻す）`Fixed(1234.5, 1) == "1234.5"`、`Fixed(0.125, 2) == "0.13"`（`toFixed` と同じ丸めかを確かめ、違えば `MidpointRounding.AwayFromZero` を使って一致させる）、`Integer(1234567) == "1234567"`。

- [ ] **Step 2: 実装を書く**（`UiNumberFormat.cs`）

```csharp
using System;
using System.Globalization;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 表示用の数値書式。de などで「,」にならないよう InvariantCulture を明示する（plan.md §3.9）
    /// Display number formatting with explicit InvariantCulture so locales like de never print ',' (plan.md §3.9)
    /// </summary>
    public static class UiNumberFormat
    {
        // Web の toFixed と同じく、桁区切りなしで小数点は「.」
        // Like the Web's toFixed: no grouping and '.' as the decimal point
        public static string Fixed(double value, int decimals)
        {
            var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            return rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        public static string Integer(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
```

- [ ] **Step 3: LocalizedLabel の L1 を書く**（`LocalizedLabelTest.cs`、`UiTestHost` 継承）

既存の `Client.Tests/Localization/Resolution/LocalizeTest.cs` の辞書の用意の仕方（`Localize.MergeGameDictionaries` か言語の差し替え）を前例に、2 言語で値の違うキーを 1 つ用意し、(1) `SetKey` して Attach すると現在の言語の文言、(2) 言語を変えると文言が変わる、(3) Detach 後に言語を変えても文言が変わらない（購読が切れている）、(4) 引数付きの `SetKey` で `{Name}` が差し込まれる、を確かめる。テストの後で言語と辞書を元に戻す（`LocalizeTest` の TearDown に倣う）。

- [ ] **Step 4: 実装を書く**（`LocalizedLabel.cs`）

```csharp
using System;
using System.Collections.Generic;
using Client.Localization;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// ローカライズキーと引数を持ち、パネルにいる間は言語変更で解決し直すラベル（plan.md §3.9）
    /// Label holding a localization key and params that re-resolves on language change while on a panel (plan.md §3.9)
    /// </summary>
    public class LocalizedLabel : Label
    {
        private static readonly IReadOnlyList<LocalizationTextParam> NoParams = Array.Empty<LocalizationTextParam>();
        private LocalizationKey _key;
        private IReadOnlyList<LocalizationTextParam> _textParams = NoParams;
        private bool _hasKey;
        private IDisposable _languageSubscription;

        public LocalizedLabel()
        {
            RegisterCallback<AttachToPanelEvent>(_ => OnAttached());
            RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        public void SetKey(LocalizationKey key)
        {
            SetKey(key, NoParams);
        }

        public void SetKey(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)
        {
            _key = key;
            _textParams = textParams;
            _hasKey = true;
            if (panel != null) Resolve();
        }

        private void OnAttached()
        {
            // キー無しで載ったら生の文言を出さず、開発者に知らせる
            // Report a label attached without a key instead of showing raw text
            if (!_hasKey) Debug.LogError($"[LocalizedLabel] attached without a key: {name}");
            _languageSubscription = Localize.OnLanguageChanged.Subscribe(_ => Resolve());
            Resolve();
        }

        private void OnDetached()
        {
            _languageSubscription?.Dispose();
            _languageSubscription = null;
        }

        private void Resolve()
        {
            if (!_hasKey) return;
            text = _textParams.Count == 0 ? Localize.Get(_key) : Localize.GetFormatted(_key, _textParams);
        }
    }
}
```

`LocalizationKey` が値型でなく `default` が使えない型なら、`_hasKey` の代わりに `_key != null` で判定する（生成された型を見て合わせる）。

- [ ] **Step 5: UXML の生文字の L2 を書く**（`UxmlLiteralTextTest.cs`）

`Client.Ui` 配下の全 `*.uxml` を読み、属性 `text="…"`・`label="…"`・`tooltip="…"` の値が空でなければ失敗にする（キーは C# の `LocalizedLabel.SetKey` で入れる。`noJsxVisibleLiteral` の後継）。判定は `public static IReadOnlyList<string> FindLiteralTextAttributes(string uxml)` をテストクラスの中に書き、(1) `<ui:Label text="保存" />` で 1 件、(2) `<ui:Label name="title" />` で 0 件、(3) `<ui:Label text="" />` で 0 件の単体ケースと、(4) 実ファイルの走査（今は UXML が 0 件でも通る。0 件であることをメッセージに出す）の 4 テストにする。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Text\."`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 7: コミットして push する**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text moorestech_client/Assets/Scripts/Client.Tests/Ui/Text
git commit -m "feat(uitk): LocalizedLabel と InvariantCulture の数値書式、UXML の生文字を落とす L2 を足す"
git push origin feature/uitk-s1-theme
```

### Task 8: AnchorIds と TutorialAnchor の登録簿

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/AnchorIds.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/TutorialAnchorIdMapper.cs`（と Unity が消す `.meta`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/ItemViewHighLightTutorialManager.cs`（`TutorialAnchorIdMapper.FromItemId` → `AnchorIds.RecipeItem`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialAnchorContractTest.cs`・`moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/ItemViewHighLightTutorialManagerTest.cs`（参照先の付け替え）
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Anchor/TutorialAnchor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Anchor/TutorialAnchorRegistry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Anchor/TutorialAnchorResolution.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef`（references に `Client.Game` を足す）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/AnchorIdsWebParityTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorRegistryTest.cs`

**Interfaces:**
- Produces:
  - `public static class AnchorIds`: 静的 ID 19 個を `public const string`（名前は Web のキーを PascalCase に。`PlacementHud = "placement.hud"` … `EquipmentSelectedSlot = "equipment.selected-slot"`）、`public static readonly IReadOnlyList<string> StaticIds`、prefix 6 個を `public const string ResearchNodePrefix = "research.node-"` などと `public static readonly IReadOnlyList<string> DynamicPrefixes`、生成関数 `ResearchNode(string guid)`・`RecipeItem(int itemId)`・`BuildMenuEntry(string kind, string id)`・`ChallengeNode(string guid)`・`InventoryItem(string itemGuid)`・`EquipmentSlot(int index)`（小文字化は Web の関数と同じ所だけ。空白を含んだら `Debug.LogError` を出してそのまま返す＝Web の実機の扱いと同じ）
  - `public static class TutorialAnchor { public const string MarkerClassName = "tutorial-anchor"; public static void Mark(VisualElement element, params string[] ids); }`（ID を USS クラスとして付け、`AttachToPanelEvent` で登録簿へ入れ、`DetachFromPanelEvent` で同期に出す）
  - `public static class TutorialAnchorRegistry { public static TutorialAnchorResolution Resolve(IPanel panel, string anchorId); }`（パネルごとの `Dictionary<string, List<VisualElement>>`。0 件 missing・1 件 unique・2 件以上 duplicate。missing と duplicate は `Debug.LogWarning("[TutorialAnchorRegistry] …")`）
  - `public abstract class TutorialAnchorResolution`、派生 `public sealed class Missing`・`public sealed class Unique { public readonly VisualElement Element; }`・`public sealed class Duplicate { public readonly int Count; }`（入れ子の sealed class。判別 union の形は plan.md §3.2）

- [ ] **Step 1: Web と突き合わせる L0 を書く**（`AnchorIdsWebParityTest.cs`）

`moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts` をリポジトリ直下からの相対で読み、`TutorialAnchorIds = { … }` の中の `"…"` の値の集合と `AnchorIds.StaticIds`、`TutorialAnchorDynamicPrefixes = { … }` の値の集合と `AnchorIds.DynamicPrefixes` が一致することを確かめる（共存中の同期。撤去で消す。plan.md §4.5）。生成関数を 6 本とも 1 例ずつ: `ResearchNode("ABC")=="research.node-abc"`、`RecipeItem(42)=="recipe.item-42"`、`BuildMenuEntry("Block","X")=="build-menu.entry-block-x"`、`ChallengeNode("ABC")=="challenge.node-abc"`、`InventoryItem("ABC")=="inventory.item-abc"`、`EquipmentSlot(3)=="equipment.slot-3"`。

- [ ] **Step 2: AnchorIds を書き、Mapper を吸収する**

`AnchorIds.cs` を Interfaces どおりに書く（200 行以下）。`TutorialAnchorIdMapper.cs` を削除し、`ItemViewHighLightTutorialManager.cs` と 2 つのテストの参照を `AnchorIds.RecipeItem`・`AnchorIds.RecipeItemPrefix` に付け替える。`TutorialAnchorContractTest` のテスト名とフィクスチャ照合はそのまま残す。

- [ ] **Step 3: 登録簿の L1 を書く**（`TutorialAnchorRegistryTest.cs`、`UiTestHost` 継承）

(1) `Mark(e, AnchorIds.PauseMenu)` して `rootVisualElement.Add(e)` すると `Resolve(rootVisualElement.panel, "pause.menu")` が `Unique` で `Element == e`、`e.ClassListContains("pause.menu")` と `ClassListContains(TutorialAnchor.MarkerClassName)` が true、(2) 外すと `Missing`（`LogAssert.Expect(LogType.Warning, new Regex("TutorialAnchorRegistry"))`）、(3) 2 つに同じ ID を付けると `Duplicate` で `Count == 2` と警告、(4) 1 要素に 2 つの ID を付けるとどちらでも `Unique`、(5) 親ごと外すと子のアンカーも消える（Detach は子孫にも届く）。

- [ ] **Step 4: 実装を書く**（`TutorialAnchor.cs`・`TutorialAnchorRegistry.cs`・`TutorialAnchorResolution.cs`）

登録簿はパネル（`IPanel`）をキーにした static の辞書にする（ゲームのパネルは 1 枚、テストはパネルごとに分かれる。Detach で同期に消すので古い要素が残らない）。`Mark` は要素ごとに `AttachToPanelEvent`（`evt.destinationPanel` に登録）・`DetachFromPanelEvent`（`evt.originPanel` から削除）を登録する。パネルの辞書が空になったらパネルのキーも消す。`Mark` を呼んだ時点で既にパネルにいる要素は、その場で登録する。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Anchor\.|TutorialAnchorContractTest|ItemViewHighLightTutorialManagerTest"`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 6: コミットして push する**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(uitk): AnchorIds を C# の正本にして TutorialAnchorIdMapper を吸収し TutorialAnchor.Mark と登録簿を足す"
git push origin feature/uitk-s1-theme
```

### Task 9: 通しの確認とレーンの締め（本体が行う）

- [ ] **Step 1: 全体の compile と UI テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.|UiStateSubStatePublishTest|WireContractTest|TutorialAnchorContractTest|ItemViewHighLightTutorialManagerTest"`
Expected: ErrorCount 0 / 全件 Passed

- [ ] **Step 2: ゲーム起動の回帰**（R11。EditModeInPlayingTest。ランタイムの変化はテーマの差し替えとロケールのクラス付けだけで、層は空のままなので録画プレイテストではなくこれで確かめる。判断記録 D-9）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GameUiSkeletonTest"`
Expected: 1/1 Passed。続けて `uloop get-logs --project-path ./moorestech_client --log-type Error` で、骨組み報告にある WebUiHost の Node 未配置 1 件以外の Error が無いこと

- [ ] **Step 3: フォントの動的データを空に戻す**（Task 3 Step 8 のコマンド）。`git status` が clean であること

- [ ] **Step 4: G1 と台帳**

Run: `python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD`
Expected: 未記帳 0・違反なし。`docs/uitk/TODO.md` の「## 実測の結論」に S7・S10・customStyle の行があること

- [ ] **Step 5: sdd-archive と完了報告**

`scripts/sdd-archive`（moores-subagent-driven-development）でワークスペースを退避し、記録 repo（`../moorestech_logs`）の `harness/sdd/feature-uitk-stage1/lane-theme-done.md` を書いて moorestech_logs の README.md に従いコミット・push する。moores-code-review と pr-create はこのレーンでは行わない（判断記録 D-1）。

---

## 判断記録（ADR）

- 設計の正本: `docs/uitk-migration/plan.md`（§2.2・§3.4〜3.6・§3.9・§3.10・§4.5・§7.2）、`.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、`.decisions/2026-10-26-UITK移行の第一弾はポーズ設定バグ報告トーストの命綱にする.md`、`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md`（段で PR 1 本）、骨組み plan `docs/superpowers/plans/2026-11-05-uitk-stage1-skeleton.md`
- D-1 このレーンは moores-code-review と pr-create を行わない。出所: 指揮セッションの依頼文（段階0から続く「各レーンは統合ブランチへ入れ、PR は段階で 1 本」）。最終レビューと PR は指揮が統合後に行う
- D-2 S7 の結論: 既定 (a)「部品の段で実機で決める」を維持し、(b) の α 補正を Tokens.uss の初期値に機械的に当てる。(c) forceGammaRendering は sRGB 合成で完全一致したが、targetTexture 経由の合成と入力座標の付け替え（`SetScreenToPanelSpaceFunction`）とフルスクリーンの追加パスが要り、S1 の前後関係も変わるので採らない。証拠は `docs/uitk/TODO.md` の S7 行
- D-3 文字の下限 13px は文字寸法トークン（`-font-size`・`-text-size`）に機械的に当て、アイコン上の個数 2 つを外す（plan.md §3.10・S7）
- D-4 フォントは Editor で作ってコミットする（S10 成立）。Dynamic にして `clearDynamicDataOnBuild` を立て、コミット前に動的データを空にする。フォールバックは Latin→JP→KR の非循環。CJK 4 言語は JP 先頭（Web の `:root[data-locale]` と同じ並び）。韓国語は Web では OS フォントへ落ちていたが UITK は明示する（§3.10）
- D-5 トークンの C# 読み出しは「部品 USS で var() を受け直し、文字列で読んで解釈」。`:root` の customStyle が子へ継承されないため（実測。TODO.md の customStyle 行）。plan.md §3.5 の「C# は CustomStyleProperty で解決後の値を読む」はこの形で成り立つ
- D-6 GamePanel の `box-shadow`（外周の影）は USS に無く、段階1ではメッシュでも描かない（plan.md §3.4 の GamePanel の描き方に影は無い）。見た目の差は Task 5 の撮影で見て、必要なら段階2の hud variant を作るときに足す
- D-7 `GameUiTheme.tss` にトークン USS も束ねる（ドロップダウンのポップアップのように文書のルートの外へ出る要素もトークンを読めるようにするため）。`GameUiRoot.tokenStyleSheets` と `UiTestHost` のトークン USS の当て込みは同じ値の二重当てになるが、他レーンと衝突しないよう残す。統合後に integrator が片方へ寄せる（完了報告の要裁定に書く）
- D-8 `AnchorIds` は Client.Game の `InGame/Tutorial/UIHighlight/` に置く（`ItemViewHighLightTutorialManager` が使い、Client.Game は Client.Ui.State を参照できないため）。`Client.Ui` は `Client.Game` を参照に足す（向きは plan.md §3.1 の `Client.Game` ← `Client.Ui.State` ← `Client.Ui` と矛盾しない）。登録簿はパネルをキーにした static の辞書（`Mark` が static で呼ばれる API のため。Detach で同期に消す）
- D-9 ランタイム挙動の確認は録画プレイテストではなく `GameUiSkeletonTest`（EditModeInPlayingTest）の再実行で行う。このレーンが本番のパネルに足すのはテーマとロケールのクラスだけで、層は空のまま（画面は screens レーン）
- D-10 ユーザーへの質問は行わない（依頼文）。人にしか決められないものは推奨案で進め、完了報告の「要裁定」に書く。user-simulator の review は質問を出せないため回さず、設計検査（Phase 1〜2.6）の fresh-context subagent の所見を本体が既定で裁く
