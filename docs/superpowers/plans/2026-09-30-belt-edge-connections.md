# Belt edge connections Implementation Plan

> **For the controller session:** subagent-driven-developmentで実装し、最終レビューとコンパイル・テストを実施する。ユーザー指示によりPR作成・pushは行わない。

**Goal:** ワールドのベルト変更に対して、関係するedgeの有向接続だけを正しく差分更新する。
**Architecture:** inventoryのinstance contextが幾何と接続優先を所有する。Worldは変更対象componentの汎用mutation契約を呼び、contextのGetOverrideが古い接続と新しい接続を解く。既存generic connectorは指示された接続を適用する。
**Tech Stack:** C#, Unity6000.3.8f1, NUnit, 既存Game.Block/Game.World。

## Requirements

- R1: edge共有4voxelの接触判定→両側上優先→方向・形状・山谷判定の順。承認済み7状態2401配置と全件一致する。
- R2: 1edgeは有向接続0/1本。非接触は空と等価、選択後に下側fallbackをしない。
- R3: Flatは4底面edge、Up/Downは低端・高端の2edgeだけを再評価する。ワールド全件走査をしない。
- R4: before/afterのsource block/self port/target block/target portを比較し、NG→OK、OK→NG、OK→別OK、同一OKを正しく適用する。変更対象以外のsource辞書も更新する。
- R5: 設置/撤去/ロードをWorldBlockDatastoreの変更境界から駆動。constructor中のselfワールド参照をしない。Removeの旧通知順は維持する。
- R6: 機械↔ベルト、機械同士、gear接続/プレビューの既存判定を維持する。新ルールの対象pairだけlegacy追加を抑制する。
- R7: GetOverrideを中心とするPR1134のcontext責務を採用。既存TConnectJudgeはpair判定、instance contextは周囲を含む接続変更という異なる責務を持つ。
- R8: 平行の側面入力は実際の入出力port方向で評価。ベルトの輸送正面方向とedgeでの入出力方向を混同しない。
- R9: 新ブランチcodex/belt-edge-connections、base14c9f4b。稼働中checkoutは変更せず、PR/pushなし。全作業をコミットする。
- R10: segment本体・GPU・速度・フィルタ・save形式・外部master repoは変更しない。

## Global Constraints

AGENTS.md準拠。Func/partial/default引数/手書きmeta禁止。各C#200行以下、追加先10コードファイル以下、日英コメント。汎用基盤にbelt語彙を漏らさない。実装はZ:\moorestech-belt-edgesのみ。検証は同worktreeのmoorestech_clientのみ。Libraryとprivate AssetsはC:\Users\5080\Desktop\belt-edge-validationに複製済みでjunction、共有元への書込は禁止。bd CLIはこの環境に存在しないため進捗は.superpowers/sdd/progress.mdへ記録。

## Files / 配置表

全serverパスはmoorestech_server/Assets/Scripts/配下。

| ファイル/配置 | 責務・前例 |
|---|---|
| Game.Block.Interface/Component/WorldMutation/IBlockWorldMutationParticipant.cs | 変更前captureと変更後適用の汎用契約。IPostBlockLoadのWorld→component呼出しが前例 |
| Game.Block/Component/ConnectionContext/IConnectorContext.cs | generic connectorがoverride対象pairを判別し変更snapshot取得する契約、既存3引数ctor用default実装 |
| Game.Block/Component/BlockConnectorComponent.cs | contextの注入、汎用mutation契約の実装、legacy対象pair保持、差分適用のinternal API |
| Game.Block/Component/ConnectionContext/ConnectorPairJudge.cs | 既存static geometry/shape/judge実装を移設。既存TryJudgeConnect public facadeは維持 |
| Game.Block/Blocks/BeltConveyor/Connection/BeltEdge.cs | edgeを整数座標と水平軸で正規化、共有4voxel列挙 |
| 同Connection/BeltEdgeEndpoint.cs | 実在するベルト形状/向きから触れる4/2edgeと端点高さを導出 |
| 同Connection/BeltEdgeConnectionResolver.cs | 接触→上側→入出力port全候補→山谷→形状を一箇所で解決 |
| 同Connection/BeltInventoryConnectionContext.cs | 自分の配置・slope・portsでInitialize、GetOverrideから前後差分を返す。targetの実ベルト判定を所有 |
| 同Connection/BeltEdgeConnectionMutation.cs | snapshotされたedgeとbefore接続、ApplyAfterMutationでafter取得・古い削除→新規追加 |
| Game.World/DataStore/WorldBlockDatastore.cs | 登録前capture/登録後apply、撤去通知前capture/削除後apply |
| Game.World/DataStore/WorldBlockConnectionMutation.cs | 汎用component列挙。必要なら200行制限のためsave処理をWorldBlockSaveDataへ移設可 |
| Game.Block/Factory/BlockTemplate/Transport/VanillaBeltConveyorTemplate.cs | ベルトinventory connectorだけcontext付で生成 |
| 同VanillaGearBeltConveyorTemplate.cs | gear connectorは従来、inventoryだけcontext付 |
| 同VanillaFilterSplitterTemplate.cs | Flat4edgeのcontextを注入。複数生portを評価し、分配/フィルタ挙動は維持 |
| Tests/UnitTest/Game/BeltConnection/BeltEdgePatternTest.cs | 承認済み期待結果の全探索・回転対称テスト |
| 同BeltEdgeMutationTest.cs | 設置/撤去/第三者差替/再設置/load/identityテスト |
| 同BeltConnectionCompatibilityTest.cs | 機械接続と側面入力・gear regression |
| Tests.Module/TestMod/ForUnitTestModBlockId.csおよび対応blocks.json | 必要なslope test blockが無い場合だけ追加。schema/本番master変更不要 |

