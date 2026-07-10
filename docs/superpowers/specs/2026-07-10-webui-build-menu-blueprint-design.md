# web-ui ビルドメニュー + ブループリント設計

日付: 2026-07-10
ブランチ: web-ui
関連: `docs/superpowers/specs/2026-07-07-blueprint-design.md`（サーバー/uGUI側BP機能）、`docs/superpowers/specs/2026-07-06-webui-block-research-ui-design.md`（web-ui feature追加の先行例）

## 目的

uGUIにのみ存在するビルドメニュー（Bキー）とブループリント関連UI（BP選択・BPコピーの名前入力・BP削除）をweb-ui（React/CEF）へフル移植する。エントリはブロック・車両・接続ツール・BPコピー・保存済みBPの全種別を選択可能にする（uGUIビルドメニューと完全パリティ）。

## スコープ外

- ドラッグ範囲選択の可視化・ペーストのゴーストプレビュー・回転（3Dワールド描画。Unity側に残す）
- PlaceBlockステート中のweb画面（パネル無し=gameレイヤーのまま）
- uGUI側ビルドメニューの削除・変更（並存継続。表示切替は既存WebUiCefToggle機構）

## 確定済みの設計判断

- **エントリ一覧はUnity側で合成してtopic push**: 既存`BuildMenuEntryCatalog.CreateEntries`（解放済みブロック→車両→接続ツール→BPコピー→保存済みBPの順に合成済み）を再利用。web側でマスタ・アンロック状態・BP一覧を個別取得して再合成する案は、アンロック状態topicの新設が必要になり情報源が二重化するため不採用（ResearchTopicの合成push先行パターンに従う）
- **選択の反映はuGUI既存消費経路に合流**: webの選択アクションは`BuildMenuView`の選択消費キューに投入し、`BuildMenuState.GetNextUpdate`（毎フレームpoll）が既存経路で`PlacementSelection`設定とPlaceBlock遷移を行う。web専用に`PlacementSelection`へ直接書き込む迂回経路は作らない（SSOT。`BuildViewModeController`のセッション管理も自然に同一挙動になる）
- **BP名入力は既存モーダルシステムの入力対応拡張**: `WebUiModalService`/`ModalTopic`/`ModalHost`のpush型要求・応答パターンにオプショナルな入力フィールドを追加。BP専用のダイアログtopic新設は配管の重複になるため不採用
- **BP右クリック削除はuGUIと同じ即時削除**（確認モーダル無し。移植のためUX一致を優先）
- **重複BP名はサーバー側解決に従う**: サーバーが`RegisteredName`を返す既存挙動（`ClientBlueprintLibrary.CreateBlueprint`）に乗り、クライアント側の重複チェックは追加しない

## データフロー

```
Bキー(Unity入力) → UIState=BuildMenu → UiStateTopic → web: buildMenu画面表示
                                     → BuildMenuTopic(build_menu.entries) → エントリグリッド描画
webでエントリ左クリック → action: build_menu.select → BuildMenuViewの選択キューへ投入
    → BuildMenuState(既存)が消費 → PlacementSelection設定 → PlaceBlock遷移
    → UiStateTopic → web: パネル無し(gameレイヤー)
BPコピー: 3Dドラッグ(Unity) → 名前入力要求(サービス分岐) → web入力モーダル → 確定(text)
    → ClientBlueprintLibrary.CreateBlueprint → build_menu.entries再配信
BPエントリ右クリック → action: blueprint.delete → DeleteBlueprint → entries再配信
```

## Unity側（Client.WebUiHost）

### BuildMenuTopic（`build_menu.entries`）

- `BuildMenuEntryCatalog.CreateEntries`を呼び、`iconView`は無視してキーからDTOを構築する
- エントリDTO: `{entryType, entryKey, label, tooltip, iconUrl?}`
  - `entryType`: `"block" | "trainCar" | "connectTool" | "blueprintCopy" | "blueprint"`
  - `entryKey`: blockId / trainCarGuid / placeMode / blueprintName（blueprintCopyは固定キー）。**配列indexは使わない**（再配信でindexがずれるため）
  - `iconUrl`: blockは`/api/block-icons/…`、trainCarは`/api/train-car-icons/…`、connectToolは`IconItemGuid`経由で`/api/icons/…`。BP・BPコピーはnull（テキストスロット表示）
- 再配信トリガ: BPライブラリ変化・アンロック状態変化・BuildMenuステート遷移時

### TrainCarIconEndpoint

- `GET /api/train-car-icons/{trainCarGuid}.png`。`BlockIconEndpoint`と同型の実装

### アクション

- `build_menu.select`: payload = `{entryType, entryKey}`
  - 検証1: 現UIStateがBuildMenuでなければ`fail("invalid_state")`（Unity側が先に閉じたレース対策）
  - 検証2: エントリが現在のカタログに実在しなければ`fail("unknown_entry")`（削除済みBPへのstaleクリック対策。失敗時は正しい一覧を再配信）
  - 成功時: `BuildMenuView`の選択消費キューへ投入（遷移は既存`BuildMenuState`任せ）
- `blueprint.delete`: payload = `{name}`。`ClientBlueprintLibrary.DeleteBlueprint` → topic再配信
- `ui_state.request`: 許可リストに`BuildMenu`を追加（webからの開閉用）

### 入力モーダル拡張（WebUiModalService）

- `RequestInputModal(title, message, buttonText)`を追加。`ModalRequest`に入力モード用フィールドを追加し、応答は`{result, text}`
- `BlueprintCopySystem`の名前入力要求をサービス分岐化: CEFアクティブ時は`WebUiModalService.Instance.RequestInputModal`、非アクティブ時は既存`BlueprintNameInputView`
- 検証はuGUIと同一: 空白のみの名前は確定不可、確定時Trim
- コピーキャンセル時（ESC・ステート離脱）は既存`CancelPending`で保留要求をcancel解決。遅延応答はid不一致で無視（既存機構）

## Web側（moorestech_web/webui）

### `features/buildMenu/`

- `BuildMenuPanel`: SlotGrid流用のエントリグリッド。`iconUrl`ありは画像スロット、null（BP・BPコピー）はテキストスロット。ツールチップ表示
- 左クリック=`build_menu.select`送信。BPエントリのみ右クリック=`blueprint.delete`即時送信

### ルーティング・キー入力

- `uiScreenRouting`に`BuildMenu → "buildMenu"`を追加し、App.tsxにパネルを配線
- B/ESCで`ui_state.request(GameScreen)`、Tabで`ui_state.request(PlayerInventory)`（既存のwebキー処理パターンに従う）

### ModalHost拡張

- inputバリアント: テキストフィールド + 確定/キャンセル。空白のみの入力は確定ボタン無効

## エッジケース

| ケース | 挙動 |
|---|---|
| 削除済みBPへのstaleクリック | `unknown_entry`で失敗、正しい一覧を再配信 |
| BuildMenu以外でのselect到達 | `invalid_state`で拒否 |
| web入力モーダル表示中のコピーキャンセル | `CancelPending`でモーダル消去、遅延応答はid不一致で無視 |
| 重複BP名 | サーバーの`RegisteredName`解決に従う |
| 空白のみのBP名 | web側で確定ボタン無効（uGUIの`BlueprintNameInputView`と同一検証） |

## テスト

- web: vitest単体（ルーティング拡張・エントリ描画のtype別分岐・入力モーダルの確定無効化）。既存e2eパターンがあれば追随
- Unity: `uloop compile`必須。既存のBP統合プレイテストシナリオはuGUI経路のため、web経路（select・削除・名前入力）は手動確認
