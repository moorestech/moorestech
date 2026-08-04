---
spec: docs/superpowers/specs/2026-07-29-localization-foundation-design.md
---

# ローカライズ基盤 独立レビュー是正 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** PR #1111 独立レビューのCritical 22グループを解消し、設計判断11件のユーザー裁定（2026-08-02・ダイジェストコメント5件）をコードへ反映する。

**Architecture:** 裁定の核は3つ。(1) マスタ由来表示名は「宣言表→C#/TS両側生成の型付きGuid導出キー」へ一本化し（D3+D7）、connectTool（D1案A）とfluid（D6案A）をWeb側辞書解決へ揃える。(2) Web側は辞書ロードのstatusを表示まで通す（D2案A）。(3) Skitローカライズはresolverの同期機構を前例（boolフラグ+revision int）へ簡素化し、死引数・並行運搬経路・SkitCleanupOnceを畳む（D9案B）。残りは機械的修正（購読化・境界移動・定数化・barrel・テスト再編）。

**Tech Stack:** Unity C#（UniRx/UniTask/asmdef）・Roslyn SourceGenerator（mooresmaster）・React+TS+zod（moorestech_web/webui）・NUnit・Vitest。

## Global Constraints

- 後方互換性・パフォーマンス最適化・将来の拡張性は考慮不要（AGENTS.md）。フォールバック散布・optional化は禁止、必須化+全呼び出し側一括更新が正規手順
- `Func<>`禁止・`partial`禁止・デフォルト引数禁止・単純getter/setter禁止（Setは`public void SetHoge`）・1ファイル200行以下（テストは適用外）・1ディレクトリ10ファイルまで（テストは適用外・ユーザー裁定2026-07-28）
- try-catchは外部境界（外部プロセス・ネットワーク送受信・外部入力JSON/CSVパース）の隔離のみ可・境界根拠コメント必須
- イベントはUniRx（`Subject<T>`）。状態変化の検知は購読で行い、`Update()`は物理進行専用
- `#region Internal`はメソッド内ローカル関数集約専用。単一呼び出し元のprivateヘルパーは呼び出し元メソッド末尾の単一`#region Internal`内ローカル関数へ移す（reviewer core-cs-region-internal基準4）
- .metaは手動作成禁止（ファイル移動は.cs+.metaペアで行うかUnity経由）。Prefab/シーン等Unity YAMLの直接編集禁止
- mooresmaster generator/共通CSV DLLを変更したら `mooresmaster/build.sh` でclient/server両方のDLLを再ビルドしコミットする（ADR 0005帰結）
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` でエラー0を確認。テストは `--filter-type regex` で限定実行
- webui変更後は `cd moorestech_web/webui && npx tsc -b && npm run lint && npm test` を通す
- 作業ブランチ: `feature/localization-foundation`（worktree: `/Users/katsumi/moorestech/.worktrees/localization-foundation`）。タスクごとにコミットする

**裁定の対応表（2026-08-02ダイジェストコメント→本plan）:**
| 裁定 | 内容 | 反映タスク |
|---|---|---|
| D1=案A | connectTool表示名はWeb解決統一（Guidのみ送出） | Task 10 |
| D2=案A(推奨) | 辞書ロード失敗はstatus消費でUI表示 | Task 12 |
| D3=案A(推奨) | Guid導出キーは宣言表からC#/TS両側生成 | Task 8 |
| D4=案B(推奨) | Tooltip契約はキー+textParams専用化 | Task 11 |
| D5〜D11=各推奨 | push形/fluid最小/型付き/source型分離/SkitCleanupOnce廃止/buildMenuGrouping集約/関心別分割 | Task 14/9/8/13/15/10/17 |

**planスコープ外（裁定が無いWarning群・別PR推奨）:** ServerCommunicator終了レース修正の分離・ModJsonStringLoader順序変更の周知・SchemaWatch同梱の分離・detailLogic/researchLogicのuGUI同文言二重出所・isItemMasterDataのzod一本化・CSVパーサBOM対応・LocalizationDictionaryEndpointの400/404テスト・skit表示中の言語切替非追従。これらはWarningとして記録済み（`/tmp/pr-review-1111/index.html` 折りたたみ参考）であり、本planでは触らない。

---

### Task 1: 機械的小修正バッチ（C11・C13・C14・C15・asmdef 2件）

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/localization/transport.ts:13`
- Create: `moorestech_web/webui/src/features/settings/index.ts`
- Modify: `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.tsx:5`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuCategoryGrid.tsx:21-27`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Localization/SkitCommandLocalization.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Skit/Localization/SkitLocalizationDictionaryLoader.cs:67`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Client.Skit.asmdef`（`Core.Master`参照削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Client.Localization.asmdef`（`overrideReferences: true`へ）

**Interfaces:**
- Produces: `SkitCommandLocalization.KeyPrefix`（`public const string KeyPrefix = "skit.";`）— Task 15も参照する

- [ ] **Step 1: e2e mockのURL定数化（C11）** — `transport.ts` 先頭に `import { localizationLanguagesUrl } from "../../../src/bridge/transport/httpEndpoints";`（既存mock兄弟 `e2e/mock-host/httpHandler.ts` のimport形式に合わせる）を足し、`if (url !== "/api/i18n-languages")` を `if (url !== localizationLanguagesUrl)` に置換
- [ ] **Step 2: settingsのbarrel新設（C14）** — `src/features/settings/index.ts` を新規作成:

```ts
export { LanguageSelect } from "./LanguageSelect";
```

`PauseMenuPanel.tsx:5` を `import { LanguageSelect } from "@/features/settings";` に変更
- [ ] **Step 3: 根拠コメント復元（C15）** — `BuildMenuCategoryGrid.tsx` の `sectionHeading` 直上の既存コメント（「複合見出しを両分類Guidから解決 / Resolve composite headings from both classification GUIDs」）を次の2行セットに置換:

```tsx
// 複合見出しはJSX外で組み立てて可視リテラルlintを避ける（Guidからの解決は下の式）
// Build the composite heading outside JSX to avoid the visible-literal lint; GUIDs resolve below
```

- [ ] **Step 4: skit.プレフィックスのSSOT化（C13）** — `SkitCommandLocalization.cs` のクラス先頭へ

```csharp
// skitキー名前空間の唯一の定義。loaderのフィルタと必ず同一値を共有する
// Single definition of the skit key namespace, shared with the loader filter
public const string KeyPrefix = "skit.";
```

を追加し、`CreateKey` の補間を `$"{KeyPrefix}{skitTitle}.{commandId.ToString(CultureInfo.InvariantCulture)}.{field}"` へ。`SkitLocalizationDictionaryLoader.cs:67` の `"skit."` を `SkitCommandLocalization.KeyPrefix` に置換（`using Client.Skit.Localization;` は既存参照）
- [ ] **Step 5: asmdef 2件（Codex Medium・precedent-alignment W）** — `Client.Skit.asmdef` の `references` から `"Core.Master"` を削除（`grep -rn "Core.Master\|MasterHolder" moorestech_client/Assets/Scripts/Client.Skit --include="*.cs"` が0件であることを先に確認）。`Client.Localization.asmdef` の `"overrideReferences": false` を `true` へ（precompiledReferences `mooresmaster.LocalizationCsv.dll` を実際に効かせる。前例: `Client.Skit.asmdef`・`Tests.asmdef` はいずれもtrue）
- [ ] **Step 6: 検証** — `uloop compile --project-path ./moorestech_client` → Error 0。`cd moorestech_web/webui && npx tsc -b && npm run lint && npx vitest run --reporter=dot` → 全pass。e2e型検査 `npx tsc -p e2e/tsconfig.json --noEmit`
- [ ] **Step 7: コミット** — `git add -A && git commit -m "fix: レビュー機械的修正バッチ（URL定数化・barrel・根拠コメント・skit.SSOT・asmdef整理）"`

---

### Task 2: Update()毎フレーム解決の購読化（C1）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/BlockPlacePreviewTutorialManager.cs`

