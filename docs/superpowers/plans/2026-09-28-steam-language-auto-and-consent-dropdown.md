# Steam言語の自動適用と参加同意の言語ドロップダウン Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 自分で言語を選んでいないプレイヤーには、Steamのゲーム個別言語を起動時に自動適用する。あわせて、初回の参加同意ポップアップに言語ドロップダウンを置く。

**Architecture:** 言語一覧CSVの Steam 列を「Steam API 言語コードの `;` 区切りリスト」へ改め、ソースジェネレーターが `LanguageInfo.SteamLanguages`（`string[]`）を生成する。`Client.Localization.Localize` には Steam を知らない汎用口「選んだ言語が無いときだけ、保存せずに当てる」を足す。`Client.Starter` に Steam 境界（読み取り）と対応づけ（Steam言語→ゲーム言語）を置き、AfterSceneLoad で1回だけ流す。uGUI の `LanguageSetting` を母国語表記＋言語変更への追従に改め、同意ポップアップへ2つ目のインスタンスを Unity Editor 経由で置く。

**Tech Stack:** Unity 6000.3 / C# / Roslyn SourceGenerator（mooresmaster.Generator, netstandard2.0, xUnit）/ Steamworks.NET / TextMeshPro uGUI / UniRx / uloop

## Requirements

- R1: 選んだ言語（PlayerPrefs `LanguageCode` にある、選択可能な言語）があれば、起動時はそれを使う。受入: 保存値 `japanese` かつ Steam言語 `german` で起動すると japanese。
- R2: 選んだ言語が無ければ Steam のゲーム個別言語（`SteamApps.GetCurrentGameLanguage()`）を CSV で対応づけたゲーム言語で表示する。**保存しない**。受入: 保存値なし・Steam言語 `japanese` で起動すると japanese になり、PlayerPrefs に `LanguageCode` が作られない。
- R3: Steam言語が対応づけに無い、または取得できない（Steam未起動・Editor・例外）ときは english。OS言語は見ない。取得失敗は理由を `Debug.Log` に出す（無音の縮退禁止）。受入: Steam言語 `french` → english。
- R4: CSV で、ゲームの1言語に複数の Steam 言語を対応づけられる（例 `spanish;latam`）。同じ Steam 言語を2つのゲーム言語に対応づけたらジェネレーターが例外を出す。受入: xUnit。
- R5: 参加同意ポップアップ（初回のみ表示）に言語ドロップダウンを置く。選ぶと即座にポップアップ本文と背後のタイトルがその言語に変わり、選んだ言語として保存される。触らずに「了解」した人は Steam言語のまま（保存されない）。受入: Editor でポップアップを出して切替を目視（録画テスト不要、スクリーンショット可）。
- R6: ドロップダウン（タイトル既存分・同意ポップアップ分とも）は言語コードではなく CSV の `display_name`（母国語表記）を並べ、別経路で言語が変わったら表示値を追従させる。18言語以上でもスクロールで収まる（TMP_Dropdown 既定テンプレートの ScrollRect）。
- R7: 出展モードの起動言語（`EventModeAutoStart.ApplyLaunchLanguage`、保存する）は従来どおり優先される。
- やらないこと: OS言語の参照／ポーズメニュー設定画面（webui）の変更／同意ポップアップの表示規則（初回1回）の変更／フォントの多言語グリフ追加（18言語の実追加時に別件）／Steamworks 側の対応言語登録（ユーザー作業）。

## Global Constraints

- AGENTS.md の全規約（200行/ファイル、10ファイル/ディレクトリ、日英2行コメント、try-catch は外部境界のみで根拠コメント＋ログ、`Func<>` 禁止、partial 禁止、デフォルト引数禁止、UniRx でイベント、Prefab・シーンは `uloop execute-dynamic-code` 経由のみ、.meta 手動作成禁止）。
- ジェネレーター変更後は `~/.dotnet/dotnet` で `mooresmaster/build.sh` を実行して DLL を client/server 両 Plugins に配置する（`PATH=$HOME/.dotnet:$PATH bash mooresmaster/build.sh`）。
- ADR: `docs/adr/0073-display-language-from-steam-unless-player-chose-and-consent-language-dropdown.md`、`.decisions/2026-09-28-*.md`（5件）。
- Steam API 言語コードの綴りは Steamworks の「API language code」列（english, japanese, german, schinese, tchinese, koreana, latam, brazilian …）。
- コンパイル: `uloop compile --project-path ./moorestech_client`。テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。

