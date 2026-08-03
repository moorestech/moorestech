# Task 13 レポート: source疑似ロケールのsnapshot型分離（D8=案B・C12）

コミット: `b3f79ad0b refactor: source疑似ロケールをsnapshot型で実言語と分離し除外規則を構造化`
（HEAD前提 `8d501b22d`）

## 前任者が何をやっていたか

ブリーフのStep 1〜3（テスト追加・実装・ディレクトリ移動）は実質完了していた。未実施だったのはStep 4（検証）とStep 5（コミット）。

前任者の成果:

1. **snapshot型の分離** — `PublishedLocalizationDictionarySnapshot` を
   `Dictionaries`（言語＋source混在）から `Languages`（実言語のみ）＋ `SourceTexts` の2フィールドへ分割。
2. **合成途中の可変状態も同型で分離** — 新規 `LocalizationDictionaryCandidate`（`Languages` / `SourceTexts` のmutable版）。
3. **除外規則3箇所の構造的消滅**
   - `Localize.Initialize`: `savedLanguageCode != SourcePseudoLocale` を削除（`snapshot.Languages.ContainsKey` だけで判定）
   - `Localize.TrySetLanguage`: `languageCode == SourcePseudoLocale ||` を削除
   - `Localize.TryGetDictionary("source")` の特例が消滅（sourceが `Languages` に居ないため自然にfalse）
4. **HTTP境界** — `LocalizationDictionaryEndpoint` で `locale == Localize.SourcePseudoLocale` の**明示分岐**を追加し `Localize.TryGetSourceTexts(revision, out ...)` から配信。URLは `/api/i18n/{locale}` のまま。
5. **mod CSV検証は維持** — `ModLocalizationMerger.MergeCsv` の `languageCode == Localize.SourcePseudoLocale` → `Reserved localization language` 例外はそのまま。合流先だけ `candidate.SourceTexts` へ変更。コメントも「mod CSVは外部入力のため」と根拠を明記する形へ更新。
6. **TS側** — `i18nStore.ts` に `export const SOURCE_LOCALE = "source";` を `FALLBACK_LOCALE` の隣へ追加、`I18nProvider.tsx:75` の文字列リテラルを置換。専用エンドポイントは作っていない。
7. **ディレクトリ移動** — `ModLocalizationMerger` / `PublishedLocalizationDictionarySnapshot` / `VanillaLocalizationDictionaryFactory` を `Client.Localization/Dictionary/` へ `git mv`（.cs+.metaペア）。
8. **テスト7ファイル追従＋新規テスト4本**
   - `LocalizeTest.SourcePseudoLocaleIsNotReachableThroughLanguageDictionaries`
   - `LocalizeTest.TryGetSourceTextsReturnsSourceTextsForTheCurrentRevision`
   - `LocalizeTest.TryGetSourceTextsRejectsStaleRevision`
   - `LocalizationRevisionContractTest.DictionaryEndpointServesSourceTextsFromTheSameLocaleUrl`（URL不変の契約テスト）

## 私が追加/修正したこと

**コード修正はゼロ。** 全項目を独立点検した結果、ブリーフ・裁定・global-constraintsの要件をすべて満たしており、修正すべき欠陥・逸脱は見つからなかった。私が行ったのは点検・検証・コミット。点検の内訳は下の「自己レビューの所見」に記載。

## 検証結果

| コマンド | 結果 |
|---|---|
| `uloop compile --project-path ./moorestech_client` | `Success: true, ErrorCount: 0, WarningCount: 0` |
| `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value ".*(Localiz\|GameDictionary\|ModLocalization).*"` | 116件中 **114 passed / 2 failed**（失敗2件は既知のbranch-red・下記） |
| `npx tsc -b`（moorestech_web/webui） | exit 0 |
| `npm run lint` | eslint エラー0・警告0 |
| `npm test` | **543 passed / 82 files** |

失敗2件（XML: `moorestech_client/.uloop/outputs/TestResults/20260803_160557.xml`）:

```
SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues("english",139,...)  Expected: 139 / But was: 143
SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues("japanese",204,...) Expected: 204 / But was: 208
```

→ ブリーフに記載された**既知のbranch-red 2件と完全一致**（baseline 139/204 に対し実測 143/208）。origin/masterマージでskit台詞が4件増えたことが原因で、本タスクはskit辞書・当該テストのいずれにも触れていない。指示どおり未修正のまま残置。**本タスク起因の失敗は0件。**

