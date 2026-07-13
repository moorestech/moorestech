# 機械レシピ明示選択 設計書

## 概要

機械が投入物からレシピを自動判定する現在の方式を廃止し、設置済み機械ごとにプレイヤーがレシピを明示設定する方式へ変更する。未設定の機械は停止し、材料が揃っていても加工を開始しない。

レシピGUIDは機械内に1つだけ保持する。選択中レシピと実行中レシピを別状態として併存させず、加工中にレシピを変更する場合は旧加工をキャンセルしてから新レシピへ切り替える。

## ゴール

- 電力機械と歯車機械の双方で、設置済み機械ごとに任意のアンロック済み対応レシピを設定できる。
- 未設定状態では加工しない。
- 加工中のレシピ変更を、アイテムの複製・意図しない消失や中途半端な状態変更なしに処理する。
- 選択状態を保存し、BlockState経由で接続中クライアントへ同期する。
- 大規模UI移行を妨げない、薄いクライアント接続点だけを用意する。
- 同じ機械・同じ入力を持つ複数レシピをMachineRecipeGuidで選び分けられる。

## 非ゴール

- レシピ選択画面の最終レイアウト、演出、検索、並び替え。
- 自動判定モードの維持。
- ロック中または別機械用レシピの予約設定。
- 処理速度、出力抽選、モジュール効果の仕様変更。

## 確定したゲーム挙動

### レシピ状態

- 新規機械の選択GUIDは `Guid.Empty` とし、加工を開始しない。
- `Guid.Empty` のSet要求はレシピ解除として扱い、マスタ・アンロック検証の対象外とする。
- 選択可能なのは、対象機械のBlockGuidに対応し、かつアンロック済みの機械レシピだけとする。
- `MachineRecipeGuid` は全レシピで一意とし、マスタ検証で重複を拒否する。
- 同じGUIDの再設定は冪等操作とし、加工進捗やインベントリを変更しない。
- レシピ解除も変更操作として扱い、加工中なら通常のキャンセル条件を適用する。

### 加工ジョブ

- 機械が保持するレシピGUIDは選択GUIDの1つだけとする。
- 加工状態はレシピGUIDや `MachineRecipeMasterElement` を保持しない。
- 加工開始時に、総時間、残り時間、生成予定アイテム、生成予定液体、キャンセル時の返却対象アイテムをジョブスナップショットとして確定する。
- `IsRemain` が有効な入力アイテムは消費も返却もしない。
- 正常完了時はスナップショット済みの生成予定物を出力し、ジョブをクリアする。

### 加工中のレシピ変更

加工中の変更は次の順序で処理する。

1. 変更先レシピの存在、対象機械との対応、アンロック状態を検証する。
2. 返却対象アイテムを機械入力スロットへ仮想挿入する。
3. 機械へ入り切らない分を、操作プレイヤーのメインインベントリへ仮想挿入する。
4. 全アイテムを格納できなければ、インベントリ、加工状態、選択GUIDを一切変更せず要求を拒否する。
5. 全アイテムを格納できる場合だけ、仮想挿入と同じ結果を実インベントリへ反映する。
6. 旧ジョブの進捗と生成予定物を破棄し、選択GUIDを変更する。

消費済み液体は返却せず、キャンセル成功時に消失する。液体の格納可否は変更可否の判定に使用しない。

## アーキテクチャ

### 単一レシピ状態

新しい選択状態クラスや設定コンポーネントは作らない。既存 `MachineProcessContext` に `RecipeGuid` を1つだけ持たせ、Processor、Idle状態、保存処理が同じ値を参照する。

`VanillaMachineProcessorComponent.RecipeGuid` は「実行中レシピ」ではなく「選択中レシピ」を表す。加工完了後もクリアせず、プレイヤーが変更・解除するまで維持する。既存の `MachineBlockStateDetail.MachineRecipeGuid` も選択GUIDとして継続利用する。

### Processorと加工状態

`VanillaMachineProcessorComponent` はレシピ変更入口、レシピ存在・対象BlockGuid・アンロック状態の検証、加工中キャンセル、選択GUID更新、既存UniRx BlockState通知を担当する。結果enumはProcessor内へネストし、別ファイルを作らない。