**Interfaces:**
- Consumes: `Localize.OnLanguageChanged`（`IObservable<Unit>`・既存）、`ContentLocalizationKeys.ChallengeTutorialText(Guid)`
- 前例: 同PR内 `KeyControlTutorialManager.cs` の `Start()` 内 `Localize.OnLanguageChanged.Subscribe(_ => ...).AddTo(this)`

- [ ] **Step 1: MapObjectPin** — フィールド `private string _pinText = "";` を追加。`ApplyTutorial()`（`_currentTutorial` 設定箇所）の直後で `RefreshPinText();` を呼び、`Start()`（無ければ新設）に `Localize.OnLanguageChanged.Subscribe(_ => RefreshPinText()).AddTo(this);` を追加。private メソッド:

```csharp
// 言語切替時のみピン文言を再解決する（Update()は射影専用）
// Re-resolve the pin text only on language change; Update() does projection only
private void RefreshPinText()
{
    if (_currentTutorial == null) return;
    _pinText = Localize.GetContent(ContentLocalizationKeys.ChallengeTutorialText(_currentTutorial.TutorialGuid));
}
```

`PublishWebWorldPin()` 内の `Localize.GetContent(...)` 呼び出しとその2行コメントを削除し、`WorldPinStateStore.Instance.SetPin(WebPinId, _pinText, projection);` に置換
- [ ] **Step 2: BlockPlacePreviewTutorialManager** — 同型。フィールド `_message`、`ApplyTutorial` 末尾で再解決、`Start()` で購読、`Update()` は `SetPin(WebPinId, _message, projection)` のみ
- [ ] **Step 3: 検証** — `uloop compile` → Error 0。`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tutorial"` → 既存Tutorial系テスト全pass
- [ ] **Step 4: コミット** — `git commit -am "fix: チュートリアルピン文言の毎フレーム解決を言語変更購読へ置換"`

---

### Task 3: ConnectToolのGuid.Empty契約統一（C3の根本）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ConnectTool/ConnectToolCatalog.cs:44-54`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementTargetPickService.cs:38-46`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConnectTool/ConnectToolCatalogTest.cs`

**Interfaces:**
- Produces: `public static bool TryResolveDefaultConnectToolGuid(ConnectToolType toolType, IGameUnlockStateData unlockState, out Guid connectToolGuid)`（旧 `ResolveDefaultConnectToolGuid` は削除。他の呼び出し元は `grep -rn "ResolveDefaultConnectToolGuid"` で全列挙し追従させる）

- [ ] **Step 1: 失敗するテストを書く** — `ConnectToolCatalogTest.cs` へ追加:

```csharp
[Test]
// 未解放時はターゲットを構築させない（Guid.Emptyを下流へ漏らさない）
// When locked, no target must be constructed; Guid.Empty must not leak downstream
public void TryResolveDefaultConnectToolGuid_ReturnsFalse_WhenNothingUnlocked()
{
    var unlockState = CreateAllLockedUnlockState(); // 既存テストのセットアップヘルパーに合わせる
    var resolved = ConnectToolCatalog.TryResolveDefaultConnectToolGuid(
        ConnectToolType.ElectricWireConnect, unlockState, out var guid);
    Assert.IsFalse(resolved);
    Assert.AreEqual(Guid.Empty, guid);
}
```

- [ ] **Step 2: 失敗確認** — `uloop run-tests --filter-type regex --filter-value "ConnectToolCatalogTest"` → コンパイルエラー（メソッド未定義）で赤
- [ ] **Step 3: 実装** — `ConnectToolCatalog.cs` の `ResolveDefaultConnectToolGuid` を置換:

```csharp
public static bool TryResolveDefaultConnectToolGuid(ConnectToolType toolType, IGameUnlockStateData unlockState, out Guid connectToolGuid)
{
    var masterToolType = ToMasterToolType(toolType);
    var element = MasterHolder.ConnectToolMaster.All
        .Where(e => e.ToolType == masterToolType)
        .Where(e => unlockState.ConnectToolUnlockStateInfos.TryGetValue(e.ConnectToolGuid, out var info) && info.IsUnlocked)
        .OrderBy(e => e.SortPriority)
        .FirstOrDefault();
    connectToolGuid = element?.ConnectToolGuid ?? Guid.Empty;
    return element != null;
}
```

`PlacementTargetPickService.cs` の電線スポイト分岐を:

```csharp
if (!BlockClickDetectUtil.TryGetCursorOnElectricWire(out _)) return false;
// 未解放ならスポイト自体を不成立にする（Guid.Emptyターゲットを作らない）
// If locked, the eyedropper itself fails; never construct a Guid.Empty target
if (!ConnectToolCatalog.TryResolveDefaultConnectToolGuid(ConnectToolType.ElectricWireConnect, _gameUnlockStateData, out var wireToolGuid)) return false;
target = new ConnectToolPlacementTarget(wireToolGuid);
return true;
```

もう1つの呼び出し元 `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/GearChainPoleConnectSystem.cs:63` も同形へ更新する（`if (!ConnectToolCatalog.TryResolveDefaultConnectToolGuid(ConnectToolType.GearChainPoleConnect, _gameUnlockStateData, out var connectToolGuid)) return;` — 未解放時はチェーン接続を不成立にする。呼び出し元はこの2箇所で全部（grep実測）
- [ ] **Step 4: 検証** — Step 1のテスト緑 + `uloop run-tests --filter-type regex --filter-value "ConnectTool|PlaceSystem"` 全pass + `uloop compile` Error 0
- [ ] **Step 5: コミット** — `git commit -am "fix: connectTool未解放時のGuid.Empty漏出を遮断しNRE経路を根絶"`

---

### Task 4: Localize.TrySetLanguage集約とデッドAPI削除（C6・C20）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs:103-119,170-187`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Localization/LocalizationActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/LanguageSetting.cs:22`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/LocalizeTest.cs`、`Client.Tests/Localization/Skit/SkitLocalizationResolverBoundaryTest.cs`

**Interfaces:**
- Produces: `public static bool TrySetLanguage(string languageCode)`（null/空/未知言語でfalse・状態不変・LogErrorなし。旧 `SetLanguage(void)` は削除し呼び出し側を全更新）
- 削除: `Localize.TryGetContentWithoutSource`（production参照ゼロの確認済みデッドAPI）

- [ ] **Step 1: 失敗するテストを書く** — `LocalizeTest.cs` の既存「不正localeで状態変化なし」テスト（181-193行）を `TrySetLanguage` 前提に書き換え、戻り値検証を追加:

```csharp
Assert.IsFalse(Localize.TrySetLanguage("klingon"));
Assert.IsFalse(Localize.TrySetLanguage(null));
Assert.IsFalse(Localize.TrySetLanguage(Localize.SourcePseudoLocale));
Assert.IsTrue(Localize.TrySetLanguage("japanese"));
```

- [ ] **Step 2: 失敗確認** — `uloop run-tests --filter-type regex --filter-value "LocalizeTest"` → 赤（メソッド未定義）
- [ ] **Step 3: 実装** — `Localize.cs` の `SetLanguage` を置換:

```csharp
public static bool TrySetLanguage(string languageCode)
{
    // 可否は戻り値だけで表す（外部入力ハンドラがActionResultへ変換する）
    // Success/failure is expressed only via the return value; handlers map it to ActionResult
    if (string.IsNullOrEmpty(languageCode)) return false;
    var snapshot = /* 既存SetLanguageのsnapshot取得コードをそのまま */;
    if (languageCode == SourcePseudoLocale || !snapshot.ContainsKey(languageCode)) return false;
    /* 既存の言語適用・PlayerPrefs保存・onLanguageChangedSubject.OnNext(Unit.Default) をそのまま */
    return true;
}
```

`LocalizationActions.cs` の `ExecuteAsync` 本体を1行化し `IsSelectableLocale` と `using Mooresmaster.Localization.Generated;` を削除:

