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
| 新プロトコル/同期 | 新設前に「既存同期情報から導出できないこと」を示す。作る場合は creating-server-protocol スキル | design-question-triage の導出可能テスト |
| スキーマ変更 | edit-schema スキル必須（csc.rsp / _CompileRequester / JSON更新箇所） | edit-schema スキル |

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
