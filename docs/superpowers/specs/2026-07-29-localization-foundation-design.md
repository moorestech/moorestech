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
- **skitは独立した既存構造を持つ**: 台詞はマスタJSONでなく `moorestech_client/Assets/AddressableResources/Skit/skits/*.json`（**Guidなし**・ファイル名が実質ID・各commandはint連番`id`・話者は`characterId`（例 `chr_001`）＋`overideCharacterName`上書き）。さらに `Skit/i18n/{japanese,english}.json` という**既存の別i18n辞書**（`master.characters.chr_001`形式キー）が実在するが、C#コードからの消費箇所はgrepで0件（commandForgeEditor由来の可能性）。
- 研究/チャレンジのGuidは実在: `research.yml` の `researchNodeGuid`＋`researchNodeName`/`researchNodeDescription`、`challenges.yml` の `challengeGuid`＋`title`/`summary`、カテゴリ `categoryGuid`＋`categoryName`/`categoryDescription`。

## 設計

### 二層のテキスト体系

| | バニラ文言（コード参照） | コンテンツ文言（マスタ由来） |
|---|---|---|
| キー | `ui.inventory.title` 形式の名前空間キー（型付き） | `<type>.<guid>.<field>` の導出キー（ベタ書き禁止） |
| 正本 | moorestech リポジトリ内 CSV（スキーマとは別の専用ディレクトリ、案: `Localization/localization.csv`） | `mods/<mod>/localization/localization.csv`（バニラCSVと完全同一フォーマット） |
| 参照方法 | C#: SourceGenerator生成の型付きキー / TS: 同一CSVから生成した定数 | Guid から実行時に動的構築 |
| 欠落時 | CI/テストでエラー化。実行時は `[!key]` | 対象言語 → english → master の name 原文 → `[!key]` |

- CSVフォーマットは現行踏襲: `key,Source,english,japanese,...`（Source列は作者向け原文でコードから読まない）。
- キーに modId は含めない（Guidがグローバル一意。翻訳modが他modの翻訳を提供可能）。

### 生成系

- mooresmaster DLL 内に**第2の `[Generator]` クラス**を追加（既存YAML generatorと同居、`.csv` の AdditionalFile を処理）。キー定数＋バニラ辞書本体をC#へ埋め込み、`config/localization.csv` の実行時読み込みは廃止。
- `Core.Master/csc.rsp`（または対象asmdefの新設csc.rsp）に `/additionalfile:` を追加。`SchemaWatcher` の監視対象に新ディレクトリを追加。
- webui はビルド/コード生成ステップで同一CSVからTS定数を生成。`t()` への生文字列リテラルを lint で禁止（既存 `no-jsx-visible-literal` に追加）。
- generator 変更時は `mooresmaster/build.sh` で client/server 両 DLL を再ビルドしコミット。

### 実行時合成と配信

- 合成辞書の正本はクライアント側 `Localize`（後継）。起動時にバニラ埋め込み辞書＋全mod CSVを単一辞書へ合成。サーバーは非関与。
- Webへの配信は既存 `/api/i18n/{locale}` + `localization.current` を維持。
- ホスト側 Name 解決・payload 同梱は**全廃**し、Web は Guid→導出キー→合成辞書で解決。言語切替はWeb側再描画のみで完結。

### 言語切替

- webui に言語選択UIを新設し、Web→ホストの set locale 経路を追加。`Localize.SetLanguage`→PlayerPrefs永続→`localization.current` push の既存往復に接続。
- 言語表示名は `localization_settings.csv` を `Localization/localization_settings.csv` へ移設・埋め込み化して活用（config/ 実行時読み込みは辞書と同様に廃止）。**言語セットの唯一の定義は辞書CSVのヘッダ列**とし、settings は表示名・Steam言語コードのメタに徹する。settings の行集合＝辞書ヘッダの列集合であることをCIで一致検査し、定義の二重化を禁止する。

### スコープ

初回対象: ①webui 430キーの名前空間キー一括移行、②item/block の name、③研究・チャレンジ等のマスタ文言、④skit台詞。
skitはGuidを持たないため導出キーは `skit.<skitファイル名>.<行id>.text` / `.speaker` 形式（Guid規約の例外。ファイル名が実質IDである既存構造に従う）。既存の `Skit/i18n/*.json` 辞書は新基盤へ吸収して**廃止**する方針（この機構が正規か仮置きかの判定は要ユーザー裁定 — 下記判断記録参照）。
対象外: レガシーuGUI文言（`KeyControlDescription` 等の日本語ベタ書き11箇所）。
付随修理: `modMeta.json` の id 空文字（required違反）、`Localize.cs` の未知ロケール例外経路、`TextMeshProLocalize` の try-catch 規約違反（基盤置換で自然消滅）。

## 判断記録（ADR）

- [ADR 0005 名前空間キー正準化とバニラCSV埋め込み](../../adr/0005-namespaced-localization-keys-embedded-vanilla-csv.md) — 出所: ユーザー裁定 2026-07-29（AskUserQuestion「正準キー空間」「Web型付け」「CSVの所在」）
- [ADR 0006 mod辞書・Guid導出キー・Web側解決](../../adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md) — 出所: ユーザー裁定 2026-07-29（AskUserQuestion「マスタ名方式」「辞書の正本」「名前解決場所」「mod辞書形式」「キー構造」「欠落時挙動」「言語切替UI」「初回スコープ」および「未翻訳フォールバックは英語→name原文」の直接指示）
- CSV置き場所をスキーマ外の専用ディレクトリとする — 出所: ユーザー裁定 2026-07-29「埋め込むが場所はスキーマ以外の場所にしたい（スキーマじゃないので）」
- skit台詞の導出キー拡張（行ID等）の詳細はplanで確定 — 出所: agent前提（skitが行単位構造で `<type>.<guid>.<field>` に乗らないという調査事実に基づく）
- 原文フォールバックは合成辞書の擬似ロケール `source` として実装（バニラはCSVのSource列、コンテンツはMasterHolderのname等原文から構築。解決チェーンは 対象言語→english→source→`[!key]` に統一され、Name同梱廃止と原文フォールバックが両立する）— 出所: agent前提（既存CSVのSource列と同概念の拡張）
- 言語表示名の埋め込み統合と言語セット定義の辞書CSVヘッダ一本化 — 出所: シミュレーター予測（SSOT観点・二重定義の分裂指摘）→適用済み。ユーザー承認待ち
- skit導出キーのファイル名＋行id規約（Guid例外）と既存 `Skit/i18n/*.json` の吸収廃止 — 出所: シミュレーター予測→ユーザー承認 2026-07-29「ok」（仮置きとして廃止・新基盤へ吸収で確定。Plan2 Task 9の裁定ゲート解除済み）
- 初期言語セットは english+japanese の2列のみ（言語セットはCSVヘッダで定義され列追加で拡張。29言語分の翻訳が存在しない状態で全列CI検査を課すのは不成立のため）— 出所: agent前提（欠落CI検査のユーザー裁定と翻訳実データ不在の両立）
