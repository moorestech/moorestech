# ElectricToGearGenerator モード選択UI（Plan 2-A）設計

**ステータス:** 設計承認済み（2026-06-06）。Codex 外部監査（session `019e9875-ef9e-7b83-8846-9b2cb15dbd64`）反映済み。

## ゴール

サーバー実装済みの `ElectricToGearGenerator`（電力消費→ギア動力生成、出力は `{rpm, torque, requiredPower}` の離散テーブル）に対し、プレイヤーがブロックを開いて**出力モードを選択できるクライアントUI**を実装する。ブロックを開くと全モードを一覧表示し、行をクリックして選択、選択は `SetElectricToGearOutputModeProtocol` でサーバーへ送られ、選択状態・電力充足率・消費電力がUIへ反映される。

## スコープ

**含む（Plan 2-A）:**
- サーバー側: `ElectricToGearGeneratorComponent` に状態変更通知（`IBlockStateObservable`）を追加し、モード切替・充足率変化を即 push 同期する。
- クライアントAPI: `VanillaApiWithResponse` にモード切替メソッドを追加。
- クライアントUI: 全モード一覧リスト形式のブロックUI（View + 行 + 充足率/消費表示）。
- UI prefab: `uloop execute-dynamic-code` で構築・Addressables登録。
- テストデータ: `EditModeInPlayingTestMod`（クライアント側 ServerData）に UI 付きテストブロックを追加。
- テスト: PlayMode で View→API→サーバー→StateDetail の実往復を検証。

**含まない（Plan 2-B 以降）:**
- 実プレイ用 Mod データ・アイテム画像（`../moorestech_master`）。
- ブロック見た目 prefab（ギア回転アニメ等）。
- UI の美装（最終的なレイアウト・配色の作り込み）。

## 重要な設計判断

### 1. 状態の責務分離: マスタ vs StateDetail
- **出力テーブル（各モードの rpm/torque/requiredPower）** は不変なので**マスタ**から読む: `BlockGameObject.BlockMasterElement.BlockParam as ElectricToGearGeneratorBlockParam` → `OutputModes`。
- **可変状態（選択 index・電力充足率・消費電力）** は **StateDetail** から読む: `BlockGameObject.GetStateDetail<ElectricToGearGeneratorBlockStateDetail>("ElectricToGearGenerator")`。

クライアントもマスタをロード済み（`MasterHolder`）なので `BlockParam` にアクセスできる。`BlockParam as ...` は null ガードし、失敗時はログ1回＋UI無効化。初回 StateDetail 未着信時（`GetStateDetail` が default）は「未同期」表示にし、毎フレームの error log は出さない。

### 2. サーバー側の状態 push（Plan 2-A で実装）
`ElectricToGearGeneratorComponent` の現状は独自の状態変更通知を持たず、状態 push は基底 `GearEnergyTransformer`（`SimpleGearService` の rpm/torque 変化）に依存する。モード切替で rpm/torque が変わらない場合や、充足率のみ変化する場合に push されない。これは Plan 1 で「ライブ更新が必要なら Plan 2 で採用」と先送りした項目。Plan 2-A の UI はライブ更新が必須なので**ここで実装する**。

- `ElectricToGearGeneratorComponent : ... , IBlockStateObservable` を追加（`FuelGearGeneratorComponent` 方式を手本）。
- 独自 `Subject<Unit>` を持ち、基底ギアの状態変化を転送（subscribe して中継）しつつ、`SetSelectedMode` 成功時と `UpdateFulfillment` で充足率が変わった時に明示発火する。
- `OnChangeBlockState` を `new` で公開（基底も `IBlockStateObservable` を実装しているため。`FuelGearGeneratorComponent` と同形）。
- 効果: モード切替→ `OnChangeBlockState` 発火→ `BlockSystem` が StateDetail を再 push →クライアントが次 Update で最新の `SelectedIndex`/`ElectricFulfillmentRate`/`ConsumedElectricPower` を読む。**他クライアントにも即 push され、Plan 1 で残した穴が完全に閉じる**。
- これにより**クライアントは楽観更新ハック不要**。毎 Update で StateDetail を読むだけでよい。プロトコル応答は失敗検出（`Success==false` 時に選択を戻す／何もしない）に使う。

### 3. インベントリ無しブロックUIの土台
このブロックはアイテムインベントリを持たない。手本は `TrainPlatform` より **`FilterSplitterBlockInventoryView` / `GearEnergyTransformerUIView`**（どちらもインベントリ無しでこの枠組みに乗っている）。
- `MonoBehaviour, IBlockInventoryView` を直接実装（`CommonBlockInventoryViewBase` は使わない）。
- `SubInventoryState.cs:118` が `instantiatedView.GetComponent<ISubInventoryView>()` を**null チェック無し**で取得するため、View component は prefab の**ルート**に付ける。
- `UpdateItemList` / `UpdateInventorySlot` は no-op。

### 4. UI prefab 構成（単一 prefab + 行テンプレート）
行を別 Addressable にせず、**単一 prefab** に以下を持たせる:
- ルートパネル（View component をルートに付与）
- content 親（行を並べる `VerticalLayoutGroup`）
- 非アクティブな**行テンプレート**（同一 prefab 内の子。実行時に複製して各モード行を生成）
- ステータス表示（充足率バー、消費電力テキスト、未同期表示）

行の選択は `ToggleGroup` + `Toggle.SetIsOnWithoutNotify` を使い、「クリック送信」と「StateDetail 反映」のフィードバックループを防ぐ。prefab は `uloop execute-dynamic-code` で構築し、既存 UI と同じ Addressables グループ（Vanilla Asset Group）に Editor API でエントリ登録する。手で `.prefab` / Addressables YAML を編集しない。

## コンポーネントとファイル

