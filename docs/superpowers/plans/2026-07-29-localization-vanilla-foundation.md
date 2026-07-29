---
spec: docs/superpowers/specs/2026-07-29-localization-foundation-design.md
---

# Localization Vanilla Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** バニラ文言（コード参照UI文言）の正本CSVをリポジトリに新設し、SourceGeneratorでC#型付きキー＋埋め込み辞書、codegenでTS型付きキーを生成し、webuiの日本語原文キー430個を名前空間キーへ一括移行する。

**Architecture:** `Localization/localization.csv` を単一正本とし、runtime参照可能な `mooresmaster.LocalizationCsv` 共通DLLがCSVパース・行モデル・例外を一元所有する。mooresmaster DLL内の第2 GeneratorクラスとUnity runtimeは同じ共通DLLを参照し、generatorは `Client.Localization` アセンブリへキー定数と辞書本体を埋め込む（実行時バニラCSVロード廃止）。webuiは同一CSVからNode製スクリプトでTS定数を生成し、欠落解決は 対象言語→english→source（原文）→`[!key]` とする。

**Tech Stack:** Roslyn IIncrementalGenerator (netstandard2.0) / Unity asmdef + csc.rsp / React + vitest + Node (mjs) / uloop

## Global Constraints

- partial禁止。如何なる条件でもpartialを絶対に使ってはいけない（AGENTS.md）
- `Func<>` 使用禁止。イベントはUniRx（AGENTS.md）
- try-catch原則禁止。例外は外部境界のみ・根拠コメント必須（AGENTS.md）
- 1ファイル200行以下（**自動生成ファイルとCSVは対象外**。生成物はMooresmaster.Model同様の扱い）
- 主要処理に日本語→英語の2行セットコメント（AGENTS.md）
- .metaファイル手動作成・スクリプト生成・上書き禁止。build.shから既存 `generate_meta` 関数と全呼び出しを削除し、全metaはUnity Editorだけに生成/設定させる
- Prefab/シーンの直接テキスト編集禁止。変更は `uloop execute-dynamic-code` 経由（AGENTS.md）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- 名前空間キーの表記: dot区切り・セグメントはlowerCamel（例 `ui.buildMenu.close`）。キーは「葉と枝を兼ねない」（`ui.save` と `ui.save.confirm` の併存禁止。generatorが検査）
- CSVヘッダは `key,Source,english,japanese`（初期2言語。言語セットの唯一の定義はこのヘッダ）
- generator用とUnity runtime用でCSV parser・行モデル・例外を複製しない。両者は `mooresmaster.LocalizationCsv.dll` の同一実装を参照する
- 空翻訳は欠落としてruntime辞書へ登録/返却せず次のfallback段へ進む。parserはCI検査のため空fieldを保持する
- コミットは各タスク末で必ず行う（worktree作業消失防止・AGENTS.md）

## File Structure

```
Localization/
└── localization.csv                     ← 新設・バニラ文言の単一正本

mooresmaster/
├── mooresmaster.LocalizationCsv/        ← 新設・runtime参照可能なnetstandard2.0共通DLL
│   ├── mooresmaster.LocalizationCsv.csproj
│   ├── LocalizationCsv.cs              ← CSV/行モデル
│   ├── LocalizationCsvException.cs     ← 不正入力例外
│   └── LocalizationCsvParser.cs        ← generator/runtime共用parser
├── mooresmaster.Generator/
│   ├── mooresmaster.Generator.csproj    ← 共通DLL ProjectReference追加
│   ├── LocalizationSourceGenerator.cs  ← 新設・第2の[Generator]（オーケストレーションのみ）
│   └── Localization/
│       ├── LocalizationKeyTree.cs       ← 新設・キー→ネスト木＋葉枝衝突検査（純関数）
│       └── LocalizationCodeGenerator.cs ← 新設・木＋辞書→C#コード文字列（純関数）
└── build.sh                             ← 共通DLL+generatorをclient/serverへデプロイ

mooresmaster/mooresmaster.Tests/LocalizationTests/
├── LocalizationCsvParserTest.cs         ← 新設
├── LocalizationKeyTreeTest.cs           ← 新設
└── LocalizationCodeGeneratorTest.cs     ← 新設

moorestech_client/Assets/Scripts/Client.Localization/
├── csc.rsp                              ← 新設・additionalfile配線
├── _CompileRequester.cs                 ← 新設・SchemaWatcherのtouch先
├── Localize.cs                          ← 全面書き換え（埋め込み辞書化）
└── TextMeshProLocalize.cs               ← try-catch除去・GetLegacy経由化

moorestech_client/Assets/Plugins/mooresmaster.LocalizationCsv.dll ← build.sh配置・Unityがruntime plugin metaを生成
moorestech_server/Assets/Plugins/mooresmaster.LocalizationCsv.dll ← build.sh配置・Unityがruntime plugin metaを生成

moorestech_client/Assets/Scripts/Client.Localization/Client.Localization.asmdef  ← versionDefines追加
moorestech_server/Assets/Scripts/Editor/SchemaWatcher.cs                          ← 監視対象の複数化

moorestech_web/webui/
├── scripts/generate-localization-keys.mjs        ← 新設・CSV→TS生成
├── src/shared/i18n/generated/localizationKeys.ts ← 生成物（コミットする）
├── src/shared/i18n/i18nStore.ts                  ← 型付け＋解決チェーン変更
├── src/shared/i18n/I18nProvider.tsx              ← source辞書fetch追加
├── src/shared/i18n/localizationKeysFreshness.test.ts ← 新設・CSVと生成物の鮮度検査
├── src/shared/i18n/allScreensI18n.test.ts        ← 全キー×全列の欠落検査へ拡張
└── src/**（約45コンポーネント）                    ← 430キーの一括移行
```

---

### Task 1: CSV正本の新設（初期キーセット）

**Files:**
- Create: `Localization/localization.csv`

**Interfaces:**
- Produces: `Localization/localization.csv` — ヘッダ `key,Source,english,japanese`。以降の全タスクの入力

- [ ] **Step 1: 旧CSVから生きているキーの日本語訳を確認する**

Run: `grep -E "^(Play locally|Exit Game|How To Controll|Save this game|Save and Back to MainMenu|Disconnected from server)," /Users/katsumi/moorestech_master/server_v8/config/localization.csv | cut -d, -f1,4`
Expected: 各キーの japanese 列の値が表示される（次ステップの訳文に使う）

- [ ] **Step 2: CSVを作成する**

`Localization/localization.csv`（japanese列はStep 1の実値で置き換える。以下は移行元の値）:

```csv
key,Source,english,japanese
ui.mainMenu.playLocally,Play locally,Play locally,ローカルでプレイ
ui.mainMenu.exitGame,Exit Game,Exit Game,ゲーム終了
ui.game.howToControl,How To Controll,How To Controll,操作方法
ui.game.saveGame,Save this game,Save this game,セーブする
ui.game.saveAndBackToMainMenu,Save and Back to MainMenu,Save and Back to MainMenu,セーブしてメインメニューに戻る
ui.game.disconnectedFromServer,Disconnected from server,Disconnected from server,サーバーから切断されました
```

- [ ] **Step 3: コミットする**

```bash
git add Localization/localization.csv
git commit -m "feat: バニラローカライズCSV正本を新設"
```

---

### Task 2: CSVパーサー共通DLL（generator/runtime共有・TDD）

**Files:**
- Create: `mooresmaster/mooresmaster.LocalizationCsv/mooresmaster.LocalizationCsv.csproj`
- Create: `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsv.cs`
- Create: `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvException.cs`
- Create: `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvParser.cs`
- Modify: `mooresmaster/mooresmaster.Generator/mooresmaster.Generator.csproj`
- Modify: `mooresmaster/mooresmaster.Tests/mooresmaster.Tests.csproj`
- Test: `mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationCsvParserTest.cs`