```csharp
return UniTask.FromResult(Localize.TrySetLanguage(locale) ? ActionResult.Success() : ActionResult.Fail("unknown_locale"));
```

`LanguageSetting.cs:22` は `Localize.TrySetLanguage(code);`（戻り値破棄で可・ドロップダウンは有効値しか出さない）。`TryGetContentWithoutSource` を削除し、`SkitLocalizationResolverBoundaryTest.cs:86,89` の当該検証を `Localize.GetContent` と `Localize.TryGetDictionary` による同等の境界検証（「source段を含まない解決が英語で止まること」をComposer経由で検証する既存テスト形）へ書き換え
- [ ] **Step 4: 検証** — `uloop run-tests --filter-type regex --filter-value "LocalizeTest|LocalizationLanguageSelection|SkitLocalizationResolverBoundary"` 全pass（`LocalizationLanguageSelectionContractTest` の失敗契約=0イベント・PlayerPrefs不変はそのまま通ること）+ `uloop compile` Error 0
- [ ] **Step 5: コミット** — `git commit -am "fix: 言語可否判定をTrySetLanguageへ一本化しデッドAPIを削除"`

---

### Task 5: 起動時辞書合成をtry境界内へ（C23）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:108-133`

- [ ] **Step 1: 実装** — 110行の `Localize.MergeGameDictionaries(...)` 呼び出し（と直上の2行コメント）を、124行からの既存 `try { (serverResult, assetResult, sceneLoadTask) = await UniTask.WhenAll(...); }` ブロックの**内側先頭**へ移動する（catchは既存のMainMenu復帰をそのまま使う。mod CSV不正・mod順不整合はここで捕まりMainMenuへ戻る）
- [ ] **Step 2: 検証** — `uloop compile` Error 0。`uloop run-tests --filter-type regex --filter-value "GameDictionaryRecomposition"` 全pass（合成順序はマスタロード後のまま不変であること）
- [ ] **Step 3: コミット** — `git commit -am "fix: 起動時辞書合成を初期化失敗境界内へ移し無限ローディングを解消"`

---

### Task 6: Skit日本語辞書の空文字3キー＋非空/言語集合契約テスト（C21）

**Files:**
- Modify: `moorestech_client/Assets/AddressableResources/Skit/i18n/japanese.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationDictionaryCompletenessTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationDynamicLoadContractTest.cs:48`

- [ ] **Step 1: 失敗するテストを書く** — `SkitLocalizationDictionaryCompletenessTest.cs` へ「全translations値が非空」の検証を追加:

```csharp
[Test]
// 空文字は欠落扱いで英語へフォールバックするため、辞書ファイル内の空文字は原文置換漏れ
// Empty strings fall through to English, so an empty value in the dictionary is a missed translation
public void AllTranslationValuesAreNonEmpty()
{
    foreach (var (address, json) in LoadAllSkitDictionaries()) // 既存テストのロードヘルパーを流用
    foreach (var (key, value) in ParseTranslations(json))
        Assert.IsFalse(string.IsNullOrEmpty(value), $"{address}: {key} が空文字");
}
```

- [ ] **Step 2: 失敗確認** — `uloop run-tests --filter-type regex --filter-value "SkitLocalizationDictionaryCompleteness"` → japanese.jsonの3キーで赤
- [ ] **Step 3: 訳を埋める** — `japanese.json` の3キーへ原文（`Skit/skits/100_start_game.json` id=3,4のbody・`sample_short.json` id=9のOption3Tag）と同内容の日本語を設定:
  - `skit.100_start_game.3.body`: `搭乗員一名、ヨリ、睡眠薬による昏睡を確認`
  - `skit.100_start_game.4.body`: `ワープシーケンスを開始。\n現在地、惑星セレスタル、目標、惑星アルカディア\n5..4..3..2..1....`（改行表現は同ファイル内の既存キーの流儀に合わせる）
  - `skit.sample_short.9.Option3Tag`: `第三候補`
- [ ] **Step 4: 言語集合契約（Fable推奨案B・言語追加でskitが無言全滅しない）** — `SkitLocalizationDynamicLoadContractTest.cs:48` のハードコード `{english, japanese}` を生成カタログ由来へ:

```csharp
var expected = LanguageCatalog.Languages.Select(l => $"Vanilla/Skit/i18n/{l.Code}").ToArray();
CollectionAssert.AreEquivalent(expected, addresses);
```

- [ ] **Step 5: 検証** — 同regexで全pass + `uloop compile` Error 0
- [ ] **Step 6: コミット** — `git commit -am "fix: Skit日本語辞書の空文字3キーを翻訳し非空・言語集合の契約テストを追加"`

---

### Task 7: 実績通知Guidワイヤ契約テスト（C4）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/AchievementNotificationWiringTest.cs`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts:68`

- [ ] **Step 1: 失敗するテストを書く（mutationで死ぬ形）** — 既存テストの研究完了ケースへ、取り出した `NotificationMessagePack` のdeserialize後検証を追加:

```csharp
// 表示名でなくGuidを送るワイヤ契約を固定する（Web側辞書解決の前提）
// Pin the wire contract: GUIDs are sent, not display names, for web-side dictionary resolution
Assert.AreEqual(Research1Guid.ToString("D"), data.MessageParams[0]);
```

チャレンジ完了側にも同形で `ChallengeGuid` の1ケースを追加（既存の `CompleteResearchForTest` / チャレンジ完了ヘルパーを流用）
- [ ] **Step 2: 赤→緑確認** — まず `AchievementNotificationWiring.cs:36` を一時的に `ResearchNodeName` へ戻してテストが赤になることを確認（mutation有効性）→ 戻して緑を確認。`uloop run-tests --filter-type regex --filter-value "AchievementNotificationWiring"`
- [ ] **Step 3: e2e fixtureの契約追従** — `topicControls.ts:68` の `messageParams: ["原始研究1"]` を実GuidD形式文字列（`blockLocalizationFixtures.ts` 等で使用中の研究Guidに合わせる）へ変更。`npx tsc -p e2e/tsconfig.json --noEmit` で型検査
- [ ] **Step 4: コミット** — `git commit -am "test: 実績通知のGuidワイヤ契約を固定しe2e fixtureを追従"`

---

### Task 8: Guid導出キーの宣言表→C#/TS両側生成・型付き化（D3案A＋D7案B・C18・C5の生成器分）