## `LocalizationDictionaryCandidate.cs` を残した理由

**残した。** ブリーフのFiles節に無いが、案Bの必然的な帰結であり正当と判断した。

- 分離前は合成途中の可変状態が `Dictionary<string, Dictionary<string,string>>` 1本で、sourceはその1キーとして相乗りしていた。型分離すると「実言語辞書群」と「原文辞書」の**2つの可変コレクションを1セットで**
  `VanillaLocalizationDictionaryFactory.Create()` → `ModLocalizationMerger.Merge/MergeCsv` → `Localize.OverlayMasterSourceTexts` → `Freeze` の4段に受け渡す必要が生じる。
- 代替案は「全メソッドの引数を2本に増やす」だが、2つが常に同一世代で対でなければならないという不変条件を型で表現できず、片方だけ渡し忘れる事故を許す。**除外規則を型で消すのが目的のタスクで、別の暗黙の対応関係を実行時規約に落とすのは本末転倒。**
- 実際 `LocalizationDictionaryCandidate` は公開snapshot（`Languages`/`SourceTexts`）と**同じ形の可変版**であり、「freeze前＝mutable candidate / freeze後＝immutable published」というHEAD時点からの既存の対比（`Freeze(candidate)` という既存メソッド名がその前例）をそのまま型に昇格させただけ。前例整合。
- `internal sealed` ＋ public readonly フィールドで、`PublishedLocalizationDictionarySnapshot` と全く同じ様式。22行。

## 変更したファイル

移動（.cs+.metaペア・GUID維持を実測確認）:
- `moorestech_client/Assets/Scripts/Client.Localization/ModLocalizationMerger.cs(.meta)` → `.../Client.Localization/Dictionary/`
- `.../PublishedLocalizationDictionarySnapshot.cs(.meta)` → `.../Dictionary/`
- `.../VanillaLocalizationDictionaryFactory.cs(.meta)` → `.../Dictionary/`

新規:
- `moorestech_client/Assets/Scripts/Client.Localization/Dictionary/LocalizationDictionaryCandidate.cs(.meta)`
- `moorestech_client/Assets/Scripts/Client.Localization/Dictionary.meta`（Unity生成のフォルダmeta）

変更:
- `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`
- `moorestech_client/Assets/Scripts/Client.Localization/LocalizationTextResolver.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/LocalizationDictionaryEndpoint.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/Localization/{LocalizeTest,LocalizationTextResolverTest,GameDictionaryRecompositionTest,ModLocalizationMergerTest,ModLocalizationMergerValidationTest}.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/Localization/MasterSource/MasterSourceTextCollectorTest.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Localization/LocalizationRevisionContractTest.cs`
- `moorestech_web/webui/src/shared/i18n/i18nStore.ts`
- `moorestech_web/webui/src/shared/i18n/I18nProvider.tsx`

## 自己レビューの所見

### 点検して合格だったもの

1. **除外規則の構造的消滅** — `grep -rn "SourcePseudoLocale" --include=*.cs` の全ヒットを目視。プロダクションコードに残る条件分岐は**2箇所のみ**で、どちらもブリーフが例外として許可したもの:
   - `Dictionary/ModLocalizationMerger.cs:72`（mod CSVの予約列名検証＝外部入力バリデーション）
   - `Client.WebUiHost/Game/LocalizationDictionaryEndpoint.cs:51`（HTTP境界の明示分岐＝`.decisions/2026-08-02-source-locale-wire-and-skit-language-contract.md` で承認済み）
   残りは全てテスト内の `[TestCase(Localize.SourcePseudoLocale)]` 等の契約表明で、実装側の除外条件ではない。