**Interfaces:**
- Assembly/namespace: `mooresmaster.LocalizationCsv` / `Mooresmaster.LocalizationCsv`
- Produces:
  - `sealed class LocalizationCsv { string[] LanguageCodes; LocalizationRow[] Rows; }`
  - `sealed class LocalizationRow { string Key; string Source; string[] Texts; }`（TextsはLanguageCodesと同順）
  - `public static LocalizationCsv LocalizationCsvParser.Parse(string csvText)`
  - `public static List<List<string>> LocalizationCsvParser.ParseRecords(string csvText)` — settings mapperも同じquote-aware record分割を再利用する
  - parserは空fieldを保持する。Source列と全翻訳列のliteral `\n` は同じく実改行へ変換する
  - 不正CSV（列数不一致・キー重複）は `LocalizationCsvException` を投げる（generator本体が既存のErrorFile機構で報告する）

- [ ] **Step 1: 失敗するテストを書く**

`mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationCsvParserTest.cs`:

```csharp
using Mooresmaster.LocalizationCsv;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationCsvParserTest
{
    [Fact]
    public void ヘッダから言語コードを取得できる()
    {
        var csv = "key,Source,english,japanese\nui.a.b,Hello,Hello,こんにちは\n";
        var result = LocalizationCsvParser.Parse(csv);
        Assert.Equal(new[] { "english", "japanese" }, result.LanguageCodes);
    }

    [Fact]
    public void 行のキーとテキストを取得できる()
    {
        var csv = "key,Source,english,japanese\nui.a.b,Hello,Hello,こんにちは\n";
        var result = LocalizationCsvParser.Parse(csv);
        var row = Assert.Single(result.Rows);
        Assert.Equal("ui.a.b", row.Key);
        Assert.Equal("Hello", row.Source);
        Assert.Equal(new[] { "Hello", "こんにちは" }, row.Texts);
    }

    [Fact]
    public void ダブルクォート内のカンマと改行エスケープを扱える()
    {
        var csv = "key,Source,english,japanese\nui.a.b,\"Hi, you\",\"Hi, you\",\"やあ\\nどうも\"\n";
        var result = LocalizationCsvParser.Parse(csv);
        Assert.Equal("Hi, you", result.Rows[0].Texts[0]);
        Assert.Equal("やあ\nどうも", result.Rows[0].Texts[1]);
    }

    [Fact]
    public void キー重複は例外()
    {
        var csv = "key,Source,english,japanese\nui.a,x,x,x\nui.a,y,y,y\n";
        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }

    [Fact]
    public void 列数不一致は例外()
    {
        var csv = "key,Source,english,japanese\nui.a,x,x\n";
        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }

    [Fact]
    public void Source列の改行エスケープを実改行へ変換する()
    {
        var csv = "key,Source,english,japanese\nui.a,Author\\nNote,English,日本語\n";
        var result = LocalizationCsvParser.Parse(csv);
        Assert.Equal("Author\nNote", result.Rows[0].Source);
    }

    [Fact]
    public void 空翻訳fieldは欠落検査のため保持する()
    {
        var csv = "key,Source,english,japanese\nui.a,Source,English,\n";
        var result = LocalizationCsvParser.Parse(csv);
        Assert.Equal("", result.Rows[0].Texts[1]);
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd mooresmaster && dotnet test --filter "FullyQualifiedName~LocalizationCsvParserTest"`
Expected: FAIL（LocalizationCsvParser が存在しない）

- [ ] **Step 3: 実装する**

`mooresmaster/mooresmaster.LocalizationCsv/mooresmaster.LocalizationCsv.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
```

`LocalizationCsv.cs` は上記interfaceの3値をconstructor必須で受ける2つのsealed class、`LocalizationCsvException.cs` は `Exception` 継承の公開例外とする。`LocalizationCsvParser.cs` はnetstandard2.0互換・外部依存なしで次を実装する:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mooresmaster.LocalizationCsv;

public static class LocalizationCsvParser
{
    private const int LanguageStartColumn = 2;

    public static LocalizationCsv Parse(string csvText)
    {
        // 行分割はクォート対応のフィールド分割で行う
        // Split records with quote-aware field splitting
        var records = ParseRecords(csvText);
        if (records.Count == 0) throw new LocalizationCsvException("localization.csv is empty");

        var header = records[0];
        var languageCodes = header.Skip(LanguageStartColumn).ToArray();

        var rows = new List<LocalizationRow>();
        var seenKeys = new HashSet<string>();
        for (var i = 1; i < records.Count; i++)
        {
            var fields = records[i];
            if (fields.Count != header.Count)
                throw new LocalizationCsvException($"Column count mismatch at line {i + 1}: expected {header.Count}, got {fields.Count}");
            var key = fields[0];
            if (!seenKeys.Add(key))
                throw new LocalizationCsvException($"Duplicated key: {key}");
            // \n エスケープは実改行へ変換する（既存Localize.csの挙動を踏襲）
            // Convert literal \n escapes to real newlines (same as legacy Localize.cs)
            var source = fields[1].Replace("\\n", "\n");
            var texts = fields.Skip(LanguageStartColumn).Select(t => t.Replace("\\n", "\n")).ToArray();
            rows.Add(new LocalizationRow(key, source, texts));
        }

        return new LocalizationCsv(languageCodes, rows.ToArray());
    }

