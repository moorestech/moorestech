# 機械レシピ任意選択 設計書

日付: 2026-07-10
ステータス: ユーザーレビュー待ち

## 概要

機械ブロック（電動機械・歯車機械）のレシピ決定を、現行の「インベントリ内容からの自動判定」から「プレイヤーによる明示的なレシピ選択（選択必須）」に置き換える。レシピ未選択の機械は加工しない。自動判定コードは削除する。

## 決定事項（ユーザー確認済み）

| 論点 | 決定 |
|---|---|
| 未設定時の挙動 | 選択必須。自動判定は廃止し、未選択の機械は動かない |
| 搬入制限 | しない。入力スロットは従来通り何でも受け入れ、選択レシピの材料が揃った時だけ加工 |
| 選択UI | 機械インベントリUI内にアンロック済みレシピの一覧パネルを表示し、クリックで選択 |
| 加工中のレシピ変更 | 進行中ジョブを即中断し消費済み材料を返却。キャンセル可否判定はアイテムのみで、液体は戻せる分だけ戻し溢れは消失（下記「加工中の変更」参照） |

## サーバー: 選択状態と加工開始判定

- `VanillaMachineProcessorComponent` に選択レシピ（`MachineRecipeMasterElement`、未選択は null）を保持する。変更は `SetSelectedRecipe` / `ClearSelectedRecipe` 経由。
- `IdleMachineProcessState` の判定を「未選択なら常に Idle、選択済みなら選択レシピの材料充足（既存 `RecipeConfirmation`）を確認して加工開始」に置き換える。
- 自動判定の撤去:
  - `MachineRecipesMasterUtil.Initialize` の入力組合せ→レシピ辞書構築、`GetRecipeElementKey`、`TryGetRecipeElement(blockId, itemIds, fluidIds)` を削除
  - `VanillaMachineInputInventory.TryGetRecipeElement` の自動探索経路を削除
  - これにより「同一ブロック＋同一入力集合＝1レシピ」の一意化制約は消滅し、同一入力で複数レシピが定義可能になる
- アンロック検証は Set 時にサーバー側で行う。アンロックは不可逆のため加工開始時の再チェックは不要。
- マスタ（machineRecipes スキーマ・JSON）は変更しない。

## 加工中の変更（即中断＋原子的返却）

加工中（Processing）に SetRecipe / Clear を受けた場合:

1. **収容シミュレーション（アイテムのみ）**: 進行中ジョブが開始時に消費したアイテム材料（`isRemain` を除く inputItems）について、「入力インベントリ → リクエストプレイヤーのメインインベントリ」の順で全量収容できるかを先に判定する。
2. **アイテム全量収容可能な場合のみ**: ジョブ中断（Idle へ遷移）→ 材料返却 → 選択変更、をまとめて実行する。進行中ジョブの成果物（開始時抽選済みの pendingOutputs）は破棄。
3. **アイテム収容不能な場合**: 変更自体を失敗として理由 enum で応答し、ジョブは継続。部分返却は発生させない。
4. **液体はベストエフォート返却**: 消費済みの inputFluids は入力タンクへ戻せる分だけ戻し、収まらない分は消失させる（液体はインベントリで扱えないため）。液体の返却可否はキャンセル判定に含めない。

- 同一レシピの再設定は no-op（ジョブを中断しない）。
- Idle 中の変更は返却処理なしで即反映。

## プロトコル

新プロトコル1本 `MachineRecipeSelectionProtocol`（タグ `va:machineRecipeSelection`）。creating-server-protocol スキルの規約に従う。

- リクエスト: Operation enum（`Get` / `SetRecipe` / `Clear`）＋ private コンストラクタ＋ static factory
  - `CreateGetRequest(pos)`
  - `CreateSetRecipeRequest(pos, machineRecipeGuid, playerId)`
  - `CreateClearRequest(pos, playerId)`
  - playerId は加工中変更時の材料返却先の特定に使う
- レスポンス: 成否理由 enum ＋ 現在の選択スナップショット（選択中レシピ GUID）。クライアントはこれで再描画する。
- サーバー側検証（SetRecipe 時）:
  1. 対象座標にブロックが存在し Processor コンポーネントを持つ
  2. レシピの `BlockGuid` が対象ブロックと一致する
  3. レシピがアンロック済みである
  4. （加工中のみ）返却アイテム材料が全量収容可能である（液体は判定対象外）
