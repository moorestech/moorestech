# ローカライズ基盤設計 (2026-07-29)

ゲーム全体（Unityクライアント・Web UI・mod）のローカライズ基盤。
設計の裁定は grilling セッション（2026-07-29）で確定済み。ADR: [0005](../../adr/0005-namespaced-localization-keys-embedded-vanilla-csv.md) / [0006](../../adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md)。用語は `CONTEXT.md` の「ローカライズ」節を正とする。

## 現状（調査確定事項）

- 配信パイプは完成済み: `LocalizationDictionaryEndpoint`（`GET /api/i18n/{locale}`）+ `LocalizationTopic`（`localization.current`）+ webui `I18nProvider`/`i18nStore`。
- 中身が分裂: webui は430キーを**日本語原文キー**で `t()` に渡し、辞書（`config/localization.csv`・54キー・v4から凍結）には3キーしかヒットしない。言語切替しても画面は変わらない。
- マスタの `name` は単一日本語文字列でロケール軸なし。ホスト側が `master.Name` を解決して payload 同梱（`ItemMasterEndpoint` / `BlockInventoryTopic` / `MachineRecipesTopic` / `BuildMenuEntryDtoFactory`）。
- `Localize.Get` の Unity 側消費は実質4箇所（uGUIは残置方針のため今後増えない）。
- mod合成は未実装（`MasterHolder.GetJson` が mod[0] 固定）。出荷modの `modMeta.json` の id は空文字。
- マスタJSONはネットワークを渡らず、クライアントは同一プロセス/ディレクトリの `MasterHolder` を直接参照する。
- **skitは独立した既存構造を持つ**: 台詞はマスタJSONでなく `moorestech_client/Assets/AddressableResources/Skit/skits/*.json`（Guidなし・ファイル名が実質ID・各commandはint連番`id`・話者は`characterId`）。`Skit/i18n/{japanese,english}.json` はCommandForgeEditorが `<projectPath>/i18n/*.json` から動的ロードする正式なプロジェクト辞書で、Addressableアドレス `Vanilla/Skit/i18n/{language}` も登録済み。ゲームruntimeからの消費だけが未実装。
- characters masterには操作ID `characterId` しかなく、表示名キー用の安定Guidがない。buildMenuカテゴリ/サブカテゴリも名前しか持たないため、いずれも必須Guid追加と全JSON一括更新が必要。
- 研究/チャレンジのGuidは実在: `research.yml` の `researchNodeGuid`＋`researchNodeName`/`researchNodeDescription`、`challenges.yml` の `challengeGuid`＋`title`/`summary`、カテゴリ `categoryGuid`＋`categoryName`/`categoryDescription`。

## 設計

### 二層のテキスト体系

| | バニラ文言（コード参照） | コンテンツ文言（マスタ由来） |
|---|---|---|
| キー | `ui.inventory.title` 形式の名前空間キー（型付き） | `<type>.<guid>.<field>` の導出キー（ベタ書き禁止） |
| 正本 | moorestech リポジトリ内 CSV（スキーマとは別の専用ディレクトリ、案: `Localization/localization.csv`） | `mods/<mod>/localization/localization.csv`（バニラCSVと完全同一フォーマット） |
| 参照方法 | C#: SourceGenerator生成の型付きキー / TS: 同一CSVから生成した定数 | Guid から実行時に動的構築 |
| 欠落時 | CI/テストでエラー化。実行時は `[!key]` | 対象言語 → english → master の name 原文 → `[!key]` |

- CSVフォーマットは現行踏襲: `key,Source,english,japanese,...`。Source列は作者向け原文であると同時にruntimeの最終原文fallbackとして `source` 擬似ロケールへ埋め込み/合成する。
- 空文字の翻訳は欠落として扱い、runtime辞書へ登録せず次のfallback段へ進む。parserはCI欠落検査のため空fieldを保持し、Source/翻訳列のliteral `\n` を実改行へ同じように正規化する。
- キーに modId は含めない（Guidがグローバル一意。翻訳modが他modの翻訳を提供可能）。

### 生成系

