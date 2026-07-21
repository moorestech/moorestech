# 建設メニューのカテゴリ化（Satisfactory風）設計

日付: 2026-07-21（Fable subagent 4観点レビュー反映済み: 設計原則 / webui様式 / データ契約 / 曖昧性）
対象ブランチ: feature/machine-ui-tabs から分岐（または継続）

## 背景・目的

現在のWeb UI建設メニュー（`moorestech_web/webui/src/features/buildMenu/`）は全エントリをフラットな10列グリッドで並べるだけで、73ブロック+車両+ツール+ブループリントが無分類で混在している。これをSatisfactoryのビルドメニュー同様の「左サイドバー大カテゴリ + サブカテゴリ見出し付きグリッド + 検索 + ホバー詳細」へ再設計する。

**重要な前提: 現行buildMenuはwebui-design様式以前のレガシー実装である。** GamePanel不使用の独自面（`rgb(30 30 30 / 92%)`ハードコード）、`inset: 8% 12%`の%指定、素div+px直書きのスロット、Mantine CloseButton/Tooltip剥き出し。本タスクは**カテゴリ化と同時に様式準拠への移行を含む**（違反実装の上にカテゴリUIを載せない）。

## 決定事項（ユーザー承認済み）

- カテゴリ階層は**2段**（サイドバー大カテゴリ + グリッド内サブカテゴリ見出し）
- **検索ボックスを実装する**（全カテゴリ横断・名前部分一致）
- ホバー詳細は**パネル内固定プレビュー欄**（機械レシピ選択タブ様式の前例一致。カーソル追従カードは作らない）
- カテゴリ値はマスタデータが正本。`category` は**必須化**し全ブロックへ一括付与（optional+未分類フォールバック禁止 / PR978原則）
- カテゴリ・サブカテゴリの**表示順は配信配列の初出順から導出**（順序定義用の新マスタ・契約フィールドは作らない。配列順の正は既存カタログのsortPriority昇順）
- 非ブロックエントリは**エントリ種別から固定カテゴリを導出**（マスタ拡張しない）

## 1. マスタデータ設計

### 1.1 スキーマ変更（VanillaSchema/blocks.yml）

- 既存 `category`（optional string, blocks.yml:186付近）の `optional: true` を削除して**required化**
- `subCategory`（required string）を追加

両方とも自由文字列。カテゴリの一覧・順序・表示名はデータから導出するため、カテゴリ定義用の別マスタ・enumは作らない。付与漏れはmooresmasterのロードで必須違反として大声で失敗する。

**注**: `sortPriority` は現行 optional + `?? 0` のままとし、今回はrequired化しない（既存債務として別課題。触ると全modのsortPriority付与が必要になり本タスクと無関係な波及が大きい）。

### 1.2 required化の更新対象JSON（すべて更新すること）