追加の型を分ける必要がある場合は同Connection配下を責務別分割し200行/10filesを守る。以上はagent実装配置でありユーザー裁定ではない。

## Interfaces / Produces / Consumes

- World公開契約: `IBlockWorldMutationParticipant.CaptureWorldMutation()` → `IBlockWorldMutation`、後者は `void ApplyAfterMutation()`。空の処理は明示的なno-op実装を使う（nullと別modeの二重表現を作らない）。
- connectorの既存3引数ctorは維持し、明示contextの4引数overloadに委譲する。インスタンスcontextをstaticに置かない。`IConnectorContext<TTarget>.HandlesOverride(IBlock targetBlock)`とsnapshot生成を使う。コンテキストへのself初期化はworld lookup不要なposition/port/slope/connectorの実体を渡す。
- `BeltInventoryConnectionContext.GetOverride()`は各edgeの望ましい有向接続を返す。空は接続リスト0件、成立は必要なsource/port/target/portが全部揃った接続値1件。未初期化値やmode用nullを返さない。
- `BlockConnectorComponent`のinternal接続適用APIはsourceで古いtarget削除/new ConnectedInfo設定。辞書インスタンスは保持。既存のtarget key制約を維持し、一致接続は書き換えない。比較にport GUID/connector identityを含める。
- 対象beltの分類は具体inventory context/descriptorを正として集約する。FilterSplitterはIItemCollectableBeltConveyorを実装しないのでこのinterfaceだけで分類しない。通常/gear/filterの3templateへ注入する。
- 任意のWorld更新で全参加者を巡回しない。変更されるブロック自身のcomponentからsnapshotを取り、その4/2edgeの旧pairと新pairだけを操作。
- 設置factory生成時のlegacy追加をcontextで抑制するためcontextはconstructor初期購読より前に渡す。変更多発時の再入など未使用の汎用機構は追加しない。
- 撤去callbackは旧componentがDestroy済みでも使えるimmutable配置とsnapshotを保持。破棄済みsource辞書へ追加しない。既存removeイベントが旧リンクを先に消してもremoveは冪等に扱う。

## データフローと機構選択

Worldの登録/削除操作 → 対象componentのcapture/apply → inventory context GetOverride → 既存ConnectedTargets → 既存搬送。
新contextは接続状態の書き手。既存place/remove購読でbelt-beltまで追加して後から訂正すると、削除前通知で第三者の復帰判定を解けず誤接続も混在する。このため対象belt pairのみcontextへ委譲し、その他の既存購読は存続させる。直接駆動はユーザーが提起し今回承認した方向。

## 操作の死活表

| 操作 | 結果 |
|---|---|
| 普通/傾斜ベルト設置撤去 | 対象edge差分が即時反映 |
| 既存機械/チェストとベルト | 従来の位置/shape判定で接続 |
| 機械同士 | 従来の接続と搬送を維持 |
| gear接続/preview | TryJudgeConnect facadeと既存judgeで維持 |
| save/load | 既存save形式、順次登録時のedge差分で最終配置へ収束 |
| 回転/張替 | 既存remove+add経路で旧新edge双方を更新 |