## File Structure

| ファイル | 責務 | 既存部品との関係 |
|---|---|---|
| `Localization/localization_settings.csv` | 列 `steam_api_lang_code` → `steam_languages`、値を API 言語コードへ | 変更 |
| `mooresmaster/mooresmaster.Generator/Localization/LocalizationSettingsParser.cs` | 新列の解析、`;` 分割、空要素・行跨ぎ重複の拒否 | 変更 |
| `mooresmaster/mooresmaster.Generator/Localization/LanguageCatalogCodeEmitter.cs` | `string[] SteamLanguages` を生成 | 変更 |
| `mooresmaster/mooresmaster.Tests/LocalizationTests/LanguageCatalogTest.cs` | 新列のテスト | 変更 |
| `moorestech_client/Assets/Plugins/mooresmaster.Generator.dll` ほか server 側 | build.sh の成果物 | 再生成 |
| `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs` | `HasChosenLanguage()`・`TryApplyUnchosenLanguage(string)` を追加 | 変更（`TrySetLanguage` の隣。新規ファイル不要） |
| `moorestech_client/Assets/Scripts/Client.Starter/Localization/SteamGameLanguageReader.cs` | `SteamApps.GetCurrentGameLanguage()` の外部境界 | 新規。前例 `Client.PlaytestReceiver/Steam/PlaytestLocalSteamIdReader.cs` と同じ役割・形（interface＋実装、try-catchで理由付き失敗） |
| `moorestech_client/Assets/Scripts/Client.Starter/Localization/SteamLanguageMapping.cs` | Steam言語→ゲーム言語（`LanguageCatalog` を読むだけ） | 新規。前例なし（純関数） |
| `moorestech_client/Assets/Scripts/Client.Starter/Localization/SteamStartupLanguage.cs` | AfterSceneLoad で 読む→対応づけ→`TryApplyUnchosenLanguage` | 新規。前例 `EventModeAutoStart.AutoStartIfEventMode`・`PlaytestTitleGates.MarkPassedWhenBootingOutsideTitle`（同じ AfterSceneLoad 起動入口） |
| `moorestech_client/Assets/Scripts/Client.MainMenu/LanguageSetting.cs` | 表示名で並べ、`OnLanguageChanged` を購読して表示値を追従 | 変更（同意ポップアップでも同じ部品を使う） |
| `moorestech_client/Assets/Scenes/Game/MainMenu.unity` | 同意ポップアップ配下に LanguageSetting 付きドロップダウンを複製配置 | Unity Editor 経由で変更 |
| `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeUnchosenLanguageTest.cs` | R1〜R3 の Localize 側 | 新規（LocalizeTest.cs は既に長いので分ける） |
| `moorestech_client/Assets/Scripts/Client.Tests/Localization/SteamLanguageMappingTest.cs` | 対応づけ | 新規 |

`Client.Starter` は既に `com.rlabrecque.steamworks.net`・`Client.Localization` を参照し、`Client.Tests` は `Client.Starter` を参照済み（asmdef 変更不要。着手時に `grep -n steamworks moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef` で確認する）。

### 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 機構 | 判定 |
|---|---|---|---|---|
| 1 | Steam言語読み取り | Client.Starter/Localization | 外部境界 interface | 前例 PlaytestLocalSteamIdReader（Client.PlaytestReceiver）。言語はプレイテストの概念ではないため PlaytestReceiver ではなく Starter に置く |
| 2 | 保存しない適用 | Client.Localization.Localize | static＋既存 `onLanguageChangedSubject` | 基盤は「選んだ言語が無いときだけ当てる」だけを知り、Steam を知らない（汎用基盤にドメイン語彙を持ち込まない） |
| 3 | 起動時の流れ | Client.Starter AfterSceneLoad | RuntimeInitializeOnLoadMethod | 前例 EventModeAutoStart。AfterSceneLoad は最初のシーンの Awake（`SteamManager.Awake` の `SteamAPI.Init`）の後に走る |
| 4 | 表示追従 | LanguageSetting が `Localize.OnLanguageChanged` を Subscribe | UniRx | 前例 TextMeshProLocalize |