| 対象 | ブロック数 | 付与方針 |
|---|---|---|
| `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json` | 73 | §1.3の分類表 |
| `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` | 59 | 機械的に `category: "テスト"` / `subCategory: blockType名`（テストはカテゴリUIを検証しないため意味分類不要） |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/.../master/blocks.json` | 29 | 同上 |
| `mooresmaster/mooresmaster.SandBox/` 配下のblocks.json 2件 | - | 同上 |

旧バージョンmod（`../moorestech_master/` の server(v3)〜server_v7）は現行コードから参照されないため**更新しない**（旧コミット用データを新スキーマに合わせるのは逆に不整合）。

あわせてrequired追加は生成クラス `BlockMasterElement` のコンストラクタ必須引数を増やすため、以下の追従が必要:

- 手書き `new BlockMasterElement(...)` の3ファイル（`Client.Tests/PlaceSystem/PlaceSystemUtilCalcPlacePointTest.cs`、`BeltConveyorPlacePointCalculatorTest.cs`、`CommonBlockPlacePointCalculatorTest.cs`）へ引数追加
- SourceGeneratorトリガ用 `_CompileRequester.cs` のdummyText更新（edit-schemaスキル手順）

### 1.3 全73ブロックの分類（moorestechAlphaMod_8/master/blocks.json）

| category | subCategory | ブロック |
|---|---|---|
| 採掘 | 採掘機 | 風力掘削機 / 原始的な採掘機 / 鉄の採掘機 / 電気採掘機 |
| 採掘 | 液体採取 | 油井 |
| 生産 | 原始加工 | 石窯 / 原始的な粉砕機 / 原始的な加工機 / 原始的な組立機 / ふいご付き精錬炉 |
| 生産 | 電気機械 | 電気汎用工作装置 / 電気粉砕機 / 電気炉 |
| 生産 | 化学 | 酸素発生装置 / 石油蒸留機 / 化学プラント |
| 生産 | 半導体 | EUV露光式半導体製造装置 |
| 動力 | シャフト | 木のシャフト / 木の縦シャフト / 木のシャフトボックス / 鉄のシャフト / 鉄の縦シャフト / 鉄のシャフトボックス |
| 動力 | 歯車 | 木の歯車 / 鉄の歯車 / 大きな鉄の歯車 |
| 動力 | チェーンポール | 歯車チェーンポール / コンパクト歯車チェーンポール |
| 動力 | 動力源 | 燃料式風車 / ボイラー / 蒸気機関 |
| 電力 | 発電 | 回転発電機 / ガソリンエンジン発電機 |
| 電力 | 変換 | 回転生成機 |
| 電力 | 送電 | 電柱 / 高圧電柱 / 広範囲電柱 |
| 物流 | 歯車コンベア | 直線/上り/下り歯車ベルトコンベア / 歯車コンベア分岐機 / 鉄の歯車ベルトコンベア(直線/上り/下り/分岐機) |
| 物流 | 電気コンベア | ベルトコンベア(直線/上り/下り/分岐器) / 高速ベルトコンベア(直線/上り/下り/分岐器) |
| 物流 | 仕分け | フィルター分岐器 |
| 物流 | チェスト | 木のチェスト / 木のコンベアチェスト / 鉄のコンベアチェスト / 鉄のミニコンベアチェスト |
| 液体 | パイプ | 鉄のパイプ / 鋼鉄のパイプ |
| 液体 | タンク | 液体タンク |
| 液体 | ポンプ | 歯車ポンプ |
| 輸送 | 鉄道 | 貨物プラットフォーム / 蒸気機関車駅 / レール橋脚 / 液体プラットフォーム |
| 建材 | 土台 | 基本土台 / アスファルト土台 |
| 建材 | クリーンルーム | クリーンルームブロック / クリーンルームドア / クリーンルームアイテムハッチ / クリーンルームパイプコネクタ / クリーンルーム空気清浄機 |

計73ブロック（採掘5 + 生産12 + 動力14 + 電力6 + 物流21 + 液体4 + 輸送4 + 建材7）。ブロック名は実データの完全名で照合すること。

**坂バリアントの注意**: 上り/下りコンベア坂は `BuildMenuEntryCatalog` の `IsSlopeBlock` 除外により**メニューに表示されない**が、スキーマrequired化のため category/subCategory は付与する。UI上の物流表示件数は21より少なくなる（仕様どおり）。

### 1.4 非ブロックエントリの種別導出カテゴリ

| entryType | category | subCategory |
|---|---|---|
| trainCar | 輸送 | 車両 |
| connectTool | ツール | 接続 |
| blueprintCopy | ツール | ブループリント |
| blueprint | ブループリント | 保存済み |

車両はブロックの「輸送」カテゴリに合流する。カタログの列挙順（ブロック→車両→接続ツール→BPコピー→保存BP）により、輸送カテゴリ内で「車両」サブカテゴリは常に「鉄道」の後、「ツール」「ブループリント」カテゴリは常にサイドバー末尾になる（§2の順序導出規則参照）。

### 1.5 導出されるサイドバー順（現sortPriorityでの実態）

採掘(180) → 物流(200) → 生産(220) → 動力(300) → 建材(400) → 液体(570) → 輸送(780) → 電力(970) → ツール → ブループリント

**これは解放進行順であり、初期リリースはこの順をそのまま受け入れる**（thematicな並び替えのためのsortPriority大規模改編は今回しない）。順序を変えたくなった場合の調整手段はマスタのsortPriority側を直すこと。

**master-refineとの相互作用**: master-refine操作AはsortPriorityをresearch解放順+nodeGraph配置から自動再計算するため、手動調整は再計算で上書きされる。カテゴリ順=進行順という本設計はこの再計算と整合する（進行順が変われば表示順も追従するのが正）。再計算後は§7の目視確認を必ず行う。

## 2. 配信契約の変更

- `BuildMenuDtos.cs` の `BuildMenuEntryDto` に以下を追加:
  - `Category` / `SubCategory`（required string。ブロックはマスタ値、非ブロックは種別導出値）
  - `RequiredItems`（`{ itemId: int, count: int }[]`。ブロック/車両は `RequiredItems` マスタ値から、無いエントリは空配列）
- `Tooltip` フィールドは**契約から削除**（整形済み表示文字列の逆パース依存を作らない。詳細プレビューが構造化データから描画する）。`Label` は継続
- **順序フィールドは追加しない**: カタログ（`BuildMenuEntryCatalog.cs`）が既にブロックをsortPriority昇順→車両→接続ツール→BPコピー→保存BPの順で配列構築しており、**配信配列の並び順そのものが順序の正**。カテゴリ/サブカテゴリの表示順はwebui側で「配列内の初出index」から導出する。カタログのこの順序保証を契約前提としてコード内コメントに明記する
- webui側 `bridge/contract/schemas/buildMenu.ts` の zod スキーマへ `category` / `subCategory` / `requiredItems` を必須追加、`tooltip` を削除
- 更新対象テスト・フィクスチャ:
  - `Client.Tests/WebUi/WireContractTest.cs`
  - 共有fixture `Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json`（C#とwebuiの `wireContract.test.ts` が両側で読む）
  - `moorestech_web/webui/src/bridge/contract/validators.test.ts` のbuildMenuインラインpayload（accept/rejectケース）
  - e2e mockフィクスチャ `e2e/mock-host/fixtures.ts`（category/subCategory/requiredItems付与。カテゴリ切替・空カテゴリ非表示・横断検索をe2eで検証できるよう「同一カテゴリ複数サブカテゴリ」「複数カテゴリ跨ぎで検索ヒット」するエントリ構成へ拡充）

## 3. Unity側（Client.Game / Client.WebUiHost）

- `BuildMenuEntry` にカテゴリ情報・建設コスト（構造化）を追加。tooltip文字列の組み立て（`CreateBlockToolTip` 等）は撤去し、Factory が `BlockMasterElement.Category` / `SubCategory` / `RequiredItems` を直接DTOへ写す
- `BuildMenuEntryCatalog` の列挙順・unlock判定・`FreeBlockPlacement` デバッグ全表示の挙動は現状維持
- uGUI側ビルドメニューは残置のまま非対応（UIはWeb移行完了方針）

## 4. webui設計

### 4.1 レイアウト

```
┌─────┬──────────────────────────────┐
│採掘  │ [検索入力]                    │
│物流  │ ┌ 詳細プレビュー（固定高）─┐   │
│生産  │ │ アイコン 名前 建設コスト │   │
│動力  │ └──────── FadeRule ──────┘   │
│建材  │ サブカテゴリ見出し(muted)      │
│ …   │ [slot][slot][slot]…          │
│     │ サブカテゴリ見出し             │
│     │ [slot][slot]… (縦スクロール)  │
└─────┴──────────────────────────────┘
```

- **外枠はレガシー面から `GamePanel variant="default"` + `title` へ移行する。** 配置は研究パネル・大型機械パネルの前例に合わせ、stageグリッドの2列（`viewer-start / items-end`）を占有する大型レイアウト。上端は持ち物パネルと揃え、下端はホットバー手前で止める。`inset: 8% 12%` の%指定・ハードコード色・独自面CSSは全廃
- **閉じるボタンは Mantine CloseButton から `PanelCloseButton` へ置換**、Mantine Title/Groupヘッダは GamePanel の `title` 罫線様式へ置換
- 左サイドバー: `ModeSwitch` を `orientation="vertical"` で使用（既存prop。利用側CSSでの縦化はしない）。選択中は既存 `data-selected` 表現
- **スロットは素div実装から `SlotFrame` ベースへ移行**（`--slot-size`・`data-*` 状態・`useSlotMouse`・`onHoverChange` はSlotFrameが供給。64px直書き・ハードコード色・生onClickを全廃）。Mantine Tooltip も撤去（詳細プレビューが代替）
- サブカテゴリ見出し: `--text-muted` のラベル + `FadeRule`。独自装飾は作らない
- 詳細プレビュー: 機械レシピ選択タブと同様の**固定高**上段。**ホバー中エントリを表示し、無ければ `--text-muted` の案内テキスト**（ビルドメニューには「選択中」状態が契約上存在しないため2段フォールバックはしない）。内容は「アイコン + 名前 + 建設コスト」:
  - アイコンは `entry.iconUrl` の img（スロットと同表現）。iconUrl無しエントリ（BPコピー・保存BP）は名前テキストのみ
  - 建設コストは `requiredItems` を `ItemSlot` 列（個数バッジ付き）で表示。空配列ならコスト行なし
- **4辺の余白設計**: サイドバー左端・グリッド右端・スクロール下端（GamePanel下向き三角と重なる）・上端はいずれもフェード帯/装飾と干渉しうる。既存の安全帯トークン前例（`--block-panel-right-safe-area` 等）に倣い、固定長トークンで「フェード幅+視認余白」を確保する。%指定禁止
- スクロールバーはOSネイティブを出さず、ネイビートーンのカスタムスクロールバー様式を webui-design 追記とセットで定義する

### 4.2 状態と挙動

- 選択中カテゴリ: `useState<string | null>(null)` で**カテゴリ名（値）で保持**し、描画時に「nullまたは消滅したカテゴリ名なら導出順の先頭」へフォールバック（データ非同期到着・BP増減の再配信・index ずれに耐える）。パネルは閉じるとunmountされるため、再オープンで検索文字列・選択カテゴリがリセットされるのは意図した仕様
- カテゴリ順・サブカテゴリ順: エントリ配列の初出index昇順で導出（§2）。**エントリが1件も無いカテゴリはサイドバーに出さない**
- 検索: 入力非空の間は**全カテゴリ横断**で `label` 部分一致（大文字小文字無視）。**検索中は常に「カテゴリ名 / サブカテゴリ名」の複合見出し**で区切り、グループ並びはカテゴリ導出順→サブカテゴリ導出順。**検索中はサイドバーを無効化**し、検索クリアで元のカテゴリ表示へ戻る。検索0件はグリッド領域に `--text-muted` の「該当なし」
  - 無効化の実現: `ModeSwitch` に汎用 `disabled?: boolean` prop + `data-disabled` 表現を追加する（ドメイン語彙なしの汎用属性。判断はbuildMenu側が行いboolを渡すだけ）。様式は§4.4で追記
- 表示リテラル（プレースホルダ・「該当なし」・プレビュー案内等）は**すべて `t()` 経由**（lint: no-jsx-visible-literal）。カテゴリ名・サブカテゴリ名・エントリ名はマスタ由来文字列のため `t()` を通さずraw表示（既存 `entry.label` と同扱い）
- クリック挙動（左クリック選択・blueprint右クリック削除等）は現状の `select` アクション契約のまま変更しない

### 4.3 ファイル分割（200行制限）

```
features/buildMenu/
  BuildMenuPanel.tsx          全体レイアウトと状態の束ね（GamePanel化）
  CategorySidebar.tsx         縦ModeSwitchサイドバー
  BuildMenuSearchInput.tsx    検索入力（新様式）
  BuildMenuDetailPreview.tsx  固定高詳細プレビュー
  BuildMenuCategoryGrid.tsx   サブカテゴリ見出し付きグリッド
  BuildMenuSlot.tsx           SlotFrameベースへ移行
  buildMenuGrouping.ts        カテゴリ/サブカテゴリ導出・順序・検索フィルタの純関数
  style.module.css
