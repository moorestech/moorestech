# 研究UI デザイン哲学準拠リデザイン 設計書

日付: 2026-07-19
ステータス: ユーザー承認済み（対話にて承認）

## 背景と目的

研究UIはWeb UI(CEF)へ機能移行済み（`moorestech_web/webui/src/features/research/`、パン・ズーム付きTreeView、`research.tree`トピック + `research.complete`アクション）。しかし見た目がWeb UIデザイン哲学（`.claude/skills/webui-design`）に違反している:

- 全画面 `position: fixed` + 不透明 `--mantine-color-dark-8` 塗り潰し（「全画面UIは作らない」「不透明な面は作らない」違反）
- Mantine素の `Paper` / `Button` / `Tooltip` / `Title` 剥き出し（「Mantine標準テーマ剥き出し禁止」違反）
- 機能側CSSでの独自パネル面・独自カード（「GamePanel以外のパネル背景禁止」違反）

これを、モック（左=インベントリ、右=研究グラフの2ペインモーダル構成、ノードは「研究名+アイテムアイコン」カード）に沿って、デザイン哲学準拠の見た目へ再設計する。

```
 モーダルビュー
┌─────────────────────────────────────────────────────────────────┐
│ ┌───────────────┐┌───────────────────────────────────────────┐  │
│ │ Inventory     ││  Research Graph                           │  │
│ └───────────────┘└───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘

  一つの研究アイコン
┌───────────────────┐
│ Research Name     │
│   ┌──────────┐    │
│   │Item Icon │    │
│   └──────────┘    │
└───────────────────┘
```

## スコープ

- 対象: `moorestech_web/webui/src/features/research/` の見た目層、`App.tsx` の画面合成、C#側 `ResearchNodeDtoFactory` のDTO拡張（iconItemId追加）、zodスキーマ、`webui-design`スキル本文の様式追記
- 非対象（変更しない）: Rキー開閉・`ui_state.current`駆動・サーバープロトコル（`va:getResearchInfo` / `va:completeResearch`）・`researchLogic.ts`の判定ロジック・TreeViewのパン・ズームと接続線機構・uGUI側実装

## 設計

### 1. 画面構成（App.tsx）

- researchTree画面で左に既存`InventoryPanel`をそのまま表示する。`App.tsx`の`inventoryScreen`相当の表示条件にresearchTreeを追加（`GrabOverlay`も含め、インベントリは通常どおり操作可能）。
- 右側の残り領域に研究グラフパネルを配置（インベントリ画面の3カラムグリッドに倣い、グリッドエリアで配置）。
- 背景ディムはAppの共有screen backdrop 1枚のみ（researchTreeは既に`modalScreen`扱い）。パネル側で画面を暗くしない。
- キー操作ヒントは`InventoryScreenChrome`のkeyHints様式（`<kbd>` + `t()`）に従い、研究画面用のヒント（閉じる操作等）を表示する。

### 2. 研究グラフパネル

- 現在の「全画面fixed + 不透明dark-8 + z-index直書きCSS」を廃止。
- `GamePanel variant="default"` + タイトル「研究」（上下罫線あり＝一覧の置き場に該当）に載せ替える。
- body内で既存TreeView（パン・ズーム・接続線）をそのまま動かす。オーバーフローはパネルbody内でクリップ。
- 面は半透明ネイビー（GamePanelが担う）。世界背景が透ける。

### 3. 研究ノードカード（新様式）

- 構造: 「研究名1行（ellipsis）+ `ItemSlot`によるアイコン」の縦積みカード。説明文・消費アイテム・報酬・実行ボタンはカードから撤去（詳細ペインへ移動）。
- アイコンは`research.yml`の`graphViewSettings.IconItem`を表示する。**現DTOに未搭載のため拡張が必要**:
  - C#: `Client.WebUiHost/Game/Topics/Research/ResearchNodeDtoFactory.cs`（+ `ResearchTopicDtos.cs`）に`iconItemId`を追加
  - Web: `bridge/contract/schemas/research.ts`の`ResearchNodeDataSchema`に`iconItemId`を追加、wireContractテスト追従