**Files:**
- Create: `Localization/content_keys.csv`（宣言表。列: `namespace,field,sourceMaster`。行: item/name, block/name, research/name, challenge/title, challengeCategory/description, character/name, buildMenuCategory/name, buildMenuSubCategory/name, challengeTutorial/text, connectTool/name, fluid/name — **既存C#13ビルダーの全種+fluid**）
- Create: `mooresmaster/mooresmaster.Generator/Localization/ContentKeyCatalogParser.cs`
- Modify: `mooresmaster/mooresmaster.Generator/Localization/LocalizationCodeGenerator.cs`（`ContentLocalizationKey` struct＋型付きビルダーのemitを追加。既存の単一呼び出し元ヘルパー6箇所はこの改修で `Generate`/`EmitTable` 末尾の`#region Internal`ローカル関数へ集約=C5生成器分）
- Modify: `mooresmaster/mooresmaster.Generator/LocalizationSourceEmitter.cs`（AdditionalFilesに`content_keys.csv`を追加・`EmitLocalization`のローカル関数化=C5）
- Modify: `mooresmaster/mooresmaster.Generator/Localization/LanguageCatalogCodeEmitter.cs:48`・`LocalizationCodeSyntax.cs:64`（C5: `ValidateLanguageSet`/`IsLowerCamelSegment`をローカル関数化）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/ContentLocalizationKeys.cs` → **削除**（生成物 `Mooresmaster.Localization.Generated.ContentLocalizationKeys` に置換）
- Modify: `moorestech_web/webui/scripts/generate-localization-keys.mjs`（同じ宣言表から `generated/contentKeys.ts` を出力）
- Modify: `moorestech_web/webui/src/shared/i18n/contentKeys.ts` → 生成物の再exportへ縮退（`export * from "../generated/contentKeys";` 形。既存の手書きビルダー削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`（`GetContent(ContentLocalizationKey)` へシグネチャ変更・`GetLegacy` は `TextMeshProLocalize` 専用へ）
- Modify: 全 `Localize.GetContent(...)` / `ContentLocalizationKeys.*` 呼び出し側（`grep -rln "ContentLocalizationKeys\." moorestech_client/Assets/Scripts` で列挙・型変更に追従。挙動不変）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/csc.rsp`・`moorestech_server` 側同等物（additionalfile追加）
- Test: `mooresmaster/mooresmaster.Tests/LocalizationTests/ContentKeyCatalogTest.cs`（新規）、既存 `Client.Tests/Localization/ContentLocalizationKeysTest.cs`（生成物検証へ書き換え・`ChallengeTutorialText`/`ConnectToolName`/`FluidName` を網羅）、`moorestech_web/webui/src/shared/i18n/contentKeys.test.ts`（生成物とC#期待値の一致検証へ）

**Interfaces:**
- Produces（C#生成物）: `public readonly struct ContentLocalizationKey { public readonly string Key; }` / `public static class ContentLocalizationKeys { public static ContentLocalizationKey ItemName(Guid itemGuid); /* 宣言表の全行分 */ public static ContentLocalizationKey FluidName(Guid fluidGuid); ... }` / `Localize.GetContent(ContentLocalizationKey key)`
- Produces（TS生成物）: `generated/contentKeys.ts` — `export const itemNameKey = (guid: string): ContentLocalizationKey => ...` を宣言表全行分（`connectToolNameKey`・`challengeTutorialTextKey`・`fluidNameKey` を含む）＋ `ContentLocalizationKey` template literal union
- キー書式は現行と同一: `<namespace>.<小文字D形式guid>.<field>`（C#は `guid:D`・TSは `canonicalGuidSegment` の小文字化を維持）

- [ ] **Step 1: 失敗するテストを書く（generator）** — `ContentKeyCatalogTest.cs`: 宣言表CSVをparseし、生成コードをコンパイルして `ContentLocalizationKeys.ItemName(guid).Key == $"item.{guid:D}.name"` をreflection検証（既存 `LocalizationCodeGeneratorTest` の実コンパイル方式を流用）。fluid/connectTool/challengeTutorialの3行も検証に含める
- [ ] **Step 2: 失敗確認** — `cd mooresmaster && dotnet test --filter ContentKeyCatalog` → 赤
- [ ] **Step 3: generator実装** — `ContentKeyCatalogParser`（`LocalizationSettingsParser` と同形の共通Parser利用）→ `LocalizationCodeGenerator` に `EmitContentKeys` を追加し struct＋ビルダーを出力。`LocalizationSourceEmitter` は `content_keys.csv` をAdditionalFilesの対契約（既存の「片側だけの配線はコンパイルエラー」機構）に追加
- [ ] **Step 4: TS側生成** — `generate-localization-keys.mjs` に宣言表読み込みと `generated/contentKeys.ts` 出力を追加（既存のvanillaキー出力と同じ書式検査つき）。`contentKeys.ts` を再export化し、`contentKeys.test.ts` は「宣言表の全行がTS側に存在し書式が一致する」検証へ
- [ ] **Step 5: DLL再ビルド＋C#呼び出し側一括追従** — `cd mooresmaster && ./build.sh`。`ContentLocalizationKeys.cs`（手書き）を削除し、呼び出し側の型を追従（シグネチャ変更のみ・ロジック不変）。`Localize.GetContent(string)` は削除し `GetContent(ContentLocalizationKey)` のみ残す
- [ ] **Step 6: 検証** — `dotnet test`（mooresmaster全緑）→ `uloop compile` Error 0 → `uloop run-tests --filter-type regex --filter-value "Localiz"` 全pass → webui `npm run gen:i18n && npx tsc -b && npm test` 全pass（`localizationKeysFreshness.test.ts` が生成物一致を担保）
- [ ] **Step 7: コミット** — `git commit -am "feat: Guid導出キーを宣言表からC#/TS両側へ型付き生成し手書き二重定義を解消"`（再ビルドDLL含む）

---

### Task 9: fluid名のWeb辞書解決化（D6案A・C2）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/MasterSourceTextCollector.cs`（fluid収集ループ追加。本タスク時点ではまだ`Client.Localization`配下にある — Task 14が後からfluidループごと`Client.Game/Localization/`へ移設する）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs:45`（`BlockFluidSlotDto.Name` → `public string FluidGuid;`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs:168-177`・`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs:21`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineRecipeSelectionPanel.cs:110,113`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts:15`（`name: z.string()` → `fluidGuid: GuidSchema.or(z.literal(""))`。空流体は空文字）
- Modify: `moorestech_web/webui/src/shared/ui/FluidSlot/index.tsx:19`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/MasterSource/MasterSourceTextCollectorTest.cs`（fluid期待キー追加）＋ 網羅テスト新設

**Interfaces:**
- Consumes: Task 8の `ContentLocalizationKeys.FluidName(Guid)` / TS `fluidNameKey(guid)`
- Produces: `BlockFluidSlotDto.FluidGuid`（D形式小文字。空流体は `""`）

- [ ] **Step 1: 失敗するテストを書く** — `MasterSourceTextCollectorTest.cs` に「fluidマスタ全件の `fluid.<guid>.name` が原文つきで収集される」ケースを追加（既存の他マスタと同形・実装と独立に期待値を組む流儀を踏襲）。加えて再発防止の網羅テスト `MasterSourceCoverageTest.cs` を `Client.Tests/Localization/MasterSource/` に新設: 「MasterHolderの表示名を持つ全マスタ種（item/block/fluid/research/challenge/character/buildMenuCategory/connectTool/challengeTutorial）が `Collect()` 結果にキー接頭辞として1件以上現れる」
- [ ] **Step 2: 失敗確認** — `uloop run-tests --filter-type regex --filter-value "MasterSource"` → 赤（fluid欠落）
- [ ] **Step 3: 収集実装** — Collectorへ他マスタと同形のforeach。列挙は既存の `MasterHolder.FluidMaster.Fluids.Data`（`public readonly Fluids Fluids` が既にpublic・`FluidMaster.cs:27`）を使う — **新規メンバの追加は不要**（`BlockMaster.Blocks.Data` と同形）
- [ ] **Step 4: DTO/Webの一括更新** — `BlockFluidSlotDto.Name`→`FluidGuid`、Builder側は `GetFluidMaster(...).FluidGuid.ToString("D")`（`FluidMaster.EmptyFluidId` は `""`）。zodスキーマ・`FluidSlot/index.tsx` は `const { t } = useI18n(); ... label={fluid.fluidGuid ? t(fluidNameKey(fluid.fluidGuid)) : ""}`。`MachineRecipeSelectionPanel.cs` 110/113行を109/112行と同形の `Localize.GetContent(ContentLocalizationKeys.FluidName(...))` へ
- [ ] **Step 5: 検証** — `uloop compile` Error 0・上記regexテスト緑・webui `npx tsc -b && npm test` 全pass（inventory系スナップショット/契約テストの追従含む）
- [ ] **Step 6: コミット** — `git commit -am "feat: 流体名をGuid導出キーのWeb辞書解決へ移行しホスト側Name同梱を廃止"`

---

### Task 10: connectTool表示名のWeb解決統一＋placement表示名集約（D1案A・C17・C10・D10案A）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/WebBuildMenuEntryCatalog.cs:62-68`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/WebBuildMenuEntry.cs`（`CreateConnectTool(Guid connectToolGuid, IReadOnlyList<RequiredItem> requiredItems)` — label引数削除）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs:27,43-47,58`（`_languageSubscription` 削除）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PlacementModeTopic.cs:87-97`（connectTool分岐を `SelectedTargetType="connectTool"` + `public string SelectedConnectToolGuid;` へ。`"raw"` はblueprint/trainCar専用に縮退）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts`（connectToolを `label: z.never().optional()` のblock同形variantへ移動）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`（`PlacementModeDataSchema` のdiscriminatedUnionへ `connectTool` variant追加: `selectedConnectToolGuid: GuidSchema`）
- Modify: `moorestech_web/webui/src/features/buildMenu/buildMenuGrouping.ts`（`localizeSelectableTargetName` 新設＋`localizeBuildMenuEntries` のconnectTool分岐追加）
- Modify: `moorestech_web/webui/src/features/modeHud/PlacementModeHud.tsx:14-18`（三項連鎖を `localizeSelectableTargetName` 呼び出しへ置換）
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/`（buildMenu/placementのfixtureをGuid送出契約へ追従）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuProductionContractTest.cs`・webui `buildMenuGrouping` のvitest

**Interfaces:**
- Consumes: Task 8の TS `connectToolNameKey(guid)`
- Produces: `localizeSelectableTargetName(target: { type: "block"; guid: string } | { type: "connectTool"; guid: string } | { type: "blueprintCopy" } | { type: "raw"; label: string }, translate: (key: TranslationKey) => string): string`（**新規ファイルは作らず** `buildMenuGrouping.ts` に置く）

- [ ] **Step 1: 失敗するテストを書く（TS）** — `buildMenuGrouping.test.ts`（既存があれば追記）: `localizeSelectableTargetName({type:"connectTool",guid},t)` が `connectToolNameKey(guid)` の翻訳を返すこと、`localizeBuildMenuEntries` がconnectTool entryを辞書解決すること
- [ ] **Step 2: 失敗確認** — `npx vitest run src/features/buildMenu` → 赤
- [ ] **Step 3: TS実装** — `buildMenuGrouping.ts`:

```ts
// 配置対象の表示名解決はこの1本に集約する（BuildMenu/PlacementHUD共用・分岐の複製禁止）
// All selectable-target display names resolve here, shared by BuildMenu and PlacementHUD
export function localizeSelectableTargetName(
  target:
    | { type: "block"; guid: string }
    | { type: "connectTool"; guid: string }
    | { type: "blueprintCopy" }
    | { type: "raw"; label: string },
  translate: (key: TranslationKey) => string,
): string {
  switch (target.type) {
    case "block": return translate(blockNameKey(target.guid));
    case "connectTool": return translate(connectToolNameKey(target.guid));
    case "blueprintCopy": return translate(L.ui.buildMenu.blueprintCopy);
    case "raw": return target.label;
  }
}
```

`localizeBuildMenuEntries` と `PlacementModeHud.tsx` を両方この関数経由へ（HUD側はzodの新variantから `{type:"connectTool",guid:data.selectedConnectToolGuid}` を渡す）
- [ ] **Step 4: C#実装** — `WebBuildMenuEntryCatalog` のconnectTool分岐から `Localize.GetContent` と2行コメントを削除しGuidのみ渡す。`WebBuildMenuEntry.CreateConnectTool` からlabel引数削除（`EntryKey`=connectToolGuidは既存）。`BuildMenuTopic` の言語購読フィールド・購読・disposeを削除。`PlacementModeTopic` のconnectTool分岐:

```csharp
ConnectToolPlacementTarget tool => /* dto.SelectedTargetType = "connectTool"; dto.SelectedConnectToolGuid = tool.ConnectToolGuid.ToString("D"); を設定するfactory分岐 */
```

（既存の `PlacementModeDtoFactory` のswitch形を維持。`GetElementOrNull(...).Name` 参照は消滅=C3の表示面も根絶）
- [ ] **Step 5: スキーマ/fixture追従** — `buildMenu.ts`・`ui.ts` のzod更新、mock-host fixtureをGuid契約へ。`BuildMenuProductionContractTest.cs` のconnectTool期待をlabel無しへ
- [ ] **Step 6: 検証** — `uloop compile` Error 0・`uloop run-tests --filter-type regex --filter-value "BuildMenu|PlacementMode"` 全pass・webui `npx tsc -b && npm test && npx tsc -p e2e/tsconfig.json --noEmit` 全pass・`npx playwright test --config e2e/playwright.config.ts --grep "buildMenu|i18n"`（該当specのみ）
- [ ] **Step 7: コミット** — `git commit -am "feat: connectTool表示名をWeb側Guid辞書解決へ統一しBuildMenu再pushと表示名分岐複製を撤去"`

---

### Task 11: Tooltip契約のキー+textParams専用化（D4案B・C16）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs:43,61-71`・`IMouseCursorTooltip.cs:18`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs:36,46`（`IsLocalize`削除・`TextParams`追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MapObjectMiningFocusState.cs:55,101,118-125`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs:87,116` と供給側 `BlockGameObjectChild.cs:96-103`・`DeleteTargetRail.cs:49-56`・`DragDeleteSelection.cs:25-27`（enum→`LocalizationKey` を返す形へ）
- Modify: `Localization/localization.csv`（`ui.tooltip.requiredItemsPrefix` → `ui.tooltip.requiredItems` = `このアイテムが必要です: {p0}` 形式へ改キー。Source/english/japanese全列）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/`（tooltipスキーマ: `isLocalize`削除・`textParams: z.array(z.string())`）・`moorestech_web/webui/src/features/*/CursorTooltip.tsx:43-44`（常にキー解決+`{p0}`補間。`resolveTooltipText`/`translateExternalKey` の生文字列分岐削除）
- Modify: `moorestech_web/webui/src/shared/i18n/i18nStore.ts:37-41`（`isTranslationKey` をcontentキー書式（`<ns>.<uuid>.<field>` 正規表現）も受理する述語へ修正 — Web側warning 2系統一致分）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/UGuiTooltipTarget.cs:53`・`GameObjectTooltipTarget.cs:24`（prefabシリアライズのstring `textKey` は `new LocalizationKey(textKey)` で包んで新Showへ渡す。`[SerializeField] localize` フィールドは削除 — prefab実データは全4件textKey空・localize=trueのためprefab側の移行作業は不要（反証実測済み））
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Localization/SerializedLocalizedTooltipKeyTest.cs`（`FindProperty("localize")` の読み取りを削除 — localizeフィールド削除でNREになるため必須追従）
- Modify: `UGuiTooltipTarget.SetText(text, false)` のランタイム呼び出し元（`CommonSlotView.cs:70,86`・`ResearchTreeElement.cs:106,210` — 退役uGUIの合成tooltip。`{p0}` 補間キー＋textParams形へ揃える）
- Modify: 他の全 `MouseCursorTooltip.Instance.Show` 呼び出し元（`grep -rn "MouseCursorTooltip.Instance.Show" moorestech_client/Assets/Scripts --include="*.cs"` で列挙・全8箇所/5ファイル。`CraftButton.cs:123` の生文字列は専用キーをCSVへ追加して移行）
- Test: webui `CursorTooltip` のvitest・`uloop run-tests --filter-type regex --filter-value "Tooltip"`

