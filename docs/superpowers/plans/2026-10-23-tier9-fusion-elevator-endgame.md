# Tier9 終盤（核融合・反物質・宇宙エレベーター・終端研究） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Tier9 の終盤（核融合炉・重水素抽出装置・反物質製造装置・反物質爆弾・終端研究・宇宙エレベーター拠点とケーブルプール・グラフェン工程・金属3Dプリンタ・研究ツリー整理）のうち、コードで作る部分（発電機の稼働表示・宇宙エレベーター拠点・ケーブルプール・接続・昇降機・拠点 UI・簡易モデル）を、テスト用マスタで確かめた状態で入れる。moorestech_master の中身（液体・アイテム・ブロック・レシピ・研究・バランス）と unityプレイ録画テストは、S1〜S3 の実装がそろってからの統合タスク（bd `moorestech-xyuaf.2`）で入れる（下書きは末尾の「統合タスクへ引き継ぐマスタの中身」節）。この plan の成果は、統合タスクの後に遊べる状態になる。

**Architecture:** 核融合・反物質・グラフェン・金属3Dプリンタは既存の ElectricGenerator / ElectricMachine とマスタだけで作るので、この PR のコードは発電機の稼働表示（状態詳細）に限り、マスタは統合タスクで入れる。宇宙エレベーターは新しいブロック種別 `SpaceElevatorBase` と、ワールド単位のケーブルプール（新アセンブリ `Game.SpaceElevator`）で作る。プールは S2 の軌道到着サービス（`Game.Orbit`）の到着通知を購読してケーブルを数え、拠点は「接続」ボタン（専用プロトコル）でプールから1エレベーター分を引く。昇降機は衛星を1つずつ電力で運び、昇り切ったら軌道到着サービスへ渡して返ってきた成果を出力口へ入れる。昇り始める前と渡す直前に、成果が出力口へ入りきるかを軌道到着サービスの成果の問い合わせで確かめ、入らなければ待たせて理由を出す（打ち上げ後の保留は持たない）。

**Tech Stack:** Unity / C#（moorestech_server, moorestech_client）、MessagePack、UniRx、Microsoft.Extensions.DependencyInjection、React + zod（moorestech_web/webui）、テスト用 mod のマスタ JSON

## Requirements

設計 ADR: `docs/adr/0089-space-elevator-cable-pool-and-per-base-connection.md`、`docs/adr/0090-fusion-net-power-from-fuel-extraction-cost.md`。裁定: `.decisions/2026-10-23-*`（S3 分は本 plan 末尾『判断記録（ADR）』に一覧）。用語: `CONTEXT.md` の「宇宙エレベーター」「核融合」「終盤」。

R1〜R3・R5〜R13・R23 は統合タスクへ移した（末尾の節の「移した要件」）。番号は判断記録から引くので振り直さない。

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R4 | 発電機（ElectricGenerator）を開くと稼働状態（稼働率・残燃料・燃料タンク）が汎用UIに出る | サーバーテストで状態詳細が出る。webui で `generator-section` と `generator-fluid-slots` が出る（核融合炉を `openable: true` にするのは統合タスク） |
| R14 | ワールドに1つのケーブルプールが、ケーブルの軌道到着で増える | サーバーテスト: `Arrive(cable, N)` でプール残量が N 増える。ケーブル以外の到着では増えない |
| R15 | 拠点ごとの「接続」でプールから1エレベーター分を引く。足りなければ接続できず理由を UI に出す | サーバーテスト: 足りれば Connected・残量が減る／足りなければ InsufficientCable・残量不変。プロトコルテスト・webui の理由表示 |
| R16 | 接続済み拠点を撤去すると1エレベーター分がプールへ戻る。未接続の撤去は影響しない | サーバーテスト（ユーザー原文の 200→2基接続→+50→1基撤去で150→3基目接続可 のシナリオ） |
| R17 | 昇降機は1拠点1台で GPU 衛星を1つずつ運び、昇降中だけ電力を使い、不足すると遅くなる | サーバーテスト: 昇降時間・電力不足で遅くなる・未接続では昇らない |
| R18 | 昇り切ったら軌道到着サービスへ渡し、返ってきた成果を出力口へ入れる。昇り始める前と渡す直前に成果が出力口へ入りきるかを確かめ、入らなければ待たせて理由を出す | サーバーテスト: 成果が出力口に出る／出力口が塞がっていると昇り始めない・昇り切っても渡さず待つ／空くと進む。成果が空の出力口にも入らないマスタは起動時に止まる |
| R19 | 接続済みと昇降中の衛星はセーブに入り、撤去時は衛星がプレイヤーへ返る | サーバーテスト: セーブ往復で復元／撤去でプレイヤーインベントリへ返る |
| R20 | プール残量はワールドセーブに入る。セーブ形式の変更は版上げ＋移行ステップ＋テストを同梱する | `WorldSaveAllInfo.CurrentVersion` 7→8、`SaveMigrationStepV7ToV8`、移行テストと往復テスト |
| R21 | 拠点は簡易モデルで、接続でケーブルが現れ、昇降中は昇降機がケーブルを昇る | アドレス `Vanilla/Block/SpaceElevatorBase` のプレハブがあり、processor が状態詳細の接続と進みからケーブルの表示と昇降機の位置を決める。目視は統合タスクの録画 |
| R22 | 拠点 UI に接続ボタン・プール残量／1エレベーター分・昇降の進み・待ちの理由を出す | webui unit/e2e テスト（mock host） |
| R24 | この PR は moorestech_master を変えない。マスタの中身と録画テストは統合タスクで入れる | moorestech_master への差分が0。`.moorestech-external-revisions.json` は Task 5 で S2 を取り込んだ値のまま |

**やらないこと（スコープ境界）:**
- エンディング・クリア状態・最終ボタン・スキット（裁定で不採用）
- 炉本体へのモジュール・点火・再起動の手順（裁定で不採用）
- 専用のオーバークロック機能、確率振り分けのフィルターコンベア（裁定で不採用）
- モジュールの種類・値・レシピ・研究コスト、チップのレベル、GPU の性能、Tier9 研究コストの最終値（S1 の担当。S3 の下書きの仮値は統合タスクで決め直す）
- ロケット発射台・GPU衛星・計算データ・軌道到着サービス本体・初打ち上げ状態（S2 の担当。S3 は取り込んで使うだけ）
- クリーンルーム・チップ抽選・モジュールのコード（S1 の担当。触らない）
- moorestech_master の中身・バランスのシミュレーション・unityプレイ録画テスト（統合タスク bd `moorestech-xyuaf.2`。裁定は判断記録 P29。スタックテーブルが空の7件は S2 の master PR が直すので S3 は触らない）

## Global Constraints

- 作業場所: moorestech worktree `$HOME/hermes-agent/data/worktrees/moorestech/tier9-fusion-elevator-endgame`（branch `feat/tier9-fusion-elevator-endgame`）。最初に `pwd` で確かめる。moorestech_master の worktree は作らない
- PR 作成まで。moorestech はマージしない。force-push しない（S2 を取り込むため）
- 本 repo の `.moorestech-external-revisions.json` は Task 5 で S2 を取り込んだ値（S2 の master PR の head）から動かさない。S3 はマスタを変えないので書き換える理由が無い。各コミットの前に `git diff -- .moorestech-external-revisions.json` が空であることを確かめ、Editor が書き換えていたら `git checkout -- .moorestech-external-revisions.json` で戻す
- Unity は自分の worktree の Editor だけを使う（`moores-wt status` で所有者確認）。自分で起動したプロセスは PID を控えて `kill <PID>` で止める（`pkill -f` 禁止）
- バックグラウンドのポーリングで待たない。待つときは前景で `sleep` を挟む。区切り（各タスクの最後）ごとにコミットする
- .cs を変えたら `uloop compile --project-path ./moorestech_client`（ErrorCount 0）。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。Domain Reload 中エラーは 45 秒待って再試行
- 1ファイル200行未満。1ディレクトリ10ファイルまで。partial・`Func<>`・デフォルト引数・単純 getter/setter プロパティ（`{ get; private set; }` は可）・イベント用 `Action` は禁止（UniRx を使う）。try-catch は外部境界のみ
- コメントは主要処理の 3〜10 行ごとに日本語1行＋英語1行。`#region Internal` はメソッド内ローカル関数だけ
- fail-closed（拒否・保留・無視）する分岐は理由を `Debug.Log`/`LogWarning` に出す。無音の縮退は禁止
- サーバー時間は `GameUpdater` の tick だけ（`SecondsToTicks`/`TicksToSeconds`）
- .meta を手で作らない。Prefab・Scene・ScriptableObject・Addressables の .asset をテキスト編集しない（`uloop execute-dynamic-code` 経由は可）。Library を消さない
- スキーマ（`VanillaSchema/*.yml`）を変えるときは edit-schema スキルに従う（`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を変えて再生成、test mod・`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/`・`mooresmaster/mooresmaster.SandBox/` の JSON も更新）
- セーブ形式を変える PR は moorestech-save-migration スキルが正本（版上げ・移行ステップ・3箇所登録・移行テスト・実ロード検証）
- コードとテストはテスト用 mod の値だけを使う。出荷マスタの GUID・数値に依存するコードを書かない（出荷マスタに拠点が無くても起動・テストが通ること）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反6件・修正6件 / 発電機を燃焼中は毎tick通知・拠点 UI を専用 view へ・状態詳細からマスタ値を外す・昇降の残りを秒で保存・スキーマの切り出しをサブディレクトリへ・型を payloadTypes へ。注目点16件のうち3件（前例の差し替え・未着ログ・P2 の比較）と古い P10 を直し、新しい形（起動時のマスタ整合検査サービス）はレビュー依頼の注目点に回す。死活表6変更はすべて生きる。再検査（裁定の反映後）: 違反4件・修正4件（昇降機の部品の登録の場所・状態詳細を専用 view へ・wire 契約テストを BlockDetail へ・usingの不足）、注目点の古い2件（validator とプロトコルのコメント）を直し、前例が割れている5件は P31 でレビュー依頼の注目点へ。死活表の退化0
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強6・弱11・第3バケツ1 / 強6はユーザー裁定で接続の判定をサーバーへ（P26）、契約型を Interface 層へ（P24）・optional と列挙と翻訳キーは前例どおり（P25）・状態詳細は平らなまま（P28）を前提として宣言。弱11のうち7件はユーザー裁定の4束で直し（P27）、残り4件は派生で閉じているか直し済み・前例どおりで見送り。第3バケツの1件（出口の模擬の重複）は PR 本文の「本PR外のリファクタ提案」（P30）。再検査（裁定の反映後）: 強0・弱4・第3バケツ1 / 弱は入口の運べないアイテムを3状態にして1回ログ（A）と壊れたセーブの昇降中衛星の整え（B）を直し、出口に入るかの数え方（C）と拠点マスタの列挙（D）は理由を書いて直さない（P32・シミュレーター予測で前提宣言）。第3バケツ（燃料サービスの重複）は P30(2)

---

## 依存関係と順序

```
Task 3 発電機の稼働表示（server/client/webui）（S2 非依存。先にやる）
Task 5 S2 取り込みゲート（moorestech だけ）
Task 6 スキーマ SpaceElevatorBase とマスタ検証・テスト mod
Task 7 ケーブルプールとワールドセーブ（版 7→8）
Task 8 拠点ブロック（インベントリ・昇降機・出力口の確認・撤去返却）
Task 9 接続プロトコル
Task 10 拠点の webui
Task 11 拠点の簡易モデルと昇降の見た目
Task 14 moores-code-review
Task 15 PR 作成
（Task 1・2・4・12・13 は統合タスクへ移した。番号は判断記録と末尾の節から引くので振り直さない）
```

Task 5 は S2 の `Game.Orbit` が push されていないと進めない。Task 5 Step 1 のゲート手順に従う。

## File Structure

moorestech_master のファイル・バランスのツール・録画シナリオは統合タスクへ移した（末尾の節）。

| ファイル | 責務 | タスク |
|---|---|---|
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator/VanillaElectricGeneratorComponent.cs` | 発電機本体＋状態詳細の発火（200行未満へ圧縮） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorSaveJsonObject.cs`（新） | 発電機セーブ JSON（本体から移動） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorFuelService.cs` | 燃料管理＋状態用の読み取り口（200行未満へ分割） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorFuelSettingsBuilder.cs`（新） | 燃料設定辞書の構築（移動） | 3 |
| `.../PowerGenerator/VanillaElectricGeneratorFuelSaveConverter.cs`（新） | 燃料状態のセーブ書き出し・復元（移動） | 3 |
| `.../PowerGenerator/PowerGeneratorStateDetail.cs` | 秒値から作る ctor を追加 | 3 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectPrefabContainer.cs` | ElectricGenerator を CommonMachine 扱いから外す | 3 |
| `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx` | ElectricGenerator に液体行を出す | 3 |
| `VanillaSchema/blocks.yml`・`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/SpaceElevatorBaseParamValidator.cs`（新）・テスト mod の `{blocks,items,buildMenu}.json` | `SpaceElevatorBase` の blockType と param、マスタ検証、テスト用の拠点・ケーブル・衛星・積荷行 | 6 |
| `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator/{ISpaceElevatorBase,SpaceElevatorConnectResult}.cs`（新） | プール・接続処理が拠点に触る面と接続結果（クライアントも読む契約型） | 7 |
| `moorestech_server/Assets/Scripts/Game.Block.Interface/State/SpaceElevator/{SpaceElevatorBaseStateDetail,SpaceElevatorBaseStatus}.cs`（新） | 拠点の状態詳細と状態の列挙（クライアントも読む契約型） | 8 |
| `moorestech_server/Assets/Scripts/Game.SpaceElevator/`（新 asmdef） | `SpaceElevatorCablePool`（残量・到着購読・セーブ）、`SpaceElevatorConnectionService`（接続・撤去返却・残量 push）、`SpaceElevatorOrbitalTableCheck`（起動時の表との突き合わせ）、`SpaceElevatorBaseMasters`（拠点のマスタの列挙）、`Save/SpaceElevatorCablePoolSaveData` | 7 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/...` | WorldSaveAllInfo・保存・復元・移行・pruner | 7 |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/`（新） | 拠点のインベントリ・昇降機・電力・出口の模擬・セーブと復元 | 8 |
| `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/SpaceElevator/VanillaSpaceElevatorBaseTemplate.cs`（新） | 拠点の組み立て | 8 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs` | 返却情報を全コンポーネントから合算 | 8 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SpaceElevator/ConnectSpaceElevatorProtocol.cs`（新） | 接続リクエスト（`PacketResponse/` 直下は 50 ファイル超のためサブディレクトリ） | 9 |
| `moorestech_client/Assets/Scripts/Client.Network/API/Requests/BlockRequestApi.cs` | 接続 API | 10 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/SpaceElevator/`（新） | 拠点の DTO と DTO ビルダー | 10 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/SpaceElevator/SpaceElevatorConnectActionHandler.cs`（新） | `space_elevator.connect` | 10 |
| `moorestech_web/webui/src/bridge/contract/schemas/{spaceElevator/index.ts,train/index.ts,beltThroughput/index.ts,inventory.ts}`・`moorestech_web/webui/src/bridge/contract/payloadTypes.ts` | 受信スキーマ（inventory.ts を S2 と同じ分け方で 200 行未満へ。`schemas/` 直下は既に10ファイルなので切り出し先はドメインごとの `index.ts`。前例 `schemas/energyConversion/index.ts`）と `z.infer` の型 | 10 |
| `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/`（新）・`views/SpaceElevatorInventory.tsx`（新）・`registry/{blockComponentRegistry.ts,registeredBlockTypes.ts}` | 拠点の固有部分（接続ボタン・プール・進み・待ちの理由）と、それを載せる専用ビューのレジストリ登録 | 10 |
| `moorestech_web/webui/e2e/...` | mock host・拠点の e2e | 10 |
| `Localization/localization.csv` | 拠点 UI の文言 | 10 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/SpaceElevator/SpaceElevatorBaseStateChangeProcessor.cs`（新） | ケーブルの表示と昇降機の位置 | 11 |
| `moorestech_client/Assets/AddressableResources/Block/SpaceElevatorBase.prefab`（新、execute-dynamic-code で生成） | 簡易モデル | 11 |

### 配置と前例

| 配置 | 前例（役割が同じもの） |
|---|---|
| ケーブルプール（ワールド単位の可変状態＋セーブ節）を独立アセンブリ `Game.SpaceElevator` に置く | `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`（ワールド単位の部屋状態を独立アセンブリで持ち、`WorldSaveAllInfo` の節へ保存） |
| プールが拠点へ残量と「接続できるか」を `SetCablePoolAvailability(int, bool)` で push し、拠点が状態変化を発火する | `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomMachineEffectService.cs`（`ICleanRoomMachine.SetCleanRoomEffect` へ push） |
| `ISpaceElevatorBase` を `Game.Block.Interface/Component/SpaceElevator/` に置き、名前空間は親の `Game.Block.Interface.Component` のまま（他のサブディレクトリも同じ） | `Game.Block.Interface/Component/CleanRoom/ICleanRoomMachine.cs`（`namespace Game.Block.Interface.Component`）。S2 も Interface・PacketResponse・Client.WebUiHost のサブディレクトリを親の名前空間に置く |
| プール・接続サービスを main コンテナの具象型 singleton＋`IBootInitializable` で起動時に生成・購読する | `moorestech_server/Assets/Scripts/Game.PlayerRiding/RemovedRidableRidingHandler.cs`（Game 層のサービスが `IBootInitializable.Load` でワールドのイベントを購読する同じ役割の前例）。initializer コンテナ側の `CleanRoomDatastore` 型は S2 の Orbit（main コンテナ）に依存できないので採らない |
| 撤去でのプール返却は `OnBlockRemoveEvent` 購読 | `CleanRoomDatastore` の ctor 購読（`OnBlockRemoveEvent` は `Destroy` より前に発火する: `WorldBlockDatastore.cs:38→43`） |
| 昇降機が出口に入りきるかを昇り始める前と渡す直前に確かめる | `Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs:49`・`ProcessingMachineProcessState.cs:118`（`VanillaMachineOutputInventory.CanStoreOutputs`） |
| 拠点の電力消費（昇降中だけ要求・不足で遅く） | `Game.Block/Blocks/Pump/ElectricPumpComponent.cs`＋`ElectricPumpProcessorComponent.cs`（供給を加算し Update で latch）、`Game.Block/Blocks/Util/MachineCurrentPowerToSubSecond.cs` |
| 拠点のインベントリ（開ける・ベルト入出力） | `Game.Block/Blocks/Chest/VanillaChestComponent.cs` |
| 拠点の状態詳細（MessagePack）と状態の列挙を契約の層に置く | 置き場: `Game.Block.Interface/State/CommonMachineBlockStateDetail.cs`（名前空間 `Game.Block.Interface.State`）と S2 の `State/RocketLaunchPad/`。形（プロパティと Obsolete の ctor）: `Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorBlockStateDetail.cs` |
| 接続プロトコル | `Server.Protocol/PacketResponse/SetElectricToGearOutputModeProtocol.cs`（形）＋`RemoveBlockProtocol.cs` の拒否ログ（fail-closed のログ） |
| webui の拠点表示は専用 view `SpaceElevatorInventory` をレジストリに登録する（接続ボタンというブロック固有の操作を持つため。`SectionStackView` は「固有UIだけをレジストリへ残す」合成先で、`intentionalGeneric` は移植待ちの許可リスト） | `moorestech_web/webui/src/features/blockInventory/views/ElectricToGearInventory.tsx`・`views/TrainPlatformInventory.tsx`（固有操作を持つブロックの専用 view）＋`registry/blockComponentRegistry.ts` |
| 拠点の action と DTO | `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/ElectricToGearActions.cs`／`Topics/BlockDetail/ElectricToGearDetailDtoBuilder.cs` |
| 簡易モデルの見た目更新 | `Client.Game/InGame/BlockSystem/StateProcessor/SteamEngineEffectStateChangeProcessor.cs`（プレハブに貼る `[SerializeField]` 付き processor） |
| 発電機の状態詳細 | `Game.Block/Blocks/Gear/FuelGearGeneratorComponent.cs`（`PowerGeneratorStateDetail` を出す） |

---

## テスト mod の ID

テスト mod（`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`）に足す ID:

| 名前 | GUID |
|---|---|
| TestSpaceElevatorBase（ブロック） | 5e1ea7a0-0001-4000-8000-000000000001 |
| TestElevatorCable / TestElevatorSatellite / TestElevatorReward（アイテム） | 5e1ea7a0-0002-4000-8000-000000000001 / 5e1ea7a0-0002-4000-8000-000000000002 / 5e1ea7a0-0002-4000-8000-000000000003 |
| 拠点の入口・出口コネクタ | 5e1ea7a0-0003-4000-8000-000000000001 / 5e1ea7a0-0003-4000-8000-000000000002 |

---

### Task 1: （統合タスクへ移した）マスタ — 重水素・核融合炉・重水素抽出装置・反物質・反物質製造装置・反物質爆弾レシピ

この PR では実行しない（マスタの中身は S1〜S3 の実装後に統合タスク bd `moorestech-xyuaf.2` で入れる。判断記録 P29）。下書きは末尾「統合タスクへ引き継ぐマスタの中身」の「旧 Task 1」。

### Task 2: （統合タスクへ移した）マスタ — グラフェン工程・金属3Dプリンタ・研究ツリー整理・最終研究

この PR では実行しない（マスタの中身は S1〜S3 の実装後に統合タスク bd `moorestech-xyuaf.2` で入れる。判断記録 P29）。下書きは末尾「統合タスクへ引き継ぐマスタの中身」の「旧 Task 2」。

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
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Electric/ElectricGeneratorStateDetailTest.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `VanillaElectricGeneratorFuelService.Update()` の戻り値を `bool`（燃料の点き始め・燃え尽きの tick だけ true）に変える。発電機は「その tick に true、または燃焼中」なら状態を通知し、液体を受け入れたときも通知する（前例 `FuelGearGeneratorComponent.cs:88-91`・`VanillaMachineFluidInventoryComponent.cs:139`）
  - `VanillaElectricGeneratorFuelService`: `public bool IsBurning`、`public uint RemainingFuelTicks`、`public uint CurrentFuelTotalTicks`、`public bool TryGetFuelTank(out FluidMessagePack tank)`
  - `PowerGeneratorStateDetail(double remainingFuelSeconds, double currentFuelSeconds, float operatingRate)`
  - `VanillaElectricGeneratorComponent : IBlockStateObservable`（`PowerGenerator` と、液体燃料タンクがあれば `FluidMachineInventory` の状態詳細を出す）

- [x] **Step 1: テストを書く**

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

            // 注ぐだけでタンクの変化を通知する
            // Pouring alone notifies the tank change
            generator.AddLiquid(new FluidStack(fuel.Amount * 3, fluidId), default);
            Assert.AreEqual(1, notified);

            // 1tick進めると1単位が点き、タンクに2単位残る
            // One tick ignites one unit and leaves two in the tank
            GameUpdater.UpdateOneTick();

            var (power, tank) = ReadDetails(generator);
            Assert.AreEqual(1f, power.OperatingRate);
            Assert.AreEqual(fuel.Time, power.CurrentFuelTime, 0.01);
            Assert.AreEqual(fuel.Amount * 2, tank.Amount, 0.001);
            Assert.AreEqual(2, notified);
        }

        [Test]
        public void 燃焼中は毎tick残燃料を通知する()
        {
            var (generator, fuel, fluidId) = Setup();
            generator.AddLiquid(new FluidStack(fuel.Amount, fluidId), default);
            GameUpdater.UpdateOneTick();
            var (before, _) = ReadDetails(generator);

            var notified = 0;
            generator.OnChangeBlockState.Subscribe(_ => notified++);
            GameUpdater.RunFrames(3);

            // 残燃料バーが進むよう、燃えている間は毎tick通知する
            // Notify every tick while burning so the remaining-fuel bar advances
            var (after, _) = ReadDetails(generator);
            Assert.AreEqual(3, notified);
            Assert.Less(after.RemainingFuelTime, before.RemainingFuelTime);
        }

        [Test]
        public void 燃え尽きたら稼働率0になり通知が止まる()
        {
            var (generator, fuel, fluidId) = Setup();
            generator.AddLiquid(new FluidStack(fuel.Amount, fluidId), default);
            GameUpdater.UpdateOneTick();

            var fuelTicks = GameUpdater.SecondsToTicks(fuel.Time);
            for (var i = 0; i < fuelTicks + 1; i++) GameUpdater.RunFrames(1);

            // 燃え尽きた後は通知が止まる
            // Notifications stop once the fuel has burned out
            var notified = 0;
            generator.OnChangeBlockState.Subscribe(_ => notified++);
            GameUpdater.RunFrames(3);

            var (power, _) = ReadDetails(generator);
            Assert.AreEqual(0f, power.OperatingRate);
            Assert.AreEqual(0, notified);
        }

        private static (VanillaElectricGeneratorComponent, FuelFluidsElement, FluidId) Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GeneratorId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
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

- [x] **Step 2: 燃料サービスを分割し、状態用の読み取り口を足す**

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

- [x] **Step 3: 発電機本体に状態詳細を足し、200行未満へ縮める**

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

    // 点き始め・燃え尽きのtickと、燃焼中で残燃料が進むtickに通知する
    // Notify on ignite/burn-out ticks and on every burning tick as the remaining fuel advances
    var transitioned = _fuelService.Update();
    if (transitioned || _fuelService.IsBurning) _onChangeBlockState.OnNext(Unit.Default);
}

public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
{
    BlockException.CheckDestroy(this);
    var remainder = _fuelService.AddLiquid(fluidStack);

    // タンクが増えたら表示を更新する
    // Refresh the display when the tank gained fluid
    if (remainder.Amount < fluidStack.Amount) _onChangeBlockState.OnNext(Unit.Default);
    return remainder;
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
既存の `Update` と `AddLiquid` は上の形に置き換える。`using System; using Game.Block.Interface.State; using MessagePack; using UniRx;` を足す。`wc -l` で 200 行未満を確かめる。

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

- [x] **Step 4: クライアントと webui を直す**

`BlockGameObjectPrefabContainer.cs` の `IsCommonMachine` から `BlockTypeConst.ElectricGenerator or` の行を消す。
`SectionStackView.tsx:29` を次にする:
```tsx
  ElectricGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: "generator-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
