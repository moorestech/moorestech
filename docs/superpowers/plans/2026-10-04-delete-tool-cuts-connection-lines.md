# 削除ツールによる接続線の切断と撤去物Undo 実装計画

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 電線・歯車チェーン（接続線）を削除ツールで切断できるようにし、削除ツールで撤去・切断したもの（ブロック・接続線・レール。ブロック撤去に巻き込まれた線・レールを含む）を Ctrl+Z で撤去前と同じ姿へ戻せるようにする。

**Architecture:** サーバーは接続1本ごとに「引いた種類（ConnectToolGuid）」を保存・同期し（セーブv4・移行ステップ付き）、歯車チェーン切断・橋脚/駅撤去時のレール返却・自動接続を止める設置指定・座標同定のレール接続を備える。クライアントは電線/チェーン共通の削除対象（ConnectionLineDeleteTarget）を接続線レイヤーに置き、照準を「最前面＋ドラッグ中は同カテゴリーの最前面」で解決する。Undo は撤去物を `IRemovedObject` の同じ抽象で記録し、ブロック相（自動接続なしで一括再設置）→線相（同じ種類で引き直し）の順に送る（サーバーはFIFO単一スレッド処理なので送信順＝適用順）。

**Tech Stack:** Unity (C#), MessagePack, Newtonsoft JSON, UniRx/UniTask, NUnit (uloop run-tests), webui (TypeScript, vitest), unity-playmode-recorded-playtest

## Requirements

設計ADR: `docs/adr/0076-delete-tool-cuts-connection-lines-and-undo-restores-removed-objects.md`／裁定: `.decisions/2026-10-04-*.md`

- R1 削除ツールで電線と歯車チェーンを1本単位で切断できる。受入: 削除ツールで線をホバーすると赤くなり、クリック（ドラッグ）で切断され素材が返る（チェーン含む）。
- R2 接続線はブロックと別の破壊カテゴリー（電線・チェーンは同じ「接続線」カテゴリー）。受入: 線から始めたドラッグはブロックを拾わず、ブロックをなぞると「異なるカテゴリー」の拒否理由。逆も同様。レールはブロックと同じカテゴリーのまま。
- R3 照準はホバー・ドラッグ開始で最前面、ドラッグ中は固定カテゴリーの対象のうち最前面。受入: ブロック選択ドラッグ中に手前を電線が横切っても奥のブロックが選ばれる（純関数のテストで固定）。
- R4 電線ツールの起点未選択クリック切断を廃止する。受入: 電線ツールで線をクリックしても切断されない（空間クリック扱い）。中クリックのスポイトは線の種類を拾う。
- R5 電線・チェーンの各接続は引いた種類を保存・同期する。受入: セーブ→ロードで種類が保たれ、クライアントの状態詳細に partner＋種類が届く。
- R6 セーブ版を3→4へ上げ、旧版の電線は全て「電線」(872372d5-2998-4fb7-826c-593ceeafcfb2)、チェーンは全て「歯車チェーン」(6c1dab62-be1e-45bc-abe7-69ec7d109b88) の種類として移行する。素材が空・未知でも失敗しない。受入: v3セーブがv4へ変換されロードできるテスト。
- R7 歯車チェーンの切断を返却付きで復活し、返却が入らなければ拒否して通知する。受入: 切断で素材が返る／満杯なら拒否と `denied.gearChainDisconnect.InventoryFull` 通知。
- R8 橋脚・駅など線を持つブロックの撤去で、付いていたレールの素材を返却する（物理1本を1回、`Guid.Empty`区間は返却なし）。入りきらなければ撤去を `InventoryFull` で拒否し、クライアントは `ui.delete.inventoryFull` を表示。受入: サーバーテスト。
- R9 ブロックのホバー／選択時、一緒に消える電線・チェーン・レールも赤表示する（選択には入れない）。受入: 電柱ホバーで付随線が赤くなり、外すと戻る。
- R10 削除ツールのUndoは撤去物を同じ抽象で記録し、Ctrl+Zでブロック再設置→線の引き直しを行う。直接切った線・レール、ブロック撤去に巻き込まれた線・レールを含む。重複は除く。受入: 電線を切って Ctrl+Z で同じ種類の線が戻る／電柱を消して Ctrl+Z で電柱と線が戻る／橋脚を消して Ctrl+Z でレールが同じ種類で戻る。
- R11 Undoの再設置では電線自動接続を発動しない（記録した線だけ引く）。受入: 再設置した電柱の範囲内に別の電気ブロックがあっても線が増えない。
- R12 Undoの線の引き直しは通常の接続経路で素材を再消費する。失敗（素材不足・端点不在・未解放・上限）はサーバー通知に、クライアント側で戻せなかった分（占有済み・記録不能）は件数通知 `denied.undoRestoreSkipped` と `Debug.LogWarning` に出して残りを続ける。既に接続済みの線は何もしない。`Guid.Empty`のレール区間は記録しない。
- スコープ外: 列車車両撤去のUndo復元、駅隣接のレール自動接続の抑止、電線ツール・チェーンツールへの切断操作追加。

## Global Constraints

- AGENTS.md 全規約: 1ファイル200行未満（超える既存ファイルを触るときは分割）、1ディレクトリ10ファイルまで、partial禁止、`Func<>`禁止、デフォルト引数禁止（呼び出し側を全更新）、try-catch禁止（外部境界のみ）、イベントはUniRx、日英2行コメント、`#region Internal`はメソッド内ローカル関数のみ、`.meta`手動作成禁止、Prefab/Scene/ProjectSettingsのテキスト直接編集禁止（`uloop execute-dynamic-code`経由は可）。
- fail-closed の拒否・縮退経路は理由を開発者が読めるログ（またはプレイヤー通知）へ必ず出す。
- セーブ形式変更は `WorldSaveAllInfo.CurrentVersion` を 3→4、`SaveMigrationStepV3ToV4` を `MoorestechServerDIContainerGenerator` の `new SaveMigrationChain(...)` へ登録、旧→新変換テストを同梱（moorestech-save-migration スキルが正本）。移行ステップは `MasterHolder` を引かない。
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client` を通す。ドメインリロード中エラーは45秒待って再試行。`Localization/localization.csv` 変更後は `--force-recompile true --wait-for-domain-reload true`。
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/delete-tool-cuts-connection-lines`（branch `feat/delete-tool-cuts-connection-lines`）。bd: `moorestech-xsd18`。

## ユーザー操作を失敗させる経路

| 経路 | 通常運用で起きる時機・起こす者 | その間ユーザーに見えるもの | 操作なしで解消するか | 扱い |
|---|---|---|---|---|
| 撤去の返却が入らず撤去拒否（`InventoryFull`、レール返却で頻度増） | インベントリ満杯のプレイヤーが電柱・橋脚・駅を撤去 | カーソルツールチップ `ui.delete.inventoryFull` | しない（空きを作れば通る） | 既存の電線返却と同じ fail-closed。理由表示を新設（P9） |
| チェーン・電線切断の返却が入らず拒否 | 同上で接続線を切断 | `denied.gearChainDisconnect.InventoryFull`／既存電線通知 | しない（同上） | 既存の電線切断と同じ |
| Undoの線・ブロックの一部が戻せない（素材不足・未解放・上限・占有・記録不能） | Ctrl+Z 時のインベントリ・世界状態による | サーバー側の拒否はサーバー通知、クライアント側のスキップ（占有・記録不能）は件数通知 `denied.undoRestoreSkipped`＋開発者ログ `[RemovalRestore]` | しない（履歴は消費済み・手で引き直す） | ユーザー裁定 2026-10-05「できた分だけ戻し残りは通知」 |
| 移行ステップが構造の壊れたJSONで `Failed` | 手で壊したセーブ等の異常時のみ | ロード中断（原本は backup/3） | しない | agent判断 P5（通常運用では起きない） |

## 配置と前例

| 項目 | 配置先 | 前例 |
|---|---|---|
| `ConnectionLineRecord`（旧 `ElectricWireConnectionCost`・`GearChainConnectionCost` を1型へ統合＋種類） | Game.Block.Interface.Component（旧チェーン型と同じ場所。`Game.EnergySystem.asmdef` は `Game.Block.Interface` を参照済み） | 旧 `ElectricWireConnectionCost.cs` / `GearChainConnectionCost.cs`、レールの `RailSegment.RailTypeGuid` |
| `ConnectionLinePartnerMessagePack` | Game.Block/Blocks/ConnectionLine | `ElectricWireStateDetail.cs`（状態詳細 MessagePack は Game.Block） |
| `SaveMigrationStepV3ToV4` | Game.SaveLoad/Migration/Steps | `SaveMigrationStepV2ToV3.cs` |
| `GearChainDisconnectFailureReason` / `GearChainPlacementFailureReason` | Server.Protocol/PacketResponse/Util/GearChain | `ElectricWirePlacementFailureReason` |
| `RailRemovalRefundCalculator` | Server.Protocol/PacketResponse/Util/RailEdit | `RailConnectionEditService.cs`（切断返却の算出） |
| `BlockPlacementWiring` | `PlacePacketDto.cs`（既存DTOファイルへ追記） | `PlaceInfoMessagePack` |
| `RailConnectByDestinationProtocol` | Server.Protocol/PacketResponse（IPacketResponse） | `RailConnectionEditProtocol.cs`、登録は `PacketResponseCreator` |
| `ConnectionLineDeleteTarget` / `ConnectionLineRegistry` | Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine | `DeleteTargetRail.cs`（線状の削除対象は表示要素側に付く） |
| `DeleteTargetHitSelector` / `DeleteTargetRaycaster` | Client.Game/InGame/UI/UIState/State/DragDelete | `BlockClickDetectUtil.TryGetFrontmostSolidHit` |
| `IRemovedObject` 系・`IRemovalRestoreSender` | Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal | `RemoveOperationRecord.cs` / `IBuildOperationRecord.cs` |
| `BlockAttachedConnectionResolver` | DragDelete、`ClientDIContext` static で参照 | `ClientDIContext.BlockGameObjectDataStore` の static 参照（`ElectricWireLineViewElement.cs`） |
| `ConnectionLineConnectionJsonObject` / `ConnectionLineRefundItems` | Game.Block/Blocks/ConnectionLine | 旧 `ElectricWireConnectionJsonObject`・`GearChainPoleConnectionJsonObject`、`ElectricWireConnectorComponent.GetRefundItems` |
| `IGearChainConnectionLookup` / `IGearChainConnectionMutation` | Game.Block/Blocks/GearChainPole | 層マップ「可変DataStoreのアクセス面」（`IItemStackLevelLookup`/`IItemStackLevelUnlocker`） |
| `RailSegmentPairing` | Game.Train/RailGraph/Utility（サーバー返却とクライアント描画IDの共有正本） | クライアント `TrainRailObjectManager.SelectCanonicalPair`（ここから移設） |
| `ConnectionLineDestructionCategory` | `Core.Master.BlockMaster`（既定カテゴリー定数の隣）＋クライアント再公開 | `BlockMaster.DefaultDestructionCategory` / `BlockMasterElementExtension.DefaultDestructionCategory` |
| `BlockDestructionCategoryValidator` | Core.Master/Validator/Block | `ExtractionSettingsValidator.cs`（BlockMasterUtil から切り出した検証） |
| `RemovePreviewRequests` / `IRemovePreviewable` / `RailChainRemovePreview` | UIState/State（共有）、Train/RailGraph（レール側部品） | 新規（赤プレビューの要求者集合。既存は単一書き手前提の `SetRemovePreviewing/ResetMaterial` のみ） |
| `DeleteAimFilter` / `DeleteAimResult` | UIState/State/DragDelete | 結果型の前例 `SaveMigrationStepResult`（成功/失敗の factory） |
| `ClientLocalNotificationSource` | Client.Game/InGame/UI/Notification（`NotificationTopic` が購読） | **新規パターン**（クライアント発の通知の前例が無い。イベントは UniRx `Subject` 公開の標準形） |

データフロー: 入力（削除ツール）→ `DeleteObjectService`（書き手: 選択モデル `DragDeleteSelection`）→ 確定で各 `IDeleteTarget.Delete()` 送信＋ `RemoveOperationRecord` を履歴へ → サーバー状態変化 → 既存の状態詳細イベント → 表示（`ConnectionLineViewBase`）。Undo は履歴の読み手として送信だけを行う。既存フローへの交差点（下流への直接セッター・bool戻りの制御）は足していない。

## 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 電線ツール: 延長・孤立設置・起点選択 | 生きる | `ElectricWireEditMode` は切断経路だけ除去（Task 13） |
| 電線ツール: 線クリック切断 | 廃止 | ユーザー裁定（ADR 0076） |
| 中クリックのスポイト（線） | 生きる・線の種類を拾う | Task 13 |
| 削除ツール: ブロック・レール・列車の単体／ドラッグ削除、ESC/右短押しキャンセル、モード遷移キー | 生きる | `DeleteObjectService` の入力処理は不変、対象解決だけ差し替え（Task 10） |
| 設置の Ctrl+Z（PlaceOperationRecord） | 生きる | 引数型を `IBlockOccupancyQuery` へ（Task 11） |
| 設置プレビューのレイキャスト（Without_* マスク） | 生きる | 接続線レイヤー除外を維持（Task 8） |
| カーソルのツールチップ | 生きる（線の奥も遮らない） | Task 8 Step 3b |
| チェーンツールの接続 | 生きる・失敗が通知されるようになる | Task 4 |
| 通常設置の電線自動接続 | 生きる | AutoConnect が既定、Undoだけ NoAutoConnect（Task 6） |

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / チェーン当たり判定がツールチップのレイを遮る退化を Task 8 Step 3b で除外。配置は全て前例どおり
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱7・第3バケツ4 / 強4件全て裁定（3件反映・1件はADR文言修正）、弱4件反映・3件据え置き

---

## タスク順と依存

Task 1→2→3（サーバー記録・移行・同期）→4（チェーン切断）→5→6→7（レール返却・設置指定・座標接続）→8→9→10→11→12→13（クライアント）→14（録画テスト）→15→16（閉じタスク）。各タスクの見出し括弧内の A/B/C 番号は本文中の相互参照名。


## A群の前提事実

前提パス: `S = moorestech_server/Assets/Scripts`, `C = moorestech_client/Assets/Scripts`（Files節は展開済みのリポジトリ相対パスで書く）。
全タスク共通: 新規 `.cs` を足した直後はサーバー側 file: パッケージのため `uloop launch ./moorestech_client --restart` が要る（moorestech-save-migration Gotchas）。`.meta` は手で作らない。

---

### Task 1 (A1): 電線・チェーン共通の接続記録型（ConnectionLineRecord）へ統合し「引いた種類」を保持・セーブする

**Files:**
- Delete: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireConnectionCost.cs`、`moorestech_server/Assets/Scripts/Game.Block.Interface/Component/GearChainConnectionCost.cs`（`git rm`。各 `.meta` も `git rm`）
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ConnectionLineRecord.cs`（電線・チェーン共通の接続1本の記録。`Game.EnergySystem.asmdef` は `Game.Block.Interface` を参照済みなので電線側から使える）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLineConnectionJsonObject.cs`（電線・チェーンのセーブ接続要素を1つの型へ。JSONキーは従来どおり）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLineRefundItems.cs`（記録の素材を返却スタックへ展開する正本。電線コンポーネント・チェーン台帳・`ConnectToolMaterialConsumer` が呼ぶ）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/IGearChainConnectionLookup.cs`、`moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/IGearChainConnectionMutation.cs`（接続台帳の読み取り面／変更面。裁定 `.decisions/2026-10-05-GearChainConnectionSetは読み取りと変更のinterfaceに分ける.md`）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ConnectTool/ConnectToolMaterialConsumer.cs:43-54`（`CreateRefundItems` を正本へ委譲し、返却可否込みの `TryCreateFittingRefund` を追加）
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/IElectricWireConnector.cs:20-24`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IGearChainPole.cs:9-10`（＋読み取りAPI 1本追加）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire/ElectricWireConnectorComponent.cs:27-28,58-86,104-110,131-137`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire/ElectricWireSaveDataJsonObject.cs`（全体）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainConnectionSet.cs`（253行の `GearChainPoleComponent.cs` を200行未満へ戻すため接続台帳を切り出す）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainPoleComponent.cs:34,56-61,63-102,113-131,138-165,172-186,224-252`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainPoleSaveDataJsonObject.cs`（全体）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/Placement/ElectricWirePlacementEvaluator.cs:47-56`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/Placement/ElectricWirePlacementJudgement.cs:13-25`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectPlan.cs:16-39`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectService.cs:37,46,58,78-91,142-144`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireExtendService.cs:137`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/Connection/ElectricWireSystemUtil.cs:86-96,136-174,178`（202行→ `TryDisconnect` を切り出して200行未満へ）
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/Connection/ElectricWireDisconnectUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireDisconnectProtocol.cs:31`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainPlacementEvaluator.cs:61,72-84`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainSystemUtil.cs:57-63`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWireExtendPreviewCalculator.cs:116-117`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ElectricWireAutoConnectToolSelector.cs:80`
- Modify (tests): `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/FakeWireConnector.cs:19,35,64,89-104,115-155`、`moorestech_server/Assets/Scripts/Tests/Util/EnergySystem/ElectricWireTestUtil.cs:27`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/GearChainPoleSaveLoadTest.cs:39`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ElectricWireRemovalTest.cs:74-75`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWireSaveLoadTest.cs:75,121-122,128`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricWireSystemUtilTest.cs`（`ElectricWireSystemUtil.TryDisconnect` 2箇所）、`moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricWirePlacementEvaluatorTest.cs:94-95,125,129-131`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainPlacementEvaluatorTest.cs:115-116`
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionRecordSaveLoadTest.cs`

改名で触る全参照（`grep -rn "ElectricWireConnectionCost\|GearChainConnectionCost" --include='*.cs'` の結果。Library配下のスナップショットは除外）:
- 電線: `Game.EnergySystem/ElectricWire/ElectricWireConnectionCost.cs`、`Game.EnergySystem/ElectricWire/IElectricWireConnector.cs`、`Game.Block/Blocks/ElectricWire/ElectricWireSaveDataJsonObject.cs`、`Game.Block/Blocks/ElectricWire/ElectricWireConnectorComponent.cs`、`Server.Protocol/PacketResponse/Util/ElectricWire/Connection/ElectricWireSystemUtil.cs`、`.../AutoConnect/ElectricWireAutoConnectService.cs`、`.../AutoConnect/ElectricWireAutoConnectPlan.cs`、`.../Placement/ElectricWirePlacementEvaluator.cs`、`.../Placement/ElectricWirePlacementJudgement.cs`、`Tests/UnitTest/Game/FakeWireConnector.cs`、`Tests/Util/EnergySystem/ElectricWireTestUtil.cs`
- チェーン: `Game.Block.Interface/Component/GearChainConnectionCost.cs`、`Game.Block.Interface/Component/IGearChainPole.cs`、`Game.Block/Blocks/GearChainPole/GearChainPoleSaveDataJsonObject.cs`、`Game.Block/Blocks/GearChainPole/GearChainPoleComponent.cs`、`Server.Protocol/PacketResponse/Util/GearChain/GearChainPlacementEvaluator.cs`、`Tests/UnitTest/Game/SaveLoad/GearChainPoleSaveLoadTest.cs`
- 付随の改名（型名に連動するメンバー）: `ElectricWirePlacementJudgement.WireCost`→`WireRecord`、`GearChainPlacementJudgement.ChainCost`→`ChainRecord`、`ElectricWirePlacementEvaluator.TryCalculateWireCost`→`TryCreateWireRecord`（呼び出し: `ElectricWireExtendService.cs:137`、`ElectricWireAutoConnectService.cs:85`、クライアント `ElectricWireExtendPreviewCalculator.cs:117`、`ElectricWireAutoConnectToolSelector.cs:80`、テスト `ElectricWirePlacementEvaluatorTest.cs:131`）、`WireConnections` 値タプルの要素名 `Cost`→`Record`
- コンパイル対象外だが古い参照: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-v2.cs:61`（既に存在しない ctor `(ItemId,int)` を使う陳腐化シナリオ。本タスクでは `new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>())` へ書き換える）

**Interfaces:**
- Consumes: なし（最初のタスク）
- Produces:
  - `Game.Block.Interface.Component.ConnectionLineRecord`（readonly struct。電線・チェーン共通）: `ConnectionLineRecord(Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)`、`Guid ConnectToolGuid`、`IReadOnlyList<ConnectToolMaterialCost> Materials`、`int TotalCount`。旧 `ElectricWireConnectionCost.Empty` は廃止
  - `Game.Block.Blocks.ConnectionLine.ConnectionLineConnectionJsonObject`: `int TargetBlockInstanceId`、`Guid ConnectToolGuid`（`Required.Always`）、`List<ConnectToolMaterialSaveJsonObject> Materials`、ctor `(int targetBlockInstanceId, ConnectionLineRecord record)`、`ConnectionLineRecord ToConnectionRecord()`
  - `Game.Block.Blocks.ConnectionLine.ConnectionLineRefundItems.Create(IReadOnlyList<ConnectToolMaterialCost> materials) : List<IItemStack>`
  - `ConnectToolMaterialConsumer.TryCreateFittingRefund(IReadOnlyList<ConnectToolMaterialCost> materials, IOpenableInventory inventory, out List<IItemStack> refundStacks) : bool`（返却が入らなければfalse。電線・チェーンの切断が共有）
  - `IGearChainConnectionLookup`（`Count`、`PartnerIds`、`Targets`、`Contains`、`TryGetRecord`、`CreateRefundItems()`、`CreateSaveData()`）／`IGearChainConnectionMutation`（`Add`、`TryRemove`、`Clear`）。`GearChainConnectionSet` が両方を実装し、`GearChainPoleComponent` は読み取りを `_chainLookup`、変更を `_chainMutation` 経由に分ける
  - `IElectricWireConnector.WireConnections : IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)>`、`TryAddWireConnection(BlockInstanceId, ConnectionLineRecord)`、`TryRemoveWireConnection(BlockInstanceId, out ConnectionLineRecord)`
  - `IGearChainPole.TryAddChainConnection(BlockInstanceId, ConnectionLineRecord)`、`TryRemoveChainConnection(BlockInstanceId, out ConnectionLineRecord)`、**新設** `bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out ConnectionLineRecord record)`（A4 の返却前検査が使う）
  - `ElectricWireDisconnectUtil.TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out ElectricWirePlacementFailureReason failureReason)`（旧 `ElectricWireSystemUtil.TryDisconnect` の移設。挙動不変）
  - `GearChainConnectionSet : IGearChainConnectionLookup, IGearChainConnectionMutation`（Game.Block 内部用）
  - セーブ JSON: 電線 `state.ElectricWireConnectorComponent.connections[]` とチェーン `state.GearChainPoleComponent.connections[]` の各要素に `"connectToolGuid": "<Guid>"`（必須。欠けた状態値の読み込みは `JsonSerializationException` で落ちる＝A2 のマイグレーションが必ず埋める）

- [ ] **Step 1: テストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionRecordSaveLoadTest.cs`:

```csharp
using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.ElectricWire
{
    // 電線・チェーンの接続記録が「引いた種類」をセーブ往復で保つことを検証する
    // Verify wire and chain connection records keep the connect tool they were drawn with across save/load
    public class ConnectionRecordSaveLoadTest
    {
        private const int PlayerId = 1;
        private static readonly Guid WireToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid ChainItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 電線の種類がセーブロードで復元される()
        {
            var saveJson = BuildSave(out var posPole, out var posGenerator, out _, out _);

            // 別ワールドへロードし、電線記録の種類が保存前と一致することを確かめる
            // Load into a fresh world and check the wire record keeps the same connect tool
            var loaded = LoadInto(saveJson);
            var pole = ServerContext.WorldBlockDatastore.GetBlock(posPole).GetComponent<IElectricWireConnector>();
            var generator = ServerContext.WorldBlockDatastore.GetBlock(posGenerator).GetComponent<IElectricWireConnector>();
            Assert.AreEqual(WireToolGuid, pole.WireConnections[generator.BlockInstanceId].Record.ConnectToolGuid);
            Assert.AreEqual(WireToolGuid, generator.WireConnections[pole.BlockInstanceId].Record.ConnectToolGuid);
            Assert.IsNotNull(loaded);
        }

        [Test]
        public void チェーンの種類がセーブロードで復元される()
        {
            var saveJson = BuildSave(out _, out _, out var posChainA, out var posChainB);

            // 別ワールドへロードし、チェーン記録の種類が保存前と一致することを確かめる
            // Load into a fresh world and check the chain record keeps the same connect tool
            LoadInto(saveJson);
            var poleA = ServerContext.WorldBlockDatastore.GetBlock(posChainA).GetComponent<IGearChainPole>();
            var poleB = ServerContext.WorldBlockDatastore.GetBlock(posChainB).GetComponent<IGearChainPole>();
            Assert.IsTrue(poleA.TryGetChainConnectionRecord(poleB.BlockInstanceId, out var record));
            Assert.AreEqual(ChainToolGuid, record.ConnectToolGuid);
            Assert.AreEqual(10, record.Materials.Sum(m => m.Count));
        }

        [Test]
        public void 種類を持たない接続のセーブ状態は読み込みで例外になる()
        {
            var save = JObject.Parse(BuildSave(out _, out _, out _, out _));

            // 種類キーを消した旧形の接続は、無音で空Guidへ縮退させず読み込みで落ちる
            // An old-shape connection without the tool key must fail on load instead of silently degrading to an empty Guid
            foreach (var block in (JArray)save["world"])
            {
                if (block["state"]?["ElectricWireConnectorComponent"]?["connections"] is not JArray connections) continue;
                foreach (var connection in connections) ((JObject)connection).Remove("connectToolGuid");
            }

            Assert.Throws<JsonSerializationException>(() => LoadInto(save.ToString()));
        }

        private static string BuildSave(out Vector3Int posPole, out Vector3Int posGenerator, out Vector3Int posChainA, out Vector3Int posChainB)
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlock = provider.GetService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireToolGuid);
            unlock.UnlockConnectTool(ChainToolGuid);

            // 電線は電柱-発電機（距離2）、チェーンはポール2本（距離2）を本番経路で接続する
            // Connect pole-generator (distance 2) by wire and two poles (distance 2) by chain through the production paths
            posPole = new Vector3Int(0, 0, 0);
            posGenerator = new Vector3Int(2, 0, 0);
            posChainA = new Vector3Int(10, 0, 0);
            posChainB = new Vector3Int(12, 0, 0);
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posPole, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GeneratorId, posGenerator, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var inventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(WireItemGuid), 10));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ChainItemGuid), 10));
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posPole, posGenerator, PlayerId, WireToolGuid, out var wireError), wireError.ToString());
            Assert.IsTrue(GearChainSystemUtil.TryConnect(posChainA, posChainB, PlayerId, ChainToolGuid, out var chainError), chainError);

            return provider.GetService<AssembleSaveJsonText>().AssembleSaveJson();
        }

        private static ServiceProvider LoadInto(string saveJson)
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)provider.GetService<IWorldSaveDataLoader>()).Load(saveJson);
            return provider;
        }
    }
}
```

`ElectricWireSaveLoadTest.cs` の既存比較（L75・L121-122・L128）は `.Cost` → `.Record` に置換し、L122 の直後に `Assert.AreEqual(savedRecord.ConnectToolGuid, loadedRecord.ConnectToolGuid);` を足す（変数名も `savedRecord`/`loadedRecord` へ）。L128 の `ElectricWireSystemUtil.TryDisconnect` は `ElectricWireDisconnectUtil.TryDisconnect` へ。

- [ ] **Step 2: 実装を書く**

`moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ConnectionLineRecord.cs`（新規。旧 `ElectricWireConnectionCost.cs`・`GearChainConnectionCost.cs` は `git rm`）:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// 接続線（電線・歯車チェーン）1本の記録。引いた接続ツールの種類と払った素材を持ち、返却とUndoの引き直しに使う
    /// Record of one connection line (wire or gear chain): the connect tool it was drawn with and the paid materials, used for refunds and undo re-drawing
    /// </summary>
    public readonly struct ConnectionLineRecord
    {
        public readonly Guid ConnectToolGuid;
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public ConnectionLineRecord(Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            ConnectToolGuid = connectToolGuid;
            Materials = materials;
        }

        // プレビュー表示用の総素材数。全素材の消費数を合算する
        // Total material count for preview display; sums consumption across all materials
        public int TotalCount
        {
            get
            {
                if (Materials == null) return 0;
                var total = 0;
                foreach (var material in Materials) total += material.Count;
                return total;
            }
        }
    }
}
```

電線側（`Game.EnergySystem`）の `IElectricWireConnector.cs`・`ElectricWirePlacementJudgement.cs` 等、旧 `ElectricWireConnectionCost` を使っていたファイルには `using Game.Block.Interface.Component;` を足す。

`moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLineRefundItems.cs`（新規。返却スタック展開の正本）:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// 接続線の素材を返却スタックへ展開する。電線・チェーンの撤去返却と切断返却が同じ規則を使う
    /// Expand connection-line materials into refund stacks; removal and disconnect refunds share this one rule
    /// </summary>
    public static class ConnectionLineRefundItems
    {
        public static List<IItemStack> Create(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            var result = new List<IItemStack>();
            if (materials == null) return result;
            foreach (var material in materials)
            {
                // 数0と空アイテムは返さない
                // Skip zero counts and the empty item
                if (material.Count <= 0 || material.ItemId == ItemMaster.EmptyItemId) continue;
                result.Add(ServerContext.ItemStackFactory.Create(material.ItemId, material.Count));
            }
            return result;
        }
    }
}
```

`ConnectToolMaterialConsumer.cs` L43-54 の `CreateRefundItems` を置換し、返却可否込みの準備を足す（`using Core.Inventory;`・`using Game.Block.Blocks.ConnectionLine;` を追加）:

```csharp
        // 返却用のアイテムスタック列を生成する（展開規則は接続線の正本に委ねる）
        // Create refund item stacks for the given materials (expansion rule delegated to the connection-line definition)
        public static List<IItemStack> CreateRefundItems(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            return ConnectionLineRefundItems.Create(materials);
        }

        // 返却スタックを作り、インベントリへ入りきるかを返す。入らなければ切断させない（返却消滅の防止）
        // Build refund stacks and report whether they fit; callers refuse the disconnect otherwise (prevents item loss)
        public static bool TryCreateFittingRefund(IReadOnlyList<ConnectToolMaterialCost> materials, IOpenableInventory inventory, out List<IItemStack> refundStacks)
        {
            refundStacks = CreateRefundItems(materials);
            return refundStacks.Count == 0 || inventory.InsertionCheck(refundStacks);
        }
```

`IElectricWireConnector.cs` L20-24 を置換:

```csharp
        IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> WireConnections { get; }

        bool ContainsWireConnection(BlockInstanceId partnerId);
        bool TryAddWireConnection(BlockInstanceId partnerId, ConnectionLineRecord record);
        bool TryRemoveWireConnection(BlockInstanceId partnerId, out ConnectionLineRecord record);
```

`IGearChainPole.cs` L9-10 を置換:

```csharp
        bool TryAddChainConnection(BlockInstanceId partnerId, ConnectionLineRecord connectionRecord);
        bool TryRemoveChainConnection(BlockInstanceId partnerId, out ConnectionLineRecord record);

        // 切断前の返却検査用に、指定相手との接続記録を読む（無ければfalse）
        // Read the record of the connection to the given partner for the pre-disconnect refund check (false when absent)
        bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out ConnectionLineRecord record);
```

`ElectricWireConnectorComponent.cs` の置換メンバー（他は無変更）:

```csharp
        private readonly Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> _wireConnections = new();
        public IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> WireConnections => _wireConnections;
```
```csharp
        public bool TryAddWireConnection(BlockInstanceId partnerId, ConnectionLineRecord connectionRecord)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_wireConnections.ContainsKey(partnerId)) return false;
            if (_maxWireConnectionCount <= _wireConnections.Count) return false;
            var connector = ResolveWireTarget(partnerId);
            if (connector == null) return false;
            _wireConnections.Add(partnerId, (connector, connectionRecord));
            // 接続集合の変更点自身でdirty化し、呼び出し元の再構築漏れを構造的に防ぐ
            // Mark dirty at the mutation itself so no caller can ever forget the rebuild
            ServerContext.GetService<IElectricWireNetworkMutation>().MarkTopologyDirty();
            // 状態変更を通知する
            // Notify state change
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryRemoveWireConnection(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            if (!_wireConnections.Remove(partnerId, out var connection))
            {
                record = default;
                return false;
            }
            record = connection.Record;
            ServerContext.GetService<IElectricWireNetworkMutation>().MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }
```
`GetRefundItems` を置換（展開規則は接続線の正本へ。`using Game.Block.Blocks.ConnectionLine;` を追加し、不要になった `Core.Master` 等の using は削除）:

```csharp
        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            // 接続ごとに払った素材を返却スタックへ展開する
            // Expand each connection's paid materials into refund stacks
            var refundItems = new List<IItemStack>();
            foreach (var connection in _wireConnections.Values) refundItems.AddRange(ConnectionLineRefundItems.Create(connection.Record.Materials));
            return refundItems;
        }
```
`OnPostBlockLoad` 内 `var cost = connection.ToConnectionCost(); _wireConnections.Add(targetId, (connector, cost));` → `var record = connection.ToConnectionRecord(); _wireConnections.Add(targetId, (connector, record));`。

`ElectricWireSaveDataJsonObject.cs` 全体（接続要素は電線・チェーン共通の `ConnectionLineConnectionJsonObject`。JSONキーは従来どおりで、`connectToolGuid` だけが増える）:

```csharp
using System.Collections.Generic;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ElectricWire
{
    public class ElectricWireSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<ConnectionLineConnectionJsonObject> Connections { get; set; }

        public ElectricWireSaveDataJsonObject(Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> wireConnections)
        {
            // 接続をリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<ConnectionLineConnectionJsonObject>();
            foreach (var target in wireConnections)
            {
                Connections.Add(new ConnectionLineConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Record));
            }

            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            Connections.Sort((left, right) => left.TargetBlockInstanceId.CompareTo(right.TargetBlockInstanceId));
        }

        public ElectricWireSaveDataJsonObject() { Connections = new List<ConnectionLineConnectionJsonObject>(); }
    }
}
```

`GearChainPoleSaveDataJsonObject.cs` 全体:

```csharp
using System.Collections.Generic;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<ConnectionLineConnectionJsonObject> Connections { get; set; }

        public GearChainPoleSaveDataJsonObject(IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> chainTargets)
        {
            // DictionaryからConnectionDataのリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<ConnectionLineConnectionJsonObject>();
            foreach (var target in chainTargets)
            {
                Connections.Add(new ConnectionLineConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Record));
            }

            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            Connections.Sort((left, right) => left.TargetBlockInstanceId.CompareTo(right.TargetBlockInstanceId));
        }

        public GearChainPoleSaveDataJsonObject() { Connections = new List<ConnectionLineConnectionJsonObject>(); }
    }
}
```

`moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLineConnectionJsonObject.cs`（新規。旧 `ElectricWireConnectionJsonObject`・`GearChainPoleConnectionJsonObject` を1つに畳む）:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Component;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// 電線・チェーンのセーブ上の接続1件。相手・引いた種類・払った素材を持つ
    /// One saved wire or chain connection: partner, drawn connect tool and paid materials
    /// </summary>
    public class ConnectionLineConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; set; }

        // 引いた種類はUndoの引き直しに要る。欠けた旧形はマイグレーションが埋めるので、ここでは必須で読む
        // The drawn tool is needed for undo re-drawing; the migration fills it for old saves, so it is read as required here
        [JsonProperty("connectToolGuid", Required = Required.Always)] public Guid ConnectToolGuid { get; set; }
        [JsonProperty("materials")] public List<ConnectToolMaterialSaveJsonObject> Materials { get; set; }

        public ConnectionLineConnectionJsonObject() { Materials = new List<ConnectToolMaterialSaveJsonObject>(); }

        public ConnectionLineConnectionJsonObject(int targetBlockInstanceId, ConnectionLineRecord record)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            ConnectToolGuid = record.ConnectToolGuid;
            Materials = record.Materials == null
                ? new List<ConnectToolMaterialSaveJsonObject>()
                : record.Materials.Select(m => new ConnectToolMaterialSaveJsonObject(m)).ToList();
        }

        // ロード時に永続値から接続記録を復元する
        // Restore the connection record from persisted values on load
        public ConnectionLineRecord ToConnectionRecord()
        {
            var materials = (Materials ?? new List<ConnectToolMaterialSaveJsonObject>())
                .Select(m => m.ToMaterialCost()).ToList();
            return new ConnectionLineRecord(ConnectToolGuid, materials);
        }
    }
}
```

（`ConnectToolMaterialSaveJsonObject` は `Game.Block.Interface/Component/ConnectToolMaterialSaveJsonObject.cs` の `Game.Block.Interface.Component` 名前空間にあり、上の using で見える）

`moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/IGearChainConnectionLookup.cs`（新規。台帳の読み取り面）:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーン接続台帳の読み取り面。書き換えは IGearChainConnectionMutation だけが行う
    /// Read surface of the chain connection ledger; only IGearChainConnectionMutation writes
    /// </summary>
    public interface IGearChainConnectionLookup
    {
        int Count { get; }
        IEnumerable<BlockInstanceId> PartnerIds { get; }
        IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> Targets { get; }
        bool Contains(BlockInstanceId partnerId);
        bool TryGetRecord(BlockInstanceId partnerId, out ConnectionLineRecord record);
        IReadOnlyList<IItemStack> CreateRefundItems();
        GearChainPoleSaveDataJsonObject CreateSaveData();
    }
}
```

`moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/IGearChainConnectionMutation.cs`（新規。台帳の変更面）:

```csharp
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーン接続台帳の変更面。持ち主のコンポーネントだけが持ち、外へは渡さない
    /// Mutation surface of the chain connection ledger; held only by the owning component, never handed out
    /// </summary>
    public interface IGearChainConnectionMutation
    {
        void Add(BlockInstanceId partnerId, IGearEnergyTransformer transformer, ConnectionLineRecord record);
        bool TryRemove(BlockInstanceId partnerId, out ConnectionLineRecord record);
        void Clear();
    }
}
```

`moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainConnectionSet.cs`（新規。GearChainPoleComponent を200行未満へ戻すための接続台帳）:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーンポール1本が持つチェーン接続の台帳。dirty化と状態通知は持ち主のコンポーネントが行う
    /// Ledger of a pole's chain connections; topology dirtying and state notifications stay with the owning component
    /// </summary>
    public class GearChainConnectionSet : IGearChainConnectionLookup, IGearChainConnectionMutation
    {
        private readonly Dictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> _targets = new();

        public int Count => _targets.Count;
        public IEnumerable<BlockInstanceId> PartnerIds => _targets.Keys;
        public IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> Targets => _targets;

        public bool Contains(BlockInstanceId partnerId)
        {
            return _targets.ContainsKey(partnerId);
        }

        public bool TryGetRecord(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            var found = _targets.TryGetValue(partnerId, out var connection);
            record = found ? connection.Record : default;
            return found;
        }

        public void Add(BlockInstanceId partnerId, IGearEnergyTransformer transformer, ConnectionLineRecord record)
        {
            _targets.Add(partnerId, (transformer, record));
        }

        public bool TryRemove(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            if (!_targets.Remove(partnerId, out var connection))
            {
                record = default;
                return false;
            }

            record = connection.Record;
            return true;
        }

        public void Clear()
        {
            _targets.Clear();
        }

        // 撤去時に返す素材を接続ごとに展開する（展開規則は接続線の正本）
        // Expand the materials to refund on removal, per connection (the rule lives in the connection-line definition)
        public IReadOnlyList<IItemStack> CreateRefundItems()
        {
            var refundItems = new List<IItemStack>();
            foreach (var connection in _targets.Values) refundItems.AddRange(ConnectionLineRefundItems.Create(connection.Record.Materials));
            return refundItems;
        }

        public GearChainPoleSaveDataJsonObject CreateSaveData()
        {
            return new GearChainPoleSaveDataJsonObject(_targets);
        }
    }
}
```

`GearChainPoleComponent.cs` の置換（`_chainTargets` を台帳へ寄せ、読み取りは `_chainLookup`、変更は `_chainMutation` 経由に分ける。using `Core.Item.Interface`/`Core.Master` は不要になれば削除）:

```csharp
        // 同じ台帳を読み取り面と変更面に分けて持つ。外へ出してよいのは読み取り面だけ
        // Hold the one ledger through separate read and mutation surfaces; only the read surface may leave this component
        private readonly IGearChainConnectionLookup _chainLookup;
        private readonly IGearChainConnectionMutation _chainMutation;
```
コンストラクタ先頭（既存の代入より前）に:
```csharp
            var chainConnections = new GearChainConnectionSet();
            _chainLookup = chainConnections;
            _chainMutation = chainConnections;
```
```csharp
        public bool IsConnectionFull => _chainLookup.Count >= _param.MaxConnectionCount;
```
```csharp
        public List<GearConnect> GetGearConnects()
        {
            // コネクタ経由の隣接接続にチェーン接続を加えて返す
            // Return adjacent connections via the connector plus chain connections
            var result = _gearService.GetGearConnects();
            foreach (var chainTarget in _chainLookup.Targets.Values) result.Add(new GearConnect(chainTarget.Transformer, _chainOption, _chainOption));

            return result;
        }

        public bool ContainsChainConnection(BlockInstanceId partnerId)
        {
            return _chainLookup.Contains(partnerId);
        }

        public bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            return _chainLookup.TryGetRecord(partnerId, out record);
        }

        public bool TryAddChainConnection(BlockInstanceId partnerId, ConnectionLineRecord connectionRecord)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_chainLookup.Contains(partnerId)) return false;
            if (_chainLookup.Count >= _param.MaxConnectionCount) return false;
            var transformer = ResolveChainTarget(partnerId);
            if (transformer == null) return false;
            _chainMutation.Add(partnerId, transformer, connectionRecord);
            // 接続集合の変更点自身でdirty化し、呼び出し元の再構築漏れを構造的に防ぐ
            // Mark dirty at the mutation itself so no caller can ever forget the rebuild
            ServerContext.GetService<IGearNetworkDatastore>().MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryRemoveChainConnection(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            if (!_chainMutation.TryRemove(partnerId, out record)) return false;

            ServerContext.GetService<IGearNetworkDatastore>().MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }
```
```csharp
        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            return _chainLookup.CreateRefundItems();
        }
```
`OnPostBlockLoad` 内: `_chainTargets.Clear();` → `_chainMutation.Clear();`、`if (_chainTargets.Count >= ...) break;` → `if (_chainLookup.Count >= _param.MaxConnectionCount) break;`、`if (_chainTargets.ContainsKey(targetId)) continue;` → `if (_chainLookup.Contains(targetId)) continue;`、`var cost = connection.ToConnectionCost(); _chainTargets.Add(targetId, (transformer, cost));` → `_chainMutation.Add(targetId, transformer, connection.ToConnectionRecord());`。
`Destroy` 内: `foreach (var targetId in _chainTargets.Keys.ToList())` → `foreach (var targetId in _chainLookup.PartnerIds.ToList())`、`_chainTargets.Clear();` → `_chainMutation.Clear();`。
`GetBlockStateDetails` 内: `var partnerIds = _chainTargets.Keys;` → `var partnerIds = _chainLookup.PartnerIds;`（A3 で置換）。
`GetSaveState` 本体 → `return _chainLookup.CreateSaveData();`。
空行2連（L52-53・L111-112・L188-189 等）は1行に詰めて、最終行数が200未満であることを `wc -l` で確認する。

`ElectricWirePlacementJudgement.cs` L13-25:

```csharp
        public readonly ConnectionLineRecord WireRecord;

        private ElectricWirePlacementJudgement(bool isPlaceable, ElectricWirePlacementFailureReason failureReason, ConnectionLineRecord wireRecord)
        {
            IsPlaceable = isPlaceable;
            FailureReason = failureReason;
            WireRecord = wireRecord;
        }

        public static ElectricWirePlacementJudgement Success(ConnectionLineRecord wireRecord)
        {
            return new ElectricWirePlacementJudgement(true, ElectricWirePlacementFailureReason.None, wireRecord);
        }
```

`ElectricWirePlacementEvaluator.cs` L47-56:

```csharp
            return ElectricWirePlacementJudgement.Success(new ConnectionLineRecord(connectToolGuid, materials));
        }

        // 種類と距離から電線1本の接続記録を作る。マスタに無い種類はfalse
        // Build one wire's connection record from the tool and distance; false for a tool absent from the master
        public static bool TryCreateWireRecord(Guid connectToolGuid, float distance, out ConnectionLineRecord record)
        {
            record = default;
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, distance, out var materials)) return false;
            record = new ConnectionLineRecord(connectToolGuid, materials);
            return true;
        }
```

`ElectricWireAutoConnectPlan.cs`: 3箇所の `ElectricWireConnectionCost` を `ConnectionLineRecord`、タプル要素名 `Cost` を `Record` に置換:

```csharp
        public readonly IReadOnlyList<(BlockInstanceId TargetId, ConnectionLineRecord Record)> Targets;
```
```csharp
        private ElectricWireAutoConnectPlan(IReadOnlyList<(BlockInstanceId, ConnectionLineRecord)> targets, Guid connectToolGuid, ElectricWirePlacementFailureReason failureReason, bool isPlaceable)
```
```csharp
        public static ElectricWireAutoConnectPlan Success(IReadOnlyList<(BlockInstanceId, ConnectionLineRecord)> targets, Guid connectToolGuid)
```
```csharp
            return new ElectricWireAutoConnectPlan(Array.Empty<(BlockInstanceId, ConnectionLineRecord)>(), Guid.Empty, failureReason, false);
```

`ElectricWireAutoConnectService.cs`: L37・L46 の `Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>()` → `Array.Empty<(BlockInstanceId, ConnectionLineRecord)>()`、L58 の `out List<(BlockInstanceId, ElectricWireConnectionCost)> selectedTargets` → `ConnectionLineRecord`、L78-91:

```csharp
            bool TryBuildTargets(Guid connectToolGuid, out List<(BlockInstanceId, ConnectionLineRecord)> builtTargets, out Dictionary<ItemId, int> requiredByItem)
            {
                builtTargets = new List<(BlockInstanceId, ConnectionLineRecord)>();
                requiredByItem = new Dictionary<ItemId, int>();

                foreach (var candidate in candidates)
                {
                    if (!ElectricWirePlacementEvaluator.TryCreateWireRecord(connectToolGuid, candidate.Distance, out var record))
                    {
                        builtTargets = null;
                        return false;
                    }

                    builtTargets.Add((candidate.TargetId, record));
                    foreach (var material in record.Materials)
```
L142-144: `target.Cost` → `target.Record`（2箇所）。

`ElectricWireExtendService.cs` L137: `TryCalculateWireCost(connectToolGuid, distance, out var wireCost)` → `TryCreateWireRecord(connectToolGuid, distance, out var wireCost)`（後続の `wireCost.Materials`・`TryConnectBothSides(..., wireCost)` は型推論のまま通る）。

`ElectricWireSystemUtil.cs`: L86-96 の `judgement.WireCost` → `judgement.WireRecord`（3箇所）、L178 `ElectricWireConnectionCost cost` → `ConnectionLineRecord record`（本体の `cost` も `record` へ）。L136-174 の `TryDisconnect` を丸ごと削除し、新規ファイルへ移す:

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/Connection/ElectricWireDisconnectUtil.cs`:

```csharp
using Game.Context;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.Connection
{
    /// <summary>
    /// 電線1本の切断と返却。返却が入らなければ切断させない
    /// Disconnect one wire and refund it; refuse the disconnect when the refund cannot fit
    /// </summary>
    public static class ElectricWireDisconnectUtil
    {
        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out ElectricWirePlacementFailureReason failureReason)
        {
            // 接続対象を取得する
            // Acquire target wire connectors
            failureReason = ElectricWirePlacementFailureReason.None;
            if (!ElectricWireSystemUtil.TryGetWireConnector(posA, out var connectorA) || !ElectricWireSystemUtil.TryGetWireConnector(posB, out var connectorB))
            {
                failureReason = ElectricWirePlacementFailureReason.InvalidTarget;
                return false;
            }

            // 相互接続でない場合は失敗
            // Fail when not connected to each other
            if (!connectorA.ContainsWireConnection(connectorB.BlockInstanceId) || !connectorB.ContainsWireConnection(connectorA.BlockInstanceId))
            {
                failureReason = ElectricWirePlacementFailureReason.NotConnected;
                return false;
            }

            // 返却アイテムが入らない場合は切断させない（返却消滅の防止）
            // Reject the disconnect when the refund cannot fit, preventing item loss
            var record = connectorA.WireConnections[connectorB.BlockInstanceId].Record;
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            if (!ConnectToolMaterialConsumer.TryCreateFittingRefund(record.Materials, inventory, out var refundStacks))
            {
                failureReason = ElectricWirePlacementFailureReason.InventoryFull;
                return false;
            }

            // 切断し、アイテムを返却する
            // Disconnect and refund items
            connectorA.TryRemoveWireConnection(connectorB.BlockInstanceId, out _);
            connectorB.TryRemoveWireConnection(connectorA.BlockInstanceId, out _);
            foreach (var refundStack in refundStacks) inventory.InsertItem(refundStack);
            return true;
        }
    }
}
```
（`ElectricWireSystemUtil` 側で使われなくなった using は削除。`TryGetWireConnector` が private なら public にする — エクスプローラ報告では L189 public。）
`ElectricWireDisconnectProtocol.cs` L31: `ElectricWireSystemUtil.TryDisconnect(` → `ElectricWireDisconnectUtil.TryDisconnect(`。

`GearChainPlacementEvaluator.cs`: L61 → `return GearChainPlacementJudgement.Success(new ConnectionLineRecord(connectToolGuid, materials));`、L72-84:

```csharp
        public readonly ConnectionLineRecord ChainRecord;

        public bool IsPlaceable => string.IsNullOrEmpty(FailureReason);

        private GearChainPlacementJudgement(string failureReason, ConnectionLineRecord chainRecord)
        {
            FailureReason = failureReason;
            ChainRecord = chainRecord;
        }

        public static GearChainPlacementJudgement Success(ConnectionLineRecord chainRecord)
        {
            return new GearChainPlacementJudgement(string.Empty, chainRecord);
        }
```
`GearChainSystemUtil.cs` L57-63: `var cost = judgement.ChainCost;` → `var record = judgement.ChainRecord;`、続く `TryAddChainConnection(..., cost)` 2箇所と `ConnectToolMaterialConsumer.Consume(cost.Materials, inventory)` を `record` へ。

クライアント: `ElectricWireExtendPreviewCalculator.cs` L116-117:

```csharp
                if (judgement.IsPlaceable) return judgement.WireRecord.TotalCount;
                return ElectricWirePlacementEvaluator.TryCreateWireRecord(connectToolGuid, distance, out var record) ? record.TotalCount : 0;
```
`ElectricWireAutoConnectToolSelector.cs` L80: `TryCalculateWireCost(connectToolGuid, target.Distance, out var targetCost)` → `TryCreateWireRecord(connectToolGuid, target.Distance, out var targetCost)`。

テスト側の追従:
- `FakeWireConnector.cs`: 全 `ElectricWireConnectionCost` → `ConnectionLineRecord`、タプル要素名 `Cost` → `Record`。L64 → `var record = new ConnectionLineRecord(Guid.Parse("c0000000-0000-0000-0000-000000000001"), new List<ConnectToolMaterialCost> { new(new ItemId(1), 1) });`（`using System;` 追加）。`TryRemoveWireConnection(..., out ConnectionLineRecord record)` 本体の `cost =` を `record =` に。
- `ElectricWireTestUtil.cs` L27:
```csharp
            // テスト用の電線ツール種で素材0の接続記録を張る
            // Wire with a zero-material record of the test mod's wire tool
            var record = new ConnectionLineRecord(TestWireConnectToolGuid, Array.Empty<ConnectToolMaterialCost>());
            connectorA.TryAddWireConnection(connectorB.BlockInstanceId, record);
            connectorB.TryAddWireConnection(connectorA.BlockInstanceId, record);
```
  クラス先頭に `private static readonly Guid TestWireConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");`（`using Core.Master;` を追加）。
- `GearChainPoleSaveLoadTest.cs` L39 → `var noCost = new ConnectionLineRecord(Guid.Parse("c0000000-0000-0000-0000-000000000003"), Array.Empty<ConnectToolMaterialCost>());`
- `ElectricWireRemovalTest.cs` L74-75: `.Cost` → `.Record`
- `ElectricWirePlacementEvaluatorTest.cs`: `judgement.WireCost` → `judgement.WireRecord`、L129-131 のテスト名と呼び出しを `TryCreateWireRecordは距離を切り上げて記録を作る` / `TryCreateWireRecord(ConnectToolGuid, 3.2f, out var cost)` に、末尾に `Assert.AreEqual(ConnectToolGuid, cost.ConnectToolGuid);` を追加
- `GearChainPlacementEvaluatorTest.cs` L115-116: `judgement.ChainCost` → `judgement.ChainRecord`
- `ElectricWireSystemUtilTest.cs`: `ElectricWireSystemUtil.TryDisconnect` → `ElectricWireDisconnectUtil.TryDisconnect`

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop launch ./moorestech_client --restart`（新規 .cs のため）→ `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConnectionRecordSaveLoadTest|ElectricWireSaveLoadTest|GearChainPoleSaveLoadTest|ElectricWireRemovalTest|ElectricWirePlacementEvaluatorTest|GearChainPlacementEvaluatorTest|ElectricWireSystemUtilTest|ElectricWireDisconnectProtocolTest|ChainProtocolTest|GearChainSystemUtilTest|ElectricWireAutoConnect|ElectricWireExtend"`
Expected: ErrorCount 0 / 全 PASS。`wc -l` で `GearChainPoleComponent.cs`・`ElectricWireSystemUtil.cs`・`ElectricWireConnectorComponent.cs` が200未満。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire moorestech_server/Assets/Scripts/Game.Block.Interface/Component moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-v2.cs
git commit -m "feat(server): 電線・チェーンの接続記録を1型に統合し引いた種類を持たせセーブする"
```
（`git add -A` は禁止: Unity がmasterピンファイルを書き戻すため。Unity が生成した新規 `.meta` はここで一緒に add する）

---

### Task 2 (A2): セーブ版3→4 マイグレーション（旧セーブの接続へ線種別ごとの唯一の種類を書き込む）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs:25`（`CurrentVersion = 3` → `4`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV3ToV4.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V3ToV4/ConnectionToolGuidFiller.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V3ToV4/ConnectionToolGuidFillResult.cs`（補填件数か失敗理由のどちらかを持つ結果。前例 `SaveMigrationStepResult`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs:114`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs:37`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs:137`
- Create (test): `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/ConnectToolGuidMigration/SaveMigrationStepV3ToV4Test.cs`
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/ConnectToolGuidMigrationLoadTest.cs`

**移行規則（仕様文）:** 旧セーブ（版3）の接続には種類が無い。素材は見ず、マスタも引かず、線種別ごとに固定の唯一の種類を書き込む:
- 電線（`state.ElectricWireConnectorComponent.connections[]`）→ `872372d5-2998-4fb7-826c-593ceeafcfb2`
- 歯車チェーン（`state.GearChainPoleComponent.connections[]`）→ `6c1dab62-be1e-45bc-abe7-69ec7d109b88`

出所: ユーザー裁定 2026-10-04（`.decisions/2026-10-04-旧セーブの線は線種別ごとの唯一の種類を割り当てて移行する.md`）。素材が空・未知のアイテムでも割り当てて移行し、`Failed` にはしない。既に `connectToolGuid` を持つ接続は触らない（冪等）。
`Failed` を返すのは、ステップが辿れない構造の壊れたJSONだけ（`world` が配列でない・`world` 要素がオブジェクトでない・`state[saveKey]` がオブジェクトでない・`connections` が配列でない・接続要素がオブジェクトでない）。根拠: これらを素通しすると、未変換の接続を残したまま版4が刻まれる。版4のロードは `connectToolGuid` を `Required.Always` で読む（A1）ので、後で `JsonSerializationException` を投げてワールドが落ちる。ここで止めれば、版3の原本が無傷で残る。
`state` を持たない／null のブロック、および該当 saveKey を持たないブロックは対象外として素通しする（壊れた形ではない）。

**Interfaces:**
- Consumes: A1 のセーブ JSON 形（`connections[].connectToolGuid` 必須）
- Produces: `WorldSaveAllInfo.CurrentVersion == 4`、引数なしの `SaveMigrationStepV3ToV4()`、`SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid` / `GearChainConnectToolGuid`（`public static readonly Guid`。テストが参照する）、`ConnectionToolGuidFiller.Fill(JObject state, string saveKey, Guid connectToolGuid) : ConnectionToolGuidFillResult`（`IsFilled`・`FilledCount`・`FailureReason`、`Filled(int)`/`Failed(string)`）

- [ ] **Step 1: テストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/ConnectToolGuidMigration/SaveMigrationStepV3ToV4Test.cs`:

```csharp
using System;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad.ConnectToolGuidMigration
{
    public class SaveMigrationStepV3ToV4Test
    {
        private const string WireSaveKey = "ElectricWireConnectorComponent";
        private const string ChainSaveKey = "GearChainPoleComponent";
        private const string WireItem = "00000000-0000-0000-1234-000000000001";

        [Test]
        public void 電線とチェーンへ線種別ごとの固定の種類が書き込まれるTest()
        {
            var save = Save(Block(WireSaveKey, Connection(Materials(WireItem))), Block(ChainSaveKey, Connection(Materials(WireItem))));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, ToolOf(result.Save, 0, WireSaveKey));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, ToolOf(result.Save, 1, ChainSaveKey));
        }

        [Test]
        public void 素材が空の接続も移行されるTest()
        {
            var save = Save(Block(WireSaveKey, Connection(new JArray())), Block(ChainSaveKey, Connection(new JArray())));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, ToolOf(result.Save, 0, WireSaveKey));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, ToolOf(result.Save, 1, ChainSaveKey));
        }

        [Test]
        public void 未知の素材の接続も移行されるTest()
        {
            var unknown = Materials("ffffffff-ffff-ffff-ffff-ffffffffffff");
            var save = Save(Block(WireSaveKey, Connection(unknown)), Block(ChainSaveKey, Connection((JArray)unknown.DeepClone())));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, ToolOf(result.Save, 0, WireSaveKey));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, ToolOf(result.Save, 1, ChainSaveKey));
        }

        [Test]
        public void 既に種類がある接続は上書きされないTest()
        {
            var existing = Guid.NewGuid();
            var connection = Connection(Materials(WireItem));
            connection["connectToolGuid"] = existing.ToString();
            var save = Save(Block(WireSaveKey, connection));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(existing, ToolOf(result.Save, 0, WireSaveKey));
        }

        [Test]
        public void 接続を持たないブロックは素通しされるTest()
        {
            var save = Save(JObject.Parse("{\"blockGuid\":\"x\",\"state\":{\"ChestComponent\":{}}}"), JObject.Parse("{\"blockGuid\":\"y\",\"state\":{}}"), JObject.Parse("{\"blockGuid\":\"z\"}"));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
        }

        [Test]
        public void connectionsが配列でない壊れた形はFailedで返るTest()
        {
            var save = Save(new JObject { ["blockGuid"] = "x", ["state"] = new JObject { [WireSaveKey] = new JObject { ["connections"] = "broken" } } });

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains(WireSaveKey, result.FailureReason);
        }

        [Test]
        public void worldが配列でないセーブはFailedで返るTest()
        {
            var result = new SaveMigrationStepV3ToV4().Migrate(JObject.Parse("{\"world\":{}}"));

            Assert.IsFalse(result.IsConverted);
        }

        [Test]
        public void FromVersionは3であるTest()
        {
            Assert.AreEqual(3, new SaveMigrationStepV3ToV4().FromVersion);
        }

        private static JArray Materials(string itemGuid)
        {
            return JArray.Parse($"[{{\"itemGuid\":\"{itemGuid}\",\"count\":3}}]");
        }

        private static JObject Connection(JArray materials)
        {
            return new JObject { ["targetBlockInstanceId"] = 2, ["materials"] = materials };
        }

        private static JObject Block(string saveKey, JObject connection)
        {
            return new JObject { ["blockGuid"] = "x", ["state"] = new JObject { [saveKey] = new JObject { ["connections"] = new JArray(connection) } } };
        }

        private static JObject Save(params JObject[] blocks)
        {
            return new JObject { ["worldVersion"] = 3, ["world"] = new JArray(blocks) };
        }

        private static Guid ToolOf(JObject save, int blockIndex, string saveKey)
        {
            return Guid.Parse(save["world"][blockIndex]["state"][saveKey]["connections"][0]["connectToolGuid"].Value<string>());
        }
    }
}
```

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/ConnectToolGuidMigrationLoadTest.cs`（旧版セーブ→新版ロードを本番経路で確かめるテスト）:

```csharp
using System;
using System.IO;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Migration.Steps;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.SaveLoad
{
    // 版3（種類なし）のセーブが本番と同じ連鎖で版4へ上がり、線種別の固定の種類が埋まってロードできることを検証する
    // Verify a version-3 save (no tool) rises to version 4 through the production chain and loads with the fixed per-kind tools
    public class ConnectToolGuidMigrationLoadTest
    {
        private const int PlayerId = 1;
        private static readonly Guid WireToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");

        [Test]
        public void 版3の接続が線種別の種類つきで版4へ移行されロードできる()
        {
            var save = BuildVersion3Save(out var posPole, out var posGenerator, out var posChainA, out var posChainB);
            var archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();

            // 本番と同じ連鎖で版を上げる
            // Raise the version with the production chain
            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(archiveRoot);
            var prepared = preparer.Prepare(save.ToString());
            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            Assert.AreEqual(4, prepared.Save["worldVersion"].Value<int>());

            // 移行後のセーブを新しいワールドへロードし、線種別の固定の種類が入っていることを確かめる
            // Load the migrated save into a fresh world and check each kind got its fixed tool
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)loadProvider.GetService<IWorldSaveDataLoader>()).Load(prepared.Save.ToString());
            var pole = ServerContext.WorldBlockDatastore.GetBlock(posPole).GetComponent<IElectricWireConnector>();
            var generator = ServerContext.WorldBlockDatastore.GetBlock(posGenerator).GetComponent<IElectricWireConnector>();
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, pole.WireConnections[generator.BlockInstanceId].Record.ConnectToolGuid);
            var chainA = ServerContext.WorldBlockDatastore.GetBlock(posChainA).GetComponent<IGearChainPole>();
            var chainB = ServerContext.WorldBlockDatastore.GetBlock(posChainB).GetComponent<IGearChainPole>();
            Assert.IsTrue(chainA.TryGetChainConnectionRecord(chainB.BlockInstanceId, out var chainRecord));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, chainRecord.ConnectToolGuid);

            Directory.Delete(archiveRoot, true);
        }

        // 現行の本物のセーブから種類キーを消し版3と名乗らせて、版3の形を作る
        // Strip the tool keys from a real current save and label it version 3 to reproduce the version-3 shape
        private static JObject BuildVersion3Save(out Vector3Int posPole, out Vector3Int posGenerator, out Vector3Int posChainA, out Vector3Int posChainB)
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlock = provider.GetService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireToolGuid);
            unlock.UnlockConnectTool(ChainToolGuid);
            posPole = new Vector3Int(0, 0, 0);
            posGenerator = new Vector3Int(2, 0, 0);
            posChainA = new Vector3Int(10, 0, 0);
            posChainB = new Vector3Int(12, 0, 0);
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posPole, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GeneratorId, posGenerator, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var inventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001")), 10));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000004")), 10));
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posPole, posGenerator, PlayerId, WireToolGuid, out var wireError), wireError.ToString());
            Assert.IsTrue(GearChainSystemUtil.TryConnect(posChainA, posChainB, PlayerId, ChainToolGuid, out var chainError), chainError);

            var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            var stripped = 0;
            foreach (var block in (JArray)save["world"])
            {
                foreach (var saveKey in new[] { "ElectricWireConnectorComponent", "GearChainPoleComponent" })
                {
                    if (block["state"]?[saveKey]?["connections"] is not JArray connections) continue;
                    foreach (var connection in connections) if (((JObject)connection).Remove("connectToolGuid")) stripped++;
                }
            }

            Assert.AreEqual(4, stripped, "電線2端＋チェーン2端の種類キーを消せていません");
            save["worldVersion"] = 3;
            return save;
        }
    }
}
```
（テストmodの接続ツールGuidは `c0…01`/`c0…03` だが、移行は本番の固定Guidを書く。ロード時の記録は素材とGuidを読むだけで、マスタへ照合しない（`ConnectionLineConnectionJsonObject.ToConnectionRecord`）。だから、テストmodに無いGuidでもロードは通る。）

- [ ] **Step 2: 実装を書く**

`WorldSaveAllInfo.cs` L25: `public const int CurrentVersion = 4;`

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V3ToV4/ConnectionToolGuidFillResult.cs`:

```csharp
namespace Game.SaveLoad.Migration.Steps.V3ToV4
{
    /// <summary>
    /// 1ブロックの接続への種類補填の結果。成功なら補填件数、失敗なら辿れなかった理由を持つ
    /// Result of filling tools into one block's connections: the filled count on success, the unwalkable reason on failure
    /// </summary>
    public sealed class ConnectionToolGuidFillResult
    {
        public bool IsFilled { get; }
        public int FilledCount { get; }
        public string FailureReason { get; }

        private ConnectionToolGuidFillResult(bool isFilled, int filledCount, string failureReason)
        {
            IsFilled = isFilled;
            FilledCount = filledCount;
            FailureReason = failureReason;
        }

        public static ConnectionToolGuidFillResult Filled(int filledCount)
        {
            return new ConnectionToolGuidFillResult(true, filledCount, null);
        }

        public static ConnectionToolGuidFillResult Failed(string reason)
        {
            return new ConnectionToolGuidFillResult(false, 0, reason);
        }
    }
}
```

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V3ToV4/ConnectionToolGuidFiller.cs`:

```csharp
using System;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V3ToV4
{
    /// <summary>
    /// 1ブロックのstateの中の接続配列へ、渡された種類を書き込む。既に種類がある接続は触らない（冪等）
    /// Write the given tool into one block state's connection array; connections that already have one stay untouched (idempotent)
    /// </summary>
    public static class ConnectionToolGuidFiller
    {
        public static ConnectionToolGuidFillResult Fill(JObject state, string saveKey, Guid connectToolGuid)
        {
            // この種の接続を持たないブロックは対象外
            // Blocks without this kind of connection are out of scope
            var componentState = state[saveKey];
            if (componentState == null || componentState.Type == JTokenType.Null) return ConnectionToolGuidFillResult.Filled(0);

            // 辿れない形を素通しすると未変換のまま版4が刻まれ、必須キーの読み込みで後から落ちる
            // Passing an unwalkable shape would stamp version 4 on an unconverted save that later fails on the required key
            if (!(componentState is JObject componentObject) || !(componentObject["connections"] is JArray connections))
            {
                return ConnectionToolGuidFillResult.Failed($"state['{saveKey}'].connectionsが配列ではないため種類を書き込めません。 type={componentState.Type}");
            }

            var filled = 0;
            foreach (var connectionToken in connections)
            {
                if (!(connectionToken is JObject connection))
                {
                    return ConnectionToolGuidFillResult.Failed($"state['{saveKey}']の接続要素がオブジェクトではありません。 type={connectionToken.Type}");
                }

                // 既に新形式なら上書きしない。塗り潰すと正しい値が無音で消える
                // Leave the new shape untouched; overwriting would silently erase the correct value
                if (connection["connectToolGuid"] != null) continue;

                connection["connectToolGuid"] = connectToolGuid.ToString();
                filled++;
            }

            return ConnectionToolGuidFillResult.Filled(filled);
        }
    }
}
```

`moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV3ToV4.cs`:

```csharp
using System;
using Game.SaveLoad.Migration.Steps.V3ToV4;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V3→V4移行（ADR0076）: 電線・歯車チェーンの各接続へ、線種別ごとの唯一の種類を書き込む
    // V3→V4 migration (ADR 0076): write each kind's single connect tool into every wire and gear-chain connection
    // 素材は見ずマスタも引かない（裁定 .decisions/2026-10-04-旧セーブの線は線種別ごとの唯一の種類を割り当てて移行する.md）
    // Materials are ignored and the master is never read (ruling .decisions/2026-10-04-旧セーブの線は線種別ごとの唯一の種類を割り当てて移行する.md)
    public sealed class SaveMigrationStepV3ToV4 : ISaveMigrationStep
    {
        public static readonly Guid ElectricWireConnectToolGuid = Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2");
        public static readonly Guid GearChainConnectToolGuid = Guid.Parse("6c1dab62-be1e-45bc-abe7-69ec7d109b88");

        // コンポーネントのSaveKey（nameof）。ステップは前の版の型を参照しないので文字列で持つ
        // The components' SaveKeys (nameof); the step never references live types, so they are kept as strings
        private const string WireSaveKey = "ElectricWireConnectorComponent";
        private const string ChainSaveKey = "GearChainPoleComponent";

        public int FromVersion => 3;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!(save["world"] is JArray world)) return Fail($"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}");

            var wireFilled = 0;
            var chainFilled = 0;
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Fail($"world要素がオブジェクトではないため変換できません。 type={blockToken.Type}");

                // stateが無いブロックは接続を持たないので対象外
                // A block without state holds no connections, so it is out of scope
                if (!(block["state"] is JObject state)) continue;

                // 電線とチェーンの接続へそれぞれの固定の種類を書き込む
                // Write each kind's fixed tool into wire and chain connections
                var wire = ConnectionToolGuidFiller.Fill(state, WireSaveKey, ElectricWireConnectToolGuid);
                if (!wire.IsFilled) return Fail(wire.FailureReason);
                var chain = ConnectionToolGuidFiller.Fill(state, ChainSaveKey, GearChainConnectToolGuid);
                if (!chain.IsFilled) return Fail(chain.FailureReason);
                wireFilled += wire.FilledCount;
                chainFilled += chain.FilledCount;
            }

            Debug.Log($"セーブを版3から版4へ変換しました。電線の種類補填={wireFilled}件 チェーンの種類補填={chainFilled}件");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版3から版4へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
```

`SaveAndEventServiceRegistration.cs` L114:

```csharp
            services.AddSingleton(SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2(), new SaveMigrationStepV2ToV3(), new SaveMigrationStepV3ToV4() }));
```

`SaveLoadPreparerTestFixture.cs` L37 と `SaveMigrationChainTest.cs` L137 の配列にも同じく `new SaveMigrationStepV3ToV4()` を足す。本番と同じ連鎖を保つためで、足さないと `ForCurrentVersion` の欠番検証で落ちる。

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigrationStepV3ToV4Test|ConnectToolGuidMigrationLoadTest|SaveMigrationChain|SaveLoadPreparer|SaveMigrationStepV1ToV2Test|SaveMigrationStepV2ToV3"`
Expected: ErrorCount 0 / 全 PASS
続けて実ロード検証（moorestech-save-migration「実ロード検証（必須）」）: 手元の版3セーブ（電線・チェーンを含むもの）で `references/load_test.cs` を `uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat <scratchpadに写したload_test.cs>)"` で実行し、`LOAD OK | blocks=N` を確認する。あわせて `uloop get-logs` で「セーブを版3から版4へ変換しました。電線の種類補填=X件 チェーンの種類補填=Y件」が出ていることを確認する。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad
git commit -m "feat(save): 版3→4で電線・チェーンの接続へ線種別ごとの唯一の種類を書き込む"
```

---

### Task 3 (A3): 電線・チェーンの状態同期に「接続先＋種類」を載せる

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLinePartnerMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire/ElectricWireStateDetail.cs:19-30`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainPoleStateDetail.cs:19-30`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire/ElectricWireConnectorComponent.cs:178`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainPoleComponent.cs`（`GetBlockStateDetails` の `partnerIds` 行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireStateChangeProcessor.cs:41-45`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/GearChainPoleStateChangeProcessor.cs:32-36`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPoleExtendPreviewCalculator.cs:83`
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionLineStateDetailTest.cs`

**Interfaces:**
- Consumes: A1 の `ConnectionLineRecord.ConnectToolGuid`、`IGearChainConnectionLookup.Targets`
- Produces:
  - `Game.Block.Blocks.ConnectionLine.ConnectionLinePartnerMessagePack`: `[Key(0)] int PartnerBlockInstanceId`、`[Key(1)] Guid ConnectToolGuid`、public ctor `(int partnerBlockInstanceId, Guid connectToolGuid)` と `[Obsolete]` のデシリアライズ用 ctor
  - `ElectricWireStateDetail.Partners` / `GearChainPoleStateDetail.Partners`: `[Key(0)] ConnectionLinePartnerMessagePack[]`、ctor `(ConnectionLinePartnerMessagePack[] partners)`（旧 `PartnerBlockInstanceIds` は削除）
  - `ConnectionLinePartnerMessagePack.CreateArray<TPeer>(IEnumerable<KeyValuePair<BlockInstanceId, (TPeer Peer, ConnectionLineRecord Record)>> connections) : ConnectionLinePartnerMessagePack[]`（電線・チェーンの状態詳細が共有する生成式）
  - `ConnectionLinePartnerMessagePack.ToPartnerIds(ConnectionLinePartnerMessagePack[] packs) : BlockInstanceId[]`（null は空配列。クライアントの2つの状態プロセッサが共有。Task 9 で `ConnectionLinePartner.FromMessagePacks` へ置き換わる）
  - クライアントは本タスクでは「Partners → BlockInstanceId[]」へ写すだけ（`ConnectionLineViewBase.UpdateConnectionLines(BlockInstanceId[])` は無変更）。種類を使う表示側の作り替えは契約 8 のクライアント側タスクが行う

- [ ] **Step 1: テストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionLineStateDetailTest.cs`:

```csharp
using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.ElectricWire
{
    // 電線・チェーンの状態詳細が接続先と引いた種類をクライアントへ運ぶことを検証する
    // Verify wire and chain state details carry each partner and the tool it was drawn with to the client
    public class ConnectionLineStateDetailTest
    {
        private const int PlayerId = 1;
        private static readonly Guid WireToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");

        [Test]
        public void 電線の状態詳細が接続先と種類を持つ()
        {
            var provider = CreateServer();
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            world.TryAddBlock(ForUnitTestModBlockId.GeneratorId, new Vector3Int(2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            GiveItem(provider, "00000000-0000-0000-1234-000000000001");
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(pole.BlockPositionInfo.OriginalPos, generator.BlockPositionInfo.OriginalPos, PlayerId, WireToolGuid, out var error), error.ToString());

            var detail = ReadDetail<ElectricWireStateDetail>(pole, ElectricWireStateDetail.BlockStateDetailKey);

            Assert.AreEqual(1, detail.Partners.Length);
            Assert.AreEqual(generator.BlockInstanceId.AsPrimitive(), detail.Partners[0].PartnerBlockInstanceId);
            Assert.AreEqual(WireToolGuid, detail.Partners[0].ConnectToolGuid);
        }

        [Test]
        public void チェーンの状態詳細が接続先と種類を持つ()
        {
            var provider = CreateServer();
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA);
            world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(3, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB);
            GiveItem(provider, "00000000-0000-0000-1234-000000000004");
            Assert.IsTrue(GearChainSystemUtil.TryConnect(new Vector3Int(1, 0, 0), new Vector3Int(3, 0, 0), PlayerId, ChainToolGuid, out var error), error);

            var detail = ReadDetail<GearChainPoleStateDetail>(poleA, GearChainPoleStateDetail.BlockStateDetailKey);

            Assert.AreEqual(1, detail.Partners.Length);
            Assert.AreEqual(poleB.BlockInstanceId.AsPrimitive(), detail.Partners[0].PartnerBlockInstanceId);
            Assert.AreEqual(ChainToolGuid, detail.Partners[0].ConnectToolGuid);
        }

        private static ServiceProvider CreateServer()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlock = provider.GetService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireToolGuid);
            unlock.UnlockConnectTool(ChainToolGuid);
            return provider;
        }

        private static void GiveItem(ServiceProvider provider, string itemGuid)
        {
            var inventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Guid.Parse(itemGuid)), 10));
        }

        // 1ブロックが持つ全状態観測から指定キーの詳細を1つ取り出す
        // Pull the detail with the given key out of all state observables on the block
        private static T ReadDetail<T>(IBlock block, string key)
        {
            var detail = block.GetComponents<IBlockStateObservable>()
                .SelectMany(observable => observable.GetBlockStateDetails())
                .Single(d => d.Key == key);
            return MessagePackSerializer.Deserialize<T>(detail.Value);
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

`moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLinePartnerMessagePack.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// 接続線（電線・歯車チェーン）1本ぶんの同期データ。接続先と引いた種類を運ぶ
    /// Sync data for one connection line (wire or gear chain): the partner and the tool it was drawn with
    /// </summary>
    [MessagePackObject]
    public class ConnectionLinePartnerMessagePack
    {
        [Key(0)] public int PartnerBlockInstanceId { get; set; }
        [Key(1)] public Guid ConnectToolGuid { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ConnectionLinePartnerMessagePack()
        {
        }

        public ConnectionLinePartnerMessagePack(int partnerBlockInstanceId, Guid connectToolGuid)
        {
            PartnerBlockInstanceId = partnerBlockInstanceId;
            ConnectToolGuid = connectToolGuid;
        }

        // 電線・チェーンの接続台帳から同期配列を作る（相手の型は問わない）
        // Build the sync array from a wire or chain connection ledger (the peer type does not matter)
        public static ConnectionLinePartnerMessagePack[] CreateArray<TPeer>(IEnumerable<KeyValuePair<BlockInstanceId, (TPeer Peer, ConnectionLineRecord Record)>> connections)
        {
            return connections.Select(c => new ConnectionLinePartnerMessagePack(c.Key.AsPrimitive(), c.Value.Record.ConnectToolGuid)).ToArray();
        }

        // 受信側が接続先IDだけを要るときの写像。未受信(null)は空配列
        // Map to partner ids when the receiver only needs ids; an absent array (null) becomes empty
        public static BlockInstanceId[] ToPartnerIds(ConnectionLinePartnerMessagePack[] packs)
        {
            if (packs == null) return Array.Empty<BlockInstanceId>();
            return packs.Select(p => new BlockInstanceId(p.PartnerBlockInstanceId)).ToArray();
        }
    }
}
```

`ElectricWireStateDetail.cs` L19-30（`using Game.Block.Blocks.ConnectionLine;` を追加、`System.Linq`/`Collections.Generic` は不要なら削除）:

```csharp
        /// <summary>
        /// 接続先ごとの同期データ（接続先IDと引いた種類）
        /// Per-partner sync data (partner id and the tool it was drawn with)
        /// </summary>
        [Key(0)] public ConnectionLinePartnerMessagePack[] Partners { get; set; }

        public ElectricWireStateDetail(ConnectionLinePartnerMessagePack[] partners)
        {
            Partners = partners;
        }
```

`GearChainPoleStateDetail.cs` L19-30 も同形（クラス名だけ違う）:

```csharp
        /// <summary>
        /// 接続先ごとの同期データ（接続先IDと引いた種類）
        /// Per-partner sync data (partner id and the tool it was drawn with)
        /// </summary>
        [Key(0)] public ConnectionLinePartnerMessagePack[] Partners { get; set; }

        public GearChainPoleStateDetail(ConnectionLinePartnerMessagePack[] partners)
        {
            Partners = partners;
        }
```

`ElectricWireConnectorComponent.cs` L178（`using Game.Block.Blocks.ConnectionLine;` 追加）:

```csharp
            var stateDetail = new ElectricWireStateDetail(ConnectionLinePartnerMessagePack.CreateArray(_wireConnections));
```

`GearChainPoleComponent.cs` の `GetBlockStateDetails`（`using Game.Block.Blocks.ConnectionLine;` を追加）:

```csharp
            var stateDetail = new GearChainPoleStateDetail(ConnectionLinePartnerMessagePack.CreateArray(_chainLookup.Targets));
```
（直前の `var partnerIds = ...;` 行を削除）

クライアント `ElectricWireStateChangeProcessor.cs` L41-45:

```csharp
            // 接続先InstanceIdを配列に変換する（種類の利用は表示側の作り替えで行う）
            // Convert partner instance IDs to an array (the tool is consumed by the view rework)
            var partnerInstanceIds = ConnectionLinePartnerMessagePack.ToPartnerIds(state.Partners);
```

`GearChainPoleStateChangeProcessor.cs` L32-36:

```csharp
            // 接続先InstanceIdを配列に変換
            // Convert partner instance IDs to array
            var partnerInstanceIds = ConnectionLinePartnerMessagePack.ToPartnerIds(state.Partners);
```

`GearChainPoleExtendPreviewCalculator.cs` L83（`using System.Linq;` が無ければ追加）:

```csharp
            var partnerIds = stateDetail?.Partners?.Select(p => p.PartnerBlockInstanceId).ToArray() ?? System.Array.Empty<int>();
```

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConnectionLineStateDetailTest|ChainProtocolTest|GearChainPoleExtend|ElectricWireExtend"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionLineStateDetailTest.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem
git commit -m "feat(sync): 電線・チェーンの状態詳細に接続先ごとの種類を載せる"
```

---

### Task 4 (A4): 歯車チェーンの切断（返却付き）を復活させ、切断・接続の拒否を通知する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainDisconnectFailureReason.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainPlacementFailureReason.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainSystemUtil.cs`（`TryDisconnect` 追加・`TryConnect` の失敗理由を enum 化）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GearChainConnectionEditProtocol.cs:14-49,55-80,97-102`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainPlacementEvaluator.cs`（文字列定数9本と `GearChainPlacementJudgement.FailureReason(string)` を廃し enum 1本へ。裁定 `.decisions/2026-10-05-歯車チェーンの接続失敗理由はenum1本に畳む.md`）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GearChainPoleExtendProtocol.cs:50,55,61,67,75,77,84,90,94-99,168-176`（`CreateFailed(GearChainPlacementFailureReason)`。応答 `Error` は `reason.ToString()`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPoleExtendPreviewCalculator.cs:65,120-150`（`FailureReason` を enum に）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPlacementFailureTooltipKey.cs`（全体。enum で switch）
- Modify (test, 評価器の enum 化への追従): `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainPlacementEvaluatorTest.cs:43,53,63,73,83,93`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChainPoleExtendProtocolTest.cs:76,83,92,105,124,136`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainPoleExtendTestHelper.cs:63,69`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPoleChainConnectModeTest.cs:166,182,201`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPoleFrameResultPushTest.cs:43`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPolePlaceExtendModeFeedbackTest.cs:42,60`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPlacementFailureTooltipKeyTest.cs:22-24,34-36,45-61,79-91`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:161`（`DisconnectGearChain` 追加）
- Modify: `moorestech_web/webui/src/features/notification/notificationMessages.ts:48`
- Modify: `moorestech_web/webui/src/features/notification/notificationServerIdCoverage.test.ts:23-33,98`
- Modify: `Localization/localization.csv:119`（切断3行＋接続6行を追加）
- Regenerate: `moorestech_web/webui/src/shared/i18n/generated/*`（`pnpm gen:i18n`）
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainDisconnectProtocolTest.cs`
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainConnectDeniedNotificationTest.cs`
- Modify (test, `TryConnect` の enum 化への追従): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergySaveLoadTest.cs:51`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergyTransmissionTest.cs:56`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest/GearChainRemovalTest.cs:52,97-98`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest.cs:46,64,87,111,148-149,155`、Task 1〜3 で作った `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionRecordSaveLoadTest.cs`・`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/ConnectToolGuidMigrationLoadTest.cs`・`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionLineStateDetailTest.cs`

**Interfaces:**
- Consumes: A1 の `IGearChainPole.TryGetChainConnectionRecord`、`ConnectionLineRecord.Materials`
- Produces:
  - `public enum GearChainDisconnectFailureReason { None, InvalidTarget, NotConnected, InventoryFull }`（`Server.Protocol.PacketResponse.Util.GearChain`）
  - `GearChainSystemUtil.TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out GearChainDisconnectFailureReason failureReason)`
  - `GearChainConnectionEditProtocol.ChainEditMode.Disconnect`、`GearChainConnectionEditRequest.CreateDisconnectRequest(Vector3Int posA, Vector3Int posB)`。拒否時は `denied.gearChainDisconnect.{reason}` を NotificationService で要求者へ通知
  - `public enum GearChainPlacementFailureReason { None, TooFar, AlreadyConnected, ConnectionLimit, NoItem, NoPoleItem, InvalidTarget, PositionOccupied, NotUnlocked, InsufficientItems }`（評価器・延長・接続・クライアントのプレビュー／ツールチップが端から端まで使う唯一の表現。値名は旧文字列定数の値と同じ綴りなので `ToString()` は旧 `Error` と同じ文字列）
  - `GearChainPlacementJudgement.FailureReason : GearChainPlacementFailureReason`、`IsPlaceable => FailureReason == None`、`Failure(GearChainPlacementFailureReason)`
  - `GearChainPoleExtendResponse.CreateFailed(GearChainPlacementFailureReason reason)`（`Error = reason.ToString()`）
  - クライアント `GearChainPoleExtendPreviewData.FailureReason : GearChainPlacementFailureReason`、`GearChainPlacementFailureTooltipKey.ToKey/BuildFailureLines(..., GearChainPlacementFailureReason)`
  - `GearChainSystemUtil.TryConnect(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid, out GearChainPlacementFailureReason failureReason)`（旧 `out string error` を置換）
  - `ChainEditMode.Connect` の拒否時は `denied.gearChainConnect.{reason}` を要求者へ通知し、応答 `Error` は `reason.ToString()`（成功時は空文字）。Task 11 の Undo のチェーン引き直し失敗はこの通知でプレイヤーへ届く
  - クライアント `VanillaApiSendOnly.DisconnectGearChain(Vector3Int posA, Vector3Int posB)`（削除ツールの接続線削除対象が使う）

- [ ] **Step 1: テストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainDisconnectProtocolTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    // 歯車チェーンの切断プロトコル: 返却・返却不能時の拒否・未接続の拒否通知を検証する
    // Gear chain disconnect protocol: refund, refusal when the refund cannot fit, and the not-connected denial notice
    public class GearChainDisconnectProtocolTest
    {
        private const int PlayerId = 1;
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        private static readonly Guid ChainItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private static readonly Guid FillerItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Vector3Int PosA = new(1, 0, 0);
        private static readonly Vector3Int PosB = new(3, 0, 0);

        [Test]
        public void 切断で双方の接続が消え素材が返る()
        {
            var (packet, provider, _) = SetUpConnected();
            var inventory = Inventory(provider);
            var chainItemId = MasterHolder.ItemMaster.GetItemId(ChainItemGuid);
            Assert.AreEqual(0, CountItem(inventory, chainItemId), "接続で素材10個を消費している前提");

            var response = SendDisconnect(packet);

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.IsFalse(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            Assert.IsFalse(Pole(PosB).ContainsChainConnection(Pole(PosA).BlockInstanceId));
            Assert.AreEqual(10, CountItem(inventory, chainItemId));
        }

        [Test]
        public void 返却が入らなければ拒否し接続を保ち通知する()
        {
            var (packet, provider, sink) = SetUpConnected();
            var inventory = Inventory(provider);

            // 全スロットを別アイテムの最大スタックで埋め、返却を入らなくする
            // Fill every slot with a full stack of another item so the refund cannot fit
            var fillerId = MasterHolder.ItemMaster.GetItemId(FillerItemGuid);
            var fillerMax = ItemStackLevelDataStore.Instance.GetMaxStack(fillerId);
            for (var slot = 0; slot < inventory.GetSlotSize(); slot++) inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(fillerId, fillerMax));
            sink.TakeAll();

            var response = SendDisconnect(packet);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual("InventoryFull", response.Error);
            Assert.IsTrue(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            Assert.AreEqual(1, TakeDenied(sink).Count(n => n.MessageId == "denied.gearChainDisconnect.InventoryFull"));
        }

        [Test]
        public void 未接続のポール同士はNotConnectedで拒否され通知される()
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(provider, PlayerId);
            PlacePoles();
            sink.TakeAll();

            var response = SendDisconnect(packet);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(1, TakeDenied(sink).Count(n => n.MessageId == "denied.gearChainDisconnect.NotConnected"));
        }

        private static (PacketResponseCreator packet, ServiceProvider provider, CapturedEventSink sink) SetUpConnected()
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(provider, PlayerId);
            provider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ChainToolGuid);
            PlacePoles();
            Inventory(provider).SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ChainItemGuid), 10));

            // 本番と同じ接続プロトコルで張る（距離2→1単位×10個を消費）
            // Connect through the production protocol (distance 2 → 1 unit × 10 items consumed)
            var connect = MessagePackSerializer.Serialize(GearChainConnectionEditProtocol.GearChainConnectionEditRequest.CreateConnectRequest(PosA, PosB, ChainToolGuid));
            var connected = MessagePackSerializer.Deserialize<GearChainConnectionEditProtocol.GearChainConnectionEditResponse>(packet.GetPacketResponse(connect, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First().ToArray());
            Assert.IsTrue(connected.IsSuccess, connected.Error);
            return (packet, provider, sink);
        }

        private static void PlacePoles()
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, PosA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, PosB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        private static GearChainConnectionEditProtocol.GearChainConnectionEditResponse SendDisconnect(PacketResponseCreator packet)
        {
            var payload = MessagePackSerializer.Serialize(GearChainConnectionEditProtocol.GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));
            var bytes = packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            return MessagePackSerializer.Deserialize<GearChainConnectionEditProtocol.GearChainConnectionEditResponse>(bytes.ToArray());
        }

        private static IGearChainPole Pole(Vector3Int pos)
        {
            return ServerContext.WorldBlockDatastore.GetBlock(pos).GetComponent<IGearChainPole>();
        }

        private static Core.Inventory.IOpenableInventory Inventory(ServiceProvider provider)
        {
            return provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }

        private static int CountItem(Core.Inventory.IOpenableInventory inventory, ItemId itemId)
        {
            return inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }

        private static List<NotificationMessagePack> TakeDenied(CapturedEventSink sink)
        {
            return sink.TakeAll()
                .Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(n => n.Category == NotificationCategory.OperationDenied)
                .ToList();
        }
    }
}
```
（`CapturedEventSink` の名前空間は `OperationDeniedNotificationTest.cs` の using に合わせる。`MainOpenableInventory` の型名が `IOpenableInventory` 以外なら実型へ）

- [ ] **Step 2: 実装を書く**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainDisconnectFailureReason.cs`:

```csharp
namespace Server.Protocol.PacketResponse.Util.GearChain
{
    /// <summary>
    /// 歯車チェーン切断の拒否理由。通知idの接尾辞になるため、Web表の網羅テストがこのenumを展開する
    /// Refusal reasons for a gear chain disconnect; they suffix notification ids, so the web coverage test expands this enum
    /// </summary>
    public enum GearChainDisconnectFailureReason
    {
        None,
        InvalidTarget,
        NotConnected,
        InventoryFull,
    }
}
```

`GearChainSystemUtil.cs` に追加（`TryConnect` の後ろ）:

```csharp
        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out GearChainDisconnectFailureReason failureReason)
        {
            // 両端のポールを解決する
            // Resolve both poles
            failureReason = GearChainDisconnectFailureReason.None;
            if (!TryGetGearChainPole(posA, out var poleA, out _) || !TryGetGearChainPole(posB, out var poleB, out _))
            {
                failureReason = GearChainDisconnectFailureReason.InvalidTarget;
                return false;
            }

            // 相互接続でなければ切断できない
            // A pair that is not connected both ways cannot be disconnected
            if (!poleA.TryGetChainConnectionRecord(poleB.BlockInstanceId, out var record) || !poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                failureReason = GearChainDisconnectFailureReason.NotConnected;
                return false;
            }

            // 返却が入らないなら切断させない（返却消滅の防止。電線の切断と同じ順序）
            // Refuse when the refund cannot fit, preventing item loss (same order as the wire disconnect)
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            if (!ConnectToolMaterialConsumer.TryCreateFittingRefund(record.Materials, inventory, out var refundStacks))
            {
                failureReason = GearChainDisconnectFailureReason.InventoryFull;
                return false;
            }

            // 双方から外して返却する。歯車網のdirty化は除去メソッド自身が行う
            // Remove from both sides and refund; the removal itself marks the gear topology dirty
            poleA.TryRemoveChainConnection(poleB.BlockInstanceId, out _);
            poleB.TryRemoveChainConnection(poleA.BlockInstanceId, out _);
            foreach (var refundStack in refundStacks) inventory.InsertItem(refundStack);
            return true;
        }
```

`GearChainConnectionEditProtocol.cs`（using に `Server.Event.Notification` を追加）:

```csharp
        private readonly NotificationService _notificationService;

        public GearChainConnectionEditProtocol(ServiceProvider serviceProvider)
        {
            _notificationService = serviceProvider.GetService<NotificationService>();
        }
```
`ExecuteEdit` の switch:

```csharp
                switch (data.Mode)
                {
                    case ChainEditMode.Connect:
                        success = GearChainSystemUtil.TryConnect(data.PosAVector, data.PosBVector, requesterPlayerId, data.ConnectToolGuid, out error);
                        break;

                    case ChainEditMode.Disconnect:
                        success = GearChainSystemUtil.TryDisconnect(data.PosAVector, data.PosBVector, requesterPlayerId, out var disconnectFailure);
                        error = success ? string.Empty : disconnectFailure.ToString();

                        // 送信側は応答を待たない（SendOnly）ので、拒否は通知でプレイヤーへ返す
                        // The sender does not await a response (SendOnly), so a refusal is surfaced through a notification
                        if (!success) _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.gearChainDisconnect.{disconnectFailure}", Array.Empty<string>()));
                        break;

                    default:
                        return new GearChainConnectionEditResponse(false, "Invalid mode");
                }
```
`GearChainConnectionEditRequest` に追加（`CreateConnectRequest` の後ろ）:

```csharp
            public static GearChainConnectionEditRequest CreateDisconnectRequest(Vector3Int posA, Vector3Int posB)
            {
                return new GearChainConnectionEditRequest
                {
                    Tag = GearChainConnectionEditProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    Mode = ChainEditMode.Disconnect,
                    ConnectToolGuid = Guid.Empty,
                };
            }
```
enum:

```csharp
        public enum ChainEditMode
        {
            Connect,
            Disconnect,
        }
```

クライアント `VanillaApiSendOnly.cs`（`ConnectGearChain` の後ろ）:

```csharp
        /// <summary>
        /// ギアチェーンポール間のチェーンを切断する
        /// Disconnect a chain between GearChainPoles
        /// </summary>
        public void DisconnectGearChain(Vector3Int posA, Vector3Int posB)
        {
            var request = GearChainConnectionEditRequest.CreateDisconnectRequest(posA, posB);
            _packetSender.Send(request);
        }
```

`Localization/localization.csv` の L119 直後に3行（列: key,Source,english,japanese,german,korean）:

```csv
ui.notification.gearChainDisconnectNotConnected,Disconnect failed: not connected,Disconnect failed: not connected,接続されていないため切断できませんでした,Trennen fehlgeschlagen: nicht verbunden,연결 해제 실패: 연결되어 있지 않습니다
ui.notification.gearChainDisconnectInventoryFull,Disconnect failed: inventory is full,Disconnect failed: inventory is full,インベントリが満杯のため切断できませんでした,Trennen fehlgeschlagen: Inventar ist voll,연결 해제 실패: 인벤토리가 가득 찼습니다
ui.notification.gearChainDisconnectFailed,Disconnect failed,Disconnect failed,切断に失敗しました,Trennen fehlgeschlagen,연결 해제 실패
```

`notificationMessages.ts` L48 の直後:

```ts
  ["denied.gearChainDisconnect.NotConnected", L.ui.notification.gearChainDisconnectNotConnected],
  ["denied.gearChainDisconnect.InventoryFull", L.ui.notification.gearChainDisconnectInventoryFull],
  ["denied.gearChainDisconnect.InvalidTarget", L.ui.notification.gearChainDisconnectFailed],
```

`notificationServerIdCoverage.test.ts` の `interpolatedIdEnums`（L33 の `]);` の直前）に:

```ts
  ["denied.gearChainDisconnect.", { enumName: "GearChainDisconnectFailureReason", notSentMembers: ["None"] }],
```
と、走査確認（L98 の直後）に:

```ts
    expect(ids).toContain("denied.gearChainDisconnect.InventoryFull");
```

- [ ] **Step 3: 接続失敗の通知テストを書く**

接続の拒否も通知する理由: Undo はチェーンを `SendOnly.ConnectGearChain`（`va:gearChainConnectionEdit` の Connect）で引き直す。現状、Connect の失敗は応答にしか載らず、送信側は応答を読まないので、プレイヤーには何も見えない（ユーザー裁定 2026-10-05: Undo の部分失敗は通知する。`.decisions/2026-10-05-Undoの部分失敗はできた分だけ戻し残りは通知する.md`）。
クライアントでの `Error` の消費を確認した結果: `GearChainConnectionEditResponse` を読むクライアントコードは無い（`grep -rn "GearChainConnectionEditResponse" moorestech_client/Assets/Scripts` は0件。`VanillaApi.Response` に chain edit の API も無い）。チェーンツールは `GearChainPoleConnectSystem.cs:101` で `SendOnly.ConnectGearChain` を送るだけで、`Error` 文字列は読まない。チェーンツールの延長は別プロトコル（`GearChainPoleExtendProtocol` が `GearChainPlacementEvaluator` の文字列定数を `Response.Error` に載せて返す）で、本ステップでは触らない。よって `Error` を `reason.ToString()` にしても既存クライアントの挙動は変わらない。唯一見える変化は、チェーンツールの接続が失敗したときに拒否通知が出るようになること（従来は無音）。

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainConnectDeniedNotificationTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    // 歯車チェーン接続の拒否が要求者へ通知されること（Undoの引き直し失敗を無音にしない）を検証する
    // Verify a refused gear chain connect notifies the requester, so undo re-drawing never fails silently
    public class GearChainConnectDeniedNotificationTest
    {
        private const int PlayerId = 1;
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        private static readonly Guid ChainItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private static readonly Vector3Int PosA = new(1, 0, 0);
        private static readonly Vector3Int PosB = new(3, 0, 0);

        [Test]
        public void 素材不足の接続はNoItemで拒否され通知される()
        {
            var (packet, sink, _) = SetUp(unlockTool: true);

            var response = SendConnect(packet);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual("NoItem", response.Error);
            Assert.AreEqual(1, TakeDenied(sink).Count(n => n.MessageId == "denied.gearChainConnect.NoItem"));
        }

        [Test]
        public void 未解放の種類の接続はNotUnlockedで拒否され通知される()
        {
            var (packet, sink, _) = SetUp(unlockTool: false);

            var response = SendConnect(packet);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(1, TakeDenied(sink).Count(n => n.MessageId == "denied.gearChainConnect.NotUnlocked"));
        }

        [Test]
        public void 成功した接続は拒否通知を出さない()
        {
            var (packet, sink, provider) = SetUp(unlockTool: true);
            var inventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ChainItemGuid), 10));

            var response = SendConnect(packet);

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.AreEqual(0, TakeDenied(sink).Count);
        }

        private static (PacketResponseCreator packet, CapturedEventSink sink, ServiceProvider provider) SetUp(bool unlockTool)
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(provider, PlayerId);
            if (unlockTool) provider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ChainToolGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, PosA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, PosB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            sink.TakeAll();
            return (packet, sink, provider);
        }

        private static GearChainConnectionEditProtocol.GearChainConnectionEditResponse SendConnect(PacketResponseCreator packet)
        {
            var payload = MessagePackSerializer.Serialize(GearChainConnectionEditProtocol.GearChainConnectionEditRequest.CreateConnectRequest(PosA, PosB, ChainToolGuid));
            var bytes = packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            return MessagePackSerializer.Deserialize<GearChainConnectionEditProtocol.GearChainConnectionEditResponse>(bytes.ToArray());
        }

        private static List<NotificationMessagePack> TakeDenied(CapturedEventSink sink)
        {
            return sink.TakeAll()
                .Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(n => n.Category == NotificationCategory.OperationDenied)
                .ToList();
        }
    }
}
```
（`CapturedEventSink` の名前空間は `OperationDeniedNotificationTest.cs` の using に合わせる）

- [ ] **Step 4: 失敗理由を enum 1本に畳み、接続失敗を通知する実装を書く**

裁定（`.decisions/2026-10-05-歯車チェーンの接続失敗理由はenum1本に畳む.md`）どおり、`GearChainPlacementEvaluator` の文字列定数9本（`TooFarError` 等）と `GearChainPlacementJudgement.FailureReason(string)` を廃し、評価器・延長プロトコル・接続・クライアントのプレビューとツールチップまで同じ enum で通す（前例 `ElectricWirePlacementFailureReason`）。値名は旧定数の文字列値と同じ綴りにするので、応答の `Error = reason.ToString()` は従来と同じ文字列になる。文字列→enum の写像は書かない。

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainPlacementFailureReason.cs`:

```csharp
namespace Server.Protocol.PacketResponse.Util.GearChain
{
    /// <summary>
    /// 歯車チェーンの接続・延長設置の拒否理由。評価器から応答・通知・クライアントのツールチップまでこの1本で通す
    /// Refusal reasons for gear chain connect and extend placement; this single enum flows from the evaluator to responses, notifications and client tooltips
    /// </summary>
    public enum GearChainPlacementFailureReason
    {
        None,
        TooFar,
        AlreadyConnected,
        ConnectionLimit,
        NoItem,
        NoPoleItem,
        InvalidTarget,
        PositionOccupied,
        NotUnlocked,
        InsufficientItems,
    }
}
```

`GearChainPlacementEvaluator.cs` を置換（定数9本を削除し、判定結果を enum で持つ。`EvaluatePlacement` の判定順は不変）:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Construction;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.GearChain
{
    /// <summary>
    /// 歯車チェーンの接続・延長設置可否を判定する共有ロジック。
    /// サーバーの実行処理とクライアントのプレビューが同じ判定を呼ぶことで食い違いを構造的に防ぐ。
    /// Shared judgement logic for gear chain connect and extend placement.
    /// Server execution and client preview call this same judgement to structurally prevent mismatch.
    /// </summary>
    public static class GearChainPlacementEvaluator
    {
        /// <summary>
        /// 距離・既接続・接続数上限・チェーン素材を一括判定する。消費はconnectToolマスタ駆動の複数素材。
        /// reservedMaterials に建設コスト等の予約分を渡すと、同一アイテムの予約数を必要数へ上乗せして判定する。
        /// Evaluate distance, existing connection, connection limit and chain materials at once; consumption is connectTool-master driven multi-material.
        /// Passing reservedMaterials (e.g. construction cost) adds the reserved amount of the same item to the required count.
        /// </summary>
        public static GearChainPlacementJudgement EvaluatePlacement(float connectionDistance, float fromMaxConnectionDistance, float toMaxConnectionDistance, bool alreadyConnected, bool anyConnectionFull, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            var stacks = inventoryItems as IItemStack[] ?? inventoryItems.ToArray();

            // 距離が両端の上限のminを超えると不可
            // Reject when distance exceeds the min of both max distances
            if (Mathf.Min(fromMaxConnectionDistance, toMaxConnectionDistance) < connectionDistance) return GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.TooFar);

            // 既に接続済み・接続数の上限はそれぞれ不可
            // Reject an existing connection and a full connection count
            if (alreadyConnected) return GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.AlreadyConnected);
            if (anyConnectionFull) return GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.ConnectionLimit);

            // connectToolマスタから複数素材の必要数を算出し、予約分込みで所持が足りるかを共有の正本へ委ねる
            // Compute the multi-material requirement from the connectTool master and delegate the held-vs-required check to the shared definition
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, connectionDistance, out var materials)) return GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.NoItem);
            if (!ConstructionMaterialAccounting.HasEnough(materials, stacks, reservedMaterials)) return GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.NoItem);

            return GearChainPlacementJudgement.Success(new ConnectionLineRecord(connectToolGuid, materials));
        }
    }

    /// <summary>
    /// 歯車チェーン設置可否の判定結果。失敗理由または接続記録を保持する
    /// Judgement result of gear chain placement, holding the failure reason or the connection record
    /// </summary>
    public readonly struct GearChainPlacementJudgement
    {
        public readonly GearChainPlacementFailureReason FailureReason;
        public readonly ConnectionLineRecord ChainRecord;

        public bool IsPlaceable => FailureReason == GearChainPlacementFailureReason.None;

        private GearChainPlacementJudgement(GearChainPlacementFailureReason failureReason, ConnectionLineRecord chainRecord)
        {
            FailureReason = failureReason;
            ChainRecord = chainRecord;
        }

        public static GearChainPlacementJudgement Success(ConnectionLineRecord chainRecord)
        {
            return new GearChainPlacementJudgement(GearChainPlacementFailureReason.None, chainRecord);
        }

        public static GearChainPlacementJudgement Failure(GearChainPlacementFailureReason reason)
        {
            return new GearChainPlacementJudgement(reason, default);
        }
    }
}
```

`GearChainSystemUtil.cs` の `TryConnect` を置き換える（評価器の enum をそのまま返す。`using System;` は既存）:

```csharp
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid, out GearChainPlacementFailureReason failureReason)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            failureReason = GearChainPlacementFailureReason.None;
            var foundA = TryGetGearChainPole(posA, out var poleA, out _);
            var foundB = TryGetGearChainPole(posB, out var poleB, out _);
            if (!foundA || !foundB)
            {
                // どちらの端点が無いかは通知idに載らないので開発者ログへ残す
                // Which endpoint is missing does not fit the notification id, so leave it in the developer log
                Debug.Log($"チェーン接続を拒否: 端点にポールがありません foundA={foundA} foundB={foundB} posA={posA} posB={posB}");
                failureReason = GearChainPlacementFailureReason.InvalidTarget;
                return false;
            }

            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                failureReason = GearChainPlacementFailureReason.InvalidTarget;
                return false;
            }

            // 未解放のconnectToolによる接続要求は拒否する
            // Reject connection requests using a connectTool that is not unlocked
            if (!IsConnectToolUnlocked(connectToolGuid))
            {
                failureReason = GearChainPlacementFailureReason.NotUnlocked;
                return false;
            }

            // 距離・既接続・接続数上限・チェーン素材を共有判定で検証する
            // Validate distance, existing connection, connection limit and chain materials via shared judgement
            var connectionDistance = Vector3Int.Distance(posA, posB);
            var alreadyConnected = poleA.ContainsChainConnection(poleB.BlockInstanceId) || poleB.ContainsChainConnection(poleA.BlockInstanceId);
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(connectionDistance, poleA.MaxConnectionDistance, poleB.MaxConnectionDistance, alreadyConnected, poleA.IsConnectionFull || poleB.IsConnectionFull, connectToolGuid, inventory.InventoryItems, null);
            if (!judgement.IsPlaceable)
            {
                failureReason = judgement.FailureReason;
                return false;
            }
            var record = judgement.ChainRecord;

            // 接続を確定させる。片側だけ張れた場合は張った分を戻す
            // Finalize the connection; roll back the half that was added if the other side fails
            var addedA = poleA.TryAddChainConnection(poleB.BlockInstanceId, record);
            var addedB = addedA && poleB.TryAddChainConnection(poleA.BlockInstanceId, record);
            if (!addedA || !addedB)
            {
                poleA.TryRemoveChainConnection(poleB.BlockInstanceId, out _);
                poleB.TryRemoveChainConnection(poleA.BlockInstanceId, out _);
                failureReason = GearChainPlacementFailureReason.ConnectionLimit;
                return false;
            }

            ConnectToolMaterialConsumer.Consume(record.Materials, inventory);
            return true;
        }
```

`GearChainConnectionEditProtocol.cs` の `ExecuteEdit` 内、Step 2 で書いた switch の `case ChainEditMode.Connect:` を置き換える:

```csharp
                    case ChainEditMode.Connect:
                        success = GearChainSystemUtil.TryConnect(data.PosAVector, data.PosBVector, requesterPlayerId, data.ConnectToolGuid, out var connectFailure);
                        error = success ? string.Empty : connectFailure.ToString();

                        // チェーンツールもUndoの引き直しもSendOnlyで応答を読まないので、拒否は通知でプレイヤーへ返す
                        // Both the chain tool and undo re-drawing send this without reading the response, so a refusal is surfaced through a notification
                        if (!success) _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.gearChainConnect.{connectFailure}", Array.Empty<string>()));
                        break;
```

`GearChainPoleExtendProtocol.cs`: `CreateFailed` を enum 受けにし、8か所の呼び出しを enum へ（`GearChainPlacementEvaluator.XxxError` → `GearChainPlacementFailureReason.Xxx`。L84 は `judgement.FailureReason`、L94-99 の `out var connectError` はそのまま enum で `CreateFailed(connectError)`）:

```csharp
            public static GearChainPoleExtendResponse CreateFailed(GearChainPlacementFailureReason reason)
            {
                return new GearChainPoleExtendResponse
                {
                    IsSuccess = false,
                    Error = reason.ToString(),
                    PlacedPolePos = new Vector3IntMessagePack(Vector3Int.zero),
                };
            }
```
（応答の `Error` を読むクライアントコードは無い。`GearChainPoleExtendRequestSender.cs:76-79` は `IsSuccess` と `PlacedPolePos` だけを読む）

クライアント `GearChainPoleExtendPreviewCalculator.cs`: L65 → `if (judgement.FailureReason != GearChainPlacementFailureReason.NoItem) return Array.Empty<ConstructionMaterialShortage>();`。`GearChainPoleExtendPreviewData` の `FailureReason` を enum に（L120-150）:

```csharp
        public static GearChainPoleExtendPreviewData Invalid => new(Vector3.zero, Vector3.zero, false, false, GearChainPlacementFailureReason.None, Array.Empty<ConstructionMaterialShortage>());
```
```csharp
        // 不可理由。可なら None
        // Failure reason; None when placeable
        public readonly GearChainPlacementFailureReason FailureReason;
```
```csharp
        private GearChainPoleExtendPreviewData(Vector3 startPoint, Vector3 endPoint, bool isPlaceable, bool isValid, GearChainPlacementFailureReason failureReason, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
```

`GearChainPlacementFailureTooltipKey.cs` 全体:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.GearChain;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーン失敗理由をツールチップキーへ写像
    /// Maps a gear chain failure reason to a tooltip key
    /// </summary>
    public static class GearChainPlacementFailureTooltipKey
    {
        // 素材不足は行を作らず不足リストのまま関門へ渡す。行にした瞬間に同一アイテムの畳み込みが効かなくなる
        // A material shortage is never turned into lines here; it goes to the gate as data, since lines can no longer be folded per item
        private static bool IsMaterialShortage(GearChainPlacementFailureReason failureReason)
        {
            return failureReason == GearChainPlacementFailureReason.NoItem;
        }

        // チェーン判定が素材不足で落ちたフレームか。判定の中身はこの型の外へ出さない
        // Whether the chain judgement failed on a material shortage; the judgement itself never leaves this type
        public static bool IsChainMaterialShortage(GearChainPoleExtendPreviewData chainPreview)
        {
            if (!chainPreview.IsValid || chainPreview.IsPlaceable) return false;
            return IsMaterialShortage(chainPreview.FailureReason);
        }

        // 可:行なし／素材不足:行なし（関門が出す）／他:理由1行
        // Placeable: none / material shortage: none (the gate emits it) / otherwise: one reason line
        public static IReadOnlyList<TooltipLine> BuildFailureLines(bool isPlaceable, GearChainPlacementFailureReason failureReason)
        {
            if (isPlaceable) return Array.Empty<TooltipLine>();
            if (IsMaterialShortage(failureReason)) return Array.Empty<TooltipLine>();
            return new[] { new TooltipLine(ToKey(failureReason)) };
        }

        public static LocalizationKey ToKey(GearChainPlacementFailureReason failureReason)
        {
            return failureReason switch
            {
                GearChainPlacementFailureReason.TooFar => LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar,
                GearChainPlacementFailureReason.AlreadyConnected => LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected,
                GearChainPlacementFailureReason.ConnectionLimit => LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit,
                // 素材不足(NoItem)は名指しの行を素材ごとに積むためここでは写像しない
                // Material shortage (NoItem) is not mapped here; it becomes one named line per material
                // 上記以外（未解放・サーバー側のみの理由）はクライアントの接続判定では発生しないため既定文言へ
                // Everything else (not-unlocked, server-only reasons) never arises in client connection judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed,
            };
        }
    }
}
```

`TryConnect`・評価器の呼び出し側テストを enum へ追従させる（本番の `TryConnect` 呼び出しは `GearChainConnectionEditProtocol` と `GearChainPoleExtendProtocol.cs:94` の2か所。後者は上で `CreateFailed(connectError)` が enum を受けるので無改変で通る）:
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergySaveLoadTest.cs:51`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergyTransmissionTest.cs:56`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest/GearChainRemovalTest.cs:52,97,98`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest.cs:111,148,149`: `Assert.IsEmpty(<var> ?? string.Empty);` → `Assert.AreEqual(GearChainPlacementFailureReason.None, <var>);`
- `GearChainSystemUtilTest.cs:46` `Assert.AreEqual("TooFar", error);` → `Assert.AreEqual(GearChainPlacementFailureReason.TooFar, error);`、`:64`・`:87` `"NoItem"` → `GearChainPlacementFailureReason.NoItem`、`:155` `"ConnectionLimit"` → `GearChainPlacementFailureReason.ConnectionLimit`
- `GearChainPlacementEvaluatorTest.cs`: `GearChainPlacementEvaluator.XxxError` → `GearChainPlacementFailureReason.Xxx`（L43,53,63,73,83,93）
- `GearChainPoleExtendTestHelper.cs:63,69`: 引数を `GearChainPlacementFailureReason expectedReason` にし `Assert.AreEqual(expectedReason.ToString(), response.Error);`。`GearChainPoleExtendProtocolTest.cs:76,83,92,124` の引数を enum へ、`:105`・`:136` を `Assert.AreEqual(GearChainPlacementFailureReason.NotUnlocked.ToString(), response.Error);` / `...PositionOccupied.ToString()...`
- クライアント `GearChainPoleChainConnectModeTest.cs:166,182,201`、`GearChainPoleFrameResultPushTest.cs:43`、`GearChainPolePlaceExtendModeFeedbackTest.cs:42,60`: `GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.XxxError)` → `GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.Xxx)`
- `GearChainPlacementFailureTooltipKeyTest.cs`: 全 `GearChainPlacementEvaluator.XxxError` → `GearChainPlacementFailureReason.Xxx`、L45 のタプル型 `string FailureReason` → `GearChainPlacementFailureReason FailureReason`、L88 `CreateJudgedPreview(string failureReason)` → `CreateJudgedPreview(GearChainPlacementFailureReason failureReason)`
- Task 1〜3 で作ったテストの失敗メッセージ引数（enum は `string` 引数へ暗黙変換されないため）: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionRecordSaveLoadTest.cs` と `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/ConnectToolGuidMigrationLoadTest.cs` の `out var chainError), chainError);` → `out var chainError), chainError.ToString());`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionLineStateDetailTest.cs` の `out var error), error);` → `out var error), error.ToString());`
- 各ファイルに `using Server.Protocol.PacketResponse.Util.GearChain;` が無ければ足す

`Localization/localization.csv` の Step 2 で足した3行の直後に6行追加（列: key,Source,english,japanese,german,korean）:

```csv
ui.notification.gearChainConnectInvalidTarget,Chain connection failed: no chain pole at the target,Chain connection failed: no chain pole at the target,接続先にチェーンポールが無いためチェーンを張れませんでした,Kettenverbindung fehlgeschlagen: kein Kettenmast am Ziel,체인 연결 실패: 대상에 체인 기둥이 없습니다
ui.notification.gearChainConnectNotUnlocked,Chain connection failed: this chain is not unlocked,Chain connection failed: this chain is not unlocked,このチェーンは未解放のため張れませんでした,Kettenverbindung fehlgeschlagen: diese Kette ist nicht freigeschaltet,체인 연결 실패: 이 체인은 해금되지 않았습니다
ui.notification.gearChainConnectTooFar,Chain connection failed: too far,Chain connection failed: too far,距離が遠すぎるためチェーンを張れませんでした,Kettenverbindung fehlgeschlagen: zu weit entfernt,체인 연결 실패: 거리가 너무 멉니다
ui.notification.gearChainConnectAlreadyConnected,Chain connection failed: already connected,Chain connection failed: already connected,既に接続されているためチェーンを張れませんでした,Kettenverbindung fehlgeschlagen: bereits verbunden,체인 연결 실패: 이미 연결되어 있습니다
ui.notification.gearChainConnectConnectionLimit,Chain connection failed: connection limit reached,Chain connection failed: connection limit reached,接続数の上限に達しているためチェーンを張れませんでした,Kettenverbindung fehlgeschlagen: Verbindungslimit erreicht,체인 연결 실패: 연결 수 한도에 도달했습니다
ui.notification.gearChainConnectNoItem,Chain connection failed: not enough chain materials,Chain connection failed: not enough chain materials,チェーンの素材が足りないため張れませんでした,Kettenverbindung fehlgeschlagen: nicht genug Kettenmaterial,체인 연결 실패: 체인 재료가 부족합니다
```

`notificationMessages.ts` の Step 2 で足した `denied.gearChainDisconnect.*` 3行の直後:

```ts
  ["denied.gearChainConnect.InvalidTarget", L.ui.notification.gearChainConnectInvalidTarget],
  ["denied.gearChainConnect.NotUnlocked", L.ui.notification.gearChainConnectNotUnlocked],
  ["denied.gearChainConnect.TooFar", L.ui.notification.gearChainConnectTooFar],
  ["denied.gearChainConnect.AlreadyConnected", L.ui.notification.gearChainConnectAlreadyConnected],
  ["denied.gearChainConnect.ConnectionLimit", L.ui.notification.gearChainConnectConnectionLimit],
  ["denied.gearChainConnect.NoItem", L.ui.notification.gearChainConnectNoItem],
```

`notificationServerIdCoverage.test.ts` の `interpolatedIdEnums` に、Step 2 の `denied.gearChainDisconnect.` 行の直後へ:

```ts
  ["denied.gearChainConnect.", { enumName: "GearChainPlacementFailureReason", notSentMembers: ["None", "NoPoleItem", "PositionOccupied", "InsufficientItems"] }],
```
と、走査確認（Step 2 で足した `expect(ids).toContain("denied.gearChainDisconnect.InventoryFull");` の直後）に:

```ts
    expect(ids).toContain("denied.gearChainConnect.NoItem");
```

- [ ] **Step 5: コンパイル・テストを実行して通ることを確認する**

Run:
- `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client`（localization.csv を変えたので、`LocalizationKeys` の無関係キーで CS0117 が出たら force-recompile。記憶: localization-csv-needs-force-recompile）
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearChainDisconnectProtocolTest|GearChainConnectDeniedNotificationTest|ChainProtocolTest|OperationDeniedNotificationTest|GearChainSystemUtilTest|GearChainRemovalTest|ChainEnergy|GearChainPoleExtendProtocolTest|GearChainPlacementEvaluatorTest|GearChainPoleChainConnectModeTest|GearChainPoleFrameResultPushTest|GearChainPolePlaceExtendModeFeedbackTest|GearChainPlacementFailureTooltipKeyTest|ConnectionRecordSaveLoadTest|ConnectToolGuidMigrationLoadTest|ConnectionLineStateDetailTest"`
- `cd moorestech_web/webui && pnpm gen:i18n && pnpm test -- notificationServerIdCoverage localizationKeysFreshness notificationMessages`
Expected: ErrorCount 0 / Unity 全 PASS / vitest 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChainPoleExtendProtocolTest.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs Localization/localization.csv moorestech_web/webui/src/features/notification moorestech_web/webui/src/shared/i18n/generated
git commit -m "feat: 歯車チェーンの返却付き切断を復活し失敗理由をenum1本に畳んで切断・接続の拒否を通知する"
```

---


## B群の前提事実

前提として確認済みの事実（下書き時に実コードで確認）:
- `RemoveBlockProtocol.GetResponse` は `GetRefundItems()`（財布返却＋ブロックインベントリ＋`IGetRefundItemsInfo` 1個）を **`RemoveBlock` より前**に集め、`InsertionCheck` が通らなければ `RemoveBlockFailureReason.Unknown` で拒否する（`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs:50-60,95-130`）。レール返却もここへ足せば、区間が消える前に算出され、入りきらない撤去は拒否される。返却が入らない拒否は本群で `RemoveBlockFailureReason.InventoryFull` へ分け、クライアントは `ui.delete.inventoryFull` で理由を出す（コーディネーター裁定）。
- ノード削除時に区間は `RailGraphDatastore.RemoveNode` → `RemoveRailSegmentEdgesFromNode` / `RemoveNodeTo` で消える（`Game.Train/RailGraph/RailGraphDatastore.cs:375-395`）。
- 物理レール1本は有向区間の対 `A→B` と `(B^1)→(A^1)` で表される（`RailConnectionCommandHandler.TryConnect`/`ConnectOppositeNodes`、`Game.Train/RailGraph/RailConnectionCommandHandler.cs:30-43,104-114`）。ブロックのノード（Front/Back は互いに `^1`）から出る区間を全部見れば、ブロックに触れる物理レールは必ず1回以上現れる（`Q` 側がブロック内なら `Q^1` もブロック内で、対の区間 `Q^1→P^1` がブロックのノードから出るため）。
- 駅内部の区間（`RailComponentUtility.cs:76-77`）と駅隣接の自動接続（同:154-155）は `RailNode.ConnectNode(target)` → `Guid.Empty` で張られる（`Game.Train/RailGraph/RailNode.cs:121-124`）。よって `RailTypeGuid == Guid.Empty` の区間は無償扱いで返却しない。
- レール切断の返却算出は「`TryGetRailSegmentType` → `GetRailLength` → `ConnectToolCostCalculator.TryCalculate` → `ConnectToolMaterialConsumer.CreateRefundItems`」（`Server.Protocol/PacketResponse/Util/RailEdit/RailConnectionEditService.cs:104-118`）。撤去時の返却も同じ部品を呼ぶ。
- 電線・チェーンの撤去返却はブロック部品の `IGetRefundItemsInfo`（払った素材を部品が保存）で行うが、レールの返却は区間の種類と現在の曲線長から都度算出する必要があり、その算出部品（`ConnectToolCostCalculator`・`RailConnectionEditProtocol.GetRailLength`）が `Server.Protocol` にある。`Game.Train`（`RailComponent`）は `Server.Protocol` を参照できないため、レール返却だけはプロトコル層の `RailRemovalRefundCalculator` に置く（返却方式の非対称はユーザー裁定 2026-10-05「種類＋素材を保存（現状維持）」で据え置き）。
- `ConnectionDestination` の MessagePack 表現 `ConnectionDestinationMessagePack(ConnectionDestination)` / `ToModel()` が既にある（`Server.Util/MessagePack/RailNodeMessagePack.cs:14-37`）。`IRailGraphProvider.ResolveRailNode(ConnectionDestination)` は未登録なら `null`（`RailGraphDatastore.cs:427-442,541-544`）。
- `VanillaApiSendOnly.PlaceBlock` の呼び出し元は3つ（`PlaceBlockProtocolSender.cs:33`、`RemoveOperationRecord.cs:72`、テスト `PlacementPacketCapture.cs:49`）。`SendPlaceBlockProtocolMessagePack` のサーバー側直接生成は `VanillaApiSendOnly.cs:45` とテスト3か所（`PlaceBlockProtocolTestSupport.cs:80`、`ElectricWireAutoConnectPlaceTestBase.cs:84`、`ConstructionPayerWalletTest.cs:116`）。

---

### Task 5 (B1): 橋脚・駅の撤去で付いていたレールの素材を返却する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/RailEdit/RailRemovalRefundCalculator.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Train/RailGraph/Utility/RailSegmentPairing.cs`（物理レール1本の正規化キーの正本。サーバーの返却と Task 11 のクライアント `RailObjectIdCodec` が共有する）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Train/RailSegmentPairingTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs:1-34`（using・フィールド・ctor）, `:50`（返却が入らない拒否理由）, `:104-130`（`GetRefundItems`）, `:183-188`（`RemoveBlockFailureReason`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs:103-111`（`GetRemoveDeniedReasonKey`）
- Modify: `Localization/localization.csv:182` の直後（`ui.delete.inventoryFull` 行を追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemoveBlockProtocolTest/RemoveRailBlockRefundTest.cs`

**Interfaces:**
- Consumes: 既存 `IRailGraphDatastore.GetConnectedNodesWithDistance(IRailNode)` / `TryGetRailSegmentType(int, int, out Guid)`、`RailConnectionEditProtocol.GetRailLength(IRailNode, IRailNode)`、`ConnectToolCostCalculator.TryCalculate(Guid, float, out IReadOnlyList<ConnectToolMaterialCost>)`、`ConnectToolMaterialConsumer.CreateRefundItems(IReadOnlyList<ConnectToolMaterialCost>)`
- Produces: `Game.Train.RailGraph.Utility.RailSegmentPairing.SelectCanonicalPair(int fromNodeId, int toNodeId) : (int canonicalFrom, int canonicalTo)`（A→B と対の (B^1)→(A^1) のうち起点Idが小さい方。同値なら A→B。クライアントの既存 `TrainRailObjectManager.SelectCanonicalPair` と同じ規則をここへ移す）
- Produces: `public static List<IItemStack> RailRemovalRefundCalculator.CreateRefundItems(IBlock block, IRailGraphDatastore railGraphDatastore)`（`Server.Protocol.PacketResponse.Util.RailEdit` 名前空間。ブロックの全 `RailComponent` ノードに接する有償区間を物理1本1回で返却アイテム化。レールを持たないブロックは空リスト）。クライアント側 Undo（契約 11）が「巻き込みレールの引き直しは通常どおり再消費」を前提にできる
- Produces: `RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull`（返却アイテムがインベントリへ入りきらない撤去の拒否理由）、ローカライズキー `LocalizationKeys.Ui.Delete.InventoryFull`（`ui.delete.inventoryFull`）

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.RailEdit;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveRailBlockRefundTest
    {
        private const int PlayerId = 13;
        private static readonly Guid RailConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        private static readonly Guid ReinforcingMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private static readonly Guid IronPlateGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        // 返却素材と別種の詰め物（電線アイテム）
        // Filler item distinct from the refund materials (the wire item)
        private static readonly Guid FillerItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        private TrainTestEnvironment _environment;
        private IOpenableInventory _inventory;
        private ItemId _reinforcingMaterialId;
        private ItemId _ironPlateId;

        [SetUp]
        public void SetUp()
        {
            // レール環境と解放済みレールconnectToolを準備する
            // Prepare the rail environment and the unlocked rail connectTool
            _environment = TrainTestHelper.CreateEnvironment();
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _environment.ServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(RailConnectToolGuid);
            _reinforcingMaterialId = MasterHolder.ItemMaster.GetItemId(ReinforcingMaterialGuid);
            _ironPlateId = MasterHolder.ItemMaster.GetItemId(IronPlateGuid);
        }

        [Test]
        public void 橋脚の撤去で付いていたレールの素材が返る()
        {
            var railA = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(_environment, new Vector3Int(10, 0, 0), BlockDirection.North);
            TrainTestHelper.PlaceRail(_environment, new Vector3Int(30, 0, 0), BlockDirection.North);
            var units = CalculateUnits(railA.FrontNode, railB.BackNode);
            ConnectWithTool(railA.FrontNode, railB.BackNode, units);

            // 接続の無い橋脚の撤去で、撤去そのものの返却分（建設コスト）を基準として測る
            // Measure the removal's own refund (construction cost) on an unconnected pier as the baseline
            var (baseReinforcing, baseIron) = RemoveAndMeasureGain(new Vector3Int(30, 0, 0));
            var (reinforcing, iron) = RemoveAndMeasureGain(Vector3Int.zero);

            Assert.AreEqual(baseReinforcing + units * 12, reinforcing);
            Assert.AreEqual(baseIron + units * 5, iron);
        }

        [Test]
        public void 両端の橋脚を撤去してもレール1本分しか返らない()
        {
            var railA = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(_environment, new Vector3Int(10, 0, 0), BlockDirection.North);
            TrainTestHelper.PlaceRail(_environment, new Vector3Int(30, 0, 0), BlockDirection.North);
            var units = CalculateUnits(railA.FrontNode, railB.BackNode);
            ConnectWithTool(railA.FrontNode, railB.BackNode, units);

            // 先にA側を撤去してレールを返却させ、B側の撤去では基準分しか返らないことを確かめる
            // Remove A first so the rail is refunded there; removing B must then return only the baseline
            var (baseReinforcing, baseIron) = RemoveAndMeasureGain(new Vector3Int(30, 0, 0));
            RemoveAndMeasureGain(Vector3Int.zero);
            var (reinforcing, iron) = RemoveAndMeasureGain(new Vector3Int(10, 0, 0));

            Assert.AreEqual(baseReinforcing, reinforcing);
            Assert.AreEqual(baseIron, iron);
        }

        [Test]
        public void 駅内部の区間は無償なので返却しない()
        {
            // 駅内部の区間はGuid.Emptyで張られるため返却対象にならない
            // Station-internal segments carry Guid.Empty, so they are never refunded
            var station = TrainTestHelper.PlaceBlock(_environment, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);
            var refundItems = RailRemovalRefundCalculator.CreateRefundItems(station, _environment.GetRailGraphDatastore());

            Assert.AreEqual(0, refundItems.Count);
        }

        [Test]
        public void レール返却がインベントリに入らなければInventoryFullで撤去を拒否する()
        {
            var railA = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(_environment, new Vector3Int(10, 0, 0), BlockDirection.North);
            var units = CalculateUnits(railA.FrontNode, railB.BackNode);
            ConnectWithTool(railA.FrontNode, railB.BackNode, units);

            // 返却素材と重ならない別アイテムの満杯スタックで全スロットを埋める
            // Fill every slot with full stacks of an unrelated item so the refund cannot merge anywhere
            var fillerItemId = MasterHolder.ItemMaster.GetItemId(FillerItemGuid);
            var fillerMaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(fillerItemId);
            for (var i = 0; i < _inventory.GetSlotSize(); i++) _inventory.SetItem(i, ServerContext.ItemStackFactory.Create(fillerItemId, fillerMaxStack));

            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(Vector3Int.zero));
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            var response = MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(responseBytes.ToArray());

            // 撤去されず、レールも残ることを確かめる
            // Verify nothing was removed and the rail is still there
            Assert.IsFalse(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.InventoryFull, response.FailureReason);
            Assert.IsTrue(_environment.WorldBlockDatastore.Exists(Vector3Int.zero));
            TrainTestHelper.Node2NodeCheckAndAssert(railA.FrontNode, railB.BackNode, "railA", "railB");
        }

        private int CalculateUnits(RailNode from, RailNode to)
        {
            var length = RailConnectionEditProtocol.GetRailLength(from, to);
            return Mathf.CeilToInt(length / 5f);
        }

        private void ConnectWithTool(RailNode from, RailNode to, int units)
        {
            // 接続に必要な素材をちょうど持たせ、レールconnectToolで接続する
            // Give exactly the required materials and connect with the rail connectTool
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_reinforcingMaterialId, units * 12));
            _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_ironPlateId, units * 5));
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateConnectRequest(from.NodeId, from.Guid, to.NodeId, to.Guid, RailConnectToolGuid);
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            var response = MessagePackSerializer.Deserialize<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(responseBytes.ToArray());
            Assert.IsTrue(response.Success, response.FailureReason.ToString());
        }

        private (int reinforcing, int iron) RemoveAndMeasureGain(Vector3Int position)
        {
            // インベントリを空にしてから撤去し、増えた分だけを返す
            // Empty the inventory, remove, and return only what was gained
            for (var i = 0; i < _inventory.GetSlotSize(); i++) _inventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(position));
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            Assert.IsTrue(MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(responseBytes.ToArray()).Success);
            return (CountItem(_reinforcingMaterialId), CountItem(_ironPlateId));
        }

        private int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}
```

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Train/RailSegmentPairingTest.cs`:

```csharp
using Game.Train.RailGraph.Utility;
using NUnit.Framework;

namespace Tests.UnitTest.Game.Train
{
    // 物理レール1本の向き違い2区間が同じキーになり、別のレールとは衝突しないことを確かめる
    // Both directed edges of one physical rail map to one key, and different rails never collide
    public class RailSegmentPairingTest
    {
        [Test]
        public void 向き違いの対は同じキーになる()
        {
            // A=4→B=7 の対は (7^1)→(4^1) = 6→5
            // The pair of A=4→B=7 is (7^1)→(4^1) = 6→5
            Assert.AreEqual((4, 7), RailSegmentPairing.SelectCanonicalPair(4, 7));
            Assert.AreEqual((4, 7), RailSegmentPairing.SelectCanonicalPair(6, 5));
        }

        [Test]
        public void 起点が同値の自己対は自分自身を返す()
        {
            // from == to^1 のとき対は自分と同じ区間になる
            // When from == to^1 the pair is the same edge
            Assert.AreEqual((2, 3), RailSegmentPairing.SelectCanonicalPair(2, 3));
        }

        [Test]
        public void 別のレールは別のキーになる()
        {
            Assert.AreNotEqual(RailSegmentPairing.SelectCanonicalPair(4, 7), RailSegmentPairing.SelectCanonicalPair(4, 9));
        }
    }
}
```

- [ ] **Step 2: 返却算出を新設する**

`moorestech_server/Assets/Scripts/Game.Train/RailGraph/Utility/RailSegmentPairing.cs`（新規）:

```csharp
namespace Game.Train.RailGraph.Utility
{
    /// <summary>
    /// 物理レール1本を表す有向区間の対（A→B と (B^1)→(A^1)）を1つのキーへ正規化する正本
    /// Canonical normalization of the directed-edge pair (A→B and (B^1)→(A^1)) that represents one physical rail
    /// </summary>
    public static class RailSegmentPairing
    {
        // 起点Idが小さい方を正とし、同値なら A→B を返す（同値のときは対が自分自身と一致する）
        // The pair with the smaller start id is canonical; ties return A→B (on a tie the pair equals itself)
        public static (int canonicalFrom, int canonicalTo) SelectCanonicalPair(int fromNodeId, int toNodeId)
        {
            var pairedFrom = toNodeId ^ 1;
            var pairedTo = fromNodeId ^ 1;
            return fromNodeId <= pairedFrom ? (fromNodeId, toNodeId) : (pairedFrom, pairedTo);
        }
    }
}
```


`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/RailEdit/RailRemovalRefundCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailGraph.Utility;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.RailEdit
{
    /// <summary>
    ///     レールを持つブロックの撤去で一緒に消えるレール区間の返却アイテムを算出する
    ///     Computes refund items for the rail segments that vanish when a rail-holding block is removed
    /// </summary>
    public static class RailRemovalRefundCalculator
    {
        public static List<IItemStack> CreateRefundItems(IBlock block, IRailGraphDatastore railGraphDatastore)
        {
            var refundItems = new List<IItemStack>();
            var countedRails = new HashSet<(int, int)>();

            // ブロックの全ノードから出る区間を見て、物理レール1本を1回だけ数える
            // Walk every edge leaving the block's nodes and count each physical rail once
            foreach (var railComponent in block.ComponentManager.GetComponents<RailComponent>())
            {
                AddRefundOfNode(railComponent.FrontNode);
                AddRefundOfNode(railComponent.BackNode);
            }

            return refundItems;

            #region Internal

            void AddRefundOfNode(RailNode node)
            {
                foreach (var (target, _) in railGraphDatastore.GetConnectedNodesWithDistance(node))
                {
                    if (!countedRails.Add(RailSegmentPairing.SelectCanonicalPair(node.NodeId, target.NodeId))) continue;
                    AddRefundOfSegment(node, target);
                }
            }

            void AddRefundOfSegment(IRailNode from, IRailNode to)
            {
                // 駅内部・駅隣接の自動接続はGuid.Emptyの無償区間なので返さない
                // Station-internal and adjacency auto links are costless Guid.Empty segments, so skip them
                if (!railGraphDatastore.TryGetRailSegmentType(from.NodeId, to.NodeId, out var connectToolGuid)) return;
                if (connectToolGuid == Guid.Empty) return;

                // 切断時と同じく現在の曲線長から返却素材を算出する
                // Compute the refund from the current curve length, exactly like a manual disconnect
                var length = RailConnectionEditProtocol.GetRailLength(from, to);
                if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, length, out var materials))
                {
                    Debug.LogWarning($"[RailRemovalRefund] refund skipped: cost not computable. connectToolGuid={connectToolGuid} length={length} from={from.NodeId} to={to.NodeId}");
                    return;
                }

                refundItems.AddRange(ConnectToolMaterialConsumer.CreateRefundItems(materials));
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: RemoveBlockProtocol の返却合算へ加える**

`RemoveBlockProtocol.cs` の using に `using Game.Train.RailGraph;` と `using Server.Protocol.PacketResponse.Util.RailEdit;` を足し、フィールドと ctor を次へ:

```csharp
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly ConstructionWalletService _constructionWallet;
        private readonly IRailGraphDatastore _railGraphDatastore;


        public RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _constructionWallet = serviceProvider.GetService<ConstructionWalletService>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
        }
```

`GetRefundItems()`（現 :104-130）の `IGetRefundItemsInfo` 取得の直後、`return result;` の前へ追加:

```csharp
                // 付いていたレール区間の素材も返す。区間はRemoveBlockで消えるため撤去前のここで算出する
                // Also refund the attached rail segments; they vanish in RemoveBlock, so compute them here beforehand
                result.AddRange(RailRemovalRefundCalculator.CreateRefundItems(block, _railGraphDatastore));
```

- [ ] **Step 4: 返却が入らない拒否を InventoryFull に分け、クライアントで理由を出す**

`RemoveBlockProtocol.cs:50` を:

```csharp
            // 返却がインベントリに入りきらなければ撤去しない（返却消滅の防止）
            // Refuse the removal when the refund cannot fit, so no refund is lost
            if (!TryInsertRefundItems(out var refundItems)) return RemoveBlockResponseMessagePack.CreateFailure(RemoveBlockFailureReason.InventoryFull);
```

`RemoveBlockFailureReason`（:183-188）を（末尾追加。既存値の並びは変えない）:

```csharp
        public enum RemoveBlockFailureReason
        {
            None,
            NodeInUseByTrain,
            Unknown,
            InventoryFull,
        }
```

`BlockGameObjectChild.cs` の `GetRemoveDeniedReasonKey`（:103-111）を:

```csharp
            static LocalizationKey? GetRemoveDeniedReasonKey(RemoveBlockProtocol.RemoveBlockFailureReason failureReason)
            {
                return failureReason switch
                {
                    RemoveBlockProtocol.RemoveBlockFailureReason.NodeInUseByTrain => LocalizationKeys.Ui.Delete.RailHasVehicle,
                    RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull => LocalizationKeys.Ui.Delete.InventoryFull,
                    RemoveBlockProtocol.RemoveBlockFailureReason.Unknown => LocalizationKeys.Ui.Delete.BlockDeleteFailed,
                    _ => null,
                };
            }
```

`Localization/localization.csv` の `ui.delete.unknownError` 行（:182）の直後へ1行追加（列は `key,Source,english,japanese,german,korean`）:

```csv
ui.delete.inventoryFull,Inventory is full. Make room for the refunded items before removing.,Inventory is full. Make room for the refunded items before removing.,インベントリがいっぱいです。返却されるアイテムの空きを作ってから撤去してください。,Das Inventar ist voll. Schaffe Platz für die zurückerstatteten Gegenstände.,인벤토리가 가득 찼습니다. 반환될 아이템의 공간을 확보한 뒤 철거하세요.
```

`LocalizationKeys` は csv から SourceGenerator で生成されるため、csv だけ変えた状態の通常コンパイルでは `LocalizationKeys.Ui.Delete.InventoryFull` が CS0117 になることがある（プロジェクトメモリ「localization.csvはforce-recompile」）。csv を変えたら必ず強制再コンパイルを1回挟む（force版はエラー本文を返さないので、その後に通常の compile で結果を読む）:
Run: `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true`

- [ ] **Step 5: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RailSegmentPairingTest|RemoveRailBlockRefundTest|RemoveTrainRailBlockProtocolTest|RemoveBlockRefundTest|RemoveBlockProtocolTest|DragDeleteDenyReasonTest"`
Expected: ErrorCount 0 / 全件 PASS（既存の撤去系テストが壊れていないこと）

webui の文言網羅を見ているテストがあれば同時に確かめる（csv 行追加のみなので通常は無影響）:
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Localization"`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Train/RailGraph/Utility/RailSegmentPairing.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Train/RailSegmentPairingTest.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/RailEdit/RailRemovalRefundCalculator.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemoveBlockProtocolTest/RemoveRailBlockRefundTest.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs Localization/localization.csv
git commit -m "feat: 橋脚・駅の撤去でレール素材を返却し、返却が入らない撤去はInventoryFullで拒否する"
```
（.meta は Unity が生成したものを同じコミットへ含める。手書きしない）

---

### Task 6 (B2): 設置要求に配線方式（電線自動接続あり／なし）を持たせる

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs`（末尾へ enum 追加）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs:107-127`（自動接続の評価・実行）, `:133-146`（`SendPlaceBlockProtocolMessagePack`）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:43-47`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceBlockProtocolSender.cs:33`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/RemoveOperationRecord.cs:72`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainCostIntegration/PlacementPacketCapture.cs:49`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTestSupport.cs:78-81`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest/ElectricWireAutoConnectPlaceTestBase.cs:72-86`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Construction/ConstructionPayerWalletTest.cs:116`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/free-placement-locked-block.cs:71`（録画シナリオは実行時コンパイルのため1引数呼び出しのままだと落ちる。`grep -rn "\.PlaceBlock(" .agents/skills/unity-playmode-recorded-playtest/scenarios` の該当はこの1件のみ）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest/PlaceBlockNoAutoConnectWiringTest.cs`

**Interfaces:**
- Consumes: なし（既存の `ElectricWireAutoConnectService` をそのまま使う）
- Produces:
  - `public enum BlockPlacementWiring { AutoConnect, NoAutoConnect }`（名前空間 `Server.Protocol.PacketResponse`、`PlacePacketDto.cs`）
  - `PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(List<PlaceInfo> placeInfos, BlockPlacementWiring wiring)`、`[Key(4)] public BlockPlacementWiring Wiring`
  - クライアント `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo> placePositions, BlockPlacementWiring wiring)`
  - `RemoveOperationRecord.UndoAsync` は `BlockPlacementWiring.NoAutoConnect` で送る（契約 11 の Undo 再設計タスクはこの呼び方を引き継ぐ）

- [ ] **Step 1: テストを書く**

`ElectricWireAutoConnectPlaceTestBase.cs` の `PlaceBlock`（:72-86）を次の2メソッドへ置き換える（既存テストは `PlaceBlock` を呼び続け、挙動は不変）:

```csharp
        protected static void PlaceBlock(PacketResponseCreator packet, BlockId blockId, Vector3Int position)
        {
            PlaceBlockWithWiring(packet, blockId, position, BlockPlacementWiring.AutoConnect);
        }

        protected static void PlaceBlockWithWiring(PacketResponseCreator packet, BlockId blockId, Vector3Int position, BlockPlacementWiring wiring)
        {
            var placeInfo = new List<PlaceInfo>
            {
                new()
                {
                    Position = position,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                },
            };

            var payload = MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(placeInfo, wiring));
            packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
        }
```

新規 `PlaceBlockNoAutoConnectWiringTest.cs`:

```csharp
using System;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceBlockNoAutoConnectWiringTest : ElectricWireAutoConnectPlaceTestBase
    {
        [Test]
        public void 記録どおりのみで設置すると範囲内に機械があっても電線を張らず消費もしない()
        {
            // 電柱の機械範囲内に機械を置き、電線を持たせた状態で電柱を記録どおりのみで設置する
            // Put a machine in the pole's machine range and place the pole with NoAutoConnect while holding wires
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = SetupWire(serviceProvider, 5);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlockWithWiring(packet, ForUnitTestModBlockId.ElectricPoleId, Vector3Int.zero, BlockPlacementWiring.NoAutoConnect);

            var pole = worldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsNotNull(pole);
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, machine.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(5, GetWireCount(inventory));
        }

        [Test]
        public void 記録どおりのみなら電線ゼロでも電線不足で拒否されない()
        {
            // 自動接続なら電線不足で拒否される配置でも、記録どおりのみは設置される
            // A layout that auto-connect rejects for wire shortage is still placed under NoAutoConnect
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            SetupWire(serviceProvider, 0);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlockWithWiring(packet, ForUnitTestModBlockId.ElectricPoleId, Vector3Int.zero, BlockPlacementWiring.NoAutoConnect);

            Assert.IsTrue(worldBlockDatastore.Exists(Vector3Int.zero));
        }
    }
}
```

- [ ] **Step 2: サーバー実装**

`PlacePacketDto.cs` の名前空間末尾（`PlaceInfo` クラスの後）へ追加:

```csharp
    /// <summary>
    ///     設置時の電線の張り方。AutoConnectは通常設置の自動接続、NoAutoConnectは電線の自動接続を行わず後続の明示接続に任せる（Undo再設置用）
    ///     How wires are laid on placement: AutoConnect runs the normal auto-connect, NoAutoConnect skips electric auto-connect and leaves wiring to explicit follow-up connects (undo re-placement)
    /// </summary>
    public enum BlockPlacementWiring
    {
        AutoConnect,
        NoAutoConnect,
    }
```

`PlaceBlockProtocol.cs:107-117` を次へ置き換え（判定を1か所に集める）:

```csharp
                // 自動接続設置の電気ブロックだけ事前検証し、電線不足ならスキップする（記録どおりのみは配線を後続の明示接続に任せる）
                // Pre-validate only auto-connect electric placements; NoAutoConnect leaves wiring to explicit follow-up connects
                var isAutoConnectElectric = data.Wiring == BlockPlacementWiring.AutoConnect && ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out _, out _);
                var plan = default(ElectricWireAutoConnectPlan);
                if (isAutoConnectElectric)
                {
                    // 建設コストで消費予定の素材を予約として渡し、電線の所持数判定から除外する
                    // Pass construction-cost materials as reservations to exclude them from wire availability
                    plan = ElectricWireAutoConnectService.EvaluateAutoConnect(placeBlockId, placeInfo.Position, placeInfo.Direction, placementPlan.ItemsToConsume, inventory.InventoryItems);
                    if (!plan.IsPlaceable) { wireShortageCount++; return; }
                }
```

`:125-127` を:

```csharp
                // 計画を実行しワイヤー消費
                // Execute the validated plan: add wires and consume wire items
                if (isAutoConnectElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
```

`SendPlaceBlockProtocolMessagePack`（:133-146）を:

```csharp
        [MessagePackObject]
        public class SendPlaceBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(3)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }
            [Key(4)] public BlockPlacementWiring Wiring { get; set; }

            public SendPlaceBlockProtocolMessagePack(List<PlaceInfo> placeInfos, BlockPlacementWiring wiring)
            {
                Tag = ProtocolTag;
                PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
                Wiring = wiring;
            }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendPlaceBlockProtocolMessagePack() { }
        }
```

- [ ] **Step 3: 全呼び出し側を更新する（デフォルト引数は使わない）**

`VanillaApiSendOnly.cs:43-47`:

```csharp
        public void PlaceBlock(List<PlaceInfo> placePositions, BlockPlacementWiring wiring)
        {
            var request = new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(placePositions, wiring);
            _packetSender.Send(request);
        }
```

`PlaceBlockProtocolSender.cs:33`:

```csharp
            ClientContext.VanillaApi.SendOnly.PlaceBlock(currentPlaceInfos, BlockPlacementWiring.AutoConnect);
```

`RemoveOperationRecord.cs:72`（Undo再設置は自動接続を止め、記録した線だけを後で引き直す。裁定 `.decisions/2026-10-04-Undoの再設置では自動接続を止め記録した線だけ引き直す.md`）:

```csharp
            if (placeInfos.Count != 0) ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos, BlockPlacementWiring.NoAutoConnect);
```

`PlacementPacketCapture.cs:49`:

```csharp
            Api.SendOnly.PlaceBlock(new List<PlaceInfo>(), BlockPlacementWiring.AutoConnect);
```

`PlaceBlockProtocolTestSupport.cs:78-81`:

```csharp
        public static byte[] CreatePlacePayload(List<PlaceInfo> placeInfos)
        {
            return MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(placeInfos, BlockPlacementWiring.AutoConnect));
        }
```

`ConstructionPayerWalletTest.cs:116`:

```csharp
            var payload = MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(placeInfos, BlockPlacementWiring.AutoConnect));
```

`.agents/skills/unity-playmode-recorded-playtest/scenarios/building/free-placement-locked-block.cs:71`（シナリオ先頭の using に `Server.Protocol.PacketResponse` が無ければ足す）:

```csharp
        ClientContext.VanillaApi.SendOnly.PlaceBlock(new List<PlaceInfo> { placeInfo }, BlockPlacementWiring.AutoConnect);
```

（各ファイルに `using Server.Protocol.PacketResponse;` が無ければ足す。`PlaceInfo` を使っている箇所は既に同名前空間を参照している）

- [ ] **Step 4: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockNoAutoConnectWiringTest|ElectricWireAutoConnect|PlaceBlockProtocol|ConstructionPayerWalletTest|PlacementPacketCapture|TrainPierPlacementEntryCostTest"`
Expected: ErrorCount 0 / 全件 PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceBlockProtocolSender.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/RemoveOperationRecord.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainCostIntegration/PlacementPacketCapture.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTestSupport.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Construction/ConstructionPayerWalletTest.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/building/free-placement-locked-block.cs
git commit -m "feat: 設置要求に配線方式を持たせUndo再設置では電線の自動接続を止める"
```

---

### Task 7 (B3): 座標で同定したレール端点同士を接続するプロトコル

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectByDestinationProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:66-67`（登録1行）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:113-123`（`ConnectRail`/`DisconnectRail` の並びへ1メソッド追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rail/RailConnectByDestinationProtocolTest.cs`

**Interfaces:**
- Consumes: 既存 `RailConnectionEditService.ExecuteEdit(RailConnectionEditRequest, int)`（internal・同アセンブリ）、`RailConnectionEditRequest.CreateConnectRequest(int, Guid, int, Guid, Guid)`、`IRailGraphProvider.ResolveRailNode(ConnectionDestination)`、`ConnectionDestinationMessagePack`
- Produces:
  - `RailConnectByDestinationProtocol.Tag = "va:railConnectByDestination"`
  - `RailConnectByDestinationProtocol.RailConnectByDestinationRequest(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)`（`[Key(2)] From`, `[Key(3)] To`, `[Key(4)] ConnectToolGuid`）
  - **応答なし**（`GetResponse` は `PlaceBlockProtocol` と同じく `null` を返す。専用応答型も既存 `ResponseRailConnectionEditMessagePack` の流用も作らない。コーディネーター裁定）。失敗は `denied.railEdit.{RailConnectionEditFailureReason}` を `NotificationService` で通知し、端点未解決は `Debug.LogWarning` も出す
  - 既に同じ向きで接続済みなら素材を消費せず何もしない（`Debug.Log` で記録、通知なし）。Undo で駅の隣接自動接続などが先に復元した区間を二重課金・拒否通知しないため
  - **`Guid.Empty` の区間はこのプロトコルへ送られない前提**: 駅内部・駅隣接の自動接続（`Guid.Empty`）は駅の再設置で自動復元されるため、クライアントの Undo 記録（契約 11 の `RemovedRail` 収集・C 側タスク）が記録対象から除外する。サーバー側は特別扱いせず、万一届いても既存判定どおり `NotUnlocked` で拒否・通知される（`RailConnectionEditService.cs:53-54`）
  - クライアント `VanillaApiSendOnly.ConnectRailByDestination(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)`（契約 11 の `RemovedRail` 復元が呼ぶ）

既存の接続ロジック（解放判定・`EvaluatePlacement`・`TryConnect`・素材消費）は `RailConnectionEditService.ExecuteEdit` を**そのまま呼ぶ**ことで共有し、抽出・複製はしない。座標→現在の NodeId/Guid の解決と、既接続のスキップだけがこのプロトコル固有。`ExecuteEdit` が返す応答は失敗理由の取得にだけ使い、送り返さない。

- [ ] **Step 1: テストを書く**

応答が無いため、テストは「応答パケットが0件」「グラフ上の接続」「インベントリの素材数」で観測する。

```csharp
using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Rail
{
    public class RailConnectByDestinationProtocolTest
    {
        private const int PlayerId = 12;
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        private static readonly Guid ReinforcingMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private static readonly Guid IronPlateGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Vector3Int ToRailPosition = new(10, 0, 0);

        private TrainTestEnvironment _environment;
        private IOpenableInventory _inventory;
        private RailNode _fromNode;
        private RailNode _toNode;
        private ItemId _reinforcingMaterialId;
        private ItemId _ironPlateId;

        [SetUp]
        public void SetUp()
        {
            // 接続対象のレール端点と解放済みconnectToolを準備する
            // Prepare rail endpoints and the unlocked connectTool
            _environment = TrainTestHelper.CreateEnvironment();
            _fromNode = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North).FrontNode;
            _toNode = TrainTestHelper.PlaceRail(_environment, ToRailPosition, BlockDirection.North).BackNode;
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _environment.ServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);
            _reinforcingMaterialId = MasterHolder.ItemMaster.GetItemId(ReinforcingMaterialGuid);
            _ironPlateId = MasterHolder.ItemMaster.GetItemId(IronPlateGuid);
        }

        [Test]
        public void 座標で同定したノード同士を接続し素材を消費する()
        {
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 12, units * 5);

            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination);

            Assert.AreEqual(0, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(0, CountItem(_ironPlateId));
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, _toNode, "fromNode", "toNode");
        }

        [Test]
        public void 再設置でノードIdとGuidが変わっても座標から解決して接続できる()
        {
            // 終点の橋脚を撤去・再設置し、旧Guidが無効になった状態を作る
            // Remove and re-place the target pier so the old guid becomes invalid
            var destination = _toNode.ConnectionDestination;
            var oldGuid = _toNode.Guid;
            _environment.WorldBlockDatastore.RemoveBlock(ToRailPosition, BlockRemoveReason.ManualRemove);
            var newToNode = TrainTestHelper.PlaceRail(_environment, ToRailPosition, BlockDirection.North).BackNode;
            Assert.AreNotEqual(oldGuid, newToNode.Guid);

            var units = CalculateUnits(_fromNode, newToNode);
            SetInventory(units * 12, units * 5);
            Send(_fromNode.ConnectionDestination, destination);

            Assert.AreEqual(0, CountItem(_reinforcingMaterialId));
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, newToNode, "fromNode", "newToNode");
        }

        [Test]
        public void 端点が無い座標は接続せず何も消費しない()
        {
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 12, units * 5);
            var missing = new ConnectionDestination(new Vector3Int(99, 0, 99), 0, false);

            Send(_fromNode.ConnectionDestination, missing);

            Assert.IsFalse(_fromNode.ConnectedNodes.Any());
            Assert.AreEqual(units * 12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(units * 5, CountItem(_ironPlateId));
        }

        [Test]
        public void 素材不足なら接続せず何も消費しない()
        {
            // 既存の接続判定（EvaluatePlacement）をそのまま通っていることを確かめる
            // Verify the existing connect judgement (EvaluatePlacement) is applied as-is
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 12, units * 5 - 1);

            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination);

            Assert.IsFalse(_fromNode.ConnectedNodes.Any());
            Assert.AreEqual(units * 12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(units * 5 - 1, CountItem(_ironPlateId));
        }

        [Test]
        public void 既に接続済みなら二重に消費しない()
        {
            // 2本分の素材を持たせて2回送り、2回目は消費しないことを確かめる
            // Hold materials for two rails, send twice, and verify the second send consumes nothing
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 24, units * 10);

            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination);
            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination);

            Assert.AreEqual(units * 12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(units * 5, CountItem(_ironPlateId));
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, _toNode, "fromNode", "toNode");
        }

        private int CalculateUnits(RailNode from, RailNode to)
        {
            var length = RailConnectionEditProtocol.GetRailLength(from, to);
            return Mathf.CeilToInt(length / 5f);
        }

        private void SetInventory(int reinforcingCount, int ironPlateCount)
        {
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_reinforcingMaterialId, reinforcingCount));
            _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_ironPlateId, ironPlateCount));
        }

        private void Send(ConnectionDestination from, ConnectionDestination to)
        {
            // SendOnly前提のプロトコルなので応答パケットは返らない
            // The protocol is SendOnly, so no response packet comes back
            var request = new RailConnectByDestinationProtocol.RailConnectByDestinationRequest(from, to, ConnectToolGuid);
            var responses = _environment.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.AreEqual(0, responses.Count);
        }

        private int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}
```

（`PacketResponseCreator.GetPacketResponse` は `null` 応答を空リストで返す：`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:111` で確認済み）

- [ ] **Step 2: プロトコルを新設する**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectByDestinationProtocol.cs`:

```csharp
using System;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;
using Server.Protocol.PacketResponse.Util.RailEdit;
using Server.Util.MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     ブロック座標で同定したレール端点同士を接続する。再設置でノードId/Guidが変わるUndo復元が使う。応答は返さない
    ///     Connects rail endpoints identified by block position; used by undo restore, where re-placement changes node ids and guids. Sends no response
    /// </summary>
    public class RailConnectByDestinationProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectByDestination";

        private readonly RailConnectionEditService _editService;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly NotificationService _notificationService;

        public RailConnectByDestinationProtocol(ServiceProvider serviceProvider)
        {
            _editService = new RailConnectionEditService(serviceProvider);
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _notificationService = serviceProvider.GetService<NotificationService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RailConnectByDestinationRequest>(payload);
            Connect(request, requesterPlayerId);
            return null;

            #region Internal

            void Connect(RailConnectByDestinationRequest data, int playerId)
            {
                // 座標から現在のノードを解決する（再設置後はId・Guidが撤去前と違う）
                // Resolve the current nodes from positions (ids and guids differ from before removal)
                var fromNode = _railGraphDatastore.ResolveRailNode(data.From.ToModel());
                var toNode = _railGraphDatastore.ResolveRailNode(data.To.ToModel());
                if (fromNode == null || toNode == null)
                {
                    Debug.LogWarning($"[RailConnectByDestination] endpoint not found. from={data.From.BlockPosition.Vector3Int}/{data.From.ComponentIndex}/{data.From.IsFrontSide} to={data.To.BlockPosition.Vector3Int}/{data.To.ComponentIndex}/{data.To.IsFrontSide}");
                    NotifyDenied(RailConnectionEditFailureReason.InvalidNode);
                    return;
                }

                // 既に繋がっていれば二重課金せず何もしない（駅の隣接自動接続などで先に復元済み）
                // Already connected: do nothing and charge nothing (e.g. already restored by station adjacency)
                if (_railGraphDatastore.GetConnectedNodesWithDistance(fromNode).Any(connected => connected.Item1.NodeId == toNode.NodeId))
                {
                    Debug.Log($"[RailConnectByDestination] already connected, skipped. from={fromNode.NodeId} to={toNode.NodeId}");
                    return;
                }

                // 解決したId/Guidで既存の接続処理（解放・長さ・素材の判定と消費）をそのまま通し、失敗理由だけ通知へ回す
                // Run the existing connect path (unlock, length, material check and consumption) with the resolved id/guid; only forward a failure reason
                var editRequest = RailConnectionEditRequest.CreateConnectRequest(fromNode.NodeId, fromNode.NodeGuid, toNode.NodeId, toNode.NodeGuid, data.ConnectToolGuid);
                var result = _editService.ExecuteEdit(editRequest, playerId);
                if (!result.Success) NotifyDenied(result.FailureReason);
            }

            // 応答を返さないため、拒否理由は通知でプレイヤーへ届ける
            // No response is sent, so deliver the denial reason to the player as a notification
            void NotifyDenied(RailConnectionEditFailureReason reason)
            {
                _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.railEdit.{reason}", Array.Empty<string>()));
            }

            #endregion
        }

        [MessagePackObject]
        public class RailConnectByDestinationRequest : ProtocolMessagePackBase
        {
            [Key(2)] public ConnectionDestinationMessagePack From { get; set; }
            [Key(3)] public ConnectionDestinationMessagePack To { get; set; }
            [Key(4)] public Guid ConnectToolGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectByDestinationRequest() { Tag = RailConnectByDestinationProtocol.Tag; }

            public RailConnectByDestinationRequest(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
            {
                Tag = RailConnectByDestinationProtocol.Tag;
                From = new ConnectionDestinationMessagePack(from);
                To = new ConnectionDestinationMessagePack(to);
                ConnectToolGuid = connectToolGuid;
            }
        }
    }
}
```

`PacketResponseCreator.cs` の `RailConnectionEditProtocol` 登録行（:66）の直後へ:

```csharp
            _packetResponseDictionary.Add(RailConnectByDestinationProtocol.Tag, new RailConnectByDestinationProtocol(serviceProvider));
```

- [ ] **Step 3: クライアント送信APIを足す（1プロトコル＝1メソッド）**

`VanillaApiSendOnly.cs` の `DisconnectRail`（:119-123）の直後へ。using に `using Game.Train.SaveLoad;` を足す:

```csharp
        public void ConnectRailByDestination(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
        {
            var request = new RailConnectByDestinationProtocol.RailConnectByDestinationRequest(from, to, connectToolGuid);
            _packetSender.Send(request);
        }
```

- [ ] **Step 4: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RailConnectByDestinationProtocolTest|RailConnectionEditProtocolTest"`
Expected: ErrorCount 0 / 全件 PASS

webui の通知 id 網羅テスト（`moorestech_web/webui/src/features/notification/notificationServerIdCoverage.test.ts`）は `.cs` 内の `denied.*` を走査する。新プロトコルは既存の `denied.railEdit.` 接頭辞と既存 enum だけを使うが、走査が補間接頭辞の出現ファイルを列挙する実装なら落ちるため、実行して確かめる:
Run: `cd moorestech_web/webui && pnpm vitest run src/features/notification/notificationServerIdCoverage.test.ts`
Expected: PASS（落ちた場合は同テストの補間接頭辞表へ `RailConnectByDestinationProtocol.cs` の出現を足す）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectByDestinationProtocol.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rail/RailConnectByDestinationProtocolTest.cs
git commit -m "feat: 座標で同定したレール端点同士を接続するプロトコルを追加"
```

---


## C群の前提事実

前提: サーバー側タスク（契約 1〜7）が先に入っていること。特に以下を消費する:
- `Game.Block.Blocks.ConnectionLine.ConnectionLinePartnerMessagePack`（`[Key(0)] int PartnerBlockInstanceId`, `[Key(1)] Guid ConnectToolGuid`）
- `ElectricWireStateDetail.Partners` / `GearChainPoleStateDetail.Partners`（`ConnectionLinePartnerMessagePack[]`）
- `BlockPlacementWiring { AutoConnect, NoAutoConnect }` と `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo>, BlockPlacementWiring)`
- `VanillaApiSendOnly.ConnectRailByDestination(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)`
- `VanillaApiSendOnly.DisconnectGearChain(Vector3Int posA, Vector3Int posB)`（Task A4 が定義。本群は消費のみ）
- `ConnectionLinePartnerMessagePack` の ctor `(int partnerBlockInstanceId, Guid connectToolGuid)` とフィールド `PartnerBlockInstanceId` / `ConnectToolGuid`（Task A3）
- Task A3 は状態処理側で `Partners` を一旦 `BlockInstanceId[]` へ写すだけにしてある。本群 Task C2 がその写しを `ConnectionLinePartner` へ置き換える
- Task B2 は `RemoveOperationRecord.cs:72` を `PlaceBlock(placeInfos, BlockPlacementWiring.NoAutoConnect)` へ書き換え済み。本群 Task C4 はこのファイルを全面書き換えし、その呼び出しを `VanillaRemovalRestoreSender.PlaceBlocks` へ移す
- Task B は `BlockGameObjectChild` の撤去拒否理由の写像に `InventoryFull → ui.delete.inventoryFull` を足す。本群の `BlockGameObjectChild` 変更（C4・C5）は `SetRemovePreviewing` / `ResetMaterial` / `CollectRemovedObjects` の3メソッドだけで、`GetRemoveDeniedReasonKey` には触れない

パス略記: `C = moorestech_client/Assets/Scripts`。Files節はリポジトリ相対で書く。

---

### Task 8 (C1): Unityレイヤー "ElectricWire" を "ConnectionLine" へ改名し LayerConst を追従させる

**Files:**
- Modify: `moorestech_client/ProjectSettings/TagManager.asset`（**テキスト編集禁止**。Step 2 の uloop execute-dynamic-code 経由でのみ変更する）
- Modify: `moorestech_client/Assets/Scripts/Client.Common/LayerConst.cs:12,23,25-28`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireLineViewElement.cs:97-99`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:46`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/GameObjectToolTipTargetController.cs:47`（接続線レイヤーを除外。チェーンへ当たり判定を足すと奥の物のツールチップを遮るため。線は `GameObjectTooltipTarget` を持たないので除外で失うツールチップは無い）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Control/ConnectionLineLayerTest.cs`

**Interfaces:**
- Consumes: なし
- Produces: `LayerConst.ConnectionLineLayer`（int）, `LayerConst.ConnectionLineOnlyLayerMask`（int）。旧 `ElectricWireLayer` / `ElectricWireOnlyLayerMask` は削除

- [ ] **Step 1: テストを書く**

```csharp
using Client.Common;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Control
{
    /// <summary>
    ///     接続線レイヤーの名前と番号を固定するテスト
    ///     Test pinning the connection-line layer name and index
    /// </summary>
    public class ConnectionLineLayerTest
    {
        [Test]
        public void ConnectionLineLayerReplacesElectricWireLayer()
        {
            // 旧ElectricWireと同じ番号11のまま改名されている
            // Renamed in place, keeping the old ElectricWire index 11
            Assert.AreEqual(11, LayerMask.NameToLayer("ConnectionLine"));
            Assert.AreEqual(-1, LayerMask.NameToLayer("ElectricWire"));
            Assert.AreEqual(1 << 11, LayerConst.ConnectionLineOnlyLayerMask);
        }
    }
}
```

- [ ] **Step 2: Unity Editor経由でレイヤーを改名する**

`uloop execute-dynamic-code --project-path ./moorestech_client` に次のコードを渡す（レイヤー番号は変えないので、Prefab・物理衝突行列の参照は壊れない）:

```csharp
// TagManagerのレイヤー11を名前だけ差し替える
// Rename layer 11 in the TagManager, keeping its index
var tagManagerAsset = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
var tagManager = new UnityEditor.SerializedObject(tagManagerAsset);
var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(11);
if (layer.stringValue != "ElectricWire") return $"unexpected layer 11 name: {layer.stringValue}";
layer.stringValue = "ConnectionLine";
tagManager.ApplyModifiedProperties();
UnityEditor.AssetDatabase.SaveAssets();
return "renamed layer 11 to ConnectionLine";
```

Expected: `renamed layer 11 to ConnectionLine`。`git diff moorestech_client/ProjectSettings/TagManager.asset` が `- ElectricWire` → `+ ConnectionLine` の1行だけであることを確認する。

- [ ] **Step 3: LayerConst と参照箇所を追従させる**

`LayerConst.cs` の該当行を置換:

```csharp
        public static readonly int ConnectionLineLayer = LayerMask.NameToLayer("ConnectionLine");
```
```csharp
        public static readonly int ConnectionLineOnlyLayerMask = 1 << ConnectionLineLayer;

        // 接続線は削除ツールとスポイトだけが狙うため、汎用レイキャストから除外する
        // Connection lines are aimed only by the delete tool and the eyedropper, so exclude them from generic raycasts
        public static readonly int Without_Player_MapObject_Block_LayerMask = ~MapObjectOnlyLayerMask & ~PlayerOnlyLayerMask & ~BlockOnlyLayerMask & ~ConnectionLineOnlyLayerMask;
        public static readonly int Without_Player_MapObject_BlockBoundingBox_LayerMask = ~MapObjectOnlyLayerMask & ~PlayerOnlyLayerMask & ~BlockBoundingBoxOnlyLayerMask & ~ConnectionLineOnlyLayerMask;
```

`ElectricWireLineViewElement.cs:99` を `colliderObject.layer = LayerConst.ConnectionLineLayer;`、`BlockClickDetectUtil.cs:46` の `LayerConst.ElectricWireOnlyLayerMask` を `LayerConst.ConnectionLineOnlyLayerMask` に置換する（このメソッド自体は Task C6 で接続線全般へ改名する）。

- [ ] **Step 3b: ツールチップのレイキャストから接続線レイヤーを外す**

`GameObjectToolTipTargetController.TryGetOnCursorTooltipTarget` の L47 を置換する:

```csharp
            // 接続線は当たり判定だけでツールチップを持たないため、奥の対象を遮らないよう除外する
            // Connection lines carry hit colliders but no tooltip, so exclude them to avoid occluding targets behind
            if (!Physics.Raycast(ray, out var hit, 100, ~LayerConst.ConnectionLineOnlyLayerMask)) return false;
```

ファイル先頭に `using Client.Common;` が無ければ足す。

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Control.ConnectionLineLayerTest"`
Expected: ErrorCount 0 / PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/ProjectSettings/TagManager.asset moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/GameObjectToolTipTargetController.cs moorestech_client/Assets/Scripts/Client.Common/LayerConst.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireLineViewElement.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs moorestech_client/Assets/Scripts/Client.Tests/Control/ConnectionLineLayerTest.cs
git commit -m "refactor(client): ElectricWireレイヤーを接続線共通のConnectionLineへ改名"
```

---

### Task 9 (C2): 接続線の削除対象・索引・チェーン当たり判定と、種類付き Partners の受信

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineKind.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLinePartner.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineDeleteTarget.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineRegistry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/IRemovePreviewable.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/RemovePreviewRequests.cs`（赤プレビューの要求者集合。電線・チェーン・レールが共有する唯一の書き手判定。裁定 `.decisions/2026-10-05-計画の弱発火4件を拾い残り3件は据え置く.md` (1)）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/BlockMaster.cs:20-23`（`ConnectionLineDestructionCategory` 定数を既定カテゴリーの隣へ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Common/BlockMasterElementExtension.cs:8-10`（クライアントへ再公開）
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/BlockDestructionCategoryValidator.cs`（既存の破壊カテゴリ検証を `BlockMasterUtil.cs` から移し、予約キー `connectionLine` の衝突検出を足す）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs:17,243-272`（ローカル関数を削除し新検証の呼び出しへ。403行の既存ファイルを約30行縮める）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/BlockDestructionCategoryReservedKeyTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/ConnectionLine/RemovePreviewRequestsTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineViewBase.cs:17-111`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireLineView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/GearPole/GearChainPoleChainLineView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/GearPole/GearChainPoleChainLineViewElement.cs:23-54`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireStateChangeProcessor.cs:33-55`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/GearChainPoleStateChangeProcessor.cs:22-37`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs:146`（登録追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/ConnectionLine/ConnectionLineRegistryTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/ConnectionLine/ConnectionLinePartnerTest.cs`

**Interfaces:**
- Consumes: Task C1 `LayerConst.ConnectionLineLayer`。Task A3 `ConnectionLinePartnerMessagePack`（ctor `(int, Guid)`）と `ElectricWireStateDetail.Partners` / `GearChainPoleStateDetail.Partners`、Task A4 `VanillaApiSendOnly.DisconnectGearChain(Vector3Int, Vector3Int)`
- Produces:
  - `public enum ConnectionLineKind { ElectricWire, GearChain }`
  - `public readonly struct ConnectionLinePartner { BlockInstanceId PartnerId; Guid ConnectToolGuid; static ConnectionLinePartner[] FromMessagePacks(ConnectionLinePartnerMessagePack[] packs) }`
  - `public class ConnectionLineDeleteTarget : MonoBehaviour, IDeleteTarget, IRemovePreviewable` — `void Initialize(BlockInstanceId fromId, BlockInstanceId toId, Guid connectToolGuid, ConnectionLineKind kind, ConnectionLineRegistry registry)`、`BlockInstanceId FromId`、`BlockInstanceId ToId`、`Guid ConnectToolGuid`、`ConnectionLineKind Kind`（いずれも `{ get; private set; }`）、`bool TryResolveEndpointPositions(out Vector3Int fromPos, out Vector3Int toPos)`（両端ブロック座標の解決。`Delete` と Task 11 の `CollectRemovedObjects` が共有）。`IDeleteTarget.SetRemovePreviewing()`/`ResetMaterial()` は自分自身を要求者として `RequestRemovePreview(this)`/`ReleaseRemovePreview(this)` を呼ぶ
  - `public class ConnectionLineRegistry` — `void Register(ConnectionLineDeleteTarget line)`、`void Unregister(ConnectionLineDeleteTarget line)`、`IReadOnlyList<ConnectionLineDeleteTarget> GetLinesAttachedTo(BlockInstanceId blockId)`
  - `public interface IRemovePreviewable { void RequestRemovePreview(object requester); void ReleaseRemovePreview(object requester); }`（`C/Client.Game/InGame/UI/UIState/State/IRemovePreviewable.cs`。誰かが要求している間は赤、最後の要求が外れたら戻る）
  - `public class RemovePreviewRequests { bool Add(object requester); bool Remove(object requester); }`（`Add` は最初の要求で true＝赤を付ける合図、`Remove` は最後の要求が外れたとき true＝戻す合図）
  - `Core.Master.BlockMaster.ConnectionLineDestructionCategory = "connectionLine"`（既定カテゴリー `DefaultDestructionCategory` の隣）、クライアント再公開 `Client.Game.Common.BlockMasterElementExtension.ConnectionLineDestructionCategory`。マスタの `blockDestructionCategories[].categoryKey` がこの値と一致したら BlockMaster 検証で拒否する
  - `ConnectionLineViewBase<TElement>.UpdateConnectionLines(IReadOnlyList<ConnectionLinePartner> partners)` と `protected abstract ConnectionLineKind GetLineKind()`
  - `ClientDIContext.ConnectionLineRegistry`（static, `{ get; private set; }`）

注: このタスク時点の `ConnectionLineDeleteTarget` は IDeleteTarget の既存6メンバーだけを実装する。`CollectRemovedObjects` は Task C4 で IDeleteTarget に追加された時点で実装する。

- [ ] **Step 1: テストを書く**

`ConnectionLineRegistryTest.cs`:

```csharp
using System;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.ConnectionLine
{
    /// <summary>
    ///     接続線索引が両端ブロックから線を引けることを検証する
    ///     Verifies the connection-line registry resolves lines from either endpoint block
    /// </summary>
    public class ConnectionLineRegistryTest
    {
        [Test]
        public void LineIsAttachedToBothEndpointsUntilUnregistered()
        {
            // 1本の線を登録すると両端どちらからも引ける
            // A registered line is reachable from both endpoints
            var registry = new ConnectionLineRegistry();
            var line = new GameObject("Line").AddComponent<ConnectionLineDeleteTarget>();
            line.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), Guid.NewGuid(), ConnectionLineKind.ElectricWire, registry);

            Assert.AreSame(line, registry.GetLinesAttachedTo(new BlockInstanceId(1))[0]);
            Assert.AreSame(line, registry.GetLinesAttachedTo(new BlockInstanceId(2))[0]);
            Assert.AreEqual(0, registry.GetLinesAttachedTo(new BlockInstanceId(3)).Count);

            // 破棄で索引から外れる
            // Destruction removes the line from the registry
            UnityEngine.Object.DestroyImmediate(line.gameObject);
            Assert.AreEqual(0, registry.GetLinesAttachedTo(new BlockInstanceId(1)).Count);
            Assert.AreEqual(0, registry.GetLinesAttachedTo(new BlockInstanceId(2)).Count);
        }

        [Test]
        public void LineCategoryIsConnectionLine()
        {
            // 接続線はブロックと別の破壊カテゴリーに属する
            // Connection lines belong to a destruction category separate from blocks
            var registry = new ConnectionLineRegistry();
            var line = new GameObject("Line").AddComponent<ConnectionLineDeleteTarget>();
            line.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), Guid.NewGuid(), ConnectionLineKind.GearChain, registry);

            Assert.AreEqual(Client.Game.Common.BlockMasterElementExtension.ConnectionLineDestructionCategory, line.GetDestructionCategory());
            Assert.IsTrue(line.IsRemovable(out var reason));
            Assert.IsFalse(reason.HasValue);
            UnityEngine.Object.DestroyImmediate(line.gameObject);
        }
    }
}
```

`ConnectionLinePartnerTest.cs`:

```csharp
using System;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Game.Block.Blocks.ConnectionLine;
using NUnit.Framework;

namespace Client.Tests.ConnectionLine
{
    /// <summary>
    ///     状態詳細の Partners をクライアント表現へ写す変換を検証する
    ///     Verifies conversion of the state-detail Partners into the client representation
    /// </summary>
    public class ConnectionLinePartnerTest
    {
        [Test]
        public void FromMessagePacksKeepsIdAndToolGuid()
        {
            // IDと種類の組を順序どおり写す
            // Copy id/tool pairs in order
            var guid = Guid.NewGuid();
            var packs = new[] { new ConnectionLinePartnerMessagePack(7, guid) };

            var partners = ConnectionLinePartner.FromMessagePacks(packs);

            Assert.AreEqual(1, partners.Length);
            Assert.AreEqual(7, partners[0].PartnerId.AsPrimitive());
            Assert.AreEqual(guid, partners[0].ConnectToolGuid);
        }

        [Test]
        public void FromMessagePacksTreatsNullAsEmpty()
        {
            // 接続ゼロのブロックはnull配列で届き得るため空として扱う
            // A block with no connections may arrive as a null array, treated as empty
            Assert.AreEqual(0, ConnectionLinePartner.FromMessagePacks(null).Length);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/ConnectionLine/RemovePreviewRequestsTest.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State;
using NUnit.Framework;

namespace Client.Tests.ConnectionLine
{
    /// <summary>
    ///     赤プレビューは要求者が残っている間は外れないことを検証する（巻き込み表示が他者の赤を消さない）
    ///     Verifies the red preview stays while any requester remains (a cascade reset never clears someone else's red)
    /// </summary>
    public class RemovePreviewRequestsTest
    {
        [Test]
        public void RedStaysUntilTheLastRequesterReleases()
        {
            var requests = new RemovePreviewRequests();
            var ownHover = new object();
            var cascadingPole = new object();

            // 最初の要求だけが「赤を付ける」合図になる
            // Only the first request signals "apply red"
            Assert.IsTrue(requests.Add(ownHover));
            Assert.IsFalse(requests.Add(cascadingPole));

            // 電柱側の解除では戻らず、最後の要求者の解除で戻る
            // The pole's release does not reset; the last requester's release does
            Assert.IsFalse(requests.Remove(cascadingPole));
            Assert.IsTrue(requests.Remove(ownHover));
        }

        [Test]
        public void DuplicateAndUnknownRequestsAreIgnored()
        {
            // 同じ要求者の二重要求・未登録の解除は状態を変えない
            // A duplicate request or an unknown release changes nothing
            var requests = new RemovePreviewRequests();
            var requester = new object();
            Assert.IsTrue(requests.Add(requester));
            Assert.IsFalse(requests.Add(requester));
            Assert.IsFalse(requests.Remove(new object()));
            Assert.IsTrue(requests.Remove(requester));
            Assert.IsFalse(requests.Remove(requester));
        }
    }
}
```

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/BlockDestructionCategoryReservedKeyTest.cs`:

```csharp
using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    /// <summary>
    ///     接続線用に予約した破壊カテゴリーキーをマスタが使うと検証で弾かれることを確かめる
    ///     Verifies the master is rejected when it uses the destruction category key reserved for connection lines
    /// </summary>
    public class BlockDestructionCategoryReservedKeyTest
    {
        [Test]
        public void ReservedConnectionLineKeyIsRejected()
        {
            // 依存マスタを既存の有効Modで初期化し、blocks.jsonのカテゴリーキーだけをテスト内で差し替える
            // Initialize dependency masters from the valid mod and replace only the category key in the in-test blocks.json
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(blocksJsonPath));
            blocksJToken["blockDestructionCategories"][0]["categoryKey"] = BlockMaster.ConnectionLineDestructionCategory;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"uses the reserved key {BlockMaster.ConnectionLineDestructionCategory}", errorLogs);
        }

        [Test]
        public void ExistingCategoriesPassValidation()
        {
            // 既存のテストmodのカテゴリー定義は予約キーと衝突しない
            // The test mod's existing category definitions do not collide with the reserved key
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(BlockMasterUtil.Validate(MasterHolder.BlockMaster.Blocks, out var errorLogs), errorLogs);
        }
    }
}
```


- [ ] **Step 2: 実装を書く**

`ConnectionLineKind.cs`:

```csharp
namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     接続線の種別。切断・引き直しの送信先を決める
    ///     Connection line kind, deciding where disconnect and restore requests go
    /// </summary>
    public enum ConnectionLineKind
    {
        ElectricWire,
        GearChain,
    }
}
```

`ConnectionLinePartner.cs`:

```csharp
using System;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     接続相手1件（相手ブロックと引いた種類）
    ///     One connection partner (partner block and the tool kind used)
    /// </summary>
    public readonly struct ConnectionLinePartner
    {
        public readonly BlockInstanceId PartnerId;
        public readonly Guid ConnectToolGuid;

        public ConnectionLinePartner(BlockInstanceId partnerId, Guid connectToolGuid)
        {
            PartnerId = partnerId;
            ConnectToolGuid = connectToolGuid;
        }

        // 状態詳細の配列をクライアント表現へ写す（接続ゼロはnullで届き得る）
        // Map the state-detail array to client form (zero connections may arrive as null)
        public static ConnectionLinePartner[] FromMessagePacks(ConnectionLinePartnerMessagePack[] packs)
        {
            if (packs == null) return Array.Empty<ConnectionLinePartner>();

            var partners = new ConnectionLinePartner[packs.Length];
            for (var i = 0; i < packs.Length; i++)
            {
                partners[i] = new ConnectionLinePartner(new BlockInstanceId(packs[i].PartnerBlockInstanceId), packs[i].ConnectToolGuid);
            }
            return partners;
        }
    }
}
```

`IRemovePreviewable.cs`（`C/Client.Game/InGame/UI/UIState/State/IRemovePreviewable.cs`）:

```csharp
namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    ///     削除プレビュー（赤表示）の付け外しだけを持つ表示対象
    ///     Display target that only toggles the remove (red) preview
    /// </summary>
    public interface IRemovePreviewable
    {
        // 要求者ごとに赤を求める。誰かが求めている間は赤のまま
        // Request red per requester; it stays red while anyone still requests it
        void RequestRemovePreview(object requester);
        void ReleaseRemovePreview(object requester);
    }
}
```

`RemovePreviewRequests.cs`（`C/Client.Game/InGame/UI/UIState/State/RemovePreviewRequests.cs`）:

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    ///     1つの表示対象に赤プレビューを求めている要求者の集合。自分のホバー・選択と、撤去ブロックの巻き込み表示が同時に求め得る
    ///     Set of requesters wanting the red preview on one display target; own hover/selection and a removed block's cascade may request at once
    /// </summary>
    public class RemovePreviewRequests
    {
        private readonly HashSet<object> _requesters = new();

        // 最初の要求でだけtrue（赤を付ける合図）
        // True only on the first request (signal to apply red)
        public bool Add(object requester)
        {
            return _requesters.Add(requester) && _requesters.Count == 1;
        }

        // 最後の要求が外れたときだけtrue（赤を戻す合図）
        // True only when the last request is released (signal to reset)
        public bool Remove(object requester)
        {
            return _requesters.Remove(requester) && _requesters.Count == 0;
        }
    }
}
```

`BlockMaster.cs` L20-23 の既定カテゴリー定数の直後に追加:

```csharp
        // 接続線（電線・歯車チェーン）の破壊カテゴリー。ブロックではないのでマスタの定義には現れず、予約キーとして検証で衝突を弾く
        // Destruction category for connection lines (wires, gear chains); never in master definitions, so validation rejects it as a reserved key
        public const string ConnectionLineDestructionCategory = "connectionLine";
```

`BlockMasterElementExtension.cs` の既定カテゴリー再公開の直後に追加:

```csharp
        // 接続線の破壊カテゴリー。単一の定義元はCore.MasterのBlockMasterが持つ
        // Connection-line destruction category; the single source of truth lives in Core.Master's BlockMaster
        public const string ConnectionLineDestructionCategory = BlockMaster.ConnectionLineDestructionCategory;
```

`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/BlockDestructionCategoryValidator.cs`（新規。`BlockMasterUtil.cs` L243-272 のローカル関数 `BlockDestructionCategoryValidation` をここへ移し、予約キー検査を足す。`ExistsBlockGuid` は `MasterHolder.BlockMaster` を使わず定義済みブロックの Guid 集合で判定する）:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    /// <summary>
    ///     破壊カテゴリ定義の検証。blockGuidの実在・複数カテゴリへの重複登録・予約キーとの衝突を弾く
    ///     Validates destruction category definitions: block guid existence, duplicate registration and collisions with reserved keys
    /// </summary>
    public static class BlockDestructionCategoryValidator
    {
        public static string Validate(Blocks blocks)
        {
            var logs = "";
            var definedBlockGuids = new HashSet<Guid>(blocks.Data.Select(block => block.BlockGuid));
            var assignedCategoryByBlockGuid = new Dictionary<Guid, string>();
            foreach (var category in blocks.BlockDestructionCategories)
            {
                // 接続線用の予約キーをマスタが使うと、ブロックと接続線が同じドラッグで混ざってしまう
                // A master using the connection-line key would let blocks and lines mix in one drag
                if (category.CategoryKey == BlockMaster.ConnectionLineDestructionCategory)
                {
                    logs += $"[BlockMaster] DestructionCategory uses the reserved key {BlockMaster.ConnectionLineDestructionCategory}\n";
                }

                foreach (var target in category.TargetBlocks)
                {
                    // foreignKeyは自動生成されないため参照先の実在を手動で確認する
                    // foreignKey validation is not auto-generated, so verify the referenced block exists
                    if (!definedBlockGuids.Contains(target.BlockGuid))
                    {
                        logs += $"[BlockMaster] DestructionCategory:{category.CategoryKey} has invalid BlockGuid:{target.BlockGuid}\n";
                    }

                    // 逆引きは1ブロック1カテゴリ前提。重複するとロード順で結果が変わるため弾く
                    // The reverse lookup assumes one category per block; duplicates make the result order-dependent
                    if (assignedCategoryByBlockGuid.TryGetValue(target.BlockGuid, out var existingCategory))
                    {
                        logs += $"[BlockMaster] BlockGuid:{target.BlockGuid} is assigned to multiple destruction categories ({existingCategory}, {category.CategoryKey})\n";
                    }
                    else
                    {
                        assignedCategoryByBlockGuid.Add(target.BlockGuid, category.CategoryKey);
                    }
                }
            }

            return logs;
        }
    }
}
```

`BlockMasterUtil.cs`: L17 `errorLogs += BlockDestructionCategoryValidation();` → `errorLogs += BlockDestructionCategoryValidator.Validate(blocks);`（`using Core.Master.Validator.Block;` を追加）、L243-272 のローカル関数 `BlockDestructionCategoryValidation` を削除。既存の `ExistsBlockGuid`（L367-370）は `Array.Exists(blocks.Data, b => b.BlockGuid == blockGuid)` で、新ファイルの Guid 集合照合と同じ判定。`BlockMasterUtil` 内に他の呼び出し元が残るので `ExistsBlockGuid` 自体は残す。

`ConnectionLineRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     表示中の接続線を両端ブロックから引ける索引。線は小さいId側の子に1本だけ生成されるため、相手側からはここで引く
    ///     Index resolving displayed lines from either endpoint; each line lives once under the smaller-id block, so the other side looks it up here
    /// </summary>
    public class ConnectionLineRegistry
    {
        private readonly Dictionary<BlockInstanceId, List<ConnectionLineDeleteTarget>> _linesByBlock = new();

        public void Register(ConnectionLineDeleteTarget line)
        {
            Add(line.FromId, line);
            Add(line.ToId, line);
        }

        public void Unregister(ConnectionLineDeleteTarget line)
        {
            Remove(line.FromId, line);
            Remove(line.ToId, line);
        }

        public IReadOnlyList<ConnectionLineDeleteTarget> GetLinesAttachedTo(BlockInstanceId blockId)
        {
            return _linesByBlock.TryGetValue(blockId, out var lines) ? lines : Array.Empty<ConnectionLineDeleteTarget>();
        }

        private void Add(BlockInstanceId blockId, ConnectionLineDeleteTarget line)
        {
            if (!_linesByBlock.TryGetValue(blockId, out var lines))
            {
                lines = new List<ConnectionLineDeleteTarget>();
                _linesByBlock[blockId] = lines;
            }
            lines.Add(line);
        }

        private void Remove(BlockInstanceId blockId, ConnectionLineDeleteTarget line)
        {
            if (!_linesByBlock.TryGetValue(blockId, out var lines)) return;
            lines.Remove(line);
            if (lines.Count == 0) _linesByBlock.Remove(blockId);
        }
    }
}
```

`ConnectionLineDeleteTarget.cs`（Task C4 で `CollectRemovedObjects` を追記する前提で 200 行未満に収める）:

```csharp
using System;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     電線・歯車チェーン共通の削除対象。1本の線＝1つの論理削除単位
    ///     Delete target shared by electric wires and gear chains; one line = one logical delete unit
    /// </summary>
    public class ConnectionLineDeleteTarget : MonoBehaviour, IDeleteTarget, IRemovePreviewable
    {
        public BlockInstanceId FromId { get; private set; }
        public BlockInstanceId ToId { get; private set; }
        public Guid ConnectToolGuid { get; private set; }
        public ConnectionLineKind Kind { get; private set; }

        private readonly RemovePreviewRequests _removePreviewRequests = new();
        private ConnectionLineRegistry _registry;
        private RendererMaterialReplacerController _materialReplacer;

        public void Initialize(BlockInstanceId fromId, BlockInstanceId toId, Guid connectToolGuid, ConnectionLineKind kind, ConnectionLineRegistry registry)
        {
            FromId = fromId;
            ToId = toId;
            ConnectToolGuid = connectToolGuid;
            Kind = kind;
            _registry = registry;
            _registry.Register(this);
        }

        // 自分のホバー・選択は自分自身を要求者として赤を求める
        // Own hover/selection requests red with this component as the requester
        public void SetRemovePreviewing()
        {
            RequestRemovePreview(this);
        }

        public void ResetMaterial()
        {
            ReleaseRemovePreview(this);
        }

        public void RequestRemovePreview(object requester)
        {
            if (!_removePreviewRequests.Add(requester)) return;

            // レンダラーはSetLine後に揃うため、置換器は初回プレビュー時に作る
            // Renderers are complete only after SetLine, so build the replacer on the first preview
            _materialReplacer ??= new RendererMaterialReplacerController(gameObject);
            _materialReplacer.CopyAndSetMaterial(MaterialConst.GetPreviewPlaceBlockMaterial());
            _materialReplacer.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }

        // 最後の要求者が外れたときだけ元へ戻す（巻き込み表示の解除が他者の赤を消さない）
        // Reset only when the last requester leaves (a cascade release never clears someone else's red)
        public void ReleaseRemovePreview(object requester)
        {
            if (!_removePreviewRequests.Remove(requester)) return;
            _materialReplacer?.ResetMaterial();
        }

        // 両端ブロックの座標を解決する（切断送信とUndo記録が共有）
        // Resolve both endpoint block positions (shared by the disconnect send and the undo record)
        public bool TryResolveEndpointPositions(out Vector3Int fromPos, out Vector3Int toPos)
        {
            fromPos = default;
            toPos = default;
            var store = ClientDIContext.BlockGameObjectDataStore;
            if (!store.TryGetBlockGameObject(FromId, out var fromBlock) || !store.TryGetBlockGameObject(ToId, out var toBlock)) return false;

            fromPos = fromBlock.BlockPosInfo.OriginalPos;
            toPos = toBlock.BlockPosInfo.OriginalPos;
            return true;
        }

        // 切断可否はサーバーが判定し拒否は通知で返るため、クライアントでは常に削除可とする
        // The server judges disconnects and returns refusals as notifications, so the client always allows it
        public bool IsRemovable(out LocalizationKey? deniedReason)
        {
            deniedReason = null;
            return true;
        }

        public void Delete()
        {
            // 両端ブロックの座標を解決して種類ごとの切断要求を送る
            // Resolve both endpoint positions and send the per-kind disconnect request
            if (!TryResolveEndpointPositions(out var fromPos, out var toPos))
            {
                Debug.LogWarning($"[ConnectionLineDelete] endpoint block not found: from={FromId} to={ToId}");
                return;
            }

            switch (Kind)
            {
                case ConnectionLineKind.ElectricWire:
                    ClientContext.VanillaApi.SendOnly.DisconnectElectricWire(fromPos, toPos);
                    break;
                case ConnectionLineKind.GearChain:
                    ClientContext.VanillaApi.SendOnly.DisconnectGearChain(fromPos, toPos);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null);
            }
        }

        // 線は1本ごとに1つのGameObjectなので自身を論理キーにする
        // Each line owns one GameObject, so the component itself is the logical key
        public object GetDeleteTargetKey()
        {
            return this;
        }

        public string GetDestructionCategory()
        {
            return BlockMasterElementExtension.ConnectionLineDestructionCategory;
        }

        private void OnDestroy()
        {
            _registry?.Unregister(this);
            _materialReplacer?.DestroyMaterial();
        }
    }
}
```

（`MaterialConst.PreviewColorPropertyName` は `BlockGameObject.SetRemovePreviewing` が既に使っている定数。`RendererMaterialReplacerController.DestroyMaterial` も既存。）

`ConnectionLineViewBase.cs` の変更（`UpdateConnectionLines` の引数型と生成処理。他は現状維持）:

```csharp
        // 線の種別。削除対象の切断送信先を決める
        // Line kind, deciding where the delete target sends its disconnect
        protected abstract ConnectionLineKind GetLineKind();

        /// <summary>
        /// 接続ラインの表示を更新する
        /// Update the connection line display
        /// </summary>
        public void UpdateConnectionLines(IReadOnlyList<ConnectionLinePartner> partners)
        {
            var newInstanceIds = new HashSet<BlockInstanceId>();
            foreach (var partner in partners) newInstanceIds.Add(partner.PartnerId);

            // 不要になったラインを削除する
            // Remove lines that are no longer needed
            RemoveOldLines(newInstanceIds);

            // 新規接続のラインを作成する
            // Create lines for new connections
            AddNewLines(partners);

            #region Internal

            // RemoveOldLines / ShouldDrawLine は現状のまま
            // RemoveOldLines / ShouldDrawLine stay unchanged

            void AddNewLines(IReadOnlyList<ConnectionLinePartner> lines)
            {
                foreach (var partner in lines)
                {
                    if (_activeLines.ContainsKey(partner.PartnerId)) continue;
                    if (!ShouldDrawLine(_myBlockInstanceId, partner.PartnerId)) continue;

                    _activeLines[partner.PartnerId] = CreateLineElement(partner);
                }
            }

            // ラインElementを生成し、削除対象として索引へ登録する
            // Create the line element and register it as a delete target
            TElement CreateLineElement(ConnectionLinePartner partner)
            {
                var element = Instantiate(_linePrefab, transform);
                element.SetLine(_myBlockInstanceId, partner.PartnerId);
                var deleteTarget = element.gameObject.AddComponent<ConnectionLineDeleteTarget>();
                deleteTarget.Initialize(_myBlockInstanceId, partner.PartnerId, partner.ConnectToolGuid, GetLineKind(), ClientDIContext.ConnectionLineRegistry);
                return element;
            }

            #endregion
        }
```

（`RemoveOldLines` の既存実装はそのまま残す。`using System.Linq` は RemoveOldLines が使うので維持。）

`ElectricWireLineView.cs` に追加:

```csharp
        protected override ConnectionLineKind GetLineKind()
        {
            return ConnectionLineKind.ElectricWire;
        }
```

`GearChainPoleChainLineView.cs` に追加:

```csharp
        protected override ConnectionLineKind GetLineKind()
        {
            return ConnectionLineKind.GearChain;
        }
```

`GearChainPoleChainLineViewElement.SetLine` の末尾（ライン2設定の後）にチェーンの当たり判定を追加:

```csharp
            // 削除ツールが狙えるよう、両端を結ぶトリガーカプセルを接続線レイヤーに置く
            // Place a trigger capsule spanning both ends on the connection-line layer so the delete tool can aim at it
            BuildCollider(startPos, endPos);

            #region Internal

            void BuildCollider(Vector3 start, Vector3 end)
            {
                var colliderObject = new GameObject("ChainCollider");
                colliderObject.layer = LayerConst.ConnectionLineLayer;
                colliderObject.transform.SetParent(transform, false);
                colliderObject.transform.position = (start + end) * 0.5f;
                colliderObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, end - start);

                var capsule = colliderObject.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = CapsuleDirectionYAxis;
                capsule.radius = ColliderRadius;
                capsule.height = Vector3.Distance(start, end);
            }

            #endregion
```

クラス先頭に定数を追加: `private const int CapsuleDirectionYAxis = 1;` と `private const float ColliderRadius = 0.08f;`（2本のLineRenderer間隔0.1を包む太さ）。`using Client.Common;` を追加。

`ElectricWireStateChangeProcessor.OnChangeState` の変換部（Task A3 が `Partners` を `BlockInstanceId[]` へ写している箇所）を置換:

```csharp
            // 接続相手（相手IDと引いた種類）を取り出す
            // Extract partners (partner id and tool kind)
            var partners = ConnectionLinePartner.FromMessagePacks(state.Partners);

            // 接続先ID集合を最新状態へ置き換える
            // Replace the partner ID set with the latest state
            _currentPartnerIds.Clear();
            foreach (var partner in partners) _currentPartnerIds.Add(partner.PartnerId);

            // ワイヤー表示を更新する
            // Update the wire display
            _wireLineView.UpdateConnectionLines(partners);
```

`GearChainPoleStateChangeProcessor.OnChangeState` も同様に `chainLineView.UpdateConnectionLines(ConnectionLinePartner.FromMessagePacks(state.Partners));` へ置換し、不要になった `System.Linq` / `System` の using を外す。

`ClientDIContext.cs` に追加:

```csharp
        public static ConnectionLineRegistry ConnectionLineRegistry { get; private set; }
```
コンストラクタに `ConnectionLineRegistry = diContainer.DIContainerResolver.Resolve<ConnectionLineRegistry>();`

`MainGameInteractionRegistration.cs` の `builder.Register<BuildUndoService>(Lifetime.Singleton);` の次行に `builder.Register<ConnectionLineRegistry>(Lifetime.Singleton);`


- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.ConnectionLine\.|BlockDestructionCategoryReservedKeyTest|BlockDestructionCategoryMasterDataTest|DragDeleteSelectionCategoryTest"`（新規 `.cs` を足したので先に `uloop launch ./moorestech_client --restart`）
Expected: ErrorCount 0 / 4 tests PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/RemovePreviewRequests.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/IRemovePreviewable.cs moorestech_client/Assets/Scripts/Client.Game/Common/BlockMasterElementExtension.cs moorestech_server/Assets/Scripts/Core.Master/BlockMaster.cs moorestech_server/Assets/Scripts/Core.Master/Validator moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/BlockDestructionCategoryReservedKeyTest.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/ConnectionLine
git commit -m "feat(client): 電線・歯車チェーンを共通の削除対象として当たり判定と索引を持たせる"
```

---

### Task 10 (C3): 照準の対象解決（最前面＋ドラッグ中は同カテゴリーの最前面）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteTargetHitSelector.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteAimFilter.cs`（照準の絞り込み条件の判別union。裁定 `.decisions/2026-10-05-削除ツールの照準条件は判別unionで表す.md`）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteAimResult.cs`（照準結果と外れた理由）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/DeleteTargetRaycaster.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:79-134`（レイキャスト本体を公開ヘルパーへ切り出し）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs:20-22,35,64,73-77,87,106`（`string _sessionCategory`(null=未固定) を `DeleteAimFilter _sessionFilter` へ置換し `AimFilter` で公開）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs:33`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DeleteTargetHitSelectorTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteSelectionCategoryTest.cs`（AimFilter ケース追加）

**Interfaces:**
- Consumes: Task C1 `LayerConst.ConnectionLineLayer` / `ConnectionLineOnlyLayerMask`、Task C2 `BlockMasterElementExtension.ConnectionLineDestructionCategory` と `ConnectionLineDeleteTarget.FromCollider`（本タスクで追加）
- Produces:
  - `public readonly struct DeleteTargetHit { float Distance; IDeleteTarget Target; }`
  - `public readonly struct DeleteAimFilter { static DeleteAimFilter Frontmost; static DeleteAimFilter Category(string categoryKey); bool IsCategoryRequired; bool Accepts(IDeleteTarget target) }`（null を合図に使わない。`Frontmost.Accepts` は常に true、`Category(key).Accepts` はカテゴリー一致のみ true）
  - `public enum DeleteAimOutcome { Found, NothingHit, OccludedByNonTarget, NoTargetOfCategory }`、`public readonly struct DeleteAimResult { DeleteAimOutcome Outcome; IDeleteTarget Target; bool IsFound; static Found(IDeleteTarget); static Missed(DeleteAimOutcome) }`
  - `public static class DeleteTargetHitSelector { static DeleteAimResult Select(IReadOnlyList<DeleteTargetHit> hits, DeleteAimFilter filter) }` — Frontmost は最前面（最前面が非対象なら `OccludedByNonTarget`）、Category はそのカテゴリーの最前面（無ければ `NoTargetOfCategory`）、ヒット0件は `NothingHit`
  - `public static class DeleteTargetRaycaster { static DeleteAimResult AimAt(DeleteAimFilter filter) }`
  - `BlockClickDetectUtil.TryCreateAimRay(out Ray ray)`（照準レイ生成の正本。カメラが無ければ false）と `public static int RaycastAimAll(int layerMask, float maxDistance, out RaycastHit[] hits)`（hits は共有バッファ。戻り値件数まで有効。カメラが無ければ 0）
  - `ConnectionLineDeleteTarget.FromCollider(Collider collider) : ConnectionLineDeleteTarget`（接続線コライダーから線本体を引く正本。照準と Task 13 のスポイトが共有）
  - `DragDeleteSelection.AimFilter : DeleteAimFilter`（未固定・確定後・キャンセル後は `Frontmost`）

- [ ] **Step 1: テストを書く**

`DeleteTargetHitSelectorTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     照準ヒット列から削除対象を選ぶ規則（最前面／カテゴリー指定時はその中の最前面）と外れた理由を検証する
    ///     Verifies picking a delete target from aim hits (frontmost / frontmost within a required category) and the miss reasons
    /// </summary>
    public class DeleteTargetHitSelectorTest
    {
        [Test]
        public void FrontmostPicksNearest()
        {
            // 電線がブロックの手前なら電線を取る
            // A wire in front of a block wins
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(5f, block), new(2f, wire) };

            var result = DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Frontmost);
            Assert.AreEqual(DeleteAimOutcome.Found, result.Outcome);
            Assert.AreSame(wire, result.Target);
        }

        [Test]
        public void CategorySkipsNearerOtherCategory()
        {
            // ブロックのドラッグ中は手前の電線を飛ばして奥のブロックを取る
            // During a block drag, skip the nearer wire and take the block behind it
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(2f, wire), new(5f, block) };

            var result = DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Category("default"));
            Assert.AreEqual(DeleteAimOutcome.Found, result.Outcome);
            Assert.AreSame(block, result.Target);
        }

        [Test]
        public void FrontmostNonTargetOccludes()
        {
            // 最前面が削除対象でない物体なら奥は拾わない（従来の遮蔽規則）
            // A non-target frontmost hit occludes what is behind (existing occlusion rule)
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(1f, null), new(5f, block) };

            Assert.AreEqual(DeleteAimOutcome.OccludedByNonTarget, DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Frontmost).Outcome);
        }

        [Test]
        public void CategoryWithoutMatchReportsNoTargetOfCategory()
        {
            // 指定カテゴリーの対象が1件も無ければ理由付きで外れる
            // With no target of the required category, it misses with a reason
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var hits = new List<DeleteTargetHit> { new(2f, wire) };

            Assert.AreEqual(DeleteAimOutcome.NoTargetOfCategory, DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Category("default")).Outcome);
        }

        [Test]
        public void EmptyHitsReportNothingHit()
        {
            // ヒット0件はどちらの条件でも NothingHit
            // Zero hits are NothingHit under either filter
            Assert.AreEqual(DeleteAimOutcome.NothingHit, DeleteTargetHitSelector.Select(new List<DeleteTargetHit>(), DeleteAimFilter.Frontmost).Outcome);
            Assert.AreEqual(DeleteAimOutcome.NothingHit, DeleteTargetHitSelector.Select(new List<DeleteTargetHit>(), DeleteAimFilter.Category("default")).Outcome);
        }
    }
}
```

`DragDeleteSelectionCategoryTest.cs` に追加（`using Client.Game.InGame.UI.UIState.State.DragDelete;` は既存）:

```csharp
        [Test]
        public void AimFilterIsFixedByFirstTargetAndResetOnNewDrag()
        {
            // 最初の対象で照準条件がそのカテゴリーに固定され、新しいドラッグで最前面へ戻る
            // The first target fixes the aim filter to its category and a new drag resets it to frontmost
            var selection = CreateSelection();
            selection.BeginDrag();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);

            var line = new FakeDeleteTarget { Removable = true, Category = "connectionLine" };
            selection.TryAddTarget(line, out _);
            Assert.IsTrue(selection.AimFilter.IsCategoryRequired);
            Assert.IsTrue(selection.AimFilter.Accepts(line));
            Assert.IsFalse(selection.AimFilter.Accepts(new FakeDeleteTarget { Category = "default" }));

            selection.BeginDrag();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);
        }
```

（`CreateSelection()` は Task C4 で導入するテスト内ヘルパー。C3 を C4 より先に実装する場合は `new DragDeleteSelection(new BuildOperationHistory())` を直接書き、C4 で置換する。）

- [ ] **Step 2: 実装を書く**

`DeleteAimFilter.cs`:

```csharp
namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     削除ツールの照準の絞り込み条件。最前面か、指定カテゴリーの最前面かの2択（nullを合図に使わない）
    ///     Aim filter of the delete tool: plain frontmost, or frontmost within a category (null is never used as a signal)
    /// </summary>
    public readonly struct DeleteAimFilter
    {
        public static DeleteAimFilter Frontmost => new(false, string.Empty);

        public static DeleteAimFilter Category(string categoryKey)
        {
            return new DeleteAimFilter(true, categoryKey);
        }

        public bool IsCategoryRequired { get; }
        private readonly string _categoryKey;

        private DeleteAimFilter(bool isCategoryRequired, string categoryKey)
        {
            IsCategoryRequired = isCategoryRequired;
            _categoryKey = categoryKey;
        }

        // 最前面条件は何でも受け、カテゴリー条件は一致したものだけ受ける
        // Frontmost accepts anything; a category filter accepts only matching targets
        public bool Accepts(IDeleteTarget target)
        {
            if (!IsCategoryRequired) return true;
            return target.GetDestructionCategory() == _categoryKey;
        }
    }
}
```

`DeleteAimResult.cs`:

```csharp
namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     照準の結果。外れた場合は理由を持つ（照準の外れは毎フレームの通常状態なのでログは出さない）
    ///     Aim result; carries the reason on a miss (a miss is the normal per-frame state, so it is not logged)
    /// </summary>
    public enum DeleteAimOutcome
    {
        Found,
        NothingHit,
        OccludedByNonTarget,
        NoTargetOfCategory,
    }

    public readonly struct DeleteAimResult
    {
        public DeleteAimOutcome Outcome { get; }
        public IDeleteTarget Target { get; }
        public bool IsFound => Outcome == DeleteAimOutcome.Found;

        private DeleteAimResult(DeleteAimOutcome outcome, IDeleteTarget target)
        {
            Outcome = outcome;
            Target = target;
        }

        public static DeleteAimResult Found(IDeleteTarget target)
        {
            return new DeleteAimResult(DeleteAimOutcome.Found, target);
        }

        public static DeleteAimResult Missed(DeleteAimOutcome outcome)
        {
            return new DeleteAimResult(outcome, null);
        }
    }
}
```

`DeleteTargetHitSelector.cs`:

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     照準レイ上のヒット1件（距離と、解決できた削除対象。非対象の遮蔽物はnull）
    ///     One aim-ray hit (distance and the resolved delete target; null for a non-target occluder)
    /// </summary>
    public readonly struct DeleteTargetHit
    {
        public readonly float Distance;
        public readonly IDeleteTarget Target;

        public DeleteTargetHit(float distance, IDeleteTarget target)
        {
            Distance = distance;
            Target = target;
        }
    }

    /// <summary>
    ///     ヒット列から削除対象を選ぶ。最前面条件は最前面、カテゴリー条件はそのカテゴリーの最前面
    ///     Picks a delete target from hits: frontmost under the frontmost filter, frontmost of the category under a category filter
    /// </summary>
    public static class DeleteTargetHitSelector
    {
        public static DeleteAimResult Select(IReadOnlyList<DeleteTargetHit> hits, DeleteAimFilter filter)
        {
            if (hits.Count == 0) return DeleteAimResult.Missed(DeleteAimOutcome.NothingHit);

            var bestDistance = float.MaxValue;
            IDeleteTarget best = null;
            var hasBest = false;
            foreach (var hit in hits)
            {
                if (bestDistance <= hit.Distance) continue;

                // カテゴリー条件では非対象・別カテゴリーを貫通する
                // Under a category filter, pass through non-targets and other categories
                if (filter.IsCategoryRequired && (hit.Target == null || !filter.Accepts(hit.Target))) continue;

                bestDistance = hit.Distance;
                best = hit.Target;
                hasBest = true;
            }

            // 最前面条件で最前面が非対象なら遮蔽、カテゴリー条件で該当が無ければその旨を返す
            // Under frontmost a non-target frontmost hit occludes; under a category, report that nothing matched
            if (!hasBest) return DeleteAimResult.Missed(DeleteAimOutcome.NoTargetOfCategory);
            if (best == null) return DeleteAimResult.Missed(DeleteAimOutcome.OccludedByNonTarget);
            return DeleteAimResult.Found(best);
        }
    }
}
```

`BlockClickDetectUtil.cs`: 既存の private ローカル関数 `RaycastNonAlloc` をクラス直下の public メソッドへ昇格し、`TryGetFrontmostSolidHit` はそれを呼ぶ形にする:

```csharp
        /// <summary>
        ///     照準レイで指定レイヤーを全件ヒットさせ件数を返す。hitsは共有バッファで戻り値件数までが有効
        ///     Raycasts all hits on the given layers along the aim ray; hits is a shared buffer valid up to the returned count
        /// </summary>
        public static int RaycastAimAll(int layerMask, float maxDistance, out RaycastHit[] hits)
        {
            hits = HitBuffer;
            if (!TryCreateAimRay(out var ray)) return 0;

            // 飽和したまま返すと手前のヒットを取りこぼすため、バッファを倍にして採り直す
            // A saturated buffer could drop the nearest hit, so it is doubled and re-queried
            while (true)
            {
                var count = Physics.RaycastNonAlloc(ray, HitBuffer, maxDistance, layerMask, QueryTriggerInteraction.Collide);
                hits = HitBuffer;
                if (count < HitBuffer.Length) return count;

                HitBuffer = new RaycastHit[HitBuffer.Length * 2];
            }
        }
```

照準レイ生成の正本も同ファイルへ足す（`RaycastAimAll` と Task 13 の `GetCursorOnConnectionLine` が共有）:

```csharp
        // 照準レイを作る。照準座標はAimPointProviderで視点モードに応じて一元解決する（カメラが無ければfalse）
        // Build the aim ray; the aim point is resolved centrally by AimPointProvider per view mode (false without a camera)
        public static bool TryCreateAimRay(out Ray ray)
        {
            ray = default;
            var camera = Camera.main;
            if (camera == null) return false;
            ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            return true;
        }
```

`TryGetFrontmostSolidHit` は先頭のカメラ取得〜`RaycastNonAlloc` 呼び出しを `var hitCount = RaycastAimAll(layerMask, maxDistance, out var hits);` に置換し、ループ内の `HitBuffer[index]` を `hits[index]` にする（`#region Internal` は不要になるので削除）。`QueryTriggerInteraction.Collide` は `m_QueriesHitTriggers: 1`（`ProjectSettings/DynamicsManager.asset:15`）の現状と同じ挙動を明示するだけで、既存呼び出しの結果は変わらない。

`DeleteTargetRaycaster.cs`（`C/Client.Game/InGame/Control/DeleteTargetRaycaster.cs`）:

```csharp
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using UnityEngine;

namespace Client.Game.InGame.Control
{
    /// <summary>
    ///     削除ツールの照準解決。ブロック層と接続線層を1本のレイで見て、照準条件で対象を選ぶ
    ///     Delete-tool aim resolution: one ray over block and connection-line layers, picking a target under the aim filter
    /// </summary>
    public static class DeleteTargetRaycaster
    {
        private const float RayDistance = 100f;
        private static readonly List<DeleteTargetHit> HitCandidates = new();

        public static DeleteAimResult AimAt(DeleteAimFilter filter)
        {
            var mask = LayerConst.BlockOnlyLayerMask | LayerConst.ConnectionLineOnlyLayerMask;
            var hitCount = BlockClickDetectUtil.RaycastAimAll(mask, RayDistance, out var hits);

            // 設置ゴーストは貫通し、それ以外のヒットを候補へ積む
            // Pass through placement ghosts and collect every other hit as a candidate
            HitCandidates.Clear();
            for (var i = 0; i < hitCount; i++)
            {
                var collider = hits[i].collider;
                if (collider.GetComponentInParent<BlockPreviewObject>() != null) continue;
                HitCandidates.Add(new DeleteTargetHit(hits[i].distance, ResolveTarget(collider)));
            }

            return DeleteTargetHitSelector.Select(HitCandidates, filter);

            #region Internal

            // 接続線のコライダーは線本体の子、ブロック・レール・車両は従来どおり自身か子に対象を持つ
            // Connection-line colliders sit under the line, while blocks/rails/cars keep the target on self or children as before
            static IDeleteTarget ResolveTarget(Collider collider)
            {
                if (collider.gameObject.layer == LayerConst.ConnectionLineLayer) return ConnectionLineDeleteTarget.FromCollider(collider);
                return collider.gameObject.GetComponentInChildren<IDeleteTarget>();
            }

            #endregion
        }
    }
}
```

`ConnectionLineDeleteTarget.cs` に追加（接続線コライダー→線本体の解決の正本）:

```csharp
        // 当たり判定は線本体の子オブジェクトにあるため親を辿って本体を得る（線でなければnull）
        // Hit colliders live on child objects of the line, so climb to the parent for the line itself (null when not a line)
        public static ConnectionLineDeleteTarget FromCollider(Collider collider)
        {
            return collider.GetComponentInParent<ConnectionLineDeleteTarget>();
        }
```

`DragDeleteSelection.cs`: `private string _sessionCategory;`（null=未固定）を判別unionへ置換し、照準へ公開する:

```csharp
        // 最初に選択した対象の破壊カテゴリーでセッションを固定する。未固定は最前面条件で、照準の絞り込みにもそのまま使う
        // Fix the session to the first selected target's category; unfixed is the frontmost filter, also used directly as the aim filter
        private DeleteAimFilter _sessionFilter = DeleteAimFilter.Frontmost;
        public DeleteAimFilter AimFilter => _sessionFilter;
```
- `BeginDrag`（L35）・`CancelSelection`（L87）・`CommitDelete`（L106）の `_sessionCategory = null;` → `_sessionFilter = DeleteAimFilter.Frontmost;`
- `TryAddTarget`（L64）の `_sessionCategory ??= target.GetDestructionCategory();` → `if (!_sessionFilter.IsCategoryRequired) _sessionFilter = DeleteAimFilter.Category(target.GetDestructionCategory());`
- `IsCategoryCompatible`（L73-77）の本体 → `return _sessionFilter.Accepts(target);`

`DeleteObjectService.Update` の33行目を置換:

```csharp
            // カーソル下の削除対象を取得する。選択が固定したカテゴリーがあればその最前面、無ければ最前面（ドラッグ外・確定後・キャンセル後は常に最前面）
            // Resolve the hovered target: frontmost of the selection's fixed category if any, else plain frontmost (always frontmost outside a drag, after commit or cancel)
            var hovered = DeleteTargetRaycaster.AimAt(_selection.AimFilter).Target;
```

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.UIState\.(DeleteTargetHitSelectorTest|DragDeleteSelection)"`（新規 `.cs` を足したので先に `uloop launch ./moorestech_client --restart`）
Expected: ErrorCount 0 / PASS（既存 DragDeleteSelection 系も含め全件）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineDeleteTarget.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/DeleteTargetHitSelectorTest.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteSelectionCategoryTest.cs
git commit -m "feat(client): 削除ツールの照準を最前面かつドラッグ中は同カテゴリーの最前面で解決する"
```

---

### Task 11 (C4): Undo を「撤去したもの」の同じ抽象で記録・復元する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/IRemovedObject.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/IRemovalRestoreSender.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/IBlockOccupancyQuery.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/RemovedBlock.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/RemovedConnectionLine.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/RemovedRail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/VanillaRemovalRestoreSender.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/RemovedObjectCollector.cs`（撤去物と「記録できなかった物」の件数を集める）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/RemovedRailCreateResult.cs`（レール記録の結果と記録しない理由）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Notification/ClientLocalNotificationSource.cs`（クライアント側で決まる拒否をサーバー通知と同じ表示面へ流す。前例が無い新規パターン。裁定 `.decisions/2026-10-05-計画の弱発火4件を拾い残り3件は据え置く.md` (2)）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Notification/NotificationTopic.cs:26-31,46-75`（ローカル通知も同じ中継で配る）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:107`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractNotificationTest.cs:62,74`（`NotificationTopic` の第3引数）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`（`ClientLocalNotificationSource` の static 参照）
- Modify: `moorestech_web/webui/src/features/notification/notificationMessages.ts`（`denied.undoRestoreSkipped`）
- Modify: `Localization/localization.csv`（`ui.notification.undoRestoreSkipped` 行）
- Regenerate: `moorestech_web/webui/src/shared/i18n/generated/*`（`pnpm gen:i18n`）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailObjectIdCodec.cs`（canonical 区間の選択は Task 5 の `Game.Train.RailGraph.Utility.RailSegmentPairing` を呼ぶ。サーバーの返却キーと同じ規則を1か所に保つ）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailEdgeClassifier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/RemoveOperationRecord.cs`（全面書き換え。Task B2 が72行目に入れた `NoAutoConnect` 送信は `VanillaRemovalRestoreSender.PlaceBlocks` へ移る）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/IBuildOperationRecord.cs`（引数型を `IBlockOccupancyQuery` へ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/PlaceOperationRecord.cs`（`UndoAsync` 引数型の追従のみ。BlockRemove は従来どおり `ClientContext` 経由）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildUndoService.cs:18,48`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectDataStore.cs:16,137`（`IBlockOccupancyQuery` 実装）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/IDeleteTarget.cs`（`CollectRemovedObjects` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs`（`CollectRemovedObjects`。付随線は Task C5）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/DeleteTargetRail.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs:99-100,126-127,199-210`（private static `SelectCanonicalPair`/`ComputeRailObjectId` を削除し、呼び出しを `RailSegmentPairing.SelectCanonicalPair`（`using Game.Train.RailGraph.Utility;`）と `RailObjectIdCodec.ComputeRailObjectId` へ置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/Object/TrainCarEntityChildrenObject.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineDeleteTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs:24-27,92-112`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs:19-23`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs:25-28`（未使用の `RailGraphClientCache cache` 引数を `IRemovalRestoreSender restoreSender` に置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`（`ConnectElectricWire` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`（`VanillaRemovalRestoreSender` 登録）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/Fakes/FakeDeleteTarget.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/FakeRemovalRestoreSender.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteSelectionTest.cs` / `DragDeleteSelectionCategoryTest.cs` / `DragDeleteDenyReasonTest.cs`（コンストラクタ追従）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/RightShortPressTransitionTest.cs:67` / `UIStateCameraInteractionTest.cs:91` / `UIStateFocusRestorationTest.cs:118`（`DeleteObjectState` 第1引数を `new FakeRemovalRestoreSender()` に）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/RemoveOperationRecordTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/RemovedRailTest.cs`

**Interfaces:**
- Consumes: Task C2 `ConnectionLineDeleteTarget`（FromId/ToId/ConnectToolGuid/Kind）、`ConnectionLineKind`。Task B2 `Server.Protocol.PacketResponse.BlockPlacementWiring.NoAutoConnect` / `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo>, BlockPlacementWiring)`、Task B3 `VanillaApiSendOnly.ConnectRailByDestination(ConnectionDestination, ConnectionDestination, Guid)`（同方向に接続済みなら無課金で成功）
- Produces:
  - `public interface IRemovedObject { object RestoreKey { get; } BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy); void SendConnectionRestore(IRemovalRestoreSender sender); }`、`public enum BlockRestoreOutcome { NotABlock, Appended, SkippedOccupied }`
  - `public interface IRemovalRestoreSender { void PlaceBlocks(List<PlaceInfo> placeInfos); void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid); void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid); void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid); void NotifyRestoreSkipped(int skippedCount); }`（`NotifyRestoreSkipped` はクライアント側で戻せなかった件数をプレイヤーへ知らせる）
  - `public class RemovedObjectCollector { void Add(IRemovedObject removed); void AddUnrecordable(string reason); IReadOnlyList<IRemovedObject> Objects; int UnrecordableCount }`（`AddUnrecordable` は理由を `Debug.LogWarning("[RemovalRestore] unrecordable: ...")` へ出して数える）
  - `public enum RemovedRailCreateOutcome { Created, NodeNotSynced, StationInternal, FreeSegment }`、`public readonly struct RemovedRailCreateResult { RemovedRailCreateOutcome Outcome; RemovedRail Rail; }`（`FreeSegment`＝種類Emptyの無償区間は設計上記録しない。`NodeNotSynced` は記録できなかった物として数える）
  - `public class ClientLocalNotificationSource { IObservable<NotificationMessagePack> OnNotification; void Notify(NotificationMessagePack message); }`（UniRx `Subject` を private 保持）、`ClientDIContext.ClientLocalNotificationSource`（static）
  - `public interface IBlockOccupancyQuery { bool IsOverlapPositionInfo(BlockPositionInfo target); }`
  - `RemovedBlock.From(BlockGameObject block)`、`new RemovedConnectionLine(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB, Guid connectToolGuid)`、`RemovedRail.Create(RailGraphClientCache cache, int canonicalFrom, int canonicalTo) : RemovedRailCreateResult`
  - `IDeleteTarget.CollectRemovedObjects(RemovedObjectCollector collector)`
  - `RemoveOperationRecord.CreateFrom(IReadOnlyList<IDeleteTarget> targets, IRemovalRestoreSender sender)`、`bool HasRemovedObjects`
  - `DragDeleteSelection(BuildOperationHistory history, IRemovalRestoreSender restoreSender)`、`DeleteObjectService(BuildOperationHistory, IMouseCursorTooltip, IRemovalRestoreSender)`、`DeleteObjectState(IRemovalRestoreSender restoreSender, UiStateCameraPolicyService, BuildOperationHistory, BuildUndoService, PlacementTargetPickService, RightShortPressInputService, IMouseCursorTooltip)`
  - `RailObjectIdCodec.ComputeRailObjectId(int, int)` / `Decode(ulong railObjectId)`（public static。canonical 選択は `RailSegmentPairing.SelectCanonicalPair` を直接呼ぶ）
  - `RailEdgeClassifier.IsStationInternalEdge(IRailNode from, IRailNode to)`（public static）
  - `VanillaApiSendOnly.ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)`

- [ ] **Step 1: テストを書く**

`FakeRemovalRestoreSender.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Core.Master;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     復元送信を順に記録するテスト用送信器
    ///     Test sender recording restore sends in order
    /// </summary>
    public class FakeRemovalRestoreSender : IRemovalRestoreSender
    {
        public readonly List<string> Sent = new();
        public readonly List<List<PlaceInfo>> PlacedBatches = new();

        public void PlaceBlocks(List<PlaceInfo> placeInfos)
        {
            PlacedBatches.Add(new List<PlaceInfo>(placeInfos));
            Sent.Add($"place:{placeInfos.Count}");
        }

        public void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            Sent.Add($"wire:{posA}-{posB}:{connectToolGuid}");
        }

        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            Sent.Add($"chain:{posA}-{posB}:{connectToolGuid}");
        }

        public void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
        {
            Sent.Add($"rail:{(Vector3Int)from.blockPosition}-{(Vector3Int)to.blockPosition}:{connectToolGuid}");
        }

        public void NotifyRestoreSkipped(int skippedCount)
        {
            Sent.Add($"skipped:{skippedCount}");
        }
    }
}
```

`RemoveOperationRecordTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.UI.UIState.State;
using Client.Tests.UIState.Fakes;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     撤去Undoが撤去物を重複排除し、ブロック→線の順に復元送信することを検証する
    ///     Verifies the removal undo dedupes removed objects and sends restores blocks-first, then lines
    /// </summary>
    public class RemoveOperationRecordTest
    {
        [Test]
        public void BlocksAreRestoredBeforeLinesAndDuplicatesCollapse()
        {
            // ブロック撤去の巻き込み線と直接選択した線が同じ線なら1回だけ引き直す
            // A line both cascaded from a block and selected directly is restored once
            var guid = Guid.NewGuid();
            var posA = new Vector3Int(0, 0, 0);
            var posB = new Vector3Int(5, 0, 0);
            var line = new RemovedConnectionLine(ConnectionLineKind.ElectricWire, posA, posB, guid);
            var sameLineReversed = new RemovedConnectionLine(ConnectionLineKind.ElectricWire, posB, posA, guid);
            var block = new RemovedBlock(posA, new BlockId(1), BlockDirection.North);
            var targets = new List<IDeleteTarget>
            {
                new FakeDeleteTarget { RemovedObjects = { line } },
                new FakeDeleteTarget { RemovedObjects = { block, sameLineReversed } },
            };
            var sender = new FakeRemovalRestoreSender();

            var record = RemoveOperationRecord.CreateFrom(targets, sender);
            record.UndoAsync(new FakeOccupancy(false)).Forget();

            Assert.IsTrue(record.HasRemovedObjects);
            CollectionAssert.AreEqual(new[] { "place:1", $"wire:{posA}-{posB}:{guid}" }, sender.Sent);
        }

        [Test]
        public void OccupiedBlockIsSkippedButLinesAreStillSent()
        {
            // 占有済みセルは再設置しないが、線の引き直しは送る（サーバーが端点不在を判定する）
            // An occupied cell is not re-placed, but the line restore is still sent (the server judges missing endpoints)
            var guid = Guid.NewGuid();
            var block = new RemovedBlock(Vector3Int.zero, new BlockId(1), BlockDirection.North);
            var chain = new RemovedConnectionLine(ConnectionLineKind.GearChain, Vector3Int.zero, new Vector3Int(3, 0, 0), guid);
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget { RemovedObjects = { block, chain } } }, sender);

            record.UndoAsync(new FakeOccupancy(true)).Forget();

            CollectionAssert.AreEqual(new[] { $"chain:{Vector3Int.zero}-{new Vector3Int(3, 0, 0)}:{guid}", "skipped:1" }, sender.Sent);
        }

        [Test]
        public void UnrecordableObjectsAreNotifiedOnUndo()
        {
            // 撤去時に記録できなかった物も、Undo時にプレイヤーへ件数で知らせる
            // Objects that could not be recorded at removal are also reported to the player by count on undo
            var target = new FakeDeleteTarget { UnrecordableReasons = { "rail node not synced" } };
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { target }, sender);

            Assert.IsTrue(record.HasRemovedObjects);
            record.UndoAsync(new FakeOccupancy(false)).Forget();

            CollectionAssert.AreEqual(new[] { "skipped:1" }, sender.Sent);
        }

        [Test]
        public void TargetsWithoutRemovedObjectsYieldEmptyRecord()
        {
            // 列車のように何も記録しない対象だけなら履歴に積まない（記録できなかった物も無い）
            // Only targets recording nothing (like trains), with nothing unrecordable either, produce no history entry
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget() }, new FakeRemovalRestoreSender());
            Assert.IsFalse(record.HasRemovedObjects);
        }

        private class FakeOccupancy : IBlockOccupancyQuery
        {
            private readonly bool _occupied;
            public FakeOccupancy(bool occupied) { _occupied = occupied; }
            public bool IsOverlapPositionInfo(BlockPositionInfo target) { return _occupied; }
        }
    }
}
```

（`RemovedBlock` は Guid ではなく既存 `RemovedBlockInfo` と同じ `(Vector3Int, BlockId, BlockDirection)` を持つ。`RemovedBlock.AppendBlockRestore` は `MasterHolder.BlockMaster.GetBlockMaster(BlockId).BlockSize` を引くため、テストの `new BlockId(1)` はテスト用 mod に実在する BlockId を使う。実装者はテスト mod の最小 BlockId で置換すること。）

`RemovedRailTest.cs`（駅内部・駅隣接の自動レールは `RailTypeGuid == Guid.Empty` で同期されるため、記録から外れることを固定する）:

```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Train.RailGraph;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     レール区間の撤去記録が、引いた種類を持つ区間だけを対象にすることを検証する
    ///     Verifies rail removal records cover only edges carrying the tool kind they were drawn with
    /// </summary>
    public class RemovedRailTest
    {
        [Test]
        public void EdgeWithRailTypeIsRecordedAndRestoredByDestination()
        {
            // 種類付きの区間は両端ConnectionDestinationと種類で引き直す
            // An edge with a rail type is restored by both ConnectionDestinations and that type
            var railType = Guid.NewGuid();
            var cache = CreateTwoPierCache(railType);
            var sender = new FakeRemovalRestoreSender();

            var result = RemovedRail.Create(cache, 0, 2);
            Assert.AreEqual(RemovedRailCreateOutcome.Created, result.Outcome);
            result.Rail.SendConnectionRestore(sender);

            CollectionAssert.AreEqual(new[] { $"rail:{new Vector3Int(0, 0, 0)}-{new Vector3Int(10, 0, 0)}:{railType}" }, sender.Sent);
        }

        [Test]
        public void EdgeWithEmptyRailTypeIsNotRecorded()
        {
            // 駅内部・駅隣接の自動レール（種類Empty）は記録しない（駅の再設置で自動的に戻る）
            // Station-internal / station-adjacent auto rails (Empty type) are not recorded (station re-placement restores them)
            var cache = CreateTwoPierCache(Guid.Empty);
            Assert.AreEqual(RemovedRailCreateOutcome.FreeSegment, RemovedRail.Create(cache, 0, 2).Outcome);
        }

        [Test]
        public void UnsyncedNodeIsNotRecorded()
        {
            // 未同期のノードを指す区間は記録しない
            // An edge pointing at an unsynced node is not recorded
            Assert.AreEqual(RemovedRailCreateOutcome.NodeNotSynced, RemovedRail.Create(RailGraphClientCache.CreateForEditorTest(), 0, 2).Outcome);
        }

        private static RailGraphClientCache CreateTwoPierCache(Guid railType)
        {
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 0, new Vector3Int(0, 0, 0));
            UpsertPier(cache, 2, new Vector3Int(10, 0, 0));
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);
            return cache;
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int blockPosition)
        {
            var origin = (Vector3)blockPosition;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, false), origin + Vector3.back, origin + Vector3.forward);
        }
    }
}
```

`FakeDeleteTarget.cs` に追加:

```csharp
        // CollectRemovedObjectsで返す撤去物と記録できなかった理由（未設定なら何も記録しない）
        // Removed objects and unrecordable reasons reported by CollectRemovedObjects (records nothing when empty)
        public readonly List<IRemovedObject> RemovedObjects = new();
        public readonly List<string> UnrecordableReasons = new();

        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            foreach (var removedObject in RemovedObjects) collector.Add(removedObject);
            foreach (var reason in UnrecordableReasons) collector.AddUnrecordable(reason);
        }
```

既存 DragDelete 系テストの `new DragDeleteSelection(new BuildOperationHistory())` はすべて `CreateSelection()` に置換し、各テストクラスへ次のヘルパーを足す:

```csharp
        private static DragDeleteSelection CreateSelection()
        {
            return new DragDeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
        }
```

- [ ] **Step 2: 実装を書く**

`IRemovedObject.cs`:

```csharp
using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     削除ツールで消えた1つの物（ブロック・接続線・レール）。種類を問わず同じ形でUndo履歴に載る
    ///     One thing removed by the delete tool (block, connection line, rail), held in undo history in the same shape regardless of kind
    /// </summary>
    public interface IRemovedObject
    {
        // 同じ物を二重に復元しないための論理キー
        // Logical key preventing the same thing from being restored twice
        object RestoreKey { get; }

        // ブロック相: 再設置すべきセルを積む。ブロック以外はNotABlock、占有で戻せなければSkippedOccupied
        // Block phase: append the cell to re-place; non-blocks return NotABlock, an occupied footprint returns SkippedOccupied
        BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy);

        // 接続相: ブロック再設置の送信後に線の引き直しを送る（ブロックは何もしない）
        // Connection phase: send the line restore after the block re-place has been sent (blocks do nothing)
        void SendConnectionRestore(IRemovalRestoreSender sender);
    }

    /// <summary>
    ///     ブロック相1件の結果
    ///     Outcome of one block-phase append
    /// </summary>
    public enum BlockRestoreOutcome
    {
        NotABlock,
        Appended,
        SkippedOccupied,
    }
}
```

`IRemovalRestoreSender.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去Undoの復元要求をサーバーへ送る口。サーバーは同一クライアントのパケットをFIFO単一スレッドで処理するため送信順＝適用順
    ///     Outlet sending removal-undo restore requests; the server processes a client's packets FIFO on one thread, so send order equals apply order
    /// </summary>
    public interface IRemovalRestoreSender
    {
        void PlaceBlocks(List<PlaceInfo> placeInfos);
        void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid);
        void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid);
        void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid);

        // クライアント側で戻せなかった件数（占有済み・記録不能）をプレイヤーへ知らせる。サーバー側の拒否はサーバーが通知する
        // Tell the player how many things the client could not restore (occupied / unrecordable); server-side refusals are notified by the server
        void NotifyRestoreSkipped(int skippedCount);
    }
}
```

`IBlockOccupancyQuery.cs`:

```csharp
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     再設置前の占有判定だけを公開する読み取り口
    ///     Read-only outlet exposing only the occupancy check before re-placing
    /// </summary>
    public interface IBlockOccupancyQuery
    {
        bool IsOverlapPositionInfo(BlockPositionInfo target);
    }
}
```

`RemovedBlock.cs`（既存 `RemovedBlockInfo` と `RemoveOperationRecord.UndoAsync` 内の占有判定・縦向き変換をここへ移す）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去したブロック1つ。復元は占有範囲が空いているときだけ再設置する（CreateParamsは復元不可のため空）
    ///     One removed block; restored only when its footprint is free (CreateParams cannot be restored, so empty)
    /// </summary>
    public class RemovedBlock : IRemovedObject
    {
        private readonly Vector3Int _position;
        private readonly BlockId _blockId;
        private readonly BlockDirection _direction;

        public RemovedBlock(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            _position = position;
            _blockId = blockId;
            _direction = direction;
        }

        public static RemovedBlock From(BlockGameObject block)
        {
            return new RemovedBlock(block.BlockPosInfo.OriginalPos, block.BlockId, block.BlockPosInfo.BlockDirection);
        }

        public object RestoreKey => _position;

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            // 占有中のセルは再設置しない（撤去失敗・他者設置セルを除外）。理由はログへ残し、件数はプレイヤー通知へ回る
            // Skip occupied cells (failed removals or rebuilt cells); log why, and the count goes to the player notification
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(_blockId).BlockSize;
            if (occupancy.IsOverlapPositionInfo(new BlockPositionInfo(_position, _direction, blockSize)))
            {
                Debug.LogWarning($"[RemovalRestore] skip re-place: footprint occupied at {_position}");
                return BlockRestoreOutcome.SkippedOccupied;
            }

            placeInfos.Add(new PlaceInfo
            {
                Position = _position,
                Direction = _direction,
                VerticalDirection = ToVerticalDirection(_direction),
                BlockId = _blockId,
                Placeable = true,
            });
            return BlockRestoreOutcome.Appended;
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
        }

        private static BlockVerticalDirection ToVerticalDirection(BlockDirection direction)
        {
            return direction switch
            {
                BlockDirection.UpNorth or BlockDirection.UpEast or BlockDirection.UpSouth or BlockDirection.UpWest => BlockVerticalDirection.Up,
                BlockDirection.DownNorth or BlockDirection.DownEast or BlockDirection.DownSouth or BlockDirection.DownWest => BlockVerticalDirection.Down,
                _ => BlockVerticalDirection.Horizontal,
            };
        }
    }
}
```

`RemovedConnectionLine.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去した電線・歯車チェーン1本。端点は座標で持つ（再設置でBlockInstanceIdが変わるため）
    ///     One removed wire or gear chain; endpoints are kept as positions because re-placement changes BlockInstanceIds
    /// </summary>
    public class RemovedConnectionLine : IRemovedObject
    {
        private readonly ConnectionLineKind _kind;
        private readonly Vector3Int _posA;
        private readonly Vector3Int _posB;
        private readonly Guid _connectToolGuid;

        public RemovedConnectionLine(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            // 端点順に依らず同じキーになるよう正規化する
            // Normalize so the key does not depend on endpoint order
            var aFirst = IsOrdered(posA, posB);
            _kind = kind;
            _posA = aFirst ? posA : posB;
            _posB = aFirst ? posB : posA;
            _connectToolGuid = connectToolGuid;
        }

        public object RestoreKey => (_kind, _posA, _posB);

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            return BlockRestoreOutcome.NotABlock;
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
            switch (_kind)
            {
                case ConnectionLineKind.ElectricWire:
                    sender.ConnectElectricWire(_posA, _posB, _connectToolGuid);
                    break;
                case ConnectionLineKind.GearChain:
                    sender.ConnectGearChain(_posA, _posB, _connectToolGuid);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_kind), _kind, null);
            }
        }

        private static bool IsOrdered(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z <= b.z;
        }
    }
}
```

`RemovedRail.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Game.Train.SaveLoad;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去したレール1本。端点はノードIDでなくConnectionDestinationで持つ（再設置でノードGuid/IDが変わるため）
    ///     One removed rail; endpoints are ConnectionDestinations, not node ids, since re-placement regenerates node Guids/ids
    /// </summary>
    public class RemovedRail : IRemovedObject
    {
        private readonly ConnectionDestination _from;
        private readonly ConnectionDestination _to;
        private readonly Guid _railTypeGuid;

        private RemovedRail(ConnectionDestination from, ConnectionDestination to, Guid railTypeGuid)
        {
            _from = from;
            _to = to;
            _railTypeGuid = railTypeGuid;
        }

        // canonical化済みの区間から作る。記録しない理由（未同期・駅内部・種類Emptyの無償区間）を結果で区別する
        // Build from a canonical edge; the result distinguishes why it is not recorded (unsynced, station-internal, Empty-typed free segment)
        public static RemovedRailCreateResult Create(RailGraphClientCache cache, int canonicalFrom, int canonicalTo)
        {
            if (!cache.TryGetNode(canonicalFrom, out var fromNode) || !cache.TryGetNode(canonicalTo, out var toNode)) return RemovedRailCreateResult.NotCreated(RemovedRailCreateOutcome.NodeNotSynced);
            if (RailEdgeClassifier.IsStationInternalEdge(fromNode, toNode)) return RemovedRailCreateResult.NotCreated(RemovedRailCreateOutcome.StationInternal);

            // 種類Emptyは駅隣接の自動レール等の無償区間。駅の再設置で自動的に戻るので記録しない
            // Empty-typed edges are free segments such as station-adjacent auto rails; station re-placement restores them, so they are not recorded
            if (!cache.TryGetRailType(canonicalFrom, canonicalTo, out var railTypeGuid) || railTypeGuid == Guid.Empty) return RemovedRailCreateResult.NotCreated(RemovedRailCreateOutcome.FreeSegment);

            return RemovedRailCreateResult.Created(new RemovedRail(fromNode.ConnectionDestination, toNode.ConnectionDestination, railTypeGuid));
        }

        public object RestoreKey => (_from, _to);

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            return BlockRestoreOutcome.NotABlock;
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
            sender.ConnectRail(_from, _to, _railTypeGuid);
        }
    }
}
```

（`IRailNode.ConnectionDestination` は `S/Game.Train/RailGraph/IRailNode.cs:14` に実在。）

`RemovedRailCreateResult.cs`:

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     レール区間を撤去記録にした結果。作れなかった理由を区別する（FreeSegmentは設計上の対象外、NodeNotSyncedは記録不能）
    ///     Result of turning a rail edge into a removal record; distinguishes why it was not created (FreeSegment is out of scope by design, NodeNotSynced is unrecordable)
    /// </summary>
    public enum RemovedRailCreateOutcome
    {
        Created,
        NodeNotSynced,
        StationInternal,
        FreeSegment,
    }

    public readonly struct RemovedRailCreateResult
    {
        public RemovedRailCreateOutcome Outcome { get; }
        public RemovedRail Rail { get; }

        private RemovedRailCreateResult(RemovedRailCreateOutcome outcome, RemovedRail rail)
        {
            Outcome = outcome;
            Rail = rail;
        }

        public static RemovedRailCreateResult Created(RemovedRail rail)
        {
            return new RemovedRailCreateResult(RemovedRailCreateOutcome.Created, rail);
        }

        public static RemovedRailCreateResult NotCreated(RemovedRailCreateOutcome outcome)
        {
            return new RemovedRailCreateResult(outcome, null);
        }
    }
}
```

`RemovedObjectCollector.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去直前に各削除対象から撤去物を集める。記録できなかった物は理由をログへ出して件数だけ数える
    ///     Collects removed objects from each delete target right before removal; unrecordable things are logged with a reason and only counted
    /// </summary>
    public class RemovedObjectCollector
    {
        private readonly List<IRemovedObject> _objects = new();
        public IReadOnlyList<IRemovedObject> Objects => _objects;
        public int UnrecordableCount { get; private set; }

        public void Add(IRemovedObject removed)
        {
            _objects.Add(removed);
        }

        // 復元先を持てない物。Undo時にプレイヤーへ件数で知らせる
        // Something with no restore target; reported to the player by count on undo
        public void AddUnrecordable(string reason)
        {
            Debug.LogWarning($"[RemovalRestore] unrecordable: {reason}");
            UnrecordableCount++;
        }
    }
}
```

`VanillaRemovalRestoreSender.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Train.SaveLoad;
using Server.Event.Notification;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去Undoの復元要求をVanillaApiのSendOnlyで送る本番実装。拒否はサーバーが通知で返す
    ///     Production sender using VanillaApi SendOnly; refusals come back as server notifications
    /// </summary>
    public class VanillaRemovalRestoreSender : IRemovalRestoreSender
    {
        // Undoの再設置は自動接続を止め、記録した線だけを後続で引き直す
        // Undo re-placement suppresses auto-connect; only recorded lines are re-drawn afterwards
        public void PlaceBlocks(List<PlaceInfo> placeInfos)
        {
            ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos, BlockPlacementWiring.NoAutoConnect);
        }

        public void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            ClientContext.VanillaApi.SendOnly.ConnectElectricWire(posA, posB, connectToolGuid);
        }

        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            ClientContext.VanillaApi.SendOnly.ConnectGearChain(posA, posB, connectToolGuid);
        }

        public void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
        {
            ClientContext.VanillaApi.SendOnly.ConnectRailByDestination(from, to, connectToolGuid);
        }

        // サーバー通知と同じ表示面へクライアント側の取りこぼし件数を流す
        // Push the client-side skipped count onto the same display surface as server notifications
        public void NotifyRestoreSkipped(int skippedCount)
        {
            ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied("denied.undoRestoreSkipped", new[] { skippedCount.ToString() }));
        }
    }
}
```

（`BlockPlacementWiring` は Task B2 が `Server.Protocol.PacketResponse` 名前空間・`PlacePacketDto.cs` に置く。）

`VanillaApiSendOnly.cs` に追加（電線延長プロトコルの「既存ブロックへの接続」を応答待ちなしで送る。応答を返すプロトコルを SendOnly で送る前例: `ConnectGearChain`）:

```csharp
        /// <summary>
        /// 既存の電気ブロック間に電線を引く（応答は待たず、拒否は通知で受ける）
        /// Draw a wire between existing electric blocks (no response awaited; refusals arrive as notifications)
        /// </summary>
        public void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            var request = ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(posA, posB, connectToolGuid);
            _packetSender.Send(request);
        }
```

`RemoveOperationRecord.cs`（全面書き換え）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.UI.UIState.State;
using Core.Master;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     撤去1バッチの楽観記録。ブロック・接続線・レールを同じ IRemovedObject として保持する
    ///     Optimistic record of one remove batch, holding blocks, lines and rails alike as IRemovedObject
    /// </summary>
    public class RemoveOperationRecord : IBuildOperationRecord
    {
        private readonly List<IRemovedObject> _removedObjects;
        private readonly int _unrecordableCount;
        private readonly IRemovalRestoreSender _sender;

        private RemoveOperationRecord(List<IRemovedObject> removedObjects, int unrecordableCount, IRemovalRestoreSender sender)
        {
            _removedObjects = removedObjects;
            _unrecordableCount = unrecordableCount;
            _sender = sender;
        }

        // 空バッチをPushしないためのガード。記録できなかった物だけでも、Undo時に通知するため積む
        // Guard against pushing an empty batch; a batch of only unrecordable things is still pushed so undo can report them
        public bool HasRemovedObjects => 0 < _removedObjects.Count || 0 < _unrecordableCount;

        // 撤去直前の各対象から撤去物を集め、論理キーで重複排除する
        // Collect removed objects from every target right before removal, deduped by logical key
        public static RemoveOperationRecord CreateFrom(IReadOnlyList<IDeleteTarget> targets, IRemovalRestoreSender sender)
        {
            var collector = new RemovedObjectCollector();
            foreach (var target in targets) target.CollectRemovedObjects(collector);

            var seenKeys = new HashSet<object>();
            var unique = new List<IRemovedObject>();
            foreach (var removedObject in collector.Objects)
            {
                if (seenKeys.Add(removedObject.RestoreKey)) unique.Add(removedObject);
            }
            return new RemoveOperationRecord(unique, collector.UnrecordableCount, sender);
        }

        public UniTask UndoAsync(IBlockOccupancyQuery occupancy)
        {
            // ブロック相: 空いているセルを1バッチで再設置し、占有で戻せなかった数を数える
            // Block phase: re-place free cells in one batch and count those blocked by occupancy
            var placeInfos = new List<PlaceInfo>();
            var skippedCount = _unrecordableCount;
            foreach (var removedObject in _removedObjects)
            {
                if (removedObject.AppendBlockRestore(placeInfos, occupancy) == BlockRestoreOutcome.SkippedOccupied) skippedCount++;
            }
            if (placeInfos.Count != 0) _sender.PlaceBlocks(placeInfos);

            // 接続相: 再設置の後に線を引き直す（サーバーFIFOで再設置が先に適用される）
            // Connection phase: re-draw lines after the re-place (server FIFO applies the re-place first)
            foreach (var removedObject in _removedObjects) removedObject.SendConnectionRestore(_sender);

            // クライアント側で戻せなかった分はプレイヤーへ件数で知らせる（裁定: できた分だけ戻し残りは通知）
            // Report what the client could not restore to the player by count (ruling: restore what we can, notify the rest)
            if (0 < skippedCount) _sender.NotifyRestoreSkipped(skippedCount);
            return UniTask.CompletedTask;
        }
    }
}
```

`IBuildOperationRecord.UndoAsync(BlockGameObjectDataStore)` を `UndoAsync(IBlockOccupancyQuery occupancy)` に変更し、`PlaceOperationRecord.UndoAsync` は引数型だけ追従（本体で `blockGameObjectDataStore.TryGetBlockGameObject` を使っているなら、`PlaceOperationRecord` 側は `ClientDIContext.BlockGameObjectDataStore` を直接参照する形へ置換する）。`BuildUndoService` のフィールド型は `BlockGameObjectDataStore` のまま（`IBlockOccupancyQuery` として渡す）。`BlockGameObjectDataStore` の宣言を `public class BlockGameObjectDataStore : MonoBehaviour, ISkitBlockObjectControl, IBlockOccupancyQuery` にする（`IsOverlapPositionInfo` は既存 public メソッドで満たす）。

`IDeleteTarget.cs` に追加:

```csharp
        /// <summary>
        ///     撤去で消えるものをUndo用に積む（自身と、巻き込みで消える接続線・レール）。何も復元しない対象は積まない
        ///     Append what this removal makes disappear for undo (self plus cascaded lines/rails); targets with nothing to restore append nothing
        /// </summary>
        void CollectRemovedObjects(RemovedObjectCollector collector);
```

`BlockGameObjectChild.cs` に追加（付随線の収集は Task C5 で追記）:

```csharp
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            collector.Add(RemovedBlock.From(BlockGameObject));
        }
```

`TrainCarEntityChildrenObject.cs` に追加:

```csharp
        // 車両の撤去はUndo対象外（ADR 0076の範囲外）なので何も積まない
        // Train car removal is outside undo (out of ADR 0076 scope), so nothing is appended
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
        }
```

`ConnectionLineDeleteTarget.cs` に追加:

```csharp
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            // 端点ブロックが解決できない線は復元先を持てないため記録不能として数える
            // A line whose endpoints cannot be resolved has no restore target, so count it as unrecordable
            if (!TryResolveEndpointPositions(out var fromPos, out var toPos))
            {
                collector.AddUnrecordable($"line endpoint block not found from={FromId} to={ToId}");
                return;
            }
            collector.Add(new RemovedConnectionLine(Kind, fromPos, toPos, ConnectToolGuid));
        }
```

`RailObjectIdCodec.cs`（`TrainRailObjectManager` の private static `SelectCanonicalPair`/`ComputeRailObjectId` を廃し、canonical 選択は Task 5 の `RailSegmentPairing.SelectCanonicalPair`（サーバーの返却キーと同じ正本。同値時も同じ規則）を、ID符号化はここを呼ぶ。manager は 211 行→200 行未満へ縮む）:

```csharp
namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール描画1本を表すID（canonicalな区間ペア）の符号化・復号。canonical の選択は RailSegmentPairing が正本
    ///     Encode/decode the id of one drawn rail (canonical edge pair); RailSegmentPairing owns the canonical choice
    /// </summary>
    public static class RailObjectIdCodec
    {
        public static ulong ComputeRailObjectId(int canonicalFrom, int canonicalTo)
        {
            return (ulong)canonicalFrom + ((ulong)canonicalTo << 32);
        }

        public static (int canonicalFrom, int canonicalTo) Decode(ulong railObjectId)
        {
            return (unchecked((int)(uint)railObjectId), unchecked((int)(uint)(railObjectId >> 32)));
        }
    }
}
```

`RailEdgeClassifier.cs`（`DeleteTargetRail.IsStationInternalEdge` を移設し、DeleteTargetRail も呼ぶ）:

```csharp
using Game.Train.RailGraph;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール区間の分類（プレイヤーが切れない駅内部の区間か）
    ///     Rail edge classification (whether it is a station-internal edge players cannot cut)
    /// </summary>
    public static class RailEdgeClassifier
    {
        public static bool IsStationInternalEdge(IRailNode from, IRailNode to)
        {
            if (!from.StationRef.HasStation || !to.StationRef.HasStation) return false;
            return from.StationRef.StationBlockInstanceId.Equals(to.StationRef.StationBlockInstanceId);
        }
    }
}
```

`DeleteTargetRail.cs`: `Delete()` と `CanDelete()` の `fromId/toId` 復号を `RailObjectIdCodec.Decode(railObjectId)` に置換、`IsStationInternalEdge` を削除して `RailEdgeClassifier.IsStationInternalEdge` を呼ぶ。追加:

```csharp
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            // レールIDはcanonical区間なのでそのまま記録へ写す。無償区間は設計上記録せず、未同期・駅内部は記録不能として数える
            // The rail object id is already canonical; free segments are skipped by design, unsynced/station-internal count as unrecordable
            var (fromId, toId) = RailObjectIdCodec.Decode(RailObjectIdCarrier.GetRailObjectId());
            var result = RemovedRail.Create(_railGraphClientCache, fromId, toId);
            switch (result.Outcome)
            {
                case RemovedRailCreateOutcome.Created:
                    collector.Add(result.Rail);
                    break;
                case RemovedRailCreateOutcome.FreeSegment:
                    break;
                default:
                    collector.AddUnrecordable($"rail edge {fromId}->{toId}: {result.Outcome}");
                    break;
            }
        }
```

`DragDeleteSelection.cs`: コンストラクタに `IRemovalRestoreSender restoreSender` を追加してフィールド保持。`CommitDelete` を次に置換（撤去物の収集は Delete 送信前に行う。送信後はサーバー応答で端点が消え得るため）:

```csharp
        public void CommitDelete()
        {
            if (_canceled) return;

            // 撤去物は削除送信より前に集める（送信後は端点ブロックが消え得る）
            // Collect removed objects before sending deletes (endpoints may vanish afterwards)
            var committed = new List<IDeleteTarget>(_selectedTargets.Values);
            var record = RemoveOperationRecord.CreateFrom(committed, _restoreSender);

            foreach (var target in committed)
            {
                // Delete はサーバー往復の非同期なので即座に赤プレビューだけ戻す
                // Delete is async over the server, so we just clear the red preview immediately
                target.Delete();
                target.ResetMaterial();
            }

            _selectedTargets.Clear();
            _sessionFilter = DeleteAimFilter.Frontmost;

            // Ctrl+Z用のUndo履歴を記録（空バッチはPushしない）
            // Record the undo history for Ctrl+Z (skip empty batches)
            if (record.HasRemovedObjects) _buildOperationHistory.Push(record);
        }
```

`DeleteObjectService` のコンストラクタを `DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender)` にして `new DragDeleteSelection(buildOperationHistory, restoreSender)`。`DeleteObjectState` の第1引数 `RailGraphClientCache cache`（未使用）を `IRemovalRestoreSender restoreSender` に置き換え、`new DeleteObjectService(buildOperationHistory, tooltip, restoreSender)`。`using Client.Game.InGame.Train.RailGraph;` が不要になれば外す。

`MainGameInteractionRegistration.cs` に `builder.Register<VanillaRemovalRestoreSender>(Lifetime.Singleton).As<IRemovalRestoreSender>();`、`builder.Register<ClientLocalNotificationSource>(Lifetime.Singleton);`

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Notification/ClientLocalNotificationSource.cs`（新規。クライアント側で決まる拒否をサーバー通知と同じ表示面へ流す口。前例が無いため新規パターンとしてレビュー注目点に挙げる）:

```csharp
using System;
using Server.Event.Notification;
using UniRx;

namespace Client.Game.InGame.UI.Notification
{
    /// <summary>
    ///     クライアント側だけで決まる拒否・取りこぼしを、サーバー通知と同じ通知表示へ流す発行元
    ///     Publisher pushing client-only refusals and skips onto the same notification display as server notifications
    /// </summary>
    public class ClientLocalNotificationSource
    {
        private readonly Subject<NotificationMessagePack> _onNotification = new();
        public IObservable<NotificationMessagePack> OnNotification => _onNotification;

        public void Notify(NotificationMessagePack message)
        {
            _onNotification.OnNext(message);
        }
    }
}
```

`ClientDIContext.cs` に `public static ClientLocalNotificationSource ClientLocalNotificationSource { get; private set; }` を追加し、コンストラクタで `ClientLocalNotificationSource = diContainer.DIContainerResolver.Resolve<ClientLocalNotificationSource>();`（`using Client.Game.InGame.UI.Notification;`）。

`NotificationTopic.cs`: コンストラクタにローカル発行元を受け、サーバー通知と同じ処理へ合流させる（受信処理は `NotificationMessagePack` を受ける `Publish` へ分ける）:

```csharp
        private readonly IDisposable _localSubscription;

        public NotificationTopic(WebSocketHub hub, IVanillaApiEvent vanillaApiEvent, ClientLocalNotificationSource localNotificationSource)
        {
            _hub = hub;
            _subscription = vanillaApiEvent.SubscribeEventResponse(NotificationService.EventTag, OnNotification);

            // クライアント側で決まる拒否も同じ中継・同じ表で表示する
            // Client-side refusals go through the same relay and the same table
            _localSubscription = localNotificationSource.OnNotification.Subscribe(Publish);
        }
```
```csharp
        public void Dispose()
        {
            _subscription.Dispose();
            _localSubscription.Dispose();
        }

        private void OnNotification(byte[] payload)
        {
            Publish(MessagePackSerializer.Deserialize<NotificationMessagePack>(payload));
        }
```
既存 `OnNotification` の本体（`var message = ...` 以降）は `private void Publish(NotificationMessagePack message)` へそのまま移す（`using Client.Game.InGame.UI.Notification;`・`using UniRx;` を追加）。

`WebUiGameBinder.cs:107`:

```csharp
            hub.RegisterTopic(NotificationTopic.TopicName, new NotificationTopic(hub, ClientContext.VanillaApi.Event, ClientDIContext.ClientLocalNotificationSource));
```

`WireContractNotificationTest.cs:62,74`: `new NotificationTopic(new WebSocketHub(), vanillaApiEvent)` → `new NotificationTopic(new WebSocketHub(), vanillaApiEvent, new ClientLocalNotificationSource())`。

`Localization/localization.csv`（`ui.notification.` 行の並びの末尾へ。列: key,Source,english,japanese,german,korean）:

```csv
ui.notification.undoRestoreSkipped,Undo could not restore {p0} removed objects,Undo could not restore {p0} removed objects,元に戻せなかった撤去物が{p0}件あります,{p0} entfernte Objekte konnten nicht wiederhergestellt werden,되돌리지 못한 철거물이 {p0}개 있습니다
```

`notificationMessages.ts` の `notificationKeys` へ:

```ts
  ["denied.undoRestoreSkipped", L.ui.notification.undoRestoreSkipped],
```
（クライアント発行のidなので `notificationServerIdCoverage.test.ts` の走査対象外。表に載せれば `resolveNotificationKey` で解決する）

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(BuildUndo|UIState|WebUi)\."`（新規 `.cs` を足したので先に `uloop launch ./moorestech_client --restart`。csv を変えたので `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true`）→ `cd moorestech_web/webui && pnpm gen:i18n && pnpm test -- notificationMessages notificationServerIdCoverage localizationKeysFreshness`
Expected: ErrorCount 0 / PASS（RemoveOperationRecordTest 4件・RemovedRailTest 3件＋既存 BuildOperationHistoryTest・DragDelete 系・UIState 系）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo moorestech_client/Assets/Scripts/Client.Game/InGame/Block moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/Object/TrainCarEntityChildrenObject.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineDeleteTarget.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs moorestech_client/Assets/Scripts/Client.Tests moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Notification moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Game Localization/localization.csv moorestech_web/webui/src/features/notification moorestech_web/webui/src/shared/i18n/generated
git commit -m "feat(client): 撤去Undoをブロック・接続線・レール共通のIRemovedObjectで記録し、戻せなかった件数を通知する"
```

---

### Task 12 (C5): ブロック撤去に巻き込まれる線・レールの赤表示と Undo 記録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/AttachedRailEdgeEnumerator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/BlockAttachedConnectionResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs`（`TryGetRailChain` 追加）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailChainRemovePreview.cs`（レール1本の赤プレビュー要求者を数える部品。`BezierRailChain` の GameObject に実行時に付き、`BezierRailChain.cs`（319行の既存ファイル）自体は触らない）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/DeleteTargetRail.cs:36-43`（赤表示を `RailChainRemovePreview` 経由へ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs`（SetRemovePreviewing / ResetMaterial / CollectRemovedObjects）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/AttachedRailEdgeEnumeratorTest.cs`

**Interfaces:**
- Consumes: Task C2 `ConnectionLineRegistry.GetLinesAttachedTo`、`IRemovePreviewable`、`RemovePreviewRequests`、Task C4 `RemovedRail.Create`、`RemovedObjectCollector`、`RailObjectIdCodec`
- Produces:
  - `public static class AttachedRailEdgeEnumerator { static void Collect(RailGraphClientCache cache, Vector3Int blockPosition, ICollection<(int canonicalFrom, int canonicalTo)> edges) }`
  - `public class BlockAttachedConnectionResolver` — ctor `(ConnectionLineRegistry registry, RailGraphClientCache railCache)`、`void RequestCascadePreview(BlockGameObject block)`、`void ReleaseCascadePreview(BlockGameObject block)`（要求者はそのブロック。線・レール自身のホバー／選択の赤は消さない）、`void CollectRemovedConnections(BlockGameObject block, RemovedObjectCollector collector)`
  - `public class RailChainRemovePreview : MonoBehaviour, IRemovePreviewable` — `static RailChainRemovePreview Of(BezierRailChain chain)`（無ければ付ける）。最初の要求で `chain.SetRemovePreviewing()`、最後の解除で `chain.ResetMaterial()`
  - `TrainRailObjectManager.TryGetRailChain(ulong railObjectId, out BezierRailChain chain)`
  - `ClientDIContext.BlockAttachedConnectionResolver`（static, `{ get; private set; }`）

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     ブロック座標に付いたレール区間を物理1本＝1件で列挙することを検証する
    ///     Verifies rail edges attached to a block position are enumerated once per physical rail
    /// </summary>
    public class AttachedRailEdgeEnumeratorTest
    {
        [Test]
        public void OnePhysicalRailBetweenTwoPiersIsEnumeratedOnce()
        {
            // 橋脚A(ノード0/1)と橋脚B(ノード2/3)を往復1組で結ぶ
            // Connect pier A (nodes 0/1) and pier B (nodes 2/3) with one round-trip pair
            var cache = RailGraphClientCache.CreateForEditorTest();
            var pierA = new Vector3Int(0, 0, 0);
            var pierB = new Vector3Int(10, 0, 0);
            UpsertPier(cache, 0, pierA);
            UpsertPier(cache, 2, pierB);
            var railType = Guid.NewGuid();
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);

            var edgesOfA = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(cache, pierA, edgesOfA);
            var edgesOfB = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(cache, pierB, edgesOfB);

            // 両側から引いても同じcanonical区間1件になる
            // Either side yields the same single canonical edge
            Assert.AreEqual(1, edgesOfA.Count);
            CollectionAssert.AreEqual(edgesOfA, edgesOfB);
        }

        [Test]
        public void BlockWithoutRailsYieldsNothing()
        {
            // レールを持たないブロックは何も列挙しない
            // A block with no rails enumerates nothing
            var edges = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(RailGraphClientCache.CreateForEditorTest(), Vector3Int.one, edges);
            Assert.AreEqual(0, edges.Count);
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int blockPosition)
        {
            var origin = (Vector3)blockPosition;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, false), origin + Vector3.back, origin + Vector3.forward);
        }
    }
}
```

（`ConnectionDestination` のコンストラクタ引数順は `S/Game.Train/SaveLoad/ConnectionDestination.cs:29` 以降を確認して合わせる。）

- [ ] **Step 2: 実装を書く**

`AttachedRailEdgeEnumerator.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Game.Train.RailGraph.Utility;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     指定ブロック座標のレールノードから出る区間をcanonical化して列挙する（往復の2辺は1本として数える）
    ///     Enumerates canonical edges leaving rail nodes at a block position (the two directions of one rail count once)
    /// </summary>
    public static class AttachedRailEdgeEnumerator
    {
        public static void Collect(RailGraphClientCache cache, Vector3Int blockPosition, ICollection<(int canonicalFrom, int canonicalTo)> edges)
        {
            var seen = new HashSet<(int, int)>();
            for (var nodeId = 0; nodeId < cache.Nodes.Count; nodeId++)
            {
                // このブロックに属するノードだけを見る（表裏ノードとも同じブロック座標を持つ）
                // Only nodes owned by this block (front and back nodes share the block position)
                var node = cache.Nodes[nodeId];
                if (node == null || (Vector3Int)node.ConnectionDestination.blockPosition != blockPosition) continue;

                // 出る辺だけで両向きを網羅できる（入る辺は対向ノードの出る辺）
                // Outgoing edges cover both directions (an incoming edge is the opposite node's outgoing one)
                foreach (var (targetId, _) in cache.ConnectNodes[nodeId])
                {
                    var canonical = RailSegmentPairing.SelectCanonicalPair(nodeId, targetId);
                    if (seen.Add(canonical)) edges.Add(canonical);
                }
            }
        }
    }
}
```

`RailChainRemovePreview.cs`（`C/Client.Game/InGame/Train/RailGraph/RailChainRemovePreview.cs`）:

```csharp
using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール1本の赤プレビューを要求者ごとに数え、最初の要求で赤く・最後の解除で戻す（電線・チェーンと同じ規則）
    ///     Counts red-preview requesters for one rail; reddens on the first request and resets on the last release (same rule as wires/chains)
    /// </summary>
    public class RailChainRemovePreview : MonoBehaviour, IRemovePreviewable
    {
        private readonly RemovePreviewRequests _requests = new();
        private BezierRailChain _chain;

        // チェーンのGameObjectに1つだけ付ける（プレハブを変えずに済むよう実行時に付与）
        // Attach exactly one per chain GameObject (added at runtime so the prefab stays untouched)
        public static RailChainRemovePreview Of(BezierRailChain chain)
        {
            var preview = chain.GetComponent<RailChainRemovePreview>();
            if (preview != null) return preview;

            preview = chain.gameObject.AddComponent<RailChainRemovePreview>();
            preview._chain = chain;
            return preview;
        }

        public void RequestRemovePreview(object requester)
        {
            if (_requests.Add(requester)) _chain.SetRemovePreviewing();
        }

        public void ReleaseRemovePreview(object requester)
        {
            if (_requests.Remove(requester)) _chain.ResetMaterial();
        }
    }
}
```

`DeleteTargetRail.cs` L36-43 を置換（自分のホバー・選択も要求者の1人として数える）:

```csharp
        public void SetRemovePreviewing()
        {
            RailChainRemovePreview.Of(RailChain).RequestRemovePreview(this);
        }
        public void ResetMaterial()
        {
            RailChainRemovePreview.Of(RailChain).ReleaseRemovePreview(this);
        }
```

`BlockAttachedConnectionResolver.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Train.RailGraph;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     ブロック撤去に巻き込まれて消える接続線・レールの解決（赤表示とUndo記録）
    ///     Resolves connection lines and rails that vanish with a block removal (red preview and undo records)
    /// </summary>
    public class BlockAttachedConnectionResolver
    {
        private readonly ConnectionLineRegistry _registry;
        private readonly RailGraphClientCache _railCache;

        // ブロックごとに赤を求めた付随物を覚え、解除は同じ集合へ行う（その間に増減した線を取り違えない）
        // Remember what each block requested red on, and release exactly that set (lines changed meanwhile are not confused)
        private readonly Dictionary<BlockGameObject, List<IRemovePreviewable>> _requested = new();
        private readonly List<(int canonicalFrom, int canonicalTo)> _edgeBuffer = new();

        public BlockAttachedConnectionResolver(ConnectionLineRegistry registry, RailGraphClientCache railCache)
        {
            _registry = registry;
            _railCache = railCache;
        }

        // 要求者はブロック自身。線・レールが自分でホバー・選択されていても、その赤は相手側の要求として残る
        // The requester is the block itself; a line/rail hovered or selected on its own keeps its red as that other request
        public void RequestCascadePreview(BlockGameObject block)
        {
            if (_requested.ContainsKey(block)) return;

            var targets = new List<IRemovePreviewable>();
            foreach (var line in _registry.GetLinesAttachedTo(block.BlockInstanceId)) targets.Add(line);
            foreach (var edge in CollectRailEdges(block))
            {
                if (TrainRailObjectManager.Instance.TryGetRailChain(RailObjectIdCodec.ComputeRailObjectId(edge.canonicalFrom, edge.canonicalTo), out var chain)) targets.Add(RailChainRemovePreview.Of(chain));
            }

            foreach (var target in targets) target.RequestRemovePreview(block);
            _requested[block] = targets;
        }

        public void ReleaseCascadePreview(BlockGameObject block)
        {
            if (!_requested.Remove(block, out var targets)) return;
            foreach (var target in targets)
            {
                // 赤表示中に線が切れて破棄済みのことがある
                // A line may have been destroyed while red
                if (target is UnityEngine.Object unityObject && unityObject == null) continue;
                target.ReleaseRemovePreview(block);
            }
        }

        public void CollectRemovedConnections(BlockGameObject block, RemovedObjectCollector collector)
        {
            foreach (var line in _registry.GetLinesAttachedTo(block.BlockInstanceId)) line.CollectRemovedObjects(collector);
            foreach (var edge in CollectRailEdges(block))
            {
                // 無償区間（駅隣接の自動レール等）は駅の再設置で戻るので記録しない。未同期・駅内部は記録不能として数える
                // Free segments (e.g. station-adjacent auto rails) return with station re-placement, so skip; unsynced/station-internal count as unrecordable
                var result = RemovedRail.Create(_railCache, edge.canonicalFrom, edge.canonicalTo);
                if (result.Outcome == RemovedRailCreateOutcome.Created) collector.Add(result.Rail);
                else if (result.Outcome != RemovedRailCreateOutcome.FreeSegment && result.Outcome != RemovedRailCreateOutcome.StationInternal) collector.AddUnrecordable($"cascaded rail {edge.canonicalFrom}->{edge.canonicalTo}: {result.Outcome}");
            }
        }

        private List<(int canonicalFrom, int canonicalTo)> CollectRailEdges(BlockGameObject block)
        {
            _edgeBuffer.Clear();
            AttachedRailEdgeEnumerator.Collect(_railCache, block.BlockPosInfo.OriginalPos, _edgeBuffer);
            return _edgeBuffer;
        }
    }
}
```

（駅撤去の巻き込みで駅内部の区間は駅ブロックそのものの一部なので、駅の再設置で戻る。よって巻き込み収集では `StationInternal` も記録不能に数えない。直接選択した駅内部レールは `DeleteTargetRail.IsRemovable` が拒否するため記録経路に来ない）

`TrainRailObjectManager.cs` に追加（canonical 選択は Task 5 の `RailSegmentPairing`、ID符号化は Task C4 の `RailObjectIdCodec` を使う）:

```csharp
        // 描画中のレール1本をIDから引く（巻き込み赤表示用）
        // Look up a drawn rail by id (for cascade red preview)
        public bool TryGetRailChain(ulong railObjectId, out BezierRailChain chain)
        {
            chain = null;
            if (!_railObjs.TryGetValue(railObjectId, out var gobj) || gobj == null) return false;
            chain = gobj.GetComponent<BezierRailChain>();
            return chain != null;
        }
```

`BezierRailChain.cs` は変更しない（赤プレビューの数え上げは `RailChainRemovePreview` が持ち、既存 public `SetRemovePreviewing()`/`ResetMaterial()` を呼ぶだけ。319行の既存ファイルに触れないので分割も要らない）。

`BlockGameObjectChild.cs` の3メソッドを置換:

```csharp
        public void SetRemovePreviewing()
        {
            // ブロック本体と、一緒に消える接続線・レールを赤くする（線・レールにはブロックを要求者として求める）
            // Redden the block and the lines/rails that vanish with it (requesting red on them with the block as requester)
            BlockGameObject.SetRemovePreviewing();
            ClientDIContext.BlockAttachedConnectionResolver.RequestCascadePreview(BlockGameObject);
        }

        public void ResetMaterial()
        {
            BlockGameObject.ResetMaterial();
            ClientDIContext.BlockAttachedConnectionResolver.ReleaseCascadePreview(BlockGameObject);
        }

        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            // ブロック本体と、巻き込みで消える接続線・レールを同じ撤去物として積む
            // Append the block and the cascaded lines/rails as removed objects of the same kind
            collector.Add(RemovedBlock.From(BlockGameObject));
            ClientDIContext.BlockAttachedConnectionResolver.CollectRemovedConnections(BlockGameObject, collector);
        }
```

`ClientDIContext.cs` に `public static BlockAttachedConnectionResolver BlockAttachedConnectionResolver { get; private set; }` を追加しコンストラクタで Resolve。`MainGameInteractionRegistration.cs` に `builder.Register<BlockAttachedConnectionResolver>(Lifetime.Singleton);`（`ConnectionLineRegistry` と `RailGraphClientCache` は登録済み）。

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(UIState\.AttachedRailEdgeEnumeratorTest|ConnectionLine\.RemovePreviewRequestsTest|BuildUndo\.)"`（新規 `.cs` を足したので先に `uloop launch ./moorestech_client --restart`）
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/AttachedRailEdgeEnumeratorTest.cs
git commit -m "feat(client): ブロック撤去で巻き込まれる接続線・レールを赤表示しUndoへ記録する"
```

---

### Task 13 (C6): 電線ツールのクリック切断を廃止し、スポイトを接続線全般の種類へ

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Modes/ElectricWireEditMode.cs:10-13,33-40,79-91`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWireExtendRequestSender.cs:78-81`（`Disconnect` 削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:36-51`（`TryGetCursorOnElectricWire` → `GetCursorOnConnectionLine`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/PlacementTargetPickService.cs:36-53`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConnectTool/ConnectionLinePickResolverTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/ConnectionLinePickResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/ConnectionLinePickResult.cs`（スポイトの結果と不成立の理由）

**Interfaces:**
- Consumes: Task C2 `ConnectionLineDeleteTarget.ConnectToolGuid`、Task C1 `LayerConst.ConnectionLineOnlyLayerMask`、Task C3 `BlockClickDetectUtil.TryCreateAimRay` と `ConnectionLineDeleteTarget.FromCollider`
- Produces:
  - `BlockClickDetectUtil.GetCursorOnConnectionLine() : ConnectionLineAimResult`（`Found`/`NoCamera`/`NothingHit`/`NotALine` と線。旧 `TryGetCursorOnElectricWire` は削除。照準レイは `TryCreateAimRay`、コライダー解決は `ConnectionLineDeleteTarget.FromCollider` を呼ぶ）
  - `public static class ConnectionLinePickResolver { static ConnectionLinePickResult Resolve(Guid lineConnectToolGuid, IGameUnlockStateData unlockState) }`（`Picked`/`UnknownTool`/`Locked` と対象）

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.ConnectTool
{
    /// <summary>
    ///     接続線のスポイトが線を引いた種類そのものを選び、未解放なら不成立になることを検証する
    ///     Verifies the line eyedropper picks the exact tool the line was drawn with and fails when locked
    /// </summary>
    public class ConnectionLinePickResolverTest
    {
        [Test]
        public void UnlockedLineToolIsPicked()
        {
            // 線の種類が解放済みならその種類の接続ツールを選ぶ
            // An unlocked line tool is picked as-is
            var guid = Guid.NewGuid();
            var result = ConnectionLinePickResolver.Resolve(guid, new FakeUnlockState(guid, true));

            Assert.AreEqual(ConnectionLinePickOutcome.Picked, result.Outcome);
            Assert.AreEqual(guid, ((ConnectToolPlacementTarget)result.Target).ConnectToolGuid);
        }

        [Test]
        public void LockedLineToolFailsPick()
        {
            // 未解放の種類はスポイト自体を不成立にする
            // A locked tool makes the eyedropper fail
            var guid = Guid.NewGuid();
            Assert.AreEqual(ConnectionLinePickOutcome.Locked, ConnectionLinePickResolver.Resolve(guid, new FakeUnlockState(guid, false)).Outcome);
        }

        [Test]
        public void ToolAbsentFromUnlockStateIsUnknown()
        {
            // 解放状態に無い種類（マスタから消えた等）は理由を分けて不成立にする
            // A tool absent from the unlock state (e.g. removed from the master) fails with its own reason
            Assert.AreEqual(ConnectionLinePickOutcome.UnknownTool, ConnectionLinePickResolver.Resolve(Guid.NewGuid(), new FakeUnlockState(Guid.NewGuid(), true)).Outcome);
        }

        /// <summary>
        ///     接続ツールの解放状態だけを差し込むテスト用スタブ（前例: CraftActionTest.StubUnlockStateData）
        ///     Test stub injecting only connect-tool unlock state (precedent: CraftActionTest.StubUnlockStateData)
        /// </summary>
        private class FakeUnlockState : IGameUnlockStateData
        {
            public FakeUnlockState(Guid connectToolGuid, bool isUnlocked)
            {
                ConnectToolUnlockStateInfos = new Dictionary<Guid, ConnectToolUnlockStateInfo> { { connectToolGuid, new ConnectToolUnlockStateInfo(connectToolGuid, isUnlocked) } };
            }

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } = new Dictionary<Guid, BlockUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; }
            public bool IsBlueprintUnlocked { get; } = false;
        }
    }
}
```

（既存のスタブは各テストクラスの private class なので共有せず、同じ形でこのテスト内に持つ）

- [ ] **Step 2: 実装を書く**

`ConnectionLinePickResult.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    ///     接続線スポイトの結果。不成立の理由（種類が解放状態に無い／未解放）を区別する
    ///     Result of the connection-line eyedropper; distinguishes why it failed (tool unknown to the unlock state / locked)
    /// </summary>
    public enum ConnectionLinePickOutcome
    {
        Picked,
        UnknownTool,
        Locked,
    }

    public readonly struct ConnectionLinePickResult
    {
        public ConnectionLinePickOutcome Outcome { get; }
        public IPlacementTarget Target { get; }

        private ConnectionLinePickResult(ConnectionLinePickOutcome outcome, IPlacementTarget target)
        {
            Outcome = outcome;
            Target = target;
        }

        public static ConnectionLinePickResult Picked(IPlacementTarget target)
        {
            return new ConnectionLinePickResult(ConnectionLinePickOutcome.Picked, target);
        }

        public static ConnectionLinePickResult Failed(ConnectionLinePickOutcome outcome)
        {
            return new ConnectionLinePickResult(outcome, null);
        }
    }
}
```

`ConnectionLinePickResolver.cs`:

```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    ///     カーソル下の接続線から、その線を引いた接続ツールをスポイト対象に解決する
    ///     Resolves the connect tool that drew the hovered line as the eyedropper target
    /// </summary>
    public static class ConnectionLinePickResolver
    {
        public static ConnectionLinePickResult Resolve(Guid lineConnectToolGuid, IGameUnlockStateData unlockState)
        {
            // 解放状態に無い種類・未解放の種類はスポイト自体を不成立にし、理由を分ける（Guid.Emptyを下流へ流さない）
            // An unknown or locked tool fails the eyedropper with distinct reasons (never pass Guid.Empty downstream)
            if (!unlockState.ConnectToolUnlockStateInfos.TryGetValue(lineConnectToolGuid, out var info)) return ConnectionLinePickResult.Failed(ConnectionLinePickOutcome.UnknownTool);
            if (!info.IsUnlocked) return ConnectionLinePickResult.Failed(ConnectionLinePickOutcome.Locked);

            return ConnectionLinePickResult.Picked(new ConnectToolPlacementTarget(lineConnectToolGuid));
        }
    }
}
```

`BlockClickDetectUtil.cs` の `TryGetCursorOnElectricWire` を置換し、結果型を同ファイル末尾（クラスの外）に置く:

```csharp
        public static ConnectionLineAimResult GetCursorOnConnectionLine()
        {
            // 照準レイの生成とコライダー→線本体の解決は正本を呼ぶ（Task 10）
            // Ray creation and collider-to-line resolution call their single definitions (Task 10)
            if (!TryCreateAimRay(out var ray)) return ConnectionLineAimResult.Missed(ConnectionLineAimOutcome.NoCamera);

            // 接続線は専用レイヤのため単独Raycastで判定する
            // Connection lines live on a dedicated layer, so probe them with their own raycast
            if (!Physics.Raycast(ray, out var hit, RayDistance, LayerConst.ConnectionLineOnlyLayerMask, QueryTriggerInteraction.Collide)) return ConnectionLineAimResult.Missed(ConnectionLineAimOutcome.NothingHit);

            var line = ConnectionLineDeleteTarget.FromCollider(hit.collider);
            return line == null ? ConnectionLineAimResult.Missed(ConnectionLineAimOutcome.NotALine) : ConnectionLineAimResult.Found(line);
        }
```
```csharp
    /// <summary>
    ///     接続線への照準結果。外れた理由を区別する（スポイトの外れは通常操作なのでログは出さない）
    ///     Aim result on a connection line; distinguishes why it missed (an eyedropper miss is normal, so it is not logged)
    /// </summary>
    public enum ConnectionLineAimOutcome
    {
        Found,
        NoCamera,
        NothingHit,
        NotALine,
    }

    public readonly struct ConnectionLineAimResult
    {
        public ConnectionLineAimOutcome Outcome { get; }
        public ConnectionLineDeleteTarget Line { get; }

        private ConnectionLineAimResult(ConnectionLineAimOutcome outcome, ConnectionLineDeleteTarget line)
        {
            Outcome = outcome;
            Line = line;
        }

        public static ConnectionLineAimResult Found(ConnectionLineDeleteTarget line)
        {
            return new ConnectionLineAimResult(ConnectionLineAimOutcome.Found, line);
        }

        public static ConnectionLineAimResult Missed(ConnectionLineAimOutcome outcome)
        {
            return new ConnectionLineAimResult(outcome, null);
        }
    }
```

（`using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;` を `...StateProcessor.ConnectionLine;` へ。`RayDistance` は同クラスの既存 `private const float RayDistance = 100f;`）

`PlacementTargetPickService.cs`（`using UnityEngine;` を追加。`ConnectionLineAimOutcome` は `Client.Game.InGame.Control` 名前空間で既存 using が通る）: コメント「電線→列車→ブロックの順」を「接続線→列車→ブロックの順（線は細いため最優先で拾う）」へ、`TryPickElectricWire` を次に置換し、呼び出し側も `TryPickConnectionLine` に改名:

```csharp
            bool TryPickConnectionLine(out IPlacementTarget target)
            {
                target = null;
                var aim = BlockClickDetectUtil.GetCursorOnConnectionLine();
                if (aim.Outcome != ConnectionLineAimOutcome.Found) return false;

                // 線を引いた種類そのものをスポイトする。解放状態に無い種類は異常なので理由をログへ出し、未解放は通常の不成立
                // Pick the exact tool the line was drawn with; an unknown tool is abnormal and logged, a locked tool is an ordinary miss
                var pick = ConnectionLinePickResolver.Resolve(aim.Line.ConnectToolGuid, _gameUnlockStateData);
                if (pick.Outcome == ConnectionLinePickOutcome.UnknownTool) Debug.LogWarning($"[PlacementPick] line tool not in unlock state: {aim.Line.ConnectToolGuid}");
                target = pick.Target;
                return pick.Outcome == ConnectionLinePickOutcome.Picked;
            }
```

`ElectricWireEditMode.cs`:
- クラスコメントを「起点未選択時の挙動。電気系ブロックの起点選択・電柱の孤立設置を処理する（切断は削除ツールが担う）」/「Behavior while no origin is selected: source selection and isolated pole placement (cutting belongs to the delete tool)」へ
- `Update` 内の「ワイヤーを優先判定し、クリックでヒットしたら切断する」ブロック（`if (isClicked && BlockClickDetectUtil.TryGetCursorOnElectricWire(out var wire)) { Disconnect(wire); return null; }`）を削除
- `#region Internal` の `Disconnect` ローカル関数を削除
- `Update` のメソッドコメント「切断・孤立設置・未選択時はnull」を「孤立設置・未選択時はnull」へ
- 不要になった `using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;` を外す

`ElectricWireExtendRequestSender.cs` の `Disconnect` メソッドを削除。

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.(ConnectTool|ElectricWireConnect)\."`
Expected: ErrorCount 0 / PASS。加えて `grep -rn "TryGetCursorOnElectricWire\|RequestSender.Disconnect\|TryResolvePickTarget" moorestech_client/Assets/Scripts` が0件。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConnectTool
git commit -m "feat(client): 電線ツールのクリック切断を廃止し接続線のスポイトを線の種類で解決する"
```

---

### Task 14 (C7): unityプレイ録画テストで削除ツール切断と撤去Undoを通しで検証する（電線・チェーン・レール）

判断: unity-playmode-recorded-playtest（プレイテストDSL）を使う。理由: 記録フックは `DragDeleteSelection.CommitDelete`（削除ツールUI経路）と `BuildUndoService.ManualUpdate`（Ctrl+Z）にあり、実入力・実レイキャスト・実コライダー（チェーンの実行時コライダー、ConnectionLineレイヤー）を通さないと偽陰性になる。同じ操作の前例 `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs`（G→ドラッグ撤去→Ctrl+Z注入）と、橋脚＋レールをホットバー経由で設置・結線する前例 `.../scenarios/train/train-rail-connect-via-ui.cs` があるため、レールの巻き込みUndoも同じシナリオで覆える。EditModeInPlayingTest はUI入力注入の前例が薄く採らない。

使う実APIの確認結果（推測なし）:
- クライアントの座標引き: `BlockGameObjectDataStore.TryGetBlockGameObject(Vector3Int, out BlockGameObject)`（`C/Client.Game/InGame/Block/BlockGameObjectDataStore.cs:47`）
- サーバーのブロック部品: `IBlock.TryGetComponent<T>(out T)` 拡張（`S/Game.Block.Interface/Extension/BlockExtension.cs:23`）、`p.GetBlock(pos)`（`PlaytestDriver.cs:90`）
- チェーン接続の有無と種類: `IGearChainPole.ContainsChainConnection(BlockInstanceId)`（既存）と Task A1 が新設する `IGearChainPole.TryGetChainConnectionRecord(BlockInstanceId, out ConnectionLineRecord)`（`.ConnectToolGuid`）。接続数メンバーは使わない
- 電線の種類: Task A1 の `IElectricWireConnector.WireConnections[partner].Record.ConnectToolGuid`
- レール接続と種類: `RailComponent.FrontNode/BackNode`（`train-rail-connect-via-ui.cs:65-71` と同じ）、`IRailNode.ConnectedNodes`、`RailNode.NodeId`、`p.ServerService<RailGraphDatastore>().TryGetRailSegmentType(int, int, out Guid)`（`S/Game.Train/RailGraph/RailGraphDatastore.cs:111`、DI 登録 `ServerContextRegistration.cs:100`）
- 接続ツールGuidの名前引き: `MasterHolder.ConnectToolMaster.All`（`PlaytestHotbarOps.cs:79-85` と同じ）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs`

**Interfaces:**
- Consumes: Task C1〜C6、Task A1（Record.ConnectToolGuid / TryGetChainConnectionRecord）、A4（チェーン切断）、B1（橋脚撤去のレール返却）、B2（NoAutoConnect）、B3（座標同定のレール接続）
- Produces: 録画・result.json・スクショ（run ディレクトリ）

- [ ] **Step 1: シナリオを書く**

```csharp
// 削除ツールで電線・歯車チェーンを切断し、ブロック撤去の巻き込み（電線・チェーン・レール）もCtrl+Zで同じ種類のまま復元されることの録画検証
// Recorded check: the delete tool cuts wires/chains, and Ctrl+Z restores cut and cascaded lines (wire, chain, rail) with the same tool kind
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.Train.RailGraph;
using Game.UnlockState;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("delete-tool-cut-and-undo-connection-lines", options, async p =>
{
    // 警告・エラーログを収集し、最後に拒否・復元失敗が無いことを確かめる
    // Collect warning/error logs to assert no refusal or restore failure at the end
    var badLogs = new List<string>();
    Application.logMessageReceived += (condition, _, type) =>
    {
        if (type == LogType.Log) return;
        if (type == LogType.Exception || condition.Contains("denied.") || condition.Contains("[RemovalRestore]") || condition.Contains("[ConnectionLineDelete]")) badLogs.Add($"{type}: {condition}");
    };

    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(8f, 34f, -6f));
    await p.SkipOpeningSkit();
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    // 電線・チェーン・レールを解放し素材を渡す
    // Unlock wire, chain and rail tools and hand out materials
    var wireToolGuid = ToolGuid("銅のワイヤー接続");
    var chainToolGuid = ToolGuid("歯車チェーン");
    var railToolGuid = ToolGuid("レール");
    var unlock = p.ServerService<IGameUnlockStateDataController>();
    unlock.UnlockConnectTool(wireToolGuid);
    unlock.UnlockConnectTool(chainToolGuid);
    unlock.UnlockConnectTool(railToolGuid);
    await p.PrepareBlockForUiPlacement("電柱", 5);
    await p.GiveConstructionCost("歯車チェーンポール", 5);
    p.UnlockBlock("レール橋脚");
    await p.GiveConstructionCost("レール橋脚", 5);
    await p.GiveItem("銅のワイヤー", 64);
    await p.GiveItem("鉄のワイヤー", 64);
    await p.GiveItem("補強棒材", 64);
    await p.GiveItem("鉄板", 32);

    // 自動接続の走らないサーバー直置きで配置する。poleNearはAの接続範囲内だが繋がない（自動接続抑止の検出用）
    // Place server-side directly so no auto-connect runs; poleNear is within A's range but left unwired (detects auto-connect suppression)
    var poleA = new Vector3Int(5, 32, 4);
    var poleB = new Vector3Int(11, 32, 4);
    var poleNear = new Vector3Int(5, 32, 0);
    var chainC = new Vector3Int(5, 32, 9);
    var chainD = new Vector3Int(9, 32, 9);
    var pierE = new Vector3Int(14, 32, 6);
    var pierF = new Vector3Int(14, 32, 14);
    foreach (var pos in new[] { poleA, poleB, poleNear }) p.PlaceBlockDirect("電柱", pos, BlockDirection.North);
    foreach (var pos in new[] { chainC, chainD }) p.PlaceBlockDirect("歯車チェーンポール", pos, BlockDirection.North);
    foreach (var pos in new[] { pierE, pierF }) p.PlaceBlockDirect("レール橋脚", pos, BlockDirection.North);
    foreach (var pos in new[] { poleA, poleB, poleNear, chainC, chainD, pierE, pierF }) await p.WaitBlockGameObject(pos);

    // 線は既存ユーティリティでサーバー直結する（UI結線は既存シナリオが別途検証済み）
    // Lines are connected server-side via the existing utils (UI wiring is covered by other scenarios)
    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
    p.Assert(ElectricWireSystemUtil.TryConnect(poleA, poleB, playerId, wireToolGuid, out _), "準備: A-B を銅線で接続");
    p.Assert(GearChainSystemUtil.TryConnect(chainC, chainD, playerId, chainToolGuid, out _), "準備: C-D をチェーンで接続");
    await ConnectRailViaUi();
    await p.Until(() => LinesOf(poleA).Count == 1 && LinesOf(chainC).Count == 1, 15f, "準備: クライアントに電線・チェーンが出る");
    await p.Screenshot("01-prepared");

    // Step 1: 削除ツールで電線だけを切る
    // Step 1: cut only the wire with the delete tool
    p.Note("Step 1: Gで削除ツール→電線の中点を照準してクリック");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    var copperBefore = p.CountItem("銅のワイヤー");
    await DragBetween(LineMidpoint(LinesOf(poleA)[0]), LineMidpoint(LinesOf(poleA)[0]));
    await p.Until(() => !WireConnected(poleA, poleB), 15f, "Step1: サーバー上でA-B電線が切れる");
    p.Assert(copperBefore < p.CountItem("銅のワイヤー"), "Step1: 切断で銅線が返却される");
    p.Assert(p.GetBlock(poleA) != null && p.GetBlock(poleB) != null, "Step1: 電柱は消えない（線だけ切れる）");
    await p.Screenshot("02-wire-cut");

    // Step 2: Ctrl+Zで同じ種類のまま復元
    // Step 2: Ctrl+Z restores it with the same kind
    p.Note("Step 2: Ctrl+Zで電線を引き直す");
    await PressCtrlZ();
    await p.Until(() => WireConnected(poleA, poleB), 15f, "Step2: A-B電線が復元される");
    p.Assert(WireToolOf(poleA, poleB) == wireToolGuid, "Step2: 復元した電線は元と同じ銅線");
    await p.Screenshot("03-wire-restored");

    // Step 3: 電柱Aとチェーンポールcを1ドラッグで撤去（巻き込みで線も消える）
    // Step 3: remove pole A and chain pole C in one drag (their lines vanish with them)
    p.Note("Step 3: 電柱A→チェーンポールCをドラッグして撤去");
    await DragBetween(BlockCenter(poleA), BlockCenter(chainC));
    await p.Until(() => p.GetBlock(poleA) == null && p.GetBlock(chainC) == null, 15f, "Step3: AとCが撤去される");
    p.Assert(Connector(poleB).WireConnections.Count == 0 && !ChainConnected(chainD, chainC), "Step3: 付いていた電線・チェーンも消える");
    await p.WaitSeconds(1f);
    await p.Screenshot("04-blocks-removed");

    // Step 4: Ctrl+Zでブロックと線を撤去前どおりに戻す（自動接続は走らない）
    // Step 4: Ctrl+Z restores blocks and lines exactly (no auto-connect)
    p.Note("Step 4: Ctrl+Zでブロックと線を戻す");
    await PressCtrlZ();
    await p.Until(() => WireConnected(poleA, poleB) && ChainConnected(chainC, chainD), 20f, "Step4: A-B電線とC-Dチェーンが戻る");
    p.Assert(WireToolOf(poleA, poleB) == wireToolGuid, "Step4: 電線の種類が保たれる");
    p.Assert(ChainToolOf(chainC, chainD) == chainToolGuid, "Step4: チェーンの種類が保たれる");
    p.Assert(!WireConnected(poleA, poleNear), "Step4: 範囲内の電柱へ自動接続されていない");
    p.Assert(Connector(poleA).WireConnections.Count == 1, "Step4: Aの接続は撤去前と同じ1本だけ");
    await p.Until(() => LinesOf(poleA).Count == 1 && LinesOf(chainC).Count == 1, 15f, "Step4: クライアント表示も戻る");
    await p.Screenshot("05-restored");

    // Step 5: 橋脚Eを撤去するとレールも消えて素材が返り、Ctrl+Zで同じ種類のレールが戻る
    // Step 5: removing pier E drops its rail with a refund, and Ctrl+Z restores the same rail type
    p.Note("Step 5: 橋脚Eを撤去→Ctrl+Zで橋脚とレールを戻す");
    var plateBefore = p.CountItem("鉄板");
    var rodBefore = p.CountItem("補強棒材");
    await DragBetween(BlockCenter(pierE), BlockCenter(pierE));
    await p.Until(() => p.GetBlock(pierE) == null, 15f, "Step5: 橋脚Eが撤去される");
    p.Assert(plateBefore < p.CountItem("鉄板") || rodBefore < p.CountItem("補強棒材"), "Step5: 撤去でレール素材が返却される");
    await p.WaitSeconds(1f);
    await p.Screenshot("06-pier-removed");
    await PressCtrlZ();
    await p.Until(() => p.GetBlock(pierE) != null && RailConnected(pierE, pierF), 20f, "Step5: 橋脚Eとレールが戻る");
    p.Assert(RailToolOf(pierE, pierF) == railToolGuid, "Step5: レールの種類が保たれる");
    await p.Screenshot("07-rail-restored");

    p.Assert(badLogs.Count == 0, $"区間中に拒否・復元失敗ログが無い: {string.Join(" | ", badLogs)}");

    #region Internal

    Guid ToolGuid(string toolName) => MasterHolder.ConnectToolMaster.All.First(t => t.Name == toolName).ConnectToolGuid;

    IReadOnlyList<ConnectionLineDeleteTarget> LinesOf(Vector3Int pos)
    {
        if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out var go)) return Array.Empty<ConnectionLineDeleteTarget>();
        return ClientDIContext.ConnectionLineRegistry.GetLinesAttachedTo(go.BlockInstanceId);
    }

    IElectricWireConnector Connector(Vector3Int pos)
    {
        var block = p.GetBlock(pos);
        return block != null && block.TryGetComponent<IElectricWireConnector>(out var connector) ? connector : null;
    }

    bool WireConnected(Vector3Int a, Vector3Int b)
    {
        var ca = Connector(a);
        var cb = Connector(b);
        return ca != null && cb != null && ca.ContainsWireConnection(cb.BlockInstanceId);
    }

    Guid WireToolOf(Vector3Int a, Vector3Int b) => Connector(a).WireConnections[Connector(b).BlockInstanceId].Record.ConnectToolGuid;

    IGearChainPole ChainPole(Vector3Int pos)
    {
        var block = p.GetBlock(pos);
        return block != null && block.TryGetComponent<IGearChainPole>(out var pole) ? pole : null;
    }

    bool ChainConnected(Vector3Int a, Vector3Int b)
    {
        var pa = ChainPole(a);
        var pb = ChainPole(b);
        return pa != null && pb != null && pa.ContainsChainConnection(pb.BlockInstanceId);
    }

    Guid ChainToolOf(Vector3Int a, Vector3Int b)
    {
        ChainPole(a).TryGetChainConnectionRecord(ChainPole(b).BlockInstanceId, out var record);
        return record.ConnectToolGuid;
    }

    RailComponent RailOf(Vector3Int pos) => p.GetBlock(pos)?.GetComponent<RailComponent>();

    // 橋脚Eのいずれかのノードから橋脚Fのいずれかのノードへ辺があるか（train-rail-connect-via-ui.cs と同じ判定）
    // Whether any node of pier E links to any node of pier F (same check as train-rail-connect-via-ui.cs)
    bool RailConnected(Vector3Int e, Vector3Int f)
    {
        var railE = RailOf(e);
        var railF = RailOf(f);
        if (railE == null || railF == null) return false;
        return new[] { railE.FrontNode, railE.BackNode }.Any(n => n.ConnectedNodes.Any(t => t.NodeGuid == railF.FrontNode.Guid || t.NodeGuid == railF.BackNode.Guid));
    }

    Guid RailToolOf(Vector3Int e, Vector3Int f)
    {
        var railE = RailOf(e);
        var railF = RailOf(f);
        var datastore = p.ServerService<RailGraphDatastore>();
        foreach (var from in new[] { railE.FrontNode, railE.BackNode })
        foreach (var to in new[] { railF.FrontNode, railF.BackNode })
        {
            if (datastore.TryGetRailSegmentType(from.NodeId, to.NodeId, out var guid)) return guid;
        }
        return Guid.Empty;
    }

    // 橋脚E→Fをレール接続ツールのクリック結線で繋ぐ（train-rail-connect-via-ui.cs:75-100 と同じ手順）
    // Connect pier E to F with the rail tool's click-connect (same steps as train-rail-connect-via-ui.cs:75-100)
    async UniTask ConnectRailViaUi()
    {
        await p.Hotbar.AssignHotbar(1, "レール");
        await p.Hotbar.EnterBuildMode(1);
        await p.AimAt(ClosestAreaCenter(pierE, pierF));
        await p.ClickPlace();
        await p.WaitSeconds(0.3f);
        await p.AimAt(ClosestAreaCenter(pierF, pierE));
        await p.ClickPlace();
        await p.Until(() => RailConnected(pierE, pierF), 15f, "準備: 橋脚E-Fがレールで繋がる");
        await p.Hotbar.ExitBuildMode(1);
    }

    Vector3 ClosestAreaCenter(Vector3Int selfPos, Vector3Int otherPos)
    {
        ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(selfPos, out var self);
        ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(otherPos, out var other);
        var areas = self.GetComponentsInChildren<Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.TrainRailConnectAreaCollider>(true);
        var best = areas.OrderBy(a => Vector3.Distance(a.GetComponent<Collider>().bounds.center, other.transform.position)).First();
        return best.GetComponent<Collider>().bounds.center;
    }

    Vector3 BlockCenter(Vector3Int pos) => new(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);

    // カテナリーのコライダー列の中央を狙う（垂れ下がり分を含めて当たる位置）
    // Aim at the middle catenary collider (a point that hits even with sag)
    Vector3 LineMidpoint(ConnectionLineDeleteTarget line)
    {
        var colliders = line.GetComponentsInChildren<CapsuleCollider>();
        return colliders[colliders.Length / 2].transform.position;
    }

    // 照準→押下→（必要なら）照準移動→解放。単体撤去は同じ点を2回渡す
    // Aim, press, optionally re-aim, release; a single delete passes the same point twice
    async UniTask DragBetween(Vector3 from, Vector3 to)
    {
        await p.AimAt(from);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(10);
        await p.AimAt(to);
        await UniTask.DelayFrame(10);
        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(3);
        await p.WaitSeconds(0.5f);
    }

    // build-undo-ctrl-z.cs と同じ注入順（LeftCtrl保持中にZ）
    // Same injection order as build-undo-ctrl-z.cs (Z while LeftCtrl is held)
    async UniTask PressCtrlZ()
    {
        SemanticInput.KeyDown(Key.LeftCtrl);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyDown(Key.Z);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.Z);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.LeftCtrl);
        await UniTask.DelayFrame(3);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
```

実行前に実装者が確かめる点（いずれも実在は確認済みで、値だけが環境依存）:
- 接続ツール名 `銅のワイヤー接続` / `歯車チェーン` / `レール` は `MasterHolder.ConnectToolMaster.All` の `Name` と一致させる。電線は既存シナリオが Guid `872372d5-2998-4fb7-826c-593ceeafcfb2` を直書きしているので、名前が一致しなければその Guid を使う（`electric-wire-tool-extend-chain-via-ui.cs:50`）
- `RailComponent` の名前空間（`train-rail-connect-via-ui.cs:16-18` の using に合わせる）と `TrainRailConnectAreaCollider` の名前空間（同 `:11`）
- レール素材名 `補強棒材` / `鉄板` は `train-rail-connect-via-ui.cs:38-39` と同じ
- 照準が外れる場合は `WarpPlayer` の位置を調整する（電柱座標は `electric-wire-tool-extend-chain-via-ui.cs:57-59`、橋脚の並びは `train-rail-connect-via-ui.cs:44-45` を基準にした）
- 駅（station）の巻き込みは本シナリオに含めない。駅内部・駅隣接の自動レールは `RailTypeGuid == Guid.Empty` で記録対象外であり（Task C4 `RemovedRailTest.EdgeWithEmptyRailTypeIsNotRecorded`）、駅の再設置が自動で張り直す。録画での駅の通し確認は閉じタスクで残課題として起票する

- [ ] **Step 2: 実行する**

```bash
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech/delete-tool-cuts-connection-lines && pwd
SKILL=.claude/skills/unity-playmode-recorded-playtest
uloop control-play-mode --project-path ./moorestech_client --action stop
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs"
```

Expected: result.json の Asserts 全 PASS、動画 0 byte でない、スクショ 01〜07 に電柱・線・レール・HUD が実プレイ視点で映る。

- [ ] **Step 3: 拒否・喪失ログが区間でゼロであることを確認する**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Warning` と `--log-type Error`
Expected: シナリオ実行区間に `denied.` / `[RemovalRestore]` / `[ConnectionLineDelete]` / `refus` / `mismatch` / `Exception` を含む行が0件（result.json 内の「区間中に拒否・復元失敗ログが無い」アサートと二重確認）。

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs
git commit -m "test(playtest): 削除ツールの接続線切断と撤去Undo（電線・チェーン・レール）の録画シナリオを追加"
```

---


### Task 15: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1:** 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。
- [ ] **Step 2:** レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 14 のunityプレイ録画テストを反映後のバイナリで再実施してから完了とする。合否は期待行の存在ではなく、録画区間で `denied.` 通知・`[RemovalRestore]`・`[ConnectionLineDelete]` 警告・`Error` ログがゼロであること（`uloop get-logs --project-path ./moorestech_client --log-type Error` と Warning で確認）。
- [ ] **Step 3:** plan・進捗台帳・録画記録に書いた「未検証」「未確認」「残差」を1件ずつ `bd create --parent moorestech-xsd18` で起票し、記録の結論に issue 番号を列挙する（少なくとも Task 14 で録画対象外とした「駅撤去のUndo」）。

### Task 16: セッション終了可能化（PR作成・必須）

- [ ] **Step 1:** pr-create スキルでPRを作成する。masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。
- [ ] **Step 2:** 全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認する。PR本文に ADR 0076・セーブ版4への移行・`moorestech-xsd18` を記す。全実装タスク完了＝完了ではない。PR未作成のまま終わるのはplan未完了である。
- [ ] **Step 3:** `bd close moorestech-xsd18 --reason "<PR URL>"` の後、`moores-wt rm delete-tool-cuts-connection-lines` でworktreeとEditorを畳む。

---

## 本PR外のリファクタ提案（第3バケツ）

Phase 2.6 検査6の最終行（同役割の既存実装が plan 外にある）。plan のタスクには足さない。着手可否は人間が決める。

- 既存 `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/RailEdit/RailConnectionEditService.cs`（L139-146 のローカル関数 `IsStationInternalEdge`）／新設 `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailEdgeClassifier.cs`／同じ述語 `HasStation && StationBlockInstanceId.Equals` をサーバーの切断判定とクライアントの削除可否の両方に持つ
- 既存 `RailConnectionEditService.cs`（L104-118 の切断時返却算出）／新設 `RailRemovalRefundCalculator`（区間返却）／「種類Guid → `GetRailLength` → Empty か算出不能なら返却なし → `CreateRefundItems`」という同じ区間返却の導出
- 既存 `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetCatalog.cs`（L129 `IsAssignable`、L151-164）と `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ConnectTool/ConnectToolCatalog.cs`（L51）／新設 `ConnectionLinePickResolver.Resolve`／接続ツールGuidの解放判定 `ConnectToolUnlockStateInfos.TryGetValue && IsUnlocked`
- （本PR内で解消済み）既存 `ConnectToolMaterialConsumer.CreateRefundItems` と新設チェーン台帳の返却展開の重複は、弱発火「重複した導出を1本化」の反映で正本 `ConnectionLineRefundItems.Create` へ寄せ、`ConnectToolMaterialConsumer.CreateRefundItems` は委譲だけにした（Task 1）

## 判断記録（ADR）

設計ADR: `docs/adr/0076-delete-tool-cuts-connection-lines-and-undo-restores-removed-objects.md`（設計セッションの裁定と出所はADR側が正。ここでは書き換えない）。裁定記録: `.decisions/2026-10-04-*.md`・`.decisions/2026-10-05-*.md`。

planning 中に新たに生じた判断:

| # | 判断 | 出所 |
|---|---|---|
| P1 | 電線・チェーンは接続ごとに ConnectToolGuid を保存・同期する（セーブv4） | ユーザー裁定 2026-10-04 選択「接続ごとに種類を保存」 |
| P2 | 旧セーブの線は線種別ごとの唯一の種類を固定定数で割り当てる（素材を見ない・失敗しない） | ユーザー裁定 2026-10-04 選択「線種別ごとの唯一の種類を割り当て」 |
| P3 | 橋脚・駅撤去で付いていたレールも返却する | ユーザー裁定 2026-10-04 選択「撤去でレールも返却する」 |
| P4 | 接続1本の型を `ElectricWireConnectionCost`/`GearChainConnectionCost` から電線・チェーン共通の `ConnectionLineRecord` 1型へ統合し種類を持たせる（配置は `Game.Block.Interface.Component`。`Game.EnergySystem.asmdef` は `Game.Block.Interface` を参照済み）。`Empty` は廃止。セーブ接続要素も `ConnectionLineConnectionJsonObject` 1型に畳む | agent判断（user-simulator review 指摘・名前を実処理と一致させる規約・ユーザー裁定 2026-10-04「歯車チェーンも、適切に共通化する」） |
| P5 | 移行ステップは構造の壊れたJSON（配列・オブジェクトでない）だけ `Failed`。素通しすると未変換のまま版4が刻まれ `Required.Always` で落ちるため | agent判断（save-migration スキル「ステップは冪等・JObjectだけで書く」） |
| P6 | チェーン切断の理由は `out string` でなく `GearChainDisconnectFailureReason` enum（webui の通知id網羅テストが補間idを enum 展開でしか分類できないため）。接続・延長の理由 `GearChainPlacementFailureReason` とは別 enum に保つ（理由集合が InvalidTarget 以外重ならず、同じ理由の二重表現ではない） | agent判断（`notificationServerIdCoverage.test.ts:23-33`） |
| P7 | `IGearChainPole.TryGetChainConnectionRecord` を追加（返却の InsertionCheck を除去前に行うため） | agent判断（電線 TryDisconnect の順序の前例） |
| P8 | 200行超の既存ファイルを分割: `GearChainPoleComponent.cs`→`GearChainConnectionSet.cs`、`ElectricWireSystemUtil.cs`→`ElectricWireDisconnectUtil.cs`、`TrainRailObjectManager.cs`→`RailObjectIdCodec.cs`＋`RailSegmentPairing`、`BlockMasterUtil.cs`（403行）は本PRの関心（破壊カテゴリ検証）だけを `BlockDestructionCategoryValidator.cs` へ移して縮める（全体分割は本PR外）。`BezierRailChain.cs`（319行）は赤プレビューの数え上げを別部品 `RailChainRemovePreview` に持たせて触らないため分割不要 | agent判断（AGENTS.md 200行規約、user-simulator review 指摘） |
| P9 | 撤去の返却が入らないときの拒否理由を `RemoveBlockFailureReason.InventoryFull`／`ui.delete.inventoryFull` として新設（従来は Unknown で理由が出ない） | agent判断（無音縮退禁止・レール返却で満杯拒否が増えるため） |
| P10 | 設置要求に `BlockPlacementWiring { AutoConnect, NoAutoConnect }` を持たせ、Undoだけ NoAutoConnect。駅隣接のレール自動接続は抑止しない | ユーザー裁定 2026-10-05 選択「止めない（電線だけ止める）」（値名 `NoAutoConnect` はサーバーの実処理名に合わせた agent判断・user-simulator review 指摘） |
| P11 | 座標同定のレール接続 `va:railConnectByDestination` を新設し応答なし（null）。接続処理は `RailConnectionEditService.ExecuteEdit` を呼ぶだけで複製しない。既接続は無消費で何もしない | agent判断（再設置でNodeGuidが振り直される事実 `RailComponent.cs:44-45`、Id再利用 `RailNodeIdAllocator`） |
| P12 | `Guid.Empty` のレール区間（駅内部・駅隣接自動）は返却もUndo記録もしない | agent判断（`RailNode.ConnectNode` が Empty で張る事実 `RailNode.cs:121-124`） |
| P13 | 線の引き直しは電線も SendOnly（`SendOnly.ConnectElectricWire` 新設）。Response経路は電線ツールの世代管理と競合するため | agent判断（前例 `VanillaApiSendOnly.ConnectGearChain`） |
| P14 | Undo記録は `IRemovedObject` の2相メソッド（`AppendBlockRestore`→`BlockRestoreOutcome`／`SendConnectionRestore`）。電線とチェーンは `RemovedConnectionLine(ConnectionLineKind, …)` 1クラス。収集は `RemovedObjectCollector`（記録不能を理由付きで数える） | agent判断（ブロックは1回の PlaceBlock にまとめる既存形 `RemoveOperationRecord.cs:72`、削除対象と対称） |
| P15 | 線の削除対象キーはコンポーネント自身（線は小さいId側に1本だけ生成される `ConnectionLineViewBase.cs:81-84`）。Undoの重複排除は kind＋正規化座標 | agent判断 |
| P16 | Unityレイヤー "ElectricWire" を "ConnectionLine" へ `uloop execute-dynamic-code` で改名（チェーンも同じレイヤーへ） | agent判断（名前を実処理と一致させる・テキスト編集禁止規約） |
| P17 | 照準の解決は純関数 `DeleteTargetHitSelector` と薄い `DeleteTargetRaycaster` に分け、`BlockClickDetectUtil` には照準レイ生成の正本 `TryCreateAimRay` と共有レイキャストだけ足す。照準・スポイトの外れは毎フレームの通常状態なので理由は結果型で返すがログは出さない（スポイトで種類が解放状態に無い異常だけログ） | agent判断（テスト可能性） |
| P18 | `BlockAttachedConnectionResolver` は `ClientDIContext` の static 経由で `BlockGameObjectChild` から使う | agent判断（前例 `ClientDIContext.BlockGameObjectDataStore` を `ElectricWireLineViewElement` が static 参照） |
| P19 | `IBuildOperationRecord.UndoAsync` の引数を `IBlockOccupancyQuery` へ、`DeleteObjectState` の未使用 `RailGraphClientCache` 引数を `IRemovalRestoreSender` へ置換（テスト3か所更新） | agent判断（テスト可能性・未使用引数の削除） |
| P21 | Undoの部分失敗はできた分だけ戻し、失敗分は通知して履歴は消費する | ユーザー裁定 2026-10-05 選択「できた分だけ戻し残りは通知」 |
| P22 | 削除ツールの照準条件は string＋null でなく判別union `DeleteAimFilter`（Frontmost / Category）で表し、`DragDeleteSelection.AimFilter` で公開する | ユーザー裁定 2026-10-05 選択「判別union 1本に畳む」 |
| P23 | `GearChainConnectionSet` を `IGearChainConnectionLookup`／`IGearChainConnectionMutation` に分け、コンポーネントは読み取りと変更を別フィールドで持つ | ユーザー裁定 2026-10-05 選択「分ける」 |
| P24 | 歯車チェーンの接続・延長の失敗理由を `GearChainPlacementFailureReason` 1本で評価器・延長/接続プロトコル・クライアントのプレビュー／ツールチップまで通す（評価器の文字列定数と `FailureReason(string)` を廃止。応答 `Error` は `ToString()`） | ユーザー裁定 2026-10-05 選択「enum 1本に畳む」 |
| P25 | 弱発火4件を反映: 赤プレビューを要求者集合 `RemovePreviewRequests` で数え巻き込み解除が他者の赤を消さない／Undoのクライアント側スキップ（占有・記録不能）を `ClientLocalNotificationSource` 経由でプレイヤー通知 `denied.undoRestoreSkipped`／重複導出を正本へ（`TryCreateFittingRefund`・`ConnectionLineRefundItems`・`TryResolveEndpointPositions`・`RailSegmentPairing`・`TryCreateAimRay`・`ConnectionLineDeleteTarget.FromCollider`・`ConnectionLinePartnerMessagePack.CreateArray/ToPartnerIds`・`ConnectionLineConnectionJsonObject.ToConnectionRecord`）／複数の失敗理由を持つ Try を結果型へ（`ConnectionToolGuidFillResult`・`DeleteAimResult`・`RemovedRailCreateResult`・`ConnectionLinePickResult`・`ConnectionLineAimResult`） | ユーザー裁定 2026-10-05 選択「赤プレビューの書き手を1本化, 占有スキップもプレイヤーへ通知, 重複した導出を1本化, Tryの失敗理由を型で区別」 |
| P26 | 弱発火のうち「空文字で成功を表す形」（P24 で解消）・「契約型を Interface 層へ移す」（既存 StateDetail/PlacePacketDto の前例どおり）・「Try の戻り値を捨てる既存箇所」は据え置く | ユーザー裁定 2026-10-05「このままでよい」（弱発火の一括質問で選ばなかった項目） |
| P27 | 電線・チェーンの接続記録は種類と払った素材の両方を保存し、返却は払った素材（レールは種類のみ保存・長さ×現単価で返却する非対称を残す） | ユーザー裁定 2026-10-05 選択「種類＋素材を保存（現状維持）」 |
| P28 | 破壊カテゴリーキー `connectionLine` は `BlockMaster.ConnectionLineDestructionCategory`（既定カテゴリーの隣）に置き、マスタの `categoryKey` との衝突を BlockMaster 検証で拒否する | agent判断（user-simulator review 指摘。既存定数の置き場の前例 `BlockMaster.DefaultDestructionCategory`） |
| P29 | 撤去返却のレールだけ `Server.Protocol` の `RailRemovalRefundCalculator` に置く（`Game.Train` は `ConnectToolCostCalculator`/`GetRailLength` のある `Server.Protocol` を参照できない） | agent判断（user-simulator review 指摘への説明。B群前提事実に明記） |
| P30 | クライアント発の通知の前例が無いため、`ClientLocalNotificationSource`（UniRx `Subject`）を新設し `NotificationTopic` が購読してサーバー通知と同じ表で表示する | agent判断（新規パターン。レビュー注目点） |
| P31 | 録画シナリオ `free-placement-locked-block.cs:71` の1引数 `PlaceBlock` を `AutoConnect` 指定へ更新（実行時コンパイルのため Unity コンパイルでは検出されない） | agent判断（user-simulator review 指摘） |
| P20 | 通し検証は unityプレイ録画テスト（EditModeInPlayingTest でなく。既存 `build-undo-ctrl-z.cs` と `train-rail-connect-via-ui.cs` の操作を再利用）。駅撤去のUndoは録画対象外とし Task 15 で起票 | agent判断（入力・カメラ・Undoキーを含むランタイム挙動のため） |
