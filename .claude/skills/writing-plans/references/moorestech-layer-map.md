# moorestech 層マップと機構規約（Phase 2 突合表）

## アセンブリ層マップ

| アセンブリ | 責務 | 置いてよいもの | 置いてはいけないもの |
|---|---|---|---|
| Core.Master | マスタデータの生ロード・保持・ID⇔GUID解決 | Loader呼び出し、`XxxMaster`（生成物の保持と辞書化）、汎用Validate | **ドメイン固有の解釈ロジック**（型名・メソッド名にプレイヤー/インベントリ/研究等のドメイン語が現れるもの） |
| Core.Item / Core.Inventory / Core.* | ドメイン非依存の基盤演算（アイテムスタック計算、汎用インベントリサービス等） | どのドメインからも同じ意味で使える演算 | 特定ドメインの状態・ルール |
| Game.Xxx.Interface | ドメインXxxの公開契約 | interface、定数、JsonObject、**マスタ生成物を読むだけの static util** | 実装クラス、可変状態 |
| Game.Xxx | ドメインXxxの実装・実行時状態の所有 | DataStore/Service実装、UniRx Subject | 他ドメインの状態直接操作 |
| Game.Action / Game.UnlockState | gameAction実行と永続アンロック状態 | GameActionExecutorのcase追加、Unlock系状態 | — |
| Game.SaveLoad | セーブJSON集約・ロード順制御 | WorldSaveAllInfoV1へのフィールド追加、ロード順の決定 | ドメインロジック |
| Server.Protocol / Server.Event | 通信（MessagePack） | プロトコル、イベントパケット | 永続化への流用（MessagePackはセーブ禁止） |
| Client.* | 表示・入力・ローカル状態 | View、ローカルインベントリ | サーバー状態の直接変更 |

## 機構規約表

