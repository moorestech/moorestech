# Task 16 レポート: region-internal残り4ファイル（C5のSchemaWatch+CSVパーサ分）

コミット: `e089d9e78 refactor: 単一呼び出し元ヘルパーをregion Internalローカル関数へ集約（SchemaWatch/CSVパーサ）`

## 何を実装したか（4ファイル各々の変換内容）

### 1. `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvParser.cs`
- `private static string ConvertEscapedNewlines(string text)` を `Parse` 末尾の `#region Internal` ローカル関数へ移動。
- 引数 `text` は保持。`Parse` 内の2つの呼び出しはループ内スコープの値（`fields[1]` / `fields[languageIndex + LanguageStartColumn]`）を渡すため、メソッド末尾で宣言されるローカル関数からはクロージャで拾えない。
- **追加の構造変更（ブリーフ外・理由は下記）**: `ParseRecords` を新ファイル `LocalizationCsvRecordReader.cs`（`public static class LocalizationCsvRecordReader`）へ分離。呼び出し側5箇所を `LocalizationCsvRecordReader.ParseRecords(...)` へ更新（`LocalizationCsvParser.Parse` / `ContentKeyCatalogParser` / `LocalizationSettingsParser` / テスト3箇所）。メソッド名 `ParseRecords` は変えていないため既存テストのメソッド名（`ParseRecordsは…`）も有効。`LocalizationCsvParser.cs` は `using System.Text;` が不要になったので削除。

  **分離した理由**: 元ファイルはちょうど **200行**（上限ぴったり）で、region化により **204行** になり `1ファイル200行以下` の必須規約を破る。moores-code-review の決定論チェック `checks_static._file_length_rule` は `MAX_FILE_LINES(200) < count` で `file-too-long` を確定検出するため、Task 19 で必ず差し戻される。TS側は既に同じ理由で `scripts/localizationCsvRecords.mjs` に `parseRecords` を分離済み（Task 8）であり、**前例一致**の分割。結果 76行 / 135行 で両方とも規約内。ロジックは1行も変えていない（`sed` で該当範囲をそのまま移設）。

### 2. `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchCache.cs`
- `Load()` → コンストラクタ末尾の `#region Internal` ローカル関数へ。
- `LoadVersionTwoLine(string line)` → `Load` 内のネストした `#region Internal` へ（引数保持: for ループ内の `lines[index]` を渡すため）。
- `Escape(string value)` → `Save` 末尾の `#region Internal` ローカル関数へ（引数保持: ループ内の3種の値を渡すため）。
- **ブリーフ外の1件**: `EnsureTargetHashes(string watchPath)` も `Load` のネスト `#region Internal` へ移動した。**理由**: 変換前は `Load` と `LoadVersionTwoLine` の2メソッドから呼ばれていたためC5の「単一呼び出し元」に該当しなかったが、`LoadVersionTwoLine` を `Load` 内へ移した結果、呼び出し元メソッドがコンストラクタ1つだけになる。放置すると **本タスクが新たなC5違反を作る**ことになるため同時に移動した。`hashesByWatchPath` はフィールドなのでローカル関数からそのまま参照できる。
- CS0136（内側スコープの名前衝突）回避のため、`Load` 内 legacy 移行ループの変数を `line`/`parts` → `legacyLine`/`legacyParts` へリネーム（`LoadVersionTwoLine` の引数 `line` が囲みスコープと衝突するため）。挙動には影響しない。

### 3. `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchOrchestrator.cs`
- `UpdateRequesterScript` → `CheckForChanges` 末尾の `#region Internal` ローカル関数へ。引数は保持（呼び出しは `foreach` ボディ内で宣言される `target` / `out var currentHashes` を渡すため、メソッド末尾のローカル関数からはクロージャで拾えない）。ただし CS0136 回避のため仮引数名を `watchTarget` / `watchHashes` へリネーム。
- `ComputeRequesterToken` → `UpdateRequesterScript` 内のネスト `#region Internal` へ。**引数 `currentHashes` は削除**（外側 `UpdateRequesterScript` の仮引数 `watchHashes` をクロージャで拾えるため）。呼び出し側は `ComputeRequesterToken()` へ同時更新。