データフロー: （Steam／ドロップダウン／設定画面／出展env）→ `Localize.currentLanguageCode`（共有状態・書き手が1人増える）→ `OnLanguageChanged` → 表示（TMP・webui・スキット・ドロップダウン）。交差点なし。

### 共有状態の最新化（Self-Review 7）

| 書き換え | 保持者 | 最新化経路 |
|---|---|---|
| `TryApplyUnchosenLanguage` | TextMeshProLocalize / LocalizationTopic / スキット / LanguageSetting(2個) | すべて `OnLanguageChanged` 購読（R6 で LanguageSetting を購読側へ加える） |
| ドロップダウン選択（`TrySetLanguage`） | 同上＋もう一方の LanguageSetting | 同上 |

---

### Task 1: 言語一覧CSVの Steam 列を複数値にする（ジェネレーター）

**Files:**
- Modify: `Localization/localization_settings.csv`
- Modify: `mooresmaster/mooresmaster.Generator/Localization/LocalizationSettingsParser.cs`
- Modify: `mooresmaster/mooresmaster.Generator/Localization/LanguageCatalogCodeEmitter.cs`
- Modify: `mooresmaster/mooresmaster.Tests/LocalizationTests/LanguageCatalogTest.cs`
- Regenerate: `moorestech_client/Assets/Plugins/mooresmaster.Generator.dll`, `moorestech_server/Assets/Plugins/mooresmaster.Generator.dll`（LocalizationCsv DLL も build.sh が上書きする）

**Interfaces:**
- Produces: 生成コード `Mooresmaster.Localization.Generated.LanguageInfo` に `public readonly string[] SteamLanguages;`（`SteamApiLangCode` は削除）。`LanguageCatalog.Languages` は従来どおり。

- [ ] **Step 1: テストを新仕様へ書き換える**（`LanguageCatalogTest.cs`）

`SettingsCsv` を `"lang_name,display_name,steam_languages\nenglish,English,english\njapanese,日本語,japanese\n"` に変え、全テストのヘッダ文字列を `steam_languages` に置換する。`LanguageCatalogが生成される` の `Assert.Contains("\"ja\"", code)` を `Assert.Contains("new string[] { \"japanese\" }", code)` にする。`設定値のquotedCommaを保持する` の最終行を `Assert.Equal(new[] { "english" }, setting.SteamLanguages);`（CSV 値も `english`）にする。`Steam言語コードが空または空白なら例外` は据え置き（ヘッダだけ置換）。以下を追加:

```csharp
    [Fact]
    public void Steam言語はセミコロン区切りで複数持てる()
    {
        const string settingsCsv =
            "lang_name,display_name,steam_languages\nspanish,Español,spanish;latam\n";

        var setting = Assert.Single(LocalizationSettingsParser.Parse(settingsCsv));

        Assert.Equal(new[] { "spanish", "latam" }, setting.SteamLanguages);
    }

    [Theory]
    [InlineData("spanish;")]
    [InlineData(";latam")]
    [InlineData("spanish; ;latam")]
    public void Steam言語に空要素があれば例外(string steamLanguages)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_languages\nspanish,Español,{steamLanguages}\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Fact]
    public void 同じSteam言語を二つの言語へ対応づけたら例外()
    {
        const string settingsCsv =
            "lang_name,display_name,steam_languages\nspanish,Español,spanish;latam\nlatam_spanish,Español (LA),latam\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }
```

- [ ] **Step 2: 失敗を確認**

Run: `cd mooresmaster && ~/.dotnet/dotnet test mooresmaster.Tests --filter "FullyQualifiedName~LanguageCatalogTest"`
Expected: FAIL（ヘッダ不一致の例外・`SteamLanguages` 未定義のコンパイルエラー）

- [ ] **Step 3: パーサーを実装**（`LocalizationSettingsParser.cs`）

record を `public record LanguageSetting(string Code, string DisplayName, string[] SteamLanguages)`（フィールドも `public readonly string[] SteamLanguages = SteamLanguages;`）に変える。ヘッダ検査の3列目を `"steam_languages"`、メッセージを `lang_name, display_name, and steam_languages` にする。ループ前に `var seenSteamLanguages = new HashSet<string>();` を置き、`steamApiLangCode` の検査部分を次に置き換える:

```csharp
            // Steam言語は;区切りで複数持て、空要素と言語間の重複は入力境界で拒否する
            // Steam languages are ;-separated; empty items and cross-language duplicates are rejected at the input boundary
            var steamLanguagesField = fields[2];
            if (string.IsNullOrWhiteSpace(steamLanguagesField))
            {
                throw new LocalizationCsvException("Language setting Steam languages must not be empty");
            }

            var steamLanguages = steamLanguagesField.Split(';');
            foreach (var steamLanguage in steamLanguages)
            {
                if (string.IsNullOrWhiteSpace(steamLanguage))
                {
                    throw new LocalizationCsvException($"Language setting {code} has an empty Steam language in: {steamLanguagesField}");
                }

                if (!seenSteamLanguages.Add(steamLanguage))
                {
                    throw new LocalizationCsvException($"Steam language {steamLanguage} is mapped to more than one language");
                }
            }
```

生成行は `new LanguageSetting(code, displayName, steamLanguages)`。

- [ ] **Step 4: エミッターを実装**（`LanguageCatalogCodeEmitter.cs`）

`SteamApiLangCode` の3箇所（フィールド・ctor 引数・代入）を `string[] SteamLanguages`／`string[] steamLanguages`／`SteamLanguages = steamLanguages;` に変え、要素出力を次にする:

```csharp
        foreach (var setting in settings)
        {
            var steamLanguages = string.Join(", ", Array.ConvertAll(setting.SteamLanguages, language => $"\"{LocalizationCodeSyntax.Escape(language)}\""));
            builder.AppendLine(
                $"            new LanguageInfo(\"{LocalizationCodeSyntax.Escape(setting.Code)}\", \"{LocalizationCodeSyntax.Escape(setting.DisplayName)}\", new string[] {{ {steamLanguages} }}),");
        }
```

- [ ] **Step 5: CSV を更新**（`Localization/localization_settings.csv`）

```
lang_name,display_name,steam_languages
english,English,english
japanese,日本語,japanese
german,Deutsch,german
```

`moorestech_web/webui/e2e/mock-host/localization/transport.ts` は先頭2列しか読まないので変更不要（確認のみ）。

- [ ] **Step 6: テスト通過を確認**

Run: `cd mooresmaster && ~/.dotnet/dotnet test mooresmaster.Tests`
Expected: 全件 PASS（`SteamApiLangCode`・`steam_api_lang_code` を参照する他テストが落ちたら同様に置換する: `grep -rn "steam_api_lang_code\|SteamApiLangCode" mooresmaster/`）

- [ ] **Step 7: DLL を再配置し、Unity でコンパイル**

