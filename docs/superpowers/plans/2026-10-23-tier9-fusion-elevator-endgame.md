# Tier9 終盤（核融合・反物質・宇宙エレベーター・終端研究） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Tier9 の終盤（核融合炉・重水素抽出装置・反物質製造装置・反物質爆弾・終端研究・宇宙エレベーター拠点とケーブルプール・グラフェン工程・金属3Dプリンタ・研究ツリー整理）を、マスタと実装の両方で遊べる状態にする。

**Architecture:** 核融合・反物質・グラフェン・金属3Dプリンタは既存の ElectricGenerator / ElectricMachine とマスタだけで作り、新コードは発電機の稼働表示（状態詳細）に限る。宇宙エレベーターは新しいブロック種別 `SpaceElevatorBase` と、ワールド単位のケーブルプール（新アセンブリ `Game.SpaceElevator`）で作る。プールは S2 の軌道到着サービス（`Game.Orbit`）の到着通知を購読してケーブルを数え、拠点は「接続」ボタン（専用プロトコル）でプールから1エレベーター分を引く。昇降機は衛星を1つずつ電力で運び、昇り切ったら軌道到着サービスへ渡して返ってきた成果を保留経由で出力口へ流す。

**Tech Stack:** Unity / C#（moorestech_server, moorestech_client）、MessagePack、UniRx、Microsoft.Extensions.DependencyInjection、React + zod（moorestech_web/webui）、マスタ JSON（moorestech_master）、Python3（マスタ整合ツール）

## Requirements

設計 ADR: `docs/adr/0089-space-elevator-cable-pool-and-per-base-connection.md`、`docs/adr/0090-fusion-net-power-from-fuel-extraction-cost.md`。裁定: `.decisions/2026-10-23-*`（S3 分は本 plan 末尾『判断記録（ADR）』に一覧）。用語: `CONTEXT.md` の「宇宙エレベーター」「核融合」「終盤」。

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R1 | 液体「重水素」を作る | fluids.json に `重水素` があり、ローカライズ行がある |
| R2 | 重水素抽出装置（ElectricMachine）が水から重水素を作る。大電力・モジュールスロットあり | 水を入れて電力を与えると重水素タンクが増える（マスタ読込スモーク＋シミュレーション） |
| R3 | 核融合炉（ElectricGenerator）が重水素を燃やして大電力を出す。モジュールスロット・点火手順は無い | 重水素を入れると出力が出る。炉1基の差し引き電力（炉出力−必要台数の抽出装置の消費）がモジュールなしでガソリン発電機 3〜5 台分（シミュレーションで検査） |
| R4 | 発電機（ElectricGenerator）を開くと稼働状態（稼働率・残燃料・燃料タンク）が汎用UIに出る | サーバーテストで状態詳細が出る。webui で `generator-section` と `generator-fluid-slots` が出る。核融合炉は `openable: true` |
| R5 | 反物質製造装置（ElectricMachine）が重水素と核融合炉級の電力で反物質を作る | マスタ読込スモーク・シミュレーションで反物質1機の消費電力が炉の差し引き電力（モジュールなし）の3倍以上 |
| R6 | 反物質爆弾を既存の電気汎用工作装置で作る | machineRecipes に 反物質＋ICチップLv1＋鋼鉄板＋コンピュータ → 反物質爆弾 がある |
| R7 | 終端研究（反物質爆弾を規定量消費する仮の研究）で終わる。クリア状態・エンディングは作らない | research.json に終端研究ノード（consume 反物質爆弾 10、clearedActions 空、前提 反物質爆弾）がある |
| R8 | グラフェンケーブルを既存機械の短い直線で作り、使用済み銅板は電気炉で銅板に戻す | 石炭→高純度炭素（電気炉）→グラフェンシート＋使用済み銅板（電気炉）→グラフェンケーブル（電気汎用工作装置）、使用済み銅板→銅板（電気炉）の4レシピがある |
| R9 | 金属3Dプリンタを ElectricMachine ブロックにし、ICチップ等からコンピュータ・制御モータを1工程で作る。研究「金属超精密加工」で解放 | 旧アイテム「金属3Dプリンタ」が消え、ブロックとレシピ2本があり、金属超精密加工が unlockBlock する |
| R10 | 研究「フィルター分岐器」を消し、金属超精密加工の前提を張り替える | ノードが無く、金属超精密加工の前提が `機械オーバークロック` だけになる。progression_check が詰みを増やさない |
| R11 | 研究「機械オーバークロック」は S1 が速度モジュール研究として作る。S3 は触らない | S3 の差分で当該ノードが変わっていない |
| R12 | 「軌道エレベーターの建設」から発射台・筐体・燃料の解放を外し（a）、計算データを消費し（b）、前提にロケット技術を足す（c） | research.json の当該ノードが (a)(b)(c) を満たす |
| R13 | グラフェンケーブルは軌道到着の積荷表に 1便1000本・成果なしで載り、スタック上限 1000 のテーブルを持つ | items.json の `orbitalPayloads` に行があり、グラフェンケーブルの `stackLevelTableGuid` が [1000] のテーブルを指す |
| R14 | ワールドに1つのケーブルプールが、ケーブルの軌道到着で増える | サーバーテスト: `Arrive(cable, N)` でプール残量が N 増える。ケーブル以外の到着では増えない |
| R15 | 拠点ごとの「接続」でプールから1エレベーター分を引く。足りなければ接続できず理由を UI に出す | サーバーテスト: 足りれば Connected・残量が減る／足りなければ InsufficientCable・残量不変。プロトコルテスト・webui の理由表示 |
| R16 | 接続済み拠点を撤去すると1エレベーター分がプールへ戻る。未接続の撤去は影響しない | サーバーテスト（ユーザー原文の 200→2基接続→+50→1基撤去で150→3基目接続可 のシナリオ） |
| R17 | 昇降機は1拠点1台で GPU 衛星を1つずつ運び、昇降中だけ電力を使い、不足すると遅くなる | サーバーテスト: 昇降時間・電力不足で遅くなる・未接続では昇らない |
| R18 | 昇り切ったら軌道到着サービスへ渡し、返ってきた成果を保留経由で出力口へ流す。保留が残る間は次の衛星を昇らせず理由を出す | サーバーテスト: 成果が出力口に出る／出力満杯で保留・次が昇らない・空くと流れる |
| R19 | 保留と昇降中の衛星はセーブに入り、撤去時はプレイヤーへ返る | サーバーテスト: セーブ往復で復元／撤去でプレイヤーインベントリへ返る |
| R20 | プール残量はワールドセーブに入る。セーブ形式の変更は版上げ＋移行ステップ＋テストを同梱する | `WorldSaveAllInfo.CurrentVersion` 7→8、`SaveMigrationStepV7ToV8`、移行テストと往復テスト |
| R21 | 拠点は簡易モデルで、接続でケーブルが現れ、昇降中は昇降機がケーブルを昇る | unityプレイ録画テストで目視 |
| R22 | 拠点 UI に接続ボタン・プール残量／1エレベーター分・昇降の進み・待ちの理由を出す | webui unit/e2e テストと録画 |
| R23 | 新しい値はシミュレーションで目標に合わせる（S3 の配分 5〜11 時間、全体 15〜25 時間は S1 の裁定） | `tools/balance/tier9_endgame_balance.py` が目標の範囲を検査して exit 0 |

**やらないこと（スコープ境界）:**
- エンディング・クリア状態・最終ボタン・スキット（裁定で不採用）
- 炉本体へのモジュール・点火・再起動の手順（裁定で不採用）
- 専用のオーバークロック機能、確率振り分けのフィルターコンベア（裁定で不採用）
- モジュールの種類・値・レシピ・研究コスト、チップのレベル、GPU の性能、Tier9 研究コストの最終値（S1 の担当。S3 は仮値を置き「S1 待ち」と記す）
- ロケット発射台・GPU衛星・計算データ・軌道到着サービス本体・初打ち上げ状態（S2 の担当。S3 は取り込んで使うだけ）
- クリーンルーム・チップ抽選・モジュールのコード（S1 の担当。触らない）

## Global Constraints