| 機構 | プロジェクト標準 | 前例（引用先） |
|---|---|---|
| イベント/通知 | UniRx `Subject<T>` を private 保持、`IObservable<T>` で公開。C# `event Action` 禁止 | csharp-event-pattern スキル、`Game.UnlockState/GameUnlockStateDatastoreController.cs` |
| UniRx を新アセンブリで使う | asmdef の references に `"UniRx"` を追加 | `Game.UnlockState/Game.UnlockState.asmdef` |
| 永続化フォーマット | Newtonsoft JSON（key→value）。MessagePack禁止 | `PlayerInventorySaveJsonObject.cs` |
| 永続化キー | GUID（ItemGuid等）。揮発int（ItemId/BlockId）禁止。マスタ由来値（容量・スロット数等）は保存せずロード時にマスタから導出 | `ItemStackSaveJsonObject.cs` |
| グローバル最小状態の永続化 | レベル・カウント等の単一値は `WorldSaveAllInfoV1` に**素のフィールド（int等）で追加してよい**（JsonObjectで包むのはドメイン内に複数値がある場合）。冪等再実行で導出可能でも、ロード順都合での明示保存は正 | `WorldSaveAllInfoV1.cs` の `inventorySlotLevel`（承認済み設計） |
| マスタ生成物へのアクセス | `MasterHolder.XxxMaster` の public readonly フィールド/メソッドを**読むだけ**。`Mooresmaster.Model.*` の手動作成・変更禁止 | `MasterHolder.cs`、AGENTS.md |
| マスタ値のドメイン解釈 | 該当ドメインの `Game.Xxx.Interface` に static util | `Game.PlayerInventory.Interface/PlayerInventorySlotLevelMasterUtil.cs`（承認済み設計・実装前。実装完了後に実在確認すること） |
| 永続強化・アンロック | 冪等（unlock/set-max）。increment禁止（ロード時にclearedActions再実行されるため） | `ResearchDataStore.LoadResearchData` |
| DI 登録 | `MoorestechServerDIContainerGenerator.cs` に AddSingleton | 同ファイル内の既存登録 |
| 新プロトコル/同期 | 新設前に「既存同期情報から導出できないこと」を示す。作る場合は creating-server-protocol スキル。**導出＝既存イベントが同じ情報をそのまま運んでいる場合のみ**。別ドメインの応答（研究完了・チャレンジ等）をパースして状態を推測合成するのは導出ではなく間接導出Applierであり禁止 | design-question-triage の導出可能テスト。反例: PR988で「新規プロトコル・イベント・ハンドシェイク拡張は作らない」とspecに書き、Applier 2種がレビューで全廃された |
| サーバー可変状態のクライアント同期 | **3点セット**: ①`Server.Event/EventReceive/*EventPacket`（DataStoreの`IObservable`購読+DI登録+eager init）②初期データ（`InitialHandshakeProtocol`同梱 or `va:get*`全量）③クライアント`SubscribeEventResponse`ハンドラ（`IInitializable`）。specで「イベントを作らない」と決めるのは新規パターンであり裁定事項 | `UnlockedEventPacket`+`GetGameUnlockStateProtocol`+`ClientGameUnlockStateDatastore`、`ItemStackLevelUnlockEventPacket`+`InitialHandshakeProtocol.ItemStackLevels`+`ItemStackLevelEventHandler` |
| 可変DataStoreのアクセス面 | 読み取り用 `I*Lookup`（`public static Instance`公開可）と変更用 `I*Mutation`/`I*Unlocker`/`I*Controller`（DI注入のみ）に分離。staticに変更系を露出しない | `IItemStackLevelLookup`/`IItemStackLevelUnlocker`（`ItemStackLevelDataStore`）、`ITrainUnitLookupDatastore`/`ITrainUnitMutationDatastore` |
| スキーマ新フィールド | 必須（`optional: true`原則禁止）＋YAML`default`＋全JSON一括更新（server/client TestMod・EditModeInPlayingTestMod・`../moorestech_master`）。`?? Default`フォールバック・ローダーでのJSON挿入は禁止 | `blocks.yml`/`ref/gearConsumption.yml` の `idlePowerRate`（PR978で optional+フォールバック44箇所が必須化+JSON更新に全面修正された） |
| 汎用基盤コンポーネントへの状態伝達 | 基盤（基底コンポーネント・共通サービス・Template）はドメイン語彙・`Func<bool>`述語を持たず、具体側が `SetHoge(値)` でプッシュする。状態変化の検知はUniRx購読か操作直後プッシュ（`Update()`毎tickポーリング禁止） | `GearEnergyTransformer.SetTorqueRequestRate`＋`VanillaGearMachineComponent`の`OnChangeBlockState.Subscribe`（PR978で`_isActive`/`AlwaysActive`注入が全面reverted） |
| スキーマ変更 | edit-schema スキル必須（csc.rsp / _CompileRequester / JSON更新箇所） | edit-schema スキル |
| UIステート連動コンポーネント | ステートの挙動に**参加する**もの（カメラ・カーソル・設置等の制御）はステートから明示駆動（`OnEnter`/`GetNextUpdate`/`OnExit` から呼ぶ）。`UIStateControl.OnStateChanged` 購読は**表示専用オブザーバ**に限る | 駆動: `PlaceBlockState`→`PlaceSystemStateController.ManualUpdate()/Disable()`、購読（表示のみ）: `DisplayEnergizedRange` |
| 共有選択モデル（`PlacementSelection`等）を書き換える入力サービス | UIステートから**毎フレーム`ManualUpdate()`型で駆動**し、入力検知（クリック等）と対象検知（レイキャスト・解決）をサービス内部に閉じる（ステート側に入力判定や`TryXxx()` bool戻り値の分岐を書かない）。反映は共有選択モデルへの書き込み**一本**にし、遷移が必要ならステートが選択モデルの変化から導出する。選択モデルを迂回する直接セッター経路（各システムへの`SetXxx`直呼び）の新設は、選択モデルの拡張（フィールド追加＋変化検知比較への追加）で足りない理由を示せた場合のみ | 駆動同族: `PlaceSystemStateController.ManualUpdate()`、`BuildViewModeController.ManualUpdate()`。2026-07-08スポイト設計で「Try-bool型＋ステート側クリック判定＋向きの`SetPlaceDirection`直呼び」を提示し、ユーザーに「UIステートから毎フレーム駆動するサービス＋`PlacementSelection`一本化」へ修正された実績 |