Run: `PATH=$HOME/.dotnet:$PATH bash mooresmaster/build.sh` → `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0（クライアントに `SteamApiLangCode` 参照は無い: `grep -rn SteamApiLangCode moorestech_client/Assets/Scripts` が空であること）

- [ ] **Step 8: コミット**

```bash
git add Localization/localization_settings.csv mooresmaster moorestech_client/Assets/Plugins moorestech_server/Assets/Plugins
git commit -m "feat(localization): 言語一覧CSVのSteam列を複数のSteam API言語コードへ改める (ADR 0073)"
```

### Task 2: Localize に「選んだ言語が無いときだけ保存せずに当てる」口を足す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`（`TrySetLanguage` の直後）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Resolution/LocalizeUnchosenLanguageTest.cs`

**Interfaces:**
- Produces: `public static bool HasChosenLanguage()`、`public static bool TryApplyUnchosenLanguage(string languageCode)`（選んだ言語がある・言語が選択不能なら false。保存しない。成功時 `OnLanguageChanged` を1回発火）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.Localization;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizeUnchosenLanguageTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            // 保存値を退避し、選んだ言語が無い状態から始める
            // Preserve the persisted value and start without a chosen language
            hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void AppliesWithoutPersistingWhenNothingIsChosen()
        {
            var changedCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => changedCount++);

            Assert.IsTrue(Localize.TryApplyUnchosenLanguage("japanese"));

            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual(1, changedCount);
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
            Assert.IsFalse(Localize.HasChosenLanguage());
        }

        [Test]
        public void KeepsChosenLanguage()
        {
            Localize.TrySetLanguage("japanese");

            Assert.IsFalse(Localize.TryApplyUnchosenLanguage("german"));

            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.IsTrue(Localize.HasChosenLanguage());
        }

        [Test]
        public void UnselectableSavedValueIsNotAChoice()
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, "obsolete");
            Localize.Initialize();

            Assert.IsFalse(Localize.HasChosenLanguage());
            Assert.IsTrue(Localize.TryApplyUnchosenLanguage("german"));
            Assert.AreEqual("german", Localize.GetCurrentLanguageCode());
        }

        [TestCase("french")]
        [TestCase("")]
        public void RejectsUnselectableLanguage(string languageCode)
        {
            Assert.IsFalse(Localize.TryApplyUnchosenLanguage(languageCode));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認** — `uloop compile --project-path ./moorestech_client`。Expected: CS0117（`TryApplyUnchosenLanguage` 未定義）

- [ ] **Step 3: 実装**（`Localize.cs`、`TrySetLanguage` の直後）

```csharp
        // 選択可能な保存値があることを「プレイヤーが自分で選んだ」とみなす（ADR 0073）
        // A selectable persisted value is what "the player chose" means (ADR 0073)
        public static bool HasChosenLanguage()
        {
            if (!PlayerPrefs.HasKey(LanguagePreferenceKey)) return false;
            var languages = Volatile.Read(ref publishedSnapshot).Languages;
            return languages.ContainsKey(PlayerPrefs.GetString(LanguagePreferenceKey));
        }

        // 選んだ言語が無いときだけ当て、保存しない。出どころ（Steam等）は呼び出し側が決める
        // Applies only while nothing is chosen and never persists; the caller decides the origin (Steam etc.)
        public static bool TryApplyUnchosenLanguage(string languageCode)
        {
            if (HasChosenLanguage()) return false;
            if (string.IsNullOrEmpty(languageCode)) return false;
            var languages = Volatile.Read(ref publishedSnapshot).Languages;
            if (!languages.ContainsKey(languageCode)) return false;

            currentLanguageCode = languageCode;
            onLanguageChangedSubject.OnNext(Unit.Default);
            return true;
        }
```

Localize.cs が200行を超えたら、`TryGetDictionary`/`TryGetSourceTexts` 群を別 static クラスへ移すのではなく、まず行数を `wc -l` で確認し、超えた場合のみ `HasChosenLanguage`/`TryApplyUnchosenLanguage` を `Client.Localization/LocalizeUnchosenLanguage.cs`（`Localize` の internal な `SetCurrentLanguageWithoutPersisting` を呼ぶ static クラス）に分ける。

- [ ] **Step 4: テスト通過を確認** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "LocalizeUnchosenLanguageTest|LocalizeTest"`。Expected: 全件 PASS

- [ ] **Step 5: コミット** — `git add` 上記2ファイル（Unity が生成した .meta を含む）→ `git commit -m "feat(localization): 選んだ言語が無いときだけ保存せずに言語を当てる口を足す (ADR 0073)"`

### Task 3: 起動時に Steam言語を当てる（Client.Starter）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Localization/SteamGameLanguageReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Localization/SteamLanguageMapping.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Localization/SteamStartupLanguage.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Localization/SteamLanguageMappingTest.cs`

**Interfaces:**
- Consumes: `LanguageCatalog.Languages[i].SteamLanguages`（Task 1）、`Localize.TryApplyUnchosenLanguage`/`HasChosenLanguage`（Task 2）
- Produces: `SteamLanguageMapping.ToGameLanguage(string steamLanguage) : string`（対応が無ければ `Localize.DefaultLanguageCode`）

- [ ] **Step 1: 失敗するテスト**

```csharp
using Client.Localization;
using Client.Starter.Localization;
using NUnit.Framework;

namespace Client.Tests.Localization
{
    public class SteamLanguageMappingTest
    {
        [TestCase("english", "english")]
        [TestCase("japanese", "japanese")]
        [TestCase("german", "german")]
        public void MapsSteamLanguageListedInCatalog(string steamLanguage, string expected)
        {
            Assert.AreEqual(expected, SteamLanguageMapping.ToGameLanguage(steamLanguage));
        }

        [TestCase("french")]
        [TestCase("")]
        public void FallsBackToEnglishForUnmappedSteamLanguage(string steamLanguage)
        {
            Assert.AreEqual(Localize.DefaultLanguageCode, SteamLanguageMapping.ToGameLanguage(steamLanguage));
        }
    }
}
```

- [ ] **Step 2: コンパイル失敗を確認**（`SteamLanguageMapping` 未定義）