- mooresmaster DLL の単一 `[Generator]` である `MooresmasterSourceGenerator` から `LocalizationSourceEmitter` を呼び、`.csv` AdditionalFileを処理する。独立generatorは共通CSV DLLのanalyzer依存解決がCSVを持たない全assemblyでも先に走ってコンパイルを壊したため統合した。キー定数＋バニラ辞書本体をC#へ埋め込み、`config/localization.csv` の実行時読み込みは廃止。
- CSVパーサー・行モデル・例外は runtime 参照可能な独立共通DLLへ置き、generatorとUnity runtimeの双方が同じ実装を参照する。generator/runtimeへの実装コピーは禁止し、共通DLLのテスト・ビルド・client/server両方へのデプロイを同一手順に含める。
- `Core.Master/csc.rsp`（または対象asmdefの新設csc.rsp）に `/additionalfile:` を追加。`SchemaWatcher` の監視対象に新ディレクトリを追加。
- webui はビルド/コード生成ステップで同一CSVからTS定数を生成。`t()` への生文字列リテラルを lint で禁止（既存 `no-jsx-visible-literal` に追加）。
- generator 変更時は `mooresmaster/build.sh` で client/server 両 DLL を再ビルドしコミット。

### 実行時合成と配信

- 合成辞書の正本はクライアント側 `Localize`（後継）。起動時にバニラ埋め込み辞書＋全mod CSVを単一辞書へ合成。サーバーは非関与。
- Webへの配信は既存 `/api/i18n/{locale}` + `localization.current` を維持。
- ホスト側 Name 解決・payload 同梱は**全廃**し、Web は Guid→導出キー→合成辞書で解決。言語切替はWeb側再描画のみで完結。
- バニラ `GetLegacy` も対象言語→english→source→`[!key]` の順で解決し、sourceを省略しない。

### 言語切替

- webui に言語選択UIを新設し、Web→ホストの set locale 経路を追加。`Localize.SetLanguage`→PlayerPrefs永続→`localization.current` push の既存往復に接続。
- 言語表示名は `localization_settings.csv` を `Localization/localization_settings.csv` へ移設・埋め込み化して活用（config/ 実行時読み込みは辞書と同様に廃止）。**言語セットの唯一の定義は辞書CSVのヘッダ列**とし、settings は表示名・Steam言語コードのメタに徹する。settings の行集合＝辞書ヘッダの列集合であることをCIで一致検査し、定義の二重化を禁止する。

### スコープ

初回対象: ①webui 430キーの名前空間キー一括移行、②item/block の name、③研究・チャレンジ等のマスタ文言、④skit台詞。
skitはGuidを持たないため、Skit titleの唯一の正本をAddressable assetのbasename（runtimeで得る `TextAsset.name`）とし、commandの`CommandId`と組み合わせて `skit.<skitTitle>.<commandId>.<field>` を導出する。実測では `100_start_game` / `200_star_background` / `sample_short` のasset basenameとJSON `meta.title` は一致するが、`meta.title` はキー導出に使わず一致検査だけを行う。runtimeと完全性テストは同じ純粋な `SkitTitle.FromAssetName` を通す。fieldはCommandForge command schemaの正確なプロパティ名で固定し、`text.body` / `backgroundSkitText.body`、`selection.Option1Tag`〜`Option3Tag`、`text.overrideCharacterName` / `backgroundSkitText.overrideCharacterName` を対象にする。既存JSONの `overideCharacterName` はschemaの `overrideCharacterName` へ一括正規化する。

既存 `Skit/i18n/{english,japanese}.json` は削除せず、CommandForgeEditor用 `command.*` / `master.*` キーを維持したまま `skit.*` を追加できる正本へ拡張する。ゲームはskit開始時に選択言語とenglishの2ファイルだけをAddressablesから動的ロードし、`skit.` 接頭辞かつ非空の翻訳だけを取り込む。mod合成済み辞書へSkit専用辞書を欠けているキーだけ追加し、全段で空文字を欠落として扱うため、解決順は `mod対象言語 → skit専用対象言語 → mod英語 → skit専用英語 → skit JSON原文` になる。全skit JSONの事前ロードはしない。

`Client.Skit` の汎用層は `Localize` / Addressablesを直接参照せず、`ISkitLocalizationResolver` とskit title/commandIdを保持する実行contextだけを持つ。`Client.Game`側の具体loader/resolverを `SkitManager` / `BackgroundSkitManager` がStoryContextへ登録する。character masterには必須 `characterGuid` を追加して全characters JSONを一括更新し、`characterId` は操作IDのまま維持して表示名キーだけGuidを使う。buildMenuカテゴリ・サブカテゴリも必須Guid化し、optionalや欠損補完は置かない。

言語切替中に既に表示済みの同一行を即時再描画することは非目標とし、次に表示する行と次回skit開始から新言語を反映する。設定UIがskit中も操作できる場合は、辞書reload完了後の現在行再pushが必要かを後続QAでバグ狩りとして判定する。
対象外: レガシーuGUI文言（`KeyControlDescription` 等の日本語ベタ書き11箇所）。
付随修理: `modMeta.json` の id 空文字（required違反）、`Localize.cs` の未知ロケール例外経路、`TextMeshProLocalize` の try-catch 規約違反（基盤置換で自然消滅）。