    public static List<List<string>> ParseRecords(string text)
    {
        // RFC4180準拠の最小実装（ダブルクォート・埋め込みカンマ対応）
        // Minimal RFC4180-style parser (double quotes, embedded commas)
        var records = new List<List<string>>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n')
            {
                fields.Add(field.ToString()); field.Clear();
                if (fields.Any(f => f.Length > 0)) records.Add(fields);
                fields = new List<string>();
            }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        if (fields.Any(f => f.Length > 0)) records.Add(fields);
        return records;
    }
}
```

- [ ] **Step 4: generator/testsから共通projectだけを参照する**

`mooresmaster.Generator.csproj` と `mooresmaster.Tests.csproj` へ通常のProjectReferenceを追加する。Generator側のLocalizationコードは `using Mooresmaster.LocalizationCsv;` に統一し、generator配下にparser/model/exceptionを作らない。

```xml
<ProjectReference Include="..\mooresmaster.LocalizationCsv\mooresmaster.LocalizationCsv.csproj" />
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd mooresmaster && dotnet test --filter "FullyQualifiedName~LocalizationCsvParserTest"`
Expected: PASS（7件。Source改行変換と空field保持を含む）

- [ ] **Step 6: 重複実装が無いことを検査してコミットする**

Run: `rg -l "class LocalizationCsvParser" mooresmaster moorestech_client moorestech_server`
Expected: `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvParser.cs` の1件だけ

```bash
git add mooresmaster/mooresmaster.LocalizationCsv/ mooresmaster/mooresmaster.Generator/mooresmaster.Generator.csproj mooresmaster/mooresmaster.Tests/
git commit -m "feat: generatorとruntime共用のローカライズCSVライブラリ"
```

---

### Task 3: キー木の構築と葉枝衝突検査（TDD）

**Files:**
- Create: `mooresmaster/mooresmaster.Generator/Localization/LocalizationKeyTree.cs`
- Test: `mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationKeyTreeTest.cs`

**Interfaces:**
- Produces:
  - `class LocalizationKeyNode { string Segment; string FullKey; List<LocalizationKeyNode> Children; bool IsLeaf }`
  - `static LocalizationKeyNode LocalizationKeyTree.Build(LocalizationRow[] rows)` — ルートノードを返す。葉と枝を兼ねるキーは `LocalizationCsvException`

- [ ] **Step 1: 失敗するテストを書く**

`mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationKeyTreeTest.cs`:

```csharp
using System.Linq;
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationKeyTreeTest
{
    [Fact]
    public void ネスト木を構築できる()
    {
        var rows = new[]
        {
            new LocalizationRow("ui.buildMenu.close", "", new[] { "" }),
            new LocalizationRow("ui.buildMenu.title", "", new[] { "" }),
            new LocalizationRow("ui.inventory.title", "", new[] { "" }),
        };
        var root = LocalizationKeyTree.Build(rows);
        var ui = Assert.Single(root.Children);
        Assert.Equal("ui", ui.Segment);
        Assert.Equal(2, ui.Children.Count);
        var buildMenu = ui.Children.First(c => c.Segment == "buildMenu");
        Assert.Equal("ui.buildMenu.close", buildMenu.Children.First(c => c.Segment == "close").FullKey);
    }

    [Fact]
    public void 葉と枝を兼ねるキーは例外()
    {
        var rows = new[]
        {
            new LocalizationRow("ui.save", "", new[] { "" }),
            new LocalizationRow("ui.save.confirm", "", new[] { "" }),
        };
        Assert.Throws<LocalizationCsvException>(() => LocalizationKeyTree.Build(rows));
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd mooresmaster && dotnet test --filter "FullyQualifiedName~LocalizationKeyTreeTest"`
Expected: FAIL

- [ ] **Step 3: 実装する**

`mooresmaster/mooresmaster.Generator/Localization/LocalizationKeyTree.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace mooresmaster.Generator.Localization;

public class LocalizationKeyNode
{
    public string Segment = "";
    public string FullKey = "";
    public List<LocalizationKeyNode> Children = new();
    public bool IsLeaf;
}

public static class LocalizationKeyTree
{
    public static LocalizationKeyNode Build(LocalizationRow[] rows)
    {
        var root = new LocalizationKeyNode();
        foreach (var row in rows)
        {
            var node = root;
            var segments = row.Key.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var child = node.Children.FirstOrDefault(c => c.Segment == segment);
                if (child == null)
                {
                    child = new LocalizationKeyNode { Segment = segment, FullKey = string.Join(".", segments.Take(i + 1)) };
                    node.Children.Add(child);
                }
                node = child;
            }
            node.IsLeaf = true;
        }

        // 葉と枝の兼務を全ノードで検査する
        // Reject nodes that are both a leaf and a branch
        Validate(root);
        return root;

        void Validate(LocalizationKeyNode node)
        {
            if (node.IsLeaf && node.Children.Count > 0)
                throw new LocalizationCsvException($"Key '{node.FullKey}' is both a leaf and a branch");
            foreach (var child in node.Children) Validate(child);
        }
    }
}
```

（注: `Validate` はローカル関数だがメソッド末尾のため `#region Internal` は不要な規模。20行未満）

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd mooresmaster && dotnet test --filter "FullyQualifiedName~LocalizationKeyTreeTest"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add mooresmaster/mooresmaster.Generator/Localization/LocalizationKeyTree.cs mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationKeyTreeTest.cs
git commit -m "feat: ローカライズキー木の構築と葉枝衝突検査"
```

---

### Task 4: C#コード生成（TDD）

**Files:**
- Create: `mooresmaster/mooresmaster.Generator/Localization/LocalizationCodeGenerator.cs`
- Test: `mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationCodeGeneratorTest.cs`

**Interfaces:**
- Produces: `static string LocalizationCodeGenerator.Generate(LocalizationCsv csv)` — 単一の生成ソース文字列。中身は namespace `Mooresmaster.Localization.Generated` に:
  - `public readonly struct LocalizationKey { public readonly string Key; public LocalizationKey(string key) { Key = key; } }`
  - `public static class LocalizationKeys` — ネスト静的クラス（セグメントPascalCase化）。葉は `public static readonly LocalizationKey Xxx = new LocalizationKey("full.key");`
  - `public static class VanillaLocalizationTable`:
    - `public static readonly string[] LanguageCodes;`
    - `public static bool TryGetLanguage(string code, out IReadOnlyDictionary<string, string> dictionary)`
    - `public static IReadOnlyDictionary<string, string> SourceTexts { get; }`（key→Source列）

- [ ] **Step 1: 失敗するテストを書く**

`mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationCodeGeneratorTest.cs`:

```csharp
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationCodeGeneratorTest
{
    private const string Csv = "key,Source,english,japanese\nui.buildMenu.close,Close,Close,閉じる\n";

    [Fact]
    public void ネストキー定数が生成される()
    {
        var code = LocalizationCodeGenerator.Generate(LocalizationCsvParser.Parse(Csv));
        Assert.Contains("public static class Ui", code);
        Assert.Contains("public static class BuildMenu", code);
        Assert.Contains("public static readonly LocalizationKey Close = new LocalizationKey(\"ui.buildMenu.close\");", code);
    }

    [Fact]
    public void 辞書テーブルが生成される()
    {
        var code = LocalizationCodeGenerator.Generate(LocalizationCsvParser.Parse(Csv));
        Assert.Contains("\"english\"", code);
        Assert.Contains("\"閉じる\"", code);
        Assert.Contains("SourceTexts", code);
    }

    [Fact]
    public void 特殊文字がエスケープされる()
    {
        var csv = "key,Source,english,japanese\nui.a.b,\"He said \"\"hi\"\"\",\"He said \"\"hi\"\"\",改行\\nあり\n";
        var code = LocalizationCodeGenerator.Generate(LocalizationCsvParser.Parse(csv));
        Assert.Contains("\\\"hi\\\"", code);
        Assert.Contains("改行\\nあり", code);
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd mooresmaster && dotnet test --filter "FullyQualifiedName~LocalizationCodeGeneratorTest"`
Expected: FAIL

- [ ] **Step 3: 実装する**

`mooresmaster/mooresmaster.Generator/Localization/LocalizationCodeGenerator.cs`（生成物は200行制限対象外だが、この生成器自体は200行以下に収める。文字列エスケープは `SymbolDisplay.FormatLiteral` 相当を自前実装せず、`"` と `\` と改行のみの最小エスケープでよい — CSV由来テキストに他の制御文字は現れない前提を検査付きで置く）:

```csharp
using System.Linq;
using System.Text;

namespace mooresmaster.Generator.Localization;

public static class LocalizationCodeGenerator
{
    public static string Generate(LocalizationCsv csv)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated by LocalizationSourceGenerator />");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("namespace Mooresmaster.Localization.Generated");
        builder.AppendLine("{");
        builder.AppendLine("    public readonly struct LocalizationKey");
        builder.AppendLine("    {");
        builder.AppendLine("        public readonly string Key;");
        builder.AppendLine("        public LocalizationKey(string key) { Key = key; }");
        builder.AppendLine("    }");
        builder.AppendLine();
        EmitKeys(builder, LocalizationKeyTree.Build(csv.Rows));
        EmitTable(builder, csv);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void EmitKeys(StringBuilder builder, LocalizationKeyNode root) { /* 実装 */ }
    private static void EmitTable(StringBuilder builder, LocalizationCsv csv) { /* 実装 */ }
    private static string Escape(string text) { /* 実装 */ }
    private static string ToPascalCase(string segment) { /* 実装 */ }
}
```

実装の要点（各privateメソッドの中身）:

```csharp
    // EmitKeys: 木を深さ優先で辿り、枝は `public static class {Pascal}` を、
    // 葉は `public static readonly LocalizationKey {Pascal} = new LocalizationKey("{FullKey}");` を出す。
    // ルート直下は `public static class LocalizationKeys` に包む。インデントは深さ×4スペース。
    private static void EmitKeys(StringBuilder builder, LocalizationKeyNode root)
    {
        builder.AppendLine("    public static class LocalizationKeys");
        builder.AppendLine("    {");
        foreach (var child in root.Children) EmitNode(child, 2);
        builder.AppendLine("    }");
        builder.AppendLine();

        void EmitNode(LocalizationKeyNode node, int depth)
        {
            var indent = new string(' ', depth * 4);
            if (node.IsLeaf)
            {
                builder.AppendLine($"{indent}public static readonly LocalizationKey {ToPascalCase(node.Segment)} = new LocalizationKey(\"{node.FullKey}\");");
                return;
            }
            builder.AppendLine($"{indent}public static class {ToPascalCase(node.Segment)}");
            builder.AppendLine($"{indent}{{");
            foreach (var child in node.Children) EmitNode(child, depth + 1);
            builder.AppendLine($"{indent}}}");
        }
    }

    // EmitTable: LanguageCodes配列、言語ごとの Dictionary<string,string> を static readonly で持ち、
    // TryGetLanguage は言語コード→辞書のDictionary引き。SourceTexts は key→Source列のDictionary。
    private static void EmitTable(StringBuilder builder, LocalizationCsv csv)
    {
        builder.AppendLine("    public static class VanillaLocalizationTable");
        builder.AppendLine("    {");
        builder.Append("        public static readonly string[] LanguageCodes = new[] { ");
        builder.Append(string.Join(", ", csv.LanguageCodes.Select(c => $"\"{Escape(c)}\"")));
        builder.AppendLine(" };");
        builder.AppendLine("        private static readonly Dictionary<string, Dictionary<string, string>> Languages = BuildLanguages();");
        builder.AppendLine("        public static IReadOnlyDictionary<string, string> SourceTexts { get; } = BuildSourceTexts();");
        builder.AppendLine("        public static bool TryGetLanguage(string code, out IReadOnlyDictionary<string, string> dictionary)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (Languages.TryGetValue(code, out var values)) { dictionary = values; return true; }");
        builder.AppendLine("            dictionary = null;");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        // BuildLanguages / BuildSourceTexts は行データを `dict["key"] = "text";` の羅列で構築する
        // （実装時に csv.Rows をループして生成コードを書き出す）
        builder.AppendLine("    }");
    }

    // Escape: \ → \\、" → \"、実改行 → \n の3種のみ
    // ToPascalCase: 先頭1文字を大文字化（lowerCamelセグメント前提。'-' や '_' は不許可としてそのまま）
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd mooresmaster && dotnet test --filter "FullyQualifiedName~LocalizationCodeGeneratorTest"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add mooresmaster/mooresmaster.Generator/Localization/ mooresmaster/mooresmaster.Tests/LocalizationTests/
git commit -m "feat: ローカライズC#コード生成器"
```

---

### Task 5: 第2Generatorクラスの追加とDLLデプロイ

**Files:**
- Create: `mooresmaster/mooresmaster.Generator/LocalizationSourceGenerator.cs`
- Modify: `mooresmaster/build.sh`（共通DLLも同時ビルド・デプロイ）
- Modify: `moorestech_client/Assets/Plugins/mooresmaster.LocalizationCsv.dll`（build.sh経由）
- Modify: `moorestech_server/Assets/Plugins/mooresmaster.LocalizationCsv.dll`（build.sh経由）
- Create: `moorestech_client/Assets/Plugins/mooresmaster.LocalizationCsv.dll.meta`（Unity Editor自動生成）
- Create: `moorestech_server/Assets/Plugins/mooresmaster.LocalizationCsv.dll.meta`（Unity Editor自動生成）
- Modify: `moorestech_client/Assets/Plugins/mooresmaster.Generator.dll`（build.sh経由）
- Modify: `moorestech_server/Assets/Plugins/mooresmaster.Generator.dll`（build.sh経由）

**Interfaces:**
- Consumes: Task 2〜4 の `LocalizationCsvParser.Parse` / `LocalizationCodeGenerator.Generate`
- Produces: `localization.csv` という名前のAdditionalFileを持つコンパイル単位に `mooresmaster.localization.g.cs` を注入する `[Generator]`

- [ ] **Step 1: Generatorクラスを書く**

`mooresmaster/mooresmaster.Generator/LocalizationSourceGenerator.cs`（既存 `MooresmasterSourceGenerator.cs:20-76` の構造を踏襲。既存generatorは `.yml` のみ処理（同:157）するため両者は干渉しない）:

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class LocalizationSourceGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor ErrorDescriptor = new(
        "MOORES003",
        "Mooresmaster Localization Error",
        "Localization source generator failed: {0}",
        "Mooresmaster",
        DiagnosticSeverity.Error,
        true
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalTextsProvider = context.AdditionalTextsProvider.Collect();
        var parseOptions = context.ParseOptionsProvider.Select((options, _) =>
            options is CSharpParseOptions csharp && csharp.PreprocessorSymbolNames.Contains("ENABLE_MOORESMASTER_GENERATOR"));

        context.RegisterSourceOutput(additionalTextsProvider.Combine(parseOptions), (productionContext, input) =>
        {
            var (additionalTexts, enabled) = (input.Left, input.Right);
            if (!enabled) return;

            var csvFile = additionalTexts.FirstOrDefault(a => Path.GetFileName(a.Path) == "localization.csv");
            if (csvFile == null) return;

            // CSV不正はコンパイルエラーとして報告する（無言の欠落吸収をしない）
            // Report malformed CSV as a compile error (no silent fallback)
            try
            {
                var code = LocalizationCodeGenerator.Generate(LocalizationCsvParser.Parse(csvFile.GetText()!.ToString()));
                productionContext.AddSource("mooresmaster.localization.g.cs", code);
            }
            catch (LocalizationCsvException e)
            {
                // 外部入力（CSVファイル）のパース境界。Roslynの診断へ変換する
                // Boundary for external input (the CSV file); converted into a Roslyn diagnostic
                productionContext.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, e.Message));
            }
        });
    }
}
```

- [ ] **Step 2: build.shを共通DLLデプロイへ拡張する**

`mooresmaster/build.sh` は generator build後に `mooresmaster.LocalizationCsv/bin/Release/netstandard2.0/mooresmaster.LocalizationCsv.dll` の存在を検査し、client/serverの `Assets/Plugins/` へgenerator/commonのDLL本体だけをコピーする。既存 `generate_meta` 関数、meta heredoc、`sed`置換、`echo "Generating .meta files..."`、generator metaへの2呼び出しをすべて削除し、いかなるmetaも生成・上書きしない。追跡済み `mooresmaster.Generator.dll.meta` は既存RoslynAnalyzer設定のまま保持し、build前後でdiffが無いことを検査する。新規共通DLLの `.meta` は各Unity Editorにimportさせ、通常runtime plugin前例（`Microsoft.Extensions.DependencyInjection.Abstractions.dll.meta` の `Any.enabled: 1`）と同じ設定をUnityの `PluginImporter` API/Inspectorから適用する。

- [ ] **Step 3: 全テストとビルドを確認する**

Run: `cd mooresmaster && dotnet build mooresmaster.LocalizationCsv/ -c Release && dotnet build mooresmaster.Generator/ -c Release && dotnet test`
Expected: BUILD SUCCESS・全テストPASS

- [ ] **Step 4: DLLを両プロジェクトへデプロイする**

Run: `./mooresmaster/build.sh`
Expected: `mooresmaster.LocalizationCsv.dll` と `mooresmaster.Generator.dll` がclient/serverの両方へ配置される

Run: `uloop compile --project-path ./moorestech_client && uloop compile --project-path ./moorestech_server`
Expected: Unityが両方の `mooresmaster.LocalizationCsv.dll.meta` を生成し、RoslynAnalyzer labelなし・runtime plugin有効でcompile成功。設定がdefaultと異なる場合は `uloop execute-dynamic-code` から `PluginImporter.SetCompatibleWithAnyPlatform(true)` / `SaveAndReimport()` を実行し、meta YAMLを直接編集しない

- [ ] **Step 5: 配置と参照を検証してコミットする**

Run: `shasum -a 256 mooresmaster/mooresmaster.LocalizationCsv/bin/Release/netstandard2.0/mooresmaster.LocalizationCsv.dll moorestech_client/Assets/Plugins/mooresmaster.LocalizationCsv.dll moorestech_server/Assets/Plugins/mooresmaster.LocalizationCsv.dll`
Expected: 3ファイルのhashが一致

Run: `git diff --exit-code -- moorestech_client/Assets/Plugins/mooresmaster.Generator.dll.meta moorestech_server/Assets/Plugins/mooresmaster.Generator.dll.meta && ! rg -n "generate_meta|Generating \\.meta|\\.dll\\.meta" mooresmaster/build.sh`
Expected: 追跡済みgenerator metaは無変更、build.shのmeta生成/上書き処理は0件

```bash
git add mooresmaster/mooresmaster.Generator/LocalizationSourceGenerator.cs \
  mooresmaster/build.sh \
  moorestech_client/Assets/Plugins/mooresmaster.LocalizationCsv.dll \
  moorestech_client/Assets/Plugins/mooresmaster.LocalizationCsv.dll.meta \
  moorestech_server/Assets/Plugins/mooresmaster.LocalizationCsv.dll \
  moorestech_server/Assets/Plugins/mooresmaster.LocalizationCsv.dll.meta \
  moorestech_client/Assets/Plugins/mooresmaster.Generator.dll \
  moorestech_server/Assets/Plugins/mooresmaster.Generator.dll