```
（`renderEmptyGrid: false` は液体燃料だけの発電機で空の燃料グリッドを出さないため。アイテム燃料の発電機はスロットがあるので従来どおり出る。`showFluidProgress: false` は燃料の残りを `GeneratorSection` が出すので、新しく出る液体行に進みを重ねないため。これまで `fluidRowTestId: null` で液体行が出ていなかったので、`showFluidProgress` の値は効いていなかった）
`views/__tests__/SectionStackView.test.tsx` の `["ElectricGenerator", "generator-fuel-grid", null, true, true]` を `["ElectricGenerator", "generator-fuel-grid", "generator-fluid-slots", false, false]` にする。新しい worktree なので最初に `cd moorestech_web/webui && pnpm install --frozen-lockfile`。

出荷マスタの発電機はまだ開けない（`openable: false`）ので、e2e の registry fixture と `intentionalGeneric` は変えない。核融合炉を開けるようにしたときの e2e の追加は統合タスクで行う（末尾の節「旧 Task 3 Step 4 の e2e」）。

- [x] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricGeneratorStateDetailTest|PowerGeneratorTest|PowerGeneratorSaveLoadTest"`、`cd moorestech_web/webui && npx vitest run src/features/blockInventory && npx playwright test e2e/tests/block/blockRegistryCoverage.spec.ts`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/PowerGenerator moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Electric/ElectricGeneratorStateDetailTest.cs* moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectPrefabContainer.cs moorestech_web/webui/src/features/blockInventory/views
git commit -m "feat(generator): 電気発電機が稼働率と燃料タンクを状態詳細に出す"
```

### Task 4: （統合タスクへ移した）数値シミュレーション（核融合・反物質・グラフェン）

この PR では実行しない（マスタの中身は S1〜S3 の実装後に統合タスク bd `moorestech-xyuaf.2` で入れる。判断記録 P29）。下書きは末尾「統合タスクへ引き継ぐマスタの中身」の「旧 Task 4」。

### Task 5: S2 取り込みゲート

**Files:**
- Modify（merge）: moorestech worktree（S2 の `feat/tier9-compute-data-loop` を取り込む。moorestech_master は触らない）

**Interfaces:**
- Consumes（S2）: `Game.Orbit.IOrbitalArrivalService`（`IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)`、`IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)`（Arrive と同じ成果を返し、通知も記録もしない）、`bool IsOrbitalPayload(ItemId itemId)`、`IObservable<OrbitalArrival> OnOrbitalArrival { get; }`）、`Game.Orbit.OrbitalArrival`（`public readonly ItemId PayloadItemId; public readonly int PayloadCount;`）、items.yml の `orbitalPayloads`（`payloadItemGuid` / `countPerLaunch` / `arrivalOutputs[{itemGuid, count}]`）、`WorldSaveAllInfo.CurrentVersion == 7`
- Produces: S2 の `Game.Orbit`・積荷表のスキーマ（items.yml の `orbitalPayloads`）・テスト mod の積荷2行・S2 のピン（`.moorestech-external-revisions.json`）を取り込んだ moorestech ブランチ

- [x] **Step 1: ゲート — S2 の push を確かめる**

```bash
git fetch origin
git grep -l "PreviewArrivalOutputs" origin/feat/tier9-compute-data-loop -- moorestech_server/Assets/Scripts/Game.Orbit
git grep -l "orbitalPayloads" origin/feat/tier9-compute-data-loop -- VanillaSchema/items.yml
```
2つとも当たれば（`IOrbitalArrivalService` に `PreviewArrivalOutputs` があり、S2 の items.yml に積荷表がある）Step 2 へ。S2 が既に origin/master へマージ済みなら、以降の `origin/feat/tier9-compute-data-loop` を `origin/master` に読み替える。当たらなければ、前景で `sleep 600` → 上を再実行、を最大12回（2時間）繰り返す。それでも無ければ `bd note moorestech-xyuaf.1` に「S3: S2 の Game.Orbit 未 push で Task 5 以降を保留」と書き、Task 3 の成果を push した状態で止まって報告する（解決不能な障害）。

- [x] **Step 2: S2 を取り込む**

```bash
git merge --no-ff origin/feat/tier9-compute-data-loop -m "Merge S2 (feat/tier9-compute-data-loop) for Game.Orbit"
git show HEAD:.moorestech-external-revisions.json
git show origin/feat/tier9-compute-data-loop:.moorestech-external-revisions.json
```
S3 はマスタを変えないので、`.moorestech-external-revisions.json` は S2 側の値になる（2つの `git show` の `moorestech_master` の値が一致すること。衝突したら `git checkout --theirs .moorestech-external-revisions.json` で S2 側を取る）。他の衝突は両方の変更を残す。解いたら:
- `uloop compile --project-path ./moorestech_client`（ErrorCount 0）
- `grep -n "CurrentVersion" moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs` が `= 7` 以上（6 なら S2 の版上げが未取り込み → Step 1 へ戻る。8 以上なら S1 の版上げも先に入っているので、Task 7 の版は判断記録 P11 の繰り上げ規則で読み替える）
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Orbit"` が全 PASS
- Editor でマスタを読み直し（RepositorySync がピンへ checkout する）、`uloop get-logs --project-path ./moorestech_client --log-type Error` に `MooresmasterLoaderException`・`[ItemMaster]`・`[BlockMaster]` が無い。出たら S2 の範囲なので直さず、`bd note moorestech-xyuaf.1` に内容を書いて続ける（サーバーテストはテスト mod で動く）。PR 本文にも書く
- `git diff -- .moorestech-external-revisions.json` が空（Editor が書き戻していたら `git checkout -- .moorestech-external-revisions.json`）

- [x] **Step 3: push する**

```bash
git push
```

### Task 6: スキーマ `SpaceElevatorBase` とマスタ検証・テスト mod

**Files:**
- Modify: `VanillaSchema/blocks.yml`（`blockType` の `options` と case）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（`dummyText`）
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/SpaceElevatorBaseParamValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/{blocks,items,buildMenu}.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`、`ForUnitTestItemId.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/SpaceElevator/SpaceElevatorBaseMasterValidationTest.cs`
（出荷マスタの拠点を `SpaceElevatorBase` にするのは統合タスク。末尾の節「旧 Task 6 Step 5〜7 のマスタ部分」）

**Interfaces:**
- Consumes: Task 5 の S2 取り込み（items.yml の `orbitalPayloads` とテスト mod の `orbitalPayloads` 配列）
- Produces:
  - 生成型 `Mooresmaster.Model.BlocksModule.SpaceElevatorBaseBlockParam`（`Guid[] CarriedPayloadItemGuids`, `Guid CableItemGuid`, `int CablesPerElevator`, `float ClimbSeconds`, `float RequiredPower`, `int OutputSlotCount`, `InventoryConnects InventoryConnectors`, `int MaxWireConnectionCount`, `int ConnectionRange`, `int ConnectionHeightRange`）と `BlockMasterElement.BlockTypeConst.SpaceElevatorBase`
  - `ForUnitTestModBlockId.TestSpaceElevatorBase`、`ForUnitTestItemId.TestElevatorCable` / `TestElevatorSatellite` / `TestElevatorReward`
  - テスト mod の値: 1エレベーター分 100 本、昇降 2 秒、電力 100、出口スロット 1、ケーブルの1便 50 本、衛星1機の成果 TestElevatorReward ×5

- [x] **Step 1: スキーマに blockType と case を足す**

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
        - key: outputSlotCount
          type: integer
          default: 4
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

- [x] **Step 2: マスタ検証を書く**

`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/SpaceElevatorBaseParamValidator.cs`:
```csharp
using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    // 宇宙エレベーター拠点の参照と数値を検証する。軌道到着の表との突き合わせは起動時の検査サービスが行う
    // Validates the space elevator base's references and numbers; the orbital-table cross-check runs in the boot-time check service
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
            if (param.OutputSlotCount < 1) logs += $"[BlockMaster] Name:{name} outputSlotCount must be >= 1. value:{param.OutputSlotCount}\n";
            return logs;
        }
    }
}
```
`BlockMasterUtil.Validate` の `errorLogs += BeltSpeedPerSecondValidator.Validate(blocks);` の次に `errorLogs += SpaceElevatorBaseParamValidator.Validate(blocks);` を足す。生成型の数値型が `float` でなく `double` なら `float.IsInfinity` を `double.IsInfinity` に、`1f`/`0f` を `1d`/`0d` に読み替える（生成コード `Mooresmaster.Model.BlocksModule.SpaceElevatorBaseBlockParam` を開いて確かめる）。

- [x] **Step 3: テスト mod に拠点・アイテム・積荷行を足す**

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
    "outputSlotCount": 1,
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

- [x] **Step 4: マスタ検証のテストを書く**

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
        [TestCase("outputSlotCount", 0, "outputSlotCount must be >= 1")]
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

- [x] **Step 5: 生成・コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client`（ErrorCount 0。生成型 `SpaceElevatorBaseBlockParam` が見えること）→ `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpaceElevatorBaseMasterValidationTest|GearToElectricMasterValidationTest|Orbit"`
Expected: 全 PASS。Editor でマスタを読み直し、`uloop get-logs --project-path ./moorestech_client --log-type Error` に `MooresmasterLoaderException`・`[BlockMaster]` が増えていない（新しい case だけなので、ピン先の出荷マスタはそのまま読める）。

- [x] **Step 6: コミットする**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module/TestMod moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/SpaceElevator
git commit -m "feat(schema): 宇宙エレベーター拠点のブロック種別とマスタ検証を足す"
```
（Unity が生成した新ファイルの .meta も `git status` で確かめて一緒に add する）

### Task 7: ケーブルプールとワールドセーブ（版 7→8）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator/ISpaceElevatorBase.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/Game.SpaceElevator.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorCablePool.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorConnectionService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator/SpaceElevatorConnectResult.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorBaseMasters.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorOrbitalTableCheck.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SpaceElevator/Save/SpaceElevatorCablePoolSaveData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`、`moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`、`moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef`（`Game.SpaceElevator` を参照に足す）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveDataRestorer.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveRequiredFieldGuard.cs`（S2 が版 6→7 で作る。Task 5 で取り込み済み）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV7ToV8.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`（移行チェーン3箇所）
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SpaceElevatorServiceRegistration.cs`（`GameplayServiceRegistration.cs` は202行で上限を超えているので足さない。前例 S2 の `OrbitServiceRegistration.cs`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV7ToV8Test.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorCablePoolTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorOrbitalTableCheckTest.cs`

**Interfaces:**
- Consumes: S2 `IOrbitalArrivalService`（`Arrive`, `PreviewArrivalOutputs`, `IsOrbitalPayload`, `OnOrbitalArrival`）、`OrbitalArrival.PayloadItemId` / `PayloadCount`、Task 6 の `SpaceElevatorBaseBlockParam`
- Produces:
  - `ISpaceElevatorBase : IBlockComponent`（`bool IsConnected { get; }`, `void MarkConnected()`, `void SetCablePoolAvailability(int poolAmount, bool hasEnoughCable)`）
  - `SpaceElevatorCablePool : IBootInitializable`（`int Amount { get; private set; }`, `IObservable<int> OnAmountChanged`, `internal bool HasEnough(int count)`, `internal bool TryTake(int count)`, `internal void Add(int count)`, `SpaceElevatorCablePoolSaveData GetSaveData()`, `void Restore(SpaceElevatorCablePoolSaveData saveData)`）
  - `SpaceElevatorConnectionService : IBootInitializable`（`SpaceElevatorConnectResult Connect(IBlock block)`）
  - `SpaceElevatorOrbitalTableCheck : IBootInitializable`（起動時に拠点のマスタを軌道到着の表と突き合わせ、合わなければ例外で起動を止める）
  - `enum SpaceElevatorConnectResult { Connected, AlreadyConnected, InsufficientCable, NotSpaceElevatorBase }`（`Game.Block.Interface.Component`）
  - `SpaceElevatorCablePoolSaveData`（`[JsonProperty("cableCount")] public int CableCount;`）、セーブの節名 `spaceElevatorCablePool`、`WorldSaveAllInfo.CurrentVersion == 8`、`SaveMigrationStepV7ToV8`

設計の要点（ADR 0089「呼び出し側の線引き」）: 拠点はプールの残量の判定も引き算も知らない。接続できるかの判定（残量 ≥ 1エレベーター分）はプールの `HasEnough` だけが持ち、`TryTake` も画面向けの「接続できるか」の配信も同じ `HasEnough` を通す。引き算・撤去の返却は `SpaceElevatorConnectionService` が持ち、プール本体の `HasEnough`/`TryTake`/`Add` は同アセンブリ内だけに閉じる（`internal`）。拠点は配信された「残量」と「接続できるか」を状態に載せるだけで、画面（webui）は判定しない（ユーザー裁定 [[2026-10-23-エレベーター拠点に接続できるかはサーバーのプールが判定し画面は表示するだけ]]）。

- [x] **Step 1: 拠点の面とプール・接続サービスを書く**