## Task 1: edge判定とワールド差分更新を一体で実装

**Files:** 上のFiles表。**受入:** R1〜R10。

- [ ] 既存BlockConnectorComponent/TryJudgeConnect、両belt template、WorldBlockDatastore、BlockPlaceToConnectionBlockTest/OrderedShapeCandidateConnectionTest/BlockConnectionSaveLoadTestを読む。PR1134のcontext要点を上記制約と照合。
- [ ] 上表の契約とedgeresolverを実装。正規化edgeと両側4voxelはX/Z回転に対し一意。Flat=全4底辺、slope=搬送軸両端に各入出力高さ。上側選択はportの方向評価より先。
- [ ] 選択pairの生output/inputを全て評価し（既存CalculateConnectPosToConnectorの同一targetPos上書きに依存しない）、複数コネクタの先頭がshape不適合でも後続適合を拾う。端点の物理高さ一致でedge接触を確定し、portの水平入出力方向を評価する。既存direction.yは旧隣接セル指定なので、物理接触済みFlat→lower Down等を通常ベルトだけ拒否する根拠にしない。通常とgearで同じ7状態期待結果になることを検証する。
- [ ] snapshotの旧新接続差分を全remove→全addの順に反映。source入替・target同一port変更を漏らさない。既存legacy追加をbelt pairだけ抑制しmachines/gear経路を温存。
- [ ] Worldの設置はfactory後/登録前capture、component辞書登録後apply。撤去は通知前capture、Destroyと全辞書削除後apply。既存通知順を移動しない。Loadはprivate TryAddBlock経由を含める。
- [ ] 承認済みZ:\belt-edge-patterns-20260930\cases.json/rules.pyから期待結果をテスト資産として固定（実装と同じ計算式をテストで再実装しない）。2401配置×4水平回転、鏡映と非接触空同値、25/256既存サブセットを検証する。
- [ ] 実worldを用いた設置順差/撤去後復帰/無効上側抑制/OK→別OK/二つのworld連続生成/load順/機械両方向/gear/側面入力を検証。新規testIDはForUnitTestModBlockId正規経路。
- [ ] creating-server-tests/uloop-compile/uloop-run-testsスキルに従い実行。新serverファイルのimportが必要なら隔離clientだけ再起動。`uloop compile --project-path Z:\moorestech-belt-edges\moorestech_client`、`uloop run-tests --project-path ... --filter-type regex --filter-value 'BeltEdge|BeltConnection|BlockPlaceToConnectionBlock|OrderedShapeCandidateConnection|ConnectorShapeConnection|BlockConnectionSaveLoad|BeltConveyor' --timeout-seconds 1500`。エラー/失敗を直し通過を確認する。
- [ ] `git diff --check`、scope/200行/禁止構文確認、`Task 1: implement edge-based belt connections`としてコミット。Unity起動時生成された関係ないtracked自動変更は巻き込まない。手書きmeta不可、自動生成metaは対象追加と共にcommit。

## 最終検証・レビュー（controllerが実行）

- task diffをfresh reviewerでspec/quality確認し重要所見を修正。
- moores-code-reviewでブランチ全体をレビュー。Workflow toolが無いので正規fallback、利用不能系統は事実として報告する。
- UI/segment搬送は今回変更しないため、操作境界と接続辞書を実World+Unity NUnitで検証。録画プレイテストは今回の接続ルール全探索に比べ追加の判定根拠にならないというagent判断。
- 最終commitとclean状態、稼働中checkoutのbranch維持、PR未作成を報告する。

## 判断記録（ADR）

D1〜D11の正本はdocs/adr/0072-belt-edge-connections.mdと同日.decisions。ユーザー承認済み要件はR1〜R6/R9/R10。具象API/配置/既存generic引数を残したinstance context導入はagent前提（機械側の波及を抑え、PR1134のGetOverride責務を満たす）。現在branch基点はユーザー指定であり、writing-plans既定origin/masterより優先する。通常/高速/gear/分岐器・側面入力へ共通edge規則を適用する。出所: ユーザー裁定2026-09-30「全種類・分岐器にも共通で適用」（D11）。