**Interfaces:**
- Produces: `void Show(LocalizationKey key, int fontSize);` / `void Show(LocalizationKey key, IReadOnlyList<string> textParams, int fontSize);`（`isLocalize` は全廃。デフォルト引数も同時に廃止=AGENTS.md準拠）
- `TooltipPresentation`: `readonly struct { bool Visible; string TextKey; string[] TextParams; int FontSize; }`

- [ ] **Step 1: 失敗するテストを書く（TS）** — CursorTooltipのvitestに「`textKey`+`textParams` で `{p0}` が補間される」「contentキー（`item.<guid>.name`）がそのまま辞書解決される」を追加
- [ ] **Step 2: 失敗確認** — `npx vitest run` 該当ファイル → 赤
- [ ] **Step 3: C#契約変更** — 上記シグネチャへ置換し、全呼び出し元を一括更新（`.Key` 剥がし2箇所は `LocalizationKey` を直接渡す形になり消滅）。`MapObjectMiningFocusState:118-125` はアイテム名連結をやめ `Show(LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { joinedItemNames }, fontSize)` へ
- [ ] **Step 4: wire/Web追従** — TooltipDto・zod・CursorTooltip実装。`isTranslationKey` 修正:

```ts
const CONTENT_KEY_RE = /^[a-z][a-zA-Z]*\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.[a-zA-Z]+$/;
export function isTranslationKey(value: string): value is TranslationKey {
  return translationKeys.has(value as VanillaLocalizationKey) || CONTENT_KEY_RE.test(value);
}
```

