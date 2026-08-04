---
spec: docs/superpowers/specs/2026-07-29-localization-foundation-design.md
---

# Localization Language Switch UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** webuiに言語選択UIを新設し、Web→ホストの set locale 経路を追加。言語表示名メタ（localization_settings）を埋め込み化し、modMeta id空文字と旧config CSVの残骸を片付けて基盤を完成させる。

**Architecture:** 言語一覧＋表示名は `Localization/localization_settings.csv` を第2のAdditionalFileとしてgeneratorへ渡し `LanguageCatalog` に埋め込む（言語セットの唯一の定義は辞書CSVヘッダ。不一致はコンパイルエラー）。Web→ホストは既存の `IActionHandler`/`dispatchAction` 機構で `localization.setLocale` を追加し、既存の `Localize.SetLanguage`→PlayerPrefs→`localization.current` push 往復に接続する。

**Tech Stack:** Roslyn generator（Plan1の拡張）/ Unity asmdef / React / uloop

## Global Constraints

- Plan1・Plan2完了が前提
- LanguageCatalog埋め込みと言語セットの辞書CSVヘッダ一本化はユーザー採択済み。別定義・optional settings・欠損補完を追加しない
- webuiの見た目・構造は webui-design スキルのホワイトリスト厳守。着手前に `.claude/skills/webui-design/SKILL.md` を必ず読む
- partial禁止・Func禁止・try-catch原則禁止・UniRx・200行/ファイル・日英2行コメント（AGENTS.md）
- レガシーuGUIの `LanguageSetting.cs` は触らない（uGUI残置方針・動き続ける）
- 各タスク末で必ずコミット

## File Structure

```
Localization/localization_settings.csv                       ← 移設・2言語へ縮小
mooresmaster/mooresmaster.Generator/Localization/
└── LocalizationCodeGenerator.cs                             ← LanguageCatalog生成を追加
mooresmaster/mooresmaster.Generator/LocalizationSourceEmitter.cs   ← settings AdditionalFile対応
moorestech_client/Assets/Scripts/Client.Localization/csc.rsp ← settings行を追加
moorestech_client/Assets/Scripts/Client.WebUiHost/Game/
├── LocalizationLanguagesEndpoint.cs                         ← 新設 GET /api/i18n-languages
└── Actions/LocalizationActions.cs                           ← 新設 localization.setLocale
moorestech_web/webui/src/features/settings/（既存前例に従う）  ← 言語選択UI
../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/modMeta.json ← id修理
../moorestech_master/server_v8/config/{localization.csv,localization_settings.csv} ← 削除
moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/config/ ← 同CSV削除
```

---

### Task 1: localization_settings の移設と LanguageCatalog 埋め込み

**Files:**
- Create: `Localization/localization_settings.csv`
- Modify: `mooresmaster/mooresmaster.Generator/Localization/LocalizationCodeGenerator.cs`
- Modify: `mooresmaster/mooresmaster.Generator/LocalizationSourceEmitter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/csc.rsp`
- Test: `mooresmaster/mooresmaster.Tests/LocalizationTests/LanguageCatalogTest.cs`

**Interfaces:**
- Produces: `Mooresmaster.Localization.Generated.LanguageCatalog` — `public static readonly LanguageInfo[] Languages;` / `public readonly struct LanguageInfo { public readonly string Code; public readonly string DisplayName; public readonly string SteamApiLangCode; }`
- 言語セット不一致（settingsの行集合≠辞書ヘッダ列集合）はgeneratorが `LocalizationCsvException` → コンパイルエラー

- [ ] **Step 1: 移設CSVを作る**

`Localization/localization_settings.csv`（旧 `../moorestech_master/server_v8/config/localization_settings.csv` から english/japanese の2行だけ移し、実値を確認して使う）:

```csv
lang_name,display_name,steam_api_lang_code
english,English,en
japanese,日本語,ja
```

- [ ] **Step 2: 失敗するテストを書く**

`LanguageCatalogTest.cs`:

```csharp
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LanguageCatalogTest
{
    private const string DictionaryCsv = "key,Source,english,japanese\nui.a.b,x,x,y\n";
    private const string SettingsCsv = "lang_name,display_name,steam_api_lang_code\nenglish,English,en\njapanese,日本語,ja\n";

    [Fact]
    public void LanguageCatalogが生成される()
    {
        var code = LocalizationCodeGenerator.Generate(
            LocalizationCsvParser.Parse(DictionaryCsv),
            LocalizationSettingsParser.Parse(SettingsCsv));
        Assert.Contains("LanguageCatalog", code);
        Assert.Contains("日本語", code);
        Assert.Contains("\"ja\"", code);
    }

    [Fact]
    public void 言語セット不一致は例外()
    {
        var settingsMissingJapanese = "lang_name,display_name,steam_api_lang_code\nenglish,English,en\n";
        Assert.Throws<LocalizationCsvException>(() => LocalizationCodeGenerator.Generate(
            LocalizationCsvParser.Parse(DictionaryCsv),
            LocalizationSettingsParser.Parse(settingsMissingJapanese)));
    }
}
```

