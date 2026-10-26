# UITK 段階0 第2波（撤去・差し込み記法の統一） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** UITK 移行の段階 0 のうち、使っていない UI 経路の撤去（0-1 OneJS・DebugUI／0-12 旧 SkitUI）と、翻訳文の差し込み記法の名前付きへの統一（0-6）を、見た目と挙動を変えずに行い、UI Test Framework を manifest に入れる。

**Architecture:** 差し込みは「名前と値の組」`LocalizationTextParam` に一本化する。C# の補間器は `{name}` を 1 回の走査で埋め、埋まらない枠を開発者ログへ出す。値を運ぶ経路（ツールチップ行・通知パケット・Web への JSON）はすべて名前付きにし、Web は受け取った名前付きの値をそのまま補間値に使う。撤去は、prefab とシーンの参照を uloop（Unity 自身のシリアライズ）で外してから、コードと資産を消す。

**Tech Stack:** Unity 6000.3.8f1（C#・VContainer・UniRx・MessagePack）、moorestech_web/webui（TypeScript・zod・vitest）、uloop CLI、Unity batchmode の Windows ビルド。

## Requirements

1. R1 OneJS・DebugUI の撤去: `moorestech_client/Packages/manifest.json` と `packages-lock.json` から `com.singtaa.onejs` と `com.annulusgames.debug-ui` が消えている。受け入れ: Windows ビルド（batchmode）のログと成果物で `moorestech_Data/Plugins/x86_64/quickjs_unity.dll` が無く、`Plugins/x86_64` のそれ以外のファイル一覧は基準（前回配布の Windows ビルドログ（`$PLAYTEST_RUNS/playtest-20261021-2150/unity-build-windows.log`。`$PLAYTEST_RUNS` は環境ローカル規約が示す playtest の runs 置き場） の CopyFiles 行）と一致する
2. R2 UI Test Framework の導入: manifest に `"com.unity.ui.test-framework": "1.0.0"` があり、`moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef` が `Unity.UI.TestFramework.Runtime` と `Unity.UI.TestFramework.Editor` を参照する。受け入れ: コンパイル 0 エラーで、`CompilationPipeline` から見た `Client.Tests` の参照に両アセンブリが入っている
3. R3 旧 SkitUI の削除: `SkitUI.prefab`・`Client.Skit/UI/SkitUI.cs`・`SkitUITools.cs`・`SkitPresentationMode.cs`・`Assets/Asset/UI/NewUI/Skit/` 一式が消え、スキットのコマンドは Web 経路だけを持つ。受け入れ: コンパイル 0 エラー、Skit 系の EditMode テストが緑、`MainGameUI.prefab`・`GameSystem.prefab`・`SkitTest.unity`・`StoryTest.unity` に消えた guid（`9525eda8e634954499da896e6c682cbd`・`01c05861d09e4b2f9dc6d66ceccd97dd`）が残らない、スキットの録画シナリオが緑。bd `moorestech-lnsf.8` を閉じる
4. R4 C# の補間器が名前付きを埋める: `Localize.GetFormatted(LocalizationKey, IReadOnlyList<LocalizationTextParam>)` が `{name}` を埋め、埋まらない枠があれば `Debug.LogError` を出す。位置指定の引数の口は残さない。受け入れ: `LocalizeGetFormattedTest`・`LocalizationTextInterpolatorTest` が緑
5. R5 `{pN}` の全廃: `Localization/localization.csv` の `{pN}` 32 キー・190 か所が下の対応表どおりの名前になり、`{p` で始まる枠が 0 件。受け入れ: `LocalizationPlaceholderParityTest` が緑、`grep -c '{p[0-9]' Localization/localization.csv` が 0
6. R6 値を運ぶ経路の名前付き化: `TooltipLine.TextParams`・`ITapInteractAction.HintParams`・`RemoteConnectionDenyReason.TextParams`・`LoadingProgressLog.AppendElapsed`・`NotificationMessagePack.MessageParams`・Web への `textParams`/`messageParams` が名前付きになる。受け入れ: C# の関連テストと webui の `pnpm test`・`tsc --noEmit`・`pnpm lint` が緑。録画シナリオで、ツールチップ・通知・ロード画面のログに `{` が残っていない（スクショで確認）
7. R7 統合: `origin/feature/uitk-stage0` を取り込んで push し、統合 PR #1559 に載せる本文の下書きを `.superpowers/sdd/pr-body.md` に書く。PR は作らない

**やらないこと**
- 型付きの書式メソッドの生成器は作らない（`.decisions/` 08-26 裁定。移行計画 §7.3）
- Web 側の表示文言・見た目は変えない（文言の中身は名前の置換だけ）
- 並行レーンの持ち物の設計変更はしない（Global Constraints の「レーン境界」）。コンパイルを通すための最小の書き換えだけ行う
- Steam（playtest-staging を含む）への配布・Discord への投稿はしない
- 会話 UI の予備は作らない（UITK の会話 UI は段階 2）

## Global Constraints