`IdleMachineProcessState` は `MachineProcessContext.RecipeGuid` が未設定なら待機し、設定済みならGUIDからマスタを取得して材料量、出力容量、電力・モジュール条件を確認する。開始時に入力を消費してProcessingへ遷移する。

`ProcessingMachineProcessState` は既存フィールドを拡張し、進捗、生成予定アイテム、生成予定液体、返却対象アイテムだけを保持する。レシピGUIDや `MachineRecipeMasterElement` は保持しない。

返却専用クラスは作らない。`VanillaMachineInputInventory.TryRefundConsumedItems` が一時インベントリへの仮挿入、プレイヤーインベントリの容量確認、成功後の実挿入をまとめて行う。

### 保存

選択GUIDと加工状態は既存 `VanillaMachineProcessorSaveJsonObject` を `VanillaMachineSaveComponent.cs` 内へ移して保存する。別の保存コンポーネントは作らない。保存内容は次とする。

- 選択中レシピGUID。
- ProcessingかIdleか。
- 総加工時間と残り加工時間。
- 生成予定アイテム。
- 生成予定液体。
- キャンセル時の返却対象アイテム。

選択レシピはMachineRecipeGuid、返却対象アイテムはItemGuid、生成予定液体はFluidGuidの文字列でJSON保存し、ロード時に `MasterHolder` で実行時IDへ解決する。揮発するItemId・FluidId・BlockId、マスタ由来の容量・最大スタック数・スロット数は保存しない。加工専用の2つ目のレシピGUIDは保存しない。

### 通信

`Server.Protocol` にGet/Setを持つRequest-Response型プロトコルを追加する。既存通信から選択GUIDは導出できず、ユーザー操作をサーバーへ送る既存経路にも任意GUIDのpayloadがないため、新規プロトコルが必要である。

Set要求はブロック座標とレシピGUIDを受け取る。返却先プレイヤーは要求payloadのIDではなく `PacketResponseContext.PlayerId` から特定する。応答は成功可否、適用後GUID、失敗理由を返す。

失敗理由は最低限、未ハンドシェイク、ブロック不存在、対象が機械でない、レシピ不存在、別機械用、未アンロック、返却先容量不足を区別する。

選択変更成功時はProcessorの既存UniRx通知から `BlockSystem` のBlockState配信へ流す。新しいEvent型パケットは作らない。

### クライアントUI境界

クライアントは対象機械用かつアンロック済みのレシピだけを候補として渡し、Get/Setプロトコルと既存 `MachineBlockStateDetail` を利用する。今回のUIは選択、解除、失敗理由の通知に必要な最小限に留める。

最終UIへ移行しやすいよう、候補収集・プロトコル送信・応答反映を薄い選択Viewへ閉じ込め、Processor表示やインベントリ移動処理へ設定ロジックを混ぜない。Prefab変更が必要な場合はUnity Editor経由で行い、YAMLを直接編集しない。

## データフロー

### 待機中の設定

1. クライアントがSet要求を送る。
2. サーバーが接続プレイヤー、対象ブロック、レシピを検証する。
3. Processorが `MachineProcessContext.RecipeGuid` を更新する。
4. BlockState変更を通知し、応答で最新GUIDを返す。
5. 次の機械Updateで材料が揃っていれば加工を開始する。

### 加工中の設定

1. クライアントがSet要求を送る。
2. サーバーが変更先を検証する。
3. Processing状態が返却計画をコピー上で作る。
4. 返却不能なら容量不足応答を返し、旧加工を継続する。
5. 返却可能ならアイテム返却、旧ジョブ破棄、GUID更新を順に確定する。
6. 消費済み液体は復元しない。
7. BlockState変更を通知し、応答で新GUIDを返す。

## 配置と前例

