# ブランチ赤テスト2件の解消レポート

対象: `SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues`
（english: Expected 139 / But was 143、japanese: Expected 204 / But was 208）

## 1. 原因の確認結果

### baselineが数えているもの

テストは `Skit/i18n/{lang}.json` の `translations` のうち、**`command.` または `master.` で始まるキーのみ**を
`SortedDictionary` に集め、その件数と `locale`/`name`/ソート済みkey-valueのSHA256を固定している。
`skit.*`（台詞キー）は baseline の対象外。したがって指示にあった「skit台詞が増えた」は正確ではなく、
実際に増えたのは **CommandForgeのコマンド定義キー（`command.*`）** だった。

### 増分の出所

`git diff d97cd4d9d^1 d97cd4d9d` で確認。マージ `d97cd4d9d`（Merge origin/master into feature/localization-foundation）
が取り込んだ master 側コミット **`5049214e7` 「スキット中にmapObjectとエンティティも非表示にできるようにする」**（2026-07-31）が、
`commands.yaml` の `inGameObjectControl` に `mapObjectEnable` / `entityEnable` を追加し、
両言語辞書にそれぞれ **4キー追加＋1キー値変更** を行っていた。

追加された4キー（english/japanese 共通）:
- `command.inGameObjectControl.property.mapObjectEnable.name`
- `command.inGameObjectControl.property.mapObjectEnable.description`
- `command.inGameObjectControl.property.entityEnable.name`
- `command.inGameObjectControl.property.entityEnable.description`

値が変更された1キー:
- `command.inGameObjectControl.description`
  （en: "…background and block object visibility" → "…background, block, map object and entity visibility" /
   ja: 「ゲーム内の背景とブロックオブジェクトの表示を制御」→「ゲーム内の背景・ブロック・mapObject・エンティティの表示を制御」）

この値変更があるため、count だけでなく **hash も更新が必要**だった。

### 本計画の変更が原因でないことの確認

スクリプトで実測した baseline（count, hash）:

| 対象リビジョン | english | japanese |
|---|---|---|
| `7ac9a2dec`（テストコメントが正本と宣言する Task 8 直前） | 139 / `2d400074…` | 204 / `9fc582ef…` |
| `d97cd4d9d^1`（マージ直前のブランチ先端＝Task 6 適用後） | 139 / `2d400074…` | 204 / `9fc582ef…` |
| 現ワークツリー（マージ後） | **143 / `d2fe6232…`** | **208 / `aa082c02…`** |

`7ac9a2dec` と `d97cd4d9d^1` の baseline が count・hash とも完全一致している。
すなわち Task 6〜18 のブランチ側変更（Task 6 の日本語空文字3キー翻訳を含む＝いずれも `skit.*` キー）は
baseline に一切影響しておらず、ずれは 100% マージ由来と確定。

## 2. 増えた分が両言語で翻訳済みであることの確認結果

- 追加4キーの集合は english / japanese で**完全一致**（差分なし、片方だけ増えた状態ではない）。
- 4キーとも両言語で**実訳が入っており空文字なし**（en: "Map Object Enable" 等、ja: 「mapObject表示」等）。
- さらに辞書全体を走査し、`translations` の**空文字値は english(152キー) / japanese(217キー) とも 0 件**。
- Task 6 の契約テスト `AllTranslationValuesAreNonEmpty` も pass（下記テスト結果に含む）。

→ 本物の翻訳漏れは無く、baseline 更新が正しい対処と判断した。

## 3. 変更内容

`moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationDictionaryCompletenessTest.cs`

- `[TestCase("english", 139, "2d400074…")]` → `[TestCase("english", 143, "d2fe6232…")]`
- `[TestCase("japanese", 204, "9fc582ef…")]` → `[TestCase("japanese", 208, "aa082c02…")]`
- baseline の出典コメントを `7ac9a2dec` 基準から `5049214e7`（4キー追加）後の値へ更新

他ファイルの変更なし（辞書JSON・キー生成ロジックは一切触っていない）。

## 4. 検証結果

- `uloop compile --project-path ./moorestech_client` → `Success: true, ErrorCount: 0`
- `uloop run-tests --filter-type regex --filter-value ".*(Skit|Localiz).*"`
  → `TestCount: 122 / PassedCount: 122 / FailedCount: 0 / SkippedCount: 0`（赤 0 件）