git commit -m "feat: ローカライズ共通DLLとSourceGeneratorを両プロジェクトへデプロイ"
```

---

### Task 6: Unity側配線（csc.rsp・versionDefines・SchemaWatcher）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Localization/csc.rsp`
- Create: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Client.Localization.asmdef:18`
- Modify: `moorestech_server/Assets/Scripts/Editor/SchemaWatcher.cs`

**Interfaces:**
- Produces: `Client.Localization` のコンパイルで `Mooresmaster.Localization.Generated.*` が使用可能になる。`Localization/` 編集で自動再コンパイル

- [ ] **Step 1: csc.rsp を作る**

`moorestech_client/Assets/Scripts/Client.Localization/csc.rsp`（`Core.Master/csc.rsp:1-10` と同形式。プロジェクトルート相対）:

```
/additionalfile:Assets/../../Localization/localization.csv
```

- [ ] **Step 2: asmdef に versionDefines を足す**

`Client.Localization.asmdef` の `"versionDefines": []` を（`Core.Master.asmdef` の同項目と同形式で）:

```json
  "versionDefines": [
    {
      "name": "Unity",
      "expression": "",
      "define": "ENABLE_MOORESMASTER_GENERATOR"
    }
  ],
```

- [ ] **Step 3: _CompileRequester を作る**

`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（`Core.Master/_CompileRequester.cs` と同型・クラス名は衝突回避で変える）:

```csharp
// このコードはClient.Localizationアセンブリを再コンパイルするためのスクリプトです。SchemaWatcherによって更新されます。
// This code is a script to recompile the Client.Localization assembly. It is updated by SchemaWatcher.
public class LocalizationCompileRequester
{
// ローカライズCSVを更新したら、こちらの更新もコミットしてください。
// If you update the localization csv, please also commit this update.
    private const string dummyText = "initial";
}
```

- [ ] **Step 4: SchemaWatcher を複数監視対象へ一般化する**

`moorestech_server/Assets/Scripts/Editor/SchemaWatcher.cs` を修正。単一の `schemaFolderPath`/`coreMasterFolderPath`（L12-26）を「監視対象リスト」へ置き換える:

```csharp
    // 監視対象: 監視フォルダ → 変更時にtouchするCompileRequesterのフォルダ
    // Watch targets: watched folder -> folder whose CompileRequester gets touched on change
    private static readonly (string watchPath, string requesterFolder, string requesterFile, string className)[] watchTargets;

    static SchemaWatcher()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../"));
        watchTargets = new[]
        {
            (Path.Combine(repoRoot, "VanillaSchema"),
             Path.Combine(repoRoot, "moorestech_server/Assets/Scripts/Core.Master"),
             "_CompileRequester.cs", "CompileRequester"),
            (Path.Combine(repoRoot, "Localization"),
             Path.Combine(repoRoot, "moorestech_client/Assets/Scripts/Client.Localization"),
             "_CompileRequester.cs", "LocalizationCompileRequester"),
        };
        // 以下、既存のLoadCache/EditorApplication.update登録は維持
    }
```

`CheckForChanges`（L69-102）は watchTargets をループし、対象ごとに独立したハッシュキャッシュ（キャッシュキーに watchPath を含める）で変更検知、変更のあった対象の requester のみ touch する。`UpdateDummyScript`（L144-172）は `className` を引数に取る形へ一般化（生成する `class {className}` 名を差し替え）。ファイル全体で200行を超える場合は `SchemaWatchTarget.cs` へ監視対象定義を分離する。

- [ ] **Step 5: コンパイルして生成コードの有効性を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。（`Mooresmaster.Localization.Generated` はまだ未参照なので警告も出ない）

確認として一時的に `Localize.cs` の任意メソッド内で `var _ = Mooresmaster.Localization.Generated.LocalizationKeys.Ui.MainMenu.PlayLocally;` を書いてコンパイル→成功を確認→行を戻す。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Localization/csc.rsp \
  moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs \
  moorestech_client/Assets/Scripts/Client.Localization/Client.Localization.asmdef \
  moorestech_server/Assets/Scripts/Editor/SchemaWatcher.cs