| 項目 | 配置先 | 使用機構 | 前例・理由 |
|---|---|---|---|
| 選択GUID | 既存 `MachineProcessContext` | 単一Guidフィールド | 機械加工状態の既存共有コンテキストを拡張する |
| 選択変更・通知 | 既存 `VanillaMachineProcessorComponent` | 既存UniRx Subject | Processorが加工状態変更を既に通知している |
| 入力検証・返却 | 既存 `VanillaMachineInputInventory` | 一時インベントリによる仮挿入 | 入力スロットと挿入規則の所有者へ置く |
| 加工ジョブ | 既存 `ProcessingMachineProcessState` | 既存フィールドの拡張 | 新規ジョブ型を作らず加工状態に集約する |
| 選択StateDetail | 既存 `MachineBlockStateDetail` | MessagePack BlockStateDetail | 既存GUID項目の意味を選択中へ変更する |
| Get/Setプロトコル | `Server.Protocol/PacketResponse/MachineRecipe` | MessagePack Request-Response | `FilterSplitterStateProtocol.cs` の取得・設定・失敗理由付き応答に合わせる |
| プロトコル登録 | `Server.Protocol/PacketResponseCreator.cs` | 既存辞書登録 | 同ファイルの全Request-Response登録に合わせる |
| クライアント送信API | `Client.Network/API/MachineRecipe` と `VanillaApi.cs` | `UniTask` Request-Response | `VanillaApiEvent` 等と同じく、用途別APIを `VanillaApi` が束ねる構成 |
| 最小選択UI | `Client.Game/InGame/UI/Inventory/Block/RecipeSelection` | UniRx、既存UnlockState参照 | `FilterSplitterBlockInventoryView.cs` と同じサーバー設定Viewの責務 |

`Core.Master` は既存のレシピ取得APIを読み取り利用するだけとし、選択状態や機械固有ロジックを追加しない。自動生成された `Mooresmaster.Model.*` は変更しない。新しいマスタスキーマも追加しない。

従来の「BlockId＋投入ItemId＋投入FluidId」をキーとする逆引き辞書と重複拒否は、自動判定専用なので削除する。GUIDによるレシピ取得と通常のマスタ参照整合性検証は維持する。

### 予定ファイル配置