### 4. `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchTarget.cs`
- `ComputeHash(string filePath)` → `TryReadCurrentHashes` 末尾の `#region Internal` ローカル関数へ。引数保持（`foreach` 内の `file` を渡すため）。`out` 引数 `currentHashes` はローカル関数から参照していないので capture 制約に抵触しない。

ネストした `#region Internal` の書式は Task 8 で確立した前例（`mooresmaster.Generator/Localization/LocalizationCodeGenerator.cs:55,108`）に合わせた。

## 各ヘルパーの呼び出し元が1つだけだった根拠（grep）

`grep -rn "…" --include='*.cs' .`（Library/ 除外）の結果:

| ヘルパー | 宣言 | 呼び出し | 呼び出し元メソッド |
|---|---|---|---|
| `ConvertEscapedNewlines` | LocalizationCsvParser.cs:195 | 同55, 同59 | `Parse` のみ（他ファイル0件） |
| `UpdateRequesterScript` | SchemaWatchOrchestrator.cs:62 | 同48 | `CheckForChanges` のみ |
| `ComputeRequesterToken` | SchemaWatchOrchestrator.cs:95 | 同75 | `UpdateRequesterScript` のみ |
| `LoadVersionTwoLine` | SchemaWatchCache.cs:104 | 同86 | `Load` のみ |
| `ComputeHash`（Target） | SchemaWatchTarget.cs:55 | 同49 | `TryReadCurrentHashes` のみ（`md5.ComputeHash` 等の同名メソッド呼び出しは別物として除外済み） |
| `EnsureTargetHashes` | SchemaWatchCache.cs:121 | 同99, 109, 117 | 変換前は `Load` + `LoadVersionTwoLine` の2メソッド → 変換後はコンストラクタ1つ |

`ParseRecords` は `LocalizationCsvParser.Parse` / `ContentKeyCatalogParser` / `LocalizationSettingsParser` / テスト3箇所の計6箇所から呼ばれる複数呼び出し元のため、ローカル関数化はせず **別クラスへの分離**とした（C5の対象ではない）。

## 検証結果

| コマンド | 結果 |
|---|---|
| `cd mooresmaster && dotnet test` | `失敗: 0、合格: 290`（分離前後の2回とも全緑） |
| `cd mooresmaster && ./build.sh` | `0 エラー` × 2プロジェクト、`Done! mooresmaster DLLs have been deployed.` |
| `uloop compile --project-path ./moorestech_client` | `"ErrorCount": 0`（DLL差し替え後のフル再コンパイル、Warning 499は既存分） |
| `uloop run-tests … --filter-value ".*(SchemaWatch\|Localization).*"` | 119件中 117 pass / 2 fail（fail は既知branch-red 2件のみ・下記） |
| `deterministic_checks.py`（本タスク差分に対して） | `confirmed: []`、`region_internal: []`（＝`#endregion` 下のコード無し・クラス直下 region 無しを機械判定で確認）、`file-too-long` 検出なし |

SchemaWatch のテストは `SchemaWatcherPersistenceTest`（4件・全pass）で、移設した全経路を踏んでいる:
- `Escape`/`LoadVersionTwoLine`/`EnsureTargetHashes`: 「cache保存読込は空targetとescaped文字を欠落させない」（`V|2` 保存→再読込、`|`/`%`/CRLF を含むパス）
- `ComputeHash`: 「ファイル追加変更削除を永続cacheとの差分として検出する」
- `UpdateRequesterScript`/`ComputeRequesterToken`: 「複数targetは変更対象のrequesterだけ更新しcompileを一度要求する」「requester更新後も日英説明とcommit意図を保持する」

CSVパーサ側は `mooresmaster.Tests`（`ParseRecords` 3件を含む290件）が担保。さらに Unity 側 Localization テストのキー数（english 143 / japanese 208）が変更前の実測と完全一致しており、generator の生成結果が不変であることの裏付けになっている。

## 変更したファイル