git add moorestech_client/Assets/Scripts/Client.Localization/*.meta 2>/dev/null || true
git commit -m "feat: Client.LocalizationへのCSV additionalfile配線とSchemaWatcher複数監視化"
```

（.metaはUnityが自動生成したもののみコミット。手動作成禁止）

---

### Task 7: Localize の埋め込み辞書化と TextMeshProLocalize 修正

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`（全面書き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/TextMeshProLocalize.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs:43`

**Interfaces:**
- Consumes: `VanillaLocalizationTable`（Task 5）
- Produces:
  - `public static string Localize.Get(LocalizationKey key)` — 欠落時 `[!{key}]`
  - `public static string Localize.GetLegacy(string rawKey)` — Prefab直列化キー専用（TextMeshProLocalize/UGuiTooltipTarget のみが呼ぶ）
  - `public static void Localize.SetLanguage(string languageCode)` / `IObservable<Unit> OnLanguageChanged` / `string CurrentLanguageCode` / `List<string> LanguageCodes`
  - `public static bool Localize.TryGetDictionary(string languageCode, out IReadOnlyDictionary<string, string> dictionary)` — `"source"` 擬似ロケールにも応答（Web配信用）

- [ ] **Step 1: 書き換え後の Localize.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;

namespace Client.Localization
{
    public static class Localize
    {
        private const string DefaultLanguageCode = "english";
        public const string SourcePseudoLocale = "source";

        // 言語コード → (キー → テキスト)。初期化後は不変で、Web配信にも同じ正本を公開する
        // languageCode -> (key -> text). Immutable after init; also served to the Web as-is
        private static readonly Dictionary<string, Dictionary<string, string>> mergedDictionary = new();

        private static readonly Subject<Unit> _onLanguageChangedSubject = new();
        public static IObservable<Unit> OnLanguageChanged => _onLanguageChangedSubject;

        public static string CurrentLanguageCode { get; private set; }
        public static List<string> LanguageCodes => VanillaLocalizationTable.LanguageCodes.ToList();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            mergedDictionary.Clear();

            // 埋め込みテーブルから合成辞書を構築する（mod辞書の合成はPlan2で追加）
            // Build the merged dictionary from the embedded table (mod merge lands in Plan2)
            foreach (var code in VanillaLocalizationTable.LanguageCodes)
            {
                VanillaLocalizationTable.TryGetLanguage(code, out var table);
                mergedDictionary[code] = table
                    .Where(p => !string.IsNullOrEmpty(p.Value))
                    .ToDictionary(p => p.Key, p => p.Value);
            }
            mergedDictionary[SourcePseudoLocale] = VanillaLocalizationTable.SourceTexts
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, p => p.Value);

            // PlayerPrefsの言語が現行の言語セットに無い場合はデフォルトへ戻す（旧実装の例外バグ修理）
            // Fall back to the default when the persisted language is no longer in the set (fixes legacy crash)
            var saved = PlayerPrefs.GetString("LanguageCode", DefaultLanguageCode);
            CurrentLanguageCode = mergedDictionary.ContainsKey(saved) && saved != SourcePseudoLocale ? saved : DefaultLanguageCode;
        }

        public static string Get(LocalizationKey key)
        {
            return GetLegacy(key.Key);
        }

        public static string GetLegacy(string rawKey)
        {
            // Prefab直列化キーの後方経路。新規コードは必ずLocalizationKey側のGetを使うこと
            // Legacy path for prefab-serialized keys; new code must use the LocalizationKey overload
            if (mergedDictionary[CurrentLanguageCode].TryGetValue(rawKey, out var value) && !string.IsNullOrEmpty(value)) return value;
            if (mergedDictionary[DefaultLanguageCode].TryGetValue(rawKey, out var english) && !string.IsNullOrEmpty(english)) return english;
            if (mergedDictionary[SourcePseudoLocale].TryGetValue(rawKey, out var source) && !string.IsNullOrEmpty(source)) return source;
            return $"[!{rawKey}]";
        }

        public static void SetLanguage(string languageCode)
        {
            if (languageCode != SourcePseudoLocale && mergedDictionary.ContainsKey(languageCode))
            {
                CurrentLanguageCode = languageCode;
                PlayerPrefs.SetString("LanguageCode", languageCode);
                PlayerPrefs.Save();
                _onLanguageChangedSubject.OnNext(Unit.Default);
            }
            else
            {
                Debug.LogError($"[Localize] Language Code : {languageCode} is not found");
            }
        }

        public static bool TryGetDictionary(string languageCode, out IReadOnlyDictionary<string, string> dictionary)
        {
            if (mergedDictionary.TryGetValue(languageCode, out var values))
            {
                dictionary = values;
                return true;
            }
            dictionary = null;
            return false;
        }
    }
}
```

（`Server.Boot` 参照は不要になるので asmdef の references から `Server.Boot` を削除する。CsvHelper依存も消える）

- [ ] **Step 2: TextMeshProLocalize の try-catch 除去**

`TextMeshProLocalize.cs` の `SetKey`（L24-45）を書き換え（`string.Format` の失敗はキー整備の問題であり実行時に握り潰さない。規約どおりtry-catch禁止へ戻す）:

```csharp
        public void SetKey(string key, params string[] addContents)
        {
            this.key = key;
            var text = string.Format(Localize.GetLegacy(key), addContents);
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            _text.text = text;
            _text.ForceMeshUpdate();
        }
```

`Awake`（L15-22）の `Localize.Get(key)` 2箇所は `Localize.GetLegacy(key)` へ。

- [ ] **Step 3: MouseCursorTooltip の参照を更新する**

`MouseCursorTooltip.cs:43` の `Localize.Get(key)` を `Localize.GetLegacy(key)` へ（`isLocalize:false` 経路は無変更）。

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 5: Prefab/シーンの直列化キーを新キーへ更新する**

`uloop execute-dynamic-code` で以下を実行（Prefab直接編集禁止のため）。対象は `MainMenu.unity`（`Play locally`→`ui.mainMenu.playLocally`, `Exit Game`→`ui.mainMenu.exitGame`）、`MainGameUI.prefab`（`How To Controll`→`ui.game.howToControl`, `Save this game`→`ui.game.saveGame`, `Save and Back to MainMenu`→`ui.game.saveAndBackToMainMenu`）:

```csharp
// TextMeshProLocalizeのkeyフィールドを新名前空間キーへ書き換える
// Rewrite serialized TextMeshProLocalize keys to the new namespaced keys
var mapping = new Dictionary<string, string>
{
    { "Play locally", "ui.mainMenu.playLocally" },
    { "Exit Game", "ui.mainMenu.exitGame" },
    { "How To Controll", "ui.game.howToControl" },
    { "Save this game", "ui.game.saveGame" },
    { "Save and Back to MainMenu", "ui.game.saveAndBackToMainMenu" },
};
// MainGameUI.prefab
var prefabPath = "Assets/Asset/UI/Prefab/MainGameUI.prefab";
var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
foreach (var localize in prefab.GetComponentsInChildren<Client.Localization.TextMeshProLocalize>(true))
{
    var so = new SerializedObject(localize);
    var keyProp = so.FindProperty("key");
    if (mapping.TryGetValue(keyProp.stringValue, out var next)) { keyProp.stringValue = next; so.ApplyModifiedProperties(); }
}
PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
PrefabUtility.UnloadPrefabContents(prefab);
// MainMenu.unity はシーンを開いて同処理→EditorSceneManager.SaveScene
```

- [ ] **Step 6: 動作確認とコミット**

Run: `uloop compile --project-path ./moorestech_client && uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: エラー0

```bash
git add moorestech_client/Assets/Scripts/Client.Localization/ \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs \
  moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab \
  moorestech_client/Assets/Scenes/Game/MainMenu.unity
git commit -m "feat: Localizeを埋め込み辞書化しCSV実行時ロードを廃止"
```

---

### Task 8: TS定数生成スクリプトと鮮度テスト

**Files:**
- Create: `moorestech_web/webui/scripts/generate-localization-keys.mjs`
- Create: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（スクリプトで生成しコミット）
- Test: `moorestech_web/webui/src/shared/i18n/localizationKeysFreshness.test.ts`

**Interfaces:**
- Produces:
  - `export const L = { ui: { mainMenu: { playLocally: "ui.mainMenu.playLocally", ... } } } as const;`
  - `export type VanillaLocalizationKey = "ui.mainMenu.playLocally" | ...;`（union）
  - 生成コマンド: `node scripts/generate-localization-keys.mjs`（CSVパスはスクリプトから相対で `../../../Localization/localization.csv`）

- [ ] **Step 1: 失敗する鮮度テストを書く**

`localizationKeysFreshness.test.ts`（生成物がCSVと同期しているかをCIで担保。生成ロジック自体をテストから再利用する）:

```typescript
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { generateLocalizationKeysSource, parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";

describe("localizationKeys freshness", () => {
  it("generated file matches the CSV source of truth", () => {
    const csvPath = new URL("../../../../../Localization/localization.csv", import.meta.url);
    const generatedPath = new URL("./generated/localizationKeys.ts", import.meta.url);
    const expected = generateLocalizationKeysSource(parseLocalizationCsv(readFileSync(csvPath, "utf8")));
    expect(readFileSync(generatedPath, "utf8")).toBe(expected);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/shared/i18n/localizationKeysFreshness.test.ts`
Expected: FAIL（スクリプト未実装）

- [ ] **Step 3: 生成スクリプトを実装する**

`scripts/generate-localization-keys.mjs`（Nodeビルドツール用parserは共通fixtureでクォート・埋め込みカンマ・`\n`変換・キー重複/列数検査の期待値を固定する。C#のgenerator/runtime間ではTask 2の共通DLLだけを使う。`generateLocalizationKeysSource` はキー木→ネストobjectリテラル＋union型を文字列生成し、CLI実行時に生成物へ書き込む）:

```javascript
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

export function parseLocalizationCsv(text) { /* C#版と同仕様のRFC4180最小実装。{ languageCodes, rows: [{key, source, texts}] } を返す */ }

export function generateLocalizationKeysSource(csv) {
  // キー木を作り、ネストobjectと型unionを出力する。ヘッダに生成注意コメントを付ける
  // Build the key tree and emit the nested object plus the key union type
  const header = "// generated by scripts/generate-localization-keys.mjs — DO NOT EDIT\n";
  /* ... L定数とVanillaLocalizationKey unionを構築 ... */
  return header + body;
}

const isCli = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isCli) {
  const csv = parseLocalizationCsv(readFileSync(new URL("../../../Localization/localization.csv", import.meta.url), "utf8"));
  writeFileSync(new URL("../src/shared/i18n/generated/localizationKeys.ts", import.meta.url), generateLocalizationKeysSource(csv));
}
```

- [ ] **Step 4: 生成を実行し、テストが通ることを確認する**

Run: `cd moorestech_web/webui && node scripts/generate-localization-keys.mjs && npx vitest run src/shared/i18n/localizationKeysFreshness.test.ts`
Expected: PASS

- [ ] **Step 5: package.json に生成スクリプトを登録する**

`"scripts"` へ追加: `"gen:i18n": "node scripts/generate-localization-keys.mjs"`

- [ ] **Step 6: コミットする**

```bash
git add moorestech_web/webui/scripts/generate-localization-keys.mjs \
  moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts \
  moorestech_web/webui/src/shared/i18n/localizationKeysFreshness.test.ts \
  moorestech_web/webui/package.json