## 配置と前例

| 項目 | 配置先 | 依存方向・前例 |
|---|---|---|
| CSV parser・行モデル・例外 | `mooresmaster.LocalizationCsv` 共通DLL | generator/runtime双方が参照する純粋な下流ライブラリ。実装コピーは禁止。Unity自身がruntime plugin metaを生成 |
| SourceGenerator orchestration | `mooresmaster.Generator` | 単一 `MooresmasterSourceGenerator` → `LocalizationSourceEmitter`。CSVを持つassemblyだけが共通CSV DLLを使う |
| mod辞書合成・Guidキー | `Client.Localization` | 合成辞書の既存正本 `Localize` の責務内。MasterHolderは生データ保持だけで変更しない |
| Skit resolver interface/context | `Client.Skit` | 汎用StoryContext serviceだけを定義し、`Localize` / Addressables / MasterHolderを持ち込まない |
| Skit loader/resolver具体実装 | `Client.Game/Skit/Localization` | `SkitManager` / `BackgroundSkitManager` のVContainer登録点から下流interfaceへ注入 |
| character/buildMenuのGuid | `VanillaSchema/*.yml` + 全master JSON | 既存 `researchNodeGuid` と同じ必須uuid。optional・default・ローダー補完は禁止 |

データフローは `バニラCSV → generator → 埋め込み辞書` と `mod CSV → 共通parser → Localize合成辞書` に一本化する。Skitだけは `選択言語+englishのSkit/i18n → Client.Game resolverの実行scope → Client.Skit表示` とし、ゲーム全体辞書へは `skit.*` 以外を流さない。

## 判断記録（ADR）

- [ADR 0005 名前空間キー正準化とバニラCSV埋め込み](../../adr/0005-namespaced-localization-keys-embedded-vanilla-csv.md) — 出所: ユーザー裁定 2026-07-29（AskUserQuestion「正準キー空間」「Web型付け」「CSVの所在」）
- [ADR 0006 mod辞書・Guid導出キー・Web側解決](../../adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md) — 出所: ユーザー裁定 2026-07-29（AskUserQuestion「マスタ名方式」「辞書の正本」「名前解決場所」「mod辞書形式」「キー構造」「欠落時挙動」「言語切替UI」「初回スコープ」および「未翻訳フォールバックは英語→name原文」の直接指示）
- CSV置き場所をスキーマ外の専用ディレクトリとする — 出所: ユーザー裁定 2026-07-29「埋め込むが場所はスキーマ以外の場所にしたい（スキーマじゃないので）」
- skitは既存CommandForgeEditor辞書を保持し、選択言語+englishだけを開始時動的ロードして `skit.*` のみゲーム辞書へ欠けたキーとして合成する — 出所: ユーザー裁定 2026-07-29
- skit fieldはCommandForge schemaのプロパティ名へ固定し、本文・背景本文・選択肢・上書き話者名を同一commandId由来キーで扱う — 出所: ユーザー裁定 2026-07-29
- character masterの必須characterGuid追加（characterIdは操作IDとして維持）と全characters JSON一括更新 — 出所: ユーザー裁定 2026-07-29
- buildMenuカテゴリ/サブカテゴリの必須Guid追加と全JSON一括更新 — 出所: ユーザー裁定 2026-07-29
- CSV parserをruntime参照可能な共通DLLへ置き、generator/runtime双方から参照してclient/serverへデプロイする — 出所: ユーザー裁定 2026-07-29
- 空翻訳を欠落として次段へfallbackし、Source列のliteral `\n` も実改行へ変換する — 出所: Task 0 review finding 2026-07-29
- Skit titleの正本をAddressable asset basename / `TextAsset.name` に一本化し、JSON `meta.title` は一致検査だけに使う — 出所: Task 0 review finding 2026-07-29
- 原文フォールバックは合成辞書の擬似ロケール `source` として実装（バニラはCSVのSource列、コンテンツはMasterHolderのname等原文から構築。解決チェーンは 対象言語→english→source→`[!key]` に統一され、Name同梱廃止と原文フォールバックが両立する）— 出所: agent前提（既存CSVのSource列と同概念の拡張）
- 言語表示名の埋め込み統合と言語セット定義の辞書CSVヘッダ一本化 — 出所: シミュレーター予測→ユーザー承認 2026-07-29
- 初期言語セットは english+japanese の2列のみ（言語セットはCSVヘッダで定義され列追加で拡張。29言語分の翻訳が存在しない状態で全列CI検査を課すのは不成立のため）— 出所: agent前提（欠落CI検査のユーザー裁定と翻訳実データ不在の両立）