- [ ] **Step 5: 検証** — `uloop compile` Error 0・Tooltip系/採掘系テスト緑・webui全検査pass・CSV改キーは `npm run gen:i18n` 再生成とC#再コンパイルで両側ビルドエラー0（キー切れ検出機構が働くこと自体が検証）
- [ ] **Step 6: コミット** — `git commit -am "feat: Tooltip契約をLocalizationKey+textParams専用化しIsLocalize二役を廃止"`

---

### Task 12: 辞書ロードstatusのUI消費＋失敗リトライ＋uninitialized分離（D2案A・C8・C9）

**Files:**
- Modify: `moorestech_web/webui/src/shared/i18n/i18nStore.ts:13,28,63-81,114-123`
- Modify: `moorestech_web/webui/src/shared/i18n/I18nProvider.tsx:23-59`
- Modify: `moorestech_web/webui/src/app/App.tsx:55`・`moorestech_web/webui/src/app/AppErrorBoundary.tsx:35-44`
- Modify: `moorestech_web/webui/src/features/settings/LanguageSelect.tsx:16-31`
- Test: `moorestech_web/webui/src/shared/i18n/useI18n.test.ts`・`provider/dictionaryGeneration.test.ts`・`LanguageSelect` の新規vitest

**Interfaces:**
- Produces: `I18nStatus = "uninitialized" | "loading" | "ready" | "error"`（初期snapshotは `uninitialized`。`createTranslator` の空文字返しは `status === "uninitialized"` 判定へ変更し `generation` の意味流用を解消）
- `useI18n()` のシグネチャは不変（`status` を返す既存形のまま消費者を新設）

- [ ] **Step 1: 失敗するテストを書く** — `useI18n.test.ts` へ「初期状態のstatusは `uninitialized`」「`setDictionaryLoadError` 後にstatusが `error`」を追加。`LanguageSelect.test.tsx` を新設: fetch失敗をmockし「エラーメッセージ（辞書非依存リテラル）が描画される」「成功時はoptionsが並ぶ」
- [ ] **Step 2: 失敗確認** — `npx vitest run src/shared/i18n src/features/settings` → 赤
- [ ] **Step 3: store実装** — `I18nStatus` へ `"uninitialized"` 追加・初期値変更・`createTranslator` の `generation === 0` を `status === "uninitialized"` へ。`I18nProvider` は失敗時に同一locale+revisionで1回リトライし（`setTimeout` 5秒・effect内でcleanup対応）、それでも失敗なら `error`。409は既存のrevision再push経路が回すためリトライ対象外・404/400のみ即error（fetchDictionaryが `response.status` を判別して投げ分ける）
- [ ] **Step 4: 表示実装** — `App.tsx`: `const { status } = useI18n();` を追加し `status === "error"` のとき既存reconnectオーバーレイと同型の失敗表示（**`t()`を使わないリテラル文言**: `"Failed to load language data. / 言語データの読み込みに失敗しました。"`）を1つ描画。`AppErrorFallback`: `status !== "ready"` のとき3つの `t()` を同様のリテラルへフォールバック。`LanguageSelect`: `useState<{ status: "loading" | "error" | "ready"; entries: LanguageEntry[] }>` へ変更し、`!response.ok`/`catch` を `error` へ、error時はリテラル文言＋再試行ボタン（`onClick` で再fetch）
- [ ] **Step 5: 検証** — 上記vitest緑・`npx tsc -b && npm run lint && npm test` 全pass・`npx playwright test --grep "i18n"` pass
- [ ] **Step 6: コミット** — `git commit -am "feat: 辞書ロードstatusをUIへ露出し失敗リトライとuninitialized分離を実装"`

---

### Task 13: source疑似ロケールのsnapshot型分離（D8案B・C12）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Dictionary/PublishedLocalizationDictionarySnapshot.cs`（`Languages`（実言語のみ）と `SourceTexts` の2フィールドへ分離）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Dictionary/VanillaLocalizationDictionaryFactory.cs`（`dictionaries.Add(SourcePseudoLocale, ...)` をやめ `SourceTexts` へ）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`（`TrySetLanguage`/`Initialize` のsource除外条件を削除=構造的に不要化。`TryGetDictionary("source")` の特例を `GetSourceTexts()` 公開へ置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Dictionary/ModLocalizationMerger.cs`（source列拒否の実行時checkは**維持**=mod CSVの外部入力検証。snapshotへの合流先だけ変更）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/LocalizationDictionaryEndpoint.cs`（`locale == "source"` を明示分岐で `GetSourceTexts()` から配信）
- Modify: `moorestech_web/webui/src/shared/i18n/i18nStore.ts`（`export const SOURCE_LOCALE = "source";` を `FALLBACK_LOCALE` の隣へ）・`I18nProvider.tsx:39`（`fetchDictionary(SOURCE_LOCALE, ...)`）
- Test: `Client.Tests/Localization/LocalizeTest.cs`・`GameDictionaryRecompositionTest.cs`（snapshot形の追従）

- [ ] **Step 1: 失敗するテストを書く** — `LocalizeTest.cs` へ「`GetLanguageCodes()`/選択可能言語にsourceが構造的に現れない（除外条件なしで）」「`GetSourceTexts()` が原文辞書を返す」を追加
- [ ] **Step 2: 失敗確認** — `uloop run-tests --filter-type regex --filter-value "LocalizeTest"` → 赤
- [ ] **Step 3: 実装** — 上記分離。ファイル群は `Client.Localization/Dictionary/` サブディレクトリへ同時移動（type-driven W「直下10本上限張り付き」の解消。移動は.cs+.metaペアで行う）
- [ ] **Step 4: 検証** — `uloop compile` Error 0・`uloop run-tests --filter-type regex --filter-value "Localiz"` 全pass・webui `npm test` pass（配信URL不変のためWeb挙動不変）
- [ ] **Step 5: コミット** — `git commit -am "refactor: source疑似ロケールをsnapshot型で実言語と分離し除外規則を構造化"`

---