git commit -m "feat: CSVからTSローカライズキー定数を生成"
```

---

### Task 9: i18nStore の型付けと解決チェーン変更

**Files:**
- Modify: `moorestech_web/webui/src/shared/i18n/i18nStore.ts`
- Modify: `moorestech_web/webui/src/shared/i18n/I18nProvider.tsx`
- Modify: `moorestech_web/webui/src/shared/i18n/allScreensI18n.test.ts`

**Interfaces:**
- Consumes: `VanillaLocalizationKey`（Task 8）
- Produces:
  - `export type TranslationKey = VanillaLocalizationKey;`（Plan2でcontent key unionを合流させる拡張点）
  - `t(key: TranslationKey, values?: InterpolationValues): string` — 空文字を欠落へ正規化して対象辞書→fallback辞書→source辞書→`[!key]`
  - `I18nSnapshot` に `sourceDictionary: TranslationDictionary` を追加。`setDictionaries(locale, dictionary, fallbackDictionary, sourceDictionary)`

- [ ] **Step 1: i18nStore.ts を変更する**

変更点（`i18nStore.ts:32-48`）:

```typescript
import type { VanillaLocalizationKey } from "./generated/localizationKeys";

export type TranslationKey = VanillaLocalizationKey;

function nonEmptyTranslation(value: string | undefined): string | undefined {
  return value === undefined || value.length === 0 ? undefined : value;
}

export function createTranslator(current: I18nSnapshot) {
  const warnedKeysForGeneration = warnedMissingTranslationKeys;
  return (key: TranslationKey, values: InterpolationValues = {}): string => {
    const template =
      nonEmptyTranslation(current.dictionary[key]) ??
      nonEmptyTranslation(current.fallbackDictionary[key]) ??
      nonEmptyTranslation(current.sourceDictionary[key]);
    if (template === undefined && !warnedKeysForGeneration.has(key)) {
      warnedKeysForGeneration.add(key);
      console.warn(`[i18n] Missing translation key: ${key}`);
    }

    // 欠落キーは無言の原文表示ではなく目立つプレースホルダで露出させる
    // Missing keys surface as a loud placeholder instead of silently echoing the key
    return (template ?? `[!${key}]`).replace(/\{([^{}]+)\}/g, (token, name: string) =>
      Object.hasOwn(values, name) ? String(values[name]) : token);
  };
}
```

`I18nSnapshot`・`setDictionaries`（L8-30）へ `sourceDictionary` を追加（初期値 `{}`）。

- [ ] **Step 2: I18nProvider.tsx で source 辞書を取得する**

`loadDictionaries`（L22-31）を3辞書並列fetchへ:

```typescript
async function loadDictionaries(locale: string, signal: AbortSignal): Promise<void> {
  const fallbackPromise = fetchDictionary(FALLBACK_LOCALE, signal);
  const sourcePromise = fetchDictionary("source", signal);
  const dictionaryPromise = locale === FALLBACK_LOCALE ? fallbackPromise : fetchDictionary(locale, signal);
  const [dictionary, fallbackDictionary, sourceDictionary] = await Promise.all([dictionaryPromise, fallbackPromise, sourcePromise]);
  if (signal.aborted) return;

  document.documentElement.lang = locale;
  document.documentElement.dataset.locale = locale;
  setDictionaries(locale, dictionary, fallbackDictionary, sourceDictionary);
}
```

（`LocalizationDictionaryEndpoint` は `Localize.TryGetDictionary` を素通しするため、Task 7 の `source` 擬似ロケール対応でホスト側は無変更で応答する）

- [ ] **Step 3: allScreensI18n.test.ts を「全キー×全列」検査へ拡張する**

既存の再レンダリング検査は維持しつつ（テスト内の `t("画面タイトル")` 相当は生成キーの実在キーへ差し替え）、以下を追加:

```typescript
  it("every generated key has a non-empty translation for every language column", () => {
    const csvPath = new URL("../../../../../Localization/localization.csv", import.meta.url);
    const csv = parseLocalizationCsv(readFileSync(csvPath, "utf8"));
    for (const row of csv.rows) {
      row.texts.forEach((text, i) => {
        expect(text, `key '${row.key}' is missing '${csv.languageCodes[i]}'`).not.toBe("");
      });
      expect(row.source, `key '${row.key}' is missing Source`).not.toBe("");
    }
  });
```

`i18nStore` の単体テストへ、対象言語が `""` ならenglish、対象+englishが `""` ならsource、3段すべて `""` なら `[!key]` を返す3ケースを追加し、空文字が表示値にならないことを固定する。

- [ ] **Step 4: テスト実行**

Run: `cd moorestech_web/webui && npx vitest run src/shared/i18n/`
Expected: PASS（この時点で`t()`呼び出し430箇所が型エラーになるのは次タスクで解消するため、`npx tsc -b` はまだ実行しない）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/shared/i18n/
git commit -m "feat: i18nStoreを型付きキー化しsource原文フォールバックへ変更"
```

---

### Task 10: webui 430キーの一括移行

**Files:**
- Modify: `moorestech_web/webui/src/**`（`t()` を呼ぶ約45コンポーネント＋`.ts`ロジックファイル）
- Modify: `Localization/localization.csv`（全キーの行を追加）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（再生成）

**Interfaces:**
- Consumes: `L` 定数・`TranslationKey`（Task 8/9）
- Produces: `t()` の全呼び出しが `L.ui.<feature>.<name>` 参照になり、`npx tsc -b` が通る状態

- [ ] **Step 1: 現状の全キーを抽出する**

Run:
```bash
cd moorestech_web/webui && grep -rnoE 't\("([^"]+)"' src --include='*.ts' --include='*.tsx' | sort -u > /tmp/i18n-keys.txt && wc -l /tmp/i18n-keys.txt
```
Expected: 約430行のリスト

- [ ] **Step 2: キー割当表を作る**

命名規約: `ui.<featureディレクトリ名lowerCamel>.<意味名lowerCamel>`。共有UIは `ui.common.<name>`、エラー系は `ui.error.<name>`。代表例（実際の訳はこの表に倣い全キー分作成する）:

| 現キー（日本語原文） | 新キー | english訳 |
|---|---|---|
| `持ち物` (InventoryPanel/index.tsx:24) | `ui.inventory.title` | `Inventory` |
| `ビルドメニュー` (BuildMenuPanel.tsx:50) | `ui.buildMenu.title` | `Build Menu` |
| `閉じる` (BuildMenuPanel.tsx) | `ui.common.close` | `Close` |
| `該当なし` (BuildMenuPanel.tsx:64) | `ui.common.noResults` | `No results` |
| `レシピ選択` (MachineSection.tsx:54) | `ui.blockInventory.recipeSelection` | `Select Recipe` |
| `（消費 {count}件, 供給率 {rate}%）` (NetworkSections.tsx:44) | `ui.blockInventory.consumerSummary` | `(consuming {count}, supply rate {rate}%)` |
| `UIエラーが発生しました` (AppErrorBoundary.tsx:39) | `ui.error.uiErrorOccurred` | `A UI error occurred` |
| `クラフト` (craftLogic.ts:41) | `ui.recipe.craft` | `Craft` |

文断片の連結キーは**キー統合＋補間**で直す（機械置換ではなく呼び出し側の再構成）。既知の2箇所:
- `NetworkSections.tsx:21` の `t(" / 需要 ")` — 前後の連結を `ui.blockInventory.supplyDemandSummary` = `"{supply} / demand {demand}"` 形式の単一キーへ
- `ResearchScreenChrome.tsx:11` の `t(": インベントリ")` — 接頭辞連結を `{name}` 補間の単一キーへ

他の断片は Step 1 のリストから「単語未満・記号始まり・文末/文頭が接続的」なキーを目視で拾い、同様に統合する。

- [ ] **Step 3: CSVへ全行を追加し再生成する**

割当表に従い `Localization/localization.csv` へ全キーの行を追加（Source=english訳、japanese=現原文）。englishはこの移行作業内で翻訳して埋める（空欄はTask 9のテストが弾く）。