`Game.Block.Interface/Component/SpaceElevator/ISpaceElevatorBase.cs`:
```csharp
namespace Game.Block.Interface.Component
{
    // 接続サービスが拠点に触る面。接続済みかの読み書きと、プール残量・接続できるか（プールが判定）の受け取りだけを持つ
    // The surface the connection service touches on a base: the connected flag and receiving the pool amount and whether it can connect (judged by the pool)
    public interface ISpaceElevatorBase : IBlockComponent
    {
        bool IsConnected { get; }
        void MarkConnected();
        void SetCablePoolAvailability(int poolAmount, bool hasEnoughCable);
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

`Game.Block.Interface/Component/SpaceElevator/SpaceElevatorConnectResult.cs`（通信の契約型なので実装の `Game.SpaceElevator` ではなく契約の層に置く。クライアントは `Game.SpaceElevator` を参照しない。前例 S2 の `RocketLaunchPadCommandResult`）:
```csharp
namespace Game.Block.Interface.Component
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
            // 拠点のマスタが数えるケーブルを集める。表との突き合わせは SpaceElevatorOrbitalTableCheck が起動時に行う
            // Collect the cable items base masters count; SpaceElevatorOrbitalTableCheck cross-checks them with the table at boot
            _cableItemIds.UnionWith(SpaceElevatorBaseMasters.CableItemIds());

            // ケーブルの到着だけを数える。どの打ち上げ口から来たかは見ない
            // Count only cable arrivals, regardless of which launch site sent them
            _orbitalArrivalService.OnOrbitalArrival.Subscribe(arrival =>
            {
                if (!_cableItemIds.Contains(arrival.PayloadItemId)) return;
                Add(arrival.PayloadCount);
            });
        }

        // 1エレベーター分が残っているか。接続できるかの判定はここだけが持つ
        // Whether one elevator's worth remains; this is the only place that judges whether a base can connect
        internal bool HasEnough(int count)
        {
            return Amount >= count;
        }

        // 足りれば引いてtrue。足りなければ何もしない（接続サービスだけが使う）
        // Take the count and return true when enough remains; otherwise change nothing (only the connection service uses this)
        internal bool TryTake(int count)
        {
            if (!HasEnough(count)) return false;
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

`Game.SpaceElevator/SpaceElevatorOrbitalTableCheck.cs`（成果が出口に入りきるかを先に確かめる方式は S2 の裁定。拠点は昇り始める前と渡す直前に確かめるので、衛星1機の成果が空の出口にも入らないマスタでは昇降機が永遠に待つ。その形を起動時に止める）:
```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.SpaceElevator
{
    /// <summary>
    ///     起動時に拠点のマスタを軌道到着の表と突き合わせ、合わなければ起動を止める（ADR 0089）
    ///     At boot, checks base masters against the orbital arrival table and stops the boot on a mismatch (ADR 0089)
    /// </summary>
    public class SpaceElevatorOrbitalTableCheck : IBootInitializable
    {
        private readonly IOrbitalArrivalService _orbitalArrivalService;

        public SpaceElevatorOrbitalTableCheck(IOrbitalArrivalService orbitalArrivalService)
        {
            _orbitalArrivalService = orbitalArrivalService;
        }

        public void Load()
        {
            foreach (var (blockName, param) in SpaceElevatorBaseMasters.All())
            {
                ThrowIfNotOrbitalPayload(blockName, param.CableItemGuid);
                foreach (var payloadGuid in param.CarriedPayloadItemGuids)
                {
                    ThrowIfNotOrbitalPayload(blockName, payloadGuid);
                    ThrowIfResultNeverFits(blockName, payloadGuid, param.OutputSlotCount);
                }
            }

            #region Internal

            void ThrowIfNotOrbitalPayload(string blockName, Guid itemGuid)
            {
                if (_orbitalArrivalService.IsOrbitalPayload(MasterHolder.ItemMaster.GetItemId(itemGuid))) return;
                Fail($"宇宙エレベーター拠点 {blockName} のアイテム {itemGuid} が軌道到着の表（items の orbitalPayloads）にありません");
            }

            // 衛星1機の成果が空の出口にも入らないと、昇り切っても永遠に渡せない
            // If one satellite's result cannot fit even into empty outlets, the climber could never hand it over
            void ThrowIfResultNeverFits(string blockName, Guid payloadGuid, int outputSlotCount)
            {
                var outputs = _orbitalArrivalService.PreviewArrivalOutputs(MasterHolder.ItemMaster.GetItemId(payloadGuid), 1);
                var neededSlots = CountSlotsAtStackLevelOne(outputs);
                if (neededSlots <= outputSlotCount) return;
                Fail($"宇宙エレベーター拠点 {blockName} の積荷 {payloadGuid} 1機の成果は出口 {neededSlots} スロットぶんあり、outputSlotCount {outputSlotCount} に入りません");
            }

            void Fail(string reason)
            {
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }

            #endregion
        }

        // 成果をアイテムごとに合算し、スタック上限が最も低いレベル1で何スロット要るかを数える
        // Sum the results per item and count the slots needed at stack level 1, the lowest stack limit
        // スタック上限は研究で上がるだけなので、いちばん小さいレベル1で入れば実行時の今の上限でも必ず入る
        // Stack limits only rise with research, so fitting at level one guarantees a fit at any runtime level
        private static int CountSlotsAtStackLevelOne(IReadOnlyList<IItemStack> outputs)
        {
            var totals = new Dictionary<ItemId, int>();
            foreach (var output in outputs) totals[output.Id] = totals.GetValueOrDefault(output.Id) + output.Count;

            var slots = 0;
            foreach (var pair in totals)
            {
                var element = MasterHolder.ItemMaster.GetItemMaster(pair.Key);
                var stackAtLevelOne = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid).StackCounts[0];
                slots += (pair.Value + stackAtLevelOne - 1) / stackAtLevelOne;
            }
            return slots;
        }
    }
}
```
衛星の `countPerLaunch` が 1 でないと、昇降機の `Arrive(衛星, 1)` が実行時に例外になる。S2 の `PreviewArrivalOutputs` が `Arrive` と同じく「countPerLaunch の正の整数倍でなければ例外」なら、上の `PreviewArrivalOutputs(..., 1)` がその形を起動時に止める。S2 の実装を読み、Preview がこの検査をしないなら、S2 の積荷表の生成型から countPerLaunch を引いて 1 でなければ `Fail` する行を `ThrowIfResultNeverFits` の先頭に足す。

`Game.SpaceElevator/SpaceElevatorBaseMasters.cs`（拠点マスタの列挙とケーブル集合の導出を1か所にする。プールと起動時の検査が使う。`Core.Master` の `SpaceElevatorBaseParamValidator` は `Game.*` を参照できないので、ロード時の検証は自前で列挙する）:
```csharp
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.SpaceElevator
{
    // マスタにある宇宙エレベーター拠点の列挙と、拠点が数えるケーブルの導出
    // Enumerates the space elevator bases in the master and derives the cable items they count
    internal static class SpaceElevatorBaseMasters
    {
        internal static IEnumerable<(string BlockName, SpaceElevatorBaseBlockParam Param)> All()
        {
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (block.BlockParam is SpaceElevatorBaseBlockParam param) yield return (block.Name, param);
            }
        }

        internal static HashSet<ItemId> CableItemIds()
        {
            var ids = new HashSet<ItemId>();
            foreach (var (_, param) in All()) ids.Add(MasterHolder.ItemMaster.GetItemId(param.CableItemGuid));
            return ids;
        }
    }
}
```
`MasterHolder.BlockMaster.Blocks.Data` の実名は `Core.Master/BlockMaster.cs` で確かめる（全ブロックを列挙するプロパティ名に合わせる）。

`Game.SpaceElevator/SpaceElevatorConnectionService.cs`:
```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.SpaceElevator
{
    /// <summary>
    ///     拠点の接続・撤去時の返却・残量と接続できるかの配信を受け持つ。拠点はプールの残量の判定も1エレベーター分の引き算も知らない
    ///     Owns connecting, the refund on removal and pushing the pool amount and connectability; bases know neither the pool check nor the per-elevator subtraction
    /// </summary>
    public class SpaceElevatorConnectionService : IBootInitializable
    {
        private readonly SpaceElevatorCablePool _pool;
        private readonly Dictionary<BlockInstanceId, TrackedSpaceElevatorBase> _bases = new();

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
            _pool.OnAmountChanged.Subscribe(_ => PushAvailabilityToAll());
        }

        public SpaceElevatorConnectResult Connect(IBlock block)
        {
            if (!_bases.TryGetValue(block.BlockInstanceId, out var tracked)) return SpaceElevatorConnectResult.NotSpaceElevatorBase;
            if (tracked.Base.IsConnected) return SpaceElevatorConnectResult.AlreadyConnected;

            // 1エレベーター分が残っていれば引いて接続する
            // Connect after taking one elevator's worth when it remains
            if (!_pool.TryTake(tracked.CablesPerElevator)) return SpaceElevatorConnectResult.InsufficientCable;
            tracked.Base.MarkConnected();
            return SpaceElevatorConnectResult.Connected;
        }

        private void OnBlockPlaced(IBlock block)
        {
            if (!block.ComponentManager.TryGetComponent<ISpaceElevatorBase>(out var elevatorBase)) return;
            var cablesPerElevator = ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).CablesPerElevator;
            var tracked = new TrackedSpaceElevatorBase(elevatorBase, cablesPerElevator);
            _bases[block.BlockInstanceId] = tracked;
            PushAvailability(tracked);
        }

        private void OnBlockRemoved(IBlock block)
        {
            if (!_bases.Remove(block.BlockInstanceId, out var tracked)) return;

            // 接続済みの拠点だけが1エレベーター分をプールへ戻す。撤去イベントはDestroyより前に来るので状態を読める
            // Only a connected base returns one elevator's worth; the remove event fires before Destroy, so its state is still readable
            if (tracked.Base.IsConnected) _pool.Add(tracked.CablesPerElevator);
        }

        private void PushAvailabilityToAll()
        {
            foreach (var tracked in _bases.Values) PushAvailability(tracked);
        }

        // 接続できるかはプールの HasEnough で決め、拠点へは結果だけを渡す
        // Connectability is decided by the pool's HasEnough; the base only receives the result
        private void PushAvailability(TrackedSpaceElevatorBase tracked)
        {
            tracked.Base.SetCablePoolAvailability(_pool.Amount, _pool.HasEnough(tracked.CablesPerElevator));
        }

        // 追跡中の拠点と、その拠点の1エレベーター分の量
        // A tracked base and its cables per elevator
        private class TrackedSpaceElevatorBase
        {
            public readonly ISpaceElevatorBase Base;
            public readonly int CablesPerElevator;

            public TrackedSpaceElevatorBase(ISpaceElevatorBase elevatorBase, int cablesPerElevator)
            {
                Base = elevatorBase;
                CablesPerElevator = cablesPerElevator;
            }
        }
    }
}
```

`Server.Boot/Composition/SpaceElevatorServiceRegistration.cs`（前例 S2 の `OrbitServiceRegistration.cs`。`GameplayServiceRegistration.cs` は202行で上限を超えているので足さない）:
```csharp
using Game.SpaceElevator;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.Composition
{
    // 宇宙エレベーターのプール・接続・起動時の検査を登録する。起動時に購読を張るので具象型で登録する
    // Registers the space elevator pool, connections and boot-time check by concrete type so their boot-time subscriptions run
    internal static class SpaceElevatorServiceRegistration
    {
        internal static void Register(IServiceCollection services)
        {
            services.AddSingleton<SpaceElevatorOrbitalTableCheck>();
            services.AddSingleton<SpaceElevatorCablePool>();
            services.AddSingleton<SpaceElevatorConnectionService>();
        }
    }
}
```
`MoorestechServerDIContainerGenerator.cs` の S2 の `OrbitServiceRegistration.Register(services);` の次の行（`SaveAndEventServiceRegistration.Register(...)` より前。その末尾の `AddInitializableForwarding()` が `IBootInitializable` へ転送するため）に `SpaceElevatorServiceRegistration.Register(services);` を足す。S2 の行が無ければ `GameplayServiceRegistration.Register(...)` の次に足す。
`Game.SaveLoad.asmdef`・`Server.Boot.asmdef`・`Server.Tests.asmdef` の `references` に `"Game.SpaceElevator"` を足す（`Game.Orbit` が無ければそれも）。

- [x] **Step 2: ワールドセーブに節を足し版を 8 にする**

`WorldSaveAllInfo.cs`: `CurrentVersion` を `8` にする（Task 5 で見た版が 7 でなく N だったら、この Task の 7→8 をすべて N→N+1 と読み替える。判断記録 P11）。ctor の最後の引数（S2 が足した引数の後ろ）に `SpaceElevatorCablePoolSaveData spaceElevatorCablePool` を足し、本体で `SpaceElevatorCablePool = spaceElevatorCablePool;`、プロパティ `[JsonProperty("spaceElevatorCablePool")] public SpaceElevatorCablePoolSaveData SpaceElevatorCablePool { get; }` を足す（`using Game.SpaceElevator.Save;`。欠損は null のまま残し、復元側で止める）。
`AssembleSaveJsonText.cs`: ctor に `SpaceElevatorCablePool spaceElevatorCablePool` を足して `_spaceElevatorCablePool` に保持し、`Capture()` の `new WorldSaveAllInfo(...)` の最後の引数に `_spaceElevatorCablePool.GetSaveData()` を渡す。
`WorldSaveDataRestorer.cs`: ctor に `SpaceElevatorCablePool spaceElevatorCablePool` を足す。`Restore` の `_worldBlockDatastore.LoadBlockDataList(load.World);` の直前に:
```csharp
            // ケーブルプールはブロックより先に戻し、ロードで置かれる拠点が設置時に残量を受け取れるようにする
            // Restore the cable pool before blocks so bases placed by the load receive the amount when placed
            _spaceElevatorCablePool.Restore(load.SpaceElevatorCablePool);
```
S2 の `WorldSaveRequiredFieldGuard.ThrowIfMissing(load)`（`Game.SaveLoad/Json/WorldSaveRequiredFieldGuard.cs`）の欠損の一覧に `if (load.SpaceElevatorCablePool == null) missing.Add("spaceElevatorCablePool");` を足す（S2 の書き方に合わせる。`WorldSaveDataRestorer.cs` が200行を超えないよう、必須欠損の検査はこのファイルに集める）。
`MissingMasterPruner.cs` の `SectionsWithoutMasterPruning` の「マスタguidを持たない」行に `"spaceElevatorCablePool"` を足す（残量の数だけでマスタ guid を持たない）。

- [x] **Step 3: 移行ステップを書いて3箇所に登録する**

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
            // 既にあれば形だけ確かめて素通しする（冪等）。値の範囲（負でないか）はロード時の Restore だけが見る
            // When present, only check the shape and pass it through (idempotent); only Restore at load checks the value range
            var existing = save[PoolKey];
            if (existing != null)
            {
                if (existing is JObject pool && pool[CountKey]?.Type == JTokenType.Integer) return SaveMigrationStepResult.Converted(save);
                return Fail($"{PoolKey} が整数の {CountKey} を持つオブジェクトではありません。 value={existing}");
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

- [x] **Step 4: テストを書く**

`Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV7ToV8Test.cs`:
```csharp
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
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
using UniRx;

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
        public void 成果の問い合わせでは到着の通知が流れずプールも増えない()
        {
            var provider = CreateServer();
            var orbital = provider.GetService<IOrbitalArrivalService>();
            var pool = provider.GetService<SpaceElevatorCablePool>();
            var notified = 0;
            orbital.OnOrbitalArrival.Subscribe(_ => notified++);

            // 昇降機は出口待ちの間 毎tick問い合わせるので、問い合わせに副作用が無いことを確かめる
            // The climber asks every tick while the outlet is full, so the query must have no side effect
            orbital.PreviewArrivalOutputs(ForUnitTestItemId.TestElevatorSatellite, 1);
            orbital.PreviewArrivalOutputs(ForUnitTestItemId.TestElevatorCable, 50);

            Assert.AreEqual(0, notified);
            Assert.AreEqual(0, pool.Amount);
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

`Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorOrbitalTableCheckTest.cs`（テスト mod の拠点は出口1スロット。偽の到着サービスで表の中身だけを差し替える）:
```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorOrbitalTableCheckTest
    {
        [Test]
        public void 衛星1機の成果が空の出口に入らなければ起動時に止まる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // レベル1のスタック上限より1つ多い成果は2スロット要り、出口1スロットに入らない
            // One more than the level-1 stack limit needs two slots and cannot fit the single outlet
            var element = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.TestElevatorReward);
            var stackAtLevelOne = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid).StackCounts[0];
            var oversized = new List<IItemStack>
            {
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, stackAtLevelOne),
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 1),
            };

            LogAssert.Expect(LogType.Error, new Regex("outputSlotCount 1 に入りません"));
            Assert.Throws<InvalidOperationException>(() => new SpaceElevatorOrbitalTableCheck(new FixedOrbitalArrivalService(true, oversized)).Load());
        }

        [Test]
        public void 積荷が軌道到着の表に無ければ起動時に止まる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            LogAssert.Expect(LogType.Error, new Regex("軌道到着の表"));
            Assert.Throws<InvalidOperationException>(() => new SpaceElevatorOrbitalTableCheck(new FixedOrbitalArrivalService(false, new List<IItemStack>())).Load());
        }

        // 表の判定と成果の問い合わせだけを固定で返す偽の到着サービス
        // A fake arrival service returning fixed answers for the table check and the result preview
        private class FixedOrbitalArrivalService : IOrbitalArrivalService
        {
            private readonly bool _isOrbitalPayload;
            private readonly IReadOnlyList<IItemStack> _preview;

            public FixedOrbitalArrivalService(bool isOrbitalPayload, IReadOnlyList<IItemStack> preview)
            {
                _isOrbitalPayload = isOrbitalPayload;
                _preview = preview;
            }

            public IObservable<OrbitalArrival> OnOrbitalArrival => Observable.Empty<OrbitalArrival>();
            public bool IsOrbitalPayload(ItemId itemId) { return _isOrbitalPayload; }
            public IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount) { return _preview; }
            public IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount) { throw new NotSupportedException("検査は到着させない"); }
        }
    }
}
```
（S2 の `IOrbitalArrivalService` に上の4つ以外のメンバーがあれば、偽物にも同じ形で足す。テスト mod の正しいマスタが起動時の検査を通ることは、`CreateServer` を使う全テストが通ることで確かめられる）

- [x] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpaceElevatorCablePoolTest|SpaceElevatorOrbitalTableCheckTest|SaveMigrationStepV7ToV8Test|SaveMigrationChainTest|MissingMasterSectionCoverageTest|SaveLoad"`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 6: 実ロード検証**

moorestech-save-migration スキルの `references/load_test.cs` の手順で、S2 の版7セーブ（S2 の移行テストの入力か、S2 の plan が置いたサンプル）を版8へ読み込めることを `uloop execute-dynamic-code` で確かめ、出力（変換ログの行）を判断記録に貼る。

- [x] **Step 7: コミットする**

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
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/SpaceElevator/SpaceElevatorBaseStateDetail.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/SpaceElevator/SpaceElevatorBaseStatus.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorClimberSaveRestorer.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorOutputSimulation.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorSatelliteInletState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorBaseStatusResolver.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/SpaceElevator/VanillaSpaceElevatorBaseTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef`（`Game.Orbit` が無ければ足す）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`（S2 が直していなければ）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorConnectionTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorClimbTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorBaseSaveAndRefundTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorTestUtil.cs`

**Interfaces:**
- Consumes: Task 7 の `ISpaceElevatorBase`・`SpaceElevatorConnectionService.Connect(IBlock)`、S2 の `IOrbitalArrivalService.Arrive`・`PreviewArrivalOutputs`、Task 6 の `SpaceElevatorBaseBlockParam.OutputSlotCount`
- Produces:
  - `SpaceElevatorBaseStateDetail`（MessagePack、キー `"SpaceElevatorBase"`。`[Key(0)] bool IsConnected`、`[Key(1)] int CablePoolAmount`、`[Key(2)] float ClimbProgress`、`[Key(3)] SpaceElevatorBaseStatus Status`。1エレベーター分の量はマスタ値なので載せず、クライアントの DTO ビルダーが param から引く）
  - `enum SpaceElevatorBaseStatus { ReadyToConnect, InsufficientCable, WaitingSatellite, Climbing, LowPower, OutputBlocked }`（未接続は、プールが判定した「接続できるか」で `ReadyToConnect`／`InsufficientCable` に分かれる）
  - インベントリのスロット: 0＝衛星の入口（ベルト・プレイヤーから入るのは運ぶ積荷だけ。マスタを変えた後のロードでだけ運べない衛星が残り得る）、1〜`OutputSlotCount`＝成果の出口（取り出しのみ）。`SpaceElevatorBaseInventoryComponent.SatelliteSlot = 0`、`FirstOutputSlot = 1`、`internal bool IsCarriedPayload(ItemId)`、`internal SatelliteInletState PeekSatellite(out ItemId)`（`Empty` / `Carriable` / `NotCarried`）、`internal void ReturnSatelliteWithoutEvent(IItemStack)`
  - 入口に運べないアイテムがあれば昇降機は運ばず、その状態に入った tick に1回だけ警告ログを出す（プレイヤーは拠点 UI の入口から取り出せる）
  - 出口待ちに入った tick に1回だけ理由をログへ出す（Global Constraints「fail-closed はログ必須」。UI の `OutputBlocked` 表示と併せる）
  - ロード時のガード（`SpaceElevatorClimberSaveRestorer`）: 昇降中の衛星が運ぶ積荷でなければエラーログを出して昇降を取り消し入口へ戻す、衛星が2機以上ならエラーログを出して1機だけ昇らせ余りを入口へ戻す、残りが負・NaN・無限ならエラーログを出して最初から昇り直す、衛星が無いのに残りがあればエラーログを出して昇降していない状態で戻す、残りが昇降の総時間を超えていれば警告ログを出して総時間に収める。インベントリはセーブのスロットがマスタより多ければエラーログを出して超えた分を捨てる（前例 `VanillaChestComponent.cs:49-52`）
  - 出口の確認は機械の前例（`Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs:49` の開始前、`ProcessingMachineProcessState.cs:118` の完了直前に `CanStoreOutputs`）と同じ2点: 昇り始める前と、頂上で軌道到着サービスへ渡す直前。どちらも `PreviewArrivalOutputs(衛星, 1)` が出口へ入りきるかを見る。入らなければ衛星を入口に残す／頂上で待たせ、状態を `OutputBlocked` にする。頂上で待つ間は電力を使わない
  - `RemoveBlockProtocol` は `IGetRefundItemsInfo` を全コンポーネントから合算する

- [ ] **Step 1: 撤去返却の合算を確かめて直す**

`grep -n "IGetRefundItemsInfo" moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`。`TryGetComponent(out IGetRefundItemsInfo refundInfo)` のままなら（S2 未修正）、次へ置き換える（拠点はワイヤー端点と昇降機の2つが `IGetRefundItemsInfo` を持つため、先頭1件だけでは昇降中の衛星が返らない）:
```csharp
                // 返却すべきアイテム情報を全コンポーネントから集める（ワイヤー端点と本体の両方が持つブロックがある）
                // Gather refundable items from every component; some blocks carry both a wire connector and a body that refund
                foreach (var refundInfo in block.ComponentManager.GetComponents<IGetRefundItemsInfo>())
                {
                    result.AddRange(refundInfo.GetRefundItems());
                }
