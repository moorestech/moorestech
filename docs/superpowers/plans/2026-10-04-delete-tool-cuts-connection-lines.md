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
- R12 Undoの線の引き直しは通常の接続経路で素材を再消費する。失敗（素材不足・端点不在・未解放・上限）はサーバー通知とクライアント `Debug.LogWarning` に出して残りを続ける。既に接続済みの線は何もしない。`Guid.Empty`のレール区間は記録しない。
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
| Undoの線・ブロックの一部が戻せない（素材不足・未解放・上限・占有） | Ctrl+Z 時のインベントリ・世界状態による | サーバー拒否通知＋開発者ログ `[RemovalRestore]` | しない（履歴は消費済み・手で引き直す） | ユーザー裁定 2026-10-05「できた分だけ戻し残りは通知」 |
| 移行ステップが構造の壊れたJSONで `Failed` | 手で壊したセーブ等の異常時のみ | ロード中断（原本は backup/3） | しない | agent判断 P5（通常運用では起きない） |

## 配置と前例

| 項目 | 配置先 | 前例 |
|---|---|---|
| `ElectricWireConnectionRecord` / `GearChainConnectionRecord`（旧 Cost の改名＋種類） | Game.EnergySystem / Game.Block.Interface.Component（旧型と同じ場所） | 旧 `ElectricWireConnectionCost.cs` / `GearChainConnectionCost.cs`、レールの `RailSegment.RailTypeGuid` |
| `ConnectionLinePartnerMessagePack` | Game.Block/Blocks/ConnectionLine | `ElectricWireStateDetail.cs`（状態詳細 MessagePack は Game.Block） |
| `SaveMigrationStepV3ToV4` | Game.SaveLoad/Migration/Steps | `SaveMigrationStepV2ToV3.cs` |
| `GearChainDisconnectFailureReason` / `GearChainConnectFailureReason` | Server.Protocol/PacketResponse/Util/GearChain | `ElectricWirePlacementFailureReason` |
| `RailRemovalRefundCalculator` | Server.Protocol/PacketResponse/Util/RailEdit | `RailConnectionEditService.cs`（切断返却の算出） |
| `BlockPlacementWiring` | `PlacePacketDto.cs`（既存DTOファイルへ追記） | `PlaceInfoMessagePack` |
| `RailConnectByDestinationProtocol` | Server.Protocol/PacketResponse（IPacketResponse） | `RailConnectionEditProtocol.cs`、登録は `PacketResponseCreator` |
| `ConnectionLineDeleteTarget` / `ConnectionLineRegistry` | Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine | `DeleteTargetRail.cs`（線状の削除対象は表示要素側に付く） |
| `DeleteTargetHitSelector` / `DeleteTargetRaycaster` | Client.Game/InGame/UI/UIState/State/DragDelete | `BlockClickDetectUtil.TryGetFrontmostSolidHit` |
| `IRemovedObject` 系・`IRemovalRestoreSender` | Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal | `RemoveOperationRecord.cs` / `IBuildOperationRecord.cs` |
| `BlockAttachedConnectionResolver` | DragDelete、`ClientDIContext` static で参照 | `ClientDIContext.BlockGameObjectDataStore` の static 参照（`ElectricWireLineViewElement.cs`） |

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
| 通常設置の電線自動接続 | 生きる | AutoConnect が既定、Undoだけ RecordedOnly（Task 6） |

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / チェーン当たり判定がツールチップのレイを遮る退化を Task 8 Step 3b で除外。配置は全て前例どおり
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## タスク順と依存

Task 1→2→3（サーバー記録・移行・同期）→4（チェーン切断）→5→6→7（レール返却・設置指定・座標接続）→8→9→10→11→12→13（クライアント）→14（録画テスト）→15→16（閉じタスク）。各タスクの見出し括弧内の A/B/C 番号は本文中の相互参照名。


## A群の前提事実

前提パス: `S = moorestech_server/Assets/Scripts`, `C = moorestech_client/Assets/Scripts`（Files節は展開済みのリポジトリ相対パスで書く）。
全タスク共通: 新規 `.cs` を足した直後はサーバー側 file: パッケージのため `uloop launch ./moorestech_client --restart` が要る（moorestech-save-migration Gotchas）。`.meta` は手で作らない。

---

### Task 1 (A1): 接続1本の記録型（Record）へ改名し「引いた種類」を保持・セーブする

**Files:**
- Rename+Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireConnectionCost.cs` → `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireConnectionRecord.cs`（`git mv`。`.meta` も `git mv` で追従させ、GUIDを保つ）
- Rename+Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/GearChainConnectionCost.cs` → `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/GearChainConnectionRecord.cs`（同上）
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
- コンパイル対象外だが古い参照: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-v2.cs:61`（既に存在しない ctor `(ItemId,int)` を使う陳腐化シナリオ。本タスクでは `new ElectricWireConnectionRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>())` へ書き換える）

**Interfaces:**
- Consumes: なし（最初のタスク）
- Produces:
  - `Game.EnergySystem.ElectricWireConnectionRecord`（readonly struct）: `ElectricWireConnectionRecord(Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)`、`Guid ConnectToolGuid`、`IReadOnlyList<ConnectToolMaterialCost> Materials`、`int TotalCount`。`Empty` は廃止
  - `Game.Block.Interface.Component.GearChainConnectionRecord`（同形）
  - `IElectricWireConnector.WireConnections : IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionRecord Record)>`、`TryAddWireConnection(BlockInstanceId, ElectricWireConnectionRecord)`、`TryRemoveWireConnection(BlockInstanceId, out ElectricWireConnectionRecord)`
  - `IGearChainPole.TryAddChainConnection(BlockInstanceId, GearChainConnectionRecord)`、`TryRemoveChainConnection(BlockInstanceId, out GearChainConnectionRecord)`、**新設** `bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out GearChainConnectionRecord record)`（A4 の返却前検査が使う）
  - `ElectricWireDisconnectUtil.TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out ElectricWirePlacementFailureReason failureReason)`（旧 `ElectricWireSystemUtil.TryDisconnect` の移設。挙動不変）
  - `GearChainConnectionSet`（Game.Block 内部用 public class）: `Count`、`PartnerIds`、`Transformers`、`Contains`、`Add`、`TryRemove`、`TryGetRecord`、`Clear`、`CreateRefundItems()`、`CreateSaveData()`
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

`moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireConnectionRecord.cs`（`git mv` 後に全置換）:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.EnergySystem
{
    /// <summary>
    /// 電線1本の接続記録。引いた接続ツールの種類と消費した素材を持ち、切断・撤去の返却とUndoの引き直しに使う
    /// Record of one wire connection: the connect tool it was drawn with and the consumed materials, used for refunds and undo re-drawing
    /// </summary>
    public readonly struct ElectricWireConnectionRecord
    {
        public readonly Guid ConnectToolGuid;
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public ElectricWireConnectionRecord(Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)
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

`moorestech_server/Assets/Scripts/Game.Block.Interface/Component/GearChainConnectionRecord.cs`（`git mv` 後に全置換。既存の using を保持）:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// 歯車チェーン1接続の記録。引いた接続ツールの種類と消費した素材を持ち、切断・撤去の返却とUndoの引き直しに使う
    /// Record of one gear-chain connection: the connect tool it was drawn with and the consumed materials, used for refunds and undo re-drawing
    /// </summary>
    public readonly struct GearChainConnectionRecord
    {
        public readonly Guid ConnectToolGuid;
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public GearChainConnectionRecord(Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)
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

`IElectricWireConnector.cs` L20-24 を置換:

```csharp
        IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionRecord Record)> WireConnections { get; }

        bool ContainsWireConnection(BlockInstanceId partnerId);
        bool TryAddWireConnection(BlockInstanceId partnerId, ElectricWireConnectionRecord record);
        bool TryRemoveWireConnection(BlockInstanceId partnerId, out ElectricWireConnectionRecord record);
```

`IGearChainPole.cs` L9-10 を置換:

```csharp
        bool TryAddChainConnection(BlockInstanceId partnerId, GearChainConnectionRecord connectionRecord);
        bool TryRemoveChainConnection(BlockInstanceId partnerId, out GearChainConnectionRecord record);

        // 切断前の返却検査用に、指定相手との接続記録を読む（無ければfalse）
        // Read the record of the connection to the given partner for the pre-disconnect refund check (false when absent)
        bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out GearChainConnectionRecord record);
```

`ElectricWireConnectorComponent.cs` の置換メンバー（他は無変更）:

```csharp
        private readonly Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionRecord Record)> _wireConnections = new();
        public IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionRecord Record)> WireConnections => _wireConnections;
```
```csharp
        public bool TryAddWireConnection(BlockInstanceId partnerId, ElectricWireConnectionRecord connectionRecord)
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

        public bool TryRemoveWireConnection(BlockInstanceId partnerId, out ElectricWireConnectionRecord record)
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
`GetRefundItems` 内 `var materials = connection.Cost.Materials;` → `var materials = connection.Record.Materials;`。
`OnPostBlockLoad` 内 `var cost = connection.ToConnectionCost(); _wireConnections.Add(targetId, (connector, cost));` → `var record = connection.ToConnectionRecord(); _wireConnections.Add(targetId, (connector, record));`。

`ElectricWireSaveDataJsonObject.cs` 全体:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ElectricWire
{
    public class ElectricWireSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<ElectricWireConnectionJsonObject> Connections { get; set; }

        public ElectricWireSaveDataJsonObject(Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionRecord Record)> wireConnections)
        {
            // 接続をリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<ElectricWireConnectionJsonObject>();
            foreach (var target in wireConnections)
            {
                Connections.Add(new ElectricWireConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Record));
            }

            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            Connections.Sort((left, right) => left.TargetBlockInstanceId.CompareTo(right.TargetBlockInstanceId));
        }

        public ElectricWireSaveDataJsonObject() { Connections = new List<ElectricWireConnectionJsonObject>(); }
    }


    public class ElectricWireConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; set; }

        // 引いた種類はUndoの引き直しに要る。欠けた旧形はマイグレーションが埋めるので、ここでは必須で読む
        // The drawn tool is needed for undo re-drawing; the migration fills it for old saves, so it is read as required here
        [JsonProperty("connectToolGuid", Required = Required.Always)] public Guid ConnectToolGuid { get; set; }
        [JsonProperty("materials")] public List<ConnectToolMaterialSaveJsonObject> Materials { get; set; }

        public ElectricWireConnectionJsonObject() { Materials = new List<ConnectToolMaterialSaveJsonObject>(); }

        public ElectricWireConnectionJsonObject(int targetBlockInstanceId, ElectricWireConnectionRecord record)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            ConnectToolGuid = record.ConnectToolGuid;
            Materials = record.Materials == null
                ? new List<ConnectToolMaterialSaveJsonObject>()
                : record.Materials.Select(m => new ConnectToolMaterialSaveJsonObject(m)).ToList();
        }

        // ロード時に永続値から接続記録を復元する
        // Restore the connection record from persisted values on load
        public ElectricWireConnectionRecord ToConnectionRecord()
        {
            var materials = (Materials ?? new List<ConnectToolMaterialSaveJsonObject>())
                .Select(m => m.ToMaterialCost()).ToList();
            return new ElectricWireConnectionRecord(ConnectToolGuid, materials);
        }
    }
}
```

`GearChainPoleSaveDataJsonObject.cs` 全体:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<GearChainPoleConnectionJsonObject> Connections { get; set; }

        public GearChainPoleSaveDataJsonObject(IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, GearChainConnectionRecord Record)> chainTargets)
        {
            // DictionaryからConnectionDataのリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<GearChainPoleConnectionJsonObject>();
            foreach (var target in chainTargets)
            {
                Connections.Add(new GearChainPoleConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Record));
            }

            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            Connections.Sort((left, right) => left.TargetBlockInstanceId.CompareTo(right.TargetBlockInstanceId));
        }

        public GearChainPoleSaveDataJsonObject() { Connections = new List<GearChainPoleConnectionJsonObject>(); }
    }


    public class GearChainPoleConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; set; }

        // 引いた種類はUndoの引き直しに要る。欠けた旧形はマイグレーションが埋めるので、ここでは必須で読む
        // The drawn tool is needed for undo re-drawing; the migration fills it for old saves, so it is read as required here
        [JsonProperty("connectToolGuid", Required = Required.Always)] public Guid ConnectToolGuid { get; set; }
        [JsonProperty("materials")] public List<ConnectToolMaterialSaveJsonObject> Materials { get; set; }

        public GearChainPoleConnectionJsonObject() { Materials = new List<ConnectToolMaterialSaveJsonObject>(); }

        public GearChainPoleConnectionJsonObject(int targetBlockInstanceId, GearChainConnectionRecord record)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            ConnectToolGuid = record.ConnectToolGuid;
            Materials = record.Materials == null
                ? new List<ConnectToolMaterialSaveJsonObject>()
                : record.Materials.Select(m => new ConnectToolMaterialSaveJsonObject(m)).ToList();
        }

        // ロード時に永続値から接続記録を復元する
        // Restore the connection record from persisted values on load
        public GearChainConnectionRecord ToConnectionRecord()
        {
            var materials = (Materials ?? new List<ConnectToolMaterialSaveJsonObject>())
                .Select(m => m.ToMaterialCost()).ToList();
            return new GearChainConnectionRecord(ConnectToolGuid, materials);
        }
    }
}
```

`moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainConnectionSet.cs`（新規。GearChainPoleComponent を200行未満へ戻すための接続台帳）:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Gear.Common;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーンポール1本が持つチェーン接続の台帳。dirty化と状態通知は持ち主のコンポーネントが行う
    /// Ledger of a pole's chain connections; topology dirtying and state notifications stay with the owning component
    /// </summary>
    public class GearChainConnectionSet
    {
        private readonly Dictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, GearChainConnectionRecord Record)> _targets = new();

        public int Count => _targets.Count;
        public IEnumerable<BlockInstanceId> PartnerIds => _targets.Keys;
        public IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, GearChainConnectionRecord Record)> Targets => _targets;

        public bool Contains(BlockInstanceId partnerId)
        {
            return _targets.ContainsKey(partnerId);
        }

        public void Add(BlockInstanceId partnerId, IGearEnergyTransformer transformer, GearChainConnectionRecord record)
        {
            _targets.Add(partnerId, (transformer, record));
        }

        public bool TryRemove(BlockInstanceId partnerId, out GearChainConnectionRecord record)
        {
            if (!_targets.Remove(partnerId, out var connection))
            {
                record = default;
                return false;
            }

            record = connection.Record;
            return true;
        }

        public bool TryGetRecord(BlockInstanceId partnerId, out GearChainConnectionRecord record)
        {
            var found = _targets.TryGetValue(partnerId, out var connection);
            record = found ? connection.Record : default;
            return found;
        }

        public void Clear()
        {
            _targets.Clear();
        }

        // 撤去時に返す素材を接続ごとに展開する
        // Expand the materials to refund on removal, per connection
        public IReadOnlyList<IItemStack> CreateRefundItems()
        {
            var refundItems = new List<IItemStack>();
            foreach (var connection in _targets.Values)
            {
                var materials = connection.Record.Materials;
                if (materials == null) continue;
                foreach (var material in materials)
                {
                    if (material.Count <= 0 || material.ItemId == ItemMaster.EmptyItemId) continue;
                    refundItems.Add(ServerContext.ItemStackFactory.Create(material.ItemId, material.Count));
                }
            }

            return refundItems;
        }

        public GearChainPoleSaveDataJsonObject CreateSaveData()
        {
            return new GearChainPoleSaveDataJsonObject(_targets);
        }
    }
}
```

`GearChainPoleComponent.cs` の置換（`_chainTargets` を台帳へ寄せる。using `Core.Item.Interface`/`Core.Master` は不要になれば削除）:

```csharp
        private readonly GearChainConnectionSet _chainConnections = new();
```
```csharp
        public bool IsConnectionFull => _chainConnections.Count >= _param.MaxConnectionCount;
```
```csharp
        public List<GearConnect> GetGearConnects()
        {
            // コネクタ経由の隣接接続にチェーン接続を加えて返す
            // Return adjacent connections via the connector plus chain connections
            var result = _gearService.GetGearConnects();
            foreach (var chainTarget in _chainConnections.Targets.Values) result.Add(new GearConnect(chainTarget.Transformer, _chainOption, _chainOption));

            return result;
        }

        public bool ContainsChainConnection(BlockInstanceId partnerId)
        {
            return _chainConnections.Contains(partnerId);
        }

        public bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out GearChainConnectionRecord record)
        {
            return _chainConnections.TryGetRecord(partnerId, out record);
        }

        public bool TryAddChainConnection(BlockInstanceId partnerId, GearChainConnectionRecord connectionRecord)
        {
            // 新しい接続先を記録する
            // Store new partner connection
            if (_chainConnections.Contains(partnerId)) return false;
            if (_chainConnections.Count >= _param.MaxConnectionCount) return false;
            var transformer = ResolveChainTarget(partnerId);
            if (transformer == null) return false;
            _chainConnections.Add(partnerId, transformer, connectionRecord);
            // 接続集合の変更点自身でdirty化し、呼び出し元の再構築漏れを構造的に防ぐ
            // Mark dirty at the mutation itself so no caller can ever forget the rebuild
            ServerContext.GetService<IGearNetworkDatastore>().MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }

        public bool TryRemoveChainConnection(BlockInstanceId partnerId, out GearChainConnectionRecord record)
        {
            if (!_chainConnections.TryRemove(partnerId, out record)) return false;

            ServerContext.GetService<IGearNetworkDatastore>().MarkTopologyDirty();
            _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }
```
```csharp
        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            return _chainConnections.CreateRefundItems();
        }
```
`OnPostBlockLoad` 内: `_chainTargets.Clear();` → `_chainConnections.Clear();`、`if (_chainTargets.Count >= ...) break;` → `if (_chainConnections.Count >= _param.MaxConnectionCount) break;`、`if (_chainTargets.ContainsKey(targetId)) continue;` → `if (_chainConnections.Contains(targetId)) continue;`、`var cost = connection.ToConnectionCost(); _chainTargets.Add(targetId, (transformer, cost));` → `_chainConnections.Add(targetId, transformer, connection.ToConnectionRecord());`。
`Destroy` 内: `foreach (var targetId in _chainTargets.Keys.ToList())` → `foreach (var targetId in _chainConnections.PartnerIds.ToList())`、`_chainTargets.Clear();` → `_chainConnections.Clear();`。
`GetBlockStateDetails` 内: `var partnerIds = _chainTargets.Keys;` → `var partnerIds = _chainConnections.PartnerIds;`（A3 で置換）。
`GetSaveState` 本体 → `return _chainConnections.CreateSaveData();`。
空行2連（L52-53・L111-112・L188-189 等）は1行に詰めて、最終行数が200未満であることを `wc -l` で確認する。

`ElectricWirePlacementJudgement.cs` L13-25:

```csharp
        public readonly ElectricWireConnectionRecord WireRecord;

        private ElectricWirePlacementJudgement(bool isPlaceable, ElectricWirePlacementFailureReason failureReason, ElectricWireConnectionRecord wireRecord)
        {
            IsPlaceable = isPlaceable;
            FailureReason = failureReason;
            WireRecord = wireRecord;
        }

        public static ElectricWirePlacementJudgement Success(ElectricWireConnectionRecord wireRecord)
        {
            return new ElectricWirePlacementJudgement(true, ElectricWirePlacementFailureReason.None, wireRecord);
        }
```

`ElectricWirePlacementEvaluator.cs` L47-56:

```csharp
            return ElectricWirePlacementJudgement.Success(new ElectricWireConnectionRecord(connectToolGuid, materials));
        }

        // 種類と距離から電線1本の接続記録を作る。マスタに無い種類はfalse
        // Build one wire's connection record from the tool and distance; false for a tool absent from the master
        public static bool TryCreateWireRecord(Guid connectToolGuid, float distance, out ElectricWireConnectionRecord record)
        {
            record = default;
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, distance, out var materials)) return false;
            record = new ElectricWireConnectionRecord(connectToolGuid, materials);
            return true;
        }
```

`ElectricWireAutoConnectPlan.cs`: 3箇所の `ElectricWireConnectionCost` を `ElectricWireConnectionRecord`、タプル要素名 `Cost` を `Record` に置換:

```csharp
        public readonly IReadOnlyList<(BlockInstanceId TargetId, ElectricWireConnectionRecord Record)> Targets;
```
```csharp
        private ElectricWireAutoConnectPlan(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionRecord)> targets, Guid connectToolGuid, ElectricWirePlacementFailureReason failureReason, bool isPlaceable)
```
```csharp
        public static ElectricWireAutoConnectPlan Success(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionRecord)> targets, Guid connectToolGuid)
```
```csharp
            return new ElectricWireAutoConnectPlan(Array.Empty<(BlockInstanceId, ElectricWireConnectionRecord)>(), Guid.Empty, failureReason, false);