```

index.ts込み9ファイルで1ディレクトリ10上限内。

### 4.4 webui-designへの様式追記（実装より先）

以下を `.claude/skills/webui-design/SKILL.md` に追記してから実装に入る（節番号は現行最終節に続く番号で採番。§8.8は既にワールドピンHUDで使用済みのため衝突させない）:

1. **ModeSwitchの縦利用の成文化**（§8.6拡張）: 既存 `orientation="vertical"` propによるサイドバーナビ利用の許可
2. **ModeSwitchのdisabled状態**（§8.6拡張）: `disabled?: boolean` + `data-disabled`（`--text-muted` 系の減衰表現・クリック不可）
3. **検索入力の様式**（新規）: 素の `<input>` + GamePanel同族の半透明面トークン + `--text-muted` プレースホルダ + focus-visible表現はModeSwitch前例踏襲 + 寸法は固定長トークン。Mantine TextInputは使わない
4. **カスタムスクロールバー様式**（新規）: ネイビートーン・トークン参照
5. **建設メニュー節**（新規）: 本specのレイアウト（大型2列占有・縦サイドバー+検索+固定プレビュー+サブカテゴリ見出しグリッド）を構成要素・使用トークン込みでホワイトリスト化

## 5. エッジケース

- **付与漏れブロック**: スキーマrequired化によりマスタロードで即失敗（無言の未分類落ちなし）
- **保存BPの動的増減**: 種別導出のため常に「ブループリント」カテゴリに出る。メニューを開いたままのBP保存/削除は再配信を起こすが、選択カテゴリは名前保持のため維持され、消滅時のみ先頭へフォールバック
- **検索0件**: `--text-muted` の「該当なし」テキスト
- **未解放進行初期**: 解放済みが1カテゴリしか無い場合もサイドバーは1項目で表示（特別扱いしない）
- **輸送ブロック未解放で車両のみ解放**: カタログ列挙順により輸送カテゴリは車両の位置で初出し、接続ツールより前に出る（順序導出は初出indexのため矛盾しない）

## 6. 検証計画

1. `uloop compile --project-path ./moorestech_client`（BlockMasterElement手書き構築3ファイルの追従込み）
2. クライアント全テスト（テストmodのJSON更新漏れは MooresmasterLoaderException で全滅するため、契約テストだけでなく広めに回す）+ `WireContractTest`
3. webui: `validators.test.ts` / `wireContract.test.ts` 更新、既存 `e2e/tests/regression/buildMenu.spec.ts` を再構成（現行の「3エントリ同時可視」assertはカテゴリ分散で成立しなくなるため、select・BP右クリック削除テストにカテゴリ切替操作を挿入）。追加シナリオ: カテゴリ切替 / 横断検索と複合見出し / 検索中サイドバー無効 / 検索0件表示 / 検索クリア復元 / ホバーでプレビュー更新・解除でフォールバック / 空カテゴリ非表示
4. mockホストで目視QA（webui-design §10: 4辺の余白・フェード帯・中央揃え・見出し区別をスクショ確認）
5. 実マスタで起動し、表示対象ブロック（坂除く）の分類・カテゴリ順を目視確認。blocks.json全73件の category/subCategory 付与網羅は jq 等で機械確認
6. master-refine 操作Aを実行した場合はカテゴリ順の目視確認を再実施（§1.5）

## 7. 自己反証

- 「順序を配列初出順（=sortPriority）から導出」への反例: サイドバー順が進行順になりthematic順にならない（§1.5の実態）。→ 初期リリースは進行順を仕様として受け入れ、UI側に順序テーブルを持たない。調整はマスタ側・master-refine側の責務
- 「検索中サイドバー無効化」への反例: 検索しながらカテゴリを絞りたいユースケース。→ 73種の規模では横断検索で十分（YAGNI）。必要になったら様式更新から入る
- 「category自由文字列」への反例: タイポで新カテゴリが生えて気づかない。→ 検証計画5の機械確認+目視で検出する。foreignKey化（カテゴリマスタ新設）は導出可能テストにより不採用
- 「tooltip契約削除」への反例: 他にtooltipを使う消費者がいれば破壊。→ 現状の消費者はBuildMenuSlotのMantine Tooltipのみ（同時撤去）で、契約はbuildMenuトピック閉域。破壊なし