```
（`GetComponents<T>` の実名は `Game.Block.Interface/BlockComponentManager.cs` で確かめる。S2 が既に直していれば何もしない。`RemoveBlockProtocol.cs` は今ちょうど200行なので、置き換える元の行数と同じ行数に収め（コメント2行は元のコメントと差し替える）、`wc -l` が200以下であることを確かめる）

- [ ] **Step 2: 状態・セーブの型を書く**

`Game.Block.Interface/State/SpaceElevator/SpaceElevatorBaseStatus.cs`（状態詳細と列挙は通信の契約型なので契約の層に置く。前例 S2 の `State/RocketLaunchPad/`）:
```csharp
namespace Game.Block.Interface.State
{
    // 拠点の今の状態。待ちの理由もここで表す。未接続は接続できるか（プールが判定）で2つに分かれる
    // The base's current state, including why it is waiting; unconnected splits by whether it can connect (judged by the pool)
    public enum SpaceElevatorBaseStatus
    {
        ReadyToConnect,
        InsufficientCable,
        WaitingSatellite,
        Climbing,
        LowPower,
        OutputBlocked,
    }
}
```
`Game.Block.Interface/State/SpaceElevator/SpaceElevatorBaseStateDetail.cs`（置き場の前例 `Game.Block.Interface/State/CommonMachineBlockStateDetail.cs`、形の前例 `ElectricToGearGeneratorBlockStateDetail.cs`）:
```csharp
using System;
using MessagePack;

namespace Game.Block.Interface.State
{
    [Serializable]
    [MessagePackObject]
    public class SpaceElevatorBaseStateDetail
    {
        public const string BlockStateDetailKey = "SpaceElevatorBase";

        [Key(0)] public bool IsConnected { get; set; }
        [Key(1)] public int CablePoolAmount { get; set; }
        // 昇降の進み（0〜1）。昇っていなければ0
        // Climb progress from 0 to 1; zero while not climbing
        [Key(2)] public float ClimbProgress { get; set; }
        [Key(3)] public SpaceElevatorBaseStatus Status { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SpaceElevatorBaseStateDetail()
        {
        }

        public SpaceElevatorBaseStateDetail(bool isConnected, int cablePoolAmount, float climbProgress, SpaceElevatorBaseStatus status)
        {
            IsConnected = isConnected;
            CablePoolAmount = cablePoolAmount;
            ClimbProgress = climbProgress;
            Status = status;
        }
    }
}
```
`SpaceElevatorClimberSaveJsonObject.cs`:
```csharp
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
        [JsonProperty("remainingClimbSeconds")] public double RemainingClimbSeconds;
    }
}
```

- [ ] **Step 3: インベントリを書く**

`SpaceElevatorBaseInventoryComponent.cs`（スロットの扱いの前例 `Game.Block/Blocks/Chest/VanillaChestComponent.cs`、出口の模擬の前例 `Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs:73-125`）:
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
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点のスロット。0は運ぶ衛星の入口、1以降は成果の出口。出口だけを隣のベルトへ押し出す
    // The base's slots: slot 0 takes satellites to carry, the rest hold results; only the outlets are pushed to adjacent belts
    public class SpaceElevatorBaseInventoryComponent : IOpenableBlockInventoryComponent, IBlockSaveState, IUpdatableBlockComponent
    {
        public const int SatelliteSlot = 0;
        public const int FirstOutputSlot = 1;

        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        public BlockInstanceId BlockInstanceId { get; }
        public string SaveKey { get; } = typeof(SpaceElevatorBaseInventoryComponent).FullName;

        private readonly int _slotCount;
        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly HashSet<ItemId> _carriedPayloadIds = new();

        public SpaceElevatorBaseInventoryComponent(BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryInserter = blockInventoryInserter;
            _slotCount = FirstOutputSlot + param.OutputSlotCount;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, _slotCount);
            foreach (var payloadGuid in param.CarriedPayloadItemGuids) _carriedPayloadIds.Add(MasterHolder.ItemMaster.GetItemId(payloadGuid));
        }

        public SpaceElevatorBaseInventoryComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter) :
            this(blockInstanceId, param, blockInventoryInserter)
        {
            // ロード時はまだワールドに登録されていないのでイベントを出さない
            // Loading does not fire events because the block is not registered in the world yet
            var itemJsons = BlockComponentStateReader.Read<List<ItemStackSaveJsonObject>>(componentStates, SaveKey);
            for (var i = 0; i < _slotCount && i < itemJsons.Count; i++) _itemDataStoreService.SetItemWithoutEvent(i, itemJsons[i].ToItemStack());

            // マスタで出口を減らすと超えた分は捨てる。黙って捨てずログに残す
            // Reducing the outlets in the master drops the overflow; log it instead of dropping silently
            if (_slotCount < itemJsons.Count) Debug.LogError($"[SpaceElevatorBase] セーブのスロット数 {itemJsons.Count} がマスタの {_slotCount} を超えるため、超えた分を捨てます BlockInstanceId:{blockInstanceId}");
        }

        internal bool IsCarriedPayload(ItemId itemId) { return _carriedPayloadIds.Contains(itemId); }

        // 入口の中身を見る。運べないアイテムはマスタを変えた後のロードでだけ残る
        // Peek at the inlet; a non-carried item only remains after loading with a changed master
        internal SatelliteInletState PeekSatellite(out ItemId satelliteId)
        {
            CheckDestroy(this);
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            satelliteId = stack.Id;
            if (stack.Count == 0) return SatelliteInletState.Empty;
            return IsCarriedPayload(stack.Id) ? SatelliteInletState.Carriable : SatelliteInletState.NotCarried;
        }

        // ロードで取り消した昇降中の衛星を入口へ戻す。入らない分はエラーログを出して失う
        // Return a satellite whose climb was cancelled on load to the inlet; anything that does not fit is logged and lost
        internal void ReturnSatelliteWithoutEvent(IItemStack satellite)
        {
            var result = _itemDataStoreService.GetItem(SatelliteSlot).AddItem(satellite);
            _itemDataStoreService.SetItemWithoutEvent(SatelliteSlot, result.ProcessResultItemStack);
            if (result.RemainderItemStack.Count > 0) Debug.LogError($"[SpaceElevatorBase] 入口が埋まっていて衛星を戻せず失いました item:{satellite.Id} count:{result.RemainderItemStack.Count}");
        }

        // 入口から衛星を1つ取り出す。同じtickのPeekSatelliteがCarriableのときだけ呼ぶ
        // Take one satellite from the inlet; call only after PeekSatellite returned Carriable in the same tick
        public IItemStack TakeOneSatellite()
        {
            CheckDestroy(this);
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            _itemDataStoreService.SetItem(SatelliteSlot, stack.SubItem(1));
            return ServerContext.ItemStackFactory.Create(stack.Id, 1);
        }

        // 成果が出口へ入りきるかを、出口の複製に積んで確かめる
        // Check whether the results fit by stacking them onto a copy of the outlets
        public bool CanStoreOutputs(IReadOnlyList<IItemStack> outputs)
        {
            CheckDestroy(this);
            return SpaceElevatorOutputSimulation.TryStack(CopyOutlets(), outputs, out _);
        }

        // 成果を出口へ入れる。確認と同じ模擬で積むので、確認を通った成果は必ず入る
        // Insert results into the outlets; the same simulation as the check is used, so checked results always fit
        public void InsertOutputs(IReadOnlyList<IItemStack> outputs)
        {
            CheckDestroy(this);
            if (!SpaceElevatorOutputSimulation.TryStack(CopyOutlets(), outputs, out var simulated))
            {
                Debug.LogError("[SpaceElevatorBase] 渡す直前の出口の確認を通ったのに出口へ入らず、成果が失われた");
                return;
            }

            for (var i = 0; i < simulated.Count; i++)
            {
                var current = _itemDataStoreService.GetItem(FirstOutputSlot + i);
                if (simulated[i].Id == current.Id && simulated[i].Count == current.Count) continue;
                _itemDataStoreService.SetItem(FirstOutputSlot + i, simulated[i]);
            }
        }

        public void Update()
        {
            CheckDestroy(this);
            for (var slot = FirstOutputSlot; slot < _slotCount; slot++)
            {
                var output = _itemDataStoreService.GetItem(slot);
                if (output.Count == 0) continue;
                _itemDataStoreService.SetItem(slot, _blockInventoryInserter.InsertItem(output));
            }
        }

        // ベルト・プレイヤーからは運ぶ積荷だけを入口へ受ける
        // Belts and players may only put carried payloads into the inlet
        public IItemStack InsertItem(IItemStack itemStack)
        {
            CheckDestroy(this);
            // silent-ok: 積荷以外はベルト上に残す通常の受け入れ拒否 / プレイヤーはベルトの詰まりと拠点UIの入口で気づく
            if (!IsCarriedPayload(itemStack.Id)) return itemStack;
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
                if (!IsCarriedPayload(itemStack.Id)) return false;
                var result = slot.AddItem(itemStack);
                if (result.RemainderItemStack.Count > 0) return false;
                slot = result.ProcessResultItemStack;
            }
            return true;
        }

        // 入口は積荷だけ、出口は取り出し（空にする）だけを許す
        // The inlet accepts only payloads and the outlets only taking out (emptying)
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            if (itemStack.Count == 0) return true;
            return slot == SatelliteSlot && IsCarriedPayload(itemStack.Id);
        }

        public object GetSaveState()
        {
            CheckDestroy(this);
            var itemJson = new List<ItemStackSaveJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems) itemJson.Add(new ItemStackSaveJsonObject(item));
            return itemJson;
        }

        private List<IItemStack> CopyOutlets()
        {
            var outlets = new List<IItemStack>();
            for (var slot = FirstOutputSlot; slot < _slotCount; slot++) outlets.Add(_itemDataStoreService.GetItem(slot));
            return outlets;
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
        public void Destroy() { IsDestroy = true; }
    }
}
```
`SpaceElevatorSatelliteInletState.cs`（入口の中身の3状態。空・運べる・運べない を bool 1つに畳まず、運べないときだけ昇降機がログを出せるようにする）:
```csharp
namespace Game.Block.Blocks.SpaceElevator
{
    // 入口の中身。運べないアイテムはマスタを変えた後のロードでだけ入口に残る
    // What the inlet holds; a non-carried item only remains after loading with a changed master
    internal enum SatelliteInletState
    {
        Empty,
        Carriable,
        NotCarried,
    }
}
```
`SpaceElevatorOutputSimulation.cs`（インベントリを 200 行未満に保つため、出口の模擬を別ファイルにする）:
```csharp
using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Blocks.SpaceElevator
{
    // 成果を出口の複製へ前から順に積む唯一の模擬。確認と実挿入がこれを共有する
    // The single simulation stacking results onto a copy of the outlets front to back; the check and the insert share it
    internal static class SpaceElevatorOutputSimulation
    {
        internal static bool TryStack(List<IItemStack> outlets, IReadOnlyList<IItemStack> outputs, out List<IItemStack> stacked)
        {
            stacked = outlets;
            foreach (var output in outputs)
            {
                var remainder = output;
                for (var i = 0; i < stacked.Count && remainder.Count > 0; i++)
                {
                    var result = stacked[i].AddItem(remainder);
                    stacked[i] = result.ProcessResultItemStack;
                    remainder = result.RemainderItemStack;
                }
                if (remainder.Count > 0) return false;
            }
            return true;
        }
    }
}
```
`wc -l` でインベントリが 200 行未満であることを確かめる。

- [ ] **Step 4: 昇降機と電力を書く**

`SpaceElevatorClimberComponent.cs`（電力の前例 `Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs`、進みの前例 `Game.Block/Blocks/Util/MachineCurrentPowerToSubSecond.cs`、出口の2点確認の前例 `Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs:49`・`ProcessingMachineProcessState.cs:118`）:
```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点の昇降機。衛星を1つずつ電力で昇らせ、頂上で軌道到着サービスへ渡して成果を出口へ入れる。成果が入りきらなければ昇り始めず、頂上でも待つ
    // The base's climber: lifts satellites one at a time with power and hands each over at the top; it neither starts nor hands over while the result would not fit
    public class SpaceElevatorClimberComponent : ISpaceElevatorBase, IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo
    {
        public bool IsConnected { get; private set; }
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        public string SaveKey => "spaceElevatorClimber";

        // 昇っている間だけ電力を要求する。頂上で出口の空きを待つ間は使わない
        // Request power only while ascending; waiting at the top for outlet space uses none
        public float RequestPower => IsAscending ? _param.RequiredPower : 0f;

        private bool IsCarrying => _climbingSatellite.Id != ItemMaster.EmptyItemId;
        private bool IsAscending => IsCarrying && _remainingClimbTicks > 0;

        private readonly Subject<Unit> _onChangeBlockState = new();
        private readonly SpaceElevatorBaseBlockParam _param;
        private readonly SpaceElevatorBaseInventoryComponent _inventory;
        private readonly IOrbitalArrivalService _orbitalArrivalService;
        private readonly uint _climbTotalTicks;

        private IItemStack _climbingSatellite;
        private uint _remainingClimbTicks;
        private bool _outputBlocked;
        private bool _inletNotCarried;
        private int _cablePoolAmount;
        private bool _hasEnoughCable;
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
            (_climbingSatellite, _remainingClimbTicks) = SpaceElevatorClimberSaveRestorer.Restore(save, inventory, _climbTotalTicks);
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // tick内の供給を確定してから進める
            // Latch this tick's supply before advancing
            _latchedPower = _suppliedPower;
            _suppliedPower = 0f;

            var wasOutputBlocked = _outputBlocked;
            var changed = IsCarrying ? AdvanceClimb() : TryStartClimb();
            changed |= wasOutputBlocked != _outputBlocked;

            // 出口待ちに入ったtickに1回だけ理由をログへ出す（以降はUIの状態表示で伝える）
            // Log the reason once on the tick the outlet wait begins; the UI status carries it afterwards
            if (!wasOutputBlocked && _outputBlocked) Debug.Log("[SpaceElevatorBase] 衛星1機の成果が出口に入りきらないため、出口が空くまで待ちます");

            // 昇っている間は進みが毎tick動くので毎tick通知する（同一ペイロードはイベント側が送らない）
            // While ascending the progress moves every tick, so notify every tick (the event packet drops identical payloads)
            if (changed || IsAscending) _onChangeBlockState.OnNext(Unit.Default);

            #region Internal

            bool AdvanceClimb()
            {
                // 電力不足なら供給率に比例して遅くなる
                // A power shortage slows the climb in proportion to the supply rate
                if (_remainingClimbTicks > 0)
                {
                    var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_latchedPower, _param.RequiredPower);
                    _remainingClimbTicks = subTicks >= _remainingClimbTicks ? 0 : _remainingClimbTicks - subTicks;
                    if (_remainingClimbTicks > 0) return false;
                }

                // 頂上では成果が出口へ入りきるときだけ渡す。入らなければ頂上で待つ
                // At the top, hand over only when the result fits the outlets; otherwise wait at the top
                _outputBlocked = !ResultFitsOutlets(_climbingSatellite.Id);
                if (_outputBlocked) return false;
                _inventory.InsertOutputs(_orbitalArrivalService.Arrive(_climbingSatellite.Id, 1));
                _climbingSatellite = ServerContext.ItemStackFactory.CreatEmpty();
                return true;
            }

            bool TryStartClimb()
            {
                // silent-ok: 未接続・衛星なしは通常の待機 / プレイヤーが拠点UIの状態表示（ReadyToConnect・InsufficientCable・WaitingSatellite）で気づく
                _outputBlocked = false;
                var wasInletNotCarried = _inletNotCarried;
                var inletState = _inventory.PeekSatellite(out var satelliteId);
                _inletNotCarried = inletState == SatelliteInletState.NotCarried;

                // 運べないアイテムが入口を塞いだら、その tick に1回だけ開発者ログへ出す
                // Log once on the tick a non-carried item starts blocking the inlet
                if (_inletNotCarried && !wasInletNotCarried) Debug.LogWarning($"[SpaceElevatorBase] 入口の {satelliteId} は今のマスタで運ぶ積荷ではないため昇れません。入口から取り出してください");
                if (!IsConnected || inletState != SatelliteInletState.Carriable) return false;

                // 昇り始める前に、成果が出口へ入りきるかを確かめる
                // Before starting, check that the result fits the outlets
                _outputBlocked = !ResultFitsOutlets(satelliteId);
                if (_outputBlocked) return false;
                _climbingSatellite = _inventory.TakeOneSatellite();
                _remainingClimbTicks = _climbTotalTicks;
                return true;
            }

            // 衛星1機の成果が出口へ入りきるか。昇り始める前と頂上の2点で使う
            // Whether one satellite's result fits the outlets; used before starting and at the top
            bool ResultFitsOutlets(ItemId satelliteId)
            {
                return _inventory.CanStoreOutputs(_orbitalArrivalService.PreviewArrivalOutputs(satelliteId, 1));
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

        // 接続できるかはプールが判定して渡す。拠点は判定せず状態に載せるだけ
        // The pool judges connectability and pushes it; the base only carries it in its state
        public void SetCablePoolAvailability(int poolAmount, bool hasEnoughCable)
        {
            BlockException.CheckDestroy(this);
            _cablePoolAmount = poolAmount;
            _hasEnoughCable = hasEnoughCable;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            var progress = IsCarrying ? 1f - (float)_remainingClimbTicks / _climbTotalTicks : 0f;
            var status = SpaceElevatorBaseStatusResolver.Resolve(IsConnected, _hasEnoughCable, _outputBlocked, IsCarrying, _latchedPower < _param.RequiredPower);
            var detail = new SpaceElevatorBaseStateDetail(IsConnected, _cablePoolAmount, progress, status);
            return new[] { new BlockStateDetail(SpaceElevatorBaseStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }

        // 撤去時は昇降中の衛星をプレイヤーへ返す
        // On removal, return the satellite being carried to the player
        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            return IsCarrying ? new List<IItemStack> { _climbingSatellite } : new List<IItemStack>();
        }

        public object GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return new SpaceElevatorClimberSaveJsonObject
            {
                IsConnected = IsConnected,
                ClimbingSatellite = new ItemStackSaveJsonObject(_climbingSatellite),
                RemainingClimbSeconds = GameUpdater.TicksToSeconds(_remainingClimbTicks),
            };
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; _onChangeBlockState.Dispose(); }
    }
}
```
`SpaceElevatorBaseStatusResolver.cs`（昇降機を 200 行未満に保つため、待ちの理由の選び方を分ける）:
```csharp
using Game.Block.Interface.State;

namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点の今の状態を、待ちの理由の優先順に1つ選ぶ
    // Picks the base's current status by waiting-reason priority
    internal static class SpaceElevatorBaseStatusResolver
    {
        internal static SpaceElevatorBaseStatus Resolve(bool isConnected, bool hasEnoughCable, bool outputBlocked, bool isCarrying, bool isPowerShort)
        {
            if (!isConnected) return hasEnoughCable ? SpaceElevatorBaseStatus.ReadyToConnect : SpaceElevatorBaseStatus.InsufficientCable;
            if (outputBlocked) return SpaceElevatorBaseStatus.OutputBlocked;
            if (!isCarrying) return SpaceElevatorBaseStatus.WaitingSatellite;
            return isPowerShort ? SpaceElevatorBaseStatus.LowPower : SpaceElevatorBaseStatus.Climbing;
        }
    }
}
```
`wc -l` で昇降機とインベントリが 200 行未満であることを確かめる（`IGetRefundItemsInfo.GetRefundItems` の戻り型は既存の interface に合わせる）。コメントやレビューの反映で超えたら、インベントリは出口の操作（`CanStoreOutputs`・`InsertOutputs`・`CopyOutlets`）を同じディレクトリの `internal class SpaceElevatorBaseOutlets`（`OpenableInventoryItemDataStoreService` と出口の範囲を ctor で受ける）へ、昇降機はセーブの書き出し（`GetSaveState`）をロード時のガードと同じ `SpaceElevatorClimberSaveRestorer` へ `ToSave(...)` として移す。