```

`ElectricWireAutoConnectService.cs`: L37・L46 の `Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>()` → `Array.Empty<(BlockInstanceId, ElectricWireConnectionRecord)>()`、L58 の `out List<(BlockInstanceId, ElectricWireConnectionCost)> selectedTargets` → `ElectricWireConnectionRecord`、L78-91:

```csharp
            bool TryBuildTargets(Guid connectToolGuid, out List<(BlockInstanceId, ElectricWireConnectionRecord)> builtTargets, out Dictionary<ItemId, int> requiredByItem)
            {
                builtTargets = new List<(BlockInstanceId, ElectricWireConnectionRecord)>();
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

`ElectricWireSystemUtil.cs`: L86-96 の `judgement.WireCost` → `judgement.WireRecord`（3箇所）、L178 `ElectricWireConnectionCost cost` → `ElectricWireConnectionRecord record`（本体の `cost` も `record` へ）。L136-174 の `TryDisconnect` を丸ごと削除し、新規ファイルへ移す:

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
            var refundStacks = ConnectToolMaterialConsumer.CreateRefundItems(record.Materials);
            if (0 < refundStacks.Count && !inventory.InsertionCheck(refundStacks))
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

`GearChainPlacementEvaluator.cs`: L61 → `return GearChainPlacementJudgement.Success(new GearChainConnectionRecord(connectToolGuid, materials));`、L72-84:

```csharp
        public readonly GearChainConnectionRecord ChainRecord;

        public bool IsPlaceable => string.IsNullOrEmpty(FailureReason);

        private GearChainPlacementJudgement(string failureReason, GearChainConnectionRecord chainRecord)
        {
            FailureReason = failureReason;
            ChainRecord = chainRecord;
        }

        public static GearChainPlacementJudgement Success(GearChainConnectionRecord chainRecord)
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
- `FakeWireConnector.cs`: 全 `ElectricWireConnectionCost` → `ElectricWireConnectionRecord`、タプル要素名 `Cost` → `Record`。L64 → `var record = new ElectricWireConnectionRecord(Guid.Parse("c0000000-0000-0000-0000-000000000001"), new List<ConnectToolMaterialCost> { new(new ItemId(1), 1) });`（`using System;` 追加）。`TryRemoveWireConnection(..., out ElectricWireConnectionRecord record)` 本体の `cost =` を `record =` に。
- `ElectricWireTestUtil.cs` L27:
```csharp
            // テスト用の電線ツール種で素材0の接続記録を張る
            // Wire with a zero-material record of the test mod's wire tool
            var record = new ElectricWireConnectionRecord(TestWireConnectToolGuid, Array.Empty<ConnectToolMaterialCost>());
            connectorA.TryAddWireConnection(connectorB.BlockInstanceId, record);
            connectorB.TryAddWireConnection(connectorA.BlockInstanceId, record);
```
  クラス先頭に `private static readonly Guid TestWireConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");`（`using Core.Master;` を追加）。
- `GearChainPoleSaveLoadTest.cs` L39 → `var noCost = new GearChainConnectionRecord(Guid.Parse("c0000000-0000-0000-0000-000000000003"), Array.Empty<ConnectToolMaterialCost>());`
- `ElectricWireRemovalTest.cs` L74-75: `.Cost` → `.Record`
- `ElectricWirePlacementEvaluatorTest.cs`: `judgement.WireCost` → `judgement.WireRecord`、L129-131 のテスト名と呼び出しを `TryCreateWireRecordは距離を切り上げて記録を作る` / `TryCreateWireRecord(ConnectToolGuid, 3.2f, out var cost)` に、末尾に `Assert.AreEqual(ConnectToolGuid, cost.ConnectToolGuid);` を追加
- `GearChainPlacementEvaluatorTest.cs` L115-116: `judgement.ChainCost` → `judgement.ChainRecord`
- `ElectricWireSystemUtilTest.cs`: `ElectricWireSystemUtil.TryDisconnect` → `ElectricWireDisconnectUtil.TryDisconnect`

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop launch ./moorestech_client --restart`（新規 .cs のため）→ `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConnectionRecordSaveLoadTest|ElectricWireSaveLoadTest|GearChainPoleSaveLoadTest|ElectricWireRemovalTest|ElectricWirePlacementEvaluatorTest|GearChainPlacementEvaluatorTest|ElectricWireSystemUtilTest|ElectricWireDisconnectProtocolTest|ChainProtocolTest|GearChainSystemUtilTest|ElectricWireAutoConnect|ElectricWireExtend"`
Expected: ErrorCount 0 / 全 PASS。`wc -l` で `GearChainPoleComponent.cs`・`ElectricWireSystemUtil.cs`・`ElectricWireConnectorComponent.cs` が200未満。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire moorestech_server/Assets/Scripts/Game.Block.Interface/Component moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-v2.cs
git commit -m "feat(server): 電線・チェーンの接続記録に引いた種類を持たせセーブする"
```
（`git add -A` は禁止: Unity がmasterピンファイルを書き戻すため。Unity が生成した新規 `.meta` はここで一緒に add する）

---

### Task 2 (A2): セーブ版3→4 マイグレーション（旧セーブの接続へ線種別ごとの唯一の種類を書き込む）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs:25`（`CurrentVersion = 3` → `4`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV3ToV4.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V3ToV4/ConnectionToolGuidFiller.cs`
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
- Produces: `WorldSaveAllInfo.CurrentVersion == 4`、引数なしの `SaveMigrationStepV3ToV4()`、`SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid` / `GearChainConnectToolGuid`（`public static readonly Guid`。テストが参照する）、`ConnectionToolGuidFiller.TryFill(JObject state, string saveKey, Guid connectToolGuid, out int filled, out string failureReason)`

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
（テストmodの接続ツールGuidは `c0…01`/`c0…03` だが、移行は本番の固定Guidを書く。ロード時の記録は素材とGuidを読むだけで、マスタへ照合しない（`ElectricWireConnectionJsonObject.ToConnectionRecord`）。だから、テストmodに無いGuidでもロードは通る。）

- [ ] **Step 2: 実装を書く**

`WorldSaveAllInfo.cs` L25: `public const int CurrentVersion = 4;`

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
        public static bool TryFill(JObject state, string saveKey, Guid connectToolGuid, out int filled, out string failureReason)
        {
            filled = 0;
            failureReason = null;

            // この種の接続を持たないブロックは対象外
            // Blocks without this kind of connection are out of scope
            var componentState = state[saveKey];
            if (componentState == null || componentState.Type == JTokenType.Null) return true;

            // 辿れない形を素通しすると未変換のまま版4が刻まれ、必須キーの読み込みで後から落ちる
            // Passing an unwalkable shape would stamp version 4 on an unconverted save that later fails on the required key
            if (!(componentState is JObject componentObject) || !(componentObject["connections"] is JArray connections))
            {
                failureReason = $"state['{saveKey}'].connectionsが配列ではないため種類を書き込めません。 type={componentState.Type}";
                return false;
            }

            foreach (var connectionToken in connections)
            {
                if (!(connectionToken is JObject connection))
                {
                    failureReason = $"state['{saveKey}']の接続要素がオブジェクトではありません。 type={connectionToken.Type}";
                    return false;
                }

                // 既に新形式なら上書きしない。塗り潰すと正しい値が無音で消える
                // Leave the new shape untouched; overwriting would silently erase the correct value
                if (connection["connectToolGuid"] != null) continue;

                connection["connectToolGuid"] = connectToolGuid.ToString();
                filled++;
            }

            return true;
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
                if (!ConnectionToolGuidFiller.TryFill(state, WireSaveKey, ElectricWireConnectToolGuid, out var wire, out var reason)) return Fail(reason);
                if (!ConnectionToolGuidFiller.TryFill(state, ChainSaveKey, GearChainConnectToolGuid, out var chain, out reason)) return Fail(reason);
                wireFilled += wire;
                chainFilled += chain;
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
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainConnectionSet.cs`（A1 新設。メソッド1本追加）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainPoleComponent.cs`（`GetBlockStateDetails` の `partnerIds` 行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireStateChangeProcessor.cs:41-45`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/GearChainPoleStateChangeProcessor.cs:32-36`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPoleExtendPreviewCalculator.cs:83`
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionLineStateDetailTest.cs`

**Interfaces:**
- Consumes: A1 の `ElectricWireConnectionRecord.ConnectToolGuid`、`GearChainConnectionSet.Targets`
- Produces:
  - `Game.Block.Blocks.ConnectionLine.ConnectionLinePartnerMessagePack`: `[Key(0)] int PartnerBlockInstanceId`、`[Key(1)] Guid ConnectToolGuid`、public ctor `(int partnerBlockInstanceId, Guid connectToolGuid)` と `[Obsolete]` のデシリアライズ用 ctor
  - `ElectricWireStateDetail.Partners` / `GearChainPoleStateDetail.Partners`: `[Key(0)] ConnectionLinePartnerMessagePack[]`、ctor `(ConnectionLinePartnerMessagePack[] partners)`（旧 `PartnerBlockInstanceIds` は削除）
  - `GearChainConnectionSet.CreatePartnerMessagePacks() : ConnectionLinePartnerMessagePack[]`
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
            var stateDetail = new ElectricWireStateDetail(_wireConnections.Select(c => new ConnectionLinePartnerMessagePack(c.Key.AsPrimitive(), c.Value.Record.ConnectToolGuid)).ToArray());
```

`GearChainConnectionSet.cs` に追加（`using System.Linq;`・`using Game.Block.Blocks.ConnectionLine;`）:

```csharp
        // クライアント同期用に接続先と種類を並べる
        // List partners and tools for client sync
        public ConnectionLinePartnerMessagePack[] CreatePartnerMessagePacks()
        {
            return _targets.Select(t => new ConnectionLinePartnerMessagePack(t.Key.AsPrimitive(), t.Value.Record.ConnectToolGuid)).ToArray();
        }
```

`GearChainPoleComponent.cs` の `GetBlockStateDetails`:

```csharp
            var stateDetail = new GearChainPoleStateDetail(_chainConnections.CreatePartnerMessagePacks());
```
（直前の `var partnerIds = ...;` 行を削除）

クライアント `ElectricWireStateChangeProcessor.cs` L41-45:

```csharp
            // 接続先InstanceIdを配列に変換する（種類の利用は表示側の作り替えで行う）
            // Convert partner instance IDs to an array (the tool is consumed by the view rework)
            var partnerInstanceIds = state.Partners?
                .Select(p => new BlockInstanceId(p.PartnerBlockInstanceId))
                .ToArray() ?? Array.Empty<BlockInstanceId>();
```

`GearChainPoleStateChangeProcessor.cs` L32-36:

```csharp
            // 接続先InstanceIdを配列に変換
            // Convert partner instance IDs to array
            var partnerInstanceIds = state.Partners?
                .Select(p => new BlockInstanceId(p.PartnerBlockInstanceId))
                .ToArray() ?? Array.Empty<BlockInstanceId>();
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
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainConnectFailureReason.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainSystemUtil.cs`（`TryDisconnect` 追加・`TryConnect` の失敗理由を enum 化）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GearChainConnectionEditProtocol.cs:14-49,55-80,97-102`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:161`（`DisconnectGearChain` 追加）
- Modify: `moorestech_web/webui/src/features/notification/notificationMessages.ts:48`
- Modify: `moorestech_web/webui/src/features/notification/notificationServerIdCoverage.test.ts:23-33,98`
- Modify: `Localization/localization.csv:119`（切断3行＋接続6行を追加）
- Regenerate: `moorestech_web/webui/src/shared/i18n/generated/*`（`pnpm gen:i18n`）
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainDisconnectProtocolTest.cs`
- Create (test): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainConnectDeniedNotificationTest.cs`
- Modify (test, `TryConnect` の enum 化への追従): `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergySaveLoadTest.cs:51`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergyTransmissionTest.cs:56`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest/GearChainRemovalTest.cs:52,97-98`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest.cs:46,64,87,111,148-149,155`、Task 1〜3 で作った `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionRecordSaveLoadTest.cs`・`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/ConnectToolGuidMigrationLoadTest.cs`・`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire/ConnectionLineStateDetailTest.cs`

**Interfaces:**
- Consumes: A1 の `IGearChainPole.TryGetChainConnectionRecord`、`GearChainConnectionRecord.Materials`
- Produces:
  - `public enum GearChainDisconnectFailureReason { None, InvalidTarget, NotConnected, InventoryFull }`（`Server.Protocol.PacketResponse.Util.GearChain`）
  - `GearChainSystemUtil.TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out GearChainDisconnectFailureReason failureReason)`
  - `GearChainConnectionEditProtocol.ChainEditMode.Disconnect`、`GearChainConnectionEditRequest.CreateDisconnectRequest(Vector3Int posA, Vector3Int posB)`。拒否時は `denied.gearChainDisconnect.{reason}` を NotificationService で要求者へ通知
  - `public enum GearChainConnectFailureReason { None, InvalidTarget, NotUnlocked, TooFar, AlreadyConnected, ConnectionLimit, NoItem }`（値名は `GearChainPlacementEvaluator` の文字列定数と同じ綴り）
  - `GearChainSystemUtil.TryConnect(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid, out GearChainConnectFailureReason failureReason)`（旧 `out string error` を置換）
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
            var refundStacks = ConnectToolMaterialConsumer.CreateRefundItems(record.Materials);
            if (0 < refundStacks.Count && !inventory.InsertionCheck(refundStacks))
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

- [ ] **Step 4: 接続失敗を enum 化して通知する実装を書く**

網羅テスト（`notificationServerIdCoverage.test.ts`）が補間idを分類できるのは enum を展開する形だけなので、`GearChainSystemUtil.TryConnect` の `out string error` を enum へ置き換える。現在の失敗文字列（実コード確認済み）は次のとおり:
- `GearChainSystemUtil.TryConnect` 自身が返すもの: `InvalidTargetError`＋` (foundA=..., foundB=...)` の付記（端点が無い）、`InvalidTargetError`（同一ブロック）、`NotUnlockedError`、`"ConnectionLimit"` リテラル（両側の追加に失敗）
- `GearChainPlacementEvaluator.EvaluatePlacement` 経由のもの: `TooFarError`、`AlreadyConnectedError`、`ConnectionLimitError`、`NoItemError`

enum の各値名は、これらの定数の文字列値（`"TooFar"` 等）と一致させる。そのため `reason.ToString()` は従来の `Error` と同じ文字列になる（InvalidTarget の付記だけは消え、開発者ログへ移す）。

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainConnectFailureReason.cs`:

```csharp
namespace Server.Protocol.PacketResponse.Util.GearChain
{
    /// <summary>
    /// 歯車チェーン接続の拒否理由。値名は GearChainPlacementEvaluator の文字列定数と同じ綴りにし、通知idの接尾辞にも使う
    /// Refusal reasons for a gear chain connect; names match GearChainPlacementEvaluator's string constants and suffix notification ids
    /// </summary>
    public enum GearChainConnectFailureReason
    {
        None,
        InvalidTarget,
        NotUnlocked,
        TooFar,
        AlreadyConnected,
        ConnectionLimit,
        NoItem,
    }
}
```

`GearChainSystemUtil.cs` の `TryConnect` を置き換え、評価器の文字列を enum へ写す private メソッドを足す（`using System;` は既存）:

```csharp
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid, out GearChainConnectFailureReason failureReason)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            failureReason = GearChainConnectFailureReason.None;
            var foundA = TryGetGearChainPole(posA, out var poleA, out _);
            var foundB = TryGetGearChainPole(posB, out var poleB, out _);
            if (!foundA || !foundB)
            {
                // どちらの端点が無いかは通知idに載らないので開発者ログへ残す
                // Which endpoint is missing does not fit the notification id, so leave it in the developer log
                Debug.Log($"チェーン接続を拒否: 端点にポールがありません foundA={foundA} foundB={foundB} posA={posA} posB={posB}");
                failureReason = GearChainConnectFailureReason.InvalidTarget;
                return false;
            }

            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                failureReason = GearChainConnectFailureReason.InvalidTarget;
                return false;
            }

            // 未解放のconnectToolによる接続要求は拒否する
            // Reject connection requests using a connectTool that is not unlocked
            if (!IsConnectToolUnlocked(connectToolGuid))
            {
                failureReason = GearChainConnectFailureReason.NotUnlocked;
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
                failureReason = ToConnectFailureReason(judgement.FailureReason);
                return false;
            }
            var record = judgement.ChainRecord;

            // 接続を確定させる
            // Finalize connection
            var addedA = poleA.TryAddChainConnection(poleB.BlockInstanceId, record);
            var addedB = addedA && poleB.TryAddChainConnection(poleA.BlockInstanceId, record);
            if (!addedA || !addedB)
            {
                poleA.TryRemoveChainConnection(poleB.BlockInstanceId, out _);
                poleB.TryRemoveChainConnection(poleA.BlockInstanceId, out _);
                failureReason = GearChainConnectFailureReason.ConnectionLimit;
                return false;
            }

            ConnectToolMaterialConsumer.Consume(record.Materials, inventory);
            return true;
        }

        // 評価器の失敗文字列（延長プロトコルも共有する）を接続の拒否理由へ写す。EvaluatePlacementが返す4種以外は実装の破れ
        // Map the evaluator's failure strings (shared with the extend protocol) to connect reasons; anything beyond EvaluatePlacement's four is a broken invariant
        private static GearChainConnectFailureReason ToConnectFailureReason(string evaluatorError)
        {
            return evaluatorError switch
            {
                GearChainPlacementEvaluator.TooFarError => GearChainConnectFailureReason.TooFar,
                GearChainPlacementEvaluator.AlreadyConnectedError => GearChainConnectFailureReason.AlreadyConnected,
                GearChainPlacementEvaluator.ConnectionLimitError => GearChainConnectFailureReason.ConnectionLimit,
                GearChainPlacementEvaluator.NoItemError => GearChainConnectFailureReason.NoItem,
                _ => throw new ArgumentOutOfRangeException(nameof(evaluatorError), evaluatorError, "EvaluatePlacementが返さないはずの失敗理由です"),
            };
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

`TryConnect` の呼び出し側テストを enum へ追従させる（本番の呼び出しは `GearChainConnectionEditProtocol` だけ。`GearChainPoleExtendProtocolTest.cs:119-120` と Task 14 のシナリオは `out _` のため無変更）:
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergySaveLoadTest.cs:51`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/ChainEnergyTransmissionTest.cs:56`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest/GearChainRemovalTest.cs:52,97,98`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/GearChainSystemUtilTest.cs:111,148,149`: `Assert.IsEmpty(<var> ?? string.Empty);` → `Assert.AreEqual(GearChainConnectFailureReason.None, <var>);`
- `GearChainSystemUtilTest.cs:46` `Assert.AreEqual("TooFar", error);` → `Assert.AreEqual(GearChainConnectFailureReason.TooFar, error);`、`:64`・`:87` `"NoItem"` → `GearChainConnectFailureReason.NoItem`、`:155` `"ConnectionLimit"` → `GearChainConnectFailureReason.ConnectionLimit`
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
  ["denied.gearChainConnect.", { enumName: "GearChainConnectFailureReason", notSentMembers: ["None"] }],
```
と、走査確認（Step 2 で足した `expect(ids).toContain("denied.gearChainDisconnect.InventoryFull");` の直後）に:

```ts
    expect(ids).toContain("denied.gearChainConnect.NoItem");
```

- [ ] **Step 5: コンパイル・テストを実行して通ることを確認する**

Run:
- `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client`（localization.csv を変えたので、`LocalizationKeys` の無関係キーで CS0117 が出たら force-recompile。記憶: localization-csv-needs-force-recompile）
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearChainDisconnectProtocolTest|GearChainConnectDeniedNotificationTest|ChainProtocolTest|OperationDeniedNotificationTest|GearChainSystemUtilTest|GearChainRemovalTest|ChainEnergy|GearChainPoleExtendProtocolTest|ConnectionRecordSaveLoadTest|ConnectToolGuidMigrationLoadTest|ConnectionLineStateDetailTest"`
- `cd moorestech_web/webui && pnpm gen:i18n && pnpm test -- notificationServerIdCoverage localizationKeysFreshness notificationMessages`
Expected: ErrorCount 0 / Unity 全 PASS / vitest 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWire moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs Localization/localization.csv moorestech_web/webui/src/features/notification moorestech_web/webui/src/shared/i18n/generated
git commit -m "feat(server): 歯車チェーンの返却付き切断を復活し切断・接続の拒否理由を通知する"
```

---


## B群の前提事実

前提として確認済みの事実（下書き時に実コードで確認）:
- `RemoveBlockProtocol.GetResponse` は `GetRefundItems()`（財布返却＋ブロックインベントリ＋`IGetRefundItemsInfo` 1個）を **`RemoveBlock` より前**に集め、`InsertionCheck` が通らなければ `RemoveBlockFailureReason.Unknown` で拒否する（`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs:50-60,95-130`）。レール返却もここへ足せば、区間が消える前に算出され、入りきらない撤去は拒否される。返却が入らない拒否は本群で `RemoveBlockFailureReason.InventoryFull` へ分け、クライアントは `ui.delete.inventoryFull` で理由を出す（コーディネーター裁定）。
- ノード削除時に区間は `RailGraphDatastore.RemoveNode` → `RemoveRailSegmentEdgesFromNode` / `RemoveNodeTo` で消える（`Game.Train/RailGraph/RailGraphDatastore.cs:375-395`）。
- 物理レール1本は有向区間の対 `A→B` と `(B^1)→(A^1)` で表される（`RailConnectionCommandHandler.TryConnect`/`ConnectOppositeNodes`、`Game.Train/RailGraph/RailConnectionCommandHandler.cs:30-43,104-114`）。ブロックのノード（Front/Back は互いに `^1`）から出る区間を全部見れば、ブロックに触れる物理レールは必ず1回以上現れる（`Q` 側がブロック内なら `Q^1` もブロック内で、対の区間 `Q^1→P^1` がブロックのノードから出るため）。
- 駅内部の区間（`RailComponentUtility.cs:76-77`）と駅隣接の自動接続（同:154-155）は `RailNode.ConnectNode(target)` → `Guid.Empty` で張られる（`Game.Train/RailGraph/RailNode.cs:121-124`）。よって `RailTypeGuid == Guid.Empty` の区間は無償扱いで返却しない。
- レール切断の返却算出は「`TryGetRailSegmentType` → `GetRailLength` → `ConnectToolCostCalculator.TryCalculate` → `ConnectToolMaterialConsumer.CreateRefundItems`」（`Server.Protocol/PacketResponse/Util/RailEdit/RailConnectionEditService.cs:104-118`）。撤去時の返却も同じ部品を呼ぶ。
- `ConnectionDestination` の MessagePack 表現 `ConnectionDestinationMessagePack(ConnectionDestination)` / `ToModel()` が既にある（`Server.Util/MessagePack/RailNodeMessagePack.cs:14-37`）。`IRailGraphProvider.ResolveRailNode(ConnectionDestination)` は未登録なら `null`（`RailGraphDatastore.cs:427-442,541-544`）。
- `VanillaApiSendOnly.PlaceBlock` の呼び出し元は3つ（`PlaceBlockProtocolSender.cs:33`、`RemoveOperationRecord.cs:72`、テスト `PlacementPacketCapture.cs:49`）。`SendPlaceBlockProtocolMessagePack` のサーバー側直接生成は `VanillaApiSendOnly.cs:45` とテスト3か所（`PlaceBlockProtocolTestSupport.cs:80`、`ElectricWireAutoConnectPlaceTestBase.cs:84`、`ConstructionPayerWalletTest.cs:116`）。

---

### Task 5 (B1): 橋脚・駅の撤去で付いていたレールの素材を返却する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/RailEdit/RailRemovalRefundCalculator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs:1-34`（using・フィールド・ctor）, `:50`（返却が入らない拒否理由）, `:104-130`（`GetRefundItems`）, `:183-188`（`RemoveBlockFailureReason`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs:103-111`（`GetRemoveDeniedReasonKey`）
- Modify: `Localization/localization.csv:182` の直後（`ui.delete.inventoryFull` 行を追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemoveBlockProtocolTest/RemoveRailBlockRefundTest.cs`

**Interfaces:**
- Consumes: 既存 `IRailGraphDatastore.GetConnectedNodesWithDistance(IRailNode)` / `TryGetRailSegmentType(int, int, out Guid)`、`RailConnectionEditProtocol.GetRailLength(IRailNode, IRailNode)`、`ConnectToolCostCalculator.TryCalculate(Guid, float, out IReadOnlyList<ConnectToolMaterialCost>)`、`ConnectToolMaterialConsumer.CreateRefundItems(IReadOnlyList<ConnectToolMaterialCost>)`
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