Run: `cd mooresmaster && dotnet test --filter "FullyQualifiedName~LanguageCatalogTest"`
Expected: FAIL

- [ ] **Step 3: 実装する**

- `LocalizationSettingsParser`（新規・`Localization/` 配下）: `Mooresmaster.LocalizationCsv.LocalizationCsvParser.ParseRecords` のquote-aware record分割を使って3列CSV→`LanguageSetting[]`（`record LanguageSetting(string Code, string DisplayName, string SteamApiLangCode)`）へ写像する。別のCSV field parserを実装しない
- `LocalizationCodeGenerator.Generate(LocalizationCsv csv, LanguageSetting[] settings)` へシグネチャ変更（settingsは必須引数。デフォルト引数禁止・呼び出し側を全updateする — AGENTS.md）。settings行集合と `csv.LanguageCodes` の集合一致を検査し、`LanguageCatalog` を追加emit
- `LocalizationSourceEmitter`: `MooresmasterSourceGenerator` が収集したAdditionalFilesから `localization_settings.csv` も取得。**settingsが無い場合もコンパイルエラー**（片方だけの配線ミスを無言で通さない）。独立した第2 `[Generator]` へ戻すと共通DLLのanalyzer依存解決が全assemblyを壊すため、単一generator統合を維持する
- `csc.rsp` へ追記: `/additionalfile:Assets/../../Localization/localization_settings.csv`

Run: `cd mooresmaster && dotnet test && ./build.sh && uloop compile --project-path ./moorestech_client`
Expected: 全て成功

- [ ] **Step 4: コミット**

```bash
git add Localization/localization_settings.csv mooresmaster/ moorestech_client/Assets/Plugins/mooresmaster.Generator.dll moorestech_server/Assets/Plugins/mooresmaster.Generator.dll moorestech_client/Assets/Scripts/Client.Localization/csc.rsp
git commit -m "feat: 言語表示名メタをLanguageCatalogとして埋め込み化"
```

---

### Task 2: 言語一覧エンドポイントと set locale アクション

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/LocalizationLanguagesEndpoint.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/LocalizationActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs`（ルーティング登録・既存Endpointの登録行と同形式）
- Modify: アクション登録箇所（`BuildMenuActions` 等が登録されている場所をgrepで実測し同形式で追加）

**Interfaces:**
- Produces:
  - `GET /api/i18n-languages` → `[{ "code": "english", "displayName": "English" }, ...]`
  - action `localization.setLocale`・payload `{ "locale": "japanese" }` → `Localize.SetLanguage` 呼び出し。未知localeは `ActionResult.Fail("unknown_locale")`

- [ ] **Step 1: エンドポイントを実装する**

`LocalizationLanguagesEndpoint.cs`（`LocalizationDictionaryEndpoint.cs` と同形式）:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Microsoft.AspNetCore.Http;
using Mooresmaster.Localization.Generated;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// 選択可能な言語の一覧（コード＋表示名）を配信する
    /// Serves the selectable language list (code + display name)
    /// </summary>
    public static class LocalizationLanguagesEndpoint
    {
        public const string Path = "/api/i18n-languages";

        public static async Task HandleAsync(HttpContext context)
        {
            var languages = LanguageCatalog.Languages
                .Select(l => new LanguageDto { Code = l.Code, DisplayName = l.DisplayName })
                .ToList();
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(WebUiJson.Serialize(languages), CancellationToken.None);
        }
    }

    public class LanguageDto
    {
        public string Code;
        public string DisplayName;
    }
}
```

- [ ] **Step 2: アクションを実装する**

`LocalizationActions.cs`（`DebugActions.cs` の `IActionHandler` 前例と同形式・ただし本番でも有効なので `#if` は付けない）:

```csharp
using Client.Localization;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Webからの言語切替。SetLanguageがPlayerPrefs永続とlocalization.current pushまで担う
    /// Locale switch from the Web; SetLanguage handles persistence and the localization.current push
    /// </summary>
    public class SetLocaleActionHandler : IActionHandler
    {
        public string ActionType => "localization.setLocale";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var locale = payload?["locale"]?.ToString();
            if (string.IsNullOrEmpty(locale) || !Localize.LanguageCodes.Contains(locale))
                return UniTask.FromResult(ActionResult.Fail("unknown_locale"));

            Localize.SetLanguage(locale);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

登録: 既存アクションの登録箇所（`grep -rn "EchoActionHandler\|BuildMenuActions" moorestech_client/Assets/Scripts/Client.WebUiHost --include="*.cs"` で実測）と同形式で `SetLocaleActionHandler` を追加。ルーティングも `WebUiEndpoints.cs` の既存Endpoint登録行と同形式で追加。

- [ ] **Step 3: コンパイル・コミット**

```bash
uloop compile --project-path ./moorestech_client
git add moorestech_client/Assets/Scripts/Client.WebUiHost/ && git commit -m "feat: 言語一覧エンドポイントとsetLocaleアクション"
```

---

### Task 3: webui 言語選択UI

**Files:**
- Create: `moorestech_web/webui/src/features/settings/LanguageSelect.tsx`（設置先は既存メニュー/設定前例の実測に従い調整）
- Modify: `moorestech_web/webui/src/bridge/`（`/api/i18n-languages` のURL定義・`localization.setLocale` のaction型追加）
- Modify: `Localization/localization.csv`（UI文言キー追加）+ TS再生成

**Interfaces:**
- Consumes: Task 2のエンドポイント/アクション、`localization.current` トピック（現在値）
- Produces: 言語選択UI（表示名リスト・現在値ハイライト・クリックで `dispatchAction("localization.setLocale", { locale })`）

- [ ] **Step 1: webui-designスキルを読み、設置先の前例を実測する**

`.claude/skills/webui-design/SKILL.md` を読む。既存のシステム系パネル（ポーズメニュー/システムメニュー相当。`grep -rn "PauseMenu\|SystemMenu\|settings" moorestech_web/webui/src/features -il`）を特定し、その中に言語セクションを追加する（新規モーダルの発明はしない。ホワイトリスト外の装飾禁止）。

- [ ] **Step 2: bridge層へURL・actionを追加する**

`httpEndpoints.ts` へ `export const localizationLanguagesUrl = "/api/i18n-languages";`（既存URL定義と同形式）。actions側の型定義へ `localization.setLocale` を追加（`actions.ts` の抑止コード表には追加不要 — 失敗はトースト表示が正）。

- [ ] **Step 3: UIを実装する**

```tsx
import { useEffect, useState } from "react";
import { dispatchAction, localizationLanguagesUrl, Topics, useTopic } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";

type LanguageEntry = { code: string; displayName: string };

// 言語選択リスト。現在値はlocalization.currentトピックが正
// Language list; the current value comes from the localization.current topic
export default function LanguageSelect() {
  const { t } = useI18n();
  const current = useTopic(Topics.localization)?.locale;
  const [languages, setLanguages] = useState<LanguageEntry[]>([]);

  useEffect(() => {
    const abort = new AbortController();
    void fetch(localizationLanguagesUrl, { signal: abort.signal })
      .then((res) => (res.ok ? res.json() : []))
      .then((list: LanguageEntry[]) => setLanguages(list))
      .catch(() => undefined);
    return () => abort.abort();
  }, []);

  return (
    <section>
      <h3>{t(L.ui.settings.language)}</h3>
      {languages.map((lang) => (
        <button key={lang.code} aria-pressed={lang.code === current}
          onClick={() => void dispatchAction("localization.setLocale", { locale: lang.code })}>
          {lang.displayName}
        </button>
      ))}
    </section>
  );
}
```

（実際のマークアップ/クラスはStep 1で確認したホワイトリストと設置先パネルの既存構造に合わせる。`ui.settings.language` = Source/english `Language`・japanese `言語` をCSVへ追加し `npm run gen:i18n`）

- [ ] **Step 4: E2E: 言語切替の往復を検証する**

既存E2E慣習（`moorestech_web/webui/e2e/tests/`）に従い、言語選択→`localization.current` 変化→画面文言変化のテストを追加。

Run: `cd moorestech_web/webui && npx tsc -b && npm run lint && npm run test && npm run test:e2e`
Expected: 全て成功

- [ ] **Step 5: コミット**

```bash
git add Localization/localization.csv moorestech_web/webui/ && git commit -m "feat: webuiに言語選択UIを新設"
```

---

### Task 4: modMeta id修理と旧CSVの削除

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/modMeta.json`
- Delete: `../moorestech_master/server_v8/config/localization.csv` / `localization_settings.csv`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/config/localization.csv` / `localization_settings.csv`

- [ ] **Step 1: modMeta idを修理する**

`modMeta.json` を `{"id": "moorestechAlphaMod", "name": "moorestech Alpha Mod", "version": "1.0.0", "author": "moorestech"}` へ。スキーマ（`VanillaSchema/modMeta.yml`）の `required: true` が実際に検証されているか確認（`grep -rn "modMeta" moorestech_server/Assets/Scripts/Mod.Loader --include="*.cs"`）。検証が無ければ `ModsResource` のロード時に空idを例外にする1行を追加（validate-schemaスキルの趣旨に従う）。

- [ ] **Step 2: 旧CSVを削除する**

実行時読み込みはPlan1で廃止済みのため参照ゼロを確認して削除:

Run: `grep -rn "localization.csv\|localization_settings" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts --include="*.cs" | grep -v Generated`
Expected: ヒット0（あれば先に除去）

```bash
rm ../moorestech_master/server_v8/config/localization.csv ../moorestech_master/server_v8/config/localization_settings.csv
rm moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/config/localization.csv moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/config/localization_settings.csv
```

（Client.Tests側の.metaも削除に追従。EditModeInPlayingTestを1本実行してconfig欠落で落ちないことを確認: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EditModeInPlayingTest" は重いので代表1本に絞る`）