| 役割 | パス | 新規/変更 |
|---|---|---|
| サーバー: 状態push | `moorestech_server/.../Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs`（`IBlockStateObservable` 追加） | 変更 |
| クライアントAPI | `moorestech_client/.../Client.Network/API/VanillaApiWithResponse.cs`（`SetElectricToGearOutputMode(Vector3Int, int, ct)` 追加） | 変更 |
| UI本体 | `moorestech_client/.../UI/Inventory/Block/ElectricToGearGeneratorBlockInventoryView.cs`（`MonoBehaviour, IBlockInventoryView`、行を動的生成、毎Update で StateDetail 反映） | 新規 |
| 行View | `moorestech_client/.../UI/Inventory/Block/ElectricToGearOutputModeRowView.cs`（index＋rpm/trq/W表示＋Toggle＋クリックでSubject発火） | 新規 |
| UI prefab | `moorestech_client/Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab`（uloop構築・Addressables登録） | 新規 |
| テストMod | `moorestech_client/.../Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/{blocks.json,items.json}`（UI付きブロック追加） | 変更 |
| サーバー状態push テスト | `moorestech_server/.../Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs`（モード切替で OnChangeBlockState 発火を検証） | 変更 |
| クライアントUI テスト | `moorestech_client/.../Client.Tests/EditModeInPlayingTest/ElectricToGearModeSelectUITest.cs`（往復＋スモーク） | 新規 |

## データフロー

```
[ブロックを開く]
  SubInventoryState が master の blockUIAddressablesPath の prefab を Addressables ロード＆Instantiate
  → ルートの ElectricToGearGeneratorBlockInventoryView を ISubInventoryView として取得
  → Initialize(blockGameObject)
       master の OutputModes を読み、行テンプレートを複製してモード行を生成

[表示（毎 Update）]
  state = BlockGameObject.GetStateDetail<ElectricToGearGeneratorBlockStateDetail>("ElectricToGearGenerator")
  state==default → 「未同期」表示
  else → ToggleGroup で state.SelectedIndex の行を SetIsOnWithoutNotify(true)
         充足率バー = state.ElectricFulfillmentRate, 消費 = state.ConsumedElectricPower

[操作（行クリック）]
  行View が index を Subject 発火 → View が
    VanillaApi.Response.SetElectricToGearOutputMode(blockPos, index, ct)
  → サーバー: SetSelectedMode(index) → OnChangeBlockState 発火 → StateDetail 再 push
  → クライアント: 次 Update で最新 state を反映（楽観更新不要）
  → 応答 Success==false（InvalidIndex 等）なら選択を戻す/何もしない（例外は投げない）
```

## エラー処理

- try-catch は使わない（規約）。条件分岐・null チェックで対応。
- 二重送信防止: 送信中フラグ＋ `CancellationToken`。送信中は新規クリックを無視。
- `DestroyUI` で CTS をキャンセルし購読を破棄。キャンセル後・破棄後は UI を触らない（`_isSending` 復帰時も destroy 済みなら何もしない）。
- 応答失敗（`BlockNotFound`/`NotElectricToGear`/`InvalidIndex`）は選択を StateDetail の真値へ戻すのみ。
- `BlockParam as ElectricToGearGeneratorBlockParam` が null の場合はログ1回＋UI 無効化。

## テスト

### サーバー側（既存テストに追加・EditMode）
`ElectricToGearGeneratorTest.cs` に、`IBlockStateObservable.OnChangeBlockState` がモード切替で発火することを検証するテストを追加（`FuelGearGeneratorTest` の `OnChangeBlockState` 購読パターンを手本）。

### クライアント側（PlayMode・EditModeInPlayingTest）
`EditModeInPlayingTestMod` に `blockUIAddressablesPath` を設定した ElectricToGearGenerator テストブロックを追加した上で:
1. **往復テスト**: ゲームロード→サーバー datastore に実ブロック設置→テスト用 `BlockGameObject` を作り View を `Initialize`→UI prefab/view を instantiate→対象行の `Button.onClick.Invoke()`（または View の選択メソッド）→**サーバー component の `SelectedIndex` 変化を検証**。OS入力クリックは使わない（脆い/過剰）。
2. **スモークテスト**: UI prefab が Addressables ロードでき、ルートに View component（`ISubInventoryView`）が付いていることを確認。E2E と UI 組立の失敗箇所を分離する。

## Codex 監査（session `019e9875-ef9e-7b83-8846-9b2cb15dbd64`）の反映

| 指摘 | 重大度 | 反映 |
|---|---|---|
| 楽観更新が毎Update の StateDetail 読みと競合し古い値に戻る | NO（要修正） | サーバー側状態 push を 2-A で実装（判断2）。楽観更新ハック撤廃。 |
| forUnitTest と EditModeInPlayingTest のデータは別物 | NO（要修正） | テストブロックは `EditModeInPlayingTestMod` に追加（実コードで `EditModeInPlayingTestServerDirectoryPath` を確認済み）。 |
| OS入力クリックは脆い/過剰 | NO（要修正） | 直接 View Initialize＋`Button.onClick.Invoke()`＋サーバー SelectedIndex 検証へ変更。スモークテスト分離。 |
| マスタ/StateDetail 分離 | YES | 採用。null ガード・未同期表示を追加。 |
| インベントリ無しUIの土台 | YES | FilterSplitter/GearEnergyTransformer を手本。ルートに View 付与（`SubInventoryState.cs:118` で確認）。 |
| prefab フル自動構築の落とし穴 | YES（注意） | 単一prefab＋行テンプレート方式。ToggleGroup＋SetIsOnWithoutNotify。既存グループに登録。 |
| API メソッド未追加 / キャンセル安全 / 未同期 log 連打回避 | YES | 反映（エラー処理節）。 |