- 作業場所: worktree `uitk-s0-wave2`（置き場は環境ローカル規約 CLAUDE.local.md の worktree 置き場）、ブランチ `feature/uitk-s0-wave2`。最初に `pwd` で確かめる。`moorestech_web/node` と `webui/node_modules` は用意済み
- bd: `moorestech-albof.5`（撤去）と `moorestech-albof.7`（差し込み）。区切りごとに両方へ `bd note <id> "..."` で 1〜3 行。`bd edit` は使わない
- **Codex は一切使わない。** `codex exec`・`codex_workflow_runner/run.mjs`・codex-audit・codex-implement・moores-code-review の ③ はどれも起動しない。`独立レビュー待ち` ラベルも貼らない
- **Unity Editor はマシン全体で 2 本まで**（ユーザー裁定 2026-10-26 13:24「2本で」）。起動・batchmode ビルドの前に毎回 `pgrep -fl "Unity.app/Contents/MacOS/Unity -projectPath"` で本数を数え、2 本未満のときだけ起動する。いまは C（uitk-s0-state-di）と F（uitk-s0-dsl）が 2 本を使っていて、次に空いた枠は B（uitk-s0-remote-exec）が使う。第 2 波はその後なので、Editor の要らない Task 1〜3 を先に進め、Editor が要る Task 4 以降は枠が空くまで待つ（待つ間は `moores-wt status` と pgrep を数分おきに前景で見る。待ちが 2 時間を超えたら bd note に書いて待ち続ける）
- Editor の起動は `uloop launch ./moorestech_client`（worktree 直下から）。PID を控え、作業が終わったら `kill <PID>` で止める。`pkill -f` は使わない。batchmode ビルドも Unity の 1 本に数え、自分の Editor を止めてから走らせる
- 長い処理（コンパイル・テスト・Windows ビルド・vitest）はバックグラウンドのポーリングで待たず、全部前景で実行する（Bash の timeout を最大 600000ms、テストは `--timeout-seconds` を使う）。subagent への依頼文にも同じことを書く
- 区切り（各タスクの終わり）ごとにコミットして `git push origin feature/uitk-s0-wave2` する
- AskUserQuestion は使わない。スキルが「ユーザーに聞く」と定める問いは推奨案で決め、本 plan の `## 判断記録（ADR）` に「agent 既定（ユーザー未裁定）」と書き、`.superpowers/sdd/pr-body.md` の「ユーザーに見てほしい判断」に並べる
- Steam・Discord など外へ出す操作はしない
- Claude の週間枠が速く減っている。subagent は要る数だけにし、大きいファイルの全文の読み直しを避ける（grep と行範囲の Read で読む）
- **レーン境界**: B（`Client.RemoteExec`・`WebUiEndpoints.cs`・`InitializeScenePipeline.cs`・`WebUiStartup.cs`・`WebUiHost.KestrelPort`）、C（`Client.WebUiHost` の topic の状態部分・`WebUiGameBinder`・`UIStateControl`・研究とチャレンジなどの状態クラス）、F（`Client.Playtest`・録画シナリオ・プレイテストのスキル文書）には設計変更を入れない。本 plan で型の変更に追随する 1〜数行の書き換えが要るファイルは、Task 2 の Files に列挙済み（`InitializeScenePipeline.cs`・`TooltipTopic.cs`・`NotificationTopic.cs`・`Client.Playtest/Operations/Ui/*.cs` 3 本）。書き換えたら `bd note moorestech-albof.7` にファイル名を書き、統合のマージで解く
- Prefab・シーン・ScriptableObject を Write/Edit・sed で書き換えない。`uloop execute-dynamic-code` で Unity に保存させる。`.meta` は手で作らない（削除は対の `.meta` ごと `git rm`）
- コード規約（AGENTS.md）: 1 ファイル 200 行以下、`try-catch` 禁止（外部境界を除く）、`Func<>` 禁止、デフォルト引数禁止、日本語→英語の 2 行コメント、fail-closed の経路は理由をログへ
- コマンド: コンパイル `uloop compile --project-path ./moorestech_client`、テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`、ログ `uloop get-logs --project-path ./moorestech_client --log-type Error`。Domain Reload 中のエラーは 45 秒待って再試行
- webui のコマンドは `cd moorestech_web/webui && PATH="$PWD/../node/mac-arm64/bin:$PATH" pnpm test` の形（`pnpm` が PATH に無ければ `npx pnpm`）。`tsc --noEmit -p tsconfig.json` と `pnpm lint` も同じ PATH で回す

### 名前の対応表（R5・R6。全言語列で同じ置換）

| キー | 旧 | 新 |
|---|---|---|
| `ui.notification.researchCompleted` | `{p0}` | `{research}` |
| `ui.notification.challengeCompleted` | `{p0}` | `{challenge}` |
| `ui.notification.saveMigrationMissingMasterPruned` | `{p0}` `{p1}` `{p2}` | `{blockCount}` `{itemStackCount}` `{researchCount}` |
| `ui.notification.undoRestoreSkipped` | `{p0}` | `{count}` |
| `ui.notification.blueprintPasteCostShortage`・`blueprintPasteNotUnlocked`・`blueprintPasteLineFailed`・`blueprintPastePlacementFailed` | `{p0}` | `{count}` |
| `ui.tooltip.requiredItems` | `{p0}` | `{items}` |
| `ui.tooltip.placeMaterialShortage` | `{p0}` `{p1}` `{p2}` | `{itemName}` `{ownedCount}` `{requiredCount}` |
| `ui.tooltip.placeWireCost` | `{p0}` | `{count}` |
| `ui.tooltip.namedMineHold`・`namedMineClick`・`namedCannotHandMine` | `{p0}` | `{name}` |
| `ui.tooltip.namedRequiredItems` | `{p0}` `{p1}` | `{name}` `{items}` |
| `ui.tooltip.interactOpenBlock` | `{p0}` | `{name}` |
| `ui.tooltip.blueprintCopyBlocksInRange`・`deleteBoxTargetsInRange`・`deleteBoxSelectedInRange`・`deleteSelectedCount` | `{p0}` | `{count}` |
| `ui.loading.serverConnected`・`initialDataFetched`・`blockAssetsLoaded`・`itemImagesLoaded`・`connectToolImagesLoaded`・`fluidImagesLoaded`・`blockScreenshotsCaptured`・`trainCarScreenshotsCaptured` | `{p0}` | `{elapsed}` |
| `ui.loading.terrainReady` | `{p0}` `{p1}` | `{chunkCount}` `{elapsed}` |
| `ui.mainMenu.connectPortTooLarge` | `{p0}` | `{max}` |
| `ui.mainMenu.connectPortTooSmall` | `{p0}` | `{min}` |
| `ui.mainMenu.connectFailed` | `{p0}` | `{detail}` |

通知で文言に枠が無いのに値を送っている `denied.deleteRejected`・`denied.deleteUnconfirmed` は、名前 `target` で送る（表示は今と同じく使われない）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 配置5行・データフロー・Skit死活表を実コードと照合し、層マップとの矛盾なし（Dictionary の Key 前例: `EventProtocolProvider.cs:94`・`ChangeBlockStateEventPacket.cs:98`）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱4・第3バケツ0 / 契約型の置き場・Web の名前欠損の素通り・翻訳欠損の扱い・TransitionCommand のキャンセル経路。裁定は判断記録 D11〜D14（agent 既定）

---

## 配置と前例

| # | 項目 | 配置 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `LocalizationTextParam`（新設 struct） | `Client.Localization` | 値型・`IEquatable` | `TooltipLine`（`Client.Game/InGame/UI/Tooltip/TooltipLine.cs`）の readonly struct＋等値 |
| 2 | `LocalizationTextInterpolator.Interpolate` の改修 | `Client.Localization`（internal のまま） | 1 回走査の置換 | Web の `createTranslationResolver`（`i18nStore.ts:125`）と同じ「名前で引き、無ければ枠を残す」 |
| 3 | `NotificationMessagePack.MessageParams` を `Dictionary<string,string>` に | `Server.Event`（既存） | MessagePack の Key(2) の型変更 | 同クラスの既存 Key 群。Event パケットはセーブに入らない |
| 4 | Web の `textParams`/`messageParams` を `z.record` に | `bridge/contract/schemas/ui.ts`（既存） | zod スキーマ | 同ファイルの既存 strict スキーマ |
| 5 | スキットのコマンドから非 Web 分岐を削る | `Client.Skit/Commands/*.cs`（既存の生成コマンドの partial 実装。partial は生成器の要求で既存） | 分岐の削除のみ | `SkitManager.cs:197` が Web 固定にしている現状 |

データフロー（R6）: `生産者（C#）→ TooltipLine / NotificationMessagePack の名前付き値 → JSON（record）→ Web の補間値 → 表示`。新しい分岐や第 2 の経路は足さず、運ぶ型だけを替える。

死活表（R3。SkitUI にぶら下がる操作）: スキットの台詞送り・選択肢・暗転・テキスト欄の表示切替・ボイス再生は、すべて Web 経路（`SkitPresentationStateStore`）とボイスの Unity 経路で生きている（`TextCommand.cs` の `ExecuteWebPresentationAsync`、`SelectionCommand.cs:53-58`、`TransitionCommand.cs:22-23`、`ShowTextCommand.cs:12-13`）。削るのは `WebUiEnabled == false` の分岐だけで、ADR 0052 以降つねに死にコード。退化する操作は無い。

---

## Task 1: C# の名前付き差し込み（補間器・CSV・生産者）— Editor 不要で書き、コンパイルは Task 4

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Localization/LocalizationTextParam.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/LocalizationTextInterpolator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`（`GetFormatted` のみ）
- Modify: `Localization/localization.csv`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/TooltipLine.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/IMouseCursorTooltip.cs`・`MouseCursorTooltipState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Tap/ITapInteractAction.cs`・`TapInteractionDriver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Interact/BlockOpenInteractAction.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarInteractActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionMaterialShortageLine.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/Feedback/ElectricWireFeedbackLines.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopyFeedbackLines.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxIntentView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/RemoteConnectionDenyReason.cs`・`moorestech_client/Assets/Scripts/Client.Starter/InitializeProprieties.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/Progress/LoadingProgressLog.cs`・`Client.Starter/Initialization/ServerConnectionInitializer.cs`・`Client.Starter/Initialization/ModAssetLoader.cs`・`Client.Starter/Initialization/ModAssetIconLoader.cs`
- Modify（レーン B の持ち物。1 行だけ）: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:181`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs`
- Modify（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeGetFormattedTest.cs` と、`git grep -l "TextParams\|textParams\|HintParams\|GetFormatted(" -- moorestech_client/Assets/Scripts/Client.Tests` が返すファイル（2026-10-26 時点で 21 件: `Starter/LoadingProgressLogTest.cs`・`Starter/InitializeProprietiesTest.cs`・`UIState/DragDelete/Service/DeleteBoxInputWiringTest.cs`・`Mining/MiningFocusStateTestFixture.cs`・`PlaceSystem/**` の 12 本・`Tooltip/TooltipPresentationEqualityTest.cs`・`WebUi/WireContractC2Test.cs`・`Interact/BlockInteractableTest.cs`・`Interact/HoldKey/StubTapInteractAction.cs`・`WebUi/WireFixtures/tooltip.json`）。grep に掛からないが要素型の変更で落ちる `Client.Tests/Mining/MiningFocusStateEarnItemNameTest.cs:61-66`（`shownParams[0]` を string と比べている）も直す。`tooltip.json` の `textParams` は配列から `{"count":"3"}` の形（対応表の名前）にする
- Create（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizationTextInterpolatorTest.cs`

**Interfaces:**
- Produces:
  - `public readonly struct Client.Localization.LocalizationTextParam : IEquatable<LocalizationTextParam>` — `public readonly string Name; public readonly string Value; public LocalizationTextParam(string name, string value)`、`ToString()` は `"{Name}={Value}"`（録画シナリオが `string.Join(",", line.TextParams)` でログに出しているため、F レーンのシナリオを書き換えずに読める形にする）
  - `public static string Localize.GetFormatted(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)`（`IReadOnlyList<string>` 版は消す）
  - `internal static string LocalizationTextInterpolator.Interpolate(string keyForLog, string template, IReadOnlyList<LocalizationTextParam> textParams)`
  - `TooltipLine(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)`、`public readonly IReadOnlyList<LocalizationTextParam> TextParams`
  - `IMouseCursorTooltip.Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)`
  - `ITapInteractAction.HintParams : IReadOnlyList<LocalizationTextParam>`
  - `RemoteConnectionDenyReason(LocalizationKey key, LocalizationTextParam textParam)`、`TextParams : IReadOnlyList<LocalizationTextParam>`
  - `LoadingProgressLog.AppendElapsed(LocalizationKey key)` と `AppendElapsed(LocalizationKey key, LocalizationTextParam leadingParam)`

- [ ] **Step 1: `LocalizationTextParam` を作る**

```csharp
using System;

namespace Client.Localization
{
    // 翻訳文の名前付き差し込み1個。{Name}をValueで埋める
    // One named substitution in a translated text; {Name} is replaced by Value
    public readonly struct LocalizationTextParam : IEquatable<LocalizationTextParam>
    {
        public readonly string Name;
        public readonly string Value;

        public LocalizationTextParam(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public bool Equals(LocalizationTextParam other)
        {
            return Name == other.Name && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizationTextParam other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value);
        }

        // 録画シナリオのログが名前ごと読めるようにする
        // Keeps recorded-scenario logs readable with the name attached
        public override string ToString()
        {
            return $"{Name}={Value}";
        }
    }
}
```

- [ ] **Step 2: 補間器を名前付きにする**（1 回の走査。差し込んだ値の中の `{...}` は再置換しない。埋まらない枠は残し、`Debug.LogError` を出す — 名前の食い違いは画面に生の `{name}` が出るだけで気づけないため）

```csharp
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Client.Localization
{
    // 辞書テンプレの{name}を名前付きの値で埋める
    // Fills a dictionary template's {name} placeholders with named values
    internal static class LocalizationTextInterpolator
    {
        private static readonly Regex PlaceholderPattern = new(@"\{([^{}]+)\}");

        internal static string Interpolate(string keyForLog, string template, IReadOnlyList<LocalizationTextParam> textParams)
        {
            var builder = new StringBuilder(template.Length);
            var cursor = 0;
            foreach (Match match in PlaceholderPattern.Matches(template))
            {
                builder.Append(template, cursor, match.Index - cursor);
                builder.Append(ResolvePlaceholder(match));
                cursor = match.Index + match.Length;
            }
            builder.Append(template, cursor, template.Length - cursor);
            return builder.ToString();

            #region Internal

            string ResolvePlaceholder(Match match)
            {
                // 名前が一致する値を探し、無ければ枠を残して開発者へ知らせる
                // Find the value with the matching name; otherwise keep the token and tell the developer
                var name = match.Groups[1].Value;
                foreach (var textParam in textParams)
                {
                    if (textParam.Name == name) return textParam.Value;
                }
                Debug.LogError($"[Localize] {keyForLog}: no value for placeholder {match.Value}");
                return match.Value;
            }

            #endregion
        }
    }
}
```

`Localize.cs` の `GetFormatted` を次に替える:

```csharp
        public static string GetFormatted(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)
        {
            return LocalizationTextInterpolator.Interpolate(key.Key, Get(key), textParams);
        }
```

- [ ] **Step 3: CSV を書き換える**（Global Constraints の対応表をそのまま使う。CSV のクォートを崩さないよう、行の先頭キーで当てて行単位の文字列置換にする）

スクリプトはスクラッチに置いて実行する（repo に入れない）:

```python
# rename_placeholders.py — usage: python3 -I rename_placeholders.py Localization/localization.csv
import re, sys
M = {
 "ui.notification.researchCompleted": ["research"],
 "ui.notification.challengeCompleted": ["challenge"],
 "ui.notification.saveMigrationMissingMasterPruned": ["blockCount", "itemStackCount", "researchCount"],
 "ui.notification.undoRestoreSkipped": ["count"],
 "ui.notification.blueprintPasteCostShortage": ["count"],
 "ui.notification.blueprintPasteNotUnlocked": ["count"],
 "ui.notification.blueprintPasteLineFailed": ["count"],
 "ui.notification.blueprintPastePlacementFailed": ["count"],
 "ui.tooltip.requiredItems": ["items"],
 "ui.tooltip.placeMaterialShortage": ["itemName", "ownedCount", "requiredCount"],
 "ui.tooltip.placeWireCost": ["count"],
 "ui.tooltip.namedMineHold": ["name"],
 "ui.tooltip.namedMineClick": ["name"],
 "ui.tooltip.namedCannotHandMine": ["name"],
 "ui.tooltip.namedRequiredItems": ["name", "items"],
 "ui.tooltip.interactOpenBlock": ["name"],
 "ui.tooltip.blueprintCopyBlocksInRange": ["count"],
 "ui.tooltip.deleteBoxTargetsInRange": ["count"],
 "ui.tooltip.deleteBoxSelectedInRange": ["count"],
 "ui.tooltip.deleteSelectedCount": ["count"],
 "ui.loading.serverConnected": ["elapsed"],
 "ui.loading.initialDataFetched": ["elapsed"],
 "ui.loading.blockAssetsLoaded": ["elapsed"],
 "ui.loading.itemImagesLoaded": ["elapsed"],
 "ui.loading.connectToolImagesLoaded": ["elapsed"],
 "ui.loading.fluidImagesLoaded": ["elapsed"],
 "ui.loading.blockScreenshotsCaptured": ["elapsed"],
 "ui.loading.trainCarScreenshotsCaptured": ["elapsed"],
 "ui.loading.terrainReady": ["chunkCount", "elapsed"],
 "ui.mainMenu.connectPortTooLarge": ["max"],
 "ui.mainMenu.connectPortTooSmall": ["min"],
 "ui.mainMenu.connectFailed": ["detail"],
}
path = sys.argv[1]
raw = open(path, encoding="utf-8", newline="").read()
lines = raw.split("\n")
seen, replaced = set(), 0
for i, line in enumerate(lines):
    key = line.split(",", 1)[0]
    if key not in M:
        continue
    seen.add(key)
    def sub(m):
        global replaced
        replaced += 1
        return "{" + M[key][int(m.group(1))] + "}"
    lines[i] = re.sub(r"\{p(\d+)\}", sub, line)
out = "\n".join(lines)
assert seen == set(M), set(M) - seen
assert not re.search(r"\{p\d+\}", out), "positional placeholder left"
open(path, "w", encoding="utf-8", newline="").write(out)
print("keys", len(seen), "replaced", replaced)
```

Run: `python3 -I <scratchpad>/rename_placeholders.py Localization/localization.csv`
Expected: `keys 32 replaced 190`。`git diff --stat Localization/localization.csv` が 32 行の変更だけ

- [ ] **Step 4: 生産者・運び手を名前付きにする**（対応表の名前を使う。形の例）

```csharp
// TooltipLine.cs
public readonly IReadOnlyList<LocalizationTextParam> TextParams;
public TooltipLine(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams) { Key = key; TextParams = textParams; }
public TooltipLine(LocalizationKey key) : this(key, Array.Empty<LocalizationTextParam>()) { }
// Equals は TextParams.SequenceEqual(other.TextParams)、GetHashCode は各要素の GetHashCode を Combine（今の形のまま要素型だけ替える）

// ConstructionMaterialShortageLine.cs
return new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage, new[]
{
    new LocalizationTextParam("itemName", itemName),
    new LocalizationTextParam("ownedCount", shortage.Held.ToString()),
    new LocalizationTextParam("requiredCount", shortage.Required.ToString()),
});

// BlockOpenInteractAction.cs
public IReadOnlyList<LocalizationTextParam> HintParams => new[] { new LocalizationTextParam("name", Localize.GetContent(ContentLocalizationKeys.BlockName(_blockGameObject.BlockMasterElement.BlockGuid))) };

// LoadingProgressLog.cs
public void AppendElapsed(LocalizationKey key)
{
    Append(Localize.GetFormatted(key, new[] { ElapsedParam() }));
}

// 先頭に値を1つ足す行の経路
// Path for lines with one leading value
public void AppendElapsed(LocalizationKey key, LocalizationTextParam leadingParam)
{
    Append(Localize.GetFormatted(key, new[] { leadingParam, ElapsedParam() }));
}

private LocalizationTextParam ElapsedParam()
{
    return new LocalizationTextParam("elapsed", _loadingStopwatch.Elapsed.ToString());
}

// InitializeScenePipeline.cs:181（レーン B。この 1 行だけ）
loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.TerrainReady, new LocalizationTextParam("chunkCount", fetchedChunkCount.ToString()));

// RemoteConnectionDenyReason.cs
public readonly IReadOnlyList<LocalizationTextParam> TextParams;
public RemoteConnectionDenyReason(LocalizationKey key) { Key = key; TextParams = Array.Empty<LocalizationTextParam>(); }
public RemoteConnectionDenyReason(LocalizationKey key, LocalizationTextParam textParam) { Key = key; TextParams = new[] { textParam }; }
// InitializeProprieties.cs:66 は new LocalizationTextParam("max", ...)、:72 は ("min", ...)

// ConnectServer.cs:65
Localize.GetFormatted(LocalizationKeys.Ui.MainMenu.ConnectFailed, new[] { new LocalizationTextParam("detail", failureDetail) })
```

`MiningFocusState.ShowEarnItemNamed` は名前付きキーには `name`（＋`NamedRequiredItems` は `items`）、名前なしキー（`unnamedKey`）は今渡している値を対応表の名前で渡す。`ElectricWireFeedbackLines`・`BlueprintCopyFeedbackLines`・`DeleteObjectService`・`DeleteBoxIntentView` は `count`。`PlacementFeedbackTooltipPresenter.cs:71-72` は要素型が変わるだけ。`TrainCarInteractActions` は `Array.Empty<LocalizationTextParam>()`。

- [ ] **Step 5: テストを直し、補間器のテストを足す**

`LocalizeGetFormattedTest.cs` を次に替える:

```csharp
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizeGetFormattedTest
    {
        [Test]
        public void GetFormattedFillsNamedParams()
        {
            Localize.Initialize();

            // 名前付きの枠を埋める
            // Fill a named placeholder
            var text = Localize.GetFormatted(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { new LocalizationTextParam("count", "3") });

            StringAssert.Contains("3", text);
            StringAssert.DoesNotContain("{count}", text);
        }

        [Test]
        public void GetFormattedKeepsAndReportsPlaceholderWithoutValue()
        {
            Localize.Initialize();

            // 値の無い枠は残し、開発者ログへ出す
            // A placeholder without a value stays and is reported to the developer log
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"ui\.loading\.terrainReady.*\{elapsed\}"));
            var text = Localize.GetFormatted(LocalizationKeys.Ui.Loading.TerrainReady, new[] { new LocalizationTextParam("chunkCount", "3") });

            StringAssert.Contains("3", text);
            StringAssert.Contains("{elapsed}", text);
        }
    }
}
```

`LocalizationTextInterpolatorTest.cs`（`Client.Localization/AssemblyInfo.cs` の `InternalsVisibleTo("Client.Tests")` で internal を直接呼べる）:

```csharp
using Client.Localization;
using NUnit.Framework;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizationTextInterpolatorTest
    {
        [Test]
        public void ValueContainingBracesIsNotReinterpolated()
        {
            // 差し込んだ値の中の{...}は再置換しない
            // Braces inside an inserted value are not interpolated again
            var text = LocalizationTextInterpolator.Interpolate("test.key", "{a}/{b}", new[]
            {
                new LocalizationTextParam("a", "{b}"),
                new LocalizationTextParam("b", "2"),
            });

            Assert.AreEqual("{b}/2", text);
        }
    }
}
```

その他のテストは、期待値の `new[] { "3" }` を `new[] { new LocalizationTextParam("<対応表の名前>", "3") }` に、`TextParams[0]` を `TextParams[0].Value` に替える（`git grep -n "TextParams\|HintParams\|GetFormatted(" -- moorestech_client/Assets/Scripts/Client.Tests` で全箇所を拾う）。

- [ ] **Step 6: コミットして push する**（コンパイルは Task 4 でまとめて行う。コミットメッセージに「未コンパイル」と書く）

```bash
git add -A moorestech_client/Assets/Scripts Localization/localization.csv
git commit -m "refactor(localization): 翻訳文の差し込みを名前付きに統一する（C#・CSV。未コンパイル）"
git push origin feature/uitk-s0-wave2
bd note moorestech-albof.7 "C#の補間器・CSV 32キー190か所・生産者を名前付きへ。InitializeScenePipeline.cs:181（B）を1行だけ書き換えた。コンパイルはEditor枠待ち"
```

## Task 2: 通知パケット・Web への JSON・Web の補間を名前付きにする — Editor 不要（vitest で確かめる）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Event/Notification/NotificationMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/Notification/AchievementNotificationWiring.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs:72`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteOperationHandler.cs:156`
- Modify: 空の `Array.Empty<string>()` を渡している呼び出し（`git grep -n "CreateOperationDenied(\|CreateAchievement(\|CreateAchievementWithItem(" -- '*.cs'` の全件。サーバー 18 か所・クライアント 3 か所・テスト）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/VanillaRemovalRestoreSender.cs`・`Client.Game/InGame/UI/UIState/State/DragDelete/Completion/DeleteResultFeedback.cs`・`Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCreateFailureNotifier.cs`
- Modify（レーン C の持ち物。JSON アダプタの型だけ）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs`・`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Notification/NotificationTopic.cs`
- Modify（レーン F の持ち物。追随だけ）: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/BlueprintCopyPasteScenarioChecks.cs:66`・`BlueprintPasteGhostProbe.cs:101`・`DeleteBoxScenarioInput.cs:83,91-92`
- Modify（テスト）: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/{MissingMasterPruneNotificationTest,NotificationServiceTest,AchievementNotificationWiringTest}.cs`・`BlueprintProtocolTest/BlueprintPasteProtocolTestContext.cs`・`ElectricWireAutoConnectPlaceTest/PlaceBlockNoAutoConnectWiringTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractNotificationTest.cs`・`Client.Tests/PlaceSystem/Blueprint/Copy/BlueprintCreateFailureNotifierTest.cs`・`Client.Tests/UIState/DragDelete/Completion/TrainCarRemovalRequestsTest.cs`
- Modify（Web）: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`・`src/shared/i18n/i18nStore.ts`・`src/shared/i18n/index.ts`・`src/shared/tooltip/CursorTooltip.tsx`・`src/features/notification/{notificationMessages.ts,notificationStore.ts,NotificationHost.tsx}`
- Modify（Web e2e。CI で回らないので壊れても気づけない）: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`（`textParams: []`→`{}`、`messageParams: ["<guid>"]`→`{ research: "<guid>" }` など対応表の名前）・`moorestech_web/webui/e2e/captures/capture-connection-line-notifications.ts:44`（`messageParams: ["3"]`→対応表の名前）。ほかに `git grep -n "textParams\|messageParams" -- moorestech_web/webui/e2e` が返すものも直す
- Modify（Web テスト）: `src/bridge/contract/schemas/__tests__/ui.test.ts`・`src/bridge/contract/validators.test.ts`・`src/bridge/store/topicEventDelivery.test.ts`・`src/features/notification/{notificationMessages.test.ts,notificationStore.test.ts}`・`src/shared/tooltip/CursorTooltip.test.ts`

**Interfaces:**
- Consumes: Task 1 の `LocalizationTextParam`・`TooltipLine.TextParams`
- Produces:
  - `NotificationMessagePack.MessageParams : Dictionary<string, string>`（`[Key(2)]` のまま）。factory の引数も `Dictionary<string, string> messageParams`。空は `new Dictionary<string, string>()`
  - `TooltipLineDto.TextParams : IReadOnlyDictionary<string, string>`（JSON では `{"count":"3"}`）
  - Web: `TooltipLineSchema.textParams: z.record(z.string(), z.string())`、通知 2 スキーマの `messageParams: z.record(z.string(), z.string())`、`GameNotification.messageParams: Record<string, string>`
  - Web: `buildPositionalInterpolationValues` は削除

- [ ] **Step 1: サーバーの通知パケットを名前付きにする**

```csharp
        [Key(2)] public Dictionary<string, string> MessageParams { get; set; }

        public static NotificationMessagePack CreateSaveMigrationPruned(int removedBlockCount, int emptiedItemStackCount, int removedResearchCount)
            => new(NotificationCategory.SaveMigration, "saveMigration.missingMasterPruned",
                new Dictionary<string, string>
                {
                    ["blockCount"] = removedBlockCount.ToString(),
                    ["itemStackCount"] = emptiedItemStackCount.ToString(),
                    ["researchCount"] = removedResearchCount.ToString(),
                },
                ItemMaster.EmptyItemId, 0);
```

`CreateItemEarned` は `new Dictionary<string, string>()`。呼び出し側: 研究完了は `["research"] = guid`、チャレンジ完了は `["challenge"] = guid`、`denied.undoRestoreSkipped`（サーバー `PlaceBlockProtocol.cs:72`・クライアント `VanillaRemovalRestoreSender.cs:44`）と `denied.blueprint.{reason}`（`BlueprintPasteOperationHandler.cs:156`）は `["count"]`、`DeleteResultFeedback.cs:30` は `["target"] = label`、残りは空の辞書。

- [ ] **Step 2: Web への JSON アダプタを辞書にする**

```csharp
// TooltipTopic.cs
Lines = presentation.Lines.Select(line => new TooltipLineDto
{
    TextKey = line.Key.Key,
    TextParams = line.TextParams.ToDictionary(textParam => textParam.Name, textParam => textParam.Value),
}).ToArray(),
...
public IReadOnlyDictionary<string, string> TextParams;

// NotificationTopic.cs:95
public Dictionary<string, string> MessageParams;
```

（`ToDictionary` は名前が重複すると例外になる。1 行に同じ名前を 2 回入れる生産者は無く、入れたら開発時に落ちて気づく形でよい）

- [ ] **Step 3: Client.Playtest の 3 本を追随させる**（レーン F。型の追随だけ）

```csharp
// BlueprintCopyPasteScenarioChecks.cs:66
return _tooltip.GetPresentation().Lines.Any(line => line.TextParams.Any(textParam => textParam.Name == "count" && textParam.Value == value));
// DeleteBoxScenarioInput.cs:91-92
if (line.TextParams != null && 0 < line.TextParams.Count && int.TryParse(line.TextParams[0].Value, out var count)) return count;
var actual = line.TextParams == null ? "<missing>" : string.Join(",", line.TextParams);
// DeleteBoxScenarioInput.cs:83 は string.Join のまま（ToString が name=value を返す）。Array.Empty<string>() は Array.Empty<LocalizationTextParam>() に
// BlueprintPasteGhostProbe.cs:101 は戻り値の要素型を IReadOnlyList<LocalizationTextParam> に合わせ、呼び出し側で .Value を読む
```

`BlueprintCopyPasteScenarioChecks.cs:66` の `value` が何の値か（ブロック数か）を読んで、`count` 以外ならその名前にする。

- [ ] **Step 4: Web を名前付きにする**

```ts
// ui.ts
// tooltipは辞書キーと名前付き補間値の行配列のみを受け取り、生の表示文字列も寸法値も受け付けない
// Tooltips accept only an array of lines (dictionary key + named interpolation values) — never raw display text, never sizes
export const TooltipLineSchema = z.object({
  textKey: z.string(),
  textParams: z.record(z.string(), z.string()),
}).strict();
// MessageNotificationSchema / ItemEarnedNotificationSchema の messageParams も z.record(z.string(), z.string())

// CursorTooltip.tsx
// ホストは行ごとにキーと名前付きの値だけを送るため、各行を辞書解決＋補間して表示文字列にする
// The host sends only a key and named values per line, so each line is dictionary-resolved and interpolated
export function resolveTooltipLines(
  data: VisibleTooltipData,
  translate: (key: TranslationKey, values: InterpolationValues) => string,
): string[] {
  return data.lines.map((line: TooltipLine) => translateExternalKey(line.textKey, translate, line.textParams));
}

// notificationMessages.ts
// Guidを運ぶ通知の、値の名前とcontentキー組み立て。サーバーは表示名でなくGuidを送る
// Value name and content-key builder for GUID-bearing notifications; the server sends GUIDs, not display names
const contentParamKeyBuilders = new Map<string, { name: string; buildKey: (guid: string) => TranslationKey }>([
  ["achievement.researchCompleted", { name: "research", buildKey: researchNameKey }],
  ["achievement.challengeCompleted", { name: "challenge", buildKey: challengeTitleKey }],
]);

export function resolveNotificationParams(
  messageId: string,
  messageParams: Record<string, string>,
  translate: (key: TranslationKey) => string,
): Record<string, string> {
  const builder = contentParamKeyBuilders.get(messageId);
  if (!builder) return messageParams;
  // 名前が欠けた通知は生の枠のまま出るので、開発時に気づけるよう警告する
  // A notification missing the named value shows a raw token, so warn so developers notice
  if (!Object.hasOwn(messageParams, builder.name)) {
    console.warn(`[notification] ${messageId}: missing param ${builder.name}`);
    return messageParams;
  }
  return { ...messageParams, [builder.name]: translate(builder.buildKey(messageParams[builder.name])) };
}

export function buildInterpolationValues(messageId: string, messageParams: Record<string, string>) {
  return { ...messageParams, messageId };
}
```

`i18nStore.ts` の `buildPositionalInterpolationValues` と、`index.ts` の再エクスポートを消す。「位置パラメータ」「{p0}」と書いた 2 行コメント（`ui.ts:132-133`・`i18nStore.ts:56-57`・`CursorTooltip.tsx:48`、C# の `TooltipLine.cs` の summary・`LocalizationTextInterpolator.cs` の冒頭）も名前付きの説明に直す。`notificationStore.ts:12-13` の `messageParams: string[]` を `Record<string, string>` に。`NotificationHost.tsx` は型が通れば変更不要。

- [ ] **Step 5: Web のテストを直して回す**

テストの期待値の `textParams: ["3"]` は `textParams: { count: "3" }`、`p0: "2"` は対応表の名前（例: `count: "2"`）、`messageParams: ["a"]` は `{ x: "a" }` に替える。`ui.test.ts` に「`textParams` に配列を渡すと弾く」1 件を足す:

```ts
it("rejects positional textParams arrays", () => {
  expect(TooltipLineSchema.safeParse({ textKey: "ui.tooltip.placeWireCost", textParams: ["3"] }).success).toBe(false);
});
```

Run: `cd moorestech_web/webui && PATH="$PWD/../node/mac-arm64/bin:$PATH" pnpm test && PATH="$PWD/../node/mac-arm64/bin:$PATH" npx tsc --noEmit -p tsconfig.json && PATH="$PWD/../node/mac-arm64/bin:$PATH" npx tsc --noEmit -p e2e/tsconfig.json && PATH="$PWD/../node/mac-arm64/bin:$PATH" pnpm lint`。続けて `pnpm test:e2e` を回す（playwright のブラウザが無くて起動できない場合は、tsc の e2e 検査が緑であることと、起動できなかった理由を pr-body の検証節に書く）
Expected: すべて緑。`git grep -nE "buildPositionalInterpolationValues|\{p[0-9]|\bp0\b" -- moorestech_web/webui/src moorestech_web/webui/e2e` が 0 件

- [ ] **Step 6: コミットして push する**

```bash
git add -A moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts moorestech_web/webui/src
git commit -m "refactor(notification,tooltip): 通知パケットとWebへの差し込み値を名前付きにする（C#は未コンパイル）"
git push origin feature/uitk-s0-wave2
bd note moorestech-albof.7 "通知パケットMessageParamsを辞書に、WebのtextParams/messageParamsをrecordに。C(TooltipTopic/NotificationTopic)とF(Client.Playtest 3本)を型の追随だけ書き換えた。vitest緑"
```

## Task 3: パッケージの撤去と UI Test Framework の導入（manifest・asmdef）— Editor 不要で書き、解決は Task 4

**Files:**
- Modify: `moorestech_client/Packages/manifest.json`
- Modify: `moorestech_client/Packages/packages-lock.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef`

- [ ] **Step 1: manifest を直す** — `"com.annulusgames.debug-ui": ...`（3 行目）と `"com.singtaa.onejs": ...`（13 行目）の行を消し、`"com.unity.ui.test-framework": "1.0.0",` をアルファベット順の位置へ足す（`com.unity.test-framework` の後）
- [ ] **Step 2: lock から 2 パッケージのブロックを消す** — `"com.annulusgames.debug-ui": { ... }` と `"com.singtaa.onejs": { ... }` のブロックを丸ごと削る（他パッケージの `dependencies` に両者が出ていないことを `grep -n "debug-ui\|onejs" moorestech_client/Packages/packages-lock.json` で確かめる。0 件になること）。`com.unity.ui.test-framework` の lock 項目は Task 4 で Unity が書く
- [ ] **Step 3: `Tests.asmdef` の `references` に 2 行足す** — `"Unity.InputSystem.TestFramework",` の次に `"Unity.UI.TestFramework.Runtime",` と `"Unity.UI.TestFramework.Editor",`
- [ ] **Step 4: コミットして push する**

```bash
git add moorestech_client/Packages/manifest.json moorestech_client/Packages/packages-lock.json moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef
git commit -m "chore(packages): OneJSとDebugUIを外し、UI Test Frameworkを入れる（解決はEditor起動時）"
git push origin feature/uitk-s0-wave2
bd note moorestech-albof.5 "manifest/lockからonejs・debug-uiを削除、ui.test-frameworkを追加（未解決）"
```

## Task 4: Editor を起動してコンパイル・テストを通す（Task 1〜3 の検証）

**前提:** `pgrep -fl "Unity.app/Contents/MacOS/Unity -projectPath"` が 2 本未満で、B（uitk-s0-remote-exec）が枠を使い終えていること（Global Constraints）。

**Files:** Task 1〜3 で触ったファイル（エラーの修正のみ）、`moorestech_client/Packages/packages-lock.json`（Unity が書く `com.unity.ui.test-framework` 項目）

- [ ] **Step 1: Editor を起動し PID を控える**

Run: `uloop launch ./moorestech_client` → `pgrep -fl "uitk-s0-wave2/moorestech_client"` で PID を控え、`bd note moorestech-albof.5 "Editor PID <PID>"`

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。エラーが出たら Task 1・2 の Files 内で直す（型の追随漏れ）。Files 外のファイルで出たら、追随が要るのはどれかを bd note に書いて直す

- [ ] **Step 3: UI Test Framework の参照を確かめる**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code 'var a = System.Array.Find(UnityEditor.Compilation.CompilationPipeline.GetAssemblies(), x => x.name == "Client.Tests"); var names = new System.Collections.Generic.List<string>(); foreach (var r in a.assemblyReferences) if (r.name.StartsWith("Unity.UI.TestFramework")) names.Add(r.name); return string.Join(",", names);'`
Expected: `Unity.UI.TestFramework.Runtime,Unity.UI.TestFramework.Editor`（順不同）。`git diff moorestech_client/Packages/packages-lock.json` に `com.unity.ui.test-framework` の項目が入っている

- [ ] **Step 4: テストを回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Localiz|Tooltip|Notification|LoadingProgressLog|InitializeProprieties|PlaceSystem|DragDelete|Mining|WireContract|Blueprint|Interact" --timeout-seconds 1500`
Expected: Failed 0。`LocalizationPlaceholderParityTest` と `LocalizeGetFormattedTest` を含む

- [ ] **Step 5: Error ログを確かめ、コミットして push する**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error` → 0 件（テストが `LogAssert.Expect` で拾ったものを除く）

```bash
git add -A moorestech_client moorestech_server
git commit -m "fix: 名前付き差し込みとパッケージ変更をコンパイル・テストで通す"
git push origin feature/uitk-s0-wave2
bd note moorestech-albof.7 "コンパイル0・関連テスト緑（件数を書く）"
```

## Task 5: 旧 SkitUI を削除する（0-12）

**Files:**
- Modify（uloop 経由）: `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab`・`moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab`・`moorestech_client/Assets/Scenes/Other/SkitTest.unity`・`moorestech_client/Assets/Scenes/Other/StoryTest.unity`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs`・`moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`・`moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameContainerActivation.cs`・`moorestech_client/Assets/Scripts/Client.DebugSystem/Skit/SkitTester.cs`・`moorestech_client/Assets/Scripts/Client.Skit/Context/StoryContextExtension.cs`・`moorestech_client/Assets/Scripts/Client.Skit/Commands/{TextCommand,SelectionCommand,TransitionCommand,ShowTextCommand}.cs`
- Modify（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitFailureCleanupTest.cs`
- Delete: `moorestech_client/Assets/Asset/Skit/SkitUI.prefab`・`moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUI.cs`・`moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUITools.cs`・`moorestech_client/Assets/Scripts/Client.Skit/UI/SkitPresentationMode.cs`・`moorestech_client/Assets/Asset/UI/NewUI/Skit/`（フォルダ全体。中の png・uxml・uss・`SkitPanelSettings.asset` は SkitUI からしか参照されていない。2026-10-26 に guid で確認済み）と各 `.meta`

- [ ] **Step 1: prefab とシーンから SkitUI の実体を外す**（Editor 上で。YAML を手で書き換えない）

```csharp
// uloop execute-dynamic-code で 1 回ずつ実行する
// 1) MainGameUI.prefab の中の SkitUI プレハブインスタンスを消す
var path = "Assets/Asset/UI/Prefab/MainGameUI.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var skit = root.GetComponentInChildren<Client.Skit.UI.SkitUI>(true);
var name = skit.gameObject.name;
UnityEngine.Object.DestroyImmediate(skit.gameObject);
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
return "removed " + name;
```

```csharp
// 2) SkitTest.unity と StoryTest.unity のシーン上の SkitUI を消す（シーンごとに）
var scenePath = "Assets/Scenes/Other/SkitTest.unity";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
var found = UnityEngine.Object.FindObjectsByType<Client.Skit.UI.SkitUI>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
foreach (var ui in found) UnityEngine.Object.DestroyImmediate(UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(ui.gameObject) ?? ui.gameObject);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "removed " + found.Length;
```

（SkitUI がプレハブインスタンスの子なら、`GetOutermostPrefabInstanceRoot` が SkitUI.prefab 自身のインスタンスかを確かめてから消す。別のプレハブの中なら、そのプレハブを 1) と同じ手で直す。PlayMode 中は実行しない）

- [ ] **Step 2: コードから SkitUI と `SkitPresentationMode` を消す**
  - `SkitManager.cs`: `[SerializeField] private SkitUI skitUI;`（28 行目）、`Awake` の `skitUI.SetActive(false)`、161 行目付近の `skitUI.SetActive(false)` とその 2 行コメント、`builder.RegisterInstance(skitUI)`（177 行目）、`builder.RegisterInstance(new SkitPresentationMode(true))` とその 2 行コメント（195-197 行目）、`Cleanup()` の `skitUI.SetActive(false)`。空になった `Awake` は消す
  - `MainGameStarter.cs`: 88 行目のフィールドと 143 行目の `builder.RegisterComponent(skitUI)`。`MainGameContainerActivation.cs:31` の `resolver.Resolve<SkitUI>()`。`SkitTester.cs` の 26 行目のフィールドと 46 行目の登録。`StoryContextExtension.cs:13` の `GetSkitUI`
  - コマンドは Web 経路だけを残す（`TransitionCommand` の例。他の 3 本も同じ形で `if (mode.WebUiEnabled)` の中身だけにする）:

```csharp
            var transitionWait = isSkip ? 0 : Duration;
            SkitPresentationStateStore.Instance.SetTransitionVisible(Enabled);
            await UniTask.Delay((int)(transitionWait * 1000), isSkip);
            return null;
```

  （`duration` の計算は今の形のまま残してよい。上は形の例）。`TextCommand` は `return await ExecuteWebPresentationAsync(...)` だけにし、文字送り・`Input.GetMouseButtonDown` の待ち・`TextDuration`/`WaitDuration` など非 Web 側でしか使わない定数とローカル関数を消す。`SelectionCommand` は `store.PresentChoices` → `WaitForSelectionAsync` → `ResolveSelectedIndex` だけを残す
  - `SkitFailureCleanupTest.cs`: SkitUI の GameObject を作る 3 行・`SetPrivateField(manager, "skitUI", ...)`・`Assert.IsFalse(skitUiObject.activeSelf)` を消す（失敗時の後始末の検証は残す）

- [ ] **Step 3: ファイルを消す**

```bash
git rm -r moorestech_client/Assets/Asset/Skit/SkitUI.prefab moorestech_client/Assets/Asset/Skit/SkitUI.prefab.meta \
  moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUI.cs moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUI.cs.meta \
  moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUITools.cs moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUITools.cs.meta \
  moorestech_client/Assets/Scripts/Client.Skit/UI/SkitPresentationMode.cs moorestech_client/Assets/Scripts/Client.Skit/UI/SkitPresentationMode.cs.meta \
  moorestech_client/Assets/Asset/UI/NewUI/Skit moorestech_client/Assets/Asset/UI/NewUI/Skit.meta
```

- [ ] **Step 4: コンパイルし、残った参照を Unity に掃除させる**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0。続けて execute-dynamic-code で `UnityEditor.AssetDatabase.ForceReserializeAssets(new[] { "Assets/Asset/Common/Prefab/GameSystem.prefab", "Assets/Asset/UI/Prefab/MainGameUI.prefab", "Assets/Scenes/Other/SkitTest.unity", "Assets/Scenes/Other/StoryTest.unity", "Assets/Scenes/Game/MainGame.unity" }); return "ok";`

Run: `grep -rl "9525eda8e634954499da896e6c682cbd\|01c05861d09e4b2f9dc6d66ceccd97dd\|b4094a4383afd4170be17cb074b9269c" moorestech_client/Assets --include='*.prefab' --include='*.unity' --include='*.asset'`
Expected: 0 件。`git diff --stat` の prefab・シーンの変更が SkitUI の除去とフィールド行の削除に限られる（無関係な大量差分が出たら、その再シリアライズ分を `git checkout -- <file>` で戻し、該当ファイルだけ再保存する）

- [ ] **Step 5: テストを回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Skit" --timeout-seconds 1200`
Expected: Failed 0。`uloop get-logs --project-path ./moorestech_client --log-type Error` が 0 件

- [ ] **Step 6: コミットして push し、bd を閉じる**

```bash
git add -A moorestech_client
git commit -m "refactor(skit): 旧UITKのSkitUIとSkitPresentationModeを削除しWeb経路だけにする"
git push origin feature/uitk-s0-wave2
bd close moorestech-lnsf.8 --reason "albof.5で削除（.decisions/2026-10-26-旧UITKのSkitUIは削除し会話UIの予備を持たない.md）"
bd note moorestech-albof.5 "SkitUI・SkitPresentationMode・NewUI/Skit資産を削除。prefab/シーンはuloopで除去。Skitテスト緑"
```

## Task 6: unityプレイ録画テストで表示を確かめる

**Files:** なし（結果は `../moorestech_logs` かスキルの既定の出力先。repo にはコミットしない）

- [ ] **Step 1: 録画シナリオを回す**（`unity-playmode-recorded-playtest` スキルの `scripts/run-scenario.sh`。Editor は自分の 1 本を使う。レーン F が DSL を改名中でも、このブランチのシナリオはこのブランチの DSL で動く）

対象（R3・R6）:
- `misc/skit-opening-world-hidden.cs`・`misc/skit-pause-menu-esc.cs`（スキットの Web 経路が生きている）
- `building/placement-reason-tooltip.cs`（`placeMaterialShortage`・`placeWireCost` のツールチップ）
- `misc/interact-key-unification.cs`（`interactOpenBlock`・`namedMineClick`）
- `building/delete-tool-box-select-hold-confirm.cs`（`deleteSelectedCount`・`deleteBoxTargetsInRange`）
- `server-notification-smoke.cs`（通知）

Expected: すべて合格。各録画のツールチップ・通知が出ているフレームのスクショ（シナリオの成果物、無ければ `uloop screenshot`）で、文言に `{` が残らず値が入っている。ロード画面のログは PlayMode 開始直後のスクショで同じく確かめる。既知の壊れたシナリオ（bd `moorestech-wwxy`）に当たったら対象外として記録する

- [ ] **Step 2: 結果を記録する**

```bash
bd note moorestech-albof.7 "録画: <シナリオ名と合否>。ツールチップ・通知・ロードログに生の{name}無し（スクショのパス）"
bd note moorestech-albof.5 "録画: skit 2本の合否"
```

## Task 7: Windows ビルドで quickjs が消えたことを確かめる（R1）

**前提:** 自分の Editor を `kill <PID>` で止めてから行う（batchmode も Unity 1 本に数える）。`pgrep` で 2 本未満を確かめる。

**Files:** なし（ログと成果物はスクラッチに置く）

- [ ] **Step 1: 配布工程と同じ口で Windows を焼く**（Steam へは上げない）

`scripts/playtest/release-playtest.sh` の `release_build_player` と同じ呼び方にする。`UNITY_BIN` と `MOORESTECH_MASTER_DATA_ROOT` の決め方は `scripts/playtest/lib/` の `release_resolve_master_data_root` を読んで合わせる（master データはこの worktree の互換ピンの worktree を使う。`moores-wt status` が示す `pin-<commit8>`）。

```bash
OUT=<scratchpad>/wave2-build
MOORESTECH_BUILD_OUTPUT="$OUT/build-windows" MOORESTECH_STEAM_BUILD_LABEL="wave2-local" MOORESTECH_BUILD_BRANCH="feature/uitk-s0-wave2" \
MOORESTECH_MASTER_DATA_ROOT="<pin worktree>" \
"<UNITY_BIN>" -batchmode -nographics -projectPath "$PWD/moorestech_client" \
  -executeMethod Client.Editor.Build.SteamPlaytestBuildCli.WindowsSteamPlaytestBuild -logFile "$OUT/unity-build-windows.log"
```

（前景で実行し、Bash の timeout は 600000ms。超えそうなら同じコマンドを `timeout` 付きで分けず、終わるまで前景の呼び出しを繰り返し確認する — ログ末尾の `Build Finished`/`Exiting batchmode` を見る）

- [ ] **Step 2: Plugins/x86_64 を基準と突き合わせる**

```bash
BASE=$PLAYTEST_RUNS/playtest-20261021-2150/unity-build-windows.log
grep -o "Plugins/x86_64/[^ ]*" "$BASE" | sort -u > $OUT/base-plugins.txt
ls "$OUT/build-windows/moorestech_Data/Plugins/x86_64" | sed 's#^#Plugins/x86_64/#' | sort -u > $OUT/new-plugins.txt
diff $OUT/base-plugins.txt $OUT/new-plugins.txt
grep -c "quickjs" "$OUT/unity-build-windows.log"
```

Expected: `diff` の差分は `< Plugins/x86_64/quickjs_unity.dll` の 1 行と、理由を説明できる差分だけ（基準は 10-21 の master ビルドなので、統合ブランチの他レーンや master の変更による差分はあり得る）。`quickjs` の出現が 0。差分が他にあれば、その理由（本ブランチ由来か、10-21 以降の master の変更か）を `git log 4700dd77ad --since=2026-10-21 -- moorestech_client/Packages moorestech_client/Assets/Plugins` で調べて pr-body の検証節に書く

- [ ] **Step 3: winbuild で起動を確かめる**（できる範囲で。Steam は使わない）

`ssh winbuild` へ `scp -r $OUT/build-windows winbuild:<一時フォルダ>` で置き、起動して Player.log に `DllNotFoundException`・`quickjs`・`OneJS` が無いことを見る。ssh からの起動手順が `scripts/playtest/windows/` に無く成り立たない場合は、試した内容と止まった理由を pr-body の「省いた検証」に書いて先へ進む。終わったら一時フォルダを消す

- [ ] **Step 4: 記録する**

```bash
bd note moorestech-albof.5 "Windowsビルド: Plugins/x86_64の差分はquickjs_unity.dllの削除1件のみ（diff結果）。winbuild起動: <結果>"
```

## Task 8: moores-code-review でブランチ全体をレビューする（必須・省略不可）

- [ ] **Step 1:** moores-code-review スキルを `origin/feature/uitk-stage0...HEAD` に対して実行する。**Claude Code ホストの既定どおり Workflow で回す。③ Codex 外部監査は省く**（理由: Codex の週次枠が 100% で、使うとクレジットの課金になる。`.decisions/2026-10-26-UITK段階0でCodexで進められない実装はClaude-Codeで進める.md`。独立レビューは統合 PR に 1 回だけ行う）。省いた系統と理由を pr-body の「省いた系統」に書く
- [ ] **Step 2:** 機械的な指摘はスキルの規定どおり適用し、設計判断はユーザーに聞かず推奨案で決めて `## 判断記録（ADR）` に「agent 既定（ユーザー未裁定）」で追記し、pr-body の「ユーザーに見てほしい判断」に並べる
- [ ] **Step 3:** 指摘の反映がツールチップ・通知・スキットの判定経路や条件式に触れたら、Task 6 の該当シナリオを反映後のバイナリで回し直す
- [ ] **Step 4:** コンパイル・関連テスト・vitest を回し直し、コミットして push する

## Task 9: 統合ブランチを取り込み、push し、本文の下書きを書く（必須。PR は作らない）

- [ ] **Step 1: 取り込む**

```bash
git fetch origin
git merge --no-ff origin/feature/uitk-stage0 -m "Merge branch 'feature/uitk-stage0' into feature/uitk-s0-wave2"
```

衝突は「両方の意図を残す」で解く（他レーンの変更を捨てない）。他レーンが `TooltipLine`・`GetFormatted`・`NotificationMessagePack` を位置指定で新しく使っていたら名前付きへ直す（`git grep -n "GetFormatted(\|new TooltipLine(\|CreateOperationDenied(" -- '*.cs'` と `git grep -n "{p[0-9]" Localization/localization.csv` で確かめる）

- [ ] **Step 2: コンパイルとテスト**（Editor 枠を確かめてから）`uloop compile` → ErrorCount 0、Task 4 Step 4 と Task 5 Step 5 の正規表現でテスト → Failed 0、webui の `pnpm test`・`tsc --noEmit` → 緑。終わったら自分の Editor を `kill <PID>` で止める
- [ ] **Step 3: push する** — `git push origin feature/uitk-s0-wave2`。`git status` がクリーンで、`git log origin/feature/uitk-s0-wave2 -1` が HEAD と一致することを確かめる
- [ ] **Step 4: `.superpowers/sdd/pr-body.md` を書く**（統合 PR #1559 に載せる節。コミットはしない — `.superpowers/` は作業用）

```markdown
## 第2波: 撤去と差し込み記法の統一（albof.5・albof.7）
### 変更内容
- 0-1 / 0-12 / 0-6 / UI Test Framework の導入（それぞれ 1〜3 行）
### 検証
- コンパイル・テスト（件数）・vitest・録画（シナリオ名と合否・スクショのパス）・Windows ビルドの Plugins 差分（diff の結果）・winbuild 起動（結果）
### ユーザーに見てほしい判断
- （判断記録 D1〜D6 と、レビューで agent 既定にしたもの）
### 記録の更新
- `.decisions/2026-08-21-カーソルtooltip契約…` の契約は `textParams` が名前付きの record になった（10-26 裁定の帰結。新しい裁定ファイルは作らず本文に書く）
### 省いた系統
- moores-code-review の ③ Codex 外部監査（Codex の週次枠 100%・課金になるため。独立レビューは統合 PR で 1 回）
- （winbuild 起動を省いた場合はその理由）
### レーン境界で触ったファイル
- B: InitializeScenePipeline.cs:181 / C: TooltipTopic.cs・NotificationTopic.cs / F: Client.Playtest の 3 本
```

- [ ] **Step 5:** `bd note` を両方へ書き、`bd close moorestech-albof.5 --reason "..."`・`bd close moorestech-albof.7 --reason "..."` で閉じる。worktree は消さない（統合のマージまで残す）

---

## 判断記録（ADR）

設計の出所: `docs/uitk-migration/plan.md` §2.1（0-1・0-6・0-12）・§4.3「導入」、`.decisions/2026-10-26-旧UITKのSkitUIは削除し会話UIの予備を持たない.md`、`.decisions/2026-10-26-翻訳文の差し込みは名前付きに統一しGetFormattedも名前付きに改める.md`、`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md`、`.decisions/2026-10-26-UITK段階0でCodexで進められない実装はClaude-Codeで進める.md`、ADR 0052。

| # | 判断 | 出所 |
|---|---|---|
| D1 | 差し込みの値は C# では `LocalizationTextParam`（名前と値の組）の順序付きリストで運ぶ。辞書にしないのは `TooltipLine` の等値・ハッシュを今の形（`SequenceEqual`）のまま保つため | agent 既定（ユーザー未裁定） |
| D2 | 補間器は 1 回の走査で埋め、値の中の `{...}` を再置換しない。埋まらない枠は残して `Debug.LogError`（AGENTS.md「無音の縮退は禁止」）。Web の補間と同じ意味にそろえる | agent 既定（ユーザー未裁定） |
| D3 | 通知パケット（サーバー→クライアント）と Web への JSON も名前付きにする。位置指定のまま Web 側で名前を割り当てる案は、名前の対応表が送り手と別の場所に 2 つ目としてでき、統一の裁定の趣旨（書き方を 1 つにする）に反するので棄却。Event パケットはセーブに入らないので、セーブのマイグレーションは要らない | agent 既定（ユーザー未裁定） |
| D3a | サーバー側の改修範囲: `NotificationMessagePack.cs`（`MessageParams` を `Dictionary<string,string>` に）、`AchievementNotificationWiring.cs`（研究・チャレンジ完了を `research`・`challenge` の名前で送る）、`PlaceBlockProtocol.cs:72`（`undoRestoreSkipped` を `count`）、`BlueprintPasteOperationHandler.cs:156`（`denied.blueprint.*` を `count`）。どれも送る値の名前を足すだけで、状態・判定・同期の 3 点セットには触れない | agent 既定（ユーザー未裁定）・D3 の帰結 |
| D4 | 名前は Global Constraints の対応表で決めた。既存の名前付きキーで使われている名前（`count`・`itemName`・`ownedCount`・`requiredCount`・`name`）を優先して再利用した | agent 既定（ユーザー未裁定） |
| D5 | 通知の値は `Dictionary<string,string>` で、名前の取り違えを型で閉じない（Phase 2.6 弱）。補間器のエラーログ（C# 側）と録画のスクショ（Web 側）で気づく。メッセージごとの型を作るのは移行計画 §7.3 の「型付きの書式メソッドを作らない」裁定の趣旨に合わないため見送る | agent 既定（ユーザー未裁定） |
| D6 | `LocalizationTextParam.ToString()` を `name=value` にし、F レーンの録画シナリオ（`placement-reason-tooltip.cs`・`terrain-default-y-poles-and-pier-via-ui.cs` の `string.Join`）を書き換えずに済ませる | agent 既定（ユーザー未裁定） |
| D7 | 作業順は「Editor 不要の Task 1〜3 → Editor 枠が空いてから Task 4 以降」。Editor はマシン全体で 2 本まで、次の枠は B が先 | ユーザー裁定 2026-10-26 13:24「2本で」（指揮セッション moorestech-f2 経由） |
| D8 | `.decisions/2026-08-21` のツールチップ契約（`lines[{textKey,textParams}]`）は形を保ち、`textParams` の中身だけを配列から名前付きの record に替える。08-26 の「GetFormatted のシグネチャは変えない」は 10-26 裁定で改められている | 10-26 裁定の帰結 |
| D9 | ランタイム挙動（ツールチップ・通知・ロード画面・スキット）に触れるので、unityプレイ録画テスト（Task 6）を入れる | moores-writing-plans の必須規則 |
| D10 | moores-code-review は Workflow で回し、③ Codex 外部監査は省く。PR は作らず、統合ブランチを取り込んで push し、本文の下書きを `.superpowers/sdd/pr-body.md` に書く | ユーザー指示 2026-10-26（本 plan の依頼文）・`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md` |

| D11 | `LocalizationTextParam` は Interface 層へ切り出さず `Client.Localization` に置く。運ぶのはクライアント内（Client.Game・Client.Starter・Client.WebUiHost・Client.Playtest はすでに Client.Localization を参照）だけで、サーバーは `Dictionary<string,string>` で別に持つ（Phase 2.6 弱 5-G） | agent 既定（ユーザー未裁定） |
| D12 | Web の通知で、内容キーに引き直す値（`research`・`challenge`）の名前が欠けていたら `console.warn` を出して素通しする（Phase 2.6 弱 5-C）。console は開発時にしか見えないが、画面に生の `{research}` が出るのでテスターの報告でも気づける | agent 既定（ユーザー未裁定） |
| D13 | 翻訳が欠けたときの扱い（`translate`・`translateExternalKey` の戻り値をそのまま表示に使う）は今の Web の i18n の規約のまま変えない（Phase 2.6 弱 5-I。本 plan は名前の置換だけ） | agent 既定（ユーザー未裁定） |
| D14 | スキットのコマンドのキャンセル経路と `UniTask.Delay(ms, isSkip)`（第 2 引数は ignoreTimeScale）は既存のまま触らない。非 Web 分岐を削るだけの純削除にとどめる（Phase 2.6 弱 5-F） | agent 既定（ユーザー未裁定） |
| D16 | user-simulator の指摘（e2e モックの追随・テストの波及 21 件と `MiningFocusStateEarnItemNameTest`・コメントの直し・08-21 契約の記述・Windows の基準差分の書き方）を plan に反映した | シミュレーター予測（適用済み・ユーザー未確認） |
| D15 | `LocalizationTextParam.ToString()` はログ表示用。テスト専用の public ではなく、ログ・デバッガ表示にも効くので残す（設計検査の注目点） | agent 既定（ユーザー未裁定） |

### 本PR外のリファクタ提案（第3バケツ）

なし。