`SpaceElevatorClimberSaveRestorer.cs`（ロード時のガード。セーブの値を今のマスタで成り立つ形に直す）:
```csharp
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // セーブの昇降中の衛星と残りを、今のマスタで成り立つ形に直して返す
    // Normalises the saved climbing satellite and remaining time to what the current master allows
    internal static class SpaceElevatorClimberSaveRestorer
    {
        internal static (IItemStack Satellite, uint RemainingTicks) Restore(SpaceElevatorClimberSaveJsonObject save, SpaceElevatorBaseInventoryComponent inventory, uint climbTotalTicks)
        {
            var satellite = save.ClimbingSatellite.ToItemStack();
            if (satellite.Id == ItemMaster.EmptyItemId)
            {
                // 衛星が無いのに残りがあるのは壊れたセーブ。昇降していない状態で戻す
                // A remaining time without a satellite means a broken save; restore as not climbing
                if (save.RemainingClimbSeconds != 0) Debug.LogError($"[SpaceElevatorBase] 昇降中の衛星が無いのに残り {save.RemainingClimbSeconds} 秒がセーブにあるため、昇降していない状態で戻します");
                return (satellite, 0);
            }

            // 運ぶ積荷から外れた衛星は頂上で軌道到着サービスが例外を出すので、昇降を取り消して入口へ戻す
            // A satellite no longer carried would make the orbital service throw at the top, so cancel the climb and return it to the inlet
            if (!inventory.IsCarriedPayload(satellite.Id))
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の衛星 {satellite.Id} は今のマスタで運ぶ積荷ではないため、昇降を取り消して入口へ戻します");
                inventory.ReturnSatelliteWithoutEvent(satellite);
                return (ServerContext.ItemStackFactory.CreatEmpty(), 0);
            }

            // 昇降機は1機ずつ運ぶ。壊れたセーブで2機以上あれば1機だけ昇らせ、余りを入口へ戻す
            // The climber carries one at a time; a broken save with more keeps one climbing and returns the rest to the inlet
            if (satellite.Count > 1)
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の衛星が {satellite.Count} 機セーブにあるため、1機だけ昇らせ残りを入口へ戻します");
                inventory.ReturnSatelliteWithoutEvent(ServerContext.ItemStackFactory.Create(satellite.Id, satellite.Count - 1));
                satellite = ServerContext.ItemStackFactory.Create(satellite.Id, 1);
            }

            // 残りが負・NaN・無限のセーブは壊れている。最初から昇り直す（0は頂上で出口待ちの正しい値）
            // A negative, NaN or infinite remaining time is a broken save; climb again from the start (zero is the valid waiting-at-top value)
            if (!(save.RemainingClimbSeconds >= 0) || double.IsInfinity(save.RemainingClimbSeconds))
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降の残り {save.RemainingClimbSeconds} 秒は不正なため、最初から昇り直します");
                return (satellite, climbTotalTicks);
            }

            // マスタで昇降時間が縮んだときは総時間に収め、進みが負にならないようにする
            // When the master shortened the climb, clamp to the total so the progress never goes negative
            var remainingTicks = GameUpdater.SecondsToTicks(save.RemainingClimbSeconds);
            if (remainingTicks <= climbTotalTicks) return (satellite, remainingTicks);
            Debug.LogWarning($"[SpaceElevatorBase] 昇降の残り {remainingTicks}tick が総時間 {climbTotalTicks}tick を超えるため、総時間に収めます");
            return (satellite, climbTotalTicks);
        }
    }
}
```

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
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestSpaceElevatorBase, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }

        // 拠点に電柱をつなぎ、指定電力の発電機をセグメントへ足す
        // Wire a pole to the base and add a generator of the given power to the segment
        public static void PowerBase(IBlock elevatorBase, Vector3Int basePosition, float power)
        {
            var polePosition = basePosition + new Vector3Int(2, 0, 0);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            ElectricWireTestUtil.Connect(basePosition, polePosition);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ServerContext.GetService<IElectricWireNetworkLookup>().TryGetEnergySegment(elevatorBase.BlockInstanceId, out var segment));
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
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
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
            Assert.AreEqual(SpaceElevatorBaseStatus.InsufficientCable, ReadDetail(elevatorBase).Status);

            // テストmodの1エレベーター分は100。残量が届き、足りると接続できる状態に変わる
            // The test mod needs 100 per elevator; the amount arrives and the state turns connectable once enough remains
            LaunchCables(150);
            var detail = ReadDetail(elevatorBase);
            Assert.AreEqual(150, detail.CablePoolAmount);
            Assert.AreEqual(SpaceElevatorBaseStatus.ReadyToConnect, detail.Status);
        }

        public static SpaceElevatorBaseStateDetail ReadDetail(IBlock elevatorBase)
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
using Game.Block.Interface;
using Game.Block.Interface.State;
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

            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Id);
            Assert.AreEqual(5, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
        }

        [Test]
        public void 電力が半分なら倍かかる()
        {
            var elevatorBase = SetupConnected(50f);
            Inventory(elevatorBase).SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 1);

            RunSeconds(2.2);
            Assert.AreEqual(0, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.LowPower, ReadDetail(elevatorBase).Status);

            RunSeconds(2.4);
            Assert.AreEqual(5, Inventory(elevatorBase).GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
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
            Assert.AreEqual(SpaceElevatorBaseStatus.InsufficientCable, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 出口が塞がっていると昇り始めず空くと昇る()
        {
            var elevatorBase = SetupConnected(100f);
            var inventory = Inventory(elevatorBase);

            // 出口を別アイテムで塞ぐと、衛星は入口に残り理由を出す
            // Blocking the outlet with another item keeps the satellite in the inlet and shows why
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, ForUnitTestItemId.ItemId1, 1);
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, ReadDetail(elevatorBase).Status);
            Assert.AreEqual(1, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
            Assert.AreEqual(0f, ReadDetail(elevatorBase).ClimbProgress);

            // 出口を空けると昇り始め、2秒で成果が出る
            // Clearing the outlet starts the climb and the result appears after two seconds
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunSeconds(2.2);
            Assert.AreEqual(0, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
            Assert.AreEqual(5, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
        }

        [Test]
        public void 昇る間に出口が塞がると頂上で電力を使わず待ち空くと渡す()
        {
            var elevatorBase = SetupConnected(100f);
            var inventory = Inventory(elevatorBase);
            var climber = elevatorBase.GetComponent<SpaceElevatorClimberComponent>();
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);

            // 昇降中に出口を塞ぐと、昇り切っても渡さず頂上で待つ
            // Blocking the outlet mid-climb makes it wait at the top without handing over
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, ForUnitTestItemId.ItemId1, 1);
            RunSeconds(2);
            var waiting = ReadDetail(elevatorBase);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, waiting.Status);
            Assert.AreEqual(1f, waiting.ClimbProgress);
            Assert.AreEqual(0f, climber.RequestPower);

            // 出口を空けると次のtickで渡し、成果が出る
            // Clearing the outlet hands it over on the next tick and the result appears
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunSeconds(0.1);
            Assert.AreEqual(5, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 入口は運ぶ積荷だけを受ける()
        {
            CreateServer();
            var inventory = Inventory(PlaceBase(Vector3Int.zero));
            var rejected = inventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));
            Assert.AreEqual(3, rejected.Count);
            Assert.IsFalse(inventory.IsAllowedToPlace(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 1)));
        }

        private static IBlock SetupConnected(float power)
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            PowerBase(elevatorBase, Vector3Int.zero, power);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            return elevatorBase;
        }

        private static SpaceElevatorBaseInventoryComponent Inventory(IBlock elevatorBase)
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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SpaceElevator;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorBaseSaveAndRefundTest
    {
        private const int PlayerId = 3;

        [Test]
        public void 接続と昇降中がセーブを往復する()
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

            // ロードで置かれた拠点も接続サービスが追跡し、撤去で接続分がプールへ戻る
            // The connection service also tracks a base placed by loading, and removing it returns the connection to the pool
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, loadedProvider.GetService<SpaceElevatorConnectionService>().Connect(loaded));
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            Assert.AreEqual(100, loadedProvider.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void 撤去すると昇降中の衛星とスロットがプレイヤーへ返り接続分がプールへ戻る()
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

        [Test]
        public void ロードした残りが昇降時間を超えていたら総時間に収める()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var param = (SpaceElevatorBaseBlockParam)elevatorBase.BlockMasterElement.BlockParam;
            var satellite = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(satellite), RemainingClimbSeconds = param.ClimbSeconds * 10 };

            // マスタで昇降時間を縮めた後のロードを模す。進みは負にならず0から始まる
            // Mimic loading after the master shortened the climb; the progress starts at zero instead of going negative
            LogAssert.Expect(LogType.Warning, new Regex("総時間に収めます"));
            var climber = LoadClimber(elevatorBase, save);
            Assert.AreEqual(0f, ReadClimberDetail(climber).ClimbProgress);
        }

        [Test]
        public void 運ぶ積荷でない衛星が昇降中でロードされたら入口へ戻す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var stray = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(stray), RemainingClimbSeconds = 1 };

            // 頂上で軌道到着サービスが例外を出さないよう、昇降を取り消して入口へ戻す
            // Cancel the climb and return it to the inlet so the orbital service never throws at the top
            LogAssert.Expect(LogType.Error, new Regex("運ぶ積荷ではない"));
            var climber = LoadClimber(elevatorBase, save);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            Assert.AreEqual(ForUnitTestItemId.ItemId1, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Id);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadClimberDetail(climber).Status);

            // 入口の運べないアイテムは運ばず、入口を塞いだ tick に開発者ログへ出す
            // The non-carried item in the inlet is never carried, and the tick it starts blocking is logged
            LogAssert.Expect(LogType.Warning, new Regex("運ぶ積荷ではないため昇れません"));
            climber.Update();
            climber.Update();
            Assert.AreEqual(ForUnitTestItemId.ItemId1, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Id);
            Assert.AreEqual(0, climber.GetRefundItems().Count);
        }

        [Test]
        public void 昇降中の衛星が2機以上のセーブは1機だけ昇らせ残りを入口へ戻す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var satellites = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 3);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(satellites), RemainingClimbSeconds = 1 };

            // 壊れたセーブを模す。1機だけ昇り、余りの2機は入口へ戻る
            // Mimic a broken save; one satellite keeps climbing and the extra two go back to the inlet
            LogAssert.Expect(LogType.Error, new Regex("1機だけ昇らせ"));
            var climber = LoadClimber(elevatorBase, save);
            var inventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            Assert.AreEqual(2, inventory.GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
            Assert.AreEqual(1, climber.GetRefundItems()[0].Count);
        }

        [Test]
        public void 残りが負のセーブは最初から昇り直す()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var satellite = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1);
            var save = new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(satellite), RemainingClimbSeconds = -5 };

            // 負の残りで即座に頂上へ着かせない
            // A negative remaining time must not jump straight to the top
            LogAssert.Expect(LogType.Error, new Regex("最初から昇り直します"));
            var climber = LoadClimber(elevatorBase, save);
            Assert.AreEqual(0f, ReadClimberDetail(climber).ClimbProgress);
            Assert.AreEqual(1, climber.GetRefundItems().Count);
        }

        private static SpaceElevatorClimberComponent LoadClimber(IBlock elevatorBase, SpaceElevatorClimberSaveJsonObject save)
        {
            var param = (SpaceElevatorBaseBlockParam)elevatorBase.BlockMasterElement.BlockParam;
            var states = new Dictionary<string, object> { { "spaceElevatorClimber", save } };
            return new SpaceElevatorClimberComponent(states, param, elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>());
        }

        private static SpaceElevatorBaseStateDetail ReadClimberDetail(SpaceElevatorClimberComponent climber)
        {
            return MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(climber.GetBlockStateDetails()[0].Value);
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
git commit -m "feat(space-elevator): 拠点ブロック（昇降機・電力・出口の確認・撤去返却）を足す"
```

### Task 9: 接続プロトコル

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SpaceElevator/ConnectSpaceElevatorProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef`（`Game.SpaceElevator` を参照に足す）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/SpaceElevator/ConnectSpaceElevatorProtocolTest.cs`（プロトコルのテストの置き場。前例 `Tests/CombinedTest/Server/PacketTest/ElectricToGearOutputModeProtocolTest.cs`、S2 の `PacketTest/Rocket/`）

**Interfaces:**
- Consumes: Task 7 の `SpaceElevatorConnectionService.Connect(IBlock)`・`SpaceElevatorConnectResult`、Task 8 の `SpaceElevatorConnectionTest.ReadDetail(IBlock)`
- Produces: タグ `va:connectSpaceElevator`、`ConnectSpaceElevatorRequest(Vector3Int position)`（`[Key(2)] Vector3IntMessagePack Position`）、`ConnectSpaceElevatorResponse`（`[Key(2)] SpaceElevatorConnectResult Result`）

- [ ] **Step 1: プロトコルを書く**

`Server.Protocol/PacketResponse/SpaceElevator/ConnectSpaceElevatorProtocol.cs`（形の前例 `SetElectricToGearOutputModeProtocol.cs`、拒否ログの前例 `RemoveBlockProtocol.cs` の `Refuse`。`PacketResponse/` 直下は既に 50 ファイルを超えるのでサブディレクトリに置く）:
```csharp
using System;
using Game.Block.Interface.Component;
using Game.Context;
using Game.SpaceElevator;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
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

            // 接続できなかった理由を開発者ログへ残す。プレイヤーには拠点の状態から理由を出す
            // Log why the connection was refused for developers; players see the reason from the base's status
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
`PacketResponseCreator.cs` の `SetElectricToGearOutputModeProtocol` の登録行の次に `_packetResponseDictionary.Add(ConnectSpaceElevatorProtocol.ProtocolTag, new ConnectSpaceElevatorProtocol(serviceProvider));` を足す。

- [ ] **Step 2: テストを書く**

`Tests/CombinedTest/Server/PacketTest/SpaceElevator/ConnectSpaceElevatorProtocolTest.cs`:
```csharp
using Game.Block.Interface.Component;
using Game.SpaceElevator;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Server.PacketTest.SpaceElevator
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
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/SpaceElevator
git commit -m "feat(space-elevator): 拠点の接続プロトコルを足す"
```

### Task 10: 拠点の UI（クライアントの DTO・action と webui）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Requests/BlockRequestApi.cs`（契約型は `Game.Block.Interface` と `Server.Protocol` にあるので、クライアントの asmdef には参照を足さない）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/SpaceElevator/SpaceElevatorDetailDto.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/SpaceElevator/SpaceElevatorDetailDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs`、`BlockDetailDtoBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/SpaceElevator/SpaceElevatorConnectActionHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_space_elevator.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/WireContractSpaceElevatorTest.cs`（ブロック詳細のテストの置き場。`Client.Tests/WebUi/BlockDetail/` は既存で DtoBuilder のテスト群がある。`WebUi/` 直下は既に 40 ファイルで1ディレクトリ10ファイルの規則を超えているので増やさない。S2 の `BlockDetail/RocketLaunchPadWireContractTest.cs` の有無に関係なくここに置く）
- Modify: `Localization/localization.csv`
- Create: `moorestech_web/webui/src/bridge/contract/schemas/spaceElevator/index.ts`
- Create（S2 の分割が未取り込みのときだけ）: `moorestech_web/webui/src/bridge/contract/schemas/train/index.ts`、`moorestech_web/webui/src/bridge/contract/schemas/beltThroughput/index.ts`（inventory.ts から移す。S2 と同じ分け方）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`、`moorestech_web/webui/src/bridge/contract/schemas/index.ts`、`moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/SpaceElevatorSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/spaceElevatorDisplay.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/views/SpaceElevatorInventory.tsx`
- Modify: `moorestech_web/webui/src/features/blockInventory/registry/blockComponentRegistry.ts`、`moorestech_web/webui/src/features/blockInventory/registry/registeredBlockTypes.ts`、`moorestech_web/webui/src/features/blockInventory/registry/blockComponentRegistry.test.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/blocks/energyFixtures.ts`、`moorestech_web/webui/e2e/mock-host/httpHandler.ts`、`moorestech_web/webui/e2e/mock-host/wsHandler.ts`、`moorestech_web/webui/e2e/mock-host/detailActions.ts`
- Create: `moorestech_web/webui/e2e/tests/block/spaceElevator.spec.ts`

**Interfaces:**
- Consumes: Task 8 の `SpaceElevatorBaseStateDetail`・`SpaceElevatorBaseStatus`、Task 9 の `ConnectSpaceElevatorRequest`/`ConnectSpaceElevatorResponse`
- Produces:
  - wire の `spaceElevator: { connected: boolean; cablePoolAmount: number; cablesPerElevator: number; climbProgress: number; status: "readyToConnect" | "insufficientCable" | "waitingSatellite" | "climbing" | "lowPower" | "outputBlocked" }`（blockType `SpaceElevatorBase` のときだけ。接続できるかはサーバーのプールが判定して `status` に載せ、webui は判定しない）
  - action `space_elevator.connect`（payload `{}`）
  - testid: `space-elevator-section`, `space-elevator-status`, `space-elevator-pool`, `space-elevator-progress`, `space-elevator-connect`, `space-elevator-grid`, `space-elevator-view`

配置: 拠点は接続ボタンというブロック固有の操作を持つので、専用 view `views/SpaceElevatorInventory.tsx`（スロット・固有部分・電力網）を作ってレジストリに登録する（前例 `views/ElectricToGearInventory.tsx`・`views/TrainPlatformInventory.tsx`）。`SectionStackView` には足さず、e2e の `intentionalGeneric` にも入れない（`SectionStackView.tsx:51-52`「固有UIだけをレジストリへ残す」、`intentionalGeneric` は移植待ちの許可リスト）。

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

`Topics/BlockDetail/SpaceElevator/SpaceElevatorDetailDto.cs`:
```csharp
namespace Client.WebUiHost.Game.Topics.BlockDetail
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
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 宇宙エレベーター拠点の状態詳細をDTOへ詰める
    /// Fills the space elevator base's state detail into its capability DTO
    /// </summary>
    public static class SpaceElevatorDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            if (param is not SpaceElevatorBaseBlockParam elevatorParam) return;
            var state = block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
            // 初回の状態が届く前は未着として何も詰めない（webui は詳細なしで描く）
            // Before the first state arrives nothing is filled; the webui renders without the detail
            if (state == null)
            {
                Debug.LogWarning("SpaceElevatorBase の状態詳細が未着のため DTO を保留します");
                return;
            }

            dto.SpaceElevator = new SpaceElevatorDetailDto
            {
                Connected = state.IsConnected,
                CablePoolAmount = state.CablePoolAmount,
                // 1エレベーター分の量はマスタ値なので param から引く
                // The cables per elevator is a master value, so read it from the param
                CablesPerElevator = elevatorParam.CablesPerElevator,
                ClimbProgress = state.ClimbProgress,
                Status = ToWireStatus(state.Status),
            };
        }

        private static string ToWireStatus(SpaceElevatorBaseStatus status)
        {
            return status switch
            {
                SpaceElevatorBaseStatus.ReadyToConnect => "readyToConnect",
                SpaceElevatorBaseStatus.InsufficientCable => "insufficientCable",
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
`BlockInventoryDtos.cs` の `public ElectricToGearDetailDto ElectricToGear;` の次に `public SpaceElevatorDetailDto SpaceElevator;`。`BlockDetailDtoBuilder.Apply` の `ElectricToGearDetailDtoBuilder.Apply(dto, block, param);` の次に `SpaceElevatorDetailDtoBuilder.Apply(dto, block, param);`。

`Actions/SpaceElevator/SpaceElevatorConnectActionHandler.cs`（前例 `Actions/ElectricToGearActions.cs`。`Actions/` 直下は既に10ファイルを超えるのでサブディレクトリに置く）:
```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.Component;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
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
`WebUiGameBinder.cs` の `hub.RegisterAction(new ElectricToGearSetOutputModeActionHandler(subInventoryState));` の次に `hub.RegisterAction(new SpaceElevatorConnectActionHandler(subInventoryState));`（`wc -l` が 200 以上になったら `ElectricToGear` 以降のブロック系 action 登録を `Actions/BlockActionRegistration.cs` の `public static void Register(WebUiHub hub, SubInventoryState subInventoryState)` へ移す）。
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
`Client.Tests/WebUi/BlockDetail/WireContractSpaceElevatorTest.cs`（前例 `WireContractElectricToGearTest.cs` をそのまま写し、名前空間を同じディレクトリの他のテストと同じ `Client.Tests.WebUi.BlockDetail` にし、DTO を次にする）:
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
ui.blockInventory.spaceElevatorStatusInsufficientCable,Not enough cable. Launch graphene cables to fill the pool,Not enough cable. Launch graphene cables to fill the pool,ケーブルが足りません。グラフェンケーブルを打ち上げてプールを満たしてください,Nicht genug Kabel. Starte Graphenkabel,케이블이 부족합니다. 그래핀 케이블을 발사하세요
ui.blockInventory.spaceElevatorStatusReadyToConnect,Not connected; ready to connect,Not connected; ready to connect,未接続（接続できます）,Nicht verbunden; bereit,연결 안 됨 (연결 가능)
ui.blockInventory.spaceElevatorStatusWaitingSatellite,Waiting for a GPU satellite,Waiting for a GPU satellite,GPU衛星を待っています,Warte auf GPU-Satelliten,GPU 위성 대기 중
ui.blockInventory.spaceElevatorStatusClimbing,Climbing,Climbing,昇降中,Steigt auf,상승 중
ui.blockInventory.spaceElevatorStatusLowPower,Climbing slowly (low power),Climbing slowly (low power),電力不足で遅くなっています,Langsam (zu wenig Strom),전력 부족으로 느려짐
ui.blockInventory.spaceElevatorStatusOutputBlocked,Output full; waiting until the result fits,Output full; waiting until the result fits,出口が満杯のため、成果が入るまで待っています,Ausgang voll; wartet bis das Ergebnis passt,출구가 가득 차 결과가 들어갈 때까지 대기 중
```
`cd moorestech_web/webui && npm run gen:i18n` で `localizationKeys.ts` を再生成する。

- [ ] **Step 4: webui の受信スキーマを足し、inventory.ts を 200 行未満にする**

`schemas/spaceElevator/index.ts`:
```ts
import { z } from "zod";

export const SpaceElevatorStatusSchema = z.enum(["readyToConnect", "insufficientCable", "waitingSatellite", "climbing", "lowPower", "outputBlocked"]);
export const SpaceElevatorDataSchema = z.object({
  connected: z.boolean(),
  cablePoolAmount: z.number().int().nonnegative(),
  cablesPerElevator: z.number().int().positive(),
  climbProgress: z.number().min(0).max(1),
  status: SpaceElevatorStatusSchema,
}).strict();
```
`payloadTypes.ts` の `import type { ... } from "./schemas";` に `SpaceElevatorDataSchema, SpaceElevatorStatusSchema,` を足し、`TrainPlatformMode` の行の次に足す（型は schemas から出さず、ここに集める。前例 `GearToElectricGenerationState`）:
```ts
export type SpaceElevatorData = z.infer<typeof SpaceElevatorDataSchema>;
export type SpaceElevatorStatus = z.infer<typeof SpaceElevatorStatusSchema>;
```
`inventory.ts`（215行）は S2 と同じ分け方で 200 行未満にする（S2 plan の D11 と webui の手順。同じファイルを別の分け方で切ると、S2 を取り込んだときに同じ schema が2か所にできる）。Task 5 で取り込んだ S2 に `schemas/train/index.ts`・`schemas/beltThroughput/index.ts` が既にあれば分割はせず、下の2か所を足すだけにする。無ければ S2 と同じ内容で分割する: `inventory.ts` の `TrainPlatformModeSchema`〜`TrainStationDetailSchema` を `schemas/train/index.ts` へ、`BeltThroughputWindowDataSchema`・`BeltThroughputDataSchema` を `schemas/beltThroughput/index.ts` へ本体ごと移し（`const` だった `TrainTimetableStateSchema`・`TrainStationDetailSchema` は `export const` にする）、`inventory.ts` はそれを import する。`schemas/index.ts` に `export * from "./train"; export * from "./beltThroughput";` を足す。挙動は変えない（Task 15 で S2 の最新を取り込んで同じファイルが衝突したら S2 側を取る）。そのうえで `BlockInventoryOpenSchema` に `spaceElevator: SpaceElevatorDataSchema.optional(),` を足し、`superRefine` に:
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
  readyToConnect: { labelKey: L.ui.blockInventory.spaceElevatorStatusReadyToConnect, insufficient: false },
  insufficientCable: { labelKey: L.ui.blockInventory.spaceElevatorStatusInsufficientCable, insufficient: true },
  waitingSatellite: { labelKey: L.ui.blockInventory.spaceElevatorStatusWaitingSatellite, insufficient: false },
  climbing: { labelKey: L.ui.blockInventory.spaceElevatorStatusClimbing, insufficient: false },
  lowPower: { labelKey: L.ui.blockInventory.spaceElevatorStatusLowPower, insufficient: true },
  outputBlocked: { labelKey: L.ui.blockInventory.spaceElevatorStatusOutputBlocked, insufficient: true },
};

export function spaceElevatorStatusDisplay(status: SpaceElevatorStatus): SpaceElevatorStatusDisplay {
  return StatusDisplayTable[status];
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
import { spaceElevatorStatusDisplay } from "./spaceElevatorDisplay";

// 接続状態・プール残量・昇降の進みと、未接続なら接続ボタンを出す。接続できるかはサーバーが status で渡す
// Show the connection state, the pool amount and the climb progress, plus the connect button while unconnected; the server passes connectability in status
export default function SpaceElevatorSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.spaceElevator;
  if (!detail) return null;
  const display = spaceElevatorStatusDisplay(detail.status);
  const canConnect = detail.status === "readyToConnect";
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
    </Stack>
  );
}
```
`views/SpaceElevatorInventory.tsx`（前例 `views/TrainPlatformInventory.tsx`）:
```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import BlockItemGrid from "../BlockItemGrid";
import { ElectricNetworkSection } from "../details/NetworkSections";
import SpaceElevatorSection from "../details/spaceElevator/SpaceElevatorSection";