- `VanillaApi`（VanillaApiWithResponse）へのメソッド追加は1本のみ。Request は呼び出し側で構築する。

## セーブ / Blueprint

- `VanillaMachineProcessorSaveJsonObject` に `selectedRecipeGuid` を追加する。既存の `recipeGuid`（加工中ジョブのレシピ）とは別フィールド。
- ロード時に GUID がマスタから解決できなければ未選択にフォールバックする（マスタ更新でレシピが消えても破損しない。機械は停止する）。
- 既存セーブはフィールド無し＝未選択として読める（後方互換は要件ではないが自然に成立する）。
- Blueprint 対応: `IBlockBlueprintSettings` を `VanillaMachineProcessorComponent` に Get/Apply 対で実装し、選択レシピ GUID を保存する。Apply 時も Set と同じ検証（ブロック一致・アンロック）を通し、不成立なら未選択のまま設置する（設置直後のため加工中返却は発生しない）。

## クライアント同期・UI

- 選択状態の取得は UI オープン時の Get リクエスト（FilterSplitter と同じ経路）。常時同期の `MachineBlockStateDetail` は現状のまま（加工中レシピ表示の既存用途のみ）。
- `MachineBlockInventoryView` にレシピ一覧パネルを追加:
  - 対象ブロックのアンロック済みレシピを `ItemRecipeViewerDataContainer` の列挙から導出してグリッド表示（一覧取得プロトコルは新設しない）
  - クリックで SetRecipe 送信、レスポンスのスナップショットで選択中ハイライトを更新
  - 変更失敗（返却不能等）は理由に応じたフィードバックを表示
  - 未選択の機械には「レシピ未設定」表示を出す
- 歯車機械 UI（`GearMachineBlockInventoryView`）は継承で同機能を得る。
- 図鑑のレシピビューア（`MachineRecipeView`）は閲覧用途のまま変更しない。

## テスト

- 自動判定前提の既存サーバーテストを「レシピ設定 → 加工検証」に書き換える（多数あるはず。実装計画で洗い出す）。
- 新規パケットテスト: Get/Set/Clear の往復、別ブロック用レシピの拒否、未アンロックレシピの拒否。
- 加工中変更: 入力インベントリへの返却、入力インベントリ溢れ時のプレイヤー返却、アイテム全量収容不能時の変更キャンセル（ジョブ継続）、液体の入力タンク返却とタンク溢れ分の消失（変更は成立すること）、isRemain 材料が返却されないこと。
- セーブ/ロードで選択が復元されること。消滅 GUID の未選択フォールバック。
- 同一入力素材で複数レシピを持つブロックが、選択どおりのレシピで加工すること。
- Blueprint の Get/Apply でレシピ選択がコピーされること。

## 自己反証（設計が弾くべきケース）

- **別ブロック用レシピの直送**: クライアントが歯車機械に電気炉用レシピの GUID を SetRecipe で送る（UI は自ブロックのレシピしか出さないが、プロトコルは任意 GUID を受け取れる）→ サーバー検証②（`BlockGuid` 照合）で拒否。
- **Blueprint 経由の混入**: 別ブロックのレシピ GUID が Blueprint に紛れる → Apply 時の同一検証で未選択フォールバック。
- **返却不能な中断**: 入力・プレイヤー両インベントリがアイテムで満杯 → 変更キャンセル、ジョブ継続、部分返却なし。入力タンクがパイプで再充填済みで液体が戻せないケースは変更を妨げず、溢れ分の液体のみ消失する。
- **マスタ更新でのレシピ消滅**: セーブ中の selectedRecipeGuid が解決不能 → 未選択フォールバックで停止（破損しない）。

## 実装時参照（調査済みの主要ファイル）

- 判定起点: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs`
- 自動探索: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs` / `Game.Block/Blocks/Machine/MachineRecipeMaster.cs` / `Core.Master/Validator/MachineRecipesMasterUtil.cs`
- 状態機械・セーブ: `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` / `State/ProcessingMachineProcessState.cs` / `VanillaMachineSaveComponent.cs` / `VanillaMachineTemplate.cs`
- プロトコル雛形: `Server.Protocol/PacketResponse/FilterSplitterStateProtocol.cs`（登録は `PacketResponseCreator.cs`）
- クライアント: `Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs` / `FilterSplitterBlockInventoryView.cs` / `RecipeViewer/ItemRecipeViewerDataContainer.cs`