- [ ] **Step 2: 返却算出を新設する**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/RailEdit/RailRemovalRefundCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
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
                    if (!countedRails.Add(ToPhysicalRailKey(node.NodeId, target.NodeId))) continue;
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

            // A→B と対の (B^1)→(A^1) を同じ物理レールとして同一キーに正規化する
            // Normalize A→B and its paired (B^1)→(A^1) to one key for the same physical rail
            static (int, int) ToPhysicalRailKey(int fromNodeId, int toNodeId)
            {
                var pairedFrom = toNodeId ^ 1;
                var pairedTo = fromNodeId ^ 1;
                var isDirectSmaller = fromNodeId < pairedFrom || (fromNodeId == pairedFrom && toNodeId <= pairedTo);
                return isDirectSmaller ? (fromNodeId, toNodeId) : (pairedFrom, pairedTo);
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

Run: `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoveRailBlockRefundTest|RemoveTrainRailBlockProtocolTest|RemoveBlockRefundTest|RemoveBlockProtocolTest|DragDeleteDenyReasonTest"`
Expected: ErrorCount 0 / 全件 PASS（既存の撤去系テストが壊れていないこと）

webui の文言網羅を見ているテストがあれば同時に確かめる（csv 行追加のみなので通常は無影響）:
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Localization"`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/RailEdit/RailRemovalRefundCalculator.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemoveBlockProtocolTest/RemoveRailBlockRefundTest.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs Localization/localization.csv
git commit -m "feat: 橋脚・駅の撤去でレール素材を返却し、返却が入らない撤去はInventoryFullで拒否する"
```
（.meta は Unity が生成したものを同じコミットへ含める。手書きしない）

---

### Task 6 (B2): 設置要求に配線方式（自動接続／記録どおりのみ）を持たせる

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
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest/PlaceBlockRecordedOnlyWiringTest.cs`

**Interfaces:**
- Consumes: なし（既存の `ElectricWireAutoConnectService` をそのまま使う）
- Produces:
  - `public enum BlockPlacementWiring { AutoConnect, RecordedOnly }`（名前空間 `Server.Protocol.PacketResponse`、`PlacePacketDto.cs`）
  - `PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(List<PlaceInfo> placeInfos, BlockPlacementWiring wiring)`、`[Key(4)] public BlockPlacementWiring Wiring`
  - クライアント `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo> placePositions, BlockPlacementWiring wiring)`
  - `RemoveOperationRecord.UndoAsync` は `BlockPlacementWiring.RecordedOnly` で送る（契約 11 の Undo 再設計タスクはこの呼び方を引き継ぐ）

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

新規 `PlaceBlockRecordedOnlyWiringTest.cs`:

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
    public class PlaceBlockRecordedOnlyWiringTest : ElectricWireAutoConnectPlaceTestBase
    {
        [Test]
        public void 記録どおりのみで設置すると範囲内に機械があっても電線を張らず消費もしない()
        {
            // 電柱の機械範囲内に機械を置き、電線を持たせた状態で電柱を記録どおりのみで設置する
            // Put a machine in the pole's machine range and place the pole with RecordedOnly while holding wires
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = SetupWire(serviceProvider, 5);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlockWithWiring(packet, ForUnitTestModBlockId.ElectricPoleId, Vector3Int.zero, BlockPlacementWiring.RecordedOnly);

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
            // A layout that auto-connect rejects for wire shortage is still placed under RecordedOnly
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            SetupWire(serviceProvider, 0);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlockWithWiring(packet, ForUnitTestModBlockId.ElectricPoleId, Vector3Int.zero, BlockPlacementWiring.RecordedOnly);

            Assert.IsTrue(worldBlockDatastore.Exists(Vector3Int.zero));
        }
    }
}
```

- [ ] **Step 2: サーバー実装**

`PlacePacketDto.cs` の名前空間末尾（`PlaceInfo` クラスの後）へ追加:

```csharp
    /// <summary>
    ///     設置時の電線の張り方。AutoConnectは通常設置の自動接続、RecordedOnlyは自動接続を行わず後続の明示接続に任せる（Undo再設置用）
    ///     How wires are laid on placement: AutoConnect runs the normal auto-connect, RecordedOnly skips it and leaves wiring to explicit follow-up connects (undo re-placement)
    /// </summary>
    public enum BlockPlacementWiring
    {
        AutoConnect,
        RecordedOnly,
    }
```

`PlaceBlockProtocol.cs:107-117` を次へ置き換え（判定を1か所に集める）:

```csharp
                // 自動接続設置の電気ブロックだけ事前検証し、電線不足ならスキップする（記録どおりのみは配線を後続の明示接続に任せる）
                // Pre-validate only auto-connect electric placements; RecordedOnly leaves wiring to explicit follow-up connects
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
            if (placeInfos.Count != 0) ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos, BlockPlacementWiring.RecordedOnly);
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

（各ファイルに `using Server.Protocol.PacketResponse;` が無ければ足す。`PlaceInfo` を使っている箇所は既に同名前空間を参照している）

- [ ] **Step 4: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockRecordedOnlyWiringTest|ElectricWireAutoConnect|PlaceBlockProtocol|ConstructionPayerWalletTest|PlacementPacketCapture|TrainPierPlacementEntryCostTest"`
Expected: ErrorCount 0 / 全件 PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceBlockProtocolSender.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/RemoveOperationRecord.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainCostIntegration/PlacementPacketCapture.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTestSupport.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Construction/ConstructionPayerWalletTest.cs
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
- `BlockPlacementWiring { AutoConnect, RecordedOnly }` と `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo>, BlockPlacementWiring)`
- `VanillaApiSendOnly.ConnectRailByDestination(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)`
- `VanillaApiSendOnly.DisconnectGearChain(Vector3Int posA, Vector3Int posB)`（Task A4 が定義。本群は消費のみ）
- `ConnectionLinePartnerMessagePack` の ctor `(int partnerBlockInstanceId, Guid connectToolGuid)` とフィールド `PartnerBlockInstanceId` / `ConnectToolGuid`（Task A3）
- Task A3 は状態処理側で `Partners` を一旦 `BlockInstanceId[]` へ写すだけにしてある。本群 Task C2 がその写しを `ConnectionLinePartner` へ置き換える
- Task B2 は `RemoveOperationRecord.cs:72` を `PlaceBlock(placeInfos, BlockPlacementWiring.RecordedOnly)` へ書き換え済み。本群 Task C4 はこのファイルを全面書き換えし、その呼び出しを `VanillaRemovalRestoreSender.PlaceBlocks` へ移す
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
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DestructionCategories.cs`
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
  - `public class ConnectionLineDeleteTarget : MonoBehaviour, IDeleteTarget, IRemovePreviewable` — `void Initialize(BlockInstanceId fromId, BlockInstanceId toId, Guid connectToolGuid, ConnectionLineKind kind, ConnectionLineRegistry registry)`、`BlockInstanceId FromId`、`BlockInstanceId ToId`、`Guid ConnectToolGuid`、`ConnectionLineKind Kind`（いずれも `{ get; private set; }`）
  - `public class ConnectionLineRegistry` — `void Register(ConnectionLineDeleteTarget line)`、`void Unregister(ConnectionLineDeleteTarget line)`、`IReadOnlyList<ConnectionLineDeleteTarget> GetLinesAttachedTo(BlockInstanceId blockId)`
  - `public interface IRemovePreviewable { void SetRemovePreviewing(); void ResetMaterial(); }`（`C/Client.Game/InGame/UI/UIState/State/IRemovePreviewable.cs`）
  - `public static class DestructionCategories { public const string ConnectionLine = "connectionLine"; }`
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

            Assert.AreEqual(Client.Game.InGame.UI.UIState.State.DestructionCategories.ConnectionLine, line.GetDestructionCategory());
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
        void SetRemovePreviewing();
        void ResetMaterial();
    }
}
```

`DestructionCategories.cs`:

```csharp
namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    ///     クライアント側で定める破壊カテゴリー（マスタ由来のブロックカテゴリーとは別枠）
    ///     Destruction categories defined client-side (separate from master-driven block categories)
    /// </summary>
    public static class DestructionCategories
    {
        public const string ConnectionLine = "connectionLine";
    }
}
```

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

        public void SetRemovePreviewing()
        {
            // レンダラーはSetLine後に揃うため、置換器は初回プレビュー時に作る
            // Renderers are complete only after SetLine, so build the replacer on the first preview
            _materialReplacer ??= new RendererMaterialReplacerController(gameObject);
            _materialReplacer.CopyAndSetMaterial(MaterialConst.GetPreviewPlaceBlockMaterial());
            _materialReplacer.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }

        public void ResetMaterial()
        {
            _materialReplacer?.ResetMaterial();
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
            var store = ClientDIContext.BlockGameObjectDataStore;
            if (!store.TryGetBlockGameObject(FromId, out var fromBlock) || !store.TryGetBlockGameObject(ToId, out var toBlock))
            {
                Debug.LogWarning($"[ConnectionLineDelete] endpoint block not found: from={FromId} to={ToId}");
                return;
            }

            var fromPos = fromBlock.BlockPosInfo.OriginalPos;
            var toPos = toBlock.BlockPosInfo.OriginalPos;
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
            return DestructionCategories.ConnectionLine;
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

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.ConnectionLine\."`
Expected: ErrorCount 0 / 4 tests PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DestructionCategories.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/IRemovePreviewable.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/ConnectionLine
git commit -m "feat(client): 電線・歯車チェーンを共通の削除対象として当たり判定と索引を持たせる"
```

---

### Task 10 (C3): 照準の対象解決（最前面＋ドラッグ中は同カテゴリーの最前面）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteTargetHitSelector.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/DeleteTargetRaycaster.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:79-134`（レイキャスト本体を公開ヘルパーへ切り出し）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs:22,93`（`SessionCategory` 公開）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs:33`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DeleteTargetHitSelectorTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteSelectionCategoryTest.cs`（SessionCategory ケース追加）

**Interfaces:**
- Consumes: Task C1 `LayerConst.ConnectionLineLayer` / `ConnectionLineOnlyLayerMask`、Task C2 `DestructionCategories.ConnectionLine`
- Produces:
  - `public readonly struct DeleteTargetHit { float Distance; IDeleteTarget Target; }`
  - `public static class DeleteTargetHitSelector { static bool TrySelect(IReadOnlyList<DeleteTargetHit> hits, string requiredCategory, out IDeleteTarget target) }` — requiredCategory が null なら最前面（最前面が非対象なら false）、非 null ならそのカテゴリーの最前面
  - `public static class DeleteTargetRaycaster { static bool TryGetCursorOnDeleteTarget(string requiredCategory, out IDeleteTarget target) }`
  - `BlockClickDetectUtil.RaycastAimAll(int layerMask, float maxDistance, out Ray ray)` は作らず、`public static int RaycastAimAll(int layerMask, float maxDistance, out RaycastHit[] hits)`（hits は共有バッファ。戻り値件数まで有効。カメラが無ければ 0）
  - `DragDeleteSelection.SessionCategory`（`string`, 未固定は null）

- [ ] **Step 1: テストを書く**

`DeleteTargetHitSelectorTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     照準ヒット列から削除対象を選ぶ規則（最前面／カテゴリー指定時はその中の最前面）を検証する
    ///     Verifies picking a delete target from aim hits (frontmost / frontmost within a required category)
    /// </summary>
    public class DeleteTargetHitSelectorTest
    {
        [Test]
        public void WithoutCategoryPicksFrontmost()
        {
            // 電線がブロックの手前なら電線を取る
            // A wire in front of a block wins
            var wire = new FakeDeleteTarget { Category = DestructionCategories.ConnectionLine };
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(5f, block), new(2f, wire) };

            Assert.IsTrue(DeleteTargetHitSelector.TrySelect(hits, null, out var target));
            Assert.AreSame(wire, target);
        }

        [Test]
        public void WithCategorySkipsNearerOtherCategory()
        {
            // ブロックのドラッグ中は手前の電線を飛ばして奥のブロックを取る
            // During a block drag, skip the nearer wire and take the block behind it
            var wire = new FakeDeleteTarget { Category = DestructionCategories.ConnectionLine };
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(2f, wire), new(5f, block) };

            Assert.IsTrue(DeleteTargetHitSelector.TrySelect(hits, "default", out var target));
            Assert.AreSame(block, target);
        }

        [Test]
        public void WithoutCategoryFrontmostNonTargetOccludes()
        {
            // 最前面が削除対象でない物体なら奥は拾わない（従来の遮蔽規則）
            // A non-target frontmost hit occludes what is behind (existing occlusion rule)
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(1f, null), new(5f, block) };

            Assert.IsFalse(DeleteTargetHitSelector.TrySelect(hits, null, out _));
        }

        [Test]
        public void EmptyHitsSelectNothing()
        {
            // ヒット0件は対象なし
            // Zero hits select nothing
            Assert.IsFalse(DeleteTargetHitSelector.TrySelect(new List<DeleteTargetHit>(), null, out _));
            Assert.IsFalse(DeleteTargetHitSelector.TrySelect(new List<DeleteTargetHit>(), "default", out _));
        }
    }
}
```

`DragDeleteSelectionCategoryTest.cs` に追加:

```csharp
        [Test]
        public void SessionCategoryIsFixedByFirstTargetAndClearedOnNewDrag()
        {
            // 最初の対象でセッションカテゴリーが固定され、新しいドラッグで解除される
            // The first target fixes the session category and a new drag clears it
            var selection = CreateSelection();
            selection.BeginDrag();
            Assert.IsNull(selection.SessionCategory);

            selection.TryAddTarget(new FakeDeleteTarget { Removable = true, Category = "connectionLine" }, out _);
            Assert.AreEqual("connectionLine", selection.SessionCategory);

            selection.BeginDrag();
            Assert.IsNull(selection.SessionCategory);
        }