const ITEM_SLOTS_TEST_ID = "space-elevator-grid";

// 衛星の入口と成果の出口、接続と昇降の固有部分、電力網を縦に並べる
// Stack the satellite inlet and result outlets, the connect/climb section and the power network
export default function SpaceElevatorInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm" data-testid="space-elevator-view">
      <BlockItemGrid itemSlots={data.itemSlots} testId={ITEM_SLOTS_TEST_ID} />
      <SpaceElevatorSection data={data} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
```
`registry/blockComponentRegistry.ts` に `import SpaceElevatorInventory from "../views/SpaceElevatorInventory";` と `blockComponents` の `ElectricPole: ElectricPoleInventory,` の次に `SpaceElevatorBase: SpaceElevatorInventory,` を足す。`registry/registeredBlockTypes.ts` の `"ElectricPole",` の次に `"SpaceElevatorBase",` を足す。`registry/blockComponentRegistry.test.ts` の `ElectricToGearGeneratorは専用ビューへ登録する` の次に:
```ts
  it("SpaceElevatorBaseは接続ボタンを持つ専用ビューへ登録する", () => {
    expect(blockComponents).toHaveProperty("SpaceElevatorBase");
    expect(resolveBlockComponent("SpaceElevatorBase")).not.toBe(SectionStackView);
  });
```

`details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx`（前例 `views/__tests__/SectionStackView.test.tsx` の描画ヘルパーと `dispatchAction` のモックを写す）:
```tsx
import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { L } from "@/shared/i18n";
import SpaceElevatorSection from "../SpaceElevatorSection";
import { spaceElevatorStatusDisplay } from "../spaceElevatorDisplay";

describe("spaceElevatorStatusDisplay", () => {
  it("ケーブル不足は不足の文言を足りない色で出す", () => {
    expect(spaceElevatorStatusDisplay("insufficientCable")).toEqual({ labelKey: L.ui.blockInventory.spaceElevatorStatusInsufficientCable, insufficient: true });
  });
});