- [ ] **Step 3: 対応づけを実装**（`SteamLanguageMapping.cs`）

```csharp
using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Starter.Localization
{
    // Steam言語を言語一覧CSVの対応づけでゲーム言語へ寄せ、無ければ英語にする（ADR 0073）
    // Maps a Steam language to a game language through the language list CSV, else English (ADR 0073)
    public static class SteamLanguageMapping
    {
        public static string ToGameLanguage(string steamLanguage)
        {
            foreach (var language in LanguageCatalog.Languages)
            {
                foreach (var candidate in language.SteamLanguages)
                {
                    if (candidate == steamLanguage) return language.Code;
                }
            }

            return Localize.DefaultLanguageCode;
        }
    }
}
```

- [ ] **Step 4: Steam 境界を実装**（`SteamGameLanguageReader.cs`。前例 `Client.PlaytestReceiver/Steam/PlaytestLocalSteamIdReader.cs` を開いて同じ形にする）

```csharp
using System;
using Steamworks;

namespace Client.Starter.Localization
{
    public interface ISteamGameLanguageReader
    {
        bool TryRead(out string steamLanguage, out string failureReason);
    }

    // Steamのゲーム個別言語を読む境界（ADR 0073）。SteamManagerはAssembly-CSharpにあり参照できないのでネイティブへ直接聞く
    // Reads Steam's per-game language (ADR 0073); SteamManager lives in Assembly-CSharp, so ask Steam directly
    public sealed class SteamGameLanguageReader : ISteamGameLanguageReader
    {
        public bool TryRead(out string steamLanguage, out string failureReason)
        {
            // ネイティブ呼び出しはSteam未初期化・dll不在で例外になる外部境界。畳んで理由付きの失敗にする
            // The native call throws when Steam is uninitialized or the dll is absent; fold it into a reasoned failure
            try
            {
                steamLanguage = SteamApps.GetCurrentGameLanguage();
            }
            catch (Exception exception)
            {
                steamLanguage = "";
                failureReason = $"SteamApps.GetCurrentGameLanguage failed: {exception.GetBaseException().Message}";
                return false;
            }

            if (string.IsNullOrEmpty(steamLanguage))
            {
                failureReason = "SteamApps.GetCurrentGameLanguage returned an empty language";
                return false;
            }

            failureReason = "";
            return true;
        }
    }
}
```

- [ ] **Step 5: 起動入口を実装**（`SteamStartupLanguage.cs`）

```csharp
using Client.Localization;
using UnityEngine;

namespace Client.Starter.Localization
{
    // 選んだ言語が無い人へ起動時に1回だけSteam言語を当てる（ADR 0073）
    // Applies the Steam language once at boot for players without a chosen language (ADR 0073)
    public static class SteamStartupLanguage
    {
        // AfterSceneLoadは最初のシーンのAwake（SteamManagerのSteamAPI.Init）より後に走る（前例: EventModeAutoStart）
        // AfterSceneLoad runs after the first scene's Awake, where SteamManager calls SteamAPI.Init (precedent: EventModeAutoStart)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAtBoot()
        {
            Apply(new SteamGameLanguageReader());
        }

        internal static void Apply(ISteamGameLanguageReader reader)
        {
            // 選んだ言語がある人にはSteamを読みにいかない
            // Do not consult Steam for players who already chose
            if (Localize.HasChosenLanguage()) return;

            // 読めなければ既定の英語のまま、理由をログに残す
            // When unreadable, stay on the default English and log why
            if (!reader.TryRead(out var steamLanguage, out var failureReason))
            {
                Debug.Log($"[SteamStartupLanguage] staying on {Localize.GetCurrentLanguageCode()}: {failureReason}");
                return;
            }

            var gameLanguage = SteamLanguageMapping.ToGameLanguage(steamLanguage);
            if (!Localize.TryApplyUnchosenLanguage(gameLanguage))
                Debug.LogWarning($"[SteamStartupLanguage] could not apply {gameLanguage} for Steam language {steamLanguage}");
        }
    }
}
```

`internal` の `Apply` を `Client.Tests` から呼ぶため、`Client.Starter/AssemblyInfo.cs` に `InternalsVisibleTo("Client.Tests")` があるか確認する（無ければ追記）。