- 状態表現はdata属性に統一: `data-completed` / `data-researchable` / `data-locked`（未研究かつ前提未達）/ `data-selected`（詳細ペイン表示中）。色・枠はすべて`index.css`のCSS変数トークンから取得し、機能側CSSに新色をハードコードしない。
- Mantine `Paper` / `Button` / `Tooltip` は全廃。
- アイコン未設定ノードのフォールバック: 素の`SlotFrame`を表示（IconItemがマスタで必須かは実装時に確認し、必須なら本フォールバックは削除）。
- 新様式のため、実装前に`webui-design`スキル本文へ「研究ノードカード」の節を追記して裁定を取る（ホワイトリスト運用）。

### 4. 詳細ペイン（オンオフ可能）

- ノードクリックで選択 → グラフパネル脇（パネル内固定位置。パン・ズームの影響を受けない）に`GamePanel variant="craft"`の詳細ペインが開く。
- 内容: 研究名・説明文・消費アイテム（`ItemSlot` + `data-insufficient`で不足表示）・報酬アイテム/アンロックアイテム（`ItemSlot`）・研究実行ボタン。
- 研究実行ボタンは主要アクション様式（`--recipe-action-background`の青グラデ）を使用。非活性条件と理由文言は既存`deriveResearchButton`の結果をそのまま使う。
- 開閉: ノードクリックで開く。同ノード再クリック、またはペイン右上の閉じるボタンで閉じる（ペインはオンオフ可能）。別ノードクリックで内容が切り替わる。
- 研究実行は既存`dispatchAction("research.complete", { researchGuid })`。実行後のツリー再取得は既存トピック機構のまま。
- 選択状態（どのノードを開いているか）はWeb側のローカルUI状態（Reactローカルstate）。サーバー同期しない。
- 詳細ペインもGamePanelの範囲内の様式だが、「グラフ内詳細ペイン」としての使い方を`webui-design`スキルへ追記する。

### 5. 文字・i18n・通知

- 表示文字列はすべて`t()`経由（lint: no-jsx-visible-literalで担保）。
- フォント・ウェイトは既存トークンに従う（合成bold禁止）。
- ホバーツールチップは新設しない（詳細情報は詳細ペインが担う）。

## エラーハンドリング

- トピック未受信時は既存どおり空配列で描画（`ConnectingPlaceholder`の要否は既存画面の前例に従う）。
- 研究実行の失敗（サーバー側却下）は既存機構（レスポンスでの全ノード状態更新）で表示が追従する。新規のエラーUIは作らない。

## テスト

- `researchLogic.test.ts`: 変更なし（ロジック無傷のため）。
- `ResearchTreePanel.test.ts`: レイアウト・data属性の変更に追従。
- wireContract テスト: `iconItemId`追加に追従（C# DTOとzodの整合）。
- 詳細ペインの開閉（選択→表示、再クリック→非表示、別ノード→切替）のコンポーネントテストを追加。
- 視覚確認はプレイテストDSL（unity-playmode-recorded-playtest）で研究画面を開いたスクリーンショットを取得して行う。

## 自己反駁（潰した穴）

- パン操作とノードクリックの競合 → 既存TreeViewが`nodeTargetSelector`で区別済み。
- 掴んだアイテムをグラフ側にドロップ → GrabOverlayの既存挙動（戻す）に従い、新規処理は作らない。
- 研究名が長い場合 → カード幅固定+1行clamp。全文は詳細ペインで読める。
- アイコン未設定ノード → `SlotFrame`フォールバック（上記3.参照）。

## 検証済み事項の範囲

- 「研究UIがWeb実装済み」「プロトコル共有済み」は調査エージェントのコード確認による（`ResearchTreePanel.tsx`・`ResearchTopic.cs`・`GetResearchInfoProtocol.cs`等の実在確認）。
- `iconItemId`がDTOに無いことは`bridge/contract/schemas/research.ts`の実読で確認済み。C#側DTOの実ファイル構造は実装時に確認する。