2. **配信URL不変** — `PathPrefix = "/api/i18n/"` は無変更、`/api/i18n-source` 等の新設なし。TS側の `fetchDictionary(SOURCE_LOCALE, ...)` は同じ `/api/i18n/source` を叩く。新規契約テスト `DictionaryEndpointServesSourceTextsFromTheSameLocaleUrl` が 200＋`ui.mainMenu.playLocally` を実際に検証しており、pass。
3. **.metaのGUID維持** — 移動3ファイルのGUIDを `git show HEAD:...` と実ファイルで突合し全一致（`1acea27b...` / `b3ef741e...` / `57c0d009...`）。`Dictionary.meta` はUnity生成物（`folderAsset: yes`）で手書きの痕跡なし。
4. **ディレクトリ10ファイル制限** — `Client.Localization/` 直下は .cs 6本（+asmdef+csc.rsp）、`Dictionary/` は .cs 4本。いずれも10以下。移動により直下の張り付き（type-driven W指摘）が解消。
5. **1ファイル200行制限** — 最大は `Localize.cs` 198行。次点 `Dictionary/ModLocalizationMerger.cs` 99行。
6. **参照の網羅** — `ModLocalizationMerger` / `VanillaLocalizationDictionaryFactory` / `PublishedLocalizationDictionarySnapshot` / `.Dictionaries` の全参照をclient+server横断でgrep。取りこぼしゼロ（compile Error 0が裏付け）。
7. **`GetLanguageCodes()` の消費側** — `Client.MainMenu/LanguageSetting.cs` のみ。source除外の実行時フィルタは無く、`VanillaLocalizationTable.LanguageCodes` が元からsourceを含まない。ここに新たな除外は不要。

### 意図的な逸脱（正当と判断・報告事項）

- **ブリーフの `GetSourceTexts()` ではなく `TryGetSourceTexts(long expectedRevision, out ...)` になっている。**
  これは前任者の判断だが**より正しい**と判断して残した。エンドポイントは「revisionと辞書を同一snapshotから読む」ことで異世代混在を防ぎ 409/404 を出し分ける必要があり（既存 `TryGetDictionary(locale, revision, out)` と同じ契約）、単純ゲッタではその世代保証が失われる。`TryGetSourceTextsRejectsStaleRevision` テストがこの契約を固定している。またrevisionなしの `GetSourceTexts()` を別に生やすとテスト専用の準デッドAPIになるため、生やしていないのも妥当。
- **`ModLocalizationMerger` を `public` → `internal` に降格**（ブリーフ外の変更）。参照はassembly内 + `Client.Tests`（`AssemblyInfo.cs` に `InternalsVisibleTo("Client.Tests")` あり）のみで、公開面を絞る方向の変更なので残した。
- **`Dictionary/` サブディレクトリを作ったが namespace は `Client.Localization` のまま**（フォルダ非ミラー）。理由2点: (a) 本リポジトリはフォルダ⇔namespaceを厳密ミラーしていない（例: `Client.Game/InGame/Block/*.cs` の namespace は `Client.Game`）ので前例整合。(b) namespace を `Client.Localization.Dictionary` にすると同ファイル群で多用する `Dictionary<,>` の名前解決に不要な曖昧性を持ち込む。

### 軽微な残置（今回は触らなかった）

- `Dictionary/ModLocalizationMerger.cs:97` にクラス閉じ括弧直前の余分な空行。**HEAD時点から存在する既存の痕跡**で本タスクの変更範囲外のため未修正（plan Task 18 のcosmetic系で拾える）。

## 問題や懸念事項

### 既知のbranch-red（本タスク起因ではない・未修正のまま残置）

- `SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues("english", 139, ...)` — Expected 139 / But was 143
- 同 `("japanese", 204, ...)` — Expected 204 / But was 208

origin/masterマージでskit台詞が増えたためのbaseline乖離。ブリーフの指示どおり触っていない。

### 本タスク起因の懸念

なし。compile Error 0 / 対象テスト全pass（上記2件を除く）/ webui tsc・lint・test 全green。

### コミット外の残置ファイル（引き継ぎ事項）

以下はワークツリーに未コミットで残っている（本コミットには**含めていない**）。いずれもTask 13の成果物ではない:

- `M .superpowers/sdd/task-10-report.md` — Task 10（connectTool表示名のWeb解決統一）のレポート全面改稿。前タスクのセッションで書かれてコミットされずに残ったものと思われる。**Task 13のリファクタコミットに混ぜるのは不適当と判断し除外した。** 別途 docs コミットで拾う必要がある。
- `?? .decisions/2026-08-02-source-locale-wire-and-skit-language-contract.md`
- `?? .decisions/2026-08-02-skit-language-set-contract-test.md`
- `?? docs/superpowers/plans/2026-08-02-localization-review-remediation.md`