- [ ] **Step 3: コミット（両リポジトリ）**

```bash
cd ../moorestech_master && git add -A && git commit -m "chore: 旧config localization CSVを削除・modMeta id修理"
cd ../moorestech && git add -A && git commit -m "chore: テスト用旧localization CSVを削除"
```

---

### Task 5: 全体結合確認

- [ ] **Step 1: PlayModeでフル経路を確認する**

unity-playmode-recorded-playtestスキルでPlayMode起動し、録画付きで:
1. webuiの設定パネルに「English / 日本語」が表示名で並ぶ
2. Englishを選ぶ→全画面文言・アイテム名（Plan2サンプル辞書分）が英語化、他アイテム名はsource原文
3. 日本語へ戻す→全て日本語
4. ゲーム再起動→選択言語がPlayerPrefsから復元される
5. レガシーuGUIメインメニューのドロップダウンも引き続き動作する（LanguageSetting.cs無変更の確認）

- [ ] **Step 2: コミット**

```bash
git add -A && git commit -m "chore: 言語切替UI結合確認の調整" || true
```

---

### Task 6: 最終レビュー（省略不可）

- [ ] **Step 1: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

---

## 判断記録（ADR）

- 対応spec: [docs/superpowers/specs/2026-07-29-localization-foundation-design.md](../specs/2026-07-29-localization-foundation-design.md)
- **言語表示名も埋め込み（LanguageCatalog）** — 辞書CSVと同じライフサイクルで管理し、言語セットの唯一の定義を辞書CSVヘッダとしてsettings不一致をコンパイルエラー化する。出所: シミュレーター予測→ユーザー承認 2026-07-29
- **set localeはWS action機構（`localization.setLocale`）** — Web→ホストの状態変更は `IActionHandler` が既存標準（POST新設よりこちら）。出所: agent前提（`DebugActions.cs` / `WebSocketMessageDispatcher.cs:52-53` の前例一致）
- **言語一覧はHTTP GET** — 静的な埋め込みデータの配信は辞書と同じHTTP側（`/api/i18n/` 前例）。現在値だけがトピック。出所: agent前提（辞書=HTTP/現在値=トピックの既存2チャネル分離の踏襲）
- **旧CSVはテストデータ側も削除** — 実行時読み込み廃止後の残骸を残すと「読まれているように見える」誤解を生む。出所: agent前提

## 配置と前例

| 項目 | 配置先 | 前例（パス） |
|---|---|---|
| LocalizationLanguagesEndpoint | Client.WebUiHost/Game | `LocalizationDictionaryEndpoint.cs`（静的Endpoint・PathPrefix定数・WebUiJson） |
| SetLocaleActionHandler | Client.WebUiHost/Game/Actions | `Actions/DebugActions.cs`（IActionHandler・ActionType文字列・UniTask） |
| LanguageCatalog生成 | mooresmaster.Generator/Localization | Plan1の `LocalizationCodeGenerator`（同一emit経路の拡張） |
| 言語選択UI | webui features（既存システムパネル内） | webui-designホワイトリスト＋既存パネル前例（Task 3 Step 1で実測） |

機能パリティ（Phase 2.5 死活表）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| レガシーuGUIの言語ドロップダウン | 生きる | `LanguageSetting.cs` 無変更・`Localize.LanguageCodes`/`SetLanguage` API維持 |
| 言語のPlayerPrefs永続・再起動復元 | 生きる | `SetLanguage` の既存永続経路を再利用 |
| Steam言語コード（steam_api_lang_code） | 生きる（データ維持） | LanguageCatalogに埋め込み保持（消費は将来） |
| EditModeInPlayingTest | 生きる | config CSV削除は実行時参照ゼロ確認後（Task 4 Step 2） |