- 作業場所: moorestech worktree `$HOME/hermes-agent/data/worktrees/moorestech/tier9-fusion-elevator-endgame`（branch `feat/tier9-fusion-elevator-endgame`）。マスタ: `$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame`（branch `feat/tier9-fusion-elevator-endgame`、Task 1 で作る）。最初に `pwd` で確かめる
- 両 repo とも PR 作成まで。moorestech はマージしない。force-push しない（S2 と相互に取り込むため）
- 本 repo の `.moorestech-external-revisions.json` の `moorestech_master` ピンは、マスタブランチの push 済みコミットを指す。ピン変更は `git add .moorestech-external-revisions.json` 単独でコミットし、`git show HEAD:.moorestech-external-revisions.json` で値を確かめる（Editor が書き戻すため）
- Editor のマスタは既定で `../../moorestech_master`（共有の main clone）を読む。S3 のマスタで動かすときは、録画テストは `run-scenario.sh` の第3引数に `$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame/server_v8` を渡し、スモークは `MoorestechServerDIContainerOptions` に同パスを渡す。main clone の HEAD を動かさない（S1/S2 と共有）
- Unity は自分の worktree の Editor だけを使う（`moores-wt status` で所有者確認）。自分で起動したプロセスは PID を控えて `kill <PID>` で止める（`pkill -f` 禁止）
- バックグラウンドのポーリングで待たない。待つときは前景で `sleep` を挟む。区切り（各タスクの最後）ごとにコミットする
- .cs を変えたら `uloop compile --project-path ./moorestech_client`（ErrorCount 0）。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。Domain Reload 中エラーは 45 秒待って再試行
- 1ファイル200行未満。1ディレクトリ10ファイルまで。partial・`Func<>`・デフォルト引数・単純 getter/setter プロパティ（`{ get; private set; }` は可）・イベント用 `Action` は禁止（UniRx を使う）。try-catch は外部境界のみ
- コメントは主要処理の 3〜10 行ごとに日本語1行＋英語1行。`#region Internal` はメソッド内ローカル関数だけ
- fail-closed（拒否・保留・無視）する分岐は理由を `Debug.Log`/`LogWarning` に出す。無音の縮退は禁止
- サーバー時間は `GameUpdater` の tick だけ（`SecondsToTicks`/`TicksToSeconds`）
- .meta を手で作らない。Prefab・Scene・ScriptableObject・Addressables の .asset をテキスト編集しない（`uloop execute-dynamic-code` 経由は可）。Library を消さない
- マスタ JSON を変えたら moorestech_master で `python3 tools/research_sync/sync_machine_recipe_unlocks.py`（書き戻し）→ `--check`（exit 0）→ `python3 tools/research_sync/progression_check.py`（詰みを増やさない）→ `python3 scripts/check_shipped_data_contracts.py`（exit 0）
- スキーマ（`VanillaSchema/*.yml`）を変えるときは edit-schema スキルに従う（`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を変えて再生成、test mod・`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/`・`mooresmaster/mooresmaster.SandBox/` の JSON も更新）
- セーブ形式を変える PR は moorestech-save-migration スキルが正本（版上げ・移行ステップ・3箇所登録・移行テスト・実ロード検証）
- S1 の担当値（Tier9 研究コスト・チップ Lv・モジュール値）は仮値で置き、判断記録に「S1 待ち」と書く。S2 の担当値（計算データ量・GPU衛星の成果・ロケット建造時間）は S2 のマスタブランチの値を読む

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## 依存関係と順序

```
Task 1 マスタ: 核融合・反物質 ──┐
Task 2 マスタ: グラフェン・3Dプリンタ・研究整理 ──┤
Task 3 発電機の稼働表示（server/client/webui） ──┤（S2 非依存。先に全部やる）
Task 4 数値シミュレーション（核融合・反物質・グラフェン） ──┘
Task 5 S2 取り込みゲート＋S2 依存のマスタ（積荷行・スタック表・研究 (a)(b)(c)・buildMenu）
Task 6 スキーマ SpaceElevatorBase と拠点のマスタ
Task 7 ケーブルプールとワールドセーブ（版 7→8）
Task 8 拠点ブロック（インベントリ・昇降機・保留・撤去返却）と接続サービス
Task 9 接続プロトコル
Task 10 拠点の webui
Task 11 拠点の簡易モデルと昇降の見た目
Task 12 シミュレーションにエレベーターを足して値を確定
Task 13 unityプレイ録画テスト
Task 14 moores-code-review
Task 15 PR 作成（moorestech_master → moorestech の順）
```

Task 5 は S2 の `Game.Orbit` が push されていないと進めない。Task 5 Step 1 のゲート手順に従う。

## File Structure

| ファイル | 責務 | タスク |
|---|---|---|
| `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/{fluids,items,blocks,machineRecipes,research,buildMenu}.json` | 新液体・アイテム・ブロック・レシピ・研究・ビルドメニュー | 1,2,5,6 |
| `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv` | コンテンツ名の訳 | 1,2,5 |
| `moorestech_master/tools/balance/tier9_endgame_balance.py` | 差し引き電力・台数・所要時間の検査 | 4,12 |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorComponent.cs` | 発電機本体＋状態詳細の発火（200行未満へ圧縮） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorSaveJsonObject.cs`（新） | 発電機セーブ JSON（本体から移動） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorFuelService.cs` | 燃料管理＋状態用の読み取り口（200行未満へ分割） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorFuelSettingsBuilder.cs`（新） | 燃料設定辞書の構築（移動） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorFuelSaveConverter.cs`（新） | 燃料状態のセーブ書き出し・復元（移動） | 3 |
| `.../PowerGenerator/PowerGeneratorStateDetail.cs` | 秒値から作る ctor を追加 | 3 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectPrefabContainer.cs` | ElectricGenerator を CommonMachine 扱いから外す | 3 |
| `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx` | ElectricGenerator に液体行を出す | 3 |
| `VanillaSchema/blocks.yml` | `SpaceElevatorBase` の blockType と param | 6 |
| `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator/ISpaceElevatorBase.cs`（新） | プール・接続処理が拠点に触る面 | 7 |
| `moorestech_server/Assets/Scripts/Game.SpaceElevator/`（新 asmdef） | `SpaceElevatorCablePool`（残量・到着購読・セーブ）、`SpaceElevatorConnectionService`（接続・撤去返却・残量 push）、`SpaceElevatorConnectResult`、`Save/SpaceElevatorCablePoolSaveData` | 7,8 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/...` | WorldSaveAllInfo・保存・復元・移行・pruner | 7 |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/`（新） | 拠点のインベントリ・昇降機・電力・状態詳細・セーブ | 8 |
| `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/SpaceElevator/VanillaSpaceElevatorBaseTemplate.cs`（新） | 拠点の組み立て | 8 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs` | 返却情報を全コンポーネントから合算 | 8 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SpaceElevator/ConnectSpaceElevatorProtocol.cs`（新） | 接続リクエスト（`PacketResponse/` 直下は 50 ファイル超のためサブディレクトリ） | 9 |
| `moorestech_client/Assets/Scripts/Client.Network/API/Requests/BlockRequestApi.cs` | 接続 API | 10 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/SpaceElevator/`（新） | 拠点の DTO と DTO ビルダー | 10 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/SpaceElevator/SpaceElevatorConnectActionHandler.cs`（新） | `space_elevator.connect` | 10 |
| `moorestech_web/webui/src/bridge/contract/schemas/{spaceElevator/index.ts,fluidSlot.ts,beltThroughput.ts,inventory.ts}` | 受信スキーマ（inventory.ts を 200 行未満へ分割） | 10 |
| `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/`（新） | 拠点の固有部分（接続ボタン・プール・進み・待ちの理由） | 10 |
| `moorestech_web/webui/e2e/...` | registry fixture・mock host・拠点の e2e | 3,10 |
| `Localization/localization.csv` | 拠点 UI の文言 | 10 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/SpaceElevator/SpaceElevatorBaseStateChangeProcessor.cs`（新） | ケーブルの表示と昇降機の位置 | 11 |
| `moorestech_client/Assets/AddressableResources/Block/SpaceElevatorBase.prefab`（新、execute-dynamic-code で生成） | 簡易モデル | 11 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/tier9-endgame-fusion-and-elevator.cs`（新） | 録画シナリオ | 13 |

### 配置と前例

| 配置 | 前例（役割が同じもの） |
|---|---|
| ケーブルプール（ワールド単位の可変状態＋セーブ節）を独立アセンブリ `Game.SpaceElevator` に置く | `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`（ワールド単位の部屋状態を独立アセンブリで持ち、`WorldSaveAllInfo` の節へ保存） |
| プールが拠点へ残量を `SetCablePoolAmount(int)` で push し、拠点が状態変化を発火する | `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomMachineEffectService.cs`（`ICleanRoomMachine.SetCleanRoomEffect` へ push） |
| `ISpaceElevatorBase` を `Game.Block.Interface/Component/SpaceElevator/` に置く | `Game.Block.Interface/Component/CleanRoom/ICleanRoomMachine.cs` |
| プール・接続サービスを main コンテナの具象型 singleton＋`IBootInitializable` で起動時に生成・購読する | `moorestech_server/Assets/Scripts/Server.Event/EventReceive/ChangeBlockStateEventPacket.cs`（`IBootInitializable.Load` で購読を張る。`AddInitializableForwarding` が転送） |
| 撤去でのプール返却は `OnBlockRemoveEvent` 購読 | `CleanRoomDatastore` の ctor 購読（`OnBlockRemoveEvent` は `Destroy` より前に発火する: `WorldBlockDatastore.cs:38→43`） |
| 拠点の電力消費（昇降中だけ要求・不足で遅く） | `Game.Block/Blocks/Pump/ElectricPumpComponent.cs`＋`ElectricPumpProcessorComponent.cs`（供給を加算し Update で latch）、`Game.Block/Blocks/Util/MachineCurrentPowerToSubSecond.cs` |
| 拠点のインベントリ（開ける・ベルト入出力） | `Game.Block/Blocks/Chest/VanillaChestComponent.cs` |
| 拠点の状態詳細（MessagePack） | `Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorBlockStateDetail.cs` |
| 接続プロトコル | `Server.Protocol/PacketResponse/SetElectricToGearOutputModeProtocol.cs`（形）＋`RemoveBlockProtocol.cs` の拒否ログ（fail-closed のログ） |
| webui の拠点表示（専用 view を作らず、汎用 `SectionStackView` にデータの有無で固有部分を合成） | `moorestech_web/webui/src/features/blockInventory/details/GearToElectricSection.tsx`＋`views/SectionStackView.tsx` の `configByBlockType` |
| 拠点の action と DTO | `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/ElectricToGearActions.cs`／`Topics/BlockDetail/ElectricToGearDetailDtoBuilder.cs` |
| 簡易モデルの見た目更新 | `Client.Game/InGame/BlockSystem/StateProcessor/SteamEngineEffectStateChangeProcessor.cs`（プレハブに貼る `[SerializeField]` 付き processor） |
| 発電機の状態詳細 | `Game.Block/Blocks/Gear/FuelGearGeneratorComponent.cs`（`PowerGeneratorStateDetail` を出す） |

---

## 共通の GUID・参照表

既存（moorestech_master origin/master b2149a0）:

| 名前 | GUID |
|---|---|
| 核融合炉（ブロック、仮エントリ） | 01a162f1-ab04-76e2-94cf-dc713edeb63b |
| 宇宙エレベーター拠点（ブロック、仮エントリ） | 01a162aa-cec0-738c-93c5-f58adc87fa7f |
| ロケット発射台（ブロック、S2 担当） | 01a162b0-7769-7419-af24-bb35528b93d2 |
| 反物質爆弾（アイテム） | 01a162ab-07ba-73c1-9dd6-38e1e867663b |
| グラフェンケーブル / グラフェンシート / 使用済み銅板 / 高純度炭素 | 01a16290-f793-748b-a0b0-91d44be0d4ab / 01a16297-4724-73b9-82c3-2e77120be77d / 01a16297-6665-765d-8e19-96d357394c3e / 01a16297-a7f5-759b-91ba-be4021ca3bf2 |
| 金属3Dプリンタ（アイテム、削除対象） | 019e3af5-e750-71ae-ab09-2b98f3db9913 |
| 石炭 / 銅板 / 鋼鉄板 / 鉄のフレーム | f882df45-5651-4aec-b871-a9f885a5f75f / 019e0a92-1907-707a-8625-630d86a076ee / 019e3a98-cfe5-73eb-bf6a-a564400bdb4f / 231d2146-5e40-4e55-be27-22dda4175f47 |
| コンピュータ / 制御モータ / ICチップLv1 | 019f0ca4-a544-723e-8ff2-feb15f99c84e / 019f0ca5-a575-7625-a43e-f43e8471287e / f6dd7835-0845-4877-87d0-b719d037ae7d |
| 水（液体） | 9eae6979-d56a-4991-9107-b8161acec430 |
| 電気炉 / 電気汎用工作装置（ブロック） | 019e0a68-3705-75f7-a040-50fab3c7d81e / 019e1225-f49b-7283-8835-428d903852f1 |
| 研究: 機械オーバークロック / フィルター分岐器（削除） / 金属超精密加工 / 核融合炉 / 軌道エレベーターの建設 / 反物質爆弾 | 019e3ae2-5005-7439-b242-9b7b3191a2c3 / 019e3aab-32c6-7166-a184-d761153c2498 / 019e3af5-f2e6-76de-a80f-5e66e79fa35d / 019e3af0-297f-726e-8a02-0a8518124e6d / 01a16291-4094-7369-a87f-5dfe2197d25e / 019e3aef-a149-746b-8fad-159e0434cb51 |
| ベルト用コネクタ形状 | 65f063cd-3ae1-4b55-b39d-00e033b7ec98 |
| スタック表 Default | 9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa |

S2 のマスタブランチ（Task 5 で取り込む）: 研究「ロケット技術」01a167cf-af9d-7f31-817c-3069b4f6e499、計算データ 01a167cf-af88-74d3-9cd6-7d61b71052bb、GPU衛星Lv1〜Lv5 01a167cf-af8b-7894-8de6-593ee30289c0 / 01a167cf-af8e-7e95-b299-09eb712bee12 / 01a167cf-af91-7f62-93d1-e499b06f2398 / 01a167cf-af94-7e25-9654-825374c3923b / 01a167cf-af97-7f67-bf53-7833b8d17a15、ビルドメニュー「生産>宇宙」d2000000-0000-4000-8000-000200000005。

新規（本 plan で作る）:

| 名前 | GUID |
|---|---|
| 重水素（液体） | 01a167e9-21b2-7bb0-b6a3-0f17d349704e |
| 反物質（アイテム） | 01a167e9-21b5-7479-9060-995505f43137 |
| 重水素抽出装置 / 反物質製造装置 / 金属3Dプリンタ（ブロック） | 01a167e9-21b8-7db1-a289-99cdf7490fe9 / 01a167e9-21bb-76dd-9a83-0a249aa9f1fc / 01a167e9-21be-72cd-ae04-e5bbe12b95c2 |
| コネクタ: 炉の液体入口 / 抽出装置の水入口 / 抽出装置の重水素出口 / 反物質機の重水素入口 / 反物質機のアイテム出口 / 3Dプリンタ入口 / 3Dプリンタ出口 / 拠点入口 / 拠点出口 | 01a167e9-21c1-7ec0-9813-d30ce62e14c0 / 01a167e9-21c4-792a-a10b-e36304af4ddf / 01a167e9-21c7-7a2d-872c-c06b53e28700 / 01a167e9-21ca-738d-9bae-95fc4d736a88 / 01a167e9-21cd-7a18-b1df-eacf1fea87a6 / 01a167e9-21d0-749d-a485-ddc2943f0535 / 01a167e9-21d3-7e29-9915-33f9c849be17 / 01a167e9-21d6-7e9d-89c4-70dc3df73072 / 01a167e9-21d9-7b56-9484-4fb75fd18a37 |
| レシピ: 重水素 / 反物質 / 反物質爆弾 / 高純度炭素 / グラフェンシート / グラフェンケーブル / 銅板戻し / 3Dプリンタのコンピュータ / 3Dプリンタの制御モータ | 01a167e9-21dc-7eb1-a240-8345ed8fbd40 / 01a167e9-21df-7b2a-81e3-04be55cb89b0 / 01a167e9-21e2-7c4e-91be-8ad747466bde / 01a167e9-21e5-7b43-9414-15365405623b / 01a167e9-21e8-76b9-bf3f-b462360e3fa9 / 01a167e9-21eb-7e67-b6ee-1d6503d8af84 / 01a167e9-21ee-7339-b7b6-351ad1775730 / 01a167e9-21f0-7d5b-8ddc-18a841c01637 / 01a167e9-21f3-73f5-9418-35a676b8fa3f |
| 研究: 最終研究 | 01a167e9-21f6-7d26-a990-e7d6d78750d0 |
| スタック表: グラフェンケーブル [1000] | 01a167e9-21f9-70a8-be5e-08c2ba6895c9 |

テスト mod（`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`）に足す ID:

| 名前 | GUID |
|---|---|
| TestSpaceElevatorBase（ブロック） | 5e1ea7a0-0001-4000-8000-000000000001 |
| TestElevatorCable / TestElevatorSatellite / TestElevatorReward（アイテム） | 5e1ea7a0-0002-4000-8000-000000000001 / 5e1ea7a0-0002-4000-8000-000000000002 / 5e1ea7a0-0002-4000-8000-000000000003 |
| 拠点の入口・出口コネクタ | 5e1ea7a0-0003-4000-8000-000000000001 / 5e1ea7a0-0003-4000-8000-000000000002 |

---

### Task 1: マスタ — 重水素・核融合炉・重水素抽出装置・反物質・反物質製造装置・反物質爆弾レシピ

**Files:**
- Create（git worktree）: `$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/fluids.json`, `items.json`, `blocks.json`, `machineRecipes.json`, `research.json`（moorestech_master worktree 内）
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Create: `moorestech_client/Library`（コピー。git 管理外）

**Interfaces:**
- Consumes: なし
- Produces: 重水素 01a167e9-21b2-…、反物質 01a167e9-21b5-…、重水素抽出装置 01a167e9-21b8-…、反物質製造装置 01a167e9-21bb-… と各レシピ（Task 4 のシミュレーションが name で引く）

- [ ] **Step 1: 作業場所を用意する**

```bash
pwd   # $HOME/hermes-agent/data/worktrees/moorestech/tier9-fusion-elevator-endgame
git -C $HOME/hermes-agent/data/repos/moorestech_master fetch origin
git -C $HOME/hermes-agent/data/repos/moorestech_master worktree add -b feat/tier9-fusion-elevator-endgame $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame origin/master
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame log --oneline -1   # b2149a0 tire9基板 以降であること
moores-wt status   # Editor pid が空の worktree を Library のコピー元に選ぶ（候補: pr-apply）
cp -Rc $HOME/hermes-agent/data/worktrees/moorestech/pr-apply/moorestech_client/Library moorestech_client/Library
```
その後 uloop-launch スキルで `./moorestech_client` の Editor を起動し、`uloop compile --project-path ./moorestech_client` が ErrorCount 0 になることを確かめる（Editor の PID を控える）。

- [ ] **Step 2: 液体・アイテムを足す**

以下 `M=$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame/server_v8/mods/moorestechAlphaMod_8/master`。JSON は既存の整形（インデント・キー順）に合わせ、Python の `json.load`→要素追加→`json.dump(..., ensure_ascii=False, indent=<既存と同じ>)` で書く。

`$M/fluids.json` の `data` に追加:
```json
{"name": "重水素", "fluidGuid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "color": "#7FD8FF"}
```
`$M/items.json` の `data` に追加（反物質爆弾 sortPriority 1610 の直前）:
```json
{"itemGuid": "01a167e9-21b5-7479-9060-995505f43137", "imagePath": "", "name": "反物質", "sortPriority": 1605, "initialUnlocked": false, "recipeViewType": "IsUnlocked", "addressablePaths": {"handGrabModel": "", "entityModel": ""}, "stackLevelTableGuid": "9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa"}
```

- [ ] **Step 3: ブロックを足す・仮エントリを置き換える**

既存モデルの寸法を測る。`uloop execute-dynamic-code --project-path ./moorestech_client` で次を実行し、4モデルの renderer bounds（size）を読む:
```csharp
using UnityEditor; using UnityEngine; using System.Linq;
var paths = new[]{"Assets/AddressableResources/Block/FusionReactor.prefab","Assets/AddressableResources/Block/Deuterium_extraction_equipment.prefab","Assets/AddressableResources/Block/Antimatter_manufacturing_equipment.prefab","Assets/AddressableResources/Block/3D Printer.prefab","Assets/AddressableResources/Block/ElectricOven.prefab"};
return string.Join("\n", paths.Select(p => { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return $"{p} min={b.min} size={b.size}"; }));
```
各 blockSize は size を切り上げた整数にする（旧 mod の値: 核融合炉 [19,10,17]・重水素抽出装置 [3,2,3]・反物質製造装置 [3,4,3]。測定と食い違えば測定値を採り、判断記録に実測値を書く）。コネクタの offset は「入口 = [floor(x/2), 0, 0] 向き [0,0,-1]、出口 = [floor(x/2), 0, z-1] 向き [0,0,1]」で、下の JSON はこの規則で旧寸法から計算した値。寸法を変えたら offset も同じ規則で直す。

`$M/blocks.json` の核融合炉（01a162f1-…）の仮エントリを丸ごと次で置き換える（ガソリンエンジン発電機と同じキー構成）:
```json
{"blockGuid": "01a162f1-ab04-76e2-94cf-dc713edeb63b", "name": "核融合炉", "blockType": "ElectricGenerator", "blockSize": [19, 10, 17],
 "blockPrefabAddressablesPath": "Vanilla/Block/FusionReactor", "openable": true,
 "blockParam": {"fuelItemSlotCount": 0, "isInfinityPower": false, "infinityPower": 0,
  "inventoryConnectors": {"inputConnects": [], "outputConnects": []},
  "fuelFluidTankCapacity": 200,
  "fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "01a167e9-21c1-7ec0-9813-d30ce62e14c0", "offset": [9, 0, 0], "directions": [[0, 0, -1]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}], "outflowConnects": []},
  "fuelItems": [],
  "fuelFluids": [{"fluidGuid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "amount": 10, "time": 2, "power": 20000}],
  "maxWireConnectionCount": 4, "connectionRange": 30, "connectionHeightRange": 20},
 "initialUnlocked": false,
 "requiredItems": [{"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 600}, {"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 100}, {"itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 100}, {"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 100}],
 "placementsPerCost": 1, "sortPriority": 1195, "category": "電力", "subCategory": "発電"}
```
追加:
```json
{"blockGuid": "01a167e9-21b8-7db1-a289-99cdf7490fe9", "name": "重水素抽出装置", "blockType": "ElectricMachine", "blockSize": [3, 2, 3],
 "blockPrefabAddressablesPath": "Vanilla/Block/Deuterium_extraction_equipment", "openable": true,
 "blockParam": {"requiredPower": 4000, "idlePowerRate": 0.05, "inputSlotCount": 0, "outputSlotCount": 0,
  "inventoryConnectors": {"inputConnects": [], "outputConnects": []},
  "inputTankCount": 1, "outputTankCount": 1, "innerTankCapacity": 300, "moduleSlotCount": 3,
  "fluidInventoryConnectors": {
   "inflowConnects": [{"connectorGuid": "01a167e9-21c4-792a-a10b-e36304af4ddf", "offset": [1, 0, 0], "directions": [[0, 0, -1]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}],
   "outflowConnects": [{"connectorGuid": "01a167e9-21c7-7a2d-872c-c06b53e28700", "offset": [1, 0, 2], "directions": [[0, 0, 1]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}]},
  "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20},
 "initialUnlocked": false,
 "requiredItems": [{"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 100}, {"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 20}, {"itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 20}],
 "placementsPerCost": 1, "sortPriority": 1110, "category": "生産", "subCategory": "化学"}
```
```json
{"blockGuid": "01a167e9-21bb-76dd-9a83-0a249aa9f1fc", "name": "反物質製造装置", "blockType": "ElectricMachine", "blockSize": [3, 4, 3],
 "blockPrefabAddressablesPath": "Vanilla/Block/Antimatter_manufacturing_equipment", "openable": true,
 "blockParam": {"requiredPower": 12000, "idlePowerRate": 0.05, "inputSlotCount": 0, "outputSlotCount": 1,
  "inventoryConnectors": {"inputConnects": [], "outputConnects": [{"connectorGuid": "01a167e9-21cd-7a18-b1df-eacf1fea87a6", "offset": [1, 0, 2], "directions": [[0, 0, 1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}]},
  "inputTankCount": 1, "outputTankCount": 0, "innerTankCapacity": 300, "moduleSlotCount": 3,
  "fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "01a167e9-21ca-738d-9bae-95fc4d736a88", "offset": [1, 0, 0], "directions": [[0, 0, -1]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}], "outflowConnects": []},
  "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20},
 "initialUnlocked": false,
 "requiredItems": [{"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 400}, {"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 80}, {"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 100}],
 "placementsPerCost": 1, "sortPriority": 1120, "category": "生産", "subCategory": "化学"}
```

- [ ] **Step 4: レシピを足す**

`$M/machineRecipes.json` の `data` に追加（機械の入力スロットの上限はスタック上限 100 なので、1スロットの投入数は 100 以下に保つ）:
```json
{"machineRecipeGuid": "01a167e9-21dc-7eb1-a240-8345ed8fbd40", "blockGuid": "01a167e9-21b8-7db1-a289-99cdf7490fe9", "time": 8, "inputItems": [], "inputFluids": [{"fluidGuid": "9eae6979-d56a-4991-9107-b8161acec430", "amount": 100}], "outputItems": [], "outputFluids": [{"fluidGuid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "amount": 10}], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21df-7b2a-81e3-04be55cb89b0", "blockGuid": "01a167e9-21bb-76dd-9a83-0a249aa9f1fc", "time": 30, "inputItems": [], "inputFluids": [{"fluidGuid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "amount": 50}], "outputItems": [{"itemGuid": "01a167e9-21b5-7479-9060-995505f43137", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21e2-7c4e-91be-8ad747466bde", "blockGuid": "019e1225-f49b-7283-8835-428d903852f1", "time": 60, "inputItems": [{"itemGuid": "01a167e9-21b5-7479-9060-995505f43137", "count": 20}, {"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 50}, {"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 100}, {"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 20}], "inputFluids": [], "outputItems": [{"itemGuid": "01a162ab-07ba-73c1-9dd6-38e1e867663b", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
```

- [ ] **Step 5: 研究の解放物と仮コストを足す**

`$M/research.json`:
- 核融合炉（019e3af0-…）: `consumeItems` = `[{ICチップLv1, 600}, {鋼鉄板, 3000}, {コンピュータ, 600}, {制御モータ, 600}]`、`clearedActions` の unlockBlock を `["01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a167e9-21b8-7db1-a289-99cdf7490fe9"]` にする
- 反物質爆弾（019e3aef-…）: `consumeItems` = `[{ICチップLv1, 1000}, {鋼鉄板, 4000}, {コンピュータ, 1000}]`、`clearedActions` を `unlockItemRecipeView ["01a167e9-21b5-7479-9060-995505f43137", "01a162ab-07ba-73c1-9dd6-38e1e867663b"]` と `unlockBlock ["01a167e9-21bb-76dd-9a83-0a249aa9f1fc"]` にする
（`consumeItems` の要素は `{"itemGuid": ..., "itemCount": ...}`。値は agent前提・S1 待ち）

- [ ] **Step 6: ローカライズ行を足す**

`.../moorestechAlphaMod_8/localization/localization.csv`（ヘッダ `key,Source,english,japanese,german,korean`）。既存行が無いものだけ足す（`grep -n "01a162f1" localization.csv` で確認）:
```
fluid.01a167e9-21b2-7bb0-b6a3-0f17d349704e.name,重水素,Deuterium,重水素,Deuterium,중수소
item.01a167e9-21b5-7479-9060-995505f43137.name,反物質,Antimatter,反物質,Antimaterie,반물질
block.01a162f1-ab04-76e2-94cf-dc713edeb63b.name,核融合炉,Fusion Reactor,核融合炉,Fusionsreaktor,핵융합로
block.01a167e9-21b8-7db1-a289-99cdf7490fe9.name,重水素抽出装置,Deuterium Extractor,重水素抽出装置,Deuteriumextraktor,중수소 추출기
block.01a167e9-21bb-76dd-9a83-0a249aa9f1fc.name,反物質製造装置,Antimatter Fabricator,反物質製造装置,Antimateriefabrik,반물질 제조기
```

- [ ] **Step 7: 整合ツールとマスタ読込スモークを通す**

```bash
cd $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame
python3 tools/research_sync/sync_machine_recipe_unlocks.py      # 機械レシピの解放ノードを書き戻す
python3 tools/research_sync/sync_machine_recipe_unlocks.py --check   # exit 0
python3 tools/research_sync/progression_check.py                    # 詰みが増えない
python3 scripts/check_shipped_data_contracts.py                      # exit 0
```
Expected: 重水素・反物質・反物質爆弾のレシピがそれぞれ 核融合炉・反物質爆弾・反物質爆弾 ノードの unlockMachineRecipe に入る。

スモーク（`uloop execute-dynamic-code --project-path ./moorestech_client`）:
```csharp
using Server.Boot; using Core.Master; using System;
// worktree 直下の兄弟 moorestech_master/tier9-fusion-elevator-endgame を指す（Assets から4つ上が worktrees/）
var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../moorestech_master/tier9-fusion-elevator-endgame/server_v8"));
new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(dir));
var reactor = MasterHolder.BlockMaster.GetBlockMaster(Guid.Parse("01a162f1-ab04-76e2-94cf-dc713edeb63b"));
return $"{reactor.Name} {reactor.BlockType} fluid={MasterHolder.FluidMaster.GetFluidId(Guid.Parse("01a167e9-21b2-7bb0-b6a3-0f17d349704e"))}";
```
Expected: `核融合炉 ElectricGenerator fluid=<数値>`。仮エントリ（宇宙エレベーター拠点・ロケット発射台の空 blockType）でローダーが落ちた場合は、そのエラー行を判断記録に実測として書き、Task 6 の後のスモークで解消を確かめる（それ以外のエラーは直す）。

- [ ] **Step 8: コミットして push する**

```bash
cd $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame
git add server_v8/mods/moorestechAlphaMod_8
git commit -m "data(v8): 重水素・核融合炉・重水素抽出装置・反物質・反物質製造装置と爆弾レシピを足す"
git push -u origin feat/tier9-fusion-elevator-endgame
```

### Task 2: マスタ — グラフェン工程・金属3Dプリンタ・研究ツリー整理・最終研究

**Files:**
- Modify: `$M/items.json`, `$M/blocks.json`, `$M/machineRecipes.json`, `$M/research.json`, `.../localization/localization.csv`（moorestech_master worktree 内）

**Interfaces:**
- Consumes: Task 1 のマスタ
- Produces: 金属3Dプリンタ（ブロック）01a167e9-21be-…、最終研究 01a167e9-21f6-…、グラフェン4レシピ（Task 4 のシミュレーションが name で引く）

- [ ] **Step 1: 金属3Dプリンタをアイテムからブロックへ移す**

アイテム 019e3af5-e750-71ae-ab09-2b98f3db9913 を `$M/items.json` から消し、`grep -rn "019e3af5-e750-71ae-ab09-2b98f3db9913" $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame/server_v8` の残り（研究の unlockItemRecipeView・ローカライズ・クラフトレシピ）も消す。Task 1 Step 3 で測った `3D Printer` の寸法で blockSize を決め、`$M/blocks.json` に追加（下は [3,3,3] の場合の offset）:
```json
{"blockGuid": "01a167e9-21be-72cd-ae04-e5bbe12b95c2", "name": "金属3Dプリンタ", "blockType": "ElectricMachine", "blockSize": [3, 3, 3],
 "blockPrefabAddressablesPath": "Vanilla/Block/3D Printer", "openable": true,
 "blockParam": {"requiredPower": 300, "idlePowerRate": 0.2, "inputSlotCount": 3, "outputSlotCount": 1,
  "inventoryConnectors": {"inputConnects": [{"connectorGuid": "01a167e9-21d0-749d-a485-ddc2943f0535", "offset": [1, 0, 0], "directions": [[0, 0, -1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}],
   "outputConnects": [{"connectorGuid": "01a167e9-21d3-7e29-9915-33f9c849be17", "offset": [1, 0, 2], "directions": [[0, 0, 1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}]},
  "inputTankCount": 0, "outputTankCount": 0, "innerTankCapacity": 0, "moduleSlotCount": 3,
  "fluidInventoryConnectors": {"inflowConnects": [], "outflowConnects": []},
  "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20},
 "initialUnlocked": false,
 "requiredItems": [{"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 60}, {"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 10}, {"itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 10}],
 "placementsPerCost": 1, "sortPriority": 1010, "category": "生産", "subCategory": "電気機械"}
```

- [ ] **Step 2: グラフェン工程と3Dプリンタのレシピを足す**

`$M/machineRecipes.json` に追加:
```json
{"machineRecipeGuid": "01a167e9-21e5-7b43-9414-15365405623b", "blockGuid": "019e0a68-3705-75f7-a040-50fab3c7d81e", "time": 3, "inputItems": [{"itemGuid": "f882df45-5651-4aec-b871-a9f885a5f75f", "count": 2}], "inputFluids": [], "outputItems": [{"itemGuid": "01a16297-a7f5-759b-91ba-be4021ca3bf2", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21e8-76b9-bf3f-b462360e3fa9", "blockGuid": "019e0a68-3705-75f7-a040-50fab3c7d81e", "time": 6, "inputItems": [{"itemGuid": "01a16297-a7f5-759b-91ba-be4021ca3bf2", "count": 2}, {"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 1}], "inputFluids": [], "outputItems": [{"itemGuid": "01a16297-4724-73b9-82c3-2e77120be77d", "count": 1, "percent": 1}, {"itemGuid": "01a16297-6665-765d-8e19-96d357394c3e", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21eb-7e67-b6ee-1d6503d8af84", "blockGuid": "019e1225-f49b-7283-8835-428d903852f1", "time": 6, "inputItems": [{"itemGuid": "01a16297-4724-73b9-82c3-2e77120be77d", "count": 1}], "inputFluids": [], "outputItems": [{"itemGuid": "01a16290-f793-748b-a0b0-91d44be0d4ab", "count": 5, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21ee-7339-b7b6-351ad1775730", "blockGuid": "019e0a68-3705-75f7-a040-50fab3c7d81e", "time": 3, "inputItems": [{"itemGuid": "01a16297-6665-765d-8e19-96d357394c3e", "count": 1}], "inputFluids": [], "outputItems": [{"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21f0-7d5b-8ddc-18a841c01637", "blockGuid": "01a167e9-21be-72cd-ae04-e5bbe12b95c2", "time": 4, "inputItems": [{"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 1}, {"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 5}, {"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 2}], "inputFluids": [], "outputItems": [{"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21f3-73f5-9418-35a676b8fa3f", "blockGuid": "01a167e9-21be-72cd-ae04-e5bbe12b95c2", "time": 5, "inputItems": [{"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 1}, {"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 4}, {"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 4}], "inputFluids": [], "outputItems": [{"itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
```

- [ ] **Step 3: 研究ツリーを整理し最終研究を足す**

`$M/research.json`:
- フィルター分岐器（019e3aab-…）のノードを消す
- 金属超精密加工（019e3af5-f2e6-…）: `prevResearchNodeGuids` = `["019e3ae2-5005-7439-b242-9b7b3191a2c3"]`、`consumeItems` = `[{ICチップLv1, 300}, {鋼鉄板, 1500}, {制御モータ, 300}]`、`clearedActions` = `[{"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["01a167e9-21be-72cd-ae04-e5bbe12b95c2"]}}]`、説明「ICチップから部品を1工程で作る金属3Dプリンタを解放する」
- 軌道エレベーターの建設（01a16291-…）: 説明「グラフェンケーブルと宇宙エレベーター拠点を解放する」、`graphViewSettings.UIPosition` = `[4590, 0]`、`consumeItems` = `[{ICチップLv1, 800}, {鋼鉄板, 3000}]`（計算データ・ロケット関連は Task 5）
- 機械オーバークロック（019e3ae2-…）は触らない（S1 が速度モジュール研究として作る）
- 最終研究を追加:
```json
{"researchNodeGuid": "01a167e9-21f6-7d26-a990-e7d6d78750d0", "researchNodeName": "最終研究", "researchNodeDescription": "反物質爆弾を規定量そろえたことを記録する仮の研究。現段階のゲームの終点（解放物なし）",
 "consumeItems": [{"itemGuid": "01a162ab-07ba-73c1-9dd6-38e1e867663b", "itemCount": 10}],
 "prevResearchNodeGuids": ["019e3aef-a149-746b-8fad-159e0434cb51"],
 "clearedActions": [],
 "graphViewSettings": {"UIPosition": [4930, -200], "UIScale": [1, 1, 1], "IconItem": "01a162ab-07ba-73c1-9dd6-38e1e867663b"}}
```

- [ ] **Step 4: ローカライズを直す**

localization.csv: フィルター分岐器ノード（`research.019e3aab-32c6-7166-a184-d761153c2498.*`）と旧アイテム（`item.019e3af5-e750-71ae-ab09-2b98f3db9913.name`）の行を消し、次を足す（金属超精密加工・軌道エレベーターの建設の description 行は新しい説明文へ書き換える。英独韓も対応する訳に）:
```
block.01a167e9-21be-72cd-ae04-e5bbe12b95c2.name,金属3Dプリンタ,Metal 3D Printer,金属3Dプリンタ,Metall-3D-Drucker,금속 3D 프린터
research.01a167e9-21f6-7d26-a990-e7d6d78750d0.name,最終研究,Final Research,最終研究,Abschlussforschung,최종 연구
research.01a167e9-21f6-7d26-a990-e7d6d78750d0.description,反物質爆弾を規定量そろえたことを記録する仮の研究。現段階のゲームの終点（解放物なし）,"A provisional research that records stockpiling the required antimatter bombs. The current end of the game (unlocks nothing)",反物質爆弾を規定量そろえたことを記録する仮の研究。現段階のゲームの終点（解放物なし）,"Vorläufige Forschung, die das Bereitstellen der nötigen Antimateriebomben festhält. Derzeitiges Spielende (schaltet nichts frei)","필요한 반물질 폭탄을 모은 것을 기록하는 임시 연구. 현재 게임의 끝 (해금 없음)"
```

- [ ] **Step 5: 整合ツールを通し、コミットして push する**

Task 1 Step 7 と同じ4コマンドとスモーク（金属3Dプリンタのブロックが引けること）。Expected: グラフェンのレシピ4本が 軌道エレベーターの建設 に、3Dプリンタのレシピ2本が 金属超精密加工 に割り当たる。最終研究がツリーから到達可能。
```bash
git add server_v8/mods/moorestechAlphaMod_8
git commit -m "data(v8): グラフェン工程・金属3Dプリンタ・最終研究を足しフィルター分岐器ノードを消す"
git push
```

### Task 3: 発電機（ElectricGenerator）の稼働表示

核融合炉を開いたとき、汎用UIに稼働率・残燃料・燃料タンクを出す（裁定「既存モデル＋汎用UIの稼働表示」）。現状の `VanillaElectricGeneratorComponent` は状態詳細を出さない。また、クライアントは ElectricGenerator に `CommonMachineBlockStateChangeProcessor` を付けるため、CommonMachine キーを持たない状態が届くと `Debug.LogError("[CommonMachineBlockStateChangeProcessor] State is null ...")` を出す（コード読解。`CommonMachineBlockStateChangeProcessor.cs:23-28`）。発電機が状態を出すようになると必ずこの経路を通るので、ElectricGenerator を CommonMachine 扱いから外す（同 processor は state を読む以外に何もしない）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorSaveJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorFuelService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorFuelSettingsBuilder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorFuelSaveConverter.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/PowerGeneratorStateDetail.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectPrefabContainer.cs:146-154`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx:29`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/__tests__/SectionStackView.test.tsx`
- Modify: `moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json`、`moorestech_web/webui/e2e/tests/block/blockRegistryCoverage.spec.ts`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Electric/ElectricGeneratorStateDetailTest.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `VanillaElectricGeneratorFuelService.Update()` の戻り値を `bool`（燃料の点き始め・燃え尽きの tick だけ true）に変える
  - `VanillaElectricGeneratorFuelService`: `public bool IsBurning`、`public uint RemainingFuelTicks`、`public uint CurrentFuelTotalTicks`、`public bool TryGetFuelTank(out FluidMessagePack tank)`
  - `PowerGeneratorStateDetail(double remainingFuelSeconds, double currentFuelSeconds, float operatingRate)`
  - `VanillaElectricGeneratorComponent : IBlockStateObservable`（`PowerGenerator` と、液体燃料タンクがあれば `FluidMachineInventory` の状態詳細を出す）

- [ ] **Step 1: テストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Electric/ElectricGeneratorStateDetailTest.cs`:
```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core.Electric
{
    public class ElectricGeneratorStateDetailTest
    {
        [Test]
        public void 液体燃料で燃焼中は稼働率と燃料タンクを状態詳細に出す()
        {
            var (generator, fuel, fluidId) = Setup();
            var notified = 0;
            generator.OnChangeBlockState.Subscribe(_ => notified++);

            // 3単位ぶん入れて1tick進めると1単位が点き、タンクに2単位残る
            // Feed three units and advance one tick: one unit ignites and two remain in the tank
            generator.AddLiquid(new FluidStack(fuel.Amount * 3, fluidId), default);
            GameUpdater.UpdateOneTick();

            var (power, tank) = ReadDetails(generator);
            Assert.AreEqual(1f, power.OperatingRate);
            Assert.AreEqual(fuel.Time, power.CurrentFuelTime, 0.01);
            Assert.AreEqual(fuel.Amount * 2, tank.Amount, 0.001);
            Assert.AreEqual(1, notified);
        }

        [Test]
        public void 燃え尽きたら稼働率0を一度だけ通知する()
        {
            var (generator, fuel, fluidId) = Setup();
            generator.AddLiquid(new FluidStack(fuel.Amount, fluidId), default);
            GameUpdater.UpdateOneTick();

            var notified = 0;
            generator.OnChangeBlockState.Subscribe(_ => notified++);
            var fuelTicks = GameUpdater.SecondsToTicks(fuel.Time);
            for (var i = 0; i < fuelTicks + 1; i++) GameUpdater.RunFrames(1);

            var (power, _) = ReadDetails(generator);
            Assert.AreEqual(0f, power.OperatingRate);
            Assert.AreEqual(1, notified);
        }

        private static (VanillaElectricGeneratorComponent, FuelFluidsElement, FluidId) Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GeneratorId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (ElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockParam;
            var fuel = param.FuelFluids[0];
            return (block.GetComponent<VanillaElectricGeneratorComponent>(), fuel, MasterHolder.FluidMaster.GetFluidId(fuel.FluidGuid));
        }

        private static (PowerGeneratorStateDetail, FluidMessagePack) ReadDetails(VanillaElectricGeneratorComponent generator)
        {
            var details = generator.GetBlockStateDetails();
            var power = MessagePackSerializer.Deserialize<PowerGeneratorStateDetail>(details.First(d => d.Key == PowerGeneratorStateDetail.StateDetailKey).Value);
            var fluid = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(details.First(d => d.Key == FluidMachineInventoryStateDetail.BlockStateDetailKey).Value);
            return (power, fluid.InputTanks[0]);
        }
    }
}
```
（`FuelFluidsElement` の型名・名前空間は生成コードに合わせる。`VanillaElectricGeneratorFuelService.cs` の既存 using が手本）

- [ ] **Step 2: 燃料サービスを分割し、状態用の読み取り口を足す**

`VanillaElectricGeneratorFuelService.cs` は 289 行あるので、200 行未満へ分ける。
1. ctor の `#region Internal` の `BuildItemFuelSettings` / `BuildFluidFuelSettings` を、そのまま `VanillaElectricGeneratorFuelSettingsBuilder.cs` の `public static class VanillaElectricGeneratorFuelSettingsBuilder` の `public static Dictionary<ItemId, (FuelItemsElement Setting, uint Ticks)> BuildItemFuelSettings(ElectricGeneratorBlockParam param)` / `public static Dictionary<FluidId, (FuelFluidsElement Setting, uint Ticks)> BuildFluidFuelSettings(ElectricGeneratorBlockParam param)` へ移し、ctor は `_itemFuelSettings = VanillaElectricGeneratorFuelSettingsBuilder.BuildItemFuelSettings(param);` のように呼ぶ。
2. `WriteSaveData` / `Restore` の本体を `VanillaElectricGeneratorFuelSaveConverter.cs` の `internal static class VanillaElectricGeneratorFuelSaveConverter` の `internal static void Write(VanillaElectricGeneratorFuelService service, VanillaElectricGeneratorSaveJsonObject saveData)` / `internal static void Restore(VanillaElectricGeneratorFuelService service, VanillaElectricGeneratorSaveJsonObject saveData)` へ移す（`_x` を `service._x` に置き換えるだけ）。サービス側の公開メソッドは残して委譲する: `public void WriteSaveData(VanillaElectricGeneratorSaveJsonObject saveData) => VanillaElectricGeneratorFuelSaveConverter.Write(this, saveData);`（Restore も同形）。変換器が読む・書くフィールド（`_itemFuelSettings`, `_fluidFuelSettings`, `_fuelFluidContainer`, `_currentFuelItemId`, `_currentFuelFluidId`, `_currentFuelType`, `_remainingFuelTicks`）と `FuelType` を `private` から `internal` へ変える（同アセンブリの変換器だけが触る。既存テストの `BindingFlags.NonPublic` のリフレクションは internal も読めるのでそのまま通る）。フィールド名は変えない。
3. 状態用の読み取り口と `Update` の戻り値を足す:
```csharp
// 燃焼中か（表示用）
// Whether a fuel unit is burning (for display)
public bool IsBurning => _currentFuelType != FuelType.None;
public uint RemainingFuelTicks => _remainingFuelTicks;

// 燃焼中の燃料1単位の総tick。燃えていなければ0
// Total ticks of the burning fuel unit; zero when nothing burns
public uint CurrentFuelTotalTicks
{
    get
    {
        if (_currentFuelType == FuelType.Item && _itemFuelSettings.TryGetValue(_currentFuelItemId, out var item)) return item.Ticks;
        if (_currentFuelType == FuelType.Fluid && _fluidFuelSettings.TryGetValue(_currentFuelFluidId, out var fluid)) return fluid.Ticks;
        return 0;
    }
}

// 液体燃料タンクを表示用に返す。液体燃料の設定が無い発電機はタンクを持たない
// Return the fluid fuel tank for display; generators without fluid fuel settings have no tank
public bool TryGetFuelTank(out FluidMessagePack tank)
{
    tank = null;
    if (_fuelFluidContainer == null) return false;
    tank = new FluidMessagePack(_fuelFluidContainer.FluidId, _fuelFluidContainer.Amount, _fuelFluidContainer.Capacity);
    return true;
}

// 燃料の点き始め・燃え尽きのtickだけtrueを返す（状態通知の合図）
// Returns true only on ticks where a fuel unit ignites or burns out (the cue for a state notification)
public bool Update()
{
    var wasBurning = IsBurning;
    UpdateFuel();
    var ignitedNewUnit = IsBurning && _remainingFuelTicks == CurrentFuelTotalTicks;
    return wasBurning != IsBurning || ignitedNewUnit;
}
```
既存の `public void Update()` は `private void UpdateFuel()` に改名し、本体は変えない。`using Game.Block.Interface.State;` を足す。

- [ ] **Step 3: 発電機本体に状態詳細を足し、200行未満へ縮める**

`VanillaElectricGeneratorComponent.cs`:
1. 末尾の `VanillaElectricGeneratorSaveJsonObject` と `VanillaElectricGeneratorFluidSaveJsonObject` を、同じ名前空間のまま `VanillaElectricGeneratorSaveJsonObject.cs` へ移す。
2. インベントリの委譲メソッド（`GetItem`, `SetItem` ×2, `GetSlotSize`, `CreateCopiedItems`, `ReplaceItem` ×2, `InsertItem` ×4, `InsertionCheck`）を `VanillaChestComponent.cs:97-108` と同じ1行形式（`{ BlockException.CheckDestroy(this); return ...; }`）にまとめる。
3. 状態詳細を足す:
```csharp
// クラス宣言に IBlockStateObservable を足す
public class VanillaElectricGeneratorComponent : IElectricGenerator, IOpenableBlockInventoryComponent, IBlockSaveState, IUpdatableBlockComponent, IFluidInventory, IBlockStateObservable

private readonly Subject<Unit> _onChangeBlockState = new();
public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

public void Update()
{
    BlockException.CheckDestroy(this);
    if (_isInfinityPower) return;

    // 燃料の点き始め・燃え尽きでだけ状態を通知する
    // Notify the state only when a fuel unit ignites or burns out
    if (_fuelService.Update()) _onChangeBlockState.OnNext(Unit.Default);
}

public BlockStateDetail[] GetBlockStateDetails()
{
    BlockException.CheckDestroy(this);

    // 稼働率は燃焼中なら1、無限電源も常に1
    // The operating rate is 1 while burning, and always 1 for infinite power
    var operatingRate = _isInfinityPower || _fuelService.IsBurning ? 1f : 0f;
    var power = new PowerGeneratorStateDetail(GameUpdater.TicksToSeconds(_fuelService.RemainingFuelTicks), GameUpdater.TicksToSeconds(_fuelService.CurrentFuelTotalTicks), operatingRate);
    var details = new List<BlockStateDetail> { new(PowerGeneratorStateDetail.StateDetailKey, MessagePackSerializer.Serialize(power)) };

    // 液体燃料タンクは機械と同じ液体詳細で送り、webuiの液体行に出す
    // Send the fluid fuel tank as the same fluid detail machines use, shown in the webui fluid row
    if (_fuelService.TryGetFuelTank(out var tank))
    {
        var fluid = new FluidMachineInventoryStateDetail(new List<FluidMessagePack> { tank }, new List<FluidMessagePack>());
        details.Add(new BlockStateDetail(FluidMachineInventoryStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(fluid)));
    }
    return details.ToArray();
}
```
`using System; using Game.Block.Interface.State; using MessagePack; using UniRx;` を足す。`wc -l` で 200 行未満を確かめる。

`PowerGeneratorStateDetail.cs` に ctor を足す:
```csharp
// 秒値から作る（電気発電機用。燃料サービスの型に依存しない）
// Build from seconds (for electric generators; independent of the fuel service type)
public PowerGeneratorStateDetail(double remainingFuelSeconds, double currentFuelSeconds, float operatingRate)
{
    RemainingFuelTime = remainingFuelSeconds;
    CurrentFuelTime = currentFuelSeconds;
    OperatingRate = operatingRate;
}
```

- [ ] **Step 4: クライアントと webui を直す**

`BlockGameObjectPrefabContainer.cs` の `IsCommonMachine` から `BlockTypeConst.ElectricGenerator or` の行を消す。
`SectionStackView.tsx:29` を次にする:
```tsx
  ElectricGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: "generator-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
```
`SectionStackView.test.tsx` の `["ElectricGenerator", "generator-fuel-grid", null, true, true]` を `["ElectricGenerator", "generator-fuel-grid", "generator-fluid-slots", false, false]` にする。新しい worktree なので最初に `cd moorestech_web/webui && pnpm install --frozen-lockfile`。

核融合炉が `openable: true` になると、e2e の `blockRegistryCoverage.spec.ts`（blocks.json の `[blockType, openable]` を `sort -u` した fixture のうち openable の型が、レジストリか意図的 Generic に入っているかを見る）が ElectricGenerator で落ちる。発電機は専用 view を作らず汎用の `SectionStackView` で出すので、意図的 Generic に入れる:
- `e2e/fixtures/v8-block-ui-registry.json` の `{ "blockType": "ElectricGenerator", "openable": false },` の次の行に `{ "blockType": "ElectricGenerator", "openable": true },` を足す（他の発電機は openable false のままなので両方の行が残る）
- `e2e/tests/block/blockRegistryCoverage.spec.ts` の `intentionalGeneric` の `"ElectricMachine",` の前に `"ElectricGenerator",` を足す

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricGeneratorStateDetailTest|PowerGeneratorTest|PowerGeneratorSaveLoadTest"`、`cd moorestech_web/webui && npx vitest run src/features/blockInventory && npx playwright test e2e/tests/block/blockRegistryCoverage.spec.ts`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Electric/ElectricGeneratorStateDetailTest.cs* moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectPrefabContainer.cs moorestech_web/webui/src/features/blockInventory/views moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json moorestech_web/webui/e2e/tests/block/blockRegistryCoverage.spec.ts
git commit -m "feat(generator): 電気発電機が稼働率と燃料タンクを状態詳細に出す"
```

### Task 4: 数値シミュレーション（核融合・反物質・グラフェン）

**Files:**
- Create: `tools/balance/tier9_endgame_balance.py`（moorestech_master worktree 内）

**Interfaces:**
- Consumes: Task 1・2 のマスタ（名前で引く: `核融合炉`, `重水素抽出装置`, `反物質製造装置`, `ガソリンエンジン発電機`, `電気炉`, `電気汎用工作装置`, `重水素`, `反物質`, `反物質爆弾`, `グラフェンケーブル`, `グラフェンシート`, `高純度炭素`, `最終研究`）
- Produces: `python3 tools/balance/tier9_endgame_balance.py` が目標外で exit 1（Task 12 がエレベーターの検査を足す）

- [ ] **Step 1: スクリプトを書く**

```python
#!/usr/bin/env python3
# Tier9 終盤の数値（核融合の差し引き電力・反物質・グラフェン）を出荷マスタから計算し目標と照合する
# Compute Tier9 endgame numbers (fusion net power, antimatter, graphene) from the shipped master and check targets
# 目標の根拠: moorestech ADR 0090（差し引きはガソリン発電機数台分）、S1 裁定（Tier9 15〜25時間・S3 配分 5〜11時間）
# Targets: moorestech ADR 0090 (net equals a few gasoline generators) and the S1 ruling (Tier9 15-25h, S3 share 5-11h)
import json, os, sys

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
M = os.path.join(ROOT, 'server_v8', 'mods', 'moorestechAlphaMod_8', 'master')


def load(name):
    with open(os.path.join(M, name), encoding='utf-8') as f:
        return json.load(f)


blocks = {b['name']: b for b in load('blocks.json')['data']}
fluids = {f['name']: f['fluidGuid'] for f in load('fluids.json')['data']}
items = {i['name']: i['itemGuid'] for i in load('items.json')['data']}
recipes = load('machineRecipes.json')['data']
research = {r['researchNodeName']: r for r in load('research.json')['data']}
failures = []


def check(label, value, low, high):
    ok = low <= value <= high
    print(f"{'OK' if ok else 'NG'} {label}: {value:.2f}（目標 {low}〜{high}）")
    if not ok:
        failures.append(label)


def recipe_of(block_name, output_name, is_fluid):
    # 機械と出力名からレシピと出力量を引く。無ければ原因名つきで止める
    # Look up the recipe and output amount by machine and output name; abort naming the cause when missing
    block_guid = blocks[block_name]['blockGuid']
    key, guid_key, amount_key = ('outputFluids', 'fluidGuid', 'amount') if is_fluid else ('outputItems', 'itemGuid', 'count')
    target = fluids[output_name] if is_fluid else items[output_name]
    for recipe in recipes:
        if recipe['blockGuid'] != block_guid:
            continue
        for output in recipe[key]:
            if output[guid_key] == target:
                return recipe, output[amount_key]
    sys.exit(f'ERROR recipe not found: {block_name} -> {output_name}')


# 核融合: 炉1基の重水素消費を賄う抽出装置の台数と差し引き電力
# Fusion: extractors needed to feed one reactor and the resulting net power
gasoline_power = blocks['ガソリンエンジン発電機']['blockParam']['fuelFluids'][0]['power']
reactor_fuel = blocks['核融合炉']['blockParam']['fuelFluids'][0]
extractor = blocks['重水素抽出装置']['blockParam']
extract_recipe, extract_out = recipe_of('重水素抽出装置', '重水素', True)
deuterium_per_extractor = extract_out / extract_recipe['time']
extractors_per_reactor = (reactor_fuel['amount'] / reactor_fuel['time']) / deuterium_per_extractor
net_power = reactor_fuel['power'] - extractors_per_reactor * extractor['requiredPower']
print(f"炉: 出力 {reactor_fuel['power']} / 抽出装置 {extractors_per_reactor:.2f} 台 × {extractor['requiredPower']} / 差し引き {net_power:.0f}")
check('炉1基の差し引き電力（ガソリン発電機何台分）', net_power / gasoline_power, 3, 5)
check('炉1基に要る抽出装置の台数', extractors_per_reactor, 2, 8)
# モジュール値は S1 の担当。仮に省エネ3枚で消費-45%とした場合を参考表示する（検査はしない）
# Module values belong to S1; show a reference case of three efficiency modules at -45% (not checked)
print(f"参考: 抽出装置の消費-45%なら差し引き {reactor_fuel['power'] - extractors_per_reactor * extractor['requiredPower'] * 0.55:.0f}")

# 反物質: 1台の電力（重水素づくりを含む）と、最終研究の爆弾を揃える時間
# Antimatter: power per fabricator including its deuterium, and the time to stock the final research bombs
fabricator = blocks['反物質製造装置']['blockParam']
antimatter_recipe, antimatter_out = recipe_of('反物質製造装置', '反物質', False)
deuterium_in = antimatter_recipe['inputFluids'][0]['amount'] / antimatter_recipe['time']
fabricator_total_power = fabricator['requiredPower'] + deuterium_in / deuterium_per_extractor * extractor['requiredPower']
check('反物質製造装置1台の電力（炉の差し引き何基分）', fabricator_total_power / net_power, 3, 6)
bomb_recipe, _ = recipe_of('電気汎用工作装置', '反物質爆弾', False)
antimatter_per_bomb = next(i['count'] for i in bomb_recipe['inputItems'] if i['itemGuid'] == items['反物質'])
final_bombs = next(c['itemCount'] for c in research['最終研究']['consumeItems'] if c['itemGuid'] == items['反物質爆弾'])
hours = final_bombs * antimatter_per_bomb / (2 * antimatter_out / antimatter_recipe['time']) / 3600
check('反物質製造装置2台で最終研究の爆弾を揃える時間 [h]', hours, 0.5, 3)

# グラフェン: ロケット1便分（1000本）を1時間で作る機械台数
# Graphene: machines needed to make one launch (1000 cables) per hour
cable_recipe, cable_out = recipe_of('電気汎用工作装置', 'グラフェンケーブル', False)
sheet_recipe, sheet_out = recipe_of('電気炉', 'グラフェンシート', False)
carbon_recipe, carbon_out = recipe_of('電気炉', '高純度炭素', False)
cables_per_sec = 1000 / 3600
sheets_per_sec = cables_per_sec / cable_out * cable_recipe['inputItems'][0]['count']
carbon_per_sec = sheets_per_sec / sheet_out * next(i['count'] for i in sheet_recipe['inputItems'] if i['itemGuid'] == items['高純度炭素'])
machines = (cables_per_sec / (cable_out / cable_recipe['time'])
            + sheets_per_sec / (sheet_out / sheet_recipe['time'])
            + carbon_per_sec / (carbon_out / carbon_recipe['time']))
check('1便分のケーブルを1時間で作る機械台数（戻し炉を除く）', machines, 1, 12)

if failures:
    print('NG: ' + ', '.join(failures))
    sys.exit(1)
```

- [ ] **Step 2: 実行して目標に合わせる**

Run: `python3 tools/balance/tier9_endgame_balance.py`（moorestech_master worktree で）
Expected: 全行 OK、exit 0。NG が出たら blocks.json / machineRecipes.json の値（requiredPower・time・amount）を直して再実行し、直した値と理由を plan の『判断記録（ADR）』へ追記する。

- [ ] **Step 3: コミットして push する**

```bash
git add tools/balance/tier9_endgame_balance.py server_v8/mods/moorestechAlphaMod_8
git commit -m "tools: Tier9 終盤の数値を検査するシミュレーションを足す"
git push
```

### Task 5: S2 取り込みゲートと S2 依存のマスタ

**Files:**
- Modify（merge）: moorestech と moorestech_master の両 worktree
- Modify: `$M/items.json`, `$M/research.json`, `.../localization/localization.csv`

**Interfaces:**
- Consumes（S2）: `Game.Orbit.IOrbitalArrivalService`（`IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)`、`bool IsOrbitalPayload(ItemId itemId)`、`IObservable<OrbitalArrival> OnOrbitalArrival { get; }`）、`Game.Orbit.OrbitalArrival`（`public readonly ItemId PayloadItemId; public readonly int PayloadCount;`）、items.yml の `orbitalPayloads`（`payloadItemGuid` / `countPerLaunch` / `arrivalOutputs[{itemGuid, count}]`）、`WorldSaveAllInfo.CurrentVersion == 7`
- Produces: グラフェンケーブルの積荷行と [1000] スタック表、研究 (a)(b)(c)

- [ ] **Step 1: ゲート — S2 の push を確かめる**

```bash
git fetch origin
git log --oneline origin/feat/tier9-compute-data-loop | grep "feat(orbit): 軌道到着サービスと初打ち上げ履歴を足す"
git ls-tree -r --name-only origin/feat/tier9-compute-data-loop -- moorestech_server/Assets/Scripts/Game.Orbit | grep IOrbitalArrivalService
bd show moorestech-xyuaf.1 | grep "Game.Orbitと積荷表をpush済み"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame fetch origin
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame log --oneline origin/feat/tier9-compute-data-loop | head -3
```
3つとも当たれば Step 2 へ。S2 が既に origin/master へマージ済みなら、以降の `origin/feat/tier9-compute-data-loop` を `origin/master` に読み替える。当たらなければ、前景で `sleep 600` → 上を再実行、を最大12回（2時間）繰り返す。それでも無ければ `bd note` に「S2 の Game.Orbit 未 push で Task 5 以降を保留」と書き、Task 1〜4 の成果を push した状態で止まって報告する（解決不能な障害）。

- [ ] **Step 2: 両 repo へ S2 を取り込む**

```bash
git merge --no-ff origin/feat/tier9-compute-data-loop -m "Merge S2 (feat/tier9-compute-data-loop) for Game.Orbit"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame merge --no-ff origin/feat/tier9-compute-data-loop -m "Merge S2 master for orbitalPayloads"
```
衝突の解き方: `research.json`・`items.json`・`blocks.json`・`localization.csv` は両方の追加を残す（S3 は 軌道エレベーターの建設 を、S2 は ロケット技術 ほかを触っている）。`.moorestech-external-revisions.json` は S3 側を残す（Task 15 で付け直す）。解いたら `uloop compile --project-path ./moorestech_client`（ErrorCount 0）と `grep -n "CurrentVersion" moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（`= 7` であること。6 なら S2 の版上げが未取り込み → Step 1 へ戻る）。S2 のテスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Orbit"` が通ることを確かめる。

- [ ] **Step 3: グラフェンケーブルを積荷表に載せ、スタック上限 1000 の表を割り当てる**

`$M/items.json`（S2 取り込み後に `orbitalPayloads` のキー名を S2 の items.yml で確かめる）:
- `itemStackLevelTables` に `{"tableGuid": "01a167e9-21f9-70a8-be5e-08c2ba6895c9", "name": "グラフェンケーブル", "stackCounts": [1000]}` を足す
- グラフェンケーブル（01a16290-…）の `stackLevelTableGuid` を `01a167e9-21f9-70a8-be5e-08c2ba6895c9` にする
- `orbitalPayloads` に `{"payloadItemGuid": "01a16290-f793-748b-a0b0-91d44be0d4ab", "countPerLaunch": 1000, "arrivalOutputs": []}` を足す

- [ ] **Step 4: 研究「軌道エレベーターの建設」と「反物質爆弾」を S2 に合わせる**

`$M/research.json`:
- 軌道エレベーターの建設（01a16291-…）: (a) `unlockBlock` から ロケット発射台 01a162b0-7769-7419-af24-bb35528b93d2 を、`unlockItemRecipeView` から ロケット筐体素材・ロケット燃料（GUID は items.json を name で引く）を外す。(b) `consumeItems` に `{"itemGuid": "01a167cf-af88-74d3-9cd6-7d61b71052bb", "itemCount": <S2 の計算データ仮値>}` を足す（S2 の plan `docs/superpowers/plans/` の計算データ量の表を読む。見つからなければ 200 とし、判断記録に「S2 の値待ち」と書く）。(c) `prevResearchNodeGuids` に `01a167cf-af9d-7f31-817c-3069b4f6e499` を足す
- 反物質爆弾（019e3aef-…）: `consumeItems` に 計算データ ×400（agent前提・S1/S2 待ち）を足す

- [ ] **Step 5: 整合ツール・シミュレーション・スモークを通して push する**

Task 1 Step 7 の4コマンド＋`python3 tools/balance/tier9_endgame_balance.py`＋スモーク（`MasterHolder.ItemMaster` からグラフェンケーブルのスタック上限が 1000 で引けること、S2 の積荷表の検証がマスタ読込で通ること）。
```bash
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame add server_v8
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame commit -m "data(v8): グラフェンケーブルを積荷表に載せ軌道エレベーターの建設をロケット技術の後ろにする"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame push
git push   # moorestech 側のマージコミット
```

### Task 6: スキーマ `SpaceElevatorBase` と拠点のマスタ

**Files:**
- Modify: `VanillaSchema/blocks.yml`（`blockType` の `options` と case）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（`dummyText`）
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/SpaceElevatorBaseParamValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/{blocks,items,buildMenu}.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`、`ForUnitTestItemId.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/SpaceElevator/SpaceElevatorBaseMasterValidationTest.cs`
- Modify（マスタ worktree）: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`、`localization/localization.csv`

**Interfaces:**
- Consumes: Task 5 の S2 取り込み（items.yml の `orbitalPayloads`、ビルドメニュー「生産>宇宙」）
- Produces:
  - 生成型 `Mooresmaster.Model.BlocksModule.SpaceElevatorBaseBlockParam`（`Guid[] CarriedPayloadItemGuids`, `Guid CableItemGuid`, `int CablesPerElevator`, `float ClimbSeconds`, `float RequiredPower`, `InventoryConnects InventoryConnectors`, `int MaxWireConnectionCount`, `int ConnectionRange`, `int ConnectionHeightRange`）と `BlockMasterElement.BlockTypeConst.SpaceElevatorBase`
  - `ForUnitTestModBlockId.TestSpaceElevatorBase`、`ForUnitTestItemId.TestElevatorCable` / `TestElevatorSatellite` / `TestElevatorReward`
  - テスト mod の値: 1エレベーター分 100 本、昇降 2 秒、電力 100、ケーブルの1便 50 本、衛星1機の成果 TestElevatorReward ×5

- [ ] **Step 1: スキーマに blockType と case を足す**

edit-schema スキルを読む。`VanillaSchema/blocks.yml` の `blockType` の `options:` の末尾（`- CleanRoomMachine` の次）に `- SpaceElevatorBase` を足す。case 群の末尾（`when: CleanRoomMachine` の case の後）に次を足す:
```yaml
      - when: SpaceElevatorBase
        type: object
        implementationInterface:
        - IInventoryConnectors
        - IElectricWireConnectParam
        properties:
        - key: carriedPayloadItemGuids
          type: array
          items:
            type: uuid
            foreignKey:
              schemaId: items
              foreignKeyIdPath: /data/[*]/itemGuid
              displayElementPath: /data/[*]/name
        - key: cableItemGuid
          type: uuid
          foreignKey:
            schemaId: items
            foreignKeyIdPath: /data/[*]/itemGuid
            displayElementPath: /data/[*]/name
        - key: cablesPerElevator
          type: integer
          default: 1000
        - key: climbSeconds
          type: number
          default: 600
        - key: requiredPower
          type: number
          default: 2000
        - key: inventoryConnectors
          ref: inventoryConnects
        - key: maxWireConnectionCount
          type: integer
          default: 2
        - key: connectionRange
          type: integer
          default: 30
        - key: connectionHeightRange
          type: integer
          default: 20
```
`_CompileRequester.cs` の `dummyText` の末尾の数字を1つ上げる（例 `...-00-04` → `...-00-05`。S1/S2 と衝突したら大きい方＋1）。既存の他ブロックの JSON は変えない（新しい case だけなので既存エントリに必須項目は増えない）。

- [ ] **Step 2: マスタ検証を書く**

`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/SpaceElevatorBaseParamValidator.cs`:
```csharp
using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    // 宇宙エレベーター拠点の参照と数値を検証する。軌道到着の表との突き合わせはケーブルプールの起動時に行う
    // Validates the space elevator base's references and numbers; the orbital-table cross-check happens when the cable pool boots
    internal static class SpaceElevatorBaseParamValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            var cableItemGuids = new HashSet<Guid>();
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not SpaceElevatorBaseBlockParam param) continue;
                cableItemGuids.Add(param.CableItemGuid);
                logs += ValidateBase(block.Name, param);
            }

            // プールはワールドに1つなので、全拠点が同じケーブルを数える
            // The pool is world-wide, so every base must count the same cable item
            if (cableItemGuids.Count > 1) logs += $"[BlockMaster] SpaceElevatorBase blocks must share one cableItemGuid. found:{string.Join(",", cableItemGuids)}\n";
            return logs;
        }

        private static string ValidateBase(string name, SpaceElevatorBaseBlockParam param)
        {
            var logs = "";
            if (MasterHolder.ItemMaster.GetItemIdOrNull(param.CableItemGuid) == null) logs += $"[BlockMaster] Name:{name} has invalid cableItemGuid:{param.CableItemGuid}\n";
            if (param.CarriedPayloadItemGuids.Length == 0) logs += $"[BlockMaster] Name:{name} carriedPayloadItemGuids must not be empty\n";
            foreach (var payloadGuid in param.CarriedPayloadItemGuids)
            {
                if (MasterHolder.ItemMaster.GetItemIdOrNull(payloadGuid) == null) logs += $"[BlockMaster] Name:{name} has invalid carriedPayloadItemGuid:{payloadGuid}\n";

                // ケーブルを昇降機で運べるとロケット無しでプールが増える
                // Carrying cables on the climber would grow the pool without rockets
                if (payloadGuid == param.CableItemGuid) logs += $"[BlockMaster] Name:{name} must not carry its own cable item:{payloadGuid}\n";
            }

            // 数値は正で有限。昇降時間は1秒以上（tick換算で0にならない）
            // Numbers must be positive and finite; the climb lasts at least one second so it never rounds to zero ticks
            if (param.CablesPerElevator < 1) logs += $"[BlockMaster] Name:{name} cablesPerElevator must be >= 1. value:{param.CablesPerElevator}\n";
            if (!(param.ClimbSeconds >= 1f) || float.IsInfinity(param.ClimbSeconds)) logs += $"[BlockMaster] Name:{name} climbSeconds must be finite and >= 1. value:{param.ClimbSeconds}\n";
            if (!(param.RequiredPower > 0f) || float.IsInfinity(param.RequiredPower)) logs += $"[BlockMaster] Name:{name} requiredPower must be finite and > 0. value:{param.RequiredPower}\n";
            return logs;
        }
    }
}
```
`BlockMasterUtil.Validate` の `errorLogs += BeltSpeedPerSecondValidator.Validate(blocks);` の次に `errorLogs += SpaceElevatorBaseParamValidator.Validate(blocks);` を足す。生成型の数値型が `float` でなく `double` なら `float.IsInfinity` を `double.IsInfinity` に、`1f`/`0f` を `1d`/`0d` に読み替える（生成コード `Mooresmaster.Model.BlocksModule.SpaceElevatorBaseBlockParam` を開いて確かめる）。

- [ ] **Step 3: テスト mod に拠点・アイテム・積荷行を足す**

テスト mod の `items.json` の `data` に3件を足す:
```json
{ "name": "TestElevatorCable", "itemGuid": "5e1ea7a0-0002-4000-8000-000000000001", "imagePath": "", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestElevatorSatellite", "itemGuid": "5e1ea7a0-0002-4000-8000-000000000002", "imagePath": "", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestElevatorReward", "itemGuid": "5e1ea7a0-0002-4000-8000-000000000003", "imagePath": "", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" }
```
同じファイルの `orbitalPayloads`（S2 が足した配列）に2行を足す:
```json
{ "payloadItemGuid": "5e1ea7a0-0002-4000-8000-000000000001", "countPerLaunch": 50, "arrivalOutputs": [] },
{ "payloadItemGuid": "5e1ea7a0-0002-4000-8000-000000000002", "countPerLaunch": 1, "arrivalOutputs": [{ "itemGuid": "5e1ea7a0-0002-4000-8000-000000000003", "count": 5 }] }
```
（キー名は S2 の `VanillaSchema/items.yml` で確かめ、違えば S2 の名前に合わせる）

`buildMenu.json` の `"name": "CleanRoomMachine"`（subCategoryGuid `a2000000-0000-4000-8000-000100000033`）の次に `{ "name": "SpaceElevatorBase", "entrySource": "blocks", "subCategoryGuid": "a2000000-0000-4000-8000-000100000034" }` を足す（`grep -n "000100000034" <buildMenu.json>` で未使用を確かめ、使用済みなら空いている番号にする）。

`blocks.json` の `data` に足す:
```json
{
  "blockGuid": "5e1ea7a0-0001-4000-8000-000000000001",
  "name": "TestSpaceElevatorBase",
  "blockType": "SpaceElevatorBase",
  "blockSize": [1, 1, 1],
  "blockPrefabAddressablesPath": "",
  "openable": true,
  "blockParam": {
    "carriedPayloadItemGuids": ["5e1ea7a0-0002-4000-8000-000000000002"],
    "cableItemGuid": "5e1ea7a0-0002-4000-8000-000000000001",
    "cablesPerElevator": 100,
    "climbSeconds": 2,
    "requiredPower": 100,
    "inventoryConnectors": {
      "inputConnects": [{ "connectorGuid": "5e1ea7a0-0003-4000-8000-000000000001", "offset": [0, 0, 0], "directions": [[1, 0, 0], [-1, 0, 0], [0, 0, 1], [0, 0, -1]] }],
      "outputConnects": [{ "connectorGuid": "5e1ea7a0-0003-4000-8000-000000000002", "offset": [0, 0, 0], "directions": [[1, 0, 0], [-1, 0, 0], [0, 0, 1], [0, 0, -1]] }]
    },
    "maxWireConnectionCount": 2,
    "connectionRange": 30,
    "connectionHeightRange": 20
  },
  "category": "テスト",
  "subCategory": "SpaceElevatorBase",
  "placementsPerCost": 1
}
```
`ForUnitTestModBlockId.cs` に `public static BlockId TestSpaceElevatorBase => GetBlock("5e1ea7a0-0001-4000-8000-000000000001");`、`ForUnitTestItemId.cs` に次を足す:
```csharp
public static ItemId TestElevatorCable => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5e1ea7a0-0002-4000-8000-000000000001"));
public static ItemId TestElevatorSatellite => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5e1ea7a0-0002-4000-8000-000000000002"));
public static ItemId TestElevatorReward => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5e1ea7a0-0002-4000-8000-000000000003"));
```
`ForUnitTestModBlockId.cs` が 200 行以上になる場合は、宇宙エレベーターの ID だけ `Tests.Module/TestMod/ForUnitTestSpaceElevatorId.cs`（`public static class ForUnitTestSpaceElevatorId`）へ分けて置き、以降のタスクの `ForUnitTestModBlockId.TestSpaceElevatorBase` をそちらの名前に読み替える。

- [ ] **Step 4: マスタ検証のテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/SpaceElevator/SpaceElevatorBaseMasterValidationTest.cs`（前例 `Tests/UnitTest/Core/Block/EnergyConversion/GearToElectricMasterValidationTest.cs` と同形）:
```csharp
using System;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block.SpaceElevator
{
    public class SpaceElevatorBaseMasterValidationTest
    {
        private const string BaseGuid = "5e1ea7a0-0001-4000-8000-000000000001";
        private const string CableGuid = "5e1ea7a0-0002-4000-8000-000000000001";
        private MasterJsonFileContainer _original;

        [SetUp]
        public void Setup()
        {
            _original = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(CreateResource()));
            MasterHolder.Load(_original);
        }

        [TearDown]
        public void RestoreMaster()
        {
            MasterHolder.Load(_original);
        }

        [Test]
        public void テストmodの拠点はそのまま読める()
        {
            var param = (Mooresmaster.Model.BlocksModule.SpaceElevatorBaseBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase).BlockParam;
            Assert.AreEqual(100, param.CablesPerElevator);
        }

        [Test]
        public void 運ぶ積荷にケーブルを入れるとマスタ読込で止まる()
        {
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(CreateWith(param => param["carriedPayloadItemGuids"] = new JArray(CableGuid))));
            StringAssert.Contains("must not carry its own cable item", error.Message);
        }

        [TestCase("cablesPerElevator", 0, "cablesPerElevator must be >= 1")]
        [TestCase("climbSeconds", 0, "climbSeconds must be finite and >= 1")]
        [TestCase("requiredPower", 0, "requiredPower must be finite and > 0")]
        public void 数値が範囲外だとマスタ読込で止まる(string key, int value, string message)
        {
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(CreateWith(param => param[key] = value)));
            StringAssert.Contains(message, error.Message);
        }

        private static MasterJsonFileContainer CreateWith(Action<JObject> edit)
        {
            // JSON境界で拠点のパラメータを書き換え、通常ロードの検証器まで通す
            // Rewrite the base's param at the JSON boundary and run the normal loader's validator
            var configs = ModJsonStringLoader.GetMasterString(CreateResource());
            var fileName = new JsonFileName("blocks");
            var json = JObject.Parse(configs[0].JsonContents[fileName]);
            var elevatorBase = json["data"].Single(block => (string)block["blockGuid"] == BaseGuid);
            edit((JObject)elevatorBase["blockParam"]);
            configs[0].JsonContents[fileName] = json.ToString();
            return new MasterJsonFileContainer(configs);
        }

        private static ModsResource CreateResource()
        {
            return new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
        }
    }
}
```
（`Action<JObject>` は `Func<>` ではないので可。前例の例外型が `InvalidOperationException` でなければ前例に合わせる）

- [ ] **Step 5: 出荷マスタの拠点を本物にする**

マスタ worktree の `blocks.json` の 宇宙エレベーター拠点（01a162aa-cec0-738c-93c5-f58adc87fa7f）の仮エントリを次の全文で置き換える:
```json
{
  "blockGuid": "01a162aa-cec0-738c-93c5-f58adc87fa7f",
  "name": "宇宙エレベーター拠点",
  "blockType": "SpaceElevatorBase",
  "blockSize": [9, 4, 9],
  "blockPrefabAddressablesPath": "Vanilla/Block/SpaceElevatorBase",
  "openable": true,
  "blockParam": {
    "carriedPayloadItemGuids": [
      "01a167cf-af8b-7894-8de6-593ee30289c0",
      "01a167cf-af8e-7e95-b299-09eb712bee12",
      "01a167cf-af91-7f62-93d1-e499b06f2398",
      "01a167cf-af94-7e25-9654-825374c3923b",
      "01a167cf-af97-7f67-bf53-7833b8d17a15"
    ],
    "cableItemGuid": "01a16290-f793-748b-a0b0-91d44be0d4ab",
    "cablesPerElevator": 1000,
    "climbSeconds": 600,
    "requiredPower": 2000,
    "inventoryConnectors": {
      "inputConnects": [{ "connectorGuid": "01a167e9-21d6-7e9d-89c4-70dc3df73072", "offset": [4, 0, 0], "directions": [[0, 0, -1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" }],
      "outputConnects": [{ "connectorGuid": "01a167e9-21d9-7b56-9484-4fb75fd18a37", "offset": [4, 0, 8], "directions": [[0, 0, 1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" }]
    },
    "maxWireConnectionCount": 4,
    "connectionRange": 30,
    "connectionHeightRange": 20
  },
  "initialUnlocked": false,
  "requiredItems": [
    { "itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 200 },
    { "itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 50 },
    { "itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 50 },
    { "itemGuid": "231d2146-5e40-4e55-be27-22dda4175f47", "count": 100 }
  ],
  "placementsPerCost": 1,
  "sortPriority": 1210,
  "category": "生産",
  "subCategory": "宇宙"
}
```
（1エレベーター分 1000 本＝ケーブル1便。昇降 600 秒・電力 2000 は agent前提で Task 12 が S2 のロケット建造時間と比べて確定する）
`localization.csv` に `block.01a162aa-cec0-738c-93c5-f58adc87fa7f.name,宇宙エレベーター拠点,Space Elevator Base,宇宙エレベーター拠点,Weltraumaufzug-Basis,우주 엘리베이터 기지` が無ければ足す。

- [ ] **Step 6: 生成・コンパイル・テスト・整合ツール**

Run: `uloop compile --project-path ./moorestech_client`（ErrorCount 0。生成型 `SpaceElevatorBaseBlockParam` が見えること）→ `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpaceElevatorBaseMasterValidationTest|GearToElectricMasterValidationTest|Orbit"`
Expected: 全 PASS。マスタ worktree で Task 1 Step 7 の4コマンドが exit 0。スモーク（Task 1 Step 8 の dynamic code を `MasterHolder.BlockMaster.GetBlockMaster(Guid.Parse("01a162aa-cec0-738c-93c5-f58adc87fa7f")).BlockType` を出す形にして実行）で `SpaceElevatorBase` が出る。

- [ ] **Step 7: コミットして push する**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module/TestMod moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/SpaceElevator
git commit -m "feat(schema): 宇宙エレベーター拠点のブロック種別とマスタ検証を足す"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame add server_v8
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame commit -m "data(v8): 宇宙エレベーター拠点を SpaceElevatorBase にする"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame push
```
（Unity が生成した新ファイルの .meta も `git status` で確かめて一緒に add する）

### Task 7: ケーブルプールとワールドセーブ（版 7→8）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator/ISpaceElevatorBase.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/Game.SpaceElevator.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorCablePool.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorConnectionService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorConnectResult.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/Save/SpaceElevatorCablePoolSaveData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`、`moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`、`moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef`（`Game.SpaceElevator` を参照に足す）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveDataRestorer.cs`
- Create（行数次第）: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveRestoreGuards.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV7ToV8.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`（移行チェーン3箇所）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/GameplayServiceRegistration.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV7ToV8Test.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorCablePoolTest.cs`

**Interfaces:**
- Consumes: S2 `IOrbitalArrivalService`（`Arrive`, `IsOrbitalPayload`, `OnOrbitalArrival`）、`OrbitalArrival.PayloadItemId` / `PayloadCount`、Task 6 の `SpaceElevatorBaseBlockParam`
- Produces:
  - `ISpaceElevatorBase : IBlockComponent`（`bool IsConnected { get; }`, `void MarkConnected()`, `void SetCablePoolAmount(int amount)`）
  - `SpaceElevatorCablePool : IBootInitializable`（`int Amount { get; private set; }`, `IObservable<int> OnAmountChanged`, `internal bool TryTake(int count)`, `internal void Add(int count)`, `SpaceElevatorCablePoolSaveData GetSaveData()`, `void Restore(SpaceElevatorCablePoolSaveData saveData)`）
  - `SpaceElevatorConnectionService : IBootInitializable`（`SpaceElevatorConnectResult Connect(IBlock block)`）
  - `enum SpaceElevatorConnectResult { Connected, AlreadyConnected, InsufficientCable, NotSpaceElevatorBase }`
  - `SpaceElevatorCablePoolSaveData`（`[JsonProperty("cableCount")] public int CableCount;`）、セーブの節名 `spaceElevatorCablePool`、`WorldSaveAllInfo.CurrentVersion == 8`、`SaveMigrationStepV7ToV8`

設計の要点（ADR 0089「呼び出し側の線引き」）: 拠点はプールの残量も引き算も知らない。接続の判定（残量 ≥ 1エレベーター分）と引き算・撤去の返却はすべて `SpaceElevatorConnectionService` が持ち、プール本体の `TryTake`/`Add` は同アセンブリ内だけに閉じる（`internal`）。外から見えるのは `Connect` の結果 enum だけ。

- [ ] **Step 1: 拠点の面とプール・接続サービスを書く**

`Game.Block.Interface/Component/SpaceElevator/ISpaceElevatorBase.cs`:
```csharp
namespace Game.Block.Interface.Component.SpaceElevator
{
    // 接続サービスが拠点に触る面。接続済みかの読み書きとプール残量の受け取りだけを持つ
    // The surface the connection service touches on a base: read and set the connected flag and receive the pool amount
    public interface ISpaceElevatorBase : IBlockComponent
    {
        bool IsConnected { get; }
        void MarkConnected();
        void SetCablePoolAmount(int amount);
    }
}
```

`Game.SpaceElevator/Game.SpaceElevator.asmdef`:
```json
{
  "name": "Game.SpaceElevator",
  "rootNamespace": "",
  "references": [
    "Game.Block.Interface",
    "Game.World.Interface",
    "Core.Master",
    "Core.Item.Interface",
    "Game.Context",
    "Game.Orbit",
    "UniRx"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`Game.SpaceElevator/SpaceElevatorConnectResult.cs`:
```csharp
namespace Game.SpaceElevator
{
    public enum SpaceElevatorConnectResult
    {
        Connected,
        AlreadyConnected,
        InsufficientCable,
        NotSpaceElevatorBase,
    }
}
```

`Game.SpaceElevator/Save/SpaceElevatorCablePoolSaveData.cs`:
```csharp
using Newtonsoft.Json;

namespace Game.SpaceElevator.Save
{
    public class SpaceElevatorCablePoolSaveData
    {
        [JsonProperty("cableCount")] public int CableCount;
    }
}
```

`Game.SpaceElevator/SpaceElevatorCablePool.cs`:
```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator.Save;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.SpaceElevator
{
    /// <summary>
    ///     ワールドに1つのケーブルプール。ケーブルの軌道到着で増え、接続で減り、接続済み拠点の撤去で戻る（ADR 0089）
    ///     The world's single cable pool: grows on cable orbital arrivals, shrinks on connections, refills when a connected base is removed (ADR 0089)
    /// </summary>
    public class SpaceElevatorCablePool : IBootInitializable
    {
        public int Amount { get; private set; }
        public IObservable<int> OnAmountChanged => _onAmountChanged;

        private readonly Subject<int> _onAmountChanged = new();
        private readonly IOrbitalArrivalService _orbitalArrivalService;

        // 拠点のマスタが数えるケーブル。検証で高々1種に揃う。拠点の無いマスタでは空で何も数えない
        // Cable items the base masters count; validation keeps it to at most one, and a master without bases leaves it empty
        private readonly HashSet<ItemId> _cableItemIds = new();

        public SpaceElevatorCablePool(IOrbitalArrivalService orbitalArrivalService)
        {
            _orbitalArrivalService = orbitalArrivalService;
        }

        public void Load()
        {
            // 拠点のマスタを軌道到着の表と突き合わせ、合わなければ起動を止める
            // Check base masters against the orbital table and stop the boot on a mismatch
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (block.BlockParam is not SpaceElevatorBaseBlockParam param) continue;
                ThrowIfNotOrbitalPayload(block.Name, param.CableItemGuid);
                foreach (var payloadGuid in param.CarriedPayloadItemGuids) ThrowIfNotOrbitalPayload(block.Name, payloadGuid);
                _cableItemIds.Add(MasterHolder.ItemMaster.GetItemId(param.CableItemGuid));
            }

            // ケーブルの到着だけを数える。どの打ち上げ口から来たかは見ない
            // Count only cable arrivals, regardless of which launch site sent them
            _orbitalArrivalService.OnOrbitalArrival.Subscribe(arrival =>
            {
                if (!_cableItemIds.Contains(arrival.PayloadItemId)) return;
                Add(arrival.PayloadCount);
            });

            #region Internal

            void ThrowIfNotOrbitalPayload(string blockName, Guid itemGuid)
            {
                if (_orbitalArrivalService.IsOrbitalPayload(MasterHolder.ItemMaster.GetItemId(itemGuid))) return;
                var reason = $"宇宙エレベーター拠点 {blockName} のアイテム {itemGuid} が軌道到着の表（items の orbitalPayloads）にありません";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }

            #endregion
        }

        // 足りれば引いてtrue。足りなければ何もしない（判定は接続サービスだけが使う）
        // Take the count and return true when enough remains; otherwise change nothing (only the connection service uses this)
        internal bool TryTake(int count)
        {
            if (Amount < count) return false;
            SetAmount(Amount - count);
            return true;
        }

        internal void Add(int count)
        {
            SetAmount(Amount + count);
        }

        public SpaceElevatorCablePoolSaveData GetSaveData()
        {
            return new SpaceElevatorCablePoolSaveData { CableCount = Amount };
        }

        public void Restore(SpaceElevatorCablePoolSaveData saveData)
        {
            // 負の残量は手編集による破損。黙って0にせず止める
            // A negative amount means a hand-edited save; stop instead of silently clamping to zero
            if (saveData.CableCount < 0)
            {
                var reason = $"セーブの spaceElevatorCablePool.cableCount が負です value:{saveData.CableCount}";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }
            SetAmount(saveData.CableCount);
        }

        private void SetAmount(int amount)
        {
            Amount = amount;
            _onAmountChanged.OnNext(amount);
        }
    }
}
```
`MasterHolder.BlockMaster.Blocks.Data` の実名は `Core.Master/BlockMaster.cs` で確かめる（全ブロックを列挙するプロパティ名に合わせる）。

`Game.SpaceElevator/SpaceElevatorConnectionService.cs`:
```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component.SpaceElevator;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.SpaceElevator
{
    /// <summary>
    ///     拠点の接続・撤去時の返却・残量の配信を受け持つ。拠点はプールの残量も1エレベーター分の引き算も知らない
    ///     Owns connecting, the refund on removal and pushing the pool amount; bases know neither the pool amount check nor the per-elevator subtraction
    /// </summary>
    public class SpaceElevatorConnectionService : IBootInitializable
    {
        private readonly SpaceElevatorCablePool _pool;
        private readonly Dictionary<BlockInstanceId, ISpaceElevatorBase> _bases = new();

        public SpaceElevatorConnectionService(SpaceElevatorCablePool pool)
        {
            _pool = pool;
        }

        public void Load()
        {
            // ワールドロードより前に購読し、ロードで置かれる拠点も拾う
            // Subscribe before the world loads so bases placed by the load are tracked too
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(properties => OnBlockPlaced(properties.BlockData.Block));
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties => OnBlockRemoved(properties.BlockData.Block));
            _pool.OnAmountChanged.Subscribe(PushPoolAmount);
        }

        public SpaceElevatorConnectResult Connect(IBlock block)
        {
            if (!block.ComponentManager.TryGetComponent<ISpaceElevatorBase>(out var elevatorBase)) return SpaceElevatorConnectResult.NotSpaceElevatorBase;
            if (elevatorBase.IsConnected) return SpaceElevatorConnectResult.AlreadyConnected;

            // 1エレベーター分が残っていれば引いて接続する
            // Connect after taking one elevator's worth when it remains
            if (!_pool.TryTake(CablesPerElevator(block))) return SpaceElevatorConnectResult.InsufficientCable;
            elevatorBase.MarkConnected();
            return SpaceElevatorConnectResult.Connected;
        }

        private void OnBlockPlaced(IBlock block)
        {
            if (!block.ComponentManager.TryGetComponent<ISpaceElevatorBase>(out var elevatorBase)) return;
            _bases[block.BlockInstanceId] = elevatorBase;
            elevatorBase.SetCablePoolAmount(_pool.Amount);
        }

        private void OnBlockRemoved(IBlock block)
        {
            if (!_bases.Remove(block.BlockInstanceId, out var elevatorBase)) return;

            // 接続済みの拠点だけが1エレベーター分をプールへ戻す。撤去イベントはDestroyより前に来るので状態を読める
            // Only a connected base returns one elevator's worth; the remove event fires before Destroy, so its state is still readable
            if (elevatorBase.IsConnected) _pool.Add(CablesPerElevator(block));
        }

        private void PushPoolAmount(int amount)
        {
            foreach (var elevatorBase in _bases.Values) elevatorBase.SetCablePoolAmount(amount);
        }

        private static int CablesPerElevator(IBlock block)
        {
            return ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).CablesPerElevator;
        }
    }
}
```

`GameplayServiceRegistration.cs` の `IOrbitalArrivalService` の登録（S2 が足した行）の次に足す:
```csharp
            // 宇宙エレベーターのプールと接続。起動時に購読を張るので具象型で登録する
            // Space elevator pool and connections; registered by concrete type so their boot-time subscriptions run
            services.AddSingleton<SpaceElevatorCablePool>();
            services.AddSingleton<SpaceElevatorConnectionService>();
```
（`using Game.SpaceElevator;`。`IBootInitializable` への転送は `SaveAndEventServiceRegistration` の `AddInitializableForwarding()` が行う）
`Game.SaveLoad.asmdef`・`Server.Boot.asmdef`・`Server.Tests.asmdef` の `references` に `"Game.SpaceElevator"` を足す（`Game.Orbit` が無ければそれも）。

- [ ] **Step 2: ワールドセーブに節を足し版を 8 にする**

`WorldSaveAllInfo.cs`: `CurrentVersion` を `8` にする。ctor の最後の引数（S2 が足した引数の後ろ）に `SpaceElevatorCablePoolSaveData spaceElevatorCablePool` を足し、本体で `SpaceElevatorCablePool = spaceElevatorCablePool;`、プロパティ `[JsonProperty("spaceElevatorCablePool")] public SpaceElevatorCablePoolSaveData SpaceElevatorCablePool { get; }` を足す（`using Game.SpaceElevator.Save;`。欠損は null のまま残し、復元側で止める）。
`AssembleSaveJsonText.cs`: ctor に `SpaceElevatorCablePool spaceElevatorCablePool` を足して `_spaceElevatorCablePool` に保持し、`Capture()` の `new WorldSaveAllInfo(...)` の最後の引数に `_spaceElevatorCablePool.GetSaveData()` を渡す。
`WorldSaveDataRestorer.cs`: ctor に `SpaceElevatorCablePool spaceElevatorCablePool` を足す。`Restore` の `_worldBlockDatastore.LoadBlockDataList(load.World);` の直前に:
```csharp
            // ケーブルプールはブロックより先に戻し、ロードで置かれる拠点が設置時に残量を受け取れるようにする
            // Restore the cable pool before blocks so bases placed by the load receive the amount when placed
            _spaceElevatorCablePool.Restore(load.SpaceElevatorCablePool);
```
`ThrowIfRequiredFieldMissing` に `if (load.SpaceElevatorCablePool == null) missing.Add("spaceElevatorCablePool");` を足す。`wc -l` で 200 行以上になったら、`ThrowIfRequiredFieldMissing` と `WarnIfRandomStreamAdvanced` を `Game.SaveLoad/Json/WorldSaveRestoreGuards.cs` の `internal static class WorldSaveRestoreGuards` の `internal static void ThrowIfRequiredFieldMissing(WorldSaveAllInfo load)` / `internal static void WarnIfRandomStreamAdvanced(ulong[] savedState)` へ本体ごと移し、`Restore` からはそれを呼ぶ（S2 が既に同じ分割をしていればそれに足す）。
`MissingMasterPruner.cs` の `SectionsWithoutMasterPruning` の「マスタguidを持たない」行に `"spaceElevatorCablePool"` を足す（残量の数だけでマスタ guid を持たない）。

- [ ] **Step 3: 移行ステップを書いて3箇所に登録する**

moorestech-save-migration スキルを読む。`Game.SaveLoad/Migration/Steps/SaveMigrationStepV7ToV8.cs`:
```csharp
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V7→V8移行: 宇宙エレベーターのケーブルプールの節を0で足す。版7まではケーブルを軌道へ運ぶ積荷表の行が無く、0が実値である
    // V7→V8 migration: add the space elevator cable pool section at zero; up to version 7 no payload row could carry cables to orbit, so zero is the real value
    public sealed class SaveMigrationStepV7ToV8 : ISaveMigrationStep
    {
        private const string PoolKey = "spaceElevatorCablePool";
        private const string CountKey = "cableCount";

        public int FromVersion => 7;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // 既にあれば形だけ確かめて素通しする（冪等）
            // When present, only check the shape and pass it through (idempotent)
            var existing = save[PoolKey];
            if (existing != null)
            {
                if (existing is JObject pool && pool[CountKey]?.Type == JTokenType.Integer && pool[CountKey].Value<int>() >= 0) return SaveMigrationStepResult.Converted(save);
                return Fail($"{PoolKey} が0以上の {CountKey} を持つオブジェクトではありません。 value={existing}");
            }

            save[PoolKey] = new JObject { [CountKey] = 0 };
            Debug.Log("セーブを版7から版8へ変換しました。宇宙エレベーターのケーブルプールを0で足しました");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版7から版8へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
```
`backfilledFields` には足さない（0 は補填値ではなく実値）。`SaveAndEventServiceRegistration.cs`・`SaveLoadPreparerTestFixture.cs`・`SaveMigrationChainTest.cs` の3箇所の `new ISaveMigrationStep[] { ... }` の末尾（S2 の `SaveMigrationStepV6ToV7` の次）に `new SaveMigrationStepV7ToV8()` を足す。

- [ ] **Step 4: テストを書く**

`Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV7ToV8Test.cs`:
```csharp
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV7ToV8Test
    {
        [Test]
        public void 節が無ければ0で足す()
        {
            var save = JObject.Parse("{\"worldVersion\":7,\"world\":[]}");
            var result = new SaveMigrationStepV7ToV8().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
            Assert.IsNull(result.Save["backfilledFields"]);
        }

        [Test]
        public void 正しい節があれば素通しする()
        {
            var save = JObject.Parse("{\"worldVersion\":7,\"spaceElevatorCablePool\":{\"cableCount\":250}}");
            var result = new SaveMigrationStepV7ToV8().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(250, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
        }

        [TestCase("{\"spaceElevatorCablePool\":5}")]
        [TestCase("{\"spaceElevatorCablePool\":{\"cableCount\":\"x\"}}")]
        [TestCase("{\"spaceElevatorCablePool\":{\"cableCount\":-1}}")]
        public void 形の違う節は変換を拒む(string json)
        {
            LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));
            var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse(json));
            Assert.IsFalse(result.IsConverted);
        }

        [Test]
        public void FromVersionは7()
        {
            Assert.AreEqual(7, new SaveMigrationStepV7ToV8().FromVersion);
        }
    }
}
```

`Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorCablePoolTest.cs`:
```csharp
using System;
using Game.Context;
using Game.Orbit;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SpaceElevator;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorCablePoolTest
    {
        [Test]
        public void ケーブルの到着だけがプールを増やす()
        {
            var provider = CreateServer();
            var orbital = provider.GetService<IOrbitalArrivalService>();
            var pool = provider.GetService<SpaceElevatorCablePool>();

            orbital.Arrive(ForUnitTestItemId.TestElevatorCable, 50);
            orbital.Arrive(ForUnitTestItemId.TestElevatorSatellite, 1);

            Assert.AreEqual(50, pool.Amount);
        }

        [Test]
        public void 残量はセーブを往復する()
        {
            var provider = CreateServer();
            provider.GetService<IOrbitalArrivalService>().Arrive(ForUnitTestItemId.TestElevatorCable, 100);
            var json = provider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 別のDIで読み直すと残量が戻る
            // Reloading through a fresh container restores the amount
            var loadedProvider = CreateServer();
            ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(json);
            Assert.AreEqual(100, loadedProvider.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void 現在版のセーブに節が無ければロードで止まる()
        {
            var provider = CreateServer();
            var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            save.Remove("spaceElevatorCablePool");

            var loadedProvider = CreateServer();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("spaceElevatorCablePool"));
            Assert.Throws<InvalidOperationException>(() => ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(save));
        }

        private static ServiceProvider CreateServer()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return provider;
        }
    }
}
```
（`IWorldSaveDataLoader` の名前空間と `Load(JObject)` の有無は `Tests/CombinedTest/Core/CleanRoom/CleanRoomSaveLoadTest.cs:132-144` のヘルパーに合わせる）

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpaceElevatorCablePoolTest|SaveMigrationStepV7ToV8Test|SaveMigrationChainTest|MissingMasterSectionCoverageTest|SaveLoad"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 6: 実ロード検証**

moorestech-save-migration スキルの `references/load_test.cs` の手順で、S2 の版7セーブ（S2 の移行テストの入力か、S2 の plan が置いたサンプル）を版8へ読み込めることを `uloop execute-dynamic-code` で確かめ、出力（変換ログの行）を判断記録に貼る。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator moorestech_server/Assets/Scripts/Game.SpaceElevator moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests
git commit -m "feat(space-elevator): ケーブルプールと接続サービスを足しセーブを版8にする"
```

### Task 8: 拠点ブロック（インベントリ・昇降機・電力・状態・撤去返却）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorBaseInventoryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorClimberComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorClimberSaveJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorBaseElectricComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorBaseStateDetail.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorBaseStatus.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/SpaceElevator/VanillaSpaceElevatorBaseTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef`（`Game.Orbit` が無ければ足す）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`（S2 が直していなければ）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorConnectionTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorClimbTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorBaseSaveAndRefundTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorTestUtil.cs`

**Interfaces:**
- Consumes: Task 7 の `ISpaceElevatorBase`・`SpaceElevatorConnectionService.Connect(IBlock)`、S2 の `IOrbitalArrivalService.Arrive`
- Produces:
  - `SpaceElevatorBaseStateDetail`（MessagePack、キー `"SpaceElevatorBase"`。`[Key(0)] bool IsConnected`、`[Key(1)] int CablePoolAmount`、`[Key(2)] int CablesPerElevator`、`[Key(3)] float ClimbProgress`、`[Key(4)] SpaceElevatorBaseStatus Status`）
  - `enum SpaceElevatorBaseStatus { NotConnected, WaitingSatellite, Climbing, LowPower, OutputBlocked }`
  - インベントリのスロット: 0＝衛星の入口（運ぶ積荷だけ）、1＝成果の出口（取り出しのみ）。`SpaceElevatorBaseInventoryComponent.SatelliteSlot = 0`、`OutputSlot = 1`
  - `RemoveBlockProtocol` は `IGetRefundItemsInfo` を全コンポーネントから合算する

- [ ] **Step 1: 撤去返却の合算を確かめて直す**

`grep -n "IGetRefundItemsInfo" moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`。`TryGetComponent(out IGetRefundItemsInfo refundInfo)` のままなら（S2 未修正）、次へ置き換える（拠点はワイヤー端点と昇降機の2つが `IGetRefundItemsInfo` を持つため、先頭1件だけでは衛星・保留が返らない）:
```csharp
                // 返却すべきアイテム情報を全コンポーネントから集める（ワイヤー端点と本体の両方が持つブロックがある）
                // Gather refundable items from every component; some blocks carry both a wire connector and a body that refund
                foreach (var refundInfo in block.ComponentManager.GetComponents<IGetRefundItemsInfo>())
                {
                    result.AddRange(refundInfo.GetRefundItems());
                }
```
（`GetComponents<T>` の実名は `Game.Block.Interface/BlockComponentManager.cs` で確かめる。S2 が既に直していれば何もしない）

- [ ] **Step 2: 状態・セーブの型を書く**

`SpaceElevatorBaseStatus.cs`:
```csharp
namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点の今の状態。待ちの理由もここで表す
    // The base's current state, including why it is waiting
    public enum SpaceElevatorBaseStatus
    {
        NotConnected,
        WaitingSatellite,
        Climbing,
        LowPower,
        OutputBlocked,
    }
}
```
`SpaceElevatorBaseStateDetail.cs`（前例 `ElectricToGearGeneratorBlockStateDetail.cs`）:
```csharp
using System;
using MessagePack;

namespace Game.Block.Blocks.SpaceElevator
{
    [Serializable]
    [MessagePackObject]
    public class SpaceElevatorBaseStateDetail
    {
        public const string BlockStateDetailKey = "SpaceElevatorBase";

        [Key(0)] public bool IsConnected { get; set; }
        [Key(1)] public int CablePoolAmount { get; set; }
        [Key(2)] public int CablesPerElevator { get; set; }
        // 昇降の進み（0〜1）。昇っていなければ0
        // Climb progress from 0 to 1; zero while not climbing
        [Key(3)] public float ClimbProgress { get; set; }
        [Key(4)] public SpaceElevatorBaseStatus Status { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SpaceElevatorBaseStateDetail()
        {
        }

        public SpaceElevatorBaseStateDetail(bool isConnected, int cablePoolAmount, int cablesPerElevator, float climbProgress, SpaceElevatorBaseStatus status)
        {
            IsConnected = isConnected;
            CablePoolAmount = cablePoolAmount;
            CablesPerElevator = cablesPerElevator;
            ClimbProgress = climbProgress;
            Status = status;
        }
    }
}
```
`SpaceElevatorClimberSaveJsonObject.cs`:
```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.SpaceElevator
{
    public class SpaceElevatorClimberSaveJsonObject
    {
        [JsonProperty("isConnected")] public bool IsConnected;
        // 昇降中の衛星。昇っていなければ空スタック
        // The satellite being lifted; an empty stack while not climbing
        [JsonProperty("climbingSatellite")] public ItemStackSaveJsonObject ClimbingSatellite;
        [JsonProperty("remainingClimbTicks")] public uint RemainingClimbTicks;
        [JsonProperty("pendingOutputs")] public List<ItemStackSaveJsonObject> PendingOutputs;
    }
}
```

- [ ] **Step 3: インベントリを書く**

`SpaceElevatorBaseInventoryComponent.cs`（前例 `Game.Block/Blocks/Chest/VanillaChestComponent.cs`）:
```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.SpaceElevator
{
    /// <summary>
    ///     拠点のスロット。0は運ぶ衛星の入口、1は成果の出口。出口だけを隣のベルトへ押し出す
    ///     The base's slots: slot 0 takes satellites to carry, slot 1 holds results; only slot 1 is pushed to adjacent belts
    /// </summary>
    public class SpaceElevatorBaseInventoryComponent : IOpenableBlockInventoryComponent, IBlockSaveState, IUpdatableBlockComponent
    {
        public const int SatelliteSlot = 0;
        public const int OutputSlot = 1;
        private const int SlotCount = 2;

        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        public BlockInstanceId BlockInstanceId { get; }
        public string SaveKey { get; } = typeof(SpaceElevatorBaseInventoryComponent).FullName;

        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly HashSet<ItemId> _carriedPayloadIds = new();

        public SpaceElevatorBaseInventoryComponent(BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryInserter = blockInventoryInserter;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, SlotCount);
            foreach (var payloadGuid in param.CarriedPayloadItemGuids) _carriedPayloadIds.Add(MasterHolder.ItemMaster.GetItemId(payloadGuid));
        }

        public SpaceElevatorBaseInventoryComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter) :
            this(blockInstanceId, param, blockInventoryInserter)
        {
            // ロード時はまだワールドに登録されていないのでイベントを出さない
            // Loading does not fire events because the block is not registered in the world yet
            var itemJsons = BlockComponentStateReader.Read<List<ItemStackSaveJsonObject>>(componentStates, SaveKey);
            for (var i = 0; i < SlotCount && i < itemJsons.Count; i++) _itemDataStoreService.SetItemWithoutEvent(i, itemJsons[i].ToItemStack());
        }

        // 昇降機が衛星を1つ取り出す。入口が空なら空スタックでfalse
        // The climber takes one satellite; returns false with an empty stack when the inlet is empty
        public bool TryTakeSatellite(out IItemStack satellite)
        {
            CheckDestroy(this);
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            satellite = ServerContext.ItemStackFactory.Create(stack.Id, stack.Count == 0 ? 0 : 1);
            if (stack.Count == 0) return false;
            _itemDataStoreService.SetItem(SatelliteSlot, stack.SubItem(1));
            return true;
        }

        // 成果を出口へ入れ、入りきらない分を返す
        // Put results into the outlet and return what did not fit
        public IItemStack InsertOutput(IItemStack itemStack)
        {
            CheckDestroy(this);
            var result = _itemDataStoreService.GetItem(OutputSlot).AddItem(itemStack);
            _itemDataStoreService.SetItem(OutputSlot, result.ProcessResultItemStack);
            return result.RemainderItemStack;
        }

        public void Update()
        {
            CheckDestroy(this);
            var output = _itemDataStoreService.GetItem(OutputSlot);
            if (output.Count == 0) return;
            _itemDataStoreService.SetItem(OutputSlot, _blockInventoryInserter.InsertItem(output));
        }

        // ベルト・プレイヤーからは運ぶ積荷だけを入口へ受ける
        // Belts and players may only put carried payloads into the inlet
        public IItemStack InsertItem(IItemStack itemStack)
        {
            CheckDestroy(this);
            // silent-ok: 積荷以外はベルト上に残す通常の受け入れ拒否 / プレイヤーはベルトの詰まりと拠点UIの入口で気づく
            if (!_carriedPayloadIds.Contains(itemStack.Id)) return itemStack;
            var result = _itemDataStoreService.GetItem(SatelliteSlot).AddItem(itemStack);
            _itemDataStoreService.SetItem(SatelliteSlot, result.ProcessResultItemStack);
            return result.RemainderItemStack;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            var slot = _itemDataStoreService.GetItem(SatelliteSlot);
            foreach (var itemStack in itemStacks)
            {
                if (!_carriedPayloadIds.Contains(itemStack.Id)) return false;
                var result = slot.AddItem(itemStack);
                if (result.RemainderItemStack.Count > 0) return false;
                slot = result.ProcessResultItemStack;
            }
            return true;
        }

        // 入口は積荷だけ、出口は取り出し（空にする）だけを許す
        // The inlet accepts only payloads and the outlet only taking out (emptying)
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            if (itemStack.Count == 0) return true;
            return slot == SatelliteSlot && _carriedPayloadIds.Contains(itemStack.Id);
        }

        public object GetSaveState()
        {
            CheckDestroy(this);
            var itemJson = new List<ItemStackSaveJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems) itemJson.Add(new ItemStackSaveJsonObject(item));
            return itemJson;
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) { return InsertItem(itemStack); }
        public IItemStack InsertItem(ItemId itemId, int count) { return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count)); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { var remainders = new List<IItemStack>(); foreach (var itemStack in itemStacks) remainders.Add(InsertItem(itemStack)); return remainders; }
        public void SetItem(int slot, IItemStack itemStack) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemStack); }
        public void SetItem(int slot, ItemId itemId, int count) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemId, count); }
        public IItemStack GetItem(int slot) { CheckDestroy(this); return _itemDataStoreService.GetItem(slot); }
        public int GetSlotSize() { CheckDestroy(this); return _itemDataStoreService.GetSlotSize(); }
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { CheckDestroy(this); return _itemDataStoreService.CreateCopiedItems(); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemId, count); }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

- [ ] **Step 4: 昇降機と電力を書く**

`SpaceElevatorClimberComponent.cs`（電力の前例 `Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs`、進みの前例 `Game.Block/Blocks/Util/MachineCurrentPowerToSubSecond.cs`）:
```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.SpaceElevator;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.SpaceElevator
{
    /// <summary>
    ///     拠点の昇降機。接続済みなら衛星を1つずつ電力で昇らせ、昇り切ったら軌道到着サービスへ渡し、成果を保留経由で出口へ流す
    ///     The base's climber: once connected it lifts satellites one at a time with power, hands each to the orbital arrival service at the top, and drains the result through a pending list into the outlet
    /// </summary>
    public class SpaceElevatorClimberComponent : ISpaceElevatorBase, IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo
    {
        public bool IsConnected { get; private set; }
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        public string SaveKey => "spaceElevatorClimber";

        // 昇降中だけ電力を要求する
        // Request power only while climbing
        public float RequestPower => IsClimbing ? _param.RequiredPower : 0f;

        private bool IsClimbing => _climbingSatellite.Id != ItemMaster.EmptyItemId;

        private readonly Subject<Unit> _onChangeBlockState = new();
        private readonly SpaceElevatorBaseBlockParam _param;
        private readonly SpaceElevatorBaseInventoryComponent _inventory;
        private readonly IOrbitalArrivalService _orbitalArrivalService;
        private readonly uint _climbTotalTicks;
        private readonly List<IItemStack> _pendingOutputs = new();

        private IItemStack _climbingSatellite;
        private uint _remainingClimbTicks;
        private int _cablePoolAmount;
        private float _suppliedPower;
        private float _latchedPower;

        public SpaceElevatorClimberComponent(SpaceElevatorBaseBlockParam param, SpaceElevatorBaseInventoryComponent inventory)
        {
            _param = param;
            _inventory = inventory;
            _orbitalArrivalService = ServerContext.GetService<IOrbitalArrivalService>();
            _climbTotalTicks = GameUpdater.SecondsToTicks(param.ClimbSeconds);
            _climbingSatellite = ServerContext.ItemStackFactory.CreatEmpty();
        }

        public SpaceElevatorClimberComponent(Dictionary<string, object> componentStates, SpaceElevatorBaseBlockParam param, SpaceElevatorBaseInventoryComponent inventory) : this(param, inventory)
        {
            var save = BlockComponentStateReader.Read<SpaceElevatorClimberSaveJsonObject>(componentStates, SaveKey);
            IsConnected = save.IsConnected;
            _climbingSatellite = save.ClimbingSatellite.ToItemStack();
            _remainingClimbTicks = save.RemainingClimbTicks;
            foreach (var pending in save.PendingOutputs) _pendingOutputs.Add(pending.ToItemStack());
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // tick内の供給を確定してから進める
            // Latch this tick's supply before advancing
            _latchedPower = _suppliedPower;
            _suppliedPower = 0f;

            var changed = FlushPendingOutputs();
            changed |= IsClimbing ? AdvanceClimb() : TryStartClimb();

            // 昇降中は進みが毎tick動くので毎tick通知する（同一ペイロードはイベント側が送らない）
            // While climbing the progress moves every tick, so notify every tick (the event packet drops identical payloads)
            if (changed || IsClimbing) _onChangeBlockState.OnNext(Unit.Default);

            #region Internal

            bool AdvanceClimb()
            {
                // 電力不足なら供給率に比例して遅くなる
                // A power shortage slows the climb in proportion to the supply rate
                var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_latchedPower, _param.RequiredPower);
                _remainingClimbTicks = subTicks >= _remainingClimbTicks ? 0 : _remainingClimbTicks - subTicks;
                if (_remainingClimbTicks > 0) return false;

                // 昇り切ったら軌道到着サービスへ渡し、返ってきた成果を保留へ入れる
                // At the top, hand the satellite to the orbital arrival service and queue the returned result
                _pendingOutputs.AddRange(_orbitalArrivalService.Arrive(_climbingSatellite.Id, 1));
                _climbingSatellite = ServerContext.ItemStackFactory.CreatEmpty();
                FlushPendingOutputs();
                return true;
            }

            bool TryStartClimb()
            {
                // silent-ok: 未接続・保留あり・衛星なしは通常の待機 / プレイヤーが拠点UIの状態表示（Status）で気づく
                if (!IsConnected || _pendingOutputs.Count > 0) return false;
                if (!_inventory.TryTakeSatellite(out var satellite)) return false;
                _climbingSatellite = satellite;
                _remainingClimbTicks = _climbTotalTicks;
                return true;
            }

            #endregion
        }

        public void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);

            // 複数の電力セグメントから供給され得るため加算する
            // Accumulate because several electric segments may supply this base
            _suppliedPower += power;
        }

        public void MarkConnected()
        {
            BlockException.CheckDestroy(this);
            IsConnected = true;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public void SetCablePoolAmount(int amount)
        {
            BlockException.CheckDestroy(this);
            _cablePoolAmount = amount;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            var progress = IsClimbing ? 1f - (float)_remainingClimbTicks / _climbTotalTicks : 0f;
            var detail = new SpaceElevatorBaseStateDetail(IsConnected, _cablePoolAmount, _param.CablesPerElevator, progress, ResolveStatus());
            return new[] { new BlockStateDetail(SpaceElevatorBaseStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };

            #region Internal

            // 待ちの理由を優先順に1つ選ぶ。保留は昇降中には起きない（保留がある間は昇り始めない）
            // Pick one waiting reason by priority; pending results never coexist with climbing because climbs do not start while results are pending
            SpaceElevatorBaseStatus ResolveStatus()
            {
                if (!IsConnected) return SpaceElevatorBaseStatus.NotConnected;
                if (_pendingOutputs.Count > 0) return SpaceElevatorBaseStatus.OutputBlocked;
                if (!IsClimbing) return SpaceElevatorBaseStatus.WaitingSatellite;
                return _latchedPower < _param.RequiredPower ? SpaceElevatorBaseStatus.LowPower : SpaceElevatorBaseStatus.Climbing;
            }

            #endregion
        }

        // 撤去時は昇降中の衛星と保留中の成果をプレイヤーへ返す
        // On removal, return the climbing satellite and pending results to the player
        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            var refunds = new List<IItemStack>(_pendingOutputs);
            if (IsClimbing) refunds.Add(_climbingSatellite);
            return refunds;
        }

        public object GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var pending = new List<ItemStackSaveJsonObject>();
            foreach (var item in _pendingOutputs) pending.Add(new ItemStackSaveJsonObject(item));
            return new SpaceElevatorClimberSaveJsonObject
            {
                IsConnected = IsConnected,
                ClimbingSatellite = new ItemStackSaveJsonObject(_climbingSatellite),
                RemainingClimbTicks = _remainingClimbTicks,
                PendingOutputs = pending,
            };
        }

        // 保留を出口へ流し込み、入りきらなかった分だけ残す
        // Drain pending results into the outlet, keeping only what did not fit
        private bool FlushPendingOutputs()
        {
            if (_pendingOutputs.Count == 0) return false;
            var moved = false;
            var remaining = new List<IItemStack>();
            foreach (var pending in _pendingOutputs)
            {
                var remainder = _inventory.InsertOutput(pending);
                if (remainder.Count != pending.Count) moved = true;
                if (remainder.Count > 0) remaining.Add(remainder);
            }
            _pendingOutputs.Clear();
            _pendingOutputs.AddRange(remaining);
            return moved;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
        }
    }
}
```
`wc -l` で 200 行未満を確かめる。超えたら `FlushPendingOutputs` と保留リストを `SpaceElevatorPendingOutputs.cs`（`public class SpaceElevatorPendingOutputs`: `int Count`、`void AddRange(IEnumerable<IItemStack>)`、`bool FlushInto(SpaceElevatorBaseInventoryComponent inventory)`、`List<ItemStackSaveJsonObject> ToSaveJson()`、`IReadOnlyList<IItemStack> Items`）へ切り出す。

`SpaceElevatorBaseElectricComponent.cs`（前例 `Game.Block/Blocks/Pump/ElectricPumpComponent.cs`）:
```csharp
using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.SpaceElevator
{
    // 所属セグメントの確定済み供給率から実効電力を導き、昇降機へ渡す
    // Derives effective power from the segment's settled supply rate and feeds the climber
    public class SpaceElevatorBaseElectricComponent : IElectricConsumer, IElectricTickPostHandler
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_climber.RequestPower);

        private readonly SpaceElevatorClimberComponent _climber;

        public SpaceElevatorBaseElectricComponent(BlockInstanceId blockInstanceId, SpaceElevatorClimberComponent climber)
        {
            BlockInstanceId = blockInstanceId;
            _climber = climber;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);
            _climber.SupplyPower(RequestEnergy.AsPrimitive() * statistics.PowerRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

- [ ] **Step 5: テンプレートを書いて登録する**

`Game.Block/Factory/BlockTemplate/SpaceElevator/VanillaSpaceElevatorBaseTemplate.cs`（前例 `Fluid/VanillaElectricPumpTemplate.cs`・`VanillaChestTemplate.cs`）:
```csharp
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.SpaceElevator
{
    public class VanillaSpaceElevatorBaseTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var inventory = new SpaceElevatorBaseInventoryComponent(blockInstanceId, param, new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector));
            var climber = new SpaceElevatorClimberComponent(param, inventory);
            return Assemble(null, blockMasterElement, blockInstanceId, blockPositionInfo, connector, inventory, climber);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var inventory = new SpaceElevatorBaseInventoryComponent(componentStates, blockInstanceId, param, new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector));
            var climber = new SpaceElevatorClimberComponent(componentStates, param, inventory);
            return Assemble(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo, connector, inventory, climber);
        }

        private static IBlock Assemble(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo,
            BlockConnectorComponent<IBlockInventory> connector, SpaceElevatorBaseInventoryComponent inventory, SpaceElevatorClimberComponent climber)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var electric = new SpaceElevatorBaseElectricComponent(blockInstanceId, climber);
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, electric, componentStates);

            // 供給の受け取り(electric)を昇降機(climber)より前に並べる（前例 VanillaElectricPumpTemplate）
            // Order the supply receiver before the climber (precedent: VanillaElectricPumpTemplate)
            var components = new List<IBlockComponent> { connector, inventory, electric, climber, wireConnector };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
```
（`BlockTemplateUtil.CreateInventoryConnector` の戻り型名と `ConnectingInventoryListPriorityInsertItemService` の名前空間は `VanillaChestTemplate.cs` に合わせる。`ElectricWireConnectorComponent` が新規時に `null` を受けるのは `VanillaMachineTemplate.cs:47` が前例）
`VanillaIBlockTemplates.cs` の `BlockTypesDictionary.Add(BlockTypeConst.CleanRoomMachine, ...)` の次に `BlockTypesDictionary.Add(BlockTypeConst.SpaceElevatorBase, new VanillaSpaceElevatorBaseTemplate());` を足す。

- [ ] **Step 6: テストを書く**

`SpaceElevatorTestUtil.cs`（電線の前例 `Tests/CombinedTest/Core/Fluid/ElectricPumpTest.cs:28-115`）:
```csharp
using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.Orbit;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Server.Protocol;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util.ElectricWire;
using UnityEngine;
using static Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    // 宇宙エレベーターのテスト用の組み立て。電力は電柱1本＋テスト発電機で与える
    // Test assembly for the space elevator; power comes from one pole and a test generator
    public static class SpaceElevatorTestUtil
    {
        public static (PacketResponseCreator Packet, ServiceProvider Provider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static IBlock PlaceBase(Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestSpaceElevatorBase, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return block;
        }

        // 拠点に電柱をつなぎ、指定電力の発電機をセグメントへ足す
        // Wire a pole to the base and add a generator of the given power to the segment
        public static void PowerBase(IBlock elevatorBase, Vector3Int basePosition, float power)
        {
            var polePosition = basePosition + new Vector3Int(2, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(basePosition, polePosition);
            GameUpdater.UpdateOneTick();
            ServerContext.GetService<IElectricWireNetworkLookup>().TryGetEnergySegment(elevatorBase.BlockInstanceId, out var segment);
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(power), BlockInstanceId.Create()));
        }

        public static void LaunchCables(int count)
        {
            // テストmodのケーブルは1便50本
            // The test mod's cable launches carry 50 per launch
            var orbital = ServerContext.GetService<IOrbitalArrivalService>();
            for (var launched = 0; launched < count; launched += 50) orbital.Arrive(ForUnitTestItemId.TestElevatorCable, 50);
        }

        public static void RunSeconds(double seconds)
        {
            var ticks = GameUpdater.SecondsToTicks(seconds);
            for (var i = 0; i < ticks; i++) GameUpdater.RunFrames(1);
        }
    }
}
```
（`ElectricWireTestUtil`・`TestElectricGenerator`・`AddGenerator` の名前空間と ctor 引数は `ElectricPumpTest.cs` の using と呼び出しをそのまま写す）

`SpaceElevatorConnectionTest.cs`:
```csharp
using Game.Block.Blocks.SpaceElevator;
using Game.Context;
using Game.SpaceElevator;
using NUnit.Framework;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorConnectionTest
    {
        [Test]
        public void ユーザー原文の手順で接続と撤去の返却が働く()
        {
            // 1エレベーター100本。200本打ち上げて2基接続、50本足しても3基目は接続できない
            // 100 per elevator: launch 200 and connect two; adding 50 still cannot connect a third
            var (_, provider) = CreateServer();
            var service = ServerContext.GetService<SpaceElevatorConnectionService>();
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            var first = PlaceBase(new Vector3Int(0, 0, 0));
            var second = PlaceBase(new Vector3Int(10, 0, 0));
            var third = PlaceBase(new Vector3Int(20, 0, 0));
            LaunchCables(200);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(first));
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(second));
            LaunchCables(50);
            Assert.AreEqual(SpaceElevatorConnectResult.InsufficientCable, service.Connect(third));
            Assert.AreEqual(50, pool.Amount);

            // 接続済みの1基を撤去すると150になり、3基目が接続できる
            // Removing one connected base makes 150, and the third can connect
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(10, 0, 0), Game.Block.Interface.BlockRemoveReason.ManualRemove);
            Assert.AreEqual(150, pool.Amount);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(third));
            Assert.AreEqual(50, pool.Amount);
        }

        [Test]
        public void 未接続の拠点の撤去はプールに影響しない()
        {
            CreateServer();
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            PlaceBase(Vector3Int.zero);
            LaunchCables(100);
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, Game.Block.Interface.BlockRemoveReason.ManualRemove);
            Assert.AreEqual(100, pool.Amount);
        }

        [Test]
        public void ちょうど1エレベーター分で接続でき二度目は接続済みを返す()
        {
            CreateServer();
            var service = ServerContext.GetService<SpaceElevatorConnectionService>();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            LaunchCables(100);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(elevatorBase));
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, service.Connect(elevatorBase));
            Assert.AreEqual(0, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void プールの増減が全拠点の状態に届く()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            LaunchCables(150);
            var detail = ReadDetail(elevatorBase);
            Assert.AreEqual(150, detail.CablePoolAmount);
            Assert.AreEqual(100, detail.CablesPerElevator);
            Assert.AreEqual(SpaceElevatorBaseStatus.NotConnected, detail.Status);
        }

        public static SpaceElevatorBaseStateDetail ReadDetail(Game.Block.Interface.IBlock elevatorBase)
        {
            var details = elevatorBase.GetComponent<SpaceElevatorClimberComponent>().GetBlockStateDetails();
            return MessagePack.MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(details[0].Value);
        }
    }
}
```

`SpaceElevatorClimbTest.cs`:
```csharp
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Context;
using Game.SpaceElevator;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorClimbTest
    {
        [Test]
        public void 接続済みで電力が足りれば2秒で昇り成果が出口に出る()
        {
            var elevatorBase = SetupConnected(100f);
            Inventory(elevatorBase).SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 2);

            RunSeconds(2.2);

            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.OutputSlot).Id);
            Assert.AreEqual(5, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.OutputSlot).Count);
        }

        [Test]
        public void 電力が半分なら倍かかる()
        {
            var elevatorBase = SetupConnected(50f);
            Inventory(elevatorBase).SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 1);

            RunSeconds(2.2);
            Assert.AreEqual(0, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.OutputSlot).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.LowPower, ReadDetail(elevatorBase).Status);

            RunSeconds(2.4);
            Assert.AreEqual(5, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.OutputSlot).Count);
        }

        [Test]
        public void 未接続なら昇らず衛星は入口に残る()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, 100f);
            Inventory(elevatorBase).SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 1);

            RunSeconds(3);

            Assert.AreEqual(1, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.NotConnected, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 出口が満杯なら保留して次を昇らせず空くと流れる()
        {
            var elevatorBase = SetupConnected(100f);
            var inventory = Inventory(elevatorBase);

            // 出口を別アイテムで塞ぐと成果は保留され、次の衛星は入口に残る
            // Blocking the outlet with another item holds the result and leaves the next satellite in the inlet
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.OutputSlot, ForUnitTestItemId.ItemId1, 1);
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 2);
            RunSeconds(4.5);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, ReadDetail(elevatorBase).Status);
            Assert.AreEqual(1, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);

            // 出口を空けると保留が流れ、次の衛星が昇り始める
            // Clearing the outlet drains the pending result and the next satellite starts climbing
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.OutputSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunSeconds(0.2);
            Assert.AreEqual(5, inventory.GetItem(SpaceElevatorBaseInventoryComponent.OutputSlot).Count);
            Assert.AreEqual(0, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
        }

        [Test]
        public void 入口は運ぶ積荷だけを受ける()
        {
            CreateServer();
            var inventory = Inventory(PlaceBase(Vector3Int.zero));
            var rejected = inventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));
            Assert.AreEqual(3, rejected.Count);
            Assert.IsFalse(inventory.IsAllowedToPlace(SpaceElevatorBaseInventoryComponent.OutputSlot, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 1)));
        }

        private static Game.Block.Interface.IBlock SetupConnected(float power)
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, power);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            return elevatorBase;
        }

        private static SpaceElevatorBaseInventoryComponent Inventory(Game.Block.Interface.IBlock elevatorBase)
        {
            return elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
        }
    }
}
```
（テスト mod の出口の隣にベルトは無いので、成果は出口に溜まる。`ItemId1` のスタック上限が 1 以上であること）

`SpaceElevatorBaseSaveAndRefundTest.cs`:
```csharp
using System;
using System.Linq;
using Game.Block.Blocks.SpaceElevator;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SpaceElevator;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorBaseSaveAndRefundTest
    {
        private const int PlayerId = 3;

        [Test]
        public void 接続と昇降中と保留がセーブを往復する()
        {
            var (_, provider) = CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(1);
            var json = provider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 読み直すと接続済みで昇降中のまま、残り1秒で昇り切る
            // After reload it is still connected and climbing, finishing within the remaining second
            var (_, loadedProvider) = CreateServer();
            ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(json);
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsTrue(ReadDetail(loaded).IsConnected);
            Assert.Greater(ReadDetail(loaded).ClimbProgress, 0f);
            Assert.AreEqual(0, loadedProvider.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void 撤去すると昇降中の衛星と保留とスロットがプレイヤーへ返り接続分がプールへ戻る()
        {
            var (packet, provider) = CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 3);
            RunSeconds(0.5);

            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(Vector3Int.zero)), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));

            // 入口に2機・昇降中に1機の計3機が戻る
            // Two in the inlet and one climbing come back, three in total
            var playerInventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var satellites = playerInventory.InventoryItems.Where(item => item.Id == ForUnitTestItemId.TestElevatorSatellite).Sum(item => item.Count);
            Assert.AreEqual(3, satellites);
            Assert.AreEqual(100, provider.GetService<SpaceElevatorCablePool>().Amount);
        }
    }
}
```

- [ ] **Step 7: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpaceElevator|RemoveBlockRefundTest|ElectricWire"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator
git commit -m "feat(space-elevator): 拠点ブロック（昇降機・電力・保留・撤去返却）を足す"
```

### Task 9: 接続プロトコル

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SpaceElevator/ConnectSpaceElevatorProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef`（`Game.SpaceElevator` を参照に足す）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/ConnectSpaceElevatorProtocolTest.cs`

**Interfaces:**
- Consumes: Task 7 の `SpaceElevatorConnectionService.Connect(IBlock)`・`SpaceElevatorConnectResult`、Task 8 の `SpaceElevatorConnectionTest.ReadDetail(IBlock)`
- Produces: タグ `va:connectSpaceElevator`、`ConnectSpaceElevatorRequest(Vector3Int position)`（`[Key(2)] Vector3IntMessagePack Position`）、`ConnectSpaceElevatorResponse`（`[Key(2)] SpaceElevatorConnectResult Result`）

- [ ] **Step 1: プロトコルを書く**

`Server.Protocol/PacketResponse/SpaceElevator/ConnectSpaceElevatorProtocol.cs`（形の前例 `SetElectricToGearOutputModeProtocol.cs`、拒否ログの前例 `RemoveBlockProtocol.cs` の `Refuse`。`PacketResponse/` 直下は既に 50 ファイルを超えるのでサブディレクトリに置く）:
```csharp
using System;
using Game.Context;
using Game.SpaceElevator;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse.SpaceElevator
{
    // 拠点UIの「接続」ボタン。判定と引き算は接続サービスが持ち、ここは座標から拠点を引いて頼むだけ
    // The base UI's connect button; the connection service owns the check and the subtraction, this only resolves the block and asks
    public class ConnectSpaceElevatorProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:connectSpaceElevator";

        private readonly SpaceElevatorConnectionService _connectionService;

        public ConnectSpaceElevatorProtocol(ServiceProvider serviceProvider)
        {
            _connectionService = serviceProvider.GetService<SpaceElevatorConnectionService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<ConnectSpaceElevatorRequest>(payload);
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            var result = block == null ? SpaceElevatorConnectResult.NotSpaceElevatorBase : _connectionService.Connect(block);

            // 接続できなかった理由を開発者ログへ残す。プレイヤーには状態詳細の残量から理由を出す
            // Log why the connection was refused for developers; players see the reason from the state detail's amounts
            if (result != SpaceElevatorConnectResult.Connected) Debug.Log($"[ConnectSpaceElevator] connection refused: {result} pos:{request.Position.Vector3Int} player:{requesterPlayerId}");
            return new ConnectSpaceElevatorResponse(result);
        }
    }

    [MessagePackObject]
    public class ConnectSpaceElevatorRequest : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3IntMessagePack Position { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ConnectSpaceElevatorRequest()
        {
        }

        public ConnectSpaceElevatorRequest(Vector3Int position)
        {
            Tag = ConnectSpaceElevatorProtocol.ProtocolTag;
            Position = new Vector3IntMessagePack(position);
        }
    }

    [MessagePackObject]
    public class ConnectSpaceElevatorResponse : ProtocolMessagePackBase
    {
        [Key(2)] public SpaceElevatorConnectResult Result { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ConnectSpaceElevatorResponse()
        {
        }

        public ConnectSpaceElevatorResponse(SpaceElevatorConnectResult result)
        {
            Tag = ConnectSpaceElevatorProtocol.ProtocolTag;
            Result = result;
        }
    }
}
```
（`ProtocolMessagePackBase` の Tag の扱い・`Vector3IntMessagePack` の ctor は `SetElectricToGearOutputModeProtocol.cs:57-95` に合わせる）
`PacketResponseCreator.cs` の `SetElectricToGearOutputModeProtocol` の登録行の次に `_packetResponseDictionary.Add(ConnectSpaceElevatorProtocol.ProtocolTag, new ConnectSpaceElevatorProtocol(serviceProvider));` を足す（`using Server.Protocol.PacketResponse.SpaceElevator;`）。

- [ ] **Step 2: テストを書く**

`Tests/CombinedTest/Game/SpaceElevator/ConnectSpaceElevatorProtocolTest.cs`:
```csharp
using Game.SpaceElevator;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse.SpaceElevator;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class ConnectSpaceElevatorProtocolTest
    {
        [Test]
        public void 足りなければ理由を返し足りれば接続する()
        {
            var (packet, _) = CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);

            Assert.AreEqual(SpaceElevatorConnectResult.InsufficientCable, Send(packet, Vector3Int.zero).Result);
            LaunchCables(100);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, Send(packet, Vector3Int.zero).Result);
            Assert.IsTrue(ReadDetail(elevatorBase).IsConnected);
        }

        [Test]
        public void 拠点でない座標は拒否する()
        {
            var (packet, _) = CreateServer();
            Assert.AreEqual(SpaceElevatorConnectResult.NotSpaceElevatorBase, Send(packet, new Vector3Int(5, 0, 5)).Result);
        }

        private static ConnectSpaceElevatorResponse Send(PacketResponseCreator packet, Vector3Int position)
        {
            var payload = MessagePackSerializer.Serialize(new ConnectSpaceElevatorRequest(position));
            var response = packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1))[0];
            return MessagePackSerializer.Deserialize<ConnectSpaceElevatorResponse>(response);
        }
    }
}
```
（`GetPacketResponse` の戻り値の型と添字は `Tests/CombinedTest/Server/PacketTest/ElectricToGearOutputModeProtocolTest.cs:63-68` の `Send` に合わせる）

- [ ] **Step 3: コンパイルしテストを実行してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConnectSpaceElevatorProtocolTest|PacketResponseCreator|ProtocolTag"`
Expected: ErrorCount 0 / 全 PASS
```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/ConnectSpaceElevatorProtocolTest.cs*
git commit -m "feat(space-elevator): 拠点の接続プロトコルを足す"
```

### Task 10: 拠点の UI（クライアントの DTO・action と webui）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Requests/BlockRequestApi.cs`、`moorestech_client/Assets/Scripts/Client.Network/Client.Network.asmdef`（`Game.SpaceElevator` を参照に足す）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/SpaceElevator/SpaceElevatorDetailDto.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/SpaceElevator/SpaceElevatorDetailDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs`、`BlockDetailDtoBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/SpaceElevator/SpaceElevatorConnectActionHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`、Client.WebUiHost の asmdef（`Game.SpaceElevator` を参照に足す）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_space_elevator.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/SpaceElevator/WireContractSpaceElevatorTest.cs`
- Modify: `Localization/localization.csv`
- Create: `moorestech_web/webui/src/bridge/contract/schemas/spaceElevator/index.ts`
- Create: `moorestech_web/webui/src/bridge/contract/schemas/fluidSlot.ts`、`moorestech_web/webui/src/bridge/contract/schemas/beltThroughput.ts`（inventory.ts から移す）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`、`schemas/index.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/SpaceElevatorSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/spaceElevatorDisplay.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`
- Modify: `moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json`、`moorestech_web/webui/e2e/tests/block/blockRegistryCoverage.spec.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/blocks/energyFixtures.ts`、`moorestech_web/webui/e2e/mock-host/httpHandler.ts`、`moorestech_web/webui/e2e/mock-host/wsHandler.ts`、`moorestech_web/webui/e2e/mock-host/detailActions.ts`
- Create: `moorestech_web/webui/e2e/tests/block/spaceElevator.spec.ts`

**Interfaces:**
- Consumes: Task 8 の `SpaceElevatorBaseStateDetail`・`SpaceElevatorBaseStatus`、Task 9 の `ConnectSpaceElevatorRequest`/`ConnectSpaceElevatorResponse`
- Produces:
  - wire の `spaceElevator: { connected: boolean; cablePoolAmount: number; cablesPerElevator: number; climbProgress: number; status: "notConnected" | "waitingSatellite" | "climbing" | "lowPower" | "outputBlocked" }`（blockType `SpaceElevatorBase` のときだけ）
  - action `space_elevator.connect`（payload `{}`）
  - testid: `space-elevator-section`, `space-elevator-status`, `space-elevator-pool`, `space-elevator-progress`, `space-elevator-connect`, `space-elevator-insufficient`, `space-elevator-grid`

配置: 拠点のスロット・電力網・固有部分を、汎用の `SectionStackView` にデータの有無で合成する（前例 `GearToElectricSection` と `configByBlockType`。「標準ブロック表示をデータ有無で合成し、固有UIだけをレジストリへ残す」）。接続ボタンは固有部分の中に置き、専用ビューは作らない。

- [ ] **Step 1: クライアントの API・DTO・action を書く**

`BlockRequestApi.cs` の `SetElectricToGearOutputMode` の次に:
```csharp
        // 宇宙エレベーター拠点の接続を頼む
        // Ask to connect a space elevator base
        public async UniTask<ConnectSpaceElevatorResponse> ConnectSpaceElevator(Vector3Int position, CancellationToken ct)
        {
            var request = new ConnectSpaceElevatorRequest(position);
            return await _packetExchangeManager.GetPacketResponse<ConnectSpaceElevatorResponse>(request, ct);
        }
```
（`using Server.Protocol.PacketResponse.SpaceElevator;`）

`Topics/BlockDetail/SpaceElevator/SpaceElevatorDetailDto.cs`:
```csharp
namespace Client.WebUiHost.Game.Topics.BlockDetail.SpaceElevator
{
    public class SpaceElevatorDetailDto
    {
        public bool Connected;
        public int CablePoolAmount;
        public int CablesPerElevator;
        public float ClimbProgress;
        public string Status;
    }
}
```
`Topics/BlockDetail/SpaceElevator/SpaceElevatorDetailDtoBuilder.cs`（前例 `ElectricToGearDetailDtoBuilder.cs`）:
```csharp
using Client.Game.InGame.Block;
using Game.Block.Blocks.SpaceElevator;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail.SpaceElevator
{
    /// <summary>
    /// 宇宙エレベーター拠点の状態詳細をDTOへ詰める
    /// Fills the space elevator base's state detail into its capability DTO
    /// </summary>
    public static class SpaceElevatorDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            if (param is not SpaceElevatorBaseBlockParam) return;
            var state = block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
            // 初回の状態が届く前は未着として何も詰めない（webui は詳細なしで描く）
            // Before the first state arrives nothing is filled; the webui renders without the detail
            if (state == null) return;

            dto.SpaceElevator = new SpaceElevatorDetailDto
            {
                Connected = state.IsConnected,
                CablePoolAmount = state.CablePoolAmount,
                CablesPerElevator = state.CablesPerElevator,
                ClimbProgress = state.ClimbProgress,
                Status = ToWireStatus(state.Status),
            };
        }

        private static string ToWireStatus(SpaceElevatorBaseStatus status)
        {
            return status switch
            {
                SpaceElevatorBaseStatus.NotConnected => "notConnected",
                SpaceElevatorBaseStatus.WaitingSatellite => "waitingSatellite",
                SpaceElevatorBaseStatus.Climbing => "climbing",
                SpaceElevatorBaseStatus.LowPower => "lowPower",
                SpaceElevatorBaseStatus.OutputBlocked => "outputBlocked",
                _ => throw new System.ArgumentOutOfRangeException(nameof(status), status, "未対応の拠点状態"),
            };
        }
    }
}
```
`BlockInventoryDtos.cs` の `public ElectricToGearDetailDto ElectricToGear;` の次に `public SpaceElevatorDetailDto SpaceElevator;`（`using Client.WebUiHost.Game.Topics.BlockDetail.SpaceElevator;`）。`BlockDetailDtoBuilder.Apply` の `ElectricToGearDetailDtoBuilder.Apply(dto, block, param);` の次に `SpaceElevatorDetailDtoBuilder.Apply(dto, block, param);`。

`Actions/SpaceElevator/SpaceElevatorConnectActionHandler.cs`（前例 `Actions/ElectricToGearActions.cs`。`Actions/` 直下は既に10ファイルを超えるのでサブディレクトリに置く）:
```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.SpaceElevator;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.SpaceElevator
{
    /// <summary>
    /// 開いている宇宙エレベーター拠点を接続する
    /// space_elevator.connect: connects the open space elevator base
    /// </summary>
    public class SpaceElevatorConnectActionHandler : IActionHandler
    {
        public string ActionType => "space_elevator.connect";

        private readonly SubInventoryState _subInventoryState;

        public SpaceElevatorConnectActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");
            if (!source.TryGetBlockIdentity(out _, out var blockTypeName)) return ActionResult.Fail("block_not_open");
            if (blockTypeName != "SpaceElevatorBase") return ActionResult.Fail("invalid_block_type");

            // 結果は後続の状態詳細で画面へ戻る。ここは失敗理由だけを返す
            // The view updates from the following state detail; this only returns the failure reason
            var response = await ClientContext.VanillaApi.Response.Block.ConnectSpaceElevator(source.BlockPosition, CancellationToken.None);
            if (response == null) return ActionResult.Fail("connect_request_failed");
            return response.Result switch
            {
                SpaceElevatorConnectResult.Connected => ActionResult.Success(),
                SpaceElevatorConnectResult.AlreadyConnected => ActionResult.Fail("already_connected"),
                SpaceElevatorConnectResult.InsufficientCable => ActionResult.Fail("insufficient_cable"),
                _ => ActionResult.Fail("not_space_elevator_base"),
            };
        }
    }
}
```
`WebUiGameBinder.cs` の `hub.RegisterAction(new ElectricToGearSetOutputModeActionHandler(subInventoryState));` の次に `hub.RegisterAction(new SpaceElevatorConnectActionHandler(subInventoryState));`（`using Client.WebUiHost.Game.Actions.SpaceElevator;`。`wc -l` が 200 以上になったら `ElectricToGear` 以降のブロック系 action 登録を `Actions/BlockActionRegistration.cs` の `public static void Register(WebUiHub hub, SubInventoryState subInventoryState)` へ移す）。
`action_names.json` に `"space_elevator.connect"` を既存の並びの規則（アルファベット順なら該当位置）で足す。

- [ ] **Step 2: wire fixture と契約テストを書く**

`WireFixtures/block_inventory_space_elevator.json`:
```json
{
  "open": true,
  "source": "block",
  "blockType": "SpaceElevatorBase",
  "identifier": "(20, 0, 4)",
  "blockGuid": "99999999-9999-4999-8999-999999999999",
  "itemSlots": [],
  "fluidSlots": [],
  "spaceElevator": {
    "connected": true,
    "cablePoolAmount": 250,
    "cablesPerElevator": 1000,
    "climbProgress": 0.5,
    "status": "climbing"
  }
}
```
`Client.Tests/WebUi/SpaceElevator/WireContractSpaceElevatorTest.cs`（前例 `WireContractElectricToGearTest.cs` をそのまま写し、DTO を次にする）:
```csharp
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "SpaceElevatorBase",
                Identifier = "(20, 0, 4)",
                BlockGuid = "99999999-9999-4999-8999-999999999999",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                SpaceElevator = new SpaceElevatorDetailDto { Connected = true, CablePoolAmount = 250, CablesPerElevator = 1000, ClimbProgress = 0.5f, Status = "climbing" },
            };
```
（fixture のパスは `Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_space_elevator.json`。前例の fixture に null のキーが出るなら、こちらの fixture も実出力に合わせて同じキーを持たせる）
`wireContract.test.ts` の `block detail fixtures` の `cases` に `"block_inventory_space_elevator.json"` を足す。

- [ ] **Step 3: UI 文言を足す**

`Localization/localization.csv` に（列は `key,Source,english,japanese,german,korean`。差し込みは `{name}` 形式）:
```
ui.blockInventory.spaceElevatorConnect,Connect,Connect,接続する,Verbinden,연결
ui.blockInventory.spaceElevatorPool,Cable pool: {pool} / {required},Cable pool: {pool} / {required},ケーブルのプール: {pool} / 必要 {required},Kabelvorrat: {pool} / {required},케이블 풀: {pool} / {required}
ui.blockInventory.spaceElevatorInsufficientCable,Not enough cable. Launch graphene cables to fill the pool,Not enough cable. Launch graphene cables to fill the pool,ケーブルが足りません。グラフェンケーブルを打ち上げてプールを満たしてください,Nicht genug Kabel. Starte Graphenkabel,케이블이 부족합니다. 그래핀 케이블을 발사하세요
ui.blockInventory.spaceElevatorStatusNotConnected,Not connected,Not connected,未接続,Nicht verbunden,연결 안 됨
ui.blockInventory.spaceElevatorStatusWaitingSatellite,Waiting for a GPU satellite,Waiting for a GPU satellite,GPU衛星を待っています,Warte auf GPU-Satelliten,GPU 위성 대기 중
ui.blockInventory.spaceElevatorStatusClimbing,Climbing,Climbing,昇降中,Steigt auf,상승 중
ui.blockInventory.spaceElevatorStatusLowPower,Climbing slowly (low power),Climbing slowly (low power),電力不足で遅くなっています,Langsam (zu wenig Strom),전력 부족으로 느려짐
ui.blockInventory.spaceElevatorStatusOutputBlocked,Output full; the next satellite is waiting,Output full; the next satellite is waiting,出口が満杯のため次の衛星を待たせています,Ausgang voll; nächster Satellit wartet,출구가 가득 차 다음 위성이 대기 중
```
`cd moorestech_web/webui && npm run gen:i18n` で `localizationKeys.ts` を再生成する。

- [ ] **Step 4: webui の受信スキーマを足し、inventory.ts を 200 行未満にする**

`schemas/spaceElevator/index.ts`:
```ts
import { z } from "zod";

export const SpaceElevatorStatusSchema = z.enum(["notConnected", "waitingSatellite", "climbing", "lowPower", "outputBlocked"]);
export const SpaceElevatorDataSchema = z.object({
  connected: z.boolean(),
  cablePoolAmount: z.number().int().nonnegative(),
  cablesPerElevator: z.number().int().positive(),
  climbProgress: z.number().min(0).max(1),
  status: SpaceElevatorStatusSchema,
}).strict();
export type SpaceElevatorStatus = z.infer<typeof SpaceElevatorStatusSchema>;
```
`inventory.ts` の `FluidSlotWireSchema`・`FluidSlotDataSchema`（とその直前のコメント）を `schemas/fluidSlot.ts` へ、`BeltThroughputWindowDataSchema`・`BeltThroughputDataSchema`（とコメント）を `schemas/beltThroughput.ts` へ本体ごと移し、`inventory.ts` からは `import { FluidSlotDataSchema } from "./fluidSlot"; import { BeltThroughputDataSchema } from "./beltThroughput";` で使い、`export { FluidSlotDataSchema } from "./fluidSlot"; export { BeltThroughputDataSchema, BeltThroughputWindowDataSchema } from "./beltThroughput";` で再公開する（既存の import 先を変えないため）。`BlockInventoryOpenSchema` に `spaceElevator: SpaceElevatorDataSchema.optional(),` を足し、`superRefine` に:
```ts
  if (data.spaceElevator && data.blockType !== "SpaceElevatorBase")
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["spaceElevator"], message: "spaceElevator requires SpaceElevatorBase" });
```
`schemas/index.ts` に `export * from "./spaceElevator";` を足す。`wc -l src/bridge/contract/schemas/inventory.ts` が 200 未満であること。

- [ ] **Step 5: action 契約と固有部分を書く**

`actionContract.ts` の `ActionPayloads` に `"space_elevator.connect": Record<string, never>;`、`ACTION_TYPES` に `"space_elevator.connect",` を足す（`electric_to_gear.set_output_mode` の次）。

`details/spaceElevator/spaceElevatorDisplay.ts`（前例 `details/detailLogic.ts` の `gearToElectricStateDisplay`）:
```ts
import type { SpaceElevatorStatus } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// 状態ごとの表示キーと、足りない色で出すか
// Display key per status and whether it shows in the shortage color
export type SpaceElevatorStatusDisplay = { labelKey: TranslationKey; insufficient: boolean };

const StatusDisplayTable: Record<SpaceElevatorStatus, SpaceElevatorStatusDisplay> = {
  notConnected: { labelKey: L.ui.blockInventory.spaceElevatorStatusNotConnected, insufficient: true },
  waitingSatellite: { labelKey: L.ui.blockInventory.spaceElevatorStatusWaitingSatellite, insufficient: false },
  climbing: { labelKey: L.ui.blockInventory.spaceElevatorStatusClimbing, insufficient: false },
  lowPower: { labelKey: L.ui.blockInventory.spaceElevatorStatusLowPower, insufficient: true },
  outputBlocked: { labelKey: L.ui.blockInventory.spaceElevatorStatusOutputBlocked, insufficient: true },
};

export function spaceElevatorStatusDisplay(status: SpaceElevatorStatus): SpaceElevatorStatusDisplay {
  return StatusDisplayTable[status];
}

// 未接続でプールに1エレベーター分あるときだけ接続できる
// Connecting is possible only while unconnected and the pool holds one elevator's worth
export function canConnectSpaceElevator(connected: boolean, cablePoolAmount: number, cablesPerElevator: number): boolean {
  return !connected && cablePoolAmount >= cablesPerElevator;
}
```
（`TranslationKey` の import 元は `detailLogic.ts` に合わせる）

`details/spaceElevator/SpaceElevatorSection.tsx`（前例 `details/GearToElectricSection.tsx`。ボタンは `@/shared/ui` の `PanelActionButton`。見た目は ref-webui-design スキルのホワイトリストに従う）:
```tsx
import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { GaugeBar, PanelActionButton } from "@/shared/ui";
import LackHighlightText from "../LackHighlightText";
import { canConnectSpaceElevator, spaceElevatorStatusDisplay } from "./spaceElevatorDisplay";

// 接続状態・プール残量・昇降の進みと、未接続なら接続ボタンを出す
// Show the connection state, the pool amount and the climb progress, plus the connect button while unconnected
export default function SpaceElevatorSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.spaceElevator;
  if (!detail) return null;
  const display = spaceElevatorStatusDisplay(detail.status);
  const canConnect = canConnectSpaceElevator(detail.connected, detail.cablePoolAmount, detail.cablesPerElevator);
  const connect = () => {
    void dispatchAction("space_elevator.connect", {});
  };

  return (
    <Stack gap={4} data-testid="space-elevator-section">
      <LackHighlightText insufficient={display.insufficient} size="sm" testId="space-elevator-status">{t(display.labelKey)}</LackHighlightText>
      <Text size="sm" c="var(--text-default)" data-testid="space-elevator-pool">
        {t(L.ui.blockInventory.spaceElevatorPool, { pool: detail.cablePoolAmount, required: detail.cablesPerElevator })}
      </Text>
      {detail.connected ? <GaugeBar value={detail.climbProgress} testId="space-elevator-progress" /> : null}
      {detail.connected ? null : (
        <PanelActionButton onClick={connect} disabled={!canConnect} testId="space-elevator-connect">
          {t(L.ui.blockInventory.spaceElevatorConnect)}
        </PanelActionButton>
      )}
      {detail.connected || canConnect ? null : (
        <LackHighlightText insufficient size="sm" testId="space-elevator-insufficient">{t(L.ui.blockInventory.spaceElevatorInsufficientCable)}</LackHighlightText>
      )}
    </Stack>
  );
}
```
`SectionStackView.tsx`: `configByBlockType` に `SpaceElevatorBase: { itemGridTestId: "space-elevator-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: false, showFluidDetails: false },` を足し、`<GearToElectricSection data={data} />` の次に `<SpaceElevatorSection data={data} />` を足す（`import SpaceElevatorSection from "../details/spaceElevator/SpaceElevatorSection";`）。

`details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx`（前例 `views/__tests__/SectionStackView.test.tsx` の描画ヘルパーと `dispatchAction` のモックを写す）:
```tsx
import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import SpaceElevatorSection from "../SpaceElevatorSection";
import { canConnectSpaceElevator } from "../spaceElevatorDisplay";

describe("canConnectSpaceElevator", () => {
  it.each([
    [false, 1000, 1000, true],
    [false, 999, 1000, false],
    [true, 5000, 1000, false],
  ])("connected=%s pool=%s required=%s → %s", (connected, pool, required, expected) => {
    expect(canConnectSpaceElevator(connected, pool, required)).toBe(expected);
  });
});

describe("SpaceElevatorSection", () => {
  it("足りないときは接続ボタンを押せず理由を出す", () => {
    renderSection({ connected: false, cablePoolAmount: 250, cablesPerElevator: 1000, climbProgress: 0, status: "notConnected" });
    expect(screen.getByTestId("space-elevator-connect")).toBeDisabled();
    expect(screen.getByTestId("space-elevator-insufficient")).toBeInTheDocument();
  });

  it("足りるときは接続actionを送る", () => {
    const dispatch = mockDispatchAction();
    renderSection({ connected: false, cablePoolAmount: 1000, cablesPerElevator: 1000, climbProgress: 0, status: "notConnected" });
    fireEvent.click(screen.getByTestId("space-elevator-connect"));
    expect(dispatch).toHaveBeenCalledWith("space_elevator.connect", {});
  });

  it("接続済みなら進みを出しボタンを出さない", () => {
    renderSection({ connected: true, cablePoolAmount: 0, cablesPerElevator: 1000, climbProgress: 0.5, status: "climbing" });
    expect(screen.getByTestId("space-elevator-progress")).toBeInTheDocument();
    expect(screen.queryByTestId("space-elevator-connect")).toBeNull();
  });
});
```
（`renderSection(detail)` は blockType `SpaceElevatorBase` の `BlockInventoryOpen` を作って i18n プロバイダ付きで描くヘルパー、`mockDispatchAction()` は `vi.mock("@/bridge", ...)` で `dispatchAction` を差し替えて返すヘルパー。どちらも SectionStackView.test.tsx の既存ヘルパーの形で同ファイル内に書く）

- [ ] **Step 6: e2e を足す**

`e2e/fixtures/v8-block-ui-registry.json` に `{ "blockType": "SpaceElevatorBase", "openable": true }` をアルファベット順の位置に足す（Task 3 で足した `ElectricGenerator` openable true の行と合わせ、blocks.json から再生成した結果と一致させる）。`blockRegistryCoverage.spec.ts` の `intentionalGeneric` に `"SpaceElevatorBase"` を足す。
`energyFixtures.ts` の末尾に:
```ts
// 宇宙エレベーター拠点（未接続・ケーブル不足）
// Space elevator base: unconnected with too few cables
export const blockSpaceElevator = {
  open: true,
  source: "block",
  blockType: "SpaceElevatorBase",
  identifier: "block:20",
  blockGuid: BlockGuids.ELECTRIC_TO_GEAR_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  spaceElevator: { connected: false, cablePoolAmount: 1000, cablesPerElevator: 1000, climbProgress: 0, status: "notConnected" },
} satisfies BlockInventoryWireData;
```
（`BlockGuids` に専用の GUID を足す前例があればそれに従う）`httpHandler.ts` の `electricToGear: fx.blockElectricToGear,` の次に `spaceElevator: fx.blockSpaceElevator,`。`detailActions.ts` に:
```ts
// space_elevator.connect: 足りれば接続済みにしてプールから引く
// space_elevator.connect: when enough remains, mark connected and take from the pool
export function applySpaceElevatorConnect(block: BlockInventoryWireData): boolean {
  if (!block.open || !("spaceElevator" in block) || !block.spaceElevator) return false;
  const detail = block.spaceElevator;
  if (detail.connected || detail.cablePoolAmount < detail.cablesPerElevator) return false;
  detail.connected = true;
  detail.cablePoolAmount -= detail.cablesPerElevator;
  detail.status = "waitingSatellite";
  return true;
}
```
`wsHandler.ts` の `electric_to_gear.set_output_mode` 分岐の次に:
```ts
        } else if (msg.type === "space_elevator.connect") {
          const applied = applySpaceElevatorConnect(state.currentBlock);
          if (!applied) error = "insufficient_cable";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
```
`e2e/tests/block/spaceElevator.spec.ts`:
```ts
import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("接続ボタンでactionを送り、接続後は進みを出す", async ({ page }) => {
  await setBlock(page, "spaceElevator");
  await page.goto("/");

  await expect(page.getByTestId("space-elevator-section")).toBeVisible();
  await expect(page.getByTestId("space-elevator-pool")).toContainText("1000");
  await page.getByTestId("space-elevator-connect").click();
  await expect.poll(async () => (await payloadsOf(page, "space_elevator.connect"))[0]).toEqual({});
  await expect(page.getByTestId("space-elevator-progress")).toBeVisible();
  await expect(page.getByTestId("space-elevator-connect")).toHaveCount(0);
});
```

- [ ] **Step 7: コンパイル・テスト・lint**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract|LocalizationDictionaryCoverageTest|ElectricToGearOutputModeActionTest"`
Run: `cd moorestech_web/webui && npx tsc --noEmit && npx vitest run && npm run lint && npx playwright test e2e/tests/block`
Expected: ErrorCount 0 / 全 PASS / 型・lint エラー 0

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Network moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi Localization/localization.csv moorestech_web/webui/src moorestech_web/webui/e2e
git commit -m "feat(webui): 宇宙エレベーター拠点の接続ボタンと状態表示を足す"
```

### Task 11: 拠点の簡易モデルと昇降の見た目

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/SpaceElevator/SpaceElevatorBaseStateChangeProcessor.cs`
- Create（dynamic code で生成）: `moorestech_client/Assets/AddressableResources/Block/SpaceElevatorBase.prefab`、`moorestech_client/Assets/AddressableResources/Block/SpaceElevatorBaseMaterial.mat`
- Modify（dynamic code 経由）: `moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset`

**Interfaces:**
- Consumes: Task 8 の `SpaceElevatorBaseStateDetail`（`IsConnected`, `ClimbProgress`）
- Produces: アドレス `Vanilla/Block/SpaceElevatorBase` のプレハブ（子 `ClickCollider`・`Base`・`Anchor`・`Cable`・`Climber`）

- [ ] **Step 1: 見た目の processor を書く**

前例 `StateProcessor/SteamEngineEffectStateChangeProcessor.cs`（プレハブに貼る `[SerializeField]` 付き processor）:
```csharp
using Client.Game.InGame.Block;
using Game.Block.Blocks.SpaceElevator;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.SpaceElevator
{
    // 拠点の仮モデル。接続でケーブルと昇降機を出し、昇降の進みに合わせて昇降機を上げる
    // The base's placeholder model: shows the cable and climber once connected and raises the climber with the climb progress
    public class SpaceElevatorBaseStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private GameObject cable;
        [SerializeField] private Transform climber;
        [SerializeField] private Vector3 climberBottomLocalPosition;
        [SerializeField] private Vector3 climberTopLocalPosition;

        public void Initialize(BlockGameObject blockGameObject)
        {
            // 最初の状態が届くまでは未接続として描く
            // Draw as unconnected until the first state arrives
            cable.SetActive(false);
            climber.gameObject.SetActive(false);
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            var detail = blockState.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);

            // 拠点は常にこのキーを出す。欠けたらサーバーとの不整合なので知らせて描画を変えない
            // The base always sends this key; a missing one is a server mismatch, so report it and keep the drawing
            if (detail == null)
            {
                Debug.LogError("[SpaceElevatorBaseStateChangeProcessor] SpaceElevatorBase state detail is missing");
                return;
            }

            cable.SetActive(detail.IsConnected);
            climber.gameObject.SetActive(detail.IsConnected);
            climber.localPosition = Vector3.Lerp(climberBottomLocalPosition, climberTopLocalPosition, detail.ClimbProgress);
        }
    }
}
```
コンパイルする（`uloop compile --project-path ./moorestech_client`、ErrorCount 0）。

- [ ] **Step 2: プレハブを dynamic code で作り Addressables に登録する**

`uloop execute-dynamic-code --project-path ./moorestech_client` で次を実行する（blockSize [9,4,9] の土台と、高さ約80のケーブル）:
```csharp
using Client.Game.InGame.BlockSystem.StateProcessor.SpaceElevator;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

var shader = Shader.Find("Universal Render Pipeline/Lit");
if (shader == null) return "ERROR: URP Lit shader not found";
var materialPath = "Assets/AddressableResources/Block/SpaceElevatorBaseMaterial.mat";
var material = new Material(shader) { color = new Color(0.75f, 0.78f, 0.82f) };
AssetDatabase.CreateAsset(material, materialPath);

var root = new GameObject("SpaceElevatorBase");
GameObject Part(PrimitiveType type, string name, Vector3 position, Vector3 scale)
{
    var part = GameObject.CreatePrimitive(type);
    part.name = name;
    part.transform.SetParent(root.transform, false);
    part.transform.localPosition = position;
    part.transform.localScale = scale;
    part.GetComponent<Renderer>().sharedMaterial = material;
    Object.DestroyImmediate(part.GetComponent<Collider>());
    return part;
}
Part(PrimitiveType.Cube, "Base", new Vector3(4.5f, 1f, 4.5f), new Vector3(9f, 2f, 9f));
Part(PrimitiveType.Cube, "Anchor", new Vector3(4.5f, 3f, 4.5f), new Vector3(3f, 2f, 3f));
var cable = Part(PrimitiveType.Cylinder, "Cable", new Vector3(4.5f, 44f, 4.5f), new Vector3(0.3f, 40f, 0.3f));
var climber = Part(PrimitiveType.Cube, "Climber", new Vector3(4.5f, 5f, 4.5f), new Vector3(2f, 1.5f, 2f));

// クリック判定は土台全体を覆う1つの箱にする
// One box covering the whole footprint is the click target
var clickCollider = new GameObject("ClickCollider");
clickCollider.transform.SetParent(root.transform, false);
var box = clickCollider.AddComponent<BoxCollider>();
box.center = new Vector3(4.5f, 2f, 4.5f);
box.size = new Vector3(9f, 4f, 9f);

var processor = root.AddComponent<SpaceElevatorBaseStateChangeProcessor>();
var serialized = new SerializedObject(processor);
serialized.FindProperty("cable").objectReferenceValue = cable;
serialized.FindProperty("climber").objectReferenceValue = climber.transform;
serialized.FindProperty("climberBottomLocalPosition").vector3Value = new Vector3(4.5f, 5f, 4.5f);
serialized.FindProperty("climberTopLocalPosition").vector3Value = new Vector3(4.5f, 83f, 4.5f);
serialized.ApplyModifiedPropertiesWithoutUndo();

var prefabPath = "Assets/AddressableResources/Block/SpaceElevatorBase.prefab";
PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
Object.DestroyImmediate(root);

var settings = AddressableAssetSettingsDefaultObject.Settings;
var group = settings.FindGroup("Vanilla Asset Group");
var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(prefabPath), group);
entry.address = "Vanilla/Block/SpaceElevatorBase";
settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
AssetDatabase.SaveAssets();
return $"{entry.address} -> {prefabPath}";
```
Expected: `Vanilla/Block/SpaceElevatorBase -> Assets/AddressableResources/Block/SpaceElevatorBase.prefab`。`git status` で prefab・mat・その .meta・`Vanilla Asset Group.asset` の変更だけが出ることを確かめる（.meta は Unity が生成したもの）。

- [ ] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/SpaceElevator moorestech_client/Assets/AddressableResources/Block/SpaceElevatorBase* "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"
git commit -m "feat(client): 宇宙エレベーター拠点の簡易モデルと昇降の見た目を足す"
```

### Task 12: シミュレーションにエレベーターを足して値を確定する

**Files:**
- Modify: `tools/balance/tier9_endgame_balance.py`（moorestech_master worktree）
- Modify（値を直す場合）: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`、`items.json`

**Interfaces:**
- Consumes: Task 4 のスクリプト（`blocks`, `items`, `net_power`, `check`, `failures`, `load`）、Task 5・6 のマスタ、S2 の発射台の建造時間
- Produces: エレベーターの検査行（exit 0 で値が確定）

- [ ] **Step 1: 検査を足す**

S2 の `VanillaSchema/blocks.yml` の発射台の case（ロケット発射台の blockType）で、ロケット1機の建造時間（秒）のキー名を読む。`tier9_endgame_balance.py` の `if failures:` の直前に足す（`<キー名>` を読んだ名前にする）:
```python
# 宇宙エレベーター: 1エレベーター分はケーブル1便、昇降はロケットより遅く、電力は炉1基の一部
# Space elevator: one elevator equals one cable launch, climbs are slower than rockets, and power is a fraction of one reactor
ROCKET_BUILD_SECONDS_KEY = '<キー名>'
elevator = blocks['宇宙エレベーター拠点']['blockParam']
rocket_seconds = blocks['ロケット発射台']['blockParam'][ROCKET_BUILD_SECONDS_KEY]
cable_row = next(row for row in load('items.json')['orbitalPayloads'] if row['payloadItemGuid'] == items['グラフェンケーブル'])
check('1エレベーター分 / ケーブル1便', elevator['cablesPerElevator'] / cable_row['countPerLaunch'], 1, 1)
check('昇降時間 / ロケット建造時間', elevator['climbSeconds'] / rocket_seconds, 1.5, 4)
check('昇降中の電力 / 炉1基の差し引き電力', elevator['requiredPower'] / net_power, 0.1, 0.5)
```

- [ ] **Step 2: 実行して値を確定する**

Run: `python3 tools/balance/tier9_endgame_balance.py`
Expected: 全行 OK、exit 0。NG なら `climbSeconds`・`requiredPower` を直して再実行し、直した値と出力の行を判断記録に書く。値を変えたらテスト mod は変えない（テスト mod の値はテスト用で独立）。

- [ ] **Step 3: コミットして push する**

```bash
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame add tools/balance server_v8
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame commit -m "tools: エレベーターの数値をシミュレーションで確定する"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame push
```

### Task 13: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/tier9-endgame-fusion-and-elevator.cs`

**Interfaces:**
- Consumes: Task 1〜12 のすべて（出荷マスタは S3 のマスタ worktree）
- Produces: 録画・スクショ・`result.json`（PR 本文に貼る）

- [ ] **Step 1: シナリオを書く**

unity-playmode-recorded-playtest スキル（`references/write-scenario.md`・`references/run-scenario.md`）を読む。`scenarios/building/tier9-endgame-fusion-and-elevator.cs`（ブロックを開く操作は `scenarios/connect/rotary-generator-ui.cs:158-182` の `AimAtClickCollider`/`OpenGenerator`、電線は `scenarios/misc/cleanroom-v2.cs:58-64` の `TryConnectBothSides`＋`RebuildAround` と同じ形）:
```csharp
using System;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Control;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.Fluid;
using Game.Orbit;
using Game.SpaceElevator;
using MessagePack;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tier9-endgame-fusion-and-elevator", options, async p =>
{
    var reactorPos = new Vector3Int(0, 32, 0);
    var elevatorPos = new Vector3Int(22, 32, 0);
    var deuteriumId = MasterHolder.FluidMaster.GetFluidId(Guid.Parse("01a167e9-21b2-7bb0-b6a3-0f17d349704e"));
    var cableId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("01a16290-f793-748b-a0b0-91d44be0d4ab"));
    var satelliteId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("01a167cf-af8b-7894-8de6-593ee30289c0"));

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    if (p.CurrentUiState != UIStateEnum.GameScreen) await p.SkipOpeningSkit();
    InputSystem.AddDevice<Keyboard>();
    InputSystem.AddDevice<Mouse>();
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "録画の表示言語を日本語に設定");

    // 核融合炉に重水素を入れて開き、稼働表示と燃料タンクを確かめる
    // Fuel the fusion reactor with deuterium, open it and check the operating display and fuel tank
    p.Note("核融合炉に重水素を入れて開き、発電の稼働表示と燃料タンクが出ることを確かめる");
    var reactor = p.PlaceBlockDirect("核融合炉", reactorPos, BlockDirection.North);
    var reactorObject = await p.WaitBlockGameObject(reactorPos);
    reactor.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(150, deuteriumId), default);
    p.WarpPlayer(new Vector3(9.5f, 33.5f, -4f));
    await OpenBlock(reactorObject);
    await p.UntilWebUiElement("generator-section", 15f);
    await p.UntilWebUiElement("generator-fluid-slots", 15f);
    await p.Screenshot("01-fusion-reactor-operating");
    await p.ClickWebUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 拠点を置き、炉から電線で給電する
    // Place the base and power it from the reactor by wire
    p.Note("宇宙エレベーター拠点を置き、核融合炉と電線でつなぐ");
    var elevator = p.PlaceBlockDirect("宇宙エレベーター拠点", elevatorPos, BlockDirection.North);
    var elevatorObject = await p.WaitBlockGameObject(elevatorPos);
    var freeCost = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var reactorWire = reactor.GetComponent<IElectricWireConnector>();
    var elevatorWire = elevator.GetComponent<IElectricWireConnector>();
    p.Assert(ElectricWireSystemUtil.TryConnectBothSides(reactorWire, elevatorWire, freeCost), "炉と拠点を電線でつないだ");
    p.ServerService<IElectricWireNetworkDatastore>().RebuildAround(reactorWire, elevatorWire);

    // ケーブルが無いと接続できず理由が出る
    // Without cables the base cannot connect and shows why
    p.WarpPlayer(new Vector3(26.5f, 33.5f, -4f));
    await OpenBlock(elevatorObject);
    await p.UntilWebUiElement("space-elevator-section", 15f);
    await p.UntilWebUiElement("space-elevator-insufficient", 15f);
    await p.Screenshot("02-elevator-not-enough-cable");

    // ケーブル1便の軌道到着でプールを満たし、接続する
    // Fill the pool with one cable launch arriving in orbit, then connect
    p.Note("グラフェンケーブル1便（1000本）の軌道到着をサーバーで起こし、接続ボタンを押す");
    p.ServerService<IOrbitalArrivalService>().Arrive(cableId, 1000);
    await p.Until(() => Detail(elevator).CablePoolAmount == 1000, 10f, "プールが1000本になった");
    await p.ClickWebUi("space-elevator-connect");
    await p.Until(() => Detail(elevator).IsConnected, 10f, "拠点が接続済みになった");
    await p.Until(() => p.ServerService<SpaceElevatorCablePool>().Amount == 0, 10f, "プールから1エレベーター分が引かれた");
    var cableObject = elevatorObject.GetComponentsInChildren<Transform>(true).First(t => t.name == "Cable").gameObject;
    var climberObject = elevatorObject.GetComponentsInChildren<Transform>(true).First(t => t.name == "Climber");
    await p.Until(() => cableObject.activeSelf, 10f, "ケーブルが現れた");
    await p.Screenshot("03-elevator-connected");

    // 衛星を入れると電力を使って昇る
    // A satellite in the inlet climbs using power
    p.Note("GPU衛星Lv1を入口に入れ、昇降機がケーブルを昇ることを確かめる");
    elevator.GetComponent<SpaceElevatorBaseInventoryComponent>().SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, satelliteId, 1);
    await p.Until(() => Detail(elevator).Status == SpaceElevatorBaseStatus.Climbing || Detail(elevator).Status == SpaceElevatorBaseStatus.LowPower, 15f, "昇降が始まった");
    var startY = climberObject.localPosition.y;
    await p.WaitSeconds(10f);
    p.Assert(climberObject.localPosition.y > startY, $"昇降機が上がった start={startY:F2} now={climberObject.localPosition.y:F2}");
    p.Assert(Detail(elevator).ClimbProgress > 0f, $"昇降の進みが増えた progress={Detail(elevator).ClimbProgress:F3}");
    await p.Screenshot("04-elevator-climbing");
    await p.ClickWebUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 接続済みの拠点を撤去すると1エレベーター分がプールへ戻る
    // Removing the connected base returns one elevator's worth to the pool
    p.Note("接続済みの拠点を撤去し、プールに1000本が戻ることを確かめる");
    p.Assert(p.RemoveBlock(elevatorPos), "拠点を撤去した");
    await p.Until(() => p.ServerService<SpaceElevatorCablePool>().Amount == 1000, 10f, "プールに1000本が戻った");

    #region Internal

    SpaceElevatorBaseStateDetail Detail(IBlock block)
    {
        var details = block.GetComponent<SpaceElevatorClimberComponent>().GetBlockStateDetails();
        return MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(details[0].Value);
    }

    async UniTask OpenBlock(BlockGameObject blockObject)
    {
        // 通常画面ではAltでカーソル照準を解放し、Fの実操作で開く
        // Free cursor aiming with Alt in gameplay and open with the real F interaction
        SemanticInput.KeyDown(Key.LeftAlt);
        await UniTask.DelayFrame(3);
        var collider = blockObject.GetComponentsInChildren<Collider>(true).OrderByDescending(c => c.bounds.size.sqrMagnitude).First();
        await p.AimAt(new Vector3(collider.bounds.center.x, collider.bounds.min.y + 0.4f, collider.bounds.center.z));
        var selector = new InteractTargetSelector();
        await p.Until(() => selector.Scan().Primary?.GameObject == blockObject.gameObject, 10f, $"F操作前の対象が {blockObject.name}");
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
    }

    #endregion
});
```
（`ConnectionLineRecord` の接続ツール GUID・`IElectricWireNetworkDatastore` の名前空間・`PressInteract` の有無は cleanroom-v2.cs / rotary-generator-ui.cs の実物に合わせる。F で開けない位置なら `WarpPlayer` の座標だけを直す）

- [ ] **Step 2: 調査実行（録画なし）で通す**

`references/run-scenario.md` の手順どおり、`uloop control-play-mode --project-path ./moorestech_client --action stop` → シナリオを `moorestech_client/PlaytestResults/scratch/` へコピーして `Record = false` にし、前景で実行する（Bash の timeout 600000）:
```bash
SKILL=.claude/skills/unity-playmode-recorded-playtest
SCRATCH="$PWD/moorestech_client/PlaytestResults/scratch"
mkdir -p "$SCRATCH"
cp "$SKILL/scenarios/building/tier9-endgame-fusion-and-elevator.cs" "$SCRATCH/tier9-endgame-fusion-and-elevator.cs"
sed -i '' 's/Record = true/Record = false/' "$SCRATCH/tier9-endgame-fusion-and-elevator.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SCRATCH/tier9-endgame-fusion-and-elevator.cs" $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame/server_v8
```
Expected: `result.json` の `Success: true`、`Asserts` が全部 passed。`uloop get-logs --project-path ./moorestech_client --log-type Error` に `CommonMachineBlockStateChangeProcessor` と `SpaceElevatorBaseStateChangeProcessor` の行が無い。落ちたら原因を直して（シナリオの座標だけでなく本体の不具合なら本体を）再実行する。

- [ ] **Step 3: 録画ありで最終実行し、目で確かめる**

`Record = true` のまま同じコマンドで実行する。録画から `ffmpeg -i <動画> -vf fps=1 <scratch>/frames/%03d.png` でフレームを切り出し、(1) 炉のパネルに発電の稼働表示と燃料タンク、(2) 拠点パネルのケーブル不足の理由、(3) 接続後にケーブルが現れる、(4) 昇降機がケーブルを昇る、を目で確かめる。確かめたフレームのパスを判断記録に書く。動画は `share-put <動画> -n tier9-endgame-fusion-and-elevator` で置き、出た URL を PR 本文に貼る。

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/tier9-endgame-fusion-and-elevator.cs
git commit -m "test(playtest): 核融合炉と宇宙エレベーターの録画シナリオを足す"
```

### Task 14: 全ブランチレビュー

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

Codex ホストでは moores-code-review の `references/codex-host.md` の runner で実行する。レビュー指摘の反映がソースの判定経路・条件式・その評価時点（昇降機の開始条件・接続判定・保留の流し込み・撤去返却など）に触れたら、Task 13 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする。指摘の反映ごとにコミットする。

### Task 15: PR 作成とセッションを閉じられる状態

- [ ] **Step 1: セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**

順序:
1. moorestech_master: worktree の全コミットを push し、`gh pr create`（base `master`、本文に変更点・シミュレーションの出力・S2 の PR への依存）で PR を作る。
2. moorestech: `.moorestech-external-revisions.json` の `moorestech_master` を 1. の push 済み HEAD（`git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame rev-parse HEAD`）にして単独でコミットし、`git show HEAD:.moorestech-external-revisions.json` で値を確かめて push する。
3. moorestech: pr-create スキルで PR を作る（本文に master PR の URL・S2 の PR への依存・録画の share URL・判断記録の「S1 待ち」「S2 待ち」の一覧）。マージはしない。
4. 両 PR の CI の結果を確かめる。
5. `moores-wt rm tier9-fusion-elevator-endgame`（Editor と worktree を畳む）。マスタ worktree は `git -C $HOME/hermes-agent/data/repos/moorestech_master worktree remove $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame` で外す（push 済みを確かめてから）。

---

## 判断記録（ADR）

### 設計 ADR と S3 の裁定

- ADR: `docs/adr/0089-space-elevator-cable-pool-and-per-base-connection.md`（ケーブルプール・拠点ごとの接続・昇降機・軌道到着サービスへの委譲）、`docs/adr/0090-fusion-net-power-from-fuel-extraction-cost.md`（核融合の差し引き電力・炉は既存の燃料発電機）
- S3 のユーザー裁定（`.decisions/`）:
  - `2026-10-23-ストーリークリアは定義せず反物質爆弾を規定量作る仮の研究で終端にする.md`（原文「クリアについて現段階で明確に定義しない。反物質爆弾を規定量つくったらそれでおわり（終了を明示する仮の研究があってもok、その程度」）
  - `2026-10-23-宇宙エレベーターは接続でGPU人工衛星の打ち上げ経路として機能させる.md`
  - `2026-10-23-エレベーター経由の衛星打ち上げは燃料と筐体が不要だが入手に時間がかかる.md`
  - `2026-10-23-エレベーター拠点は1基につき昇降機1台で衛星を1つずつ運ぶ.md`
  - `2026-10-23-打ち上げたケーブルはワールド共通の素材プールに入り拠点ごとの接続で消費し撤去で戻る.md`
  - `2026-10-23-エレベーター拠点は簡易モデルで接続でケーブルが現れ昇降機が上昇する.md`
  - `2026-10-23-エレベーター拠点は昇降中に電力を使い不足すると遅くなる.md`
  - `2026-10-23-核融合の収支は燃料づくりの大電力で作りモジュールは燃料側に効かせる.md`
  - `2026-10-23-核融合の収支は運転中の電力だけで評価し点火手順は設けず見た目は既存モデルと汎用UI.md`
  - `2026-10-23-反物質は大電力の新機械で作り既存の組立機械で爆弾にする.md`
  - `2026-10-23-グラフェンは既存機械の短い直線工程で作り使用済み銅板は電気炉で銅板に戻す.md`
  - `2026-10-23-モジュールは種類ごとに研究を分け機械オーバークロックは速度モジュールの研究にする.md`
  - `2026-10-23-Tier9のフィルター分岐器ノードは削除し品質モジュール研究は別途作る.md`
  - `2026-10-23-金属3Dプリンタはコンピュータと制御モータを1工程で作る任意のショートカット機械にする.md`
- 他セッションの裁定で S3 に効くもの: S1「Tier9 の目標プレイ時間は半導体解放からクリアまで 15〜25 時間」「チップは別アイテムで上限 Lv5・研究とクリアは大部分 Lv1」「モジュールは品質・速度・省エネ・生産性の4種」。S2「研究ロケット技術を新設」「軌道エレベーターの建設は計算データを消費しロケット技術を前提にする」「計算データは打ち上げた設備の出力口に出し、満杯なら待たせて理由を出す」

### planning 中の判断（すべて agent前提）

| # | 判断 | 理由・根拠 |
|---|---|---|
| P1 | 発電機（ElectricGenerator）に `IBlockStateObservable` を足し、`PowerGeneratorStateDetail` と液体タンク（`FluidMachineInventoryStateDetail`）を出す。通知は燃料の点き始め・燃え尽きの tick だけ | 裁定「既存モデル＋汎用UIの稼働表示」。前例 `FuelGearGeneratorComponent`（同じ状態詳細）。毎 tick の同値判定はしない（AGENTS.md「状態変化の検知は購読で」） |
| P2 | クライアントで ElectricGenerator を CommonMachine 扱いから外す | 原因: 推測（根拠: `CommonMachineBlockStateChangeProcessor.cs:23-28` は CommonMachine キーが無い状態で `Debug.LogError` を出す。発電機が状態を出すと必ず通る）。Task 13 の録画テストでエラーログが無いことを実測で確かめる |
| P3 | 発電機は専用 view を作らず汎用 `SectionStackView` で出し、e2e の registry では意図的 Generic にする | 前例「標準ブロック表示をデータ有無で合成し、固有UIだけをレジストリへ残す」 |
| P4 | `VanillaElectricGeneratorFuelService`（289行）と `VanillaElectricGeneratorComponent` を 200 行未満へ分割する。変換器が触るフィールドを `internal` にする | AGENTS.md の 200 行規約。同アセンブリの変換器だけが触る |
| P5 | 数値の初期値: 核融合炉 重水素10/2秒で 20000、重水素抽出装置 4000（水100→重水素10/8秒）、反物質製造装置 12000（重水素50→反物質1/30秒）、反物質爆弾 反物質20＋ICチップLv1 50＋鋼鉄板100＋コンピュータ20（電気汎用工作装置 60秒）、グラフェン4レシピと3Dプリンタ2レシピは Task 1・2 の JSON の値 | Task 4 のシミュレーション（R3・R5・R23 の範囲）で検査し、NG なら直して本節へ追記する。全体の時間目標は S1 の裁定 |
| P6 | 最終研究の名前は「最終研究」、消費は反物質爆弾 10、解放物なし | ユーザー原文「終了を明示する仮の研究があってもok、その程度」。量は agent前提（Task 4 で所要時間を検査） |
| P7 | 反物質の機械名は「反物質製造装置」 | 既存モデル名 `Antimatter_manufacturing_equipment` と ADR 0090 に合わせる。裁定ファイルの「反物質生成装置」は機能の言い換え（ユーザーが名前を決めた裁定ではない） |
| P8 | グラフェンケーブルは軌道到着の積荷表に 1便1000本・成果なしで載せ、スタック表 [1000] を割り当てる。1エレベーター分 1000 本＝1便。昇降 600 秒・昇降中の電力 2000 | S2 の契約「countPerLaunch ≤ スタック上限」。Task 12 で S2 のロケット建造時間と比べて確定する |
| P9 | 研究「軌道エレベーターの建設」の計算データ消費は S2 の plan の値（見つからなければ 200）、研究「反物質爆弾」に計算データ ×400 | S2 の裁定「計算データを消費する」。量は S2 待ち・S1 待ち |
| P10 | プールは起動時（`IBootInitializable.Load`）に、拠点のマスタが指すケーブル・積荷が軌道到着の表にあるか、積荷にケーブルが含まれないかを S2 の `IsOrbitalPayload` で確かめ、違えば例外で止める | ADR 0089。S2 のマスタ API が未確定のため、公開済みの契約（`IsOrbitalPayload`）だけで確かめる |
| P11 | ワールドセーブは版 7→8（S2 が 6→7）。移行ステップはプール残量 0 を実値として足す（補完ではない） | AGENTS.md「セーブ形式の例外」。旧版のワールドにはケーブルの打ち上げが無いので 0 が正しい値 |
| P12 | 撤去返却は `RemoveBlockProtocol` で全コンポーネントの `IGetRefundItemsInfo` を合算する（Task 8 Step 1） | 拠点は昇降中の衛星と保留を返すため。既存が先頭1つだけを読むなら直す |
| P13 | 接続は専用プロトコル `va:connectSpaceElevator` で、結果は列挙（Connected / AlreadyConnected / InsufficientCable / NotSpaceElevatorBase）。拒否はサーバーログに出し、プレイヤーには状態詳細のプール残量から理由を出す | ADR 0089「呼び出し側の線引き」。前例 `SetElectricToGearOutputModeProtocol`・`RemoveBlockProtocol` の拒否ログ |
| P14 | 拠点の UI は専用 view を作らず `SectionStackView` に固有部分 `SpaceElevatorSection` を合成する | 前例 `GearToElectricSection` |
| P15 | 新しいファイルはディレクトリの 10 ファイル上限のためサブディレクトリ（`PacketResponse/SpaceElevator/`・`Actions/SpaceElevator/`・`Topics/BlockDetail/SpaceElevator/`・`StateProcessor/SpaceElevator/`）に置き、`inventory.ts`（215 行）を分割する | AGENTS.md の規約 |
| P16 | 録画テストは昇降の完了（600 秒）まで待たず、昇降機が上がること・進みが増えることまでを見る。到着後の成果・保留はユニットテストで見る。ケーブルの打ち上げはサーバーで `Arrive(cable, 1000)` を呼んで起こす | 録画の長さ。本番との差: ロケットでの打ち上げ経路は通さない（S2 の録画が担う） |
| P17 | Task 5 は S2 の `feat(orbit): 軌道到着サービスと初打ち上げ履歴を足す` が push されるまで前景で 600 秒ずつ最大 12 回待ち、来なければ止めて報告する | S2 の API に依存するため。バックグラウンドのポーリングはしない |
| P18 | ランタイム挙動（発電機の UI・拠点の UI・見た目・昇降）に触れるので、unityプレイ録画テスト（Task 13）を入れる | moores-writing-plans の録画テスト規定 |

### S1 待ち・S2 待ち

- S1 待ち: Tier9 研究のコスト（核融合炉・反物質爆弾・金属超精密加工・軌道エレベーターの建設の `consumeItems`）、機械のモジュールスロット数（重水素抽出装置・反物質製造装置・金属3Dプリンタは仮に 3）、レシピのチップ Lv（すべて ICチップLv1 の仮置き）、研究「機械オーバークロック」（S1 が速度モジュール研究として作る。S3 は触らない）
- S2 待ち: `Game.Orbit` の push（Task 5 のゲート）、計算データの消費量（P9）、ロケット1機の建造時間のキー名と値（Task 12）、GPU衛星 Lv1〜Lv5 の到着成果（S2 の表を読むだけ）

### 進め方の裁定

- ユーザー指示 2026-10-23 17:51「とりあえずまだ実装開始しないで」: plan の作成・検査・コミットまでで止め、Codex の実装セッションは起動しない。開始はユーザーの合図を待つ