## 前例を探す Grep 例

```bash
# 同種のstoreの配置を見る
grep -rln "DataStore : I" moorestech_server/Assets/Scripts/Game.*/

# イベントの標準形を見る
grep -rn "Subject<" moorestech_server/Assets/Scripts/Game.UnlockState/

# asmdef参照の前例
grep -l "UniRx" moorestech_server/Assets/Scripts/*/*.asmdef

# gameAction追加の全変更点
grep -rn "GameActionTypeConst" moorestech_server/Assets/Scripts --include="*.cs" -l
```

## 検査でよく引っかかる箇所（過去の指摘由来）

- `ItemMaster` / `BlockMaster` 等へのメソッド追加 → ほぼ常に誤り。ドメイン層の util へ
- `PlayerInventoryConst` 系「定数クラス」への状態・マスタ参照の混入 → 定数と純関数のみ許可
- 「差分最小」を理由にした既存クラスへのちょい足し → 判定質問を通す
- セーブJSONへのマスタ由来値（スロット数・容量）の保存 → レベル・GUID等の最小状態のみ保存
- 制御コンポーネントのライフサイクルを `OnStateChanged` 購読で作る → ステート駆動へ（購読は表示オブザーバ限定）。発火タイミング（`OnEnter`より後）への回避策が設計に現れたら機構選定ミスのサイン
- 既存コンポーネントを置換・吸収する設計で駆動機構を無言変更 → 置換対象の機構が第一の前例。変えるなら新規パターンとして注目点へ
- 入力→選択反映サービスを`TryXxx()` bool戻り値で作り、ステート側にクリック判定・遷移分岐を書く → 毎フレーム`ManualUpdate`駆動＋選択モデル書き込み一本へ。`TryGet`型bool戻り値の前例（`GameScreenSubInventoryInteractService`等）は「遷移コンテキストを**生成するだけで共有状態を書かない**」判定サービス限定であり、共有選択モデルを書き換えるサービスの前例にならない
- 「同一対象の再選択で`IsSelectionChanged`が発火しない」を理由に選択モデル外の直接セッターを新設 → 選択モデルの比較対象へフィールドを足せば解決する。直接セッター新設の理由にならない
- 「既存JSONを壊さないため」のoptional化・`?? Default`・ローダーでのプリフィル → 後方互換はプロジェクト方針として考慮不要（AGENTS.md）。必須化して全JSONを一括更新するのが正規手順
- 基底コンポーネントに`Func<bool> isActive`等の述語を注入して「汎用のまま拡張」 → 語彙（アイドル/採掘中/加工中）が基盤に漏れた時点で越境。具体側の`SetHoge`プッシュへ反転する
- クライアントが他イベント（研究完了等）に追従して別状態を推測する`*Applier` → 専用イベントパケット＋初期データの3点セットへ
- 判別子enum＋種別ごとに一部しか使わないフィールドを並置する共用体struct → 判別子＋ペイロード型 or static factory（前例: `RailConnectionEditRequest`・`BlueprintRequest`）
- `Server.Protocol/PacketResponse/`直下への`*Dto.cs`新設 → IPacketResponse実装のみの階層。DTOは別階層かプロトコルクラス内ネスト
- 既存共通サービス（`SimpleGearService`等）にあるロジックをコンポーネント内へ再実装 → 委譲プロパティ（`=> _gearService.CurrentRpm`）で保持し固有分だけ残す