- [ ] **Step 6: `Apply` のテストを `SteamLanguageMappingTest.cs` と同じディレクトリに追加**（`SteamStartupLanguageTest.cs`。PlayerPrefs の退避・復元は Task 2 のテストと同じ SetUp/TearDown）

```csharp
        private sealed class FixedReader : ISteamGameLanguageReader
        {
            private readonly bool _readable;
            private readonly string _language;
            public FixedReader(bool readable, string language) { _readable = readable; _language = language; }
            public bool TryRead(out string steamLanguage, out string failureReason)
            {
                steamLanguage = _language;
                failureReason = _readable ? "" : "steam not running";
                return _readable;
            }
        }

        [Test]
        public void AppliesMappedSteamLanguageWithoutPersisting()
        {
            SteamStartupLanguage.Apply(new FixedReader(true, "japanese"));
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
        }

        [Test]
        public void ChosenLanguageWinsOverSteam()
        {
            Localize.TrySetLanguage("german");
            SteamStartupLanguage.Apply(new FixedReader(true, "japanese"));
            Assert.AreEqual("german", Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void UnreadableSteamStaysEnglish()
        {
            SteamStartupLanguage.Apply(new FixedReader(false, ""));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void UnmappedSteamLanguageIsEnglish()
        {
            SteamStartupLanguage.Apply(new FixedReader(true, "french"));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }
```

`UnreadableSteamStaysEnglish` は `Debug.Log` のみなので `LogAssert` 不要。

- [ ] **Step 7: コンパイルとテスト** — `uloop compile ...` → `uloop run-tests ... --filter-value "SteamLanguageMappingTest|SteamStartupLanguageTest|LocalizeUnchosenLanguageTest"`。Expected: PASS

- [ ] **Step 8: コミット** — `git commit -m "feat(localization): 選んだ言語が無い人へ起動時にSteamのゲーム個別言語を当てる (ADR 0073)"`