```

（`CreateSelection()` は Task C4 で導入するテスト内ヘルパー。C3 を C4 より先に実装する場合は `new DragDeleteSelection(new BuildOperationHistory())` を直接書き、C4 で置換する。）

- [ ] **Step 2: 実装を書く**

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
    ///     ヒット列から削除対象を選ぶ。カテゴリー未指定は最前面、指定時はそのカテゴリーの最前面
    ///     Picks a delete target from hits: frontmost when no category is required, frontmost of that category otherwise
    /// </summary>
    public static class DeleteTargetHitSelector
    {
        public static bool TrySelect(IReadOnlyList<DeleteTargetHit> hits, string requiredCategory, out IDeleteTarget target)
        {
            target = null;
            var bestDistance = float.MaxValue;
            var found = false;

            foreach (var hit in hits)
            {
                if (bestDistance <= hit.Distance) continue;

                // カテゴリー指定時は非対象・別カテゴリーを貫通する
                // With a required category, pass through non-targets and other categories
                if (requiredCategory != null && (hit.Target == null || hit.Target.GetDestructionCategory() != requiredCategory)) continue;

                bestDistance = hit.Distance;
                target = hit.Target;
                found = true;
            }

            // 未指定で最前面が非対象なら遮蔽として何も選ばない
            // Without a category, a non-target frontmost hit occludes everything
            return found && target != null;
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
            var camera = Camera.main;
            if (camera == null) return 0;

            // 照準座標はAimPointProviderで視点モードに応じて一元解決する
            // The aim point is resolved centrally by AimPointProvider per view mode
            var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());

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

`TryGetFrontmostSolidHit` は先頭のカメラ取得〜`RaycastNonAlloc` 呼び出しを `var hitCount = RaycastAimAll(layerMask, maxDistance, out var hits);` に置換し、ループ内の `HitBuffer[index]` を `hits[index]` にする（`#region Internal` は不要になるので削除）。`QueryTriggerInteraction.Collide` は `m_QueriesHitTriggers: 1`（`ProjectSettings/DynamicsManager.asset:15`）の現状と同じ挙動を明示するだけで、既存呼び出しの結果は変わらない。

`DeleteTargetRaycaster.cs`（`C/Client.Game/InGame/Control/DeleteTargetRaycaster.cs`）:

```csharp
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using UnityEngine;

namespace Client.Game.InGame.Control
{
    /// <summary>
    ///     削除ツールの照準解決。ブロック層と接続線層を1本のレイで見て、カテゴリー条件付きで対象を選ぶ
    ///     Delete-tool aim resolution: one ray over block and connection-line layers, picking a target under an optional category
    /// </summary>
    public static class DeleteTargetRaycaster
    {
        private const float RayDistance = 100f;
        private static readonly List<DeleteTargetHit> HitCandidates = new();

        public static bool TryGetCursorOnDeleteTarget(string requiredCategory, out IDeleteTarget target)
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

            return DeleteTargetHitSelector.TrySelect(HitCandidates, requiredCategory, out target);

            #region Internal

            // 接続線のコライダーは線本体の子、ブロック・レール・車両は従来どおり自身か子に対象を持つ
            // Connection-line colliders sit under the line, while blocks/rails/cars keep the target on self or children as before
            static IDeleteTarget ResolveTarget(Collider collider)
            {
                if (collider.gameObject.layer == LayerConst.ConnectionLineLayer) return collider.GetComponentInParent<IDeleteTarget>();
                return collider.gameObject.GetComponentInChildren<IDeleteTarget>();
            }

            #endregion
        }
    }
}
```

`DragDeleteSelection.cs`: `private string _sessionCategory;` を `public string SessionCategory { get; private set; }` に畳み、ファイル内の `_sessionCategory` 参照（BeginDrag・TryAddTarget・IsCategoryCompatible・CancelSelection・CommitDelete）をすべて `SessionCategory` に置換する。コメントは「最初に選択したブロックの破壊カテゴリーをセッションのカテゴリーとして固定する（未選択時はnull）。照準のカテゴリー条件にも使う」へ更新。

`DeleteObjectService.Update` の33行目を置換:

```csharp
            // カーソル下の削除対象を取得する。ドラッグ中は固定カテゴリーの最前面、それ以外は最前面
            // Resolve the hovered target: frontmost of the fixed category while dragging, plain frontmost otherwise
            var requiredCategory = _isDragging ? _selection.SessionCategory : null;
            DeleteTargetRaycaster.TryGetCursorOnDeleteTarget(requiredCategory, out var hovered);
```

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.UIState\.(DeleteTargetHitSelectorTest|DragDeleteSelection)"`
Expected: ErrorCount 0 / PASS（既存 DragDeleteSelection 系も含め全件）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete moorestech_client/Assets/Scripts/Client.Tests/UIState/DeleteTargetHitSelectorTest.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteSelectionCategoryTest.cs
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
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailObjectIdCodec.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailEdgeClassifier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/RemoveOperationRecord.cs`（全面書き換え。Task B2 が72行目に入れた `RecordedOnly` 送信は `VanillaRemovalRestoreSender.PlaceBlocks` へ移る）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/IBuildOperationRecord.cs`（引数型を `IBlockOccupancyQuery` へ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/PlaceOperationRecord.cs`（`UndoAsync` 引数型の追従のみ。BlockRemove は従来どおり `ClientContext` 経由）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildUndoService.cs:18,48`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectDataStore.cs:16,137`（`IBlockOccupancyQuery` 実装）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/IDeleteTarget.cs`（`CollectRemovedObjects` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs`（`CollectRemovedObjects`。付随線は Task C5）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/DeleteTargetRail.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs:199-210`（canonical 計算を RailObjectIdCodec へ移す）
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
- Consumes: Task C2 `ConnectionLineDeleteTarget`（FromId/ToId/ConnectToolGuid/Kind）、`ConnectionLineKind`。Task B2 `Server.Protocol.PacketResponse.BlockPlacementWiring.RecordedOnly` / `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo>, BlockPlacementWiring)`、Task B3 `VanillaApiSendOnly.ConnectRailByDestination(ConnectionDestination, ConnectionDestination, Guid)`（同方向に接続済みなら無課金で成功）
- Produces:
  - `public interface IRemovedObject { object RestoreKey { get; } void AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy); void SendConnectionRestore(IRemovalRestoreSender sender); }`
  - `public interface IRemovalRestoreSender { void PlaceBlocks(List<PlaceInfo> placeInfos); void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid); void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid); void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid); }`
  - `public interface IBlockOccupancyQuery { bool IsOverlapPositionInfo(BlockPositionInfo target); }`
  - `RemovedBlock.From(BlockGameObject block)`、`new RemovedConnectionLine(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB, Guid connectToolGuid)`、`RemovedRail.TryCreate(RailGraphClientCache cache, int canonicalFrom, int canonicalTo, out RemovedRail removed)`
  - `IDeleteTarget.CollectRemovedObjects(ICollection<IRemovedObject> removed)`
  - `RemoveOperationRecord.CreateFrom(IReadOnlyList<IDeleteTarget> targets, IRemovalRestoreSender sender)`、`bool HasRemovedObjects`
  - `DragDeleteSelection(BuildOperationHistory history, IRemovalRestoreSender restoreSender)`、`DeleteObjectService(BuildOperationHistory, IMouseCursorTooltip, IRemovalRestoreSender)`、`DeleteObjectState(IRemovalRestoreSender restoreSender, UiStateCameraPolicyService, BuildOperationHistory, BuildUndoService, PlacementTargetPickService, RightShortPressInputService, IMouseCursorTooltip)`
  - `RailObjectIdCodec.SelectCanonicalPair(int fromNodeId, int toNodeId)` / `ComputeRailObjectId(int, int)` / `Decode(ulong railObjectId)`（public static）
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

            CollectionAssert.AreEqual(new[] { $"chain:{Vector3Int.zero}-{new Vector3Int(3, 0, 0)}:{guid}" }, sender.Sent);
        }

        [Test]
        public void TargetsWithoutRemovedObjectsYieldEmptyRecord()
        {
            // 列車のように何も記録しない対象だけなら履歴に積まない
            // Only targets recording nothing (like trains) produce no history entry
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

            Assert.IsTrue(RemovedRail.TryCreate(cache, 0, 2, out var removed));
            removed.SendConnectionRestore(sender);

            CollectionAssert.AreEqual(new[] { $"rail:{new Vector3Int(0, 0, 0)}-{new Vector3Int(10, 0, 0)}:{railType}" }, sender.Sent);
        }

        [Test]
        public void EdgeWithEmptyRailTypeIsNotRecorded()
        {
            // 駅内部・駅隣接の自動レール（種類Empty）は記録しない（駅の再設置で自動的に戻る）
            // Station-internal / station-adjacent auto rails (Empty type) are not recorded (station re-placement restores them)
            var cache = CreateTwoPierCache(Guid.Empty);
            Assert.IsFalse(RemovedRail.TryCreate(cache, 0, 2, out _));
        }

        [Test]
        public void UnsyncedNodeIsNotRecorded()
        {
            // 未同期のノードを指す区間は記録しない
            // An edge pointing at an unsynced node is not recorded
            Assert.IsFalse(RemovedRail.TryCreate(RailGraphClientCache.CreateForEditorTest(), 0, 2, out _));
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
        // CollectRemovedObjectsで返す撤去物（未設定なら何も記録しない）
        // Removed objects reported by CollectRemovedObjects (records nothing when empty)
        public readonly List<IRemovedObject> RemovedObjects = new();

        public void CollectRemovedObjects(ICollection<IRemovedObject> removed)
        {
            foreach (var removedObject in RemovedObjects) removed.Add(removedObject);
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

        // ブロック相: 再設置すべきセルを積む（ブロック以外は何もしない）
        // Block phase: append the cell to re-place (non-blocks do nothing)
        void AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy);

        // 接続相: ブロック再設置の送信後に線の引き直しを送る（ブロックは何もしない）
        // Connection phase: send the line restore after the block re-place has been sent (blocks do nothing)
        void SendConnectionRestore(IRemovalRestoreSender sender);
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

        public void AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            // 占有中のセルは再設置しない（撤去失敗・他者設置セルを除外）。理由はログへ残す
            // Skip occupied cells (failed removals or rebuilt cells) and log why
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(_blockId).BlockSize;
            if (occupancy.IsOverlapPositionInfo(new BlockPositionInfo(_position, _direction, blockSize)))
            {
                Debug.LogWarning($"[RemovalRestore] skip re-place: footprint occupied at {_position}");
                return;
            }

            placeInfos.Add(new PlaceInfo
            {
                Position = _position,
                Direction = _direction,
                VerticalDirection = ToVerticalDirection(_direction),
                BlockId = _blockId,
                Placeable = true,
            });
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

        public void AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
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

        // canonical化済みの区間から作る。駅内部・種類Empty（駅隣接の自動レール等）・未同期ノードは記録しない
        // Build from a canonical edge; station-internal, Empty-typed (e.g. station-adjacent auto rails) and unsynced edges are not recorded
        public static bool TryCreate(RailGraphClientCache cache, int canonicalFrom, int canonicalTo, out RemovedRail removed)
        {
            removed = null;
            if (!cache.TryGetNode(canonicalFrom, out var fromNode) || !cache.TryGetNode(canonicalTo, out var toNode)) return false;
            if (RailEdgeClassifier.IsStationInternalEdge(fromNode, toNode)) return false;
            if (!cache.TryGetRailType(canonicalFrom, canonicalTo, out var railTypeGuid) || railTypeGuid == Guid.Empty) return false;

            removed = new RemovedRail(fromNode.ConnectionDestination, toNode.ConnectionDestination, railTypeGuid);
            return true;
        }

        public object RestoreKey => (_from, _to);

        public void AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
            sender.ConnectRail(_from, _to, _railTypeGuid);
        }
    }
}
```

（`IRailNode.ConnectionDestination` は `S/Game.Train/RailGraph/IRailNode.cs:14` に実在。）

`VanillaRemovalRestoreSender.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Train.SaveLoad;
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
            ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos, BlockPlacementWiring.RecordedOnly);
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
        private readonly IRemovalRestoreSender _sender;

        private RemoveOperationRecord(List<IRemovedObject> removedObjects, IRemovalRestoreSender sender)
        {
            _removedObjects = removedObjects;
            _sender = sender;
        }

        // 空バッチをPushしないためのガード
        // Guard against pushing an empty batch
        public bool HasRemovedObjects => 0 < _removedObjects.Count;

        // 撤去直前の各対象から撤去物を集め、論理キーで重複排除する
        // Collect removed objects from every target right before removal, deduped by logical key
        public static RemoveOperationRecord CreateFrom(IReadOnlyList<IDeleteTarget> targets, IRemovalRestoreSender sender)
        {
            var collected = new List<IRemovedObject>();
            foreach (var target in targets) target.CollectRemovedObjects(collected);

            var seenKeys = new HashSet<object>();
            var unique = new List<IRemovedObject>();
            foreach (var removedObject in collected)
            {
                if (seenKeys.Add(removedObject.RestoreKey)) unique.Add(removedObject);
            }
            return new RemoveOperationRecord(unique, sender);
        }

        public UniTask UndoAsync(IBlockOccupancyQuery occupancy)
        {
            // ブロック相: 空いているセルを1バッチで再設置する
            // Block phase: re-place free cells in one batch
            var placeInfos = new List<PlaceInfo>();
            foreach (var removedObject in _removedObjects) removedObject.AppendBlockRestore(placeInfos, occupancy);
            if (placeInfos.Count != 0) _sender.PlaceBlocks(placeInfos);

            // 接続相: 再設置の後に線を引き直す（サーバーFIFOで再設置が先に適用される）
            // Connection phase: re-draw lines after the re-place (server FIFO applies the re-place first)
            foreach (var removedObject in _removedObjects) removedObject.SendConnectionRestore(_sender);
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
        void CollectRemovedObjects(ICollection<IRemovedObject> removed);
```

`BlockGameObjectChild.cs` に追加（付随線の収集は Task C5 で追記）:

```csharp
        public void CollectRemovedObjects(ICollection<IRemovedObject> removed)
        {
            removed.Add(RemovedBlock.From(BlockGameObject));
        }
```

`TrainCarEntityChildrenObject.cs` に追加:

```csharp
        // 車両の撤去はUndo対象外（ADR 0076の範囲外）なので何も積まない
        // Train car removal is outside undo (out of ADR 0076 scope), so nothing is appended
        public void CollectRemovedObjects(ICollection<IRemovedObject> removed)
        {
        }
```

`ConnectionLineDeleteTarget.cs` に追加:

```csharp
        public void CollectRemovedObjects(ICollection<IRemovedObject> removed)
        {
            // 端点ブロックが解決できない線は復元先を持てないためログを残して積まない
            // A line whose endpoints cannot be resolved has no restore target, so log and skip
            var store = ClientDIContext.BlockGameObjectDataStore;
            if (!store.TryGetBlockGameObject(FromId, out var fromBlock) || !store.TryGetBlockGameObject(ToId, out var toBlock))
            {
                Debug.LogWarning($"[RemovalRestore] skip line record: endpoint block not found from={FromId} to={ToId}");
                return;
            }
            removed.Add(new RemovedConnectionLine(Kind, fromBlock.BlockPosInfo.OriginalPos, toBlock.BlockPosInfo.OriginalPos, ConnectToolGuid));
        }
```

`RailObjectIdCodec.cs`（`TrainRailObjectManager` の private static 2本を移設し、manager 側は呼ぶだけにする。manager は 211 行→200 行未満へ縮む）:

```csharp
namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール描画1本を表すID（canonicalな区間ペア）の符号化・復号
    ///     Encode/decode the id of one drawn rail (canonical edge pair)
    /// </summary>
    public static class RailObjectIdCodec
    {
        // A→B と逆向き (B^1)→(A^1) のうち小さい起点の方を正とする
        // Of A->B and its opposite (B^1)->(A^1), the one with the smaller start is canonical
        public static (int canonicalFrom, int canonicalTo) SelectCanonicalPair(int fromNodeId, int toNodeId)
        {
            var alternateFrom = toNodeId ^ 1;
            var alternateTo = fromNodeId ^ 1;
            return fromNodeId <= alternateFrom ? (fromNodeId, toNodeId) : (alternateFrom, alternateTo);
        }

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
        public void CollectRemovedObjects(ICollection<IRemovedObject> removed)
        {
            // レールIDはcanonical区間なのでそのまま記録へ写す
            // The rail object id is already canonical, so map it straight into a record
            var (fromId, toId) = RailObjectIdCodec.Decode(RailObjectIdCarrier.GetRailObjectId());
            if (!RemovedRail.TryCreate(_railGraphClientCache, fromId, toId, out var removedRail))
            {
                Debug.LogWarning($"[RemovalRestore] skip rail record: edge {fromId}->{toId} is not restorable");
                return;
            }
            removed.Add(removedRail);
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
            SessionCategory = null;

            // Ctrl+Z用のUndo履歴を記録（空バッチはPushしない）
            // Record the undo history for Ctrl+Z (skip empty batches)
            if (record.HasRemovedObjects) _buildOperationHistory.Push(record);
        }
```

`DeleteObjectService` のコンストラクタを `DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender)` にして `new DragDeleteSelection(buildOperationHistory, restoreSender)`。`DeleteObjectState` の第1引数 `RailGraphClientCache cache`（未使用）を `IRemovalRestoreSender restoreSender` に置き換え、`new DeleteObjectService(buildOperationHistory, tooltip, restoreSender)`。`using Client.Game.InGame.Train.RailGraph;` が不要になれば外す。

`MainGameInteractionRegistration.cs` に `builder.Register<VanillaRemovalRestoreSender>(Lifetime.Singleton).As<IRemovalRestoreSender>();`

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(BuildUndo|UIState)\."`
Expected: ErrorCount 0 / PASS（RemoveOperationRecordTest 3件・RemovedRailTest 3件＋既存 BuildOperationHistoryTest・DragDelete 系・UIState 系）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo moorestech_client/Assets/Scripts/Client.Game/InGame/Block moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/Object/TrainCarEntityChildrenObject.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ConnectionLine/ConnectionLineDeleteTarget.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(client): 撤去Undoをブロック・接続線・レール共通のIRemovedObjectで記録し復元する"
```

---

### Task 12 (C5): ブロック撤去に巻き込まれる線・レールの赤表示と Undo 記録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/AttachedRailEdgeEnumerator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/BlockAttachedConnectionResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs`（`TryGetRailChain` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/BezierRailChain.cs:14`（`IRemovePreviewable` 実装宣言のみ。既存 `SetRemovePreviewing`/`ResetMaterial` で満たす）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectChild.cs`（SetRemovePreviewing / ResetMaterial / CollectRemovedObjects）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/AttachedRailEdgeEnumeratorTest.cs`

**Interfaces:**
- Consumes: Task C2 `ConnectionLineRegistry.GetLinesAttachedTo`、`IRemovePreviewable`、Task C4 `RemovedRail.TryCreate`、`RailObjectIdCodec`、`IRemovedObject`
- Produces:
  - `public static class AttachedRailEdgeEnumerator { static void Collect(RailGraphClientCache cache, Vector3Int blockPosition, ICollection<(int canonicalFrom, int canonicalTo)> edges) }`
  - `public class BlockAttachedConnectionResolver` — ctor `(ConnectionLineRegistry registry, RailGraphClientCache railCache)`、`void SetRemovePreviewing(BlockGameObject block)`、`void ResetMaterial(BlockGameObject block)`、`void CollectRemovedConnections(BlockGameObject block, ICollection<IRemovedObject> removed)`
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
                    var canonical = RailObjectIdCodec.SelectCanonicalPair(nodeId, targetId);
                    if (seen.Add(canonical)) edges.Add(canonical);
                }
            }
        }
    }
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

        // ブロックごとに赤くした付随物を覚え、外すときは同じ集合を戻す（その間に増減した線を取り違えない）
        // Remember what each block reddened so reset restores the same set (lines changed meanwhile are not confused)
        private readonly Dictionary<BlockGameObject, List<IRemovePreviewable>> _previewing = new();
        private readonly List<(int canonicalFrom, int canonicalTo)> _edgeBuffer = new();

        public BlockAttachedConnectionResolver(ConnectionLineRegistry registry, RailGraphClientCache railCache)
        {
            _registry = registry;
            _railCache = railCache;
        }

        public void SetRemovePreviewing(BlockGameObject block)
        {
            if (_previewing.ContainsKey(block)) return;

            var previewed = new List<IRemovePreviewable>();
            foreach (var line in _registry.GetLinesAttachedTo(block.BlockInstanceId)) previewed.Add(line);
            foreach (var edge in CollectRailEdges(block))
            {
                if (TrainRailObjectManager.Instance.TryGetRailChain(RailObjectIdCodec.ComputeRailObjectId(edge.canonicalFrom, edge.canonicalTo), out var chain)) previewed.Add(chain);
            }

            foreach (var target in previewed) target.SetRemovePreviewing();
            _previewing[block] = previewed;
        }

        public void ResetMaterial(BlockGameObject block)
        {
            if (!_previewing.Remove(block, out var previewed)) return;
            foreach (var target in previewed)
            {
                // 赤表示中に線が切れて破棄済みのことがある
                // A line may have been destroyed while red
                if (target is UnityEngine.Object unityObject && unityObject == null) continue;
                target.ResetMaterial();
            }
        }

        public void CollectRemovedConnections(BlockGameObject block, ICollection<IRemovedObject> removed)
        {
            foreach (var line in _registry.GetLinesAttachedTo(block.BlockInstanceId)) line.CollectRemovedObjects(removed);
            foreach (var edge in CollectRailEdges(block))
            {
                if (RemovedRail.TryCreate(_railCache, edge.canonicalFrom, edge.canonicalTo, out var removedRail)) removed.Add(removedRail);
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

`TrainRailObjectManager.cs` に追加（`SelectCanonicalPair`/`ComputeRailObjectId` は Task C4 で `RailObjectIdCodec` へ移設済み）:

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

`BezierRailChain` の宣言を `public class BezierRailChain : MonoBehaviour, IRemovePreviewable` にする（既存 public `SetRemovePreviewing()`/`ResetMaterial()` が満たす。ファイルは既に 200 行超の既存ファイルで、本タスクは宣言1行のみの変更）。

`BlockGameObjectChild.cs` の3メソッドを置換:

```csharp
        public void SetRemovePreviewing()
        {
            // ブロック本体と、一緒に消える接続線・レールを赤くする
            // Redden the block and the lines/rails that vanish with it
            BlockGameObject.SetRemovePreviewing();
            ClientDIContext.BlockAttachedConnectionResolver.SetRemovePreviewing(BlockGameObject);
        }

        public void ResetMaterial()
        {
            BlockGameObject.ResetMaterial();
            ClientDIContext.BlockAttachedConnectionResolver.ResetMaterial(BlockGameObject);
        }

        public void CollectRemovedObjects(ICollection<IRemovedObject> removed)
        {
            // ブロック本体と、巻き込みで消える接続線・レールを同じ撤去物として積む
            // Append the block and the cascaded lines/rails as removed objects of the same kind
            removed.Add(RemovedBlock.From(BlockGameObject));
            ClientDIContext.BlockAttachedConnectionResolver.CollectRemovedConnections(BlockGameObject, removed);
        }
```

`ClientDIContext.cs` に `public static BlockAttachedConnectionResolver BlockAttachedConnectionResolver { get; private set; }` を追加しコンストラクタで Resolve。`MainGameInteractionRegistration.cs` に `builder.Register<BlockAttachedConnectionResolver>(Lifetime.Singleton);`（`ConnectionLineRegistry` と `RailGraphClientCache` は登録済み）。

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.AttachedRailEdgeEnumeratorTest"`
Expected: ErrorCount 0 / 2 PASS

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
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:36-51`（`TryGetCursorOnElectricWire` → `TryGetCursorOnConnectionLine`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/PlacementTargetPickService.cs:36-53`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConnectTool/ConnectionLinePickResolverTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/ConnectionLinePickResolver.cs`

**Interfaces:**
- Consumes: Task C2 `ConnectionLineDeleteTarget.ConnectToolGuid`、Task C1 `LayerConst.ConnectionLineOnlyLayerMask`
- Produces:
  - `BlockClickDetectUtil.TryGetCursorOnConnectionLine(out ConnectionLineDeleteTarget line)`（旧 `TryGetCursorOnElectricWire` は削除）
  - `public static class ConnectionLinePickResolver { static bool TryResolvePickTarget(Guid lineConnectToolGuid, IGameUnlockStateData unlockState, out IPlacementTarget target) }`

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
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
            var unlockState = new FakeUnlockState(guid, true);

            Assert.IsTrue(ConnectionLinePickResolver.TryResolvePickTarget(guid, unlockState, out var target));
            Assert.AreEqual(guid, ((ConnectToolPlacementTarget)target).ConnectToolGuid);
        }

        [Test]
        public void LockedLineToolFailsPick()
        {
            // 未解放の種類はスポイト自体を不成立にする
            // A locked tool makes the eyedropper fail
            var guid = Guid.NewGuid();
            Assert.IsFalse(ConnectionLinePickResolver.TryResolvePickTarget(guid, new FakeUnlockState(guid, false), out _));
        }
    }
}
```

（`FakeUnlockState` は `IGameUnlockStateData` のテスト実装。既存テストに同等の fake があればそれを使い（`grep -rn ": IGameUnlockStateData" moorestech_client/Assets/Scripts/Client.Tests`）、無ければこのテストファイル内に `ConnectToolUnlockStateInfos` だけを返す private class として定義する。）

- [ ] **Step 2: 実装を書く**

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
        public static bool TryResolvePickTarget(Guid lineConnectToolGuid, IGameUnlockStateData unlockState, out IPlacementTarget target)
        {
            target = null;

            // 未解放の種類はスポイト自体を不成立にする（Guid.Emptyを下流へ流さない）
            // A locked tool fails the eyedropper itself (never pass Guid.Empty downstream)
            if (!unlockState.ConnectToolUnlockStateInfos.TryGetValue(lineConnectToolGuid, out var info) || !info.IsUnlocked) return false;

            target = new ConnectToolPlacementTarget(lineConnectToolGuid);
            return true;
        }
    }
}
```

`BlockClickDetectUtil.cs` の `TryGetCursorOnElectricWire` を置換:

```csharp
        public static bool TryGetCursorOnConnectionLine(out ConnectionLineDeleteTarget line)
        {
            line = null;

            var camera = Camera.main;
            if (camera == null) return false;

            // 接続線は専用レイヤのため単独Raycastで判定する
            // Connection lines live on a dedicated layer, so probe them with their own raycast
            var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.ConnectionLineOnlyLayerMask)) return false;

            // コライダーは線本体の子のため親を辿る
            // Colliders are children of the line, so climb to the parent
            line = hit.collider.GetComponentInParent<ConnectionLineDeleteTarget>();
            return line != null;
        }