### Task 14: MasterSourceTextCollectorのpush化（D5案B）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs`（`Client.Localization` から移設。namespace `Client.Game.Localization`）
- Delete: `moorestech_client/Assets/Scripts/Client.Localization/MasterSourceTextCollector.cs`（+.meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs:58-66`（pullオーバーロード `MergeGameDictionaries(ModsResource)` を削除し、`public static void MergeGameDictionaries(ModsResource modsResource, IReadOnlyList<ModId> orderedModIds, IReadOnlyDictionary<string, string> masterSourceTexts)` へ一本化）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（呼び出しを3引数形へ: `var container = ServerContext.GetService<MasterJsonFileContainer>(); Localize.MergeGameDictionaries(ServerContext.GetService<ModsResource>(), container.SortedModIds, MasterSourceTextCollector.Collect());`）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Client.Localization.asmdef`（`Game.Context` 参照を削除。`Core.Master` は `ModId` 型のためだけに残す）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/`側asmdef（Client.Gameが属するasmdefへ必要参照が既にあることを確認）
- Modify: `MasterSourceTextCollector.cs` 内 `GetTutorialDisplayText` を `private` へ（domain-boundary W）
- Test: `Client.Tests/Localization/MasterSource/MasterSourceTextCollectorTest.cs`・`GameDictionaryRecompositionTest.cs`（4引数形へ追従。Collectorのusing変更）

- [ ] **Step 1: テスト追従を先に書く** — 両テストの `Localize.MergeGameDictionaries(modsResource)` 呼び出しを3引数形（`modsResource, orderedModIds, masterSourceTexts`）へ書き換え（期待挙動は不変）→ 赤（シグネチャ未変更）を確認
- [ ] **Step 2: 実装** — 上記移設・シグネチャ一本化・asmdef参照削除。`ServerContext.GetService` はLocalize内から消滅（domain-boundary/arch-lifecycle/Codex Highの3系統指摘の解消）
- [ ] **Step 3: 検証** — `uloop compile` Error 0（Game.Context参照削除でコンパイルが通ること自体が依存断ちの証明）・`uloop run-tests --filter-type regex --filter-value "MasterSource|GameDictionary"` 全pass
- [ ] **Step 4: コミット** — `git commit -am "refactor: 原文収集をClient.Game側へ移しLocalize基盤からドメイン語彙とServerContext依存を除去"`

---

### Task 15: Skitローカライズresolver再構成（D9案B・C7・C19）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Skit/Localization/SkitLocalizationResolver.cs`（全面改修）
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Localization/ISkitLocalizationResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Localization/SkitCommandLocalization.cs`（identity引数全削除）
- Delete: `moorestech_client/Assets/Scripts/Client.Skit/Context/SkitExecutionIdentity.cs`・`StoryContextExtension.cs` の `GetExecutionIdentity()`（+RegisterInstance 2箇所: `SkitManager.cs:151-152`・`BackgroundSkitManager.cs:80-81`）
- Delete: `moorestech_client/Assets/Scripts/Client.Game/Skit/Lifecycle/SkitCleanupOnce.cs`（+.meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs`・`InGame/BackgroundSkit/BackgroundSkitManager.cs`（cleanupローカルbool化・identity登録削除）
- Modify: コマンド3本 `TextCommand.cs`・`SelectionCommand.cs`・`BackgroundSkitTextCommand.cs`（identity取得行削除・新シグネチャ）
- Test: `Client.Tests/Localization/Skit/` 配下（`SkitCommandLocalizationTest.cs`・`SkitLocalizationResolverLifecycleTest.cs`・`SkitCleanupOnceTest.cs`→behavior版へ置換・`SkitLocalizationTestFakes.cs`）

**Interfaces:**
- Produces: `ISkitLocalizationResolver`:

```csharp
UniTask PrepareAsync(string skitTitle);           // skitTitleをフィールド保持する（死引数の解消）
string ResolveCommandField(int commandId, string field, string sourceText);
string ResolveCharacterName(string characterId);
string ResolveOverriddenCharacterName(int commandId, string overrideSource);  // bool+道連れ引数の分割（schema-design W）
```

- 同期機構: `Interlocked`/`Volatile` を全廃する。バースト畳み込みの前例は `BuildMenuTopic.cs:71-90` の `bool _publishScheduled` / `bool _disposed`（前例にあるのはこのbool 2本のみ — revision intはBuildMenuTopicには存在しない）。revision対は既存resolverの `_observedRevision` / `_publishedRevision` フィールドから `Volatile.Read/Write` を剥がして素のintのまま維持する（メインスレッド専用・全入口が `UniTask.SwitchToMainThread` 後であることはprecedent-alignmentレンズが実証済み）
- 再ロード失敗時は `_publishedScope` を維持しつつ `_reloadScheduled` を戻して**次の言語変更で再試行可能**にする（cs-result-state Wの恒久ブロック解消）。失敗ログは `Debug.LogException`

- [ ] **Step 1: 失敗するテストを書く** — `SkitCommandLocalizationTest.cs` をidentity無しシグネチャへ書き換え（35,55行のidentity生成削除）→ 赤。`SkitLocalizationResolverLifecycleTest.cs` へ「再ロード失敗（Fakeローダーが1回目throw）後、次のRequestReloadで復旧する」ケースを追加 → 赤
- [ ] **Step 2: 実装** — 上記Interfaces形へ。`RequestReload`/`SchedulePendingReload`/`BuildAndPublishScopeAsync` はクラス直下のprivateメソッドへ引き上げ（unidirectional W）、`PrepareAsync` は「_skitTitle保持＋購読張り＋収束待ち」のみに縮小。`SkitCleanupOnce` はSkitManagerのローカル `var mapPinHidden = false;`＋`finally` 内の素直な一回実行へ置換（BackgroundSkitManagerはTryBegin相当も不要になる）。`SkitCleanupOnceTest.cs` のソース文字列一致検証は削除し、「skit途中で例外→cleanupが1回だけ走りresolverがDisposeされる」実挙動テスト（Fake presentation+throwするcommand）へ置換
- [ ] **Step 3: 検証** — `uloop compile` Error 0・`uloop run-tests --filter-type regex --filter-value "Skit"` 全pass
- [ ] **Step 4: コミット** — `git commit -am "refactor: Skitローカライズresolverを前例準拠の単純同期へ再構成しSkitExecutionIdentity/SkitCleanupOnceを撤去"`

---

### Task 16: region-internal残り4ファイル（C5のSchemaWatch+CSVパーサ分）

**Files:**
- Modify: `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvParser.cs:195`（`ConvertEscapedNewlines` → `Parse` 末尾 `#region Internal` のローカル関数へ）
- Modify: `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchCache.cs:72,104,132`（`Load`→ctor内・`LoadVersionTwoLine`→Loadのネスト・`Escape`→Save内の各`#region Internal`ローカル関数へ）
- Modify: `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchOrchestrator.cs:62,95`（`UpdateRequesterScript`→`CheckForChanges`内・`ComputeRequesterToken`→そのネストへ）
- Modify: `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchTarget.cs:55`（`ComputeHash`→`TryReadCurrentHashes`内へ）

- [ ] **Step 1: 機械的変換** — 各ヘルパーを唯一の呼び出し元メソッド末尾の単一 `#region Internal` ローカル関数へ移動。クロージャで拾える引数はシグネチャから削り呼び出し側も同時更新。private宣言は削除
- [ ] **Step 2: 検証** — `cd mooresmaster && dotnet test` 全緑（LocalizationCsvParser分）・`uloop compile` Error 0・`uloop run-tests --filter-type regex --filter-value "SchemaWatch"` 全pass。generator DLLに触れたため `./build.sh` 再実行＋DLLコミット
- [ ] **Step 3: コミット** — `git commit -am "refactor: 単一呼び出し元ヘルパーをregion Internalローカル関数へ集約（SchemaWatch/CSVパーサ）"`

---

### Task 17: テストディレクトリの関心別再編（D11案A）

**Files:**
- Move: `Client.Tests/Localization/` 直下10本を `Composition/`（GameDictionaryRecompositionTest, ModLocalizationMergerTest, ModLocalizationMergerValidationTest ＋ `MasterSource/MasterSourceTextCollectorTest.cs` を畳む）・`Resolution/`（LocalizeTest, LocalizationTextResolverTest, ContentLocalizationKeysTest, LocalizeContentTest）・`Display/`（ClientGameLocalizedDisplayContractTest, ItemSlotDefaultTooltipTest, SerializedLocalizedTooltipKeyTest）へ
- Delete: `Client.Tests/Localization/MasterSource/` ディレクトリ（1ファイル専用の解消。Task 9の網羅テストも `Composition/` へ）

- [ ] **Step 1: 移動** — .cs+.metaペアで `git mv`。新ディレクトリの.metaはUnity起動時生成に任せる（手動作成しない）。namespaceを `Client.Tests.Localization.Composition` / `.Resolution` / `.Display` へ更新（**`Dictionary` をディレクトリ名に使わない** — `System.Collections.Generic.Dictionary` とのCS0118/CS0104衝突防止）
- [ ] **Step 2: 検証** — `uloop compile` Error 0（ドメインリロード後）・`uloop run-tests --filter-type regex --filter-value "Client.Tests.Localization"` 全pass・結果カウントが再編前と同数であること
- [ ] **Step 3: コミット** — `git commit -am "refactor: ローカライズテストを関心別サブディレクトリへ再編"`