### Task 4: 言語ドロップダウンを母国語表記・追従にし、同意ポップアップへ置く

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/LanguageSetting.cs`
- Modify（Unity Editor 経由のみ）: `moorestech_client/Assets/Scenes/Game/MainMenu.unity`

**Interfaces:**
- Consumes: `LanguageCatalog.Languages`（`Code`・`DisplayName`）、`Localize.OnLanguageChanged`、`Localize.TrySetLanguage`

- [ ] **Step 1: LanguageSetting を書き換える**

```csharp
using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.MainMenu
{
    // 母国語表記で言語を並べ、別経路の言語変更にも表示を追従させる（ADR 0073）
    // Lists languages by native name and follows language changes made elsewhere (ADR 0073)
    public class LanguageSetting : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown tmpDropdown;

        private readonly List<string> _languageCodes = new();

        private void Start()
        {
            // 言語一覧CSVの並びで表示名とコードを揃える
            // Keep display names and codes in the language list CSV order
            var displayNames = new List<string>();
            foreach (var language in LanguageCatalog.Languages)
            {
                _languageCodes.Add(language.Code);
                displayNames.Add(language.DisplayName);
            }

            tmpDropdown.ClearOptions();
            tmpDropdown.AddOptions(displayNames);
            ShowCurrentLanguage();
            tmpDropdown.onValueChanged.AddListener(OnValueChanged);

            // もう一方のドロップダウン・Steam適用・設定画面での変更に表示を合わせる
            // Follow changes from the other dropdown, the Steam apply, or the settings screen
            Localize.OnLanguageChanged.Subscribe(_ => ShowCurrentLanguage()).AddTo(this);
        }

        private void ShowCurrentLanguage()
        {
            tmpDropdown.SetValueWithoutNotify(_languageCodes.IndexOf(Localize.GetCurrentLanguageCode()));
        }

        private void OnValueChanged(int index)
        {
            // 選択肢は選択可能な言語だけなので可否の戻り値は捨ててよい
            // Options contain only selectable languages, so the result can be discarded
            Localize.TrySetLanguage(_languageCodes[index]);
        }
    }
}
```

- [ ] **Step 2: コンパイル** — ErrorCount 0

- [ ] **Step 3: シーン構造を調べる**（`uloop execute-dynamic-code`）— MainMenu シーンを開き、`LanguageSetting` を持つ GameObject のパス・RectTransform・親、`PlaytestConsentPopup` の GameObject 配下の階層（本文 TMP・`agreeButton`）とサイズを列挙して出力する。

- [ ] **Step 4: 複製して配置**（`uloop execute-dynamic-code`）— `Object.Instantiate(タイトルのLanguageSetting GameObject, 同意ポップアップのパネル)` で複製し、名前を `ConsentLanguageDropdown` にする。パネル左上（本文より上）に置く: `anchorMin=anchorMax=pivot=(0,1)`、`anchoredPosition=(24,-24)`。本文と重なる場合は本文の RectTransform の上端を dropdown の高さ＋16 だけ下げる。ドロップダウンの Template（ScrollRect）の高さが 18 行未満しか出さないことを確認し（既定テンプレートは ScrollRect 付き）、`EditorSceneManager.MarkSceneDirty`→`SaveScene`。複製元が参照している `tmpDropdown` は Instantiate で複製側の子へ自動で張り替わることを確認する。

- [ ] **Step 5: 目視確認**（PlayMode、`uloop` のスクリーンショット）— 同意フラグを一時退避（`GameSystemPaths.BugReportDirectory/consent-acknowledged-v1` を別名へ移し、確認後に戻す）してタイトルを再生し、(a) ポップアップにドロップダウンが「English / 日本語 / Deutsch」で出る、(b) 日本語を選ぶと本文とタイトルが即座に日本語、(c) 背後のタイトルのドロップダウンも「日本語」に変わる、(d) 了解後に再生し直しても日本語（保存された）、を確認してスクショを残す。続けて PlayerPrefs `LanguageCode` を削除して再生し、Editor で Steam が動いていなければ Console に `[SteamStartupLanguage] staying on english:` が出ることを確認する。

- [ ] **Step 6: コミット** — `git add moorestech_client/Assets/Scripts/Client.MainMenu/LanguageSetting.cs moorestech_client/Assets/Scenes/Game/MainMenu.unity` → `git commit -m "feat(title): 参加同意ポップアップに母国語表記の言語ドロップダウンを置く (ADR 0073)"`

### Task 5（最終・省略不可）: 全ブランチレビュー

- [ ] **Step 1:** moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）。
- [ ] **Step 2:** 指摘の反映が判定経路（`HasChosenLanguage`・`SteamStartupLanguage.Apply`・`LanguageSetting` の購読）に触れたら、Task 4 Step 5 の目視確認を反映後のビルドで再実施する。合否は Console に `[SteamStartupLanguage] could not apply`・`Exception`・`NullReference` が確認区間で 0 件であること。
- [ ] **Step 3:** 残課題（未検証事項）は1件ずつ `bd create` で起票し、PR 本文に issue ID を列挙する。既知の残課題: 実配布ビルドで Steam言語 japanese のアカウントから起動して日本語になることの確認（Editor では Steam 初期化が無い場合がある）／18言語追加時の TMP フォントのグリフ網羅／Steamworks 対応言語の登録（ユーザー作業）。
- [ ] **Step 4:** PR を作成（通常のマージコミット運用）。ADR 0073・CONTEXT.md・`.decisions/2026-09-28-*` を同じ PR に含める。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0073-display-language-from-steam-unless-player-chose-and-consent-language-dropdown.md`（ユーザー裁定5件・agent前提は同ADR参照）。`.decisions/2026-09-28-*.md` 5件。
- planning 中の判断:
  - CSV 列名を `steam_api_lang_code` → `steam_languages` に改め、値を Steam の API 言語コード（`GetCurrentGameLanguage` の戻り値の形）にする。出所: agent判断（旧値 en/ja/de は Web API コードで、読み取り値と形が合わない）
  - 「選んだ言語」＝選択可能な保存値がある、と定義する（無効な保存値は選んでいない扱い）。出所: agent判断（Initialize が無効値を english に落とす既存挙動と揃える）
  - Steam 適用の起動入口は `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`。出所: agent判断（前例 EventModeAutoStart・PlaytestTitleGates）
  - 同意ポップアップのドロップダウンは既存 `LanguageSetting` 部品の2つ目のインスタンスとし、ラベル（"Language:"）は置かない。出所: agent判断（ドロップダウン自体が母国語名を出すため。裁定時のプレビューのラベルは副次情報で裁定対象外）
  - Steam読み取りは Client.Starter に置く（Client.PlaytestReceiver ではない）。出所: agent判断（言語はプレイテスト概念ではない）
