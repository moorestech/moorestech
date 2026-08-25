# 翻訳漏れ・誤訳の修正とドイツ語ロケール新設 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ローカライズ経路に載っていない生の日本語リテラル14箇所をCSV経由に載せ、明確な誤訳・誤字5件を正し、ドイツ語ロケールを新設する。

**Architecture:** 文言の正本は `Localization/localization.csv`（vanilla UI）と `moorestech_master` mod の `localization.csv`（アイテム/ブロック/研究/チャレンジ/スキット）の2枚のCSV。C#側は SourceGenerator が吐く `LocalizationKeys.*` の型付きキーを `Localize.Get` / `Localize.GetFormatted` に渡すだけで、表示文字列を持たない。言語の追加はCSVの列追加と `localization_settings.csv` の1行追加で完結し、言語コードのハードコードは無い（`LocalizationLanguageContract` が english 列の存在のみ必須化）。

**Tech Stack:** Unity 2022 (C#) / mooresmaster SourceGenerator / React+TypeScript (moorestech_web/webui) / pnpm / NUnit / uloop CLI

## Requirements

設計の正本は `docs/adr/0034-localization-gap-fixes-and-german-locale.md`。以下は各行が受け入れ基準を兼ねる。

1. ローディング画面の進捗ログ10行が `ui.loading.*` キー経由で表示される — 対象4ファイルに日本語文字列リテラルが1つも残らない
2. メインメニューのサーバー接続エラー4件が `ui.mainMenu.connect*` キー経由で表示される — `ConnectServer.cs` に日本語文字列リテラルが1つも残らない
3. `{p0}` 位置パラメータの埋め込みが `Client.Localization` の1箇所で行われる — `MouseCursorTooltip` の private 実装が消え、複製が存在しない
4. mod CSV の誤字2件が直る — 「いい加減目を冷ましてください」→「目を覚ましてください」、「ICチップ基盤」→「ICチップ基板」
5. mod CSV の誤訳1件が直る — `skit.100_start_game.17.body` の英語から原文に無い "broken" が消える
6. 回転生成機（電力→回転）の英語名が `Rotation Generator` から `Electric Gear Motor` へ変わる — 回転発電機（`Rotary Generator`）との取り違えが解消し、参照している研究説明文も追随する
7. 研究名「スマート分岐器 / Smart Splitter」が実ブロック名「フィルター分岐器 / Filter Splitter」に日英とも揃う
8. `Localization/localization_settings.csv` に `german,Deutsch,de` の行がある
9. `Localization/localization.csv` の全233行に非空の german 列がある
10. mod `localization.csv` の全425行に german 列がある（`skit.100_start_game.31.overrideCharacterName` のみ他言語と同じ U+3000）
11. `Skit/i18n/german.json` が存在し Addressable に `Vanilla/Skit/i18n/german` として登録されている — `SkitLocalizationDynamicLoadContractTest` が通る
12. ドイツ語訳が codex-audit のネイティブ視点レビューを1周通っている — 明らかな誤りは取り込み済み、判断がつかない争点はPR本文に列挙されている
13. 新設・修正した英語行が表記規約に従う — ラベル・タイトルは Title Case、説明文は sentence case + 末尾ピリオド
14. `pnpm gen:i18n` 済みで `localizationKeysFreshness.test.ts` が通る
15. コンパイルが通り、`Localization` 関連のEditModeテストが全て通る
16. PRが本repo1本 + `moorestech_master` 1本の計2本で、`.moorestech-external-revisions.json` のピンが master repo のPRが指すpush済みコミットを指す

### やらないこと（スコープ境界）

- 既存658行の表記スタイル一括統一（大文字化・末尾ピリオド・Liquid/Fluid・「持ち物」/「インベントリ」割れ）→ `moorestech-fpvl`
- 恒久非表示uGUIビューの日本語直書き約40箇所の削除 → `moorestech-hut5`
- `Skit/i18n/english.json` の日本語残存2件・欠落71件・キー食い違い1件の穴埋め → `moorestech-9ls3`
- ドイツ語UIのレイアウト崩れ検証 → `moorestech-iido`
- `dictionaryIndependentText.ts` の英日併記（意図的に据え置き）
- 電線プレビュー文言・キャラクター名 — master で対応済みのため作業なし

## Global Constraints

- **1ファイル200行以下。** `partial` 禁止。`Func<>` 禁止。`try-catch` は外部境界のみ（既存の `ConnectServer.Connect` の socket 周りは外部境界なので現状維持、新規追加はしない）。
- **デフォルト引数禁止。** 引数を足すときは呼び出し側を全て直す。
- **コメントは日本語1行→英語1行の2行セット**を3〜10行ごと。日本語は処理・変数20字、メソッド30字を目安。自明なコメントは書かない。
- **`.cs` を変更したら必ずコンパイルを実行する**: `uloop compile --project-path ./moorestech_client`
- **`.meta` は手動作成しない。** Unityが生成したものだけコミットする。
- **Prefab・シーン・`.asset`（AddressableAssetGroup含む）をテキスト編集しない。** 変更は `uloop execute-dynamic-code` 経由でのみ行う。
- **英語表記規約（本タスクで新設・修正する行のみに適用）**: ボタン・タブ・研究名・チャレンジtitle などのラベルは Title Case。summary・description・エラーメッセージなどの文は sentence case + 末尾ピリオド。
- **CSVの列数契約**: C#（`LocalizationCsvParser`）とJS（`scripts/localizationCsvRecords.mjs`）の双方が「全レコードのフィールド数 == ヘッダのフィールド数」を要求する。german 列を足すときは1行の取りこぼしも許されない。
- **言語コードは `source` を予約語として使えない**（`LocalizationLanguageContract`）。`english` 列は必ず1つ必要。
- **作業場所**: `moores-wt new` で作った使い捨てworktree。メインワークツリーでブランチを切らない（hookが物理拒否する）。
- **master data のピン**: 本repoの `.moorestech-external-revisions.json` は `moorestech_master` のPRが指す **push済みコミット** を指すこと。ローカルコミット止まりは禁止。
- **Beads**: 着手時にこのタスクのissue `moorestech-amjc` を claim し、完了時に close する（`bd` コマンドは装飾なしの単独実行で打つ。hookがパイプ・リダイレクト混在を拒否する）。

---

## Task 1: `{p0}` 埋め込みを `Client.Localization` へ集約する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs:84`, 同ファイルの `InterpolateTextParams`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs`

**Interfaces:**
- Consumes: 既存 `Localize.Get(LocalizationKey key) -> string`
- Produces: `public static string Localize.GetFormatted(LocalizationKey key, IReadOnlyList<string> textParams)` — 辞書テンプレートを引いた上で `{p0}`, `{p1}`, … を `textParams[0]`, `textParams[1]`, … で置換して返す。Task 2 / Task 3 がこれを使う。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs` のクラス内へ追記する。

```csharp
        [Test]
        public void GetFormattedFillsPositionalParams()
        {
            Localize.Initialize();

            // {p0}を持つ既存キーで位置パラメータ埋めを確認する
            // Verify positional filling with an existing key that carries {p0}
            var text = Localize.GetFormatted(
                LocalizationKeys.Ui.Tooltip.PlaceWireCost,
                new[] { "3" });

            StringAssert.Contains("3", text);
            StringAssert.DoesNotContain("{p0}", text);
        }

        [Test]
        public void GetFormattedLeavesTemplateIntactWithoutParams()
        {
            Localize.Initialize();

            Assert.AreEqual(
                Localize.Get(LocalizationKeys.Ui.Common.Close),
                Localize.GetFormatted(LocalizationKeys.Ui.Common.Close, System.Array.Empty<string>()));
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LocalizeTest"`
Expected: FAIL（`Localize` に `GetFormatted` が無くコンパイルエラー）

- [ ] **Step 3: `Localize.GetFormatted` を実装する**

`Localize.cs` の `GetContent` の直後へ追加する。ファイル先頭の using に `System.Collections.Generic` は既にある。

```csharp
        // 辞書テンプレートの{p0}プレースホルダを埋める（Web側translatorと同じ規約）
        // Fill the {p0} placeholders of the dictionary template, matching the web translator convention
        public static string GetFormatted(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            var text = Get(key);
            for (var index = 0; index < textParams.Count; index++)
            {
                text = text.Replace($"{{p{index}}}", textParams[index]);
            }

            return text;
        }
```

- [ ] **Step 4: `MouseCursorTooltip` の複製を消す**

`MouseCursorTooltip.cs:84` を書き換える。

変更前:
```csharp
            itemName.text = string.Join("\n", lines.Select(line => InterpolateTextParams(Localize.Get(line.Key), line.TextParams)));
```

変更後:
```csharp
            itemName.text = string.Join("\n", lines.Select(line => Localize.GetFormatted(line.Key, line.TextParams)));
```

同ファイルの private メソッド `InterpolateTextParams` を、直上の2行セットコメントごと削除する。

- [ ] **Step 5: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LocalizeTest"`
Expected: PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Localization/Localize.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs
git commit -m "refactor(localization): {p0}埋め込みをLocalize.GetFormattedへ集約"
```

---

## Task 2: ローディング画面の進捗ログ10行をローカライズする

**Files:**
- Modify: `Localization/localization.csv`（10行追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:140,166`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/ServerConnectionInitializer.cs:40,58`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/ModAssetLoader.cs:105,114,124,134`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/ModAssetIconLoader.cs:67,86`
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`pnpm gen:i18n` の出力）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs`

**Interfaces:**
- Consumes: `Localize.GetFormatted(LocalizationKey, IReadOnlyList<string>)`（Task 1）
- Produces: 生成される型付きキー `LocalizationKeys.Ui.Loading.ServerConnected` / `InitialDataFetched` / `BlockAssetsLoaded` / `ItemImagesLoaded` / `ConnectToolImagesLoaded` / `FluidImagesLoaded` / `BlockScreenshotsCaptured` / `TrainCarScreenshotsCaptured` / `TerrainReady` / `InitializationFailed`。後続タスクは参照しない。

`Client.Starter.asmdef` は既に `Client.Localization` を参照しているため asmdef 変更は不要。

- [ ] **Step 1: 失敗するテストを書く**

`LocalizeTest.cs` へ追記する。

```csharp
        [Test]
        public void LoadingKeysResolveInEveryLanguage()
        {
            Localize.Initialize();

            var keys = new[]
            {
                LocalizationKeys.Ui.Loading.ServerConnected,
                LocalizationKeys.Ui.Loading.InitialDataFetched,
                LocalizationKeys.Ui.Loading.BlockAssetsLoaded,
                LocalizationKeys.Ui.Loading.ItemImagesLoaded,
                LocalizationKeys.Ui.Loading.ConnectToolImagesLoaded,
                LocalizationKeys.Ui.Loading.FluidImagesLoaded,
                LocalizationKeys.Ui.Loading.BlockScreenshotsCaptured,
                LocalizationKeys.Ui.Loading.TrainCarScreenshotsCaptured,
                LocalizationKeys.Ui.Loading.TerrainReady,
                LocalizationKeys.Ui.Loading.InitializationFailed,
            };

            // 欠落キーは[!key]マーカーで返るため、それを弾く
            // Missing keys come back as a [!key] marker, so reject those
            foreach (var key in keys)
            {
                var text = Localize.Get(key);
                Assert.IsNotEmpty(text, key.Key);
                StringAssert.DoesNotStartWith("[!", text, key.Key);
            }
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LoadingKeysResolveInEveryLanguage"`
Expected: FAIL（`LocalizationKeys.Ui.Loading` が存在せずコンパイルエラー）

- [ ] **Step 3: `Localization/localization.csv` へ10行を追記する**

ファイル末尾へ以下をそのまま追加する（列は `key,Source,english,japanese` の4つ。Source列は english と同じ値を置く。既存 vanilla 行と同じ規約）。

```csv
ui.loading.serverConnected,Connected to server  {p0},Connected to server  {p0},サーバーとの接続完了  {p0}
ui.loading.initialDataFetched,Initial data received  {p0},Initial data received  {p0},初期データ取得完了  {p0}
ui.loading.blockAssetsLoaded,Block assets loaded  {p0},Block assets loaded  {p0},ブロックアセットロード完了  {p0}
ui.loading.itemImagesLoaded,Item images loaded  {p0},Item images loaded  {p0},アイテム画像ロード完了  {p0}
ui.loading.connectToolImagesLoaded,Connect tool images loaded  {p0},Connect tool images loaded  {p0},接続ツール画像ロード完了  {p0}
ui.loading.fluidImagesLoaded,Fluid images loaded  {p0},Fluid images loaded  {p0},液体画像ロード完了  {p0}
ui.loading.blockScreenshotsCaptured,Block screenshots captured  {p0},Block screenshots captured  {p0},ブロックスクリーンショット完了  {p0}
ui.loading.trainCarScreenshotsCaptured,Train car screenshots captured  {p0},Train car screenshots captured  {p0},車両スクリーンショット完了  {p0}
ui.loading.terrainReady,Terrain data ready ({p0} chunks)  {p1},Terrain data ready ({p0} chunks)  {p1},地形データ準備完了({p0}チャンク取得)  {p1}
ui.loading.initializationFailed,Initialization failed. Returning to the main menu.,Initialization failed. Returning to the main menu.,初期化に失敗しました。メインメニューに戻ります。
```

- [ ] **Step 4: `ServerConnectionInitializer.cs` を書き換える**

ファイル先頭の using へ `using Client.Localization;` と `using Mooresmaster.Localization.Generated;` を（未定義なら）追加する。

40行目 変更前:
```csharp
            _loadingLog.text += $"\nサーバーとの接続完了  {_loadingStopwatch.Elapsed}";
```
変更後:
```csharp
            _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.ServerConnected, new[] { _loadingStopwatch.Elapsed.ToString() });
```

58行目 変更前:
```csharp
            _loadingLog.text += $"\n初期データ取得完了  {_loadingStopwatch.Elapsed}";
```
変更後:
```csharp
            _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.InitialDataFetched, new[] { _loadingStopwatch.Elapsed.ToString() });
```

- [ ] **Step 5: `ModAssetLoader.cs` を書き換える**

using を同様に追加した上で、105 / 114 / 124 / 134 行目を順に置換する。

```csharp
                _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.BlockAssetsLoaded, new[] { _loadingStopwatch.Elapsed.ToString() });
```
```csharp
                _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.ItemImagesLoaded, new[] { _loadingStopwatch.Elapsed.ToString() });
```
```csharp
                _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.ConnectToolImagesLoaded, new[] { _loadingStopwatch.Elapsed.ToString() });
```
```csharp
                _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.FluidImagesLoaded, new[] { _loadingStopwatch.Elapsed.ToString() });
```

- [ ] **Step 6: `ModAssetIconLoader.cs` を書き換える**

67 / 86 行目。

```csharp
            _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.BlockScreenshotsCaptured, new[] { _loadingStopwatch.Elapsed.ToString() });
```
```csharp
            _loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.TrainCarScreenshotsCaptured, new[] { _loadingStopwatch.Elapsed.ToString() });
```

- [ ] **Step 7: `InitializeScenePipeline.cs` を書き換える**

140行目 変更前:
```csharp
                loadingLog.text += "\n初期化に失敗しました。メインメニューに戻ります。";
```
変更後:
```csharp
                loadingLog.text += "\n" + Localize.Get(LocalizationKeys.Ui.Loading.InitializationFailed);
```

166行目 変更前:
```csharp
                loadingLog.text += $"\n地形データ準備完了({fetchedChunkCount}チャンク取得)  {loadingStopwatch.Elapsed}";
```
変更後:
```csharp
                loadingLog.text += "\n" + Localize.GetFormatted(LocalizationKeys.Ui.Loading.TerrainReady, new[] { fetchedChunkCount.ToString(), loadingStopwatch.Elapsed.ToString() });
```

- [ ] **Step 8: 日本語リテラルが残っていないことを確認する**

Run:
```bash
grep -nP '"[^"]*[\x{3040}-\x{30ff}\x{4e00}-\x{9faf}]' \
  moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs \
  moorestech_client/Assets/Scripts/Client.Starter/Initialization/ServerConnectionInitializer.cs \
  moorestech_client/Assets/Scripts/Client.Starter/Initialization/ModAssetLoader.cs \
  moorestech_client/Assets/Scripts/Client.Starter/Initialization/ModAssetIconLoader.cs
```
Expected: `Debug.LogError` 行以外にヒットが無いこと（`InitializeScenePipeline.cs:134` の `Debug.LogError($"初期化処理中にエラーが発生しました: ...")` は開発向けログなので対象外）

- [ ] **Step 9: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [ ] **Step 10: webui のキーを再生成する**

Run:
```bash
cd moorestech_web/webui && pnpm gen:i18n && pnpm test -- localizationKeysFreshness
```
Expected: PASS。`src/shared/i18n/generated/localizationKeys.ts` に差分が出る。

- [ ] **Step 11: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LocalizeTest"`
Expected: PASS

- [ ] **Step 12: コミットする**

```bash
git add Localization/localization.csv \
        moorestech_client/Assets/Scripts/Client.Starter \
        moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs \
        moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat(localization): ローディング進捗ログ10行をui.loading.*へ載せる"
```

---

## Task 3: メインメニューのサーバー接続エラー4件をローカライズする

**Files:**
- Modify: `Localization/localization.csv`（4行追加）
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs:32,39,45,67`
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`pnpm gen:i18n` の出力）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs`

**Interfaces:**
- Consumes: `Localize.Get` / `Localize.GetFormatted`（Task 1）
- Produces: `LocalizationKeys.Ui.MainMenu.ConnectInvalidIp` / `ConnectPortTooLarge` / `ConnectPortTooSmall` / `ConnectFailed`。後続タスクは参照しない。

`Client.MainMenu` に asmdef は無く Assembly-CSharp に属する。`Client.Localization` は `autoReferenced: true` なので参照追加は不要（同ディレクトリの `LanguageSetting.cs` が既に `using Client.Localization;` している）。

- [ ] **Step 1: 失敗するテストを書く**

`LocalizeTest.cs` へ追記する。

```csharp
        [Test]
        public void MainMenuConnectErrorKeysResolve()
        {
            Localize.Initialize();

            var keys = new[]
            {
                LocalizationKeys.Ui.MainMenu.ConnectInvalidIp,
                LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge,
                LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall,
                LocalizationKeys.Ui.MainMenu.ConnectFailed,
            };

            foreach (var key in keys)
            {
                var text = Localize.Get(key);
                Assert.IsNotEmpty(text, key.Key);
                StringAssert.DoesNotStartWith("[!", text, key.Key);
            }
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MainMenuConnectErrorKeysResolve"`
Expected: FAIL（キーが存在せずコンパイルエラー）

- [ ] **Step 3: `Localization/localization.csv` へ4行を追記する**

ファイル末尾へ追加する。

```csv
ui.mainMenu.connectInvalidIp,The IP address is not valid.,The IP address is not valid.,IPアドレスが正しくありません。
ui.mainMenu.connectPortTooLarge,The port number must be 65535 or lower.,The port number must be 65535 or lower.,ポート番号は65535以下である必要があります。
ui.mainMenu.connectPortTooSmall,The port number must be greater than 1024.,The port number must be greater than 1024.,ポート番号は1024より大きい必要があります。
ui.mainMenu.connectFailed,Failed to connect to the server.\n{p0},Failed to connect to the server.\n{p0},サーバーへの接続に失敗しました。\n{p0}
```

注: 実装は `if (port <= 1024)` で 1024 自体を弾くため、旧文言の「1024以上である必要があります」は実装と食い違っていた。実装を正として日英とも「1024より大きい」に直す。

- [ ] **Step 4: `ConnectServer.cs` を書き換える**

先頭の using へ追加する。
```csharp
using Client.Localization;
using Mooresmaster.Localization.Generated;
```

32 / 39 / 45 / 67 行目を順に置換する。

```csharp
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectInvalidIp));
```
```csharp
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge));
```
```csharp
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall));
```
```csharp
                serverConnectPopup.SetText(Localize.GetFormatted(LocalizationKeys.Ui.MainMenu.ConnectFailed, new[] { e.ToString() }));
```

既存の `try-catch` は socket 接続という外部境界の隔離であり、そのまま残す。

- [ ] **Step 5: 日本語リテラルが残っていないことを確認する**

Run:
```bash
grep -nP '"[^"]*[\x{3040}-\x{30ff}\x{4e00}-\x{9faf}]' moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs
```
Expected: ヒット0件

- [ ] **Step 6: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [ ] **Step 7: webui のキーを再生成する**

Run:
```bash
cd moorestech_web/webui && pnpm gen:i18n && pnpm test -- localizationKeysFreshness
```
Expected: PASS

- [ ] **Step 8: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LocalizeTest"`
Expected: PASS

- [ ] **Step 9: コミットする**

```bash
git add Localization/localization.csv \
        moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs \
        moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat(localization): メインメニュー接続エラー4件をui.mainMenu.connect*へ載せる"
```

---

## Task 4: mod CSV の誤訳・誤字5件を直す（`moorestech_master` repo）

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

このタスクは `moorestech_master` リポジトリ側の作業。本repoからは `.moorestech-external-revisions.json` のピン更新（Task 7）で接続する。

**Interfaces:**
- Consumes: なし
- Produces: なし（データのみ）

作業前に `moorestech_master` 側でブランチを切る:
```bash
git -C ../moorestech_master fetch origin
git -C ../moorestech_master switch -c fix/localization-mistranslations-and-german origin/master
```

- [ ] **Step 1: 修正前の状態を記録する**

Run:
```bash
cd ../moorestech_master
grep -n "目を冷まして\|ICチップ基盤\|broken promises\|Rotation Generator\|Smart Splitter\|スマート分岐器" \
  server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
```
Expected: 6行ヒット（skit.33 / item基盤 / skit.17 / block回転生成機 / research回転生成機description / research スマート分岐器 name+description）

- [ ] **Step 2: 誤字2件を直す**

`skit.100_start_game.33.body` 行の Source列と japanese列の両方で「いい加減目を冷ましてください！」→「いい加減目を覚ましてください！」に置換する（english列は既に "wake up" で正しいので触らない）。

`item.019e3b03-0dc4-7328-a74d-684aa710f1a8.name` 行の Source列と japanese列で「ICチップ基盤」→「ICチップ基板」に置換する（english列 `IC Chip Substrate` は正しいので触らない）。

- [ ] **Step 3: 誤訳1件を直す**

`skit.100_start_game.17.body` の english列を変更する。

変更前:
```
I would never joke with you, Princess.\nI absolutely despise broken promises and jokes.
```
変更後:
```
I would never joke with you, Princess.\nI absolutely despise promises and jokes.
```

原文「ぼくは約束と冗談が大っ嫌いなんです」に無い "broken" を落とす。Source列・japanese列は触らない。

- [ ] **Step 4: 回転生成機の英語名を直す**

`block.019e9b5b-fa65-70a6-b45f-525f284d012a.name` の english列を `Rotation Generator` → `Electric Gear Motor` に変更する（Source列「回転生成機」・japanese列は据え置き）。

`research.019ea6d1-edc4-7694-8e3c-3f366d6437ed.description` の english列を変更する。

変更前:
```
Unlocks the Rotation Generator, which produces rotational power from electricity
```
変更後:
```
Unlocks the Electric Gear Motor, which produces rotational power from electricity.
```

（説明文なので表記規約に従い末尾ピリオドを付ける）

回転発電機 `block.019e158c-df55-7001-be7c-eca97046ca41.name` の `Rotary Generator` は据え置き。

- [ ] **Step 5: 研究名をブロック名へ揃える**

`research.019e3aab-32c6-7166-a184-d761153c2498.name` を変更する。
- Source列: `スマート分岐器` → `フィルター分岐器`
- english列: `Smart Splitter` → `Filter Splitter`
- japanese列: `スマート分岐器` → `フィルター分岐器`

`research.019e3aab-32c6-7166-a184-d761153c2498.description` を変更する。
- Source列 / japanese列: `条件で搬送先を切り替えるスマート分岐器を解放する` → `条件で搬送先を切り替えるフィルター分岐器を解放する`
- english列: `Unlocks the Smart Splitter, which switches destinations based on conditions` → `Unlocks the Filter Splitter, which switches destinations based on conditions.`

- [ ] **Step 6: 取りこぼしが無いことを確認する**

Run:
```bash
cd ../moorestech_master
grep -c "目を冷まして\|ICチップ基盤\|broken promises\|Rotation Generator\|Smart Splitter\|スマート分岐器" \
  server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
```
Expected: `0`

Run（列数が壊れていないことの確認）:
```bash
python3 -c "
import csv
rows=list(csv.reader(open('server_v8/mods/moorestechAlphaMod_8/localization/localization.csv',encoding='utf-8')))
assert all(len(r)==len(rows[0]) for r in rows), 'column count mismatch'
print(len(rows)-1, 'rows OK')
"
```
Expected: `425 rows OK`

- [ ] **Step 7: コミットする**

```bash
cd ../moorestech_master
git add server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git commit -m "fix(localization): 誤字2件・誤訳1件・紛らわしいブロック名2件を直す"
```

---

## Task 5: vanilla辞書にドイツ語を追加する

**Files:**
- Modify: `Localization/localization_settings.csv`（1行追加）
- Modify: `Localization/localization.csv`（ヘッダ含む234行すべてに1列追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs:104`
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`pnpm gen:i18n` の出力。列追加ではキーは変わらないが、差分が出たらコミットする）

**Interfaces:**
- Consumes: なし
- Produces: 言語コード `german` が `LanguageCatalog.Languages` / `Localize.GetLanguageCodes()` に現れる。Task 6 の Skit 辞書アセットと Task 7 の mod CSV 列がこれに対応する。

**Task 5 と Task 6 は分けてコミットしない（Task 6 Step 5 でまとめる）。** `LanguageCatalogCodeEmitter.ValidateLanguageSet` が「`localization_settings.csv` の行数 == `localization.csv` の言語列数」を要求するため、settings行の追加と german 列の追加は**同一ステップ群で行う**（片方だけだとSourceGeneratorが失敗しコンパイルが通らない）。また `SkitLocalizationDynamicLoadContractTest` は Task 6 で `german.json` を置くまで赤のままになる。

訳文は手打ちでCSVへ差し込まず、`key<TAB>german` の2列TSVを作ってからスクリプトで列追加する。1行の取りこぼしがCSVの列数契約違反になるため、機械的に突き合わせる。

- [ ] **Step 1: 失敗するテストを書く**

`LocalizeTest.cs` へ追記する。

```csharp
        [Test]
        public void EveryVanillaKeyHasNonEmptyTextInEveryLanguage()
        {
            Localize.Initialize();

            foreach (var languageCode in Localize.GetLanguageCodes())
            {
                Assert.IsTrue(Localize.TryGetDictionary(languageCode, out var dictionary), languageCode);
                foreach (var pair in dictionary)
                {
                    Assert.IsNotEmpty(pair.Value, $"{languageCode}:{pair.Key}");
                }
            }
        }
```

あわせて `LocalizeTest.cs:104` の言語集合を更新する。

変更前:
```csharp
            CollectionAssert.AreEqual(new[] { "english", "japanese" }, Localize.GetLanguageCodes());
```
変更後:
```csharp
            CollectionAssert.AreEqual(new[] { "english", "japanese", "german" }, Localize.GetLanguageCodes());
```

順序は `localization_settings.csv` の行順に一致する。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LocalizeTest"`
Expected: FAIL（`GetLanguageCodes` が `german` を返さない）

- [ ] **Step 3: 訳文TSVを作る**

`Localization/localization.csv` の `key` と `english` を突き合わせ、全233行ぶんのドイツ語をスクラッチ領域の `german_vanilla.tsv`（`key<TAB>german` のタブ区切り）へ書き出す。

方針:
- **英語列を原文とする。** `{p0}` `{itemId}` `{shortcut}` などのプレースホルダは字面を変えずそのまま残す。`\n` も原文どおり残す。
- ラベル・タイトルは名詞句（`Bauplan kopieren`, `Baumenü`）。説明文は文として末尾ピリオドを付ける。
- 人称は `Sie`（敬称）。プレイヤーへの指示文も `Sie` に揃える（スキットのAIと表記を割らない）。
- `→` `×` `#{id}` のような記号のみの行は英語をそのまま複写する。

Run（プレースホルダ照合。訳し終えてから実行する）:
```bash
python3 - <<'EOF'
import csv, re
SP = "german_vanilla.tsv"
ph = re.compile(r"\{[^}]+\}")
german = dict(line.rstrip("\n").split("\t", 1) for line in open(SP, encoding="utf-8"))
rows = list(csv.DictReader(open("Localization/localization.csv", encoding="utf-8")))
missing = [r["key"] for r in rows if r["key"] not in german]
assert not missing, f"missing german: {missing}"
extra = [k for k in german if k not in {r["key"] for r in rows}]
assert not extra, f"unknown key: {extra}"
for r in rows:
    a, b = sorted(ph.findall(r["english"])), sorted(ph.findall(german[r["key"]]))
    assert a == b, f"placeholder mismatch {r['key']}: {a} vs {b}"
    assert r["english"].count("\\n") == german[r["key"]].count("\\n"), f"newline mismatch {r['key']}"
print(len(rows), "rows verified")
EOF
```
Expected: `233 rows verified`

- [ ] **Step 4: settings行と german 列を同時に入れる**

`Localization/localization_settings.csv` を以下の全文にする。
```csv
lang_name,display_name,steam_api_lang_code
english,English,en
japanese,日本語,ja
german,Deutsch,de
```

続けて german 列を追記する。
```bash
python3 - <<'EOF'
import csv
SP = "german_vanilla.tsv"
german = dict(line.rstrip("\n").split("\t", 1) for line in open(SP, encoding="utf-8"))
path = "Localization/localization.csv"
with open(path, encoding="utf-8", newline="") as f:
    rows = list(csv.reader(f))
rows[0].append("german")
for row in rows[1:]:
    row.append(german[row[0]])
with open(path, "w", encoding="utf-8", newline="") as f:
    csv.writer(f, lineterminator="\n").writerows(rows)
print("appended german column to", len(rows) - 1, "rows")
EOF
```
Expected: `appended german column to 233 rows`

- [ ] **Step 5: 列数と空欄を検査する**

Run:
```bash
python3 -c "
import csv
rows=list(csv.reader(open('Localization/localization.csv',encoding='utf-8')))
assert rows[0]==['key','Source','english','japanese','german'], rows[0]
assert all(len(r)==5 for r in rows), 'column count mismatch'
assert all(r[4].strip() for r in rows[1:]), 'empty german cell'
print(len(rows)-1,'rows OK')
"
```
Expected: `233 rows OK`

- [ ] **Step 6: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0（settings行と言語列の数が揃っているので `ValidateLanguageSet` を通過する）

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LocalizeTest"`
Expected: PASS

- [ ] **Step 8: Task 6 へ進む（ここではコミットしない）**

`SkitLocalizationDynamicLoadContractTest` は `german.json` を置くまで赤のままなので、Task 6 の完了後にまとめてコミットする。

---

## Task 6: Skit辞書 `german.json` を作り Addressable へ登録する

**Files:**
- Create: `moorestech_client/Assets/AddressableResources/Skit/i18n/german.json`
- Modify: AddressableAssetGroup（`uloop execute-dynamic-code` 経由。ファイルを直接編集しない）

**Interfaces:**
- Consumes: Task 5 が追加した言語コード `german`（`LanguageCatalog.Languages`）
- Produces: Addressable address `Vanilla/Skit/i18n/german`

`SkitLocalizationDynamicLoadContractTest.AddressableSettingsContainOnlySupportedSkitDictionaryAddresses` が `LanguageCatalog.Languages` と `Vanilla/Skit/i18n/*` の完全一致を要求するため、この Task は言語追加の必須随伴物である。

- [ ] **Step 1: テストが失敗していることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "SkitLocalizationDynamicLoadContractTest"`
Expected: FAIL（`Vanilla/Skit/i18n/german` が Addressable に無い）

- [ ] **Step 2: `german.json` を english.json の複写として作る**

Run（worktree のルートで実行する）:
```bash
python3 - <<'EOF'
import json, collections
src = "moorestech_client/Assets/AddressableResources/Skit/i18n/english.json"
dst = "moorestech_client/Assets/AddressableResources/Skit/i18n/german.json"
root = json.load(open(src, encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
root["locale"] = "de"
root["name"] = "Deutsch"
with open(dst, "w", encoding="utf-8") as f:
    json.dump(root, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("written", dst)
EOF
```

`translations` は英語のまま（ADR 0034「english.json を複写し locale/name だけ独語にする」）。

- [ ] **Step 3: Unityに読み込ませて `.meta` を生成し、Addressable へ登録する**

`.meta` は手動作成しない。Unity Editor を起動してインポートさせたうえで、Addressable 登録を `uloop execute-dynamic-code` で行う。

Run:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

AssetDatabase.Refresh();
var settings = AddressableAssetSettingsDefaultObject.Settings;
var assetPath = "Assets/AddressableResources/Skit/i18n/german.json";
var guid = AssetDatabase.AssetPathToGUID(assetPath);

// 既存english辞書と同じグループへ入れ、addressだけ言語コードで分ける
// Put it in the same group as the existing English dictionary, differing only by the language code in the address
var englishGuid = AssetDatabase.AssetPathToGUID("Assets/AddressableResources/Skit/i18n/english.json");
var englishEntry = settings.FindAssetEntry(englishGuid);
var entry = settings.CreateOrMoveEntry(guid, englishEntry.parentGroup);
entry.address = "Vanilla/Skit/i18n/german";
settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
AssetDatabase.SaveAssets();
Debug.Log("registered " + entry.address + " group=" + entry.parentGroup.Name);
'
```
Expected: `registered Vanilla/Skit/i18n/german group=...` のログ

- [ ] **Step 4: コンパイルとテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Localization|Localize"`
Expected: PASS（`SkitLocalizationDynamicLoadContractTest` と `EveryVanillaKeyHasNonEmptyTextInEveryLanguage` を含む）

Run:
```bash
cd moorestech_web/webui && pnpm gen:i18n && pnpm test
```
Expected: PASS

- [ ] **Step 5: Task 5 の変更とまとめてコミットする**

```bash
git add Localization/localization_settings.csv Localization/localization.csv \
        moorestech_client/Assets/AddressableResources/Skit/i18n/german.json \
        moorestech_client/Assets/AddressableResources/Skit/i18n/german.json.meta \
        moorestech_client/Assets/AddressableAssetsData \
        moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeTest.cs \
        moorestech_web/webui/src/shared/i18n/generated
git commit -m "feat(localization): ドイツ語ロケールを新設しvanilla辞書へgerman列を追加"
```

---

## Task 7: mod CSV に german 列を追加し、ピンを更新する

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`（ヘッダ含む426行すべてに1列追加）
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 4 が直した mod CSV、Task 5 が追加した言語コード `german`
- Produces: なし

- [ ] **Step 1: 訳文TSVを作る**

Task 6 と同じ方針で、mod CSV 425行ぶんの `key<TAB>german` を `german_mod.tsv` へ書き出す。

追加の方針:
- **アイテム・ブロック名**はドイツ語の複合名詞として綴る（`Wood Plank` → `Holzbrett`、`Gear Belt Conveyor` → `Zahnrad-Förderband`）。ハイフンで区切ってよいが、1語に潰して極端に長くしない。
- **スキット本文47行**は AI → ヨリの発話を `Sie` + `Prinzessin` で訳す。`\n` の位置は英語と同数に保つ（改行数がUIの行数を決めるため）。
- `skit.100_start_game.31.overrideCharacterName` は他言語と同じ U+3000（全角スペース1文字）を入れる。
- Task 4 で直した5件は**修正後の英語**を原文とする（`Electric Gear Motor` / `Filter Splitter` / "despise promises and jokes"）。

Run（照合。Task 6 Step 3 と同じ検査を mod CSV に対して行う）:
```bash
cd ../moorestech_master
python3 - <<'EOF'
import csv, re
SP = "german_mod.tsv"
ph = re.compile(r"\{[^}]+\}")
german = dict(line.rstrip("\n").split("\t", 1) for line in open(SP, encoding="utf-8"))
path = "server_v8/mods/moorestechAlphaMod_8/localization/localization.csv"
rows = list(csv.DictReader(open(path, encoding="utf-8")))
missing = [r["key"] for r in rows if r["key"] not in german]
assert not missing, f"missing german: {missing}"
for r in rows:
    a, b = sorted(ph.findall(r["english"])), sorted(ph.findall(german[r["key"]]))
    assert a == b, f"placeholder mismatch {r['key']}: {a} vs {b}"
    assert r["english"].count("\\n") == german[r["key"]].count("\\n"), f"newline mismatch {r['key']}"
print(len(rows), "rows verified")
EOF
```
Expected: `425 rows verified`

- [ ] **Step 2: german 列を追記する**

Run:
```bash
cd ../moorestech_master
python3 - <<'EOF'
import csv
german = dict(line.rstrip("\n").split("\t", 1) for line in open("german_mod.tsv", encoding="utf-8"))
path = "server_v8/mods/moorestechAlphaMod_8/localization/localization.csv"
with open(path, encoding="utf-8", newline="") as f:
    rows = list(csv.reader(f))
rows[0].append("german")
for row in rows[1:]:
    row.append(german[row[0]])
with open(path, "w", encoding="utf-8", newline="") as f:
    csv.writer(f, lineterminator="\n").writerows(rows)
print("appended german column to", len(rows) - 1, "rows")
EOF
```
Expected: `appended german column to 425 rows`

- [ ] **Step 3: 列数と空欄を検査する**

Run:
```bash
cd ../moorestech_master
python3 -c "
import csv
p='server_v8/mods/moorestechAlphaMod_8/localization/localization.csv'
rows=list(csv.reader(open(p,encoding='utf-8')))
assert rows[0]==['key','Source','english','japanese','german'], rows[0]
assert all(len(r)==5 for r in rows), 'column count mismatch'
blank=[r[0] for r in rows[1:] if not r[4]]
assert blank==[], blank
print(len(rows)-1,'rows OK')
"
```
Expected: `425 rows OK`

- [ ] **Step 4: `moorestech_master` をコミットして push し、PRを作る**

```bash
cd ../moorestech_master
git add server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git commit -m "feat(localization): mod辞書へgerman列を追加"
git push -u origin fix/localization-mistranslations-and-german
gh pr create --title "翻訳の誤り修正とドイツ語ロケール追加" --body "$(cat <<'BODY'
本repo側 PR と対で入る。ADR: moorestech 側 `docs/adr/0034-localization-gap-fixes-and-german-locale.md`

- 誤字2件（目を冷まして→覚まして / ICチップ基盤→基板）
- 誤訳1件（skit.17 の英語から原文に無い "broken" を削除）
- 回転生成機の英語名を Rotation Generator → Electric Gear Motor（回転発電機 Rotary Generator との取り違え解消）
- 研究名スマート分岐器/Smart Splitter → フィルター分岐器/Filter Splitter（実ブロック名へ統一）
- german 列を425行ぶん追加

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

- [ ] **Step 5: 本repoのピンを push 済みコミットへ更新する**

Run（worktree のルートで実行する）:
```bash
COMMIT=$(git -C ../moorestech_master rev-parse HEAD)
git -C ../moorestech_master branch -r --contains "$COMMIT" | grep -q origin/ && echo "pushed OK"
python3 - <<EOF
import json
p = ".moorestech-external-revisions.json"
data = json.load(open(p, encoding="utf-8"))
for repo in data["repositories"]:
    if repo["key"] == "moorestech_master":
        repo["commitHash"] = "$COMMIT"
with open(p, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)
    f.write("\n")
print("pinned", "$COMMIT")
EOF
```
Expected: `pushed OK` に続いて `pinned <sha>`

注: Unity起動時にこのファイルが書き戻される既知の挙動があるため、`git add` は明示パス指定で行い `git add -A` を使わない。

- [ ] **Step 6: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: master dataピンをドイツ語対応コミットへ更新"
```

---

## Task 8: codex-audit でドイツ語訳をレビューし、指摘を反映する

**Files:**
- Modify: `Localization/localization.csv`（指摘反映分）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`（指摘反映分）

**Interfaces:**
- Consumes: Task 6 / Task 7 で入ったドイツ語658行
- Produces: なし

- [ ] **Step 1: 監査用の入力を作る**

両CSVから `key<TAB>english<TAB>german` の3列TSVをスクラッチ領域へ書き出す。

```bash
python3 - <<'EOF'
import csv
out = open("/tmp/german_audit.tsv", "w", encoding="utf-8")
for path in ["Localization/localization.csv",
             "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv"]:
    for r in csv.DictReader(open(path, encoding="utf-8")):
        out.write(f"{r['key']}\t{r['english']}\t{r['german']}\n")
out.close()
print("written /tmp/german_audit.tsv")
EOF
```

- [ ] **Step 2: codex-audit を起動する**

`codex-audit` スキルを使い、以下を依頼する。

- 立場: 工場建設ゲームのドイツ語ローカライズを検収するネイティブレビュアー
- 入力: `/tmp/german_audit.tsv`（`key / english / german` の3列）
- 見てほしい点: 誤訳・不自然な語順・ゲーム用語として定着していない訳語・敬称(`Sie`)の一貫性・複合名詞の綴り・プレースホルダ(`{p0}` 等)の欠落や増殖・スキット本文の口調の一貫性
- 出力形式: `key / 指摘 / 提案訳 / 重大度(誤り|好み)` のTSV

- [ ] **Step 3: 指摘を裁定して反映する**

- 「誤り」判定は取り込む。
- 「好み」判定は自分の訳を残す。
- 自分で判断がつかないものだけ列挙して、PR本文の「未決の争点」節へ書く（ユーザー裁定 2026-08-25「私が裁定し、判断がつかないものだけ列挙して提示」）。

反映後、Task 6 Step 5 と Task 7 Step 3 の検査コマンドを再実行する。
Expected: `233 rows OK` / `425 rows OK`

- [ ] **Step 4: コンパイルとテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Localization|Localize"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add Localization/localization.csv
git commit -m "fix(localization): codex監査の指摘をドイツ語訳へ反映"
```

```bash
cd ../moorestech_master
git add server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git commit -m "fix(localization): codex監査の指摘をドイツ語訳へ反映"
git push
```

- [ ] **Step 6: master data のピンを再更新する**

Task 7 Step 5 のコマンドを再実行して、push 済みの最新コミットへピンを進める。

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: master dataピンをcodex監査反映後へ更新"
```

---

## Task 9: 全ブランチレビューを実行する（省略不可）

**Files:**
- 変更なし（レビューのみ）

- [ ] **Step 1: `moores-code-review` スキルで全ブランチレビューを実行する**

このタスクは自動実行であり、ゴール文言による省略はできない。指摘を実コード照合のうえ反映し、設計判断が必要なものだけ末尾でユーザーへ諮る。

- [ ] **Step 2: 本repoのPRを作る**

```bash
git push -u origin <ブランチ名>
gh pr create --title "翻訳漏れ・誤訳の修正とドイツ語ロケール新設" --body "$(cat <<'BODY'
ADR: `docs/adr/0034-localization-gap-fixes-and-german-locale.md`
master data 側 PR: <moorestech_master のPR URL>

## 変更
- ローディング画面の進捗ログ10行を `ui.loading.*` へ、メインメニューのサーバー接続エラー4件を `ui.mainMenu.connect*` へ載せた（生の日本語リテラル14箇所が消えた）
- `{p0}` 埋め込みを `Localize.GetFormatted` へ集約し、`MouseCursorTooltip` の複製を削除
- mod辞書の誤字2件・誤訳1件・紛らわしいブロック名2件を修正
- ドイツ語ロケールを新設（`localization_settings.csv` + vanilla 233行 + mod 425行 + `Skit/i18n/german.json`）

## 未決の争点
<Task 8 で判断がつかなかったドイツ語訳をここに列挙する。無ければ「なし」>

## 既知の制限
- `LanguageSetting`（メインメニューの言語ドロップダウン）は `display_name` ではなく言語コードを表示するため、選択肢に `Deutsch` ではなく `german` と出る（既存の欠陥。`moorestech-f84r` で別途対応）
- ドイツ語UIのレイアウト崩れは事前検証していない（ユーザー裁定 2026-08-25「検証しない（崩れたら後日直す）」・`moorestech-iido`）

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

- [ ] **Step 3: bd を閉じる**

`moorestech-amjc` を close する（理由: 「ADR 0034 の実装完了。本repo・master repo の2PRを作成」）。コマンドは装飾なしの単独実行で打つ。

---

## 判断記録（ADR）

設計の正本: `docs/adr/0034-localization-gap-fixes-and-german-locale.md`

裁定記録:
- `.decisions/2026-08-25-ドイツ語は全訳しCodex監査で相互チェックする.md`
- `.decisions/2026-08-25-回転生成機は英語のみElectricMotor系へ改名する.md`
- `.decisions/2026-08-25-german.jsonはenglish複写でlocaleとnameだけ独語にする.md`
- `.decisions/2026-08-25-電線プレビューの文言は通知と別キー群で持つ.md`
- `.decisions/2026-08-25-電線プレビュー文言の裁定はmasterで実装済みだった.md`

planning中に新たに生じた判断:

- **`{p0}` 埋め込みを `Localize.GetFormatted` として `Client.Localization` へ置く（Task 1）。**
  出所: agent前提。既存の実装は `MouseCursorTooltip` の private `InterpolateTextParams` 1箇所のみで、webui 側の translator が同じ規約を別実装で持つ。Task 2/3 で3つ目の複製を作るのを避けるため、辞書レイヤ自身の関心事として `Client.Localization` へ寄せる。ドメイン語彙を共有層へ持ち込む変更ではない（引数は `LocalizationKey` と文字列配列のみ）。
- **`Electric Motor 系`の具体名を `Electric Gear Motor` とする（Task 4）。**
  出所: agent前提。ユーザー裁定は「Electric Motor 系」まで。既存アイテム「モーター / `Motor`」と衝突せず、電力→歯車動力という機能が読める名前を選んだ。
- **ローディングログの経過時間は `{p0}` の位置パラメータで渡す（Task 2）。**
  出所: agent前提。裁定は「全行ローカライズする」まで。時刻書式は言語非依存なので翻訳対象から外し、テンプレート側に埋め込み位置だけ持たせた。
- **`ConnectServer` のポート下限文言を「1024以上」→「1024より大きい」に直す（Task 3）。**
  出所: agent前提。実装は `if (port <= 1024)` で 1024 自体を弾いており、旧文言が実装と食い違っていた。触る行の中の実装との不一致なので、実装を正として文言を直した。
- **settings行の追加と vanilla german 列の追加を同一ステップ群に置き、Task 5 単体ではコミットしない（Task 6 とまとめる）。**
  出所: agent前提。`LanguageCatalogCodeEmitter.ValidateLanguageSet` が「`localization_settings.csv` の行数 == `localization.csv` の言語列数」を要求するため、片方だけ変更するとSourceGeneratorが失敗しコンパイルが通らない（planning中に generator の実装を読んで判明。当初は Task 5=settings / Task 6=列追加 と分けていたのを組み替えた）。また `SkitLocalizationDynamicLoadContractTest` は `german.json` を置くまで赤のままなので、コミットは Task 6 の完了後にまとめる。
- **メインメニューの言語ドロップダウンに `Deutsch` ではなく `german` と出る点は今回直さない。**
  出所: agent前提。`LanguageSetting.Start` が `Localize.GetLanguageCodes()`（言語コード）をそのまま `TMP_Dropdown` に入れており、`localization_settings.csv` の `display_name` 列が未使用という既存の欠陥。ドイツ語追加で顕在化するが原因は別なので、PR本文の既知の制限に書いたうえで `moorestech-f84r` へ切り出した。