---

### Task 18: コメント文字数の機械的短縮2件（C22の機械的分のみ）

**Files:**
- Modify: `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvParser.cs:12`（「クォートを考慮してCSV全体をレコードへ分割する」→「クォート考慮でCSVをレコード分割」。英語行も同粒度で短縮）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/MasterSourceTextCollector.cs:74`（Task 14移設先で。「チュートリアル表示文言もtutorialGuidで収集」→「tutorial文言もGuidで収集」）

- [ ] **Step 1: 置換して検証** — 2箇所とも日英2行セットの体裁を維持。`uloop compile` Error 0・`dotnet test`（CsvParser分）緑。※convention-guardの要判断6件は「短縮すると対象の明示が落ちる」例外判定に従い残置する
- [ ] **Step 2: コミット** — `git commit -am "style: コメント文字数規約の機械的短縮2件"`

---

### Task 19: 【必須・最終】moores-code-review全ブランチレビュー

- [x] **Step 1:** 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。PATCHは `origin/master...HEAD`、contextは本planの裁定対応表を4カテゴリへ転記して渡す
- [x] **Step 2:** レビュー指摘の確定修正を適用し、`uloop compile` Error 0・サーバー/クライアント関連テスト・webui全検査（tsc/lint/vitest/playwright）を最終確認してコミット
- [x] **Step 3: 実機検証** — unity-playmode-recorded-playtestスキルで言語切替を跨ぐシナリオ1本（起動→ポーズメニューで言語をEnglishへ切替→ビルドメニュー/配置HUD/流体スロット/実績通知/Tooltipの表示名が英語化されることを録画確認→日本語へ戻す）を実行し、ErrorLogs 0とresult.jsonのpassを確認する（wire契約変更が広範なため、mock検証だけで閉じない）
  - 実施結果: `scenarios/misc/localization-language-switch-via-ui.cs`（commit baee85534）。Success=true / Asserts 7件全PASS / ErrorLogs 0 / 録画10MB。流体名と実績通知のブロック解放系はmod辞書に訳が無く英語化されないため検証対象から外し、代わりに車両名の別名表示（"Locomotive"重複バグの回帰点）を確認対象に加えた

---

## 配置と前例（spec-architecture-review実施済み）

| 配置決定 | 前例（ファイルパス） |
|---|---|
| content_keys.csv を `Localization/` 直下へ | `Localization/localization.csv`・`localization_settings.csv`（辞書系CSVの既存置き場。VanillaSchemaはスキーマ専用のため不可） |
| C#/TS両側生成 | vanillaキーの `LocalizationSourceEmitter`（C#）＋`generate-localization-keys.mjs`（TS）＋`localizationKeysFreshness.test.ts`（一致強制）と同型 |
| `localizeSelectableTargetName` を `buildMenuGrouping.ts` へ | C10修正方針＋D10裁定。新規ファイル・新規serviceは作らない（reviewer指示の明文） |
| Skit resolverの同期機構 | `BuildMenuTopic.cs:71-90`（boolフラグ+Yield畳み込み。メインスレッド専用の役割同型前例） |
| 原文収集のGame層配置 | `Localize.OverlayMasterSourceTexts` が既に `IReadOnlyDictionary` を受けるpush口を持つ（Localize.cs:84）。基盤は辞書primitiveのみ（Codex High・domain-boundary裁定D5） |
| fluid DTOのGuid化 | 同PRの `BlockDetailDto.BlockName`→`BlockGuid` 移行と同一手順（zod・FluidSlot追従含む） |
| 通知は既存 `Localize.OnLanguageChanged`（UniRx Subject）のみ | 新規イベント機構なし。Update()ポーリングは購読へ置換（AGENTS.md標準） |

データフロー（表示名解決の最終形）: `マスタJSON →（起動時）MasterSourceTextCollector(Client.Game) → Localize辞書snapshot → /api/i18n配信 → webui辞書 → t(<type>.<guid>.<field>)`。ホスト側でのName解決・payload同梱は blueprint名（ユーザー命名）と trainCar（master にname不在・suppressed S1）のみ残る。

機能パリティ（死活表）: 言語切替UI=生存（Task 12でエラー時の再試行が追加）／ビルドメニューconnectTool表示=生存（解決層がWebへ移動・言語切替時はWeb再描画のみで追従が改善）／配置HUD connectTool表示=生存（原文固定→辞書解決に改善）／電線スポイト=未解放時「不成立」へ変化（従来はNREクラッシュのため機能改善であり喪失ではない）／歯車チェーンポール設置延長=生存（`GearChainPoleConnectSystem`は当該connectTool選択中のみ動作し、選択には解放が前提のため未解放分岐は通常到達しない。防御的returnは実質no-op — 到達した場合の従来挙動はGuid.Empty漏出でありreturnの方が安全）／Tooltip全種=生存（キー化・`{p0}`補間。prefab配線4件はtextKey空+localize=trueで移行作業なし）／skit台詞・実績通知・fluid名=生存（辞書解決化）。死ぬ操作なし。

## 判断記録（ADR）

- specのADR: `docs/adr/0005-namespaced-localization-keys-embedded-vanilla-csv.md`・`docs/adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md`（本planはこの2本の決定を変更しない。0006決定5の「trainCar/connectToolは暫定Label維持」のうちconnectTool側をD1裁定で正式にWeb解決へ昇格させる — 実装後にADR 0006へ追記すること（Task 10のコミットに含める））
- D1〜D11裁定: 出所「独立レビューダイジェスト（/tmp/pr-review-1111/index.html）コメント裁定 2026-08-02」— D1=案A / D2=案A / D3=案A / D4=案B / D5〜D11=各カード推奨案
- planning中の追加判断:
  - C3の直し方は「null安全復元」でなく「Try契約統一（未解放時ターゲット不成立）」を採用 — D1案AでNameデリファレンス自体が消えるため、残る欠陥は`Guid.Empty`ターゲットの漏出であり、bug-fix-intent指摘の「片側だけ守る現状が最悪＝両者の契約を一致させる」に従った（出所: レビュー指摘の修正方針・agent判断）
  - D7（型付き化）はD3の生成器実装に統合 — 生成物を最初から型付きで出す方が二度手間がない（agent判断）
  - Task 6のskit言語集合契約テストは、D番号裁定に含まれないFable全般レビューの推奨案B（テストで縛る）を採用（出所: シミュレーター予測→ユーザー承認 2026-08-02・AskUserQuestion「追加する（推奨）」）
  - D8のwire側は単一エンドポイント維持＋`SOURCE_LOCALE`定数化に留める — 型分離はC#snapshot内で完結させ、HTTP境界の分割（/api/i18n-source新設）は行わない（出所: シミュレーター予測→ユーザー承認 2026-08-02・AskUserQuestion「単一エンドポイント維持（推奨）」。棄却案の記録: .decisions/2026-08-02-source-locale-wire-and-skit-language-contract.md）
  - 裁定なきWarning群は本planスコープ外として明記（Global Constraints末尾）— 勝手に直して差分を膨らませない（agent判断）
  - Task 12の「同一locale+revisionで1回リトライ（5秒）」はD2裁定（status消費）の外側の追加機構 — Codex High「fetch失敗の無リトライ→持続不整合」の解消として残す（出所: シミュレーター指摘→agent判断・拒否権つき）
  - Task 3のGearChainPoleConnect未解放分岐は「到達しない防御分岐」と判断しreturnで統一 — 選択中のconnectToolは解放済みが前提のため（出所: シミュレーター指摘→agent判断・死活表に根拠明記）
  - Task 19に言語切替を跨ぐrecorded playtest 1本を追加（シミュレーター指摘→agent判断）