- `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvParser.cs`
- `mooresmaster/mooresmaster.LocalizationCsv/LocalizationCsvRecordReader.cs`（新規）
- `mooresmaster/mooresmaster.Generator/Localization/ContentKeyCatalogParser.cs`（呼び出し側追従1行）
- `mooresmaster/mooresmaster.Generator/Localization/LocalizationSettingsParser.cs`（同1行）
- `mooresmaster/mooresmaster.Tests/LocalizationTests/LocalizationCsvParserTest.cs`（呼び出し側追従3行）
- `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchCache.cs`
- `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchOrchestrator.cs`
- `moorestech_server/Assets/Scripts/Editor/SchemaWatch/SchemaWatchTarget.cs`
- DLL再ビルド分（`./build.sh` 実行済み・コミット同梱）:
  - `moorestech_client/Assets/Plugins/mooresmaster.Generator.dll`
  - `moorestech_client/Assets/Plugins/mooresmaster.LocalizationCsv.dll`
  - `moorestech_server/Assets/Plugins/mooresmaster.Generator.dll`
  - `moorestech_server/Assets/Plugins/mooresmaster.LocalizationCsv.dll`

`.meta` は不要（`mooresmaster/` は Unity Assets 配下ではない）。`.superpowers/sdd/task-10-report.md` の未コミット変更と未追跡の `.decisions/*`・`docs/superpowers/plans/*` は他タスク由来のため **add していない**（`git add -A` 不使用・パス明示）。

## 自己レビューの所見

- **呼び出し元の単一性**: 上表のとおり全ヘルパーを grep で全リポジトリ横断確認済み。唯一 `EnsureTargetHashes` だけが2メソッドから呼ばれていたが、これは本タスクの移動によって単一化する副作用があり、放置すると新規違反になるため同時に移した（判断根拠を上に明記）。
- **`#endregion` 下のコード**: 4ファイルすべてで `#endregion` の直後はメソッド／クラスの閉じ括弧のみ。`checks_region.py` を含む決定論チェックでも `region_internal: []`。
- **挙動不変**: `git show -w` で差分を読み、移設テキストは1トークンも変えていないことを確認。変更したのは (a) 名前衝突回避のリネーム3件（`legacyLine`/`legacyParts`/`watchTarget`/`watchHashes`）、(b) `ComputeRequesterToken` の引数削除（同じ辞書をクロージャで参照）、(c) `using System.Text;` の削除（未使用化）のみ。いずれも意味論は同一で、テストで担保済み。
- **DLL**: `LocalizationCsvParser.cs` は generator が参照する共通DLLのため `./build.sh` を実行し、client/server 両方の2DLL（計4ファイル）をコミットに含めた（ADR 0005帰結）。Unity 側フル再コンパイルが Error 0 で通っており、新DLLが実際にロードされている。
- **行数規約**: 変更後 `LocalizationCsvParser.cs` 76行 / `LocalizationCsvRecordReader.cs` 135行 / `SchemaWatchCache.cs` 148行 / `SchemaWatchOrchestrator.cs` 118行 / `SchemaWatchTarget.cs` 66行。1ディレクトリ10ファイル規約も `mooresmaster.LocalizationCsv/`（.cs 4件）で問題なし。

## 問題や懸念事項

1. **【既知のbranch-red・本タスク起因ではない】** `SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues` の2ケースが失敗:
   - `("english",139,…)`: Expected 139 / But was 143
   - `("japanese",204,…)`: Expected 204 / But was 208

   origin/master マージ由来の baseline 未更新で、指示どおり**触っていない**。変更前に実行した同フィルタでも同じ2件・同じ数値で失敗しており、本タスクの前後で1件も増減していない。

2. **ブリーフ外の変更2件**（上に理由を明記）: `ParseRecords` のファイル分離（200行規約の必須充足）と `EnsureTargetHashes` のローカル関数化（本タスクが作る新規C5違反の回避）。どちらも挙動不変で、レビューで差し戻される確定違反を潰すための最小手当てだが、**ブリーフの文面には無い**ので裁定が必要ならご指摘ください。

3. **コメント長のcandidate 6件**: 決定論チェックが `comment_length`（日本語20字目安超）を6件候補として出したが、いずれも**移設したブランチ既存コメントの原文そのまま**（SchemaWatchCache 2件・LocalizationCsvRecordReader 4件）で、本タスクで新規に書いたものではない。純粋な構造変換の原則に従い書き換えていない。Task 18（コメント文字数の機械的短縮）の対象に含めるか要判断。