| 種別 | ファイル | 責務 |
|---|---|---|
| 新規 | `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe/MachineRecipeSelectionProtocol.cs` | Get/Set、MessagePack DTO、サーバー検証 |
| 新規 | `moorestech_client/Assets/Scripts/Client.Network/API/MachineRecipe/MachineRecipeSelectionApi.cs` | PacketExchangeManagerを使う選択Get/Set API |
| 新規 | `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/RecipeSelection/MachineRecipeSelectionView.cs` | 移行用の最小選択UI |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/MachineProcessContext.cs` | 唯一の選択RecipeGuidを保持 |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` | 選択変更、加工キャンセル、既存状態通知 |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs` | 自動逆引きから選択レシピ参照へ変更 |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/ProcessingMachineProcessState.cs` | レシピ保持を廃止し、既存フィールドへ予定物・返却対象を保持 |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs` | 入力検証、実消費スタック記録、二段階返却 |
| 削除 | `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs` | 入力検証を `VanillaMachineInputInventory` へ統合 |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block.Interface/State/MachineBlockStateDetail.cs` | 既存GUID項目を選択中レシピとして扱う |
| 変更 | `moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs` | 投入物キーの自動逆引き辞書・APIを除去し、GUID取得を維持 |
| 変更 | `moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs` | 自動逆引き辞書初期化と同一入力レシピ拒否を除去し、参照整合性・MachineRecipeGuid一意性を検証 |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineSaveComponent.cs` | 既存DTOへ選択GUID・予定液体・返却対象を保存 |
| 変更 | `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs` | 選択GUIDと加工状態を既存ロード経路で復元 |
| 変更 | `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs` | 新規プロトコル登録 |
| 変更 | `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs` | 用途別 `MachineRecipeSelectionApi` の公開 |
| 変更 | `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs` | 最小選択Viewとの接続 |
| 変更 | `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/MachineRecipeConfigTest.cs` | 自動逆引きテストを削除し、明示選択用入力検証テストへ置換 |
| 変更 | `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json` | 共有GUIDをレシピごとの一意GUIDへ修正し、同一入力の選択テスト用レシピを追加 |
| 変更 | `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestMachineRecipeId.cs` | テストが揮発順序に依存せず一意GUIDを参照する定数を追加 |
| Editor変更 | `MachineBlockInventory.prefab`, `GearMachineBlockInventory.prefab` | 最小選択UIの配置。Unity Editor経由でのみ変更 |

テストは既存テストディレクトリに追加するが、1ファイル200行以下・1ディレクトリ10コードファイル以下を守るため、機械レシピ選択専用サブディレクトリへ分割する。`partial` は使用しない。

## エラー処理と不変条件

- 返却の事前計算中は実インベントリを変更しない。
- 返却不能時は加工進捗、予定出力、選択GUID、機械入力、プレイヤーインベントリが要求前と同一である。
- 変更成功時に旧ジョブの生成予定物を出力しない。
- 同じGUIDの再設定で返却・キャンセル・進捗リセットを行わない。
- Set要求のpayloadにはプレイヤーIDを含めず、接続コンテキストだけを返却先の正とする。
- マスタGUIDの文字列が不正な要求は失敗応答にし、ドメイン処理へ渡さない。
- 存在が設計上保証される機械内部依存には不要なnullチェックを追加しない。

## テスト戦略

### サーバー単体・統合テスト

- 未設定機械は材料が揃っていても加工を開始しない。
- 設定済み対応レシピだけを加工する。
- 同じ機械・同じ入力の複数レシピをGUIDで選び分けられる。
- MachineRecipeGuid重複をマスタ検証で拒否する。
- 存在しない、別機械用、未アンロックのレシピを拒否する。
- 同じGUIDの再設定で加工進捗を維持する。
- 加工中変更で、返却物が機械入力だけに収まる。
- 加工中変更で、余りが操作プレイヤーのメインインベントリへ入る。
- 機械入力とプレイヤーを合わせても収まらない場合、全状態を維持する。
- 複数種類・複数スタック・最大スタック境界でも返却計画と実挿入結果が一致する。
- `IsRemain` 入力を返却対象に含めない。
- 加工中に入力が再充填されていても、返却可能性を現在スロットから正しく判定する。
- 液体併用レシピは、アイテムが返却可能なら変更でき、消費済み液体を復元しない。
- 変更成功時に旧ジョブのアイテム・液体出力を生成しない。
- 電力機械と歯車機械で同じ挙動になる。

### 保存・ロードテスト

- Idle状態の選択GUIDを復元する。
- Processing状態の進捗、予定出力、返却対象アイテムを復元する。
- ロード直後のレシピ変更で、復元した返却対象を正しく返す。
- 保存JSONに選択GUIDが1つだけ存在し、加工専用の別GUIDがない。

### 通信・クライアントテスト

- 要求payloadにプレイヤーIDを持たず、接続コンテキストのプレイヤーへ返却する。
- 成功応答と各失敗理由が正しく返る。
- 選択変更がBlockState通知として反映される。
- 最小UIから選択・解除でき、拒否時はサーバー適用GUIDへ表示を戻す。

### QA重点項目

最初から不具合がある前提で、満杯直前のスタック、複数素材、同一素材の複数入力定義、加工開始直後・完了直前、入力再充填、液体併用、保存直後・ロード直後、2クライアントからの連続変更を重点的に確認する。

`.cs` 変更後は `uloop compile --project-path ./moorestech_client` を必ず実行し、対象テストをregex filterで限定実行する。ドメインリロード中は45秒待って再試行する。

## 受け入れ条件

- 未設定機械が自動判定で加工を始めない。
- プレイヤーが任意のアンロック済み対応レシピを設定・解除できる。
- 機械内にレシピGUIDが1つだけ存在する。
- 加工中変更時、アイテム全量返却可能なら旧加工を中止して新レシピへ切り替わる。
- アイテム全量返却不能なら変更が拒否され、旧加工がそのまま継続する。
- 消費済み液体は変更可否に影響せず、成功時に消失する。
- 選択状態と加工ジョブスナップショットが保存され、選択変更が同期される。
- 電力機械・歯車機械の回帰テスト、保存ロードテスト、通信テスト、コンパイルが通る。