```

（`using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;` を `...StateProcessor.ConnectionLine;` へ。）

`PlacementTargetPickService.cs`: コメント「電線→列車→ブロックの順」を「接続線→列車→ブロックの順（線は細いため最優先で拾う）」へ、`TryPickElectricWire` を次に置換し、呼び出し側も `TryPickConnectionLine` に改名:

```csharp
            bool TryPickConnectionLine(out IPlacementTarget target)
            {
                target = null;
                if (!BlockClickDetectUtil.TryGetCursorOnConnectionLine(out var line)) return false;

                // 線を引いた種類そのものをスポイトする（未解放なら不成立）
                // Pick the exact tool the line was drawn with (fails when locked)
                return ConnectionLinePickResolver.TryResolvePickTarget(line.ConnectToolGuid, _gameUnlockStateData, out target);
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
Expected: ErrorCount 0 / PASS。加えて `grep -rn "TryGetCursorOnElectricWire\|RequestSender.Disconnect" moorestech_client/Assets/Scripts` が0件。

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
- チェーン接続の有無と種類: `IGearChainPole.ContainsChainConnection(BlockInstanceId)`（既存）と Task A1 が新設する `IGearChainPole.TryGetChainConnectionRecord(BlockInstanceId, out GearChainConnectionRecord)`（`.ConnectToolGuid`）。接続数メンバーは使わない
- 電線の種類: Task A1 の `IElectricWireConnector.WireConnections[partner].Record.ConnectToolGuid`
- レール接続と種類: `RailComponent.FrontNode/BackNode`（`train-rail-connect-via-ui.cs:65-71` と同じ）、`IRailNode.ConnectedNodes`、`RailNode.NodeId`、`p.ServerService<RailGraphDatastore>().TryGetRailSegmentType(int, int, out Guid)`（`S/Game.Train/RailGraph/RailGraphDatastore.cs:111`、DI 登録 `ServerContextRegistration.cs:100`）
- 接続ツールGuidの名前引き: `MasterHolder.ConnectToolMaster.All`（`PlaytestHotbarOps.cs:79-85` と同じ）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs`

**Interfaces:**
- Consumes: Task C1〜C6、Task A1（Record.ConnectToolGuid / TryGetChainConnectionRecord）、A4（チェーン切断）、B1（橋脚撤去のレール返却）、B2（RecordedOnly）、B3（座標同定のレール接続）
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

## 判断記録（ADR）

設計ADR: `docs/adr/0076-delete-tool-cuts-connection-lines-and-undo-restores-removed-objects.md`（設計セッションの裁定と出所はADR側が正。ここでは書き換えない）。裁定記録: `.decisions/2026-10-04-*.md`（10件）。

planning 中に新たに生じた判断:

| # | 判断 | 出所 |
|---|---|---|
| P1 | 電線・チェーンは接続ごとに ConnectToolGuid を保存・同期する（セーブv4） | ユーザー裁定 2026-10-04 選択「接続ごとに種類を保存」 |
| P2 | 旧セーブの線は線種別ごとの唯一の種類を固定定数で割り当てる（素材を見ない・失敗しない） | ユーザー裁定 2026-10-04 選択「線種別ごとの唯一の種類を割り当て」 |
| P3 | 橋脚・駅撤去で付いていたレールも返却する | ユーザー裁定 2026-10-04 選択「撤去でレールも返却する」 |
| P4 | 接続1本の型を `ElectricWireConnectionCost`/`GearChainConnectionCost` から `...Record` へ改名し種類を持たせる。`Empty` は廃止 | agent判断（名前を実処理と一致させる規約） |
| P5 | 移行ステップは構造の壊れたJSON（配列・オブジェクトでない）だけ `Failed`。素通しすると未変換のまま版4が刻まれ `Required.Always` で落ちるため | agent判断（save-migration スキル「ステップは冪等・JObjectだけで書く」） |
| P6 | チェーン切断の理由は `out string` でなく `GearChainDisconnectFailureReason` enum（webui の通知id網羅テストが補間idを enum 展開でしか分類できないため） | agent判断（`notificationServerIdCoverage.test.ts:23-33`） |
| P7 | `IGearChainPole.TryGetChainConnectionRecord` を追加（返却の InsertionCheck を除去前に行うため） | agent判断（電線 TryDisconnect の順序の前例） |
| P8 | 200行超の既存ファイルを分割: `GearChainPoleComponent.cs`→`GearChainConnectionSet.cs`、`ElectricWireSystemUtil.cs`→`ElectricWireDisconnectUtil.cs`、`TrainRailObjectManager.cs`→`RailObjectIdCodec.cs` | agent判断（AGENTS.md 200行規約） |
| P9 | 撤去の返却が入らないときの拒否理由を `RemoveBlockFailureReason.InventoryFull`／`ui.delete.inventoryFull` として新設（従来は Unknown で理由が出ない） | agent判断（無音縮退禁止・レール返却で満杯拒否が増えるため） |
| P10 | 設置要求に `BlockPlacementWiring { AutoConnect, RecordedOnly }` を持たせ、Undoだけ RecordedOnly。駅隣接のレール自動接続は抑止しない | agent判断（ADR 0076 の「Undo再設置では自動接続を止める」裁定の実装形。対象は電線自動接続のみと裁定文が述べるため） |
| P11 | 座標同定のレール接続 `va:railConnectByDestination` を新設し応答なし（null）。接続処理は `RailConnectionEditService.ExecuteEdit` を呼ぶだけで複製しない。既接続は無消費で何もしない | agent判断（再設置でNodeGuidが振り直される事実 `RailComponent.cs:44-45`、Id再利用 `RailNodeIdAllocator`） |
| P12 | `Guid.Empty` のレール区間（駅内部・駅隣接自動）は返却もUndo記録もしない | agent判断（`RailNode.ConnectNode` が Empty で張る事実 `RailNode.cs:121-124`） |
| P13 | 線の引き直しは電線も SendOnly（`SendOnly.ConnectElectricWire` 新設）。Response経路は電線ツールの世代管理と競合するため | agent判断（前例 `VanillaApiSendOnly.ConnectGearChain`） |
| P14 | Undo記録は `IRemovedObject` の2相メソッド（`AppendBlockRestore`／`SendConnectionRestore`）。電線とチェーンは `RemovedConnectionLine(ConnectionLineKind, …)` 1クラス | agent判断（ブロックは1回の PlaceBlock にまとめる既存形 `RemoveOperationRecord.cs:72`、削除対象と対称） |
| P15 | 線の削除対象キーはコンポーネント自身（線は小さいId側に1本だけ生成される `ConnectionLineViewBase.cs:81-84`）。Undoの重複排除は kind＋正規化座標 | agent判断 |
| P16 | Unityレイヤー "ElectricWire" を "ConnectionLine" へ `uloop execute-dynamic-code` で改名（チェーンも同じレイヤーへ） | agent判断（名前を実処理と一致させる・テキスト編集禁止規約） |
| P17 | 照準の解決は純関数 `DeleteTargetHitSelector` と薄い `DeleteTargetRaycaster` に分け、`BlockClickDetectUtil` には共有レイキャストだけ足す | agent判断（テスト可能性） |
| P18 | `BlockAttachedConnectionResolver` は `ClientDIContext` の static 経由で `BlockGameObjectChild` から使う | agent判断（前例 `ClientDIContext.BlockGameObjectDataStore` を `ElectricWireLineViewElement` が static 参照） |
| P19 | `IBuildOperationRecord.UndoAsync` の引数を `IBlockOccupancyQuery` へ、`DeleteObjectState` の未使用 `RailGraphClientCache` 引数を `IRemovalRestoreSender` へ置換（テスト3か所更新） | agent判断（テスト可能性・未使用引数の削除） |
| P21 | Undoの部分失敗はできた分だけ戻し、失敗分は通知して履歴は消費する | ユーザー裁定 2026-10-05 選択「できた分だけ戻し残りは通知」 |
| P20 | 通し検証は unityプレイ録画テスト（EditModeInPlayingTest でなく。既存 `build-undo-ctrl-z.cs` と `train-rail-connect-via-ui.cs` の操作を再利用）。駅撤去のUndoは録画対象外とし Task 15 で起票 | agent判断（入力・カメラ・Undoキーを含むランタイム挙動のため） |