Run: `cd moorestech_web/webui && node scripts/generate-localization-keys.mjs`

- [ ] **Step 4: 呼び出し側を一括置換する**

割当表に基づき `t("持ち物")` → `t(L.ui.inventory.title)` の形へ全置換（`import { L } from "@/shared/i18n";` を追加。barrelの `src/shared/i18n/index.ts` から `L` をre-exportする）。`.ts` ロジックファイルが返す表示文字列（`craftLogic.ts:41` 等）は `TranslationKey` を返す形へ変え、呼び出し側の `t(button.tooltip)` は型が保証する。

- [ ] **Step 5: 型チェック・lint・テストで移行漏れを検出する**

Run: `cd moorestech_web/webui && npx tsc -b && npm run lint && npm run test`
Expected: 全て成功。`tsc` が旧日本語キーの残存を型エラーとして検出する（`t()` の引数はunion型のため、辞書に無いリテラルはコンパイルエラー）

- [ ] **Step 6: E2Eの文言参照を追従する**

Run: `cd moorestech_web/webui && npm run test:e2e`
E2Eが日本語文言をセレクタに使っている場合、表示は japanese 辞書で不変のため原則通る。落ちたケースのみ期待文言を確認して追従する。

- [ ] **Step 7: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui/
git commit -m "feat: webuiの430キーを名前空間ローカライズキーへ全面移行"
```

---

### Task 11: 結合確認（言語切替の実機経路）

**Files:**
- なし（確認のみ）

- [ ] **Step 1: PlayModeで言語切替を確認する**

`uloop` でPlayMode起動し（unity-playmode-recorded-playtestスキルの手順に従う）、以下を確認:
1. 起動後、webuiに日本語文言が表示される（辞書経由。console.warnの `[i18n] Missing translation key` が0件）
2. `uloop execute-dynamic-code` で `Client.Localization.Localize.SetLanguage("english");` を実行
3. webuiの全画面文言がenglishへ切り替わる（`localization.current` push→再fetch→再描画）
4. `[!` プレースホルダが画面に出ていない

- [ ] **Step 2: エンドポイントを直接確認する**

Run: `curl -s http://localhost:<WebUiHostポート>/api/i18n/source | head -c 300`
Expected: key→Source原文のJSONが返る（ポートは `WebUiEndpoints.cs` 起動ログで確認）

- [ ] **Step 3: 未コミット分が無いことを確認しコミットする**

```bash
git status --short && git add -A && git commit -m "chore: ローカライズバニラ基盤の結合確認調整" || true
```

---

### Task 12: 最終レビュー（省略不可）

- [ ] **Step 1: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の機械的修正を適用し、設計判断はユーザーへ。

---

## 判断記録（ADR）

- 対応spec: [docs/superpowers/specs/2026-07-29-localization-foundation-design.md](../specs/2026-07-29-localization-foundation-design.md)（ADR 0005/0006へのリンクを含む）
- **t()への生文字列禁止はlintルールではなく型unionで実現** — `TranslationKey` がstring-literal unionのため、辞書に無いキーは `tsc` がコンパイルエラー化する。存在するキーのリテラル直書き（`t("ui.common.close")`）は型上許容されるが、存在検査という目的は達成される。出所: agent前提（ユーザー裁定「キー切れを両側でビルド時エラー化」の実現手段の選定。lint追加より強い保証）
- **Prefab直列化キーのために `GetLegacy(string)` を残す** — TextMeshProLocalize/UGuiTooltipTargetのSerializeFieldキーは文字列のままにし、値だけ新キーへ更新。uGUI残置方針（メモリ: ui-web-migration-complete）のため型付き化の投資をしない。出所: agent前提
- **キーは葉と枝を兼ねない** — C#ネストクラス生成の構造的制約をCSV検査として明文化。出所: agent前提（generatorの型構造上の必然）
- **SchemaWatcherは監視対象リストへ一般化** — VanillaSchema監視の既存機構（`SchemaWatcher.cs:19-26`）をそのまま複数対象化。出所: agent前提（既存前例の拡張）
- **CSV parserはruntime参照可能な共通DLLへ分離** — generator/runtimeの依存方向を共通の純粋ライブラリへ揃え、実装コピーを禁止する。build.shは共通DLLとgenerator DLLをclient/serverへ同時デプロイする。出所: ユーザー裁定 2026-07-29
- **GetLegacyもsourceを含む4段解決** — Prefab直列化キーも対象言語→english→source→`[!key]` を省略しない。出所: ユーザー裁定 2026-07-29
- **空文字は欠落としてfallbackを継続** — parserは空fieldを保持するが、runtime合成/解決は空文字を登録/返却しない。Source列のliteral `\n` も翻訳列と同様に実改行へ変換する。出所: Task 0 review finding 2026-07-29
- **全DLL metaはUnity管理** — build.shからgeneratorを含む全meta生成/上書きを撤廃する。追跡済みgenerator metaは保持し、新規runtime DLL metaとPluginImporter設定はclient/server Unity Editorの正規APIだけで作る。出所: Task 0 re-review finding 2026-07-29

## 配置と前例

| 項目 | 配置先 | 前例（パス） |
|---|---|---|
| LocalizationCsvParser / 行モデル / 例外 | mooresmaster.LocalizationCsv（netstandard2.0共通DLL） | generator/runtime双方の下流にドメイン語彙を持たない純粋CSV境界として新設。実装は1箇所、metaはUnity生成 |
| LocalizationSourceGenerator ほかgenerator側2ファイル | mooresmaster.Generator（同一DLL・第2Generator） | `mooresmaster/mooresmaster.Generator/MooresmasterSourceGenerator.cs:20`（[Generator]クラス構造・ENABLE define検査・診断報告） |
| generatorテスト | mooresmaster.Tests/LocalizationTests | `mooresmaster/mooresmaster.Tests/`（機能別ディレクトリ構成） |
| csc.rsp / versionDefines | Client.Localization | `moorestech_server/Assets/Scripts/Core.Master/csc.rsp:1` / `Core.Master.asmdef` versionDefines |
| _CompileRequester | Client.Localization | `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（SchemaWatcher touch先の前例） |
| Localize書き換え | Client.Localization（既存位置） | 既存 `Localize.cs`（static公開・UniRx通知・TryGetDictionaryのWeb共有コメント L35-36） |
| TS生成スクリプト | webui/scripts | webuiの既存 `scripts/` 慣習（無ければ新設。package.json scripts登録で公開） |
| 生成TS | src/shared/i18n/generated | `src/bridge/` の生成物コミット前例（bridge契約の生成TSと同運用） |
| 解決チェーン変更 | i18nStore.ts | 既存 `i18nStore.ts:35`（`?? fallbackDictionary[key]` の既存チェーンの拡張） |

機構選択（検査4）: 既存の実行時CSVロード機構は「置換」であり受動的統合案（実行時ロード維持＋キー定数だけ生成）と比較した。ユーザー裁定（AskUserQuestion「CSVの所在」2026-07-29: 埋め込み方式を選択）により能動置換で確定済み。

機能パリティ（Phase 2.5 死活表）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| レガシーuGUIメインメニューの言語ドロップダウン | 生きる | `LanguageSetting.cs` は `Localize.SetLanguage`/`LanguageCodes` を呼び続け、両APIは維持 |
| uGUIツールチップ（UGuiTooltipTarget） | 生きる | `GetLegacy` 経路で辞書解決継続。Prefabキーは新キーへ更新済み |
| Web全画面の文言表示 | 生きる（改善） | 辞書ヒット3→430。japanese列に現原文を保持するため表示は不変 |
| Webの `?? key` 原文フォールバック | 廃止（意図的） | ユーザー裁定「欠落時挙動: 二層方式」— `[!key]` の目立つ表示へ |
| ミッションHUD（MissionBar・SetKey経路） | 生きる | `SetKey`→`GetLegacy`。キー空欄時は `[!]` 表示になるが現行も `[Localize] Key : is not found` 表示であり退化なし |