describe("SpaceElevatorSection", () => {
  it("サーバーがケーブル不足と判定したら接続ボタンを押せない", () => {
    renderSection({ connected: false, cablePoolAmount: 250, cablesPerElevator: 1000, climbProgress: 0, status: "insufficientCable" });
    expect(screen.getByTestId("space-elevator-connect")).toBeDisabled();
    expect(screen.getByTestId("space-elevator-status")).toBeInTheDocument();
  });

  it("サーバーが接続できると判定したら接続actionを送る", () => {
    const dispatch = mockDispatchAction();
    renderSection({ connected: false, cablePoolAmount: 1000, cablesPerElevator: 1000, climbProgress: 0, status: "readyToConnect" });
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

出荷マスタにはまだ拠点が無いので、e2e の registry fixture（blocks.json から作る）は変えない（拠点が入ったときの1行は統合タスク。末尾の節「旧 Task 10 Step 6 の e2e fixture」）。レジストリに先に登録しておくのは `blockRegistryCoverage.spec.ts` が許す（fixture に無い型の登録は落ちない）。
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
  spaceElevator: { connected: false, cablePoolAmount: 1000, cablesPerElevator: 1000, climbProgress: 0, status: "readyToConnect" },
} satisfies BlockInventoryWireData;
```
（`BlockGuids` に専用の GUID を足す前例があればそれに従う）`httpHandler.ts` の `electricToGear: fx.blockElectricToGear,` の次に `spaceElevator: fx.blockSpaceElevator,`。`detailActions.ts` に:
```ts
// space_elevator.connect: サーバーの代わりに、接続できる状態なら接続済みにしてプールから引く
// space_elevator.connect: standing in for the server, mark connected and take from the pool when the status says it can connect
export function applySpaceElevatorConnect(block: BlockInventoryWireData): boolean {
  if (!block.open || !("spaceElevator" in block) || !block.spaceElevator) return false;
  const detail = block.spaceElevator;
  if (detail.connected || detail.status !== "readyToConnect") return false;
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
using Game.Block.Interface.State;
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

### Task 12: （統合タスクへ移した）シミュレーションにエレベーターを足して値を確定する

この PR では実行しない（マスタの中身は S1〜S3 の実装後に統合タスク bd `moorestech-xyuaf.2` で入れる。判断記録 P29）。下書きは末尾「統合タスクへ引き継ぐマスタの中身」の「旧 Task 12」。

### Task 13: （統合タスクへ移した）unityプレイ録画テスト

この PR では実行しない（録画テストは統合タスク bd `moorestech-xyuaf.2` で撮る。判断記録 P18・P29）。下書きは末尾「統合タスクへ引き継ぐマスタの中身」の「旧 Task 13」。

### Task 14: 全ブランチレビュー

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

Codex ホストでは moores-code-review の `references/codex-host.md` の runner で実行する。レビュー指摘の反映がソースの判定経路・条件式・その評価時点（昇降機の開始条件・頂上で渡す条件・出口の確認・接続判定・撤去返却など）に触れたら、反映後のコードで Task 3・5〜11 のテスト一式（各タスクの「コンパイルしテストを実行する」ステップのフィルタすべてと webui の `npx vitest run`・`npx playwright test e2e/tests/block`）を再実行してから完了とする。この plan に unityプレイ録画テストは無い（統合タスクへ移した。判断記録 P18・P29）ので、レビュー報告に「録画は統合タスク bd `moorestech-xyuaf.2` で撮る」と書く。指摘の反映ごとにコミットする。

### Task 15: PR 作成とセッションを閉じられる状態

- [ ] **Step 1: セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**

順序:
1. 土台をそろえる: S2 の PR が origin/master へ入っていれば `git merge origin/master`、入っていなければ `git merge origin/feat/tier9-compute-data-loop` で S2 の最新を取り込み直す（S2 の途中のコミットだけを持つ状態で出さない）。取り込んだ後に `WorldSaveAllInfo.CurrentVersion` と `Game.SaveLoad/Migration/Steps/` を見て、S3 の移行ステップと同じ FromVersion のステップ（S1 など）が先に入っていたら、判断記録 P11 の規則で S3 の移行ステップを先に入った版の続きへ繰り上げる（FromVersion・CurrentVersion・クラス名とファイル名・テストの版・3箇所の登録の並び）。`uloop compile --project-path ./moorestech_client`（ErrorCount 0）と Task 7〜9 のテスト（`SaveMigrationChainTest` を含む）を通して push する。
2. ピンを確かめる: `git diff <1. で取り込んだ ref> -- .moorestech-external-revisions.json` が空（S3 はマスタを変えないので、ピンは S2 か origin/master と同じ値）。
3. pr-create スキルで PR を作る（base `master`）。本文に入れる節: 概要（この PR はコード・スキーマ・テスト用データまでで、遊べるのは統合タスクの後）／S2 への依存（S2 の PR が未マージなら「S2 の PR のマージ後にマージする」と書き、その URL）／統合タスクへ回したもの（末尾の節の見出しを並べ、裁定 `.decisions/2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md` と bd `moorestech-xyuaf.2` へリンク）／確かめ方（CombinedTest・プロトコルテスト・wire 契約テスト・webui の vitest と e2e の結果。録画は統合タスク）／セーブ版 7→8（S2 が 6→7。S1 が先に入っていれば繰り上げた後の版）／判断記録の「S1 待ち」「S2 待ち」の一覧／本PR外のリファクタ提案（判断記録 P30）／レビュー依頼の注目点（判断記録 P10 の起動時のマスタ整合検査サービスと P31）。マージはしない。
4. CI の結果を確かめる。
5. `bd note moorestech-xyuaf.2 "S3 の PR <URL>。S3 から統合タスクへ渡すマスタの中身・バランス・録画の下書きは S3 plan docs/superpowers/plans/2026-10-23-tier9-fusion-elevator-endgame.md の「統合タスクへ引き継ぐマスタの中身」節"` を書く。
6. `moores-wt rm tier9-fusion-elevator-endgame`（Editor と worktree を畳む。未コミット・未 push が無いことを先に確かめる）。

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
- 他セッションの裁定で S3 に効くもの: S1「Tier9 の目標プレイ時間は半導体解放からクリアまで 15〜25 時間」「チップは別アイテムで上限 Lv5・研究とクリアは大部分 Lv1」「モジュールは品質・速度・省エネ・生産性の4種」。S2「研究ロケット技術を新設」「軌道エレベーターの建設は計算データを消費しロケット技術を前提にする」「計算データは打ち上げた設備の出力口に出し、満杯なら待たせて理由を出す」「Tier9 のマスタの中身は S1〜S3 の実装後に統合タスクでまとめて入れる」（P29）

### planning 中の判断（出所は各行。書いていなければ agent前提）

| # | 判断 | 理由・根拠 |
|---|---|---|
| P1 | 発電機（ElectricGenerator）に `IBlockStateObservable` を足し、`PowerGeneratorStateDetail` と液体タンク（`FluidMachineInventoryStateDetail`）を出す。通知は点き始め・燃え尽きの tick、燃焼中の毎 tick（残燃料が進む）、液体を受け入れたとき | 裁定「既存モデル＋汎用UIの稼働表示」。前例 `FuelGearGeneratorComponent.cs:88-91`（稼働中は毎 tick 通知）・`VanillaMachineFluidInventoryComponent.cs:139`（液体の受け入れで通知）。同じ値の再送は `ChangeBlockStateEventPacket` が落とす。配置検査 V1 で「点き始め・燃え尽きだけ」から直した（残燃料バーが止まるため） |
| P2 | クライアントで ElectricGenerator を CommonMachine 扱いから外す（比べた案: 発電機専用の状態プロセッサを置く → 見た目に変化が無いので要らない。CommonMachine のキーも出す → 発電機は機械の処理状態を持たず偽の値を送ることになる） | 原因: 推測（根拠: `CommonMachineBlockStateChangeProcessor.cs:23-28` は CommonMachine キーが無い状態で `Debug.LogError` を出す。発電機が状態を出すと必ず通る）。実測は統合タスクの録画で行う（出荷マスタの発電機が開けるようになるのは統合タスク。末尾の節の旧 Task 13） |
| P3 | 発電機は専用 view を作らず汎用 `SectionStackView` で出す。e2e の registry で意図的 Generic にするのは、核融合炉が開けるようになる統合タスク（末尾の節の旧 Task 3 Step 4 の e2e） | 前例「標準ブロック表示をデータ有無で合成し、固有UIだけをレジストリへ残す」 |
| P4 | `VanillaElectricGeneratorFuelService`（289行）と `VanillaElectricGeneratorComponent` を 200 行未満へ分割する。変換器が触るフィールドを `internal` にする | AGENTS.md の 200 行規約。同アセンブリの変換器だけが触る |
| P5 | 〔統合タスクへの下書き〕数値の初期値: 核融合炉 重水素10/2秒で 20000、重水素抽出装置 4000（水100→重水素10/8秒）、反物質製造装置 12000（重水素50→反物質1/30秒）、反物質爆弾 反物質20＋ICチップLv1 50＋鋼鉄板100＋コンピュータ20（電気汎用工作装置 60秒）、グラフェン4レシピと3Dプリンタ2レシピは Task 1・2 の JSON の値 | Task 4 のシミュレーション（R3・R5・R23 の範囲）で検査し、NG なら直して本節へ追記する。全体の時間目標は S1 の裁定 |
| P6 | 〔統合タスクへの下書き〕最終研究の名前は「最終研究」、消費は反物質爆弾 10、解放物なし | ユーザー原文「終了を明示する仮の研究があってもok、その程度」。量は agent前提（Task 4 で所要時間を検査） |
| P7 | 〔統合タスクへの下書き〕反物質の機械名は「反物質製造装置」 | 既存モデル名 `Antimatter_manufacturing_equipment` と ADR 0090 に合わせる。裁定ファイルの「反物質生成装置」は機能の言い換え（ユーザーが名前を決めた裁定ではない） |
| P8 | 〔統合タスクへの下書き〕グラフェンケーブルは軌道到着の積荷表に 1便1000本・成果なしで載せ、スタック表 [1000] を割り当てる。1エレベーター分 1000 本＝1便。昇降 600 秒・昇降中の電力 2000 | S2 の契約「countPerLaunch ≤ スタック上限」。Task 12 で S2 のロケット建造時間と比べて確定する |
| P9 | 〔統合タスクへの下書き〕研究「軌道エレベーターの建設」の計算データ消費は S2 の plan の値（見つからなければ 200）、研究「反物質爆弾」に計算データ ×400 | S2 の裁定「計算データを消費する」。量は S2 待ち・S1 待ち |
| P10 | 起動時（`IBootInitializable.Load`）の検査は `SpaceElevatorOrbitalTableCheck.cs`（`Game.SpaceElevator/`）が担う。拠点のマスタが指すケーブル・積荷が軌道到着の表にあるかを S2 の `IsOrbitalPayload` で、衛星1機の成果が空の出口（`outputSlotCount`）に入りきるかを `PreviewArrivalOutputs` で確かめ、違えば例外で止める。積荷にケーブルが含まれないかはマスタだけで決まるので `SpaceElevatorBaseParamValidator`（P22）が見る。プールは起動時の検査をしない | ADR 0089。S2 のサービスに依存する検査だけを起動時に置き、マスタだけで決まる検査はロード時の validator に置く。起動時のマスタ整合検査サービスは前例の無い新しい形（配置検査の注目点4） |
| P11 | ワールドセーブは版 7→8（S2 が 6→7）。移行ステップはプール残量 0 を実値として足す（補完ではない）。版の番号は S1（6→7 と 7→8 を使う）・S2 と重なるので、後から master へマージする側が、先に入った版の続きへ FromVersion・CurrentVersion・クラス名とファイル名・テストの版・登録の並びを繰り上げる。7→8 は S2 だけが先に入った場合の値 | AGENTS.md「セーブ形式の例外」。旧版のワールドにはケーブルの打ち上げが無いので 0 が正しい値。繰り上げの規則は S1〜S3 のセッション間の取り決め（S2 のセッションからの連絡。ユーザー裁定ではない） |
| P12 | 撤去返却は `RemoveBlockProtocol.cs`（`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`）で全コンポーネントの `IGetRefundItemsInfo` を合算する（Task 8 Step 1） | 拠点は昇降中の衛星を返すため。既存が先頭1つだけを読むなら直す |
| P13 | 接続は専用プロトコル `va:connectSpaceElevator`（`ConnectSpaceElevatorProtocol.cs` を `PacketResponse/SpaceElevator/` に新設し `PacketResponseCreator.cs` に登録、`Server.Protocol.asmdef` に `Game.SpaceElevator` 参照を足す。クライアントは `BlockRequestApi.cs` に `ConnectSpaceElevator` を足す）で、結果は列挙（Connected / AlreadyConnected / InsufficientCable / NotSpaceElevatorBase）。拒否はサーバーログに出す。押せるか・不足の理由は、プールが判定して拠点の状態（`ReadyToConnect` / `InsufficientCable`）に載せたものを webui が表示する（P26） | ADR 0089「呼び出し側の線引き」。前例 `SetElectricToGearOutputModeProtocol`・`RemoveBlockProtocol` の拒否ログ |
| P14 | 拠点の UI は専用 view `views/SpaceElevatorInventory.tsx`（`BlockItemGrid`＋`SpaceElevatorSection`＋`ElectricNetworkSection`）を `registry/blockComponentRegistry.ts`・`registeredBlockTypes.ts` に登録する。`SectionStackView` には足さず、e2e の `intentionalGeneric` にも入れない | 接続ボタンはブロック固有の操作。前例 `views/ElectricToGearInventory.tsx`・`views/TrainPlatformInventory.tsx`。`SectionStackView.tsx:51-52`「固有UIだけをレジストリへ残す」、`intentionalGeneric` は移植待ちの許可リスト。配置検査 V2 で「SectionStackView に合成」から直した |
| P15 | 新しいファイルはディレクトリの 10 ファイル上限のためサブディレクトリ（`PacketResponse/SpaceElevator/`・`Actions/SpaceElevator/`・`Topics/BlockDetail/SpaceElevator/`・`StateProcessor/SpaceElevator/`・`Tests/CombinedTest/Server/PacketTest/SpaceElevator/`）に置き、名前空間は親のまま（前例 `Component/CleanRoom/`・`Topics/BlockDetail/Train/`、S2 も同じ）。`inventory.ts`（215 行）は S2 と同じ分け方（`schemas/train/`・`schemas/beltThroughput/`）で分割し、DI 登録は `SpaceElevatorServiceRegistration.cs` を新設する（`GameplayServiceRegistration.cs` は202行。前例 S2 の `OrbitServiceRegistration`） | AGENTS.md の規約。配置検査 r2 の V1〜V4 で直した |
| P16 | 〔統合タスクへの下書き〕録画テストは昇降の完了（600 秒）まで待たず、昇降機が上がること・進みが増えることまでを見る。到着後の成果・出口の空き待ちはユニットテストで見る。ケーブルの打ち上げはサーバーで `Arrive(cable, 1000)` を呼んで起こす | 録画の長さ。本番との差: ロケットでの打ち上げ経路は通さない（S2 の録画が担う） |
| P17 | Task 5 は S2 の `Game.Orbit`（`PreviewArrivalOutputs` を含む）と積荷表が push されるまで前景で 600 秒ずつ最大 12 回待ち、来なければ止めて報告する | S2 の API に依存するため。バックグラウンドのポーリングはしない |
| P18 | この plan には unityプレイ録画テストも EditModeInPlayingTest も入れない。ランタイム挙動（発電機の UI・拠点の UI・見た目・昇降）に触れるが、実ゲームで核融合炉と拠点を置いて開くにはマスタの中身が要り、それは統合タスクで入る。この PR はサーバーの CombinedTest（テスト mod で接続→昇降→成果→セーブ→撤去）・プロトコルテスト・wire 契約テスト・webui の vitest と e2e（mock host）で確かめる。録画シナリオの下書きは末尾の節の旧 Task 13 | ユーザー裁定 2026-10-23（S2 のセッション。提案「…master の中身と録画プレイテストは S1〜S3 がそろった後の統合タスクでまとめて行う」→ 回答「1 ok」。[[2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる]]）。録画テスト規定の例外はこの裁定による |
| P19 | 昇降機は打ち上げ後の保留を持たない。昇り始める前と頂上で渡す直前に `PreviewArrivalOutputs(衛星, 1)` が出口へ入りきるかを確かめ、入らなければ入口に残す／頂上で電力を使わず待つ。状態は `OutputBlocked` | S2 のユーザー裁定（S2 ブランチ `.decisions/2026-10-23-発射台は成果が出力口に入りきるときだけ建造と打ち上げを進める.md`、明示引用「発射台の出力口が満杯のとき、どこで待たせますか？」→ 選択「入りきる時だけ建造開始」）と、その裁定ファイルの「宇宙エレベーター拠点（S3）も同じ方式にする」。拠点への適用の形（2点の確認・頂上で待つ間は電力を使わない）は agent前提で、前例は機械の `IdleMachineProcessState.cs:49`・`ProcessingMachineProcessState.cs:118` |
| P20 | 拠点の出口は `outputSlotCount`（出荷 4・テスト mod 1）スロット。衛星1機の成果がレベル1のスタック上限で空の出口に入りきらないマスタは、起動時に `SpaceElevatorOrbitalTableCheck` が止める | 入りきらない成果は頂上で永遠に待つ（構造的に解消しない待ち）ため、いちばん早い段階で止める（AGENTS.md）。スロット数 4 は agent前提 |
| P21 | `VanillaSchema/blocks.yml` に blockType `SpaceElevatorBase` と case を足し、`_CompileRequester.cs`（`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`）の `dummyText` を上げて生成し直す。新しい case だけなので既存ブロックの JSON は変えない | edit-schema スキルの手順。拠点の振る舞いは既存の blockType では表せない（ケーブルプールへの接続・昇降機）ため新設 |
| P22 | 拠点のマスタ検証を `SpaceElevatorBaseParamValidator.cs`（`Core.Master/Validator/Block/`）に置き、`BlockMasterUtil.cs` の `Validate` に連結する。見るのは参照・数値・全拠点のケーブルが1種か。軌道到着の表との突き合わせは S2 のサービスが要るので起動時（P10） | 前例 `BlockMasterUtil.cs` の `BeltSpeedPerSecondValidator` の連結。AGENTS.md「必須の設定はいちばん早い段階で止める」 |
| P23 | 拠点の組み立てを `VanillaSpaceElevatorBaseTemplate.cs`（`Game.Block/Factory/BlockTemplate/SpaceElevator/`）に置き、`VanillaIBlockTemplates.cs` に登録する。供給の受け取りを昇降機より前に並べる | 前例 `VanillaElectricPumpTemplate`（電力受け取りと処理の順）・`VanillaChestTemplate`（インベントリとコネクタ） |
| P24 | 契約型（クライアントも読む型）を Interface 層に置く: 接続結果 `SpaceElevatorConnectResult` は `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator/SpaceElevatorConnectResult.cs`、状態詳細 `SpaceElevatorBaseStateDetail` と状態 `SpaceElevatorBaseStatus` は `moorestech_server/Assets/Scripts/Game.Block.Interface/State/SpaceElevator/`。クライアントの asmdef（`Client.Network`・`Client.WebUiHost`）に `Game.SpaceElevator` を足さない | agent前提（設計検査 Phase 2.6 強3。シミュレーター予測をもとに前提として宣言し、ユーザーの拒否なし。前例は S2 の判断記録 D27（契約型をインターフェース層に置く）と `Game.Block.Interface/State/` の既存の状態詳細） |
| P25 | webui の拠点詳細 `spaceElevator` は optional のまま（無ければ「拠点ではない／未着」。未着は DTO ビルダーが警告ログ）、状態は C# の列挙→camelCase 文字列→zod の enum（未対応の値は例外）、翻訳キーは `TranslationKey` 型で欠損を型とテストで止める | agent前提（設計検査 Phase 2.6 強1・強4・強5。前例 `electricToGear` の optional・`TrainPlatformDetailDtoBuilder.ToWireMode`・webui 全体の `t(キー)`。前提として宣言し、ユーザーの拒否なし） |
| P26 | 接続できるか（プールに1エレベーター分あるか）はサーバーのプールが判定する。プールは残量が変わるたび、および拠点の設置時に、各拠点へ `SetCablePoolAvailability(int poolAmount, bool hasEnoughCable)` で push し、拠点の状態の「未接続」を `ReadyToConnect` と `InsufficientCable` に分ける。webui は `status` を表示するだけで、ボタンの無効化と不足の文言も `status` から決める | ユーザー裁定 2026-10-23（質問「宇宙エレベーター拠点の『接続する』ボタンを押せるか（プールのケーブルが1エレベーター分あるか）を、どこで判定しますか？」→ 選択「サーバーが判定 (Recommended)」。[[2026-10-23-エレベーター拠点に接続できるかはサーバーのプールが判定し画面は表示するだけ]]）。ADR 0089 も同じ形に直した |
| P27 | 型検査の弱い指摘を4束とも直す: (1) 読込時と進みのガード（`SpaceElevatorClimberSaveRestorer` が残りを総時間に収める・運べない衛星を入口へ戻す、出口スロットの溢れをエラーログ）、(2) テストで前提を確かめる（設置・電力網の取得を `Assert.IsTrue`、成果の問い合わせで到着の通知が流れないテスト）、(3) 判定と導出の重複を消す（`SpaceElevatorOutputSimulation.TryStack` と昇降機のローカル関数 `ResultFitsOutlets`、`SpaceElevatorBaseMasters`、`IsCarriedPayload`）、(4) 出口待ちに入った tick に1回ログ | ユーザー裁定 2026-10-23（質問「型の検査で出た弱い指摘のうち、どれを直しますか？」→ 選択「読込時と進みのガード, テストで前提を確かめる, 判定と導出の重複を消す, 出口待ちのログ」。[[2026-10-23-エレベーター拠点の型検査の弱い指摘は4束とも直す]]） |
| P28 | 状態詳細は平らなフィールドのまま持つ（発電機の `IsBurning` と `CurrentFuelTotalTicks`、拠点の `Status` と `ClimbProgress` が同居し、動いていないときは 0） | agent前提（設計検査 Phase 2.6 強2。MessagePack の状態詳細は平らな形だけが前例: `PowerGeneratorStateDetail`・`CommonMachineBlockStateDetail`。ユーザーへ聞いたが「何の話？」の答えで、プレイヤーに見える差が無い内部の形なので前例どおりの前提で閉じた。裁定ではない） |
| P29 | moorestech_master の中身（液体・アイテム・ブロック・レシピ・研究・積荷表のグラフェンケーブル行とスタック表・軌道エレベーターの建設の変更・ビルドメニュー「宇宙」）・バランスのシミュレーション・unityプレイ録画テストは、S1〜S3 の実装がそろってから統合タスク（bd `moorestech-xyuaf.2`）で入れる。この PR は moorestech_master を変えない（S3 のスキーマ変更は新しい case だけで、出荷マスタに読み込みに要る変更が無い）。ピンは Task 5 で取り込む S2 の値。旧 Task 1・2・4・12・13 と、旧 Task 3・5・6・10 のマスタ部分は末尾の節へ下書きとして移した。スタックテーブルが空の7件は S2 の master PR が直すので S3 は触らない | ユーザー裁定 2026-10-23（S2 のセッション。原文「マスタはタスク全体の実装が終わった時に全体でまとめて入れるのが良いんじゃないの？どうせ他にもデッドロック的なタスクがあるんじゃないの？」→ 提案「S2 の PR はコード・スキーマ・テスト用データと、読み込みに必要な最小限の master 変更だけにし、master の中身と録画プレイテストは S1〜S3 がそろった後の統合タスクでまとめて行う」→ 回答「1 ok」。裁定は S1〜S3 の PR に効く。[[2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる]]（S2 ブランチで記録。Task 5 の取り込みで入る））。7件の分担は S2 からの連絡 2026-10-23 |
| P30 | 本PR外のリファクタ提案（plan のタスクには足さない）: (1) 出口へ入りきるかの模擬が3か所にある。既存 `moorestech_server/Assets/Scripts/Core.Inventory/InventoryInsertItem.cs`（`OpenableInventoryItemDataStoreService` の InsertionCheck/InsertItem）と `Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs`（CanStoreOutputs/TrySimulateOutputs）、新設 `SpaceElevatorOutputSimulation`。同じ役割（成果の並びが空きスロットに積めるか）なので共通化を提案する。(2) 発電機の燃焼判定と燃料の総時間が2つの燃料サービスにある。既存 `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/FuelGearGeneratorFuelService.cs`（`IsFuelActive`・`CurrentFuelTicks`）と、新設の `VanillaElectricGeneratorFuelService.IsBurning`・`CurrentFuelTotalTicks`。共通の型が無いので `PowerGeneratorStateDetail` に秒値の ctor を2本目として足している。燃料サービスの共通化を提案する | 設計検査 Phase 2.6 の第3バケツ（(1) は1回目、(2) は再検査）。Task 15 の PR 本文「本PR外のリファクタ提案」に載せる |
| P31 | レビュー依頼の注目点（前例が割れていて plan では直さないもの）: (a) 接続結果 `SpaceElevatorConnectResult` はワールドのサービス `SpaceElevatorConnectionService.Connect` が返す列挙を Interface 層に置き、プロトコルの応答にそのまま載せる（前例 `RideActionProtocol` の `RideActionResult`）。通信の失敗理由をプロトコルの中に持ち `Success＋FailureReason` で返す形（前例 `SetElectricToGearOutputModeProtocol`・S2 D24）もある。(b) プール残量は `SpaceElevatorCablePoolSaveData` で包んで保存する（S2 は単一値をトップレベルの nullable フィールドにした）。(c) 昇降機の部品 `SpaceElevatorClimberComponent` が拠点のインターフェース `ISpaceElevatorBase` を持ち、接続・残量・状態・セーブ・撤去返却まで受け持つ（S2 の同じ役割は `RocketLaunchPadProcessorComponent`）。(d) action の失敗コード（`already_connected` ほか）は `error_codes.json` の集合の外（既存 `ElectricToGearActions` と同じ。S2 は既存集合から選ぶ）。(e) プール残量を拠点ごとの状態詳細に写して既存の `ChangeBlockStateEventPacket` で送るので、残量が変わるたびに拠点 N 基ぶんの通知が出る（P26 の判定を拠点ごとに持つため） | agent前提（配置検査 r2 の注目点 N1・N4・N8・N11・N7。前例が割れているので violation ではない）。Task 15 の PR 本文の注目点に載せる |
| P32 | 型検査の再検査の弱4件: (A) 入口の問い合わせを `PeekSatellite` の3状態（`SatelliteInletState` の Empty/Carriable/NotCarried）にし、運べないアイテムが入口を塞いだ tick に1回だけ警告ログを出す。(B) ロード時のガードに、昇降中の衛星が2機以上（1機だけ昇らせ余りを入口へ）・残りが負/NaN/無限（最初から昇り直す）・衛星なしで残りあり（昇降していない状態で戻す）を足し、どれもエラーログを出す。(C) 起動時の検査はスタック上限レベル1で数え、実行時は今の上限で積むが、上限は研究で上がるだけなのでレベル1で入れば実行時も必ず入る。数え方は1本にせず理由を `CountSlotsAtStackLevelOne` のコメントに書く。(D) マスタ読み込み時の検証（Core.Master）はゲーム層の型を参照できないので、拠点マスタの列挙を検証側とゲーム側の2か所に持つ（1本にするとマスタ層に宇宙エレベーター専用の列挙を置くことになる）。あわせて昇降機とインベントリを 200 行未満に保つため、待ちの理由の選び方を `SpaceElevatorBaseStatusResolver` へ分ける | シミュレーター予測（確信高。前回2回とも推奨の束をすべて採用）→ 前提宣言としてユーザーへ報告し、違えば指摘を受ける |

### S1 待ち・S2 待ち

- この PR で待つのは S2 の `Game.Orbit`（`PreviewArrivalOutputs` を含む）と積荷表のスキーマの push だけ（Task 5 のゲート）
- 統合タスクで決めるもの（この PR では扱わない）: Tier9 研究のコスト（核融合炉・反物質爆弾・金属超精密加工・軌道エレベーターの建設の `consumeItems`）、機械のモジュールスロット数（重水素抽出装置・反物質製造装置・金属3Dプリンタは下書きで仮に 3）、レシピのチップ Lv（下書きはすべて ICチップLv1）、計算データの消費量（P9）、ロケット1機の建造時間（旧 Task 12）、GPU衛星 Lv1〜Lv5 の到着成果。研究「機械オーバークロック」は S1 が速度モジュール研究として作る（S3 は触らない）

### 進め方の裁定

- マスタの中身と録画テストは統合タスクへ（P29。S2 のセッションでのユーザー裁定で、S1〜S3 の PR に効く）
- ユーザー指示 2026-10-23 17:51「とりあえずまだ実装開始しないで」: plan の作成・検査・コミットまでで止め、Codex の実装セッションは起動しない。開始はユーザーの合図を待つ

---

## 統合タスクへ引き継ぐマスタの中身（この PR では実行しない）

統合タスク bd `moorestech-xyuaf.2` の入力。裁定 `.decisions/2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md`（S2 ブランチで記録。Task 5 の取り込みで入る）に従い、S3 の moorestech_master の中身・バランスのシミュレーション・unityプレイ録画テストをここへ下書きとして移した。ステップのチェックボックスは外してある。値（研究コスト・チップ Lv・モジュールスロット数・計算データ量・昇降の秒数と電力）は統合タスクで S1・S2 の中身とそろえて決め直す。マスタ worktree のパス（`$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame`）・ブランチ・ピンの手順は下書き時の想定で、統合タスクの plan が決め直す。

スタックテーブルが空の7件（グラフェンケーブル・グラフェンシート・使用済み銅板・高純度炭素・ロケット筐体素材・ロケット燃料・反物質爆弾）は S2 の master PR が Default（`9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa`）にする。下の「旧 Task 5 Step 3」はグラフェンケーブルだけを [1000] の表へ差し替える（S2 からの連絡 2026-10-23）。

#### 移した要件

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R1 | 液体「重水素」を作る | fluids.json に `重水素` があり、ローカライズ行がある |
| R2 | 重水素抽出装置（ElectricMachine）が水から重水素を作る。大電力・モジュールスロットあり | 水を入れて電力を与えると重水素タンクが増える（マスタ読込スモーク＋シミュレーション） |
| R3 | 核融合炉（ElectricGenerator）が重水素を燃やして大電力を出す。モジュールスロット・点火手順は無い | 重水素を入れると出力が出る。炉1基の差し引き電力（炉出力−必要台数の抽出装置の消費）がモジュールなしでガソリン発電機 3〜5 台分（シミュレーションで検査） |
| R5 | 反物質製造装置（ElectricMachine）が重水素と核融合炉級の電力で反物質を作る | マスタ読込スモーク・シミュレーションで反物質1機の消費電力が炉の差し引き電力（モジュールなし）の3倍以上 |
| R6 | 反物質爆弾を既存の電気汎用工作装置で作る | machineRecipes に 反物質＋ICチップLv1＋鋼鉄板＋コンピュータ → 反物質爆弾 がある |
| R7 | 終端研究（反物質爆弾を規定量消費する仮の研究）で終わる。クリア状態・エンディングは作らない | research.json に終端研究ノード（consume 反物質爆弾 10、clearedActions 空、前提 反物質爆弾）がある |
| R8 | グラフェンケーブルを既存機械の短い直線で作り、使用済み銅板は電気炉で銅板に戻す | 石炭→高純度炭素（電気炉）→グラフェンシート＋使用済み銅板（電気炉）→グラフェンケーブル（電気汎用工作装置）、使用済み銅板→銅板（電気炉）の4レシピがある |
| R9 | 金属3Dプリンタを ElectricMachine ブロックにし、ICチップ等からコンピュータ・制御モータを1工程で作る。研究「金属超精密加工」で解放 | 旧アイテム「金属3Dプリンタ」が消え、ブロックとレシピ2本があり、金属超精密加工が unlockBlock する |
| R10 | 研究「フィルター分岐器」を消し、金属超精密加工の前提を張り替える | ノードが無く、金属超精密加工の前提が `機械オーバークロック` だけになる。progression_check が詰みを増やさない |
| R11 | 研究「機械オーバークロック」は S1 が速度モジュール研究として作る。S3 は触らない | S3 の差分で当該ノードが変わっていない |
| R12 | 「軌道エレベーターの建設」から発射台・筐体・燃料の解放を外し（a）、計算データを消費し（b）、前提にロケット技術を足す（c） | research.json の当該ノードが (a)(b)(c) を満たす |
| R13 | グラフェンケーブルは軌道到着の積荷表に 1便1000本・成果なしで載り、スタック上限 1000 のテーブルを持つ | items.json の `orbitalPayloads` に行があり、グラフェンケーブルの `stackLevelTableGuid` が [1000] のテーブルを指す |
| R23 | 新しい値はシミュレーションで目標に合わせる（S3 の配分 5〜11 時間、全体 15〜25 時間は S1 の裁定） | `tools/balance/tier9_endgame_balance.py` が目標の範囲を検査して exit 0 |

#### マスタを変えるときの共通手順（旧 Global Constraints）

- マスタ JSON を変えたら moorestech_master で `python3 tools/research_sync/sync_machine_recipe_unlocks.py`（書き戻し）→ `--check`（exit 0）→ `python3 tools/research_sync/progression_check.py`（詰みを増やさない）→ `python3 scripts/check_shipped_data_contracts.py`（exit 0）
- S1 の担当値（Tier9 研究コスト・チップ Lv・モジュール値）は仮値で置き、判断記録に「S1 待ち」と書く。S2 の担当値（計算データ量・GPU衛星の成果・ロケット建造時間）は S2 のマスタブランチの値を読む
- Editor のマスタは既定で `../../moorestech_master`（共有の main clone）を読む。S3 のマスタで動かすときは、録画テストは `run-scenario.sh` の第3引数に `$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame/server_v8` を渡し、スモークは `MoorestechServerDIContainerOptions` に同パスを渡す。main clone の HEAD を動かさない（S1/S2 と共有）

#### 共通の GUID・参照表（マスタ）

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

#### 旧 Task 1: マスタ — 重水素・核融合炉・重水素抽出装置・反物質・反物質製造装置・反物質爆弾レシピ

**Files:**
- Create（git worktree）: `$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/fluids.json`, `items.json`, `blocks.json`, `machineRecipes.json`, `research.json`（moorestech_master worktree 内）
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Create: `moorestech_client/Library`（コピー。git 管理外）

**Interfaces:**
- Consumes: なし
- Produces: 重水素 01a167e9-21b2-…、反物質 01a167e9-21b5-…、重水素抽出装置 01a167e9-21b8-…、反物質製造装置 01a167e9-21bb-… と各レシピ（Task 4 のシミュレーションが name で引く）

- **Step 1: 作業場所を用意する**

```bash
pwd   # $HOME/hermes-agent/data/worktrees/moorestech/tier9-fusion-elevator-endgame
git -C $HOME/hermes-agent/data/repos/moorestech_master fetch origin
git -C $HOME/hermes-agent/data/repos/moorestech_master worktree add -b feat/tier9-fusion-elevator-endgame $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame origin/master
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame log --oneline -1   # b2149a0 tire9基板 以降であること
moores-wt status   # Editor pid が空の worktree を Library のコピー元に選ぶ（候補: pr-apply）
cp -Rc $HOME/hermes-agent/data/worktrees/moorestech/pr-apply/moorestech_client/Library moorestech_client/Library
```
その後 uloop-launch スキルで `./moorestech_client` の Editor を起動し、`uloop compile --project-path ./moorestech_client` が ErrorCount 0 になることを確かめる（Editor の PID を控える）。

- **Step 2: 液体・アイテムを足す**

以下 `M=$HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame/server_v8/mods/moorestechAlphaMod_8/master`。JSON は既存の整形（インデント・キー順）に合わせ、Python の `json.load`→要素追加→`json.dump(..., ensure_ascii=False, indent=<既存と同じ>)` で書く。

`$M/fluids.json` の `data` に追加:
```json
{"name": "重水素", "fluidGuid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "color": "#7FD8FF"}
```
`$M/items.json` の `data` に追加（反物質爆弾 sortPriority 1610 の直前）:
```json
{"itemGuid": "01a167e9-21b5-7479-9060-995505f43137", "imagePath": "", "name": "反物質", "sortPriority": 1605, "initialUnlocked": false, "recipeViewType": "IsUnlocked", "addressablePaths": {"handGrabModel": "", "entityModel": ""}, "stackLevelTableGuid": "9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa"}
```

- **Step 3: ブロックを足す・仮エントリを置き換える**

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

- **Step 4: レシピを足す**

`$M/machineRecipes.json` の `data` に追加（機械の入力スロットの上限はスタック上限 100 なので、1スロットの投入数は 100 以下に保つ）:
```json
{"machineRecipeGuid": "01a167e9-21dc-7eb1-a240-8345ed8fbd40", "blockGuid": "01a167e9-21b8-7db1-a289-99cdf7490fe9", "time": 8, "inputItems": [], "inputFluids": [{"fluidGuid": "9eae6979-d56a-4991-9107-b8161acec430", "amount": 100}], "outputItems": [], "outputFluids": [{"fluidGuid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "amount": 10}], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21df-7b2a-81e3-04be55cb89b0", "blockGuid": "01a167e9-21bb-76dd-9a83-0a249aa9f1fc", "time": 30, "inputItems": [], "inputFluids": [{"fluidGuid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "amount": 50}], "outputItems": [{"itemGuid": "01a167e9-21b5-7479-9060-995505f43137", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21e2-7c4e-91be-8ad747466bde", "blockGuid": "019e1225-f49b-7283-8835-428d903852f1", "time": 60, "inputItems": [{"itemGuid": "01a167e9-21b5-7479-9060-995505f43137", "count": 20}, {"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 50}, {"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 100}, {"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 20}], "inputFluids": [], "outputItems": [{"itemGuid": "01a162ab-07ba-73c1-9dd6-38e1e867663b", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
```

- **Step 5: 研究の解放物と仮コストを足す**

`$M/research.json`:
- 核融合炉（019e3af0-…）: `consumeItems` = `[{ICチップLv1, 600}, {鋼鉄板, 3000}, {コンピュータ, 600}, {制御モータ, 600}]`、`clearedActions` の unlockBlock を `["01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a167e9-21b8-7db1-a289-99cdf7490fe9"]` にする
- 反物質爆弾（019e3aef-…）: `consumeItems` = `[{ICチップLv1, 1000}, {鋼鉄板, 4000}, {コンピュータ, 1000}]`、`clearedActions` を `unlockItemRecipeView ["01a167e9-21b5-7479-9060-995505f43137", "01a162ab-07ba-73c1-9dd6-38e1e867663b"]` と `unlockBlock ["01a167e9-21bb-76dd-9a83-0a249aa9f1fc"]` にする
（`consumeItems` の要素は `{"itemGuid": ..., "itemCount": ...}`。値は agent前提・S1 待ち）

- **Step 6: ローカライズ行を足す**

`.../moorestechAlphaMod_8/localization/localization.csv`（ヘッダ `key,Source,english,japanese,german,korean`）。既存行が無いものだけ足す（`grep -n "01a162f1" localization.csv` で確認）:
```
fluid.01a167e9-21b2-7bb0-b6a3-0f17d349704e.name,重水素,Deuterium,重水素,Deuterium,중수소
item.01a167e9-21b5-7479-9060-995505f43137.name,反物質,Antimatter,反物質,Antimaterie,반물질
block.01a162f1-ab04-76e2-94cf-dc713edeb63b.name,核融合炉,Fusion Reactor,核融合炉,Fusionsreaktor,핵융합로
block.01a167e9-21b8-7db1-a289-99cdf7490fe9.name,重水素抽出装置,Deuterium Extractor,重水素抽出装置,Deuteriumextraktor,중수소 추출기
block.01a167e9-21bb-76dd-9a83-0a249aa9f1fc.name,反物質製造装置,Antimatter Fabricator,反物質製造装置,Antimateriefabrik,반물질 제조기
```

- **Step 7: 整合ツールとマスタ読込スモークを通す**

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

- **Step 8: コミットして push する**

```bash
cd $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame
git add server_v8/mods/moorestechAlphaMod_8
git commit -m "data(v8): 重水素・核融合炉・重水素抽出装置・反物質・反物質製造装置と爆弾レシピを足す"
git push -u origin feat/tier9-fusion-elevator-endgame
```

#### 旧 Task 2: マスタ — グラフェン工程・金属3Dプリンタ・研究ツリー整理・最終研究

**Files:**
- Modify: `$M/items.json`, `$M/blocks.json`, `$M/machineRecipes.json`, `$M/research.json`, `.../localization/localization.csv`（moorestech_master worktree 内）

**Interfaces:**
- Consumes: Task 1 のマスタ
- Produces: 金属3Dプリンタ（ブロック）01a167e9-21be-…、最終研究 01a167e9-21f6-…、グラフェン4レシピ（Task 4 のシミュレーションが name で引く）

- **Step 1: 金属3Dプリンタをアイテムからブロックへ移す**

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

- **Step 2: グラフェン工程と3Dプリンタのレシピを足す**

`$M/machineRecipes.json` に追加:
```json
{"machineRecipeGuid": "01a167e9-21e5-7b43-9414-15365405623b", "blockGuid": "019e0a68-3705-75f7-a040-50fab3c7d81e", "time": 3, "inputItems": [{"itemGuid": "f882df45-5651-4aec-b871-a9f885a5f75f", "count": 2}], "inputFluids": [], "outputItems": [{"itemGuid": "01a16297-a7f5-759b-91ba-be4021ca3bf2", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21e8-76b9-bf3f-b462360e3fa9", "blockGuid": "019e0a68-3705-75f7-a040-50fab3c7d81e", "time": 6, "inputItems": [{"itemGuid": "01a16297-a7f5-759b-91ba-be4021ca3bf2", "count": 2}, {"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 1}], "inputFluids": [], "outputItems": [{"itemGuid": "01a16297-4724-73b9-82c3-2e77120be77d", "count": 1, "percent": 1}, {"itemGuid": "01a16297-6665-765d-8e19-96d357394c3e", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21eb-7e67-b6ee-1d6503d8af84", "blockGuid": "019e1225-f49b-7283-8835-428d903852f1", "time": 6, "inputItems": [{"itemGuid": "01a16297-4724-73b9-82c3-2e77120be77d", "count": 1}], "inputFluids": [], "outputItems": [{"itemGuid": "01a16290-f793-748b-a0b0-91d44be0d4ab", "count": 5, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21ee-7339-b7b6-351ad1775730", "blockGuid": "019e0a68-3705-75f7-a040-50fab3c7d81e", "time": 3, "inputItems": [{"itemGuid": "01a16297-6665-765d-8e19-96d357394c3e", "count": 1}], "inputFluids": [], "outputItems": [{"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21f0-7d5b-8ddc-18a841c01637", "blockGuid": "01a167e9-21be-72cd-ae04-e5bbe12b95c2", "time": 4, "inputItems": [{"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 1}, {"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 5}, {"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 2}], "inputFluids": [], "outputItems": [{"itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
{"machineRecipeGuid": "01a167e9-21f3-73f5-9418-35a676b8fa3f", "blockGuid": "01a167e9-21be-72cd-ae04-e5bbe12b95c2", "time": 5, "inputItems": [{"itemGuid": "f6dd7835-0845-4877-87d0-b719d037ae7d", "count": 1}, {"itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 4}, {"itemGuid": "019e0a92-1907-707a-8625-630d86a076ee", "count": 4}], "inputFluids": [], "outputItems": [{"itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 1, "percent": 1}], "outputFluids": [], "initialUnlocked": false}
```

- **Step 3: 研究ツリーを整理し最終研究を足す**

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

- **Step 4: ローカライズを直す**

localization.csv: フィルター分岐器ノード（`research.019e3aab-32c6-7166-a184-d761153c2498.*`）と旧アイテム（`item.019e3af5-e750-71ae-ab09-2b98f3db9913.name`）の行を消し、次を足す（金属超精密加工・軌道エレベーターの建設の description 行は新しい説明文へ書き換える。英独韓も対応する訳に）:
```
block.01a167e9-21be-72cd-ae04-e5bbe12b95c2.name,金属3Dプリンタ,Metal 3D Printer,金属3Dプリンタ,Metall-3D-Drucker,금속 3D 프린터
research.01a167e9-21f6-7d26-a990-e7d6d78750d0.name,最終研究,Final Research,最終研究,Abschlussforschung,최종 연구
research.01a167e9-21f6-7d26-a990-e7d6d78750d0.description,反物質爆弾を規定量そろえたことを記録する仮の研究。現段階のゲームの終点（解放物なし）,"A provisional research that records stockpiling the required antimatter bombs. The current end of the game (unlocks nothing)",反物質爆弾を規定量そろえたことを記録する仮の研究。現段階のゲームの終点（解放物なし）,"Vorläufige Forschung, die das Bereitstellen der nötigen Antimateriebomben festhält. Derzeitiges Spielende (schaltet nichts frei)","필요한 반물질 폭탄을 모은 것을 기록하는 임시 연구. 현재 게임의 끝 (해금 없음)"
```

- **Step 5: 整合ツールを通し、コミットして push する**

Task 1 Step 7 と同じ4コマンドとスモーク（金属3Dプリンタのブロックが引けること）。Expected: グラフェンのレシピ4本が 軌道エレベーターの建設 に、3Dプリンタのレシピ2本が 金属超精密加工 に割り当たる。最終研究がツリーから到達可能。
```bash
git add server_v8/mods/moorestechAlphaMod_8
git commit -m "data(v8): グラフェン工程・金属3Dプリンタ・最終研究を足しフィルター分岐器ノードを消す"
git push
```

#### 旧 Task 3 Step 4 の e2e（核融合炉が開けるようになったら）

核融合炉が `openable: true` になると、e2e の `blockRegistryCoverage.spec.ts`（blocks.json の `[blockType, openable]` を `sort -u` した fixture のうち openable の型が、レジストリか意図的 Generic に入っているかを見る）が ElectricGenerator で落ちる。発電機は専用 view を作らず汎用の `SectionStackView` で出すので、意図的 Generic に入れる:
- `e2e/fixtures/v8-block-ui-registry.json` の `{ "blockType": "ElectricGenerator", "openable": false },` の次の行に `{ "blockType": "ElectricGenerator", "openable": true },` を足す（他の発電機は openable false のままなので両方の行が残る）
- `e2e/tests/block/blockRegistryCoverage.spec.ts` の `intentionalGeneric` の `"ElectricMachine",` の前に `"ElectricGenerator",` を足す

#### 旧 Task 4: 数値シミュレーション（核融合・反物質・グラフェン）

**Files:**
- Create: `tools/balance/tier9_endgame_balance.py`（moorestech_master worktree 内）

**Interfaces:**
- Consumes: Task 1・2 のマスタ（名前で引く: `核融合炉`, `重水素抽出装置`, `反物質製造装置`, `ガソリンエンジン発電機`, `電気炉`, `電気汎用工作装置`, `重水素`, `反物質`, `反物質爆弾`, `グラフェンケーブル`, `グラフェンシート`, `高純度炭素`, `最終研究`）
- Produces: `python3 tools/balance/tier9_endgame_balance.py` が目標外で exit 1（Task 12 がエレベーターの検査を足す）

- **Step 1: スクリプトを書く**

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

- **Step 2: 実行して目標に合わせる**

Run: `python3 tools/balance/tier9_endgame_balance.py`（moorestech_master worktree で）
Expected: 全行 OK、exit 0。NG が出たら blocks.json / machineRecipes.json の値（requiredPower・time・amount）を直して再実行し、直した値と理由を plan の『判断記録（ADR）』へ追記する。

- **Step 3: コミットして push する**

```bash
git add tools/balance/tier9_endgame_balance.py server_v8/mods/moorestechAlphaMod_8
git commit -m "tools: Tier9 終盤の数値を検査するシミュレーションを足す"
git push
```

#### 旧 Task 5 Step 3〜5（S2 を取り込んだマスタへ）

- **Step 3: グラフェンケーブルを積荷表に載せ、スタック上限 1000 の表を割り当てる**

`$M/items.json`（S2 取り込み後に `orbitalPayloads` のキー名を S2 の items.yml で確かめる）:
- `itemStackLevelTables` に `{"tableGuid": "01a167e9-21f9-70a8-be5e-08c2ba6895c9", "name": "グラフェンケーブル", "stackCounts": [1000]}` を足す
- グラフェンケーブル（01a16290-…）の `stackLevelTableGuid` を `01a167e9-21f9-70a8-be5e-08c2ba6895c9` にする
- `orbitalPayloads` に `{"payloadItemGuid": "01a16290-f793-748b-a0b0-91d44be0d4ab", "countPerLaunch": 1000, "arrivalOutputs": []}` を足す

- **Step 4: 研究「軌道エレベーターの建設」と「反物質爆弾」を S2 に合わせる**

`$M/research.json`:
- 軌道エレベーターの建設（01a16291-…）: (a) `unlockBlock` から ロケット発射台 01a162b0-7769-7419-af24-bb35528b93d2 を、`unlockItemRecipeView` から ロケット筐体素材・ロケット燃料（GUID は items.json を name で引く）を外す。(b) `consumeItems` に `{"itemGuid": "01a167cf-af88-74d3-9cd6-7d61b71052bb", "itemCount": <S2 の計算データ仮値>}` を足す（S2 の plan `docs/superpowers/plans/` の計算データ量の表を読む。見つからなければ 200 とし、判断記録に「S2 の値待ち」と書く）。(c) `prevResearchNodeGuids` に `01a167cf-af9d-7f31-817c-3069b4f6e499` を足す
- 反物質爆弾（019e3aef-…）: `consumeItems` に 計算データ ×400（agent前提・S1/S2 待ち）を足す

- **Step 5: 整合ツール・シミュレーション・スモークを通して push する**

Task 1 Step 7 の4コマンド＋`python3 tools/balance/tier9_endgame_balance.py`＋スモーク（`MasterHolder.ItemMaster` からグラフェンケーブルのスタック上限が 1000 で引けること、S2 の積荷表の検証がマスタ読込で通ること）。
```bash
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame add server_v8
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame commit -m "data(v8): グラフェンケーブルを積荷表に載せ軌道エレベーターの建設をロケット技術の後ろにする"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame push
git push   # moorestech 側のマージコミット
```

#### 旧 Task 6 Step 5〜7 のマスタ部分

- **Step 5: 出荷マスタの拠点を本物にする**

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
    "outputSlotCount": 4,
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
（1エレベーター分 1000 本＝ケーブル1便。昇降 600 秒・電力 2000 は agent前提で Task 12 が S2 のロケット建造時間と比べて確定する。出口スロット 4 は agent前提で、GPU衛星1機の成果が空の出口に入りきるかを Task 7 の起動時検査が確かめる）
`localization.csv` に `block.01a162aa-cec0-738c-93c5-f58adc87fa7f.name,宇宙エレベーター拠点,Space Elevator Base,宇宙エレベーター拠点,Weltraumaufzug-Basis,우주 엘리베이터 기지` が無ければ足す。

旧 Step 6 のマスタ側の確かめ: マスタ worktree で上の共通手順の4コマンドが exit 0。スモーク（旧 Task 1 Step 8 の dynamic code を `MasterHolder.BlockMaster.GetBlockMaster(Guid.Parse("01a162aa-cec0-738c-93c5-f58adc87fa7f")).BlockType` を出す形にして実行）で `SpaceElevatorBase` が出る。

```bash
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame add server_v8
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame commit -m "data(v8): 宇宙エレベーター拠点を SpaceElevatorBase にする"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame push
```

#### 旧 Task 10 Step 6 の e2e fixture（拠点が出荷マスタに入ったら）

`e2e/fixtures/v8-block-ui-registry.json` に `{ "blockType": "SpaceElevatorBase", "openable": true }` をアルファベット順の位置に足す（旧 Task 3 Step 4 で足した `ElectricGenerator` openable true の行と合わせ、blocks.json から再生成した結果と一致させる）。拠点は専用 view をレジストリに登録したので、`blockRegistryCoverage.spec.ts` の `intentionalGeneric` には足さない。

#### 旧 Task 12: シミュレーションにエレベーターを足して値を確定する

**Files:**
- Modify: `tools/balance/tier9_endgame_balance.py`（moorestech_master worktree）
- Modify（値を直す場合）: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`、`items.json`

**Interfaces:**
- Consumes: Task 4 のスクリプト（`blocks`, `items`, `net_power`, `check`, `failures`, `load`）、Task 5・6 のマスタ、S2 の発射台の建造時間
- Produces: エレベーターの検査行（exit 0 で値が確定）

- **Step 1: 検査を足す**

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

- **Step 2: 実行して値を確定する**

Run: `python3 tools/balance/tier9_endgame_balance.py`
Expected: 全行 OK、exit 0。NG なら `climbSeconds`・`requiredPower` を直して再実行し、直した値と出力の行を判断記録に書く。値を変えたらテスト mod は変えない（テスト mod の値はテスト用で独立）。

- **Step 3: コミットして push する**

```bash
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame add tools/balance server_v8
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame commit -m "tools: エレベーターの数値をシミュレーションで確定する"
git -C $HOME/hermes-agent/data/worktrees/moorestech_master/tier9-fusion-elevator-endgame push
```

#### 旧 Task 13: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/tier9-endgame-fusion-and-elevator.cs`

**Interfaces:**
- Consumes: Task 1〜12 のすべて（出荷マスタは S3 のマスタ worktree）
- Produces: 録画・スクショ・`result.json`（PR 本文に貼る）

- **Step 1: シナリオを書く**

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
using Game.Block.Interface.State.SpaceElevator;
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
    await p.Until(() => Detail(elevator).Status == SpaceElevatorBaseStatus.InsufficientCable, 10f, "サーバーがケーブル不足と判定した");
    await p.UntilWebUiElement("space-elevator-status", 15f);
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

- **Step 2: 調査実行（録画なし）で通す**

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

- **Step 3: 録画ありで最終実行し、目で確かめる**

`Record = true` のまま同じコマンドで実行する。録画から `ffmpeg -i <動画> -vf fps=1 <scratch>/frames/%03d.png` でフレームを切り出し、(1) 炉のパネルに発電の稼働表示と燃料タンク、(2) 拠点パネルのケーブル不足の理由、(3) 接続後にケーブルが現れる、(4) 昇降機がケーブルを昇る、を目で確かめる。確かめたフレームのパスを判断記録に書く。動画は `share-put <動画> -n tier9-endgame-fusion-and-elevator` で置き、出た URL を PR 本文に貼る。

- **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/tier9-endgame-fusion-and-elevator.cs
git commit -m "test(playtest): 核融合炉と宇宙エレベーターの録画シナリオを足す"
```

