# Tier9 計算データループ Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ロケット発射台でGPU衛星を打ち上げて計算データを得て、計算データで研究を進められるTier9のループを遊べる状態にする。

**Architecture:** 軌道に届いた後の処理（打ち上げ成果の決定と到着通知）を新設アセンブリ `Game.Orbit` の `IOrbitalArrivalService` に集め、発射台（`Game.Block`）とS3のエレベーター拠点はそれを呼ぶだけにする（ADR 0088）。発射台は資材スロット・積荷スロット・出力スロットを持つブロックで、電力と時間でロケットを建造し、初打ち上げだけプレイヤーのボタン操作（Web UI → プロトコル）を待つ。初打ち上げ済みかはワールド単位の状態 `IRocketLaunchHistory` としてセーブに入れる（版6→7）。

**Tech Stack:** Unity C#（moorestech_server/client）、UniRx、MessagePack、Newtonsoft.Json、マスタYAMLスキーマ＋SourceGenerator、React/TypeScript（moorestech_web/webui、zod、vitest）、moorestech_master（JSON）。

## Requirements

1. **R1 発射台ブロック:** blockType `RocketLaunchPad` のブロックが、資材スロット（`launchMaterials` 1件につき1スロット・そのアイテム専用）、積荷スロット（積荷表1行につき1スロット・その積荷専用）、出力スロット（`outputSlotCount` 個・取り出し専用）を持つ。受け入れ: ベルトとプレイヤー操作のどちらでも、合わないアイテムは入らず、出力スロットへは置けない。
2. **R2 建造:** 全資材が1便ぶん揃い、どれか1種の積荷が1便ぶん揃い、成果の保留が空のとき建造を始め、資材と積荷をその時点で取り込む。建造は `buildSeconds` を電力の供給率に応じて進め、電力ゼロでは止まって理由を出す。受け入れ: 供給率0.5では建造時間が約2倍になる。
3. **R3 初打ち上げの手動操作:** ワールドで初打ち上げが済むまでは、揃った台は「建造の合図待ち」で止まり、建造ボタンで建造を始め、建造後は「発射の合図待ち」で止まり、発射ボタンで打ち上げる。初打ち上げ後は全ての台（合図待ちで止まっていた台を含む）が自動で建造・打ち上げする。受け入れ: 別の台の初打ち上げで、発射の合図待ちの台が自動で打ち上がる。
4. **R4 積荷の選び方:** 1便に積むのは1種類。揃った積荷が複数あれば前回の次の積荷から順に選ぶ。受け入れ: GPU衛星とグラフェンケーブルの両方が常に揃っている台で、交互に打ち上がる。
5. **R5 軌道到着と成果:** 打ち上げ開始の時点で軌道到着とし、`IOrbitalArrivalService.Arrive` が積荷表から成果を返し、`OnOrbitalArrival` を流す。成果は発射台の保留に入り、出力スロットへ流れ、出力スロットからは接続先（ベルト）へ流れる。保留が残る間は次の建造を始めず理由を出す。受け入れ: 出力が満杯のとき次の便が建造されず、取り出すと再開する。
6. **R6 積荷表:** items.yml のトップレベル配列 `orbitalPayloads`（積荷アイテム・1便の個数・成果の並び）を必須で追加し、全 items.json を更新する。GPU衛星 Lv1〜Lv5 の行を moorestech_master に入れる。受け入れ: 積荷表に無いアイテムは積荷スロットを持たない。
7. **R7 マスタ検証:** 積荷表と発射台パラメータの不整合（存在しないアイテム・重複・1便の個数がスタック上限を超える・資材と積荷が同じアイテム・秒数や電力が0以下・発射台があるのに積荷表が空）はマスタ読込時にエラーで止まる。
8. **R8 セーブ:** 発射台の段階・残りtick・積んだロケットの中身・次に探す積荷・入れ口と出力口の中身・保留をブロックのセーブに入れ、`rocketLaunchHistory.hasLaunchedOnce` をワールドのセーブに入れる。版を6→7に上げ、旧版セーブに `hasLaunchedOnce:false` を足す移行ステップを同梱する。受け入れ: 建造中・発射の合図待ち・保留ありでセーブ→ロードして同じ状態から続く。
9. **R9 撤去返却:** 撤去時、スロットの中身に加えて、建造中・発射の合図待ちのロケットに積んだ資材と積荷、保留中の成果を返却する。打ち上げ中のロケットの中身は消費済みなので返さない。
10. **R10 見た目:** 既存の `Rocket.prefab`（発射台 base とロケット本体 body）を使う。base は常に表示、body は建造完了（発射の合図待ち）から表示し、打ち上げで `launchSeconds` かけて上昇し、終わると消える。演出・音は付けない。
11. **R11 Web UI:** 発射台を開くと、資材・積荷・出力のスロット群に見出しを付けて並べ、段階と待ち理由の文、建造・打ち上げの進み具合のゲージ、初打ち上げ前だけ「建造」「発射」ボタンを出す。
12. **R12 研究とレシピ（moorestech_master）:** 研究「ロケット技術」（前提: 半導体製造）が発射台・ロケット筐体素材・ロケット燃料のレシピを解放する。ロケット筐体素材（電気汎用工作装置）とロケット燃料（化学プラント）のレシピを入れる。計算データ・GPU衛星 Lv1〜Lv5 のアイテムと levelFamily を入れる。
13. **R13 S1待ちの分離:** GPU衛星のレシピ（GPU LvN → GPU衛星 LvN）と「半導体のアップグレード研究」（計算データでモジュールの上位を解放）は S1 の GPU アイテムとレベル継承レシピの仕組みが origin/master にあるときだけ入れる。無ければ入れず、bd とPR本文に「S1待ち」と書く。
14. **R14 S3への口:** `Game.Orbit` の API（S3 へ送付済み）を変えない。Game.Orbit 一式と積荷表（スキーマ・全JSON・GPU衛星の行）を最初の2タスクで作って push する。S3 はグラフェンケーブルの行を積荷表へ足し、「軌道エレベーターの建設」の研究変更（発射台・筐体素材・燃料の解放を外し、計算データを消費に足す）を自分で行う。
15. **R15 撤去返却の取りこぼし:** `RemoveBlockProtocol` は `IGetRefundItemsInfo` を先頭1件しか読まないため、電線（`ElectricWireConnectorComponent` も `IGetRefundItemsInfo`）と発射台の進行を併せ持つ発射台では片方の返却が消える。全件を合算する。受け入れ: 電線つきの発射台を建造中に撤去すると、積んだ資材と積荷がプレイヤーのインベントリへ戻る。

**やらないこと:**
- S1 の領域（クリーンルーム・チップ抽選・モジュール・GPU のアイテム定義とレベルの効き方）のコードとマスタを変えない。
- 宇宙エレベーター拠点・グラフェンケーブルの行・軌道エレベーターの建設の研究ノードは触らない（S3）。
- 打ち上げの演出（炎・煙・音）、カメラ演出は作らない。
- UI Toolkit への移行はしない（新UIは webui に作る）。

## Global Constraints

- 1ファイル200行以下。新規ファイルを置くディレクトリのコードファイルは10個まで（既に10個を超えているディレクトリには足さず、サブディレクトリを作る）。
- partial・`Func<>`・デフォルト引数・try-catch（外部境界以外）・単純な getter/setter プロパティは使わない。`{ get; private set; }` は可。値の設定は `SetHoge`。
- コメントは日本語1行＋英語1行の2行セットを主要処理ごとに入れる。
- サーバーの経過時間は `GameUpdater` のtickだけで測る。秒の換算は `GameUpdater.SecondsToTicks`。
- fail-closed で拒否・保留する経路は理由を `Debug.LogWarning` 以上で残す。意図して黙って続行する行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く。
- イベントは UniRx（`Subject<T>` を private、`IObservable<T>` を公開）。
- `.meta` を手で作らない。Prefab・シーンは `uloop execute-dynamic-code` 経由でだけ変える。Library を消さない。
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client` を通す。新規 `.cs` を足した直後は Unity の再読込が要ることがある（`uloop` が古い結果を返したら `docs/development/uloop-troubleshooting.md`）。
- YAMLスキーマを触る前に `edit-schema` スキルを読む。`optional: true` を足さない。プロトコルを作る前に `creating-server-protocol` スキル、セーブ移行を書く前に `moorestech-save-migration` スキル、テストを書く前に `creating-server-tests` スキル、webui を触る前に `ref-webui-design` スキルを読む。
- moorestech_master の作業場所は、このworktreeのルートから見て `../../moorestech_master/tier9-compute-data-loop`（ブランチ `feat/tier9-compute-data-loop`）。以下のコマンドはどれもこのworktreeのルートで実行し、master 側は `MASTER_WT=../../moorestech_master/tier9-compute-data-loop` を置いて `git -C "$MASTER_WT"` で操作する。`../moorestech_master` は全worktree共有のsymlinkなので、そこで checkout・編集しない。マスタを変えたら master worktree でコミット・push し、本repoの `.moorestech-external-revisions.json` の `moorestech_master.commitHash` をそのコミットへ向ける。`moores-wt status` はこの master worktree を「UNUSED master pin」と表示するが、S2 の作業場所なので `moores-wt rm --prune-master` 等で消さない。
- 名前（S3へ送付済み・変更不可）: asmdef/名前空間 `Game.Orbit`、`IOrbitalArrivalService`（`IObservable<OrbitalArrival> OnOrbitalArrival { get; }`・`bool IsOrbitalPayload(ItemId itemId)`・`IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)`（S3 へ 2026-10-23 連絡済み）・`IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)`）、`OrbitalArrival`（`public readonly ItemId PayloadItemId; public readonly int PayloadCount;`）、取得は `ServerContext.GetService<IOrbitalArrivalService>()`。
- GUID（S3へ送付済み・変更不可）: 研究「ロケット技術」`01a167cf-af9d-7f31-817c-3069b4f6e499`／計算データ `01a167cf-af88-74d3-9cd6-7d61b71052bb`／GPU衛星 Lv1 `01a167cf-af8b-7894-8de6-593ee30289c0`・Lv2 `01a167cf-af8e-7e95-b299-09eb712bee12`・Lv3 `01a167cf-af91-7f62-93d1-e499b06f2398`・Lv4 `01a167cf-af94-7e25-9654-825374c3923b`・Lv5 `01a167cf-af97-7f67-bf53-7833b8d17a15`／GPU衛星 levelFamily `01a167cf-af9a-78e5-9203-1c18ce94cdc6`／ロケット筐体素材レシピ `01a167cf-afa0-7add-8407-eec71456f4f3`／ロケット燃料レシピ `01a167cf-afa3-7c4f-82b7-c0d4f6d513c6`。
- S1 の GPU（S1 からの連絡 2026-10-23。S1 の master に入る値）: levelFamily `01a167ed-2700-7371-9f95-168966ff60bc`、Lv1 `01a167ed-2703-7014-98e4-11bd15f38571`・Lv2 `01a167ed-2706-7bfd-8361-a430dd96a569`・Lv3 `01a167ed-2709-74f0-a5c6-25026c7479d9`・Lv4 `01a167ed-270c-7873-a83a-5cf02ead6277`・Lv5 `01a167ed-270f-7276-8700-469ab54a26a2`。継承レシピの書式と上限を上げる研究アクション名は S1 の plan 確定後に届く（Task 10）。
- 既存マスタGUID: ロケット発射台 `01a162b0-7769-7419-af24-bb35528b93d2`、ロケット筐体素材 `01a162a4-9334-7286-a8ba-7daf9c0f407a`、ロケット燃料 `01a162a8-e435-73d0-bddb-08533819b80e`、コンピュータ `019f0ca4-a544-723e-8ff2-feb15f99c84e`、鋼鉄板 `019e3a98-cfe5-73eb-bf6a-a564400bdb4f`、プラスチック `019e3aaa-af6f-7527-8689-01f4ebd65426`、固形燃料 `019eded2-f06b-70fd-b8aa-960690cb2b0f`、ガソリン（流体）`019eded2-7454-76da-8943-84c4e7a59179`、化学プラント `019ee880-7f68-705e-b846-7b05036e2ca8`、電気汎用工作装置 `019e1225-f49b-7283-8835-428d903852f1`、研究「半導体製造」`019e3ad9-46e2-73fd-9555-8c1d0245d26d`、スタックテーブル Default `9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa`。
- セーブ版: S2 が 6→7（`SaveMigrationStepV6ToV7`）。S3 は 7→8 を使う。後からマージする側が番号を詰め直す。
- moorestech は PR 作成までで止める（マージしない）。moorestech_master も PR を作る。
- S3 は本ブランチ（moorestech・moorestech_master とも `feat/tier9-compute-data-loop`）を merge で取り込んでエレベーター拠点を作る。Task 2 の最後で両repoを push する（S3 の着手条件）。以後どちらのブランチも force-push・rebase・履歴の書き換えをしない。
- ビルドメニュー: 「生産」の下にサブカテゴリ「宇宙」（`subCategoryGuid: d2000000-0000-4000-8000-000200000005`、`entrySource: blocks`）を S2 が作る（S3 合意。S3 は宇宙エレベーター拠点を同じサブカテゴリへ足す）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| ファイル | 責務 |
|---|---|
| `VanillaSchema/items.yml` | `orbitalPayloads`（積荷表）を追加 |
| `VanillaSchema/blocks.yml` | blockType `RocketLaunchPad` と `RocketLaunchPadBlockParam` を追加 |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs` | 積荷表の検証（新規） |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs` | 発射台パラメータの検証（新規） |
| `moorestech_server/Assets/Scripts/Game.Orbit/*` | 軌道到着サービス・初打ち上げ履歴（新規asmdef） |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/*.cs` | 発射台の進行・電力・インベントリ・スロット並び・積み込み・成果の保留 |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/*.cs` | 段階・待ち理由・状態詳細・セーブ形・操作の種類と結果 |
| `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs` | 発射台の組み立て |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/...` | ワールドセーブへの `rocketLaunchHistory`・V6→V7 移行 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RocketLaunchPadCommandProtocol.cs` | 建造・発射の合図 |
| `moorestech_server/Assets/Scripts/Server.Boot/Composition/OrbitServiceRegistration.cs` | Game.Orbit の DI 登録 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Rocket/RocketLaunchPadStateChangeProcessor.cs` | ロケット本体の表示と上昇 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/...` | DTO・アクション・API |
| `moorestech_web/webui/src/...` | 発射台パネル |
| moorestech_master `server_v8/mods/moorestechAlphaMod_8/master/*.json` | 積荷表・アイテム・発射台・レシピ・研究 |

---

### Task 1: 積荷表と発射台のスキーマ・マスタ検証・積荷のマスタ行

**Files:**
- Modify: `VanillaSchema/items.yml`（`levelFamilies` の直後に `orbitalPayloads`）
- Modify: `VanillaSchema/blocks.yml`（blockType enum の末尾に `RocketLaunchPad`、`blockParam.cases` の末尾に case）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/items.json`
- Modify: `mooresmaster/mooresmaster.SandBox/TestMod/items.json`（edit-schema スキルが SandBox の更新を求める場合のみ。`schema/items.json` はスキーマ生成物なら同スキルの手順で再生成）
- Modify: moorestech_master `server_v8/mods/moorestechAlphaMod_8/master/items.json`（計算データ・GPU衛星 Lv1〜Lv5・levelFamily・`orbitalPayloads` 5行と、`stackLevelTableGuid` が空の7アイテムへ Default）
- Modify: moorestech_master `server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json`（「生産」に「宇宙」サブカテゴリ）
- Modify: `.moorestech-external-revisions.json`
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs`
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ItemMasterUtil.cs`（`Validate` の連鎖に1行。`ItemMaster.Validate` は委譲の1行のまま）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs`（`Validate` に1行）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`、`ForUnitTestItemId.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/OrbitalPayloadMasterValidatorTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/RocketLaunchPadParamValidatorTest.cs`

**Interfaces:**
- Produces:
  - 生成型 `Mooresmaster.Model.ItemsModule.OrbitalPayloadMasterElement`（`Guid PayloadItemGuid`・`int CountPerLaunch`・`OrbitalArrivalOutputElement[] ArrivalOutputs`）、`OrbitalArrivalOutputElement`（`Guid ItemGuid`・`int Count`）、`Items.OrbitalPayloads`
  - 生成型 `Mooresmaster.Model.BlocksModule.RocketLaunchPadBlockParam`（`float RequiredPower`・`float BuildSeconds`・`float LaunchSeconds`・`RocketLaunchMaterialElement[] LaunchMaterials`・`int OutputSlotCount`・`InventoryConnectors`・`MaxWireConnectionCount`・`ConnectionRange`・`ConnectionHeightRange`）、`RocketLaunchMaterialElement`（`Guid ItemGuid`・`int Count`）
  - `internal static string OrbitalPayloadMasterValidator.Validate(Items items)`
  - `internal static string RocketLaunchPadParamValidator.Validate(Blocks blocks)`
  - テストID: `ForUnitTestModBlockId.RocketLaunchPadId`、`ForUnitTestItemId.RocketCaseId`・`RocketFuelId`・`RocketComputerId`・`SatellitePayloadId`・`CablePayloadId`・`ComputeDataId`
  - 生成された数値型（`float`/`double`）は SourceGenerator の出力に合わせる。上の表記と違えば以降のタスクのコードも出力側に合わせる

- [ ] **Step 0: 環境を確かめる**

Run:
```bash
pwd   # worktree tier9-compute-data-loop のルートであること
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" branch --show-current   # feat/tier9-compute-data-loop であること
moores-wt status
cd moorestech_web/webui && pnpm install --frozen-lockfile && cd ../..
uloop compile --project-path ./moorestech_client
```
Expected: 自分のworktreeのEditorが居なければ `uloop launch --project-path ./moorestech_client` で起動してからcompile。ErrorCount 0。

- [ ] **Step 1: items.yml に積荷表を足す**

`levelFamilies` ブロックの直後（`itemStackLevelTables` の直前）に追加する:

```yaml
# 軌道へ運べる積荷の表。打ち上げ口（発射台・エレベーター拠点）はここに載る積荷だけを扱い、軌道到着で打ち上げ成果を出す
# Table of cargo that can be sent to orbit; launch sites only handle cargo listed here and yield the arrival outputs on orbital arrival
- key: orbitalPayloads
  type: array
  overrideCodeGeneratePropertyName: OrbitalPayloadMasterElement
  items:
    type: object
    properties:
    - key: payloadItemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
    - key: countPerLaunch
      type: integer
      default: 1
    - key: arrivalOutputs
      type: array
      overrideCodeGeneratePropertyName: OrbitalArrivalOutputElement
      items:
        type: object
        properties:
        - key: itemGuid
          type: uuid
          foreignKey:
            schemaId: items
            foreignKeyIdPath: /data/[*]/itemGuid
            displayElementPath: /data/[*]/name
        - key: count
          type: integer
          default: 1
```

- [ ] **Step 2: blocks.yml に発射台を足す**

blockType の `options` の末尾（`CleanRoomMachine` の次）に `- RocketLaunchPad` を足し、`blockParam.cases` の末尾に追加する:

```yaml
      - when: RocketLaunchPad
        type: object
        implementationInterface:
        - IInventoryConnectors
        - IElectricWireConnectParam
        properties:
        # 建造中だけ要求する電力。供給率に応じて建造が進む
        # Power requested only while building; building advances by the supply rate
        - key: requiredPower
          type: number
          default: 100
        - key: buildSeconds
          type: number
          default: 60
        # 打ち上げ開始から本体が昇り切るまでの秒数。この間は次の便を建造しない
        # Seconds from launch start until the rocket body finishes rising; no next build meanwhile
        - key: launchSeconds
          type: number
          default: 10
        # 1便ごとに消費する資材。1件につき専用スロットを1つ持つ
        # Materials consumed per launch; each entry owns one dedicated slot
        - key: launchMaterials
          type: array
          overrideCodeGeneratePropertyName: RocketLaunchMaterialElement
          items:
            type: object
            properties:
            - key: itemGuid
              type: uuid
              foreignKey:
                schemaId: items
                foreignKeyIdPath: /data/[*]/itemGuid
                displayElementPath: /data/[*]/name
            - key: count
              type: integer
              default: 1
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

- [ ] **Step 3: 全 items.json に `"orbitalPayloads": []` を足し、テストマスタに発射台と積荷を足す**

`git ls-files | grep -E "items\.json$"` の全ファイル（moorestech_master も）へ `"orbitalPayloads": []` を足す（edit-schema スキルの「全JSON一括更新」手順に従う）。そのうえで ForUnitTest の items.json の `data` に次の6件を足し、`orbitalPayloads` を2行にする:

```json
{ "name": "TestRocketCase", "itemGuid": "5c8e0000-0000-4000-8000-000000000001", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestRocketFuel", "itemGuid": "5c8e0000-0000-4000-8000-000000000002", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestRocketComputer", "itemGuid": "5c8e0000-0000-4000-8000-000000000003", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestSatellitePayload", "itemGuid": "5c8e0000-0000-4000-8000-000000000004", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestCablePayload", "itemGuid": "5c8e0000-0000-4000-8000-000000000005", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestComputeData", "itemGuid": "5c8e0000-0000-4000-8000-000000000006", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" }
```

```json
"orbitalPayloads": [
  { "payloadItemGuid": "5c8e0000-0000-4000-8000-000000000004", "countPerLaunch": 1, "arrivalOutputs": [ { "itemGuid": "5c8e0000-0000-4000-8000-000000000006", "count": 150 } ] },
  { "payloadItemGuid": "5c8e0000-0000-4000-8000-000000000005", "countPerLaunch": 5, "arrivalOutputs": [] }
]
```

（成果150はスタック上限100を超え、分割と出力満杯の検証に使う。出力2スロットに1便ぶん（100＋50）が入りきるので、発射台パラメータの検証は通る。）

ForUnitTest の blocks.json の `data` に発射台を足す。共通キーはテスト機械 `TestElectricMachine`（`00000000-0000-0000-0000-000000000001`）と同じ値にし、`inventoryConnectors` は同じ形で `connectorGuid` だけ新しくする（テストでは 1×1×1 で足りる）:

```json
{
  "blockGuid": "5c8e0000-0000-4000-8000-000000000100",
  "name": "TestRocketLaunchPad",
  "blockType": "RocketLaunchPad",
  "openable": true,
  "maxStack": 100,
  "modelTransform": { "scale": [1, 1, 1], "rotation": [0, 0, 0], "position": [0, 0, 0] },
  "blockSize": [1, 1, 1],
  "category": "テスト",
  "subCategory": "RocketLaunchPad",
  "placementsPerCost": 1,
  "blockParam": {
    "requiredPower": 100,
    "buildSeconds": 1,
    "launchSeconds": 1,
    "launchMaterials": [
      { "itemGuid": "5c8e0000-0000-4000-8000-000000000001", "count": 2 },
      { "itemGuid": "5c8e0000-0000-4000-8000-000000000002", "count": 3 },
      { "itemGuid": "5c8e0000-0000-4000-8000-000000000003", "count": 1 }
    ],
    "outputSlotCount": 2,
    "inventoryConnectors": {
      "inputConnects": [ { "offset": [0, 0, 0], "directions": [[1, 0, 0], [-1, 0, 0], [0, 0, 1], [0, 0, -1]], "connectorGuid": "607ee58d-6dc6-4647-a882-7213cdd1334a" } ],
      "outputConnects": [ { "offset": [0, 0, 0], "directions": [[1, 0, 0], [-1, 0, 0], [0, 0, 1], [0, 0, -1]], "connectorGuid": "6fa4b379-b0ea-4b4e-8484-2dabea65fe2d" } ]
    },
    "maxWireConnectionCount": 2,
    "connectionRange": 30,
    "connectionHeightRange": 20
  }
}
```

`ForUnitTestModBlockId.cs` に `public static BlockId RocketLaunchPadId => GetBlock("5c8e0000-0000-4000-8000-000000000100");`、`ForUnitTestItemId.cs` に既存の書式で6件（`RocketCaseId`=…0001、`RocketFuelId`=…0002、`RocketComputerId`=…0003、`SatellitePayloadId`=…0004、`CablePayloadId`=…0005、`ComputeDataId`=…0006）を足す。

moorestech_master の items.json では、`stackLevelTableGuid` が空の7件（グラフェンケーブル・グラフェンシート・使用済み銅板・高純度炭素・ロケット筐体素材・ロケット燃料・反物質爆弾）へ Default `9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa` を入れる（空のままだと `ItemMasterUtil` の参照検証で読込が止まる）。

続けて同じ items.json に積荷の行を入れる（S3 が Task 2 の push 後に取り込むため、ここで入れる）:
- `data` に `name` が「計算データ」（`01a167cf-af88-74d3-9cd6-7d61b71052bb`）と「GPU衛星Lv1」〜「GPU衛星Lv5」（GUID は Global Constraints。名前の書式は既存の「ICチップLv1」と同じ）の6件を足す。`stackLevelTableGuid` は Default、`sortPriority` はロケット燃料の値の後ろへ連番、`imagePath`・`recipeViewType`・`initialUnlocked` はロケット燃料の行と同じ値にする。
- `levelFamilies`（キーが無ければ作る。書式は ForUnitTest の items.json の `levelFamilies` と同じ）に `{ "familyGuid": "01a167cf-af9a-78e5-9203-1c18ce94cdc6", "name": "GPU衛星", "baseItemGuid": "01a167cf-af8b-7894-8de6-593ee30289c0", "levelItemGuids": ["01a167cf-af8b-7894-8de6-593ee30289c0", "01a167cf-af8e-7e95-b299-09eb712bee12", "01a167cf-af91-7f62-93d1-e499b06f2398", "01a167cf-af94-7e25-9654-825374c3923b", "01a167cf-af97-7f67-bf53-7833b8d17a15"] }`。
- `orbitalPayloads` に GPU衛星 Lv1〜Lv5 の5行。どれも `countPerLaunch: 1`、`arrivalOutputs` は計算データ `01a167cf-af88-74d3-9cd6-7d61b71052bb` を Lv1=10・Lv2=20・Lv3=40・Lv4=80・Lv5=160（判断記録 D12）。

buildMenu.json の「生産」（`categoryGuid: d1000000-0000-4000-8000-000000000002`）の `subCategories` 末尾（「半導体」の次）に `{ "name": "宇宙", "entrySource": "blocks", "subCategoryGuid": "d2000000-0000-4000-8000-000200000005" }` を足す。ブロック側は名前（`category: "生産"`・`subCategory: "宇宙"`）でここへ入る。中身の無いサブカテゴリでマスタ読込やビルドメニューの検証が止まる場合は、Task 9 Step 2 の発射台の `category`・`subCategory` の設定をこのステップへ前倒しする。

- [ ] **Step 4: 積荷表の検証を書く**

`moorestech_server/Assets/Scripts/Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.OrbitalPayload
{
    // 積荷表の行がアイテム定義と矛盾しないことを読込時に確かめる
    // Validates at load that orbital payload rows agree with the item definitions
    internal static class OrbitalPayloadMasterValidator
    {
        internal static string Validate(Items items)
        {
            var logs = "";
            var itemByGuid = items.Data.ToDictionary(item => item.ItemGuid);
            var tableByGuid = items.ItemStackLevelTables.ToDictionary(table => table.TableGuid);
            var seenPayloads = new HashSet<Guid>();
            foreach (var payload in items.OrbitalPayloads)
            {
                logs += ValidatePayloadItem(payload);
                foreach (var output in payload.ArrivalOutputs) logs += ValidateOutput(payload, output);
            }
            return logs;

            #region Internal

            string ValidatePayloadItem(OrbitalPayloadMasterElement payload)
            {
                // 積荷は実在し、表に1回だけ載り、1便ぶんが1スロットに収まる
                // A payload must exist, appear once, and fit one launch into a single slot
                if (!itemByGuid.TryGetValue(payload.PayloadItemGuid, out var item)) return $"[ItemMaster.orbitalPayloads] unknown payloadItemGuid:{payload.PayloadItemGuid}\n";
                if (!seenPayloads.Add(payload.PayloadItemGuid)) return $"[ItemMaster.orbitalPayloads] duplicate payload:{item.Name}\n";
                if (payload.CountPerLaunch < 1) return $"[ItemMaster.orbitalPayloads] {item.Name} countPerLaunch must be >= 1 (was {payload.CountPerLaunch})\n";

                // 発射台は積荷1種につき1スロットなので、レベル1のスタック上限を超える1便は積めない
                // A launch pad has one slot per payload, so a launch above the level-1 stack limit can never be loaded
                // silent-ok: テーブル欠落・段数0のテーブルは ItemMasterUtil の既存検証が報告する / マスタ読込時のエラーログで開発者が気づく
                if (!tableByGuid.TryGetValue(item.StackLevelTableGuid, out var table) || table.StackCounts.Length == 0) return "";
                var firstLevelStack = table.StackCounts[0];
                return payload.CountPerLaunch > firstLevelStack ? $"[ItemMaster.orbitalPayloads] {item.Name} countPerLaunch {payload.CountPerLaunch} exceeds level-1 stack {firstLevelStack}\n" : "";
            }

            string ValidateOutput(OrbitalPayloadMasterElement payload, OrbitalArrivalOutputElement output)
            {
                if (!itemByGuid.ContainsKey(output.ItemGuid)) return $"[ItemMaster.orbitalPayloads] payload:{payload.PayloadItemGuid} has unknown arrival output:{output.ItemGuid}\n";
                return output.Count < 1 ? $"[ItemMaster.orbitalPayloads] payload:{payload.PayloadItemGuid} arrival output count must be >= 1\n" : "";
            }

            #endregion
        }
    }
}
```

（スタックテーブル欠落と段数0のテーブルは `ItemMasterUtil` の既存検証（`StackLevelTableValidation`）が報告するので、ここでは二重に出さない。段数0を見送るのは既存の `InitialLevelMaxStackValidation` と同じ扱い。）

`ItemMaster.Validate` は委譲の1行のまま残し、連鎖は `ItemMasterUtil.Validate` に足す（前例 `BlockMasterUtil.Validate` の `errorLogs +=` 連結）。`ItemMasterUtil.cs` に `using Core.Master.Validator.OrbitalPayload;` を足し、`errorLogs += InitialEquipmentValidation();` の次の行に足す:

```csharp
            errorLogs += OrbitalPayloadMasterValidator.Validate(items);
```

（`ItemMasterUtil.cs` は既に244行で、この2行で246行になる。分割は本PRの範囲を広げるので行わず、Task 13 の PR 本文「本PR外のリファクタ提案」に書く。）

- [ ] **Step 5: 発射台パラメータの検証を書く**

`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    // 発射台が建造・打ち上げを進められないパラメータを読込時に止める
    // Stops at load any launch pad parameters that could never build or launch
    internal static class RocketLaunchPadParamValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            var payloads = MasterHolder.ItemMaster.Items.OrbitalPayloads;
            var payloadGuids = new HashSet<Guid>(payloads.Select(payload => payload.PayloadItemGuid));
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not RocketLaunchPadBlockParam param) continue;
                logs += ValidateScalars(block.Name, param);
                logs += ValidateMaterials(block.Name, param);
                logs += ValidateOutputCapacity(block.Name, param);

                // 積荷表が空だと揃う積荷が無く、永遠に建造しない
                // With an empty payload table nothing can ever be loaded, so the pad would never build
                if (payloadGuids.Count == 0) logs += $"[BlockMaster] {block.Name} is a RocketLaunchPad but items.orbitalPayloads is empty\n";
            }
            return logs;

            #region Internal

            string ValidateScalars(string name, RocketLaunchPadBlockParam param)
            {
                var result = "";
                if (param.RequiredPower <= 0) result += $"[BlockMaster] {name} requiredPower must be > 0\n";
                if (param.BuildSeconds <= 0) result += $"[BlockMaster] {name} buildSeconds must be > 0\n";
                if (param.LaunchSeconds <= 0) result += $"[BlockMaster] {name} launchSeconds must be > 0\n";
                if (param.OutputSlotCount < 1) result += $"[BlockMaster] {name} outputSlotCount must be >= 1\n";
                if (param.LaunchMaterials.Length == 0) result += $"[BlockMaster] {name} launchMaterials must not be empty\n";
                return result;
            }

            string ValidateMaterials(string name, RocketLaunchPadBlockParam param)
            {
                // 資材は実在・重複なし・積荷と別アイテム・1便ぶんがレベル1のスタックに収まる
                // Materials must exist, be unique, differ from payloads, and fit one launch in a level-1 stack
                var result = "";
                var seen = new HashSet<Guid>();
                foreach (var material in param.LaunchMaterials)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(material.ItemGuid);
                    if (itemId == null) { result += $"[BlockMaster] {name} has unknown launch material:{material.ItemGuid}\n"; continue; }
                    if (!seen.Add(material.ItemGuid)) result += $"[BlockMaster] {name} has duplicate launch material:{material.ItemGuid}\n";
                    if (payloadGuids.Contains(material.ItemGuid)) result += $"[BlockMaster] {name} launch material {material.ItemGuid} is also an orbital payload\n";
                    if (material.Count < 1) result += $"[BlockMaster] {name} launch material count must be >= 1\n";

                    var firstLevelStack = FirstLevelStackOrNull(itemId.Value);
                    if (firstLevelStack != null && material.Count > firstLevelStack) result += $"[BlockMaster] {name} launch material {material.ItemGuid} count {material.Count} exceeds level-1 stack {firstLevelStack}\n";
                }
                return result;
            }

            string ValidateOutputCapacity(string name, RocketLaunchPadBlockParam param)
            {
                // 空の出力口に1便ぶんの成果が入りきらない積荷は、その台で永遠に出力満杯のまま打ち上がらない
                // A payload whose single launch outputs cannot fit empty output slots would stay output-full forever on this pad
                var result = "";
                foreach (var payload in payloads)
                {
                    var stackCount = 0;
                    foreach (var output in payload.ArrivalOutputs)
                    {
                        var outputItemId = MasterHolder.ItemMaster.GetItemIdOrNull(output.ItemGuid);
                        var firstLevelStack = outputItemId == null ? null : FirstLevelStackOrNull(outputItemId.Value);

                        // silent-ok: 未知の成果アイテムは積荷表の検証、段数0のテーブルは ItemMasterUtil が報告する / マスタ読込時のエラーログで開発者が気づく
                        if (firstLevelStack == null) continue;
                        stackCount += (output.Count + firstLevelStack.Value - 1) / firstLevelStack.Value;
                    }
                    if (stackCount > param.OutputSlotCount) result += $"[BlockMaster] {name} outputSlotCount {param.OutputSlotCount} cannot hold one launch of payload {payload.PayloadItemGuid} ({stackCount} stacks at level-1 stack)\n";
                }
                return result;
            }

            int? FirstLevelStackOrNull(ItemId itemId)
            {
                var element = MasterHolder.ItemMaster.GetItemMaster(itemId);
                var stackCounts = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid).StackCounts;

                // silent-ok: 段数0のテーブルは ItemMasterUtil の既存検証が報告する / マスタ読込時のエラーログで開発者が気づく
                return stackCounts.Length == 0 ? null : stackCounts[0];
            }

            #endregion
        }
    }
}
```

成果のスタック数はレベル1のスタック上限で数える。実行時の分割（`CreateSplitStacks`）はその時点のスタック上限を使い、レベルが上がるほど上限は大きくなるので、レベル1で入りきれば実行時も空の出力口に入りきる。

`BlockMasterUtil.Validate` の `errorLogs += BeltSpeedPerSecondValidator.Validate(blocks);` の次に `errorLogs += RocketLaunchPadParamValidator.Validate(blocks);` を足す。

- [ ] **Step 6: 検証のテストを書く**

検証クラスは `internal` のまま、テストは公開された集約点 `ItemMasterUtil.Validate`・`BlockMasterUtil.Validate` を呼ぶ（前例 `Tests/UnitTest/Game/BeltSpeedPerSecondMasterTest.cs` の `BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs)`）。

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/OrbitalPayloadMasterValidatorTest.cs`:

```csharp
using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.OrbitalPayload
{
    public class OrbitalPayloadMasterValidatorTest
    {
        [Test]
        public void テストマスタの積荷表は検証を通る()
        {
            // DIの生成でマスタが読み込まれ、検証エラーなら例外で落ちる
            // Building the container loads the master and would throw on a validation error
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(2, MasterHolder.ItemMaster.Items.OrbitalPayloads.Length);
        }

        [TestCase("countPerLaunch", 101, "exceeds level-1 stack")]
        [TestCase("countPerLaunch", 0, "must be >= 1")]
        public void 積荷の個数の不正を止める(string key, int value, string expected)
        {
            var json = LoadTestItemsJson();
            json["orbitalPayloads"][0][key] = value;

            var isValid = ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains(expected, errorLogs);
        }

        [Test]
        public void 重複した積荷を止める()
        {
            var json = LoadTestItemsJson();
            ((JArray)json["orbitalPayloads"]).Add(json["orbitalPayloads"][0].DeepClone());

            var isValid = ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains("duplicate payload", errorLogs);
        }

        private static JObject LoadTestItemsJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "items.json");
            return JObject.Parse(File.ReadAllText(path));
        }
    }
}
```

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/RocketLaunchPadParamValidatorTest.cs`（`BeltSpeedPerSecondMasterTest` の `PrepareBlocksJson`・`FindBlockParam` を同ファイル内に写す。`PrepareBlocksJson` が DI を作るので `MasterHolder.ItemMaster` は読込済み）:
- `出力口に1便ぶんの成果が入りきらない台を止める`: `TestRocketLaunchPad` の `outputSlotCount` を1にして `BlockMasterUtil.Validate` が false、ログに `cannot hold one launch of payload 5c8e0000-0000-4000-8000-000000000004 (2 stacks at level-1 stack)` を含む（成果150はレベル1のスタック100で2スタック）。
- `資材の個数がスタックを超える台を止める`: `launchMaterials[0].count` を101にして `exceeds level-1 stack 100` を含む。
- `テストマスタの発射台は検証を通る`: 変更しないJSONで `BlockMasterUtil.Validate` が true。

(`TestModDirectory.ForUnitTestModDirectory` の実際のパス構成は前例 `BeltSpeedPerSecondMasterTest.PrepareBlocksJson` と同じ組み立てにする。)

- [ ] **Step 7: マスタを push しピンを向ける**

```bash
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" add server_v8/mods/moorestechAlphaMod_8/master/items.json server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json
git -C "$MASTER_WT" commit -m "data(v8): 計算データ・GPU衛星と積荷表、ビルドメニューの宇宙を足し、スタックテーブル未設定のTier9アイテムへDefaultを入れる"
git -C "$MASTER_WT" push origin feat/tier9-compute-data-loop
git -C "$MASTER_WT" rev-parse HEAD   # この値を .moorestech-external-revisions.json の moorestech_master.commitHash へ
```

- [ ] **Step 8: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "OrbitalPayloadMasterValidatorTest|RocketLaunchPadParamValidatorTest|ItemMaster|BlockMaster"`
Expected: ErrorCount 0 / 全 PASS。Editor の Console に `[ItemMaster]` `[BlockMaster]` のエラーが無い（ピン先のマスタが読めている）。

- [ ] **Step 9: コミットする**

```bash
git add VanillaSchema/items.yml VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData .moorestech-external-revisions.json mooresmaster
git commit -m "feat(master): 積荷表と発射台のスキーマと読込時検証を足す"
```

---

### Task 2: 軌道到着サービスと初打ち上げ履歴（Game.Orbit）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/Game.Orbit.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/OrbitalArrival.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/IOrbitalArrivalService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/OrbitalArrivalService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/IRocketLaunchHistory.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/RocketLaunchHistory.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/RocketLaunchHistoryJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Composition/OrbitServiceRegistration.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`SaveAndEventServiceRegistration.Register(...)` の直前に1行）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`、`moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef`、`moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`、テストの asmdef（`Tests` 配下で `Game.CleanRoom` を参照している asmdef すべて）の `references` に `"Game.Orbit"`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/OrbitalArrivalServiceTest.cs`

**Interfaces:**
- Consumes: Task 1 の `Items.OrbitalPayloads`、`OrbitalPayloadMasterElement`
- Produces:
  - `Game.Orbit.OrbitalArrival`（`public readonly ItemId PayloadItemId; public readonly int PayloadCount;`、ctor `(ItemId payloadItemId, int payloadCount)`）
  - `Game.Orbit.IOrbitalArrivalService`（`IObservable<OrbitalArrival> OnOrbitalArrival { get; }`・`bool IsOrbitalPayload(ItemId itemId)`・`IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)`・`IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)`）
  - `Game.Orbit.IRocketLaunchHistory`（`bool HasLaunchedOnce { get; }`・`IObservable<Unit> OnFirstLaunch { get; }`・`void RecordLaunch()`・`RocketLaunchHistoryJsonObject GetSaveData()`・`void LoadSaveData(RocketLaunchHistoryJsonObject saveData)`）
  - `Game.Orbit.RocketLaunchHistoryJsonObject`（`[JsonProperty("hasLaunchedOnce")] public bool HasLaunchedOnce;`）
  - DI: `IRocketLaunchHistory`→`RocketLaunchHistory`、`IOrbitalArrivalService`→`OrbitalArrivalService`（どちらもシングルトン）

- [ ] **Step 1: asmdef を作る**

`Game.Orbit.asmdef`（書式は `Game.CleanRoom.asmdef` と同じ）:

```json
{
  "name": "Game.Orbit",
  "rootNamespace": "",
  "references": [
    "Core.Master",
    "Core.Item.Interface",
    "Game.Context",
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

- [ ] **Step 2: 到着通知とサービスを書く**

`OrbitalArrival.cs`:

```csharp
using Core.Master;

namespace Game.Orbit
{
    // 軌道に届いた積荷の通知。S3はこれでケーブルの累計を数える
    // Notification for cargo that reached orbit; S3 counts its cable total from it
    public class OrbitalArrival
    {
        public readonly ItemId PayloadItemId;
        public readonly int PayloadCount;

        public OrbitalArrival(ItemId payloadItemId, int payloadCount)
        {
            PayloadItemId = payloadItemId;
            PayloadCount = payloadCount;
        }
    }
}
```

`IOrbitalArrivalService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Game.Orbit
{
    // 打ち上げ口が軌道到着を伝える窓口。成果の量や積荷の種類による分岐はここだけが持つ
    // The single entry launch sites use to report orbital arrival; only this owns output amounts and per-payload branching
    public interface IOrbitalArrivalService
    {
        IObservable<OrbitalArrival> OnOrbitalArrival { get; }

        bool IsOrbitalPayload(ItemId itemId);

        // 到着させずに成果だけを返す。打ち上げ口は出力口に入りきるかをこれで確かめてから打ち上げる
        // Returns the outputs without arriving; launch sites check output space with it before launching
        IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount);

        // 成果を新しいスタックの並びで返し、到着を通知する。表に無い積荷・1便の整数倍でない個数は呼び出し側のバグとして例外
        // Returns the outputs as fresh stacks and publishes the arrival; unknown payloads or non-multiple counts are caller bugs and throw
        IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount);
    }
}
```

`OrbitalArrivalService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.ItemsModule;
using UniRx;

namespace Game.Orbit
{
    // 積荷表から打ち上げ成果を決め、軌道到着を流す
    // Decides arrival outputs from the payload table and publishes orbital arrivals
    public class OrbitalArrivalService : IOrbitalArrivalService
    {
        public IObservable<OrbitalArrival> OnOrbitalArrival => _onOrbitalArrival;

        private readonly Subject<OrbitalArrival> _onOrbitalArrival = new();
        private readonly Dictionary<ItemId, OrbitalPayloadMasterElement> _payloadByItemId = new();

        public OrbitalArrivalService()
        {
            // 積荷表を積荷アイテムで引けるように索引する
            // Index the payload table by payload item
            foreach (var payload in MasterHolder.ItemMaster.Items.OrbitalPayloads)
                _payloadByItemId.Add(MasterHolder.ItemMaster.GetItemId(payload.PayloadItemGuid), payload);
        }

        public bool IsOrbitalPayload(ItemId itemId)
        {
            return _payloadByItemId.ContainsKey(itemId);
        }

        public IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)
        {
            var payload = FindPayload();

            // 成果は1便ぶん×便数。スタック上限を超える量は分割して返す
            // Outputs are per-launch amounts times launches, split when above the stack limit
            var launchCount = payloadCount / payload.CountPerLaunch;
            var outputs = new List<IItemStack>();
            foreach (var output in payload.ArrivalOutputs)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(output.ItemGuid);
                outputs.AddRange(ServerContext.ItemStackFactory.CreateSplitStacks(outputItemId, output.Count * launchCount));
            }
            return outputs;

            #region Internal

            OrbitalPayloadMasterElement FindPayload()
            {
                if (!_payloadByItemId.TryGetValue(payloadItemId, out var found))
                    throw new InvalidOperationException($"積荷表に無いアイテムの打ち上げ成果を求められました ItemId:{payloadItemId}");
                if (payloadCount <= 0 || payloadCount % found.CountPerLaunch != 0)
                    throw new ArgumentException($"積荷の個数が1便の個数の正の整数倍ではありません ItemId:{payloadItemId} Count:{payloadCount} CountPerLaunch:{found.CountPerLaunch}");
                return found;
            }

            #endregion
        }

        public IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)
        {
            // 先に知らせた成果と同じものを返し、到着を流す
            // Return the same outputs as the preview and publish the arrival
            var outputs = PreviewArrivalOutputs(payloadItemId, payloadCount);
            _onOrbitalArrival.OnNext(new OrbitalArrival(payloadItemId, payloadCount));
            return outputs;
        }
    }
}
```

- [ ] **Step 3: 初打ち上げ履歴を書く**

`IRocketLaunchHistory.cs`:

```csharp
using System;
using UniRx;

namespace Game.Orbit
{
    // ワールドで初打ち上げが済んだか。済むまで全ての発射台は建造と発射の合図を待つ
    // Whether the world has had its first launch; until then every launch pad waits for build and launch commands
    public interface IRocketLaunchHistory
    {
        bool HasLaunchedOnce { get; }
        IObservable<Unit> OnFirstLaunch { get; }

        void RecordLaunch();
        RocketLaunchHistoryJsonObject GetSaveData();
        void LoadSaveData(RocketLaunchHistoryJsonObject saveData);
    }
}
```

`RocketLaunchHistory.cs`:

```csharp
using System;
using UniRx;

namespace Game.Orbit
{
    public class RocketLaunchHistory : IRocketLaunchHistory
    {
        public bool HasLaunchedOnce { get; private set; }
        public IObservable<Unit> OnFirstLaunch => _onFirstLaunch;

        private readonly Subject<Unit> _onFirstLaunch = new();

        public void RecordLaunch()
        {
            // 通知はワールドで最初の1回だけ。2回目以降は状態が変わらない
            // Notify only on the world's first launch; later launches change nothing
            if (HasLaunchedOnce) return;
            HasLaunchedOnce = true;
            _onFirstLaunch.OnNext(Unit.Default);
        }

        public RocketLaunchHistoryJsonObject GetSaveData()
        {
            return new RocketLaunchHistoryJsonObject { HasLaunchedOnce = HasLaunchedOnce };
        }

        public void LoadSaveData(RocketLaunchHistoryJsonObject saveData)
        {
            HasLaunchedOnce = saveData.HasLaunchedOnce;
        }
    }
}
```

`RocketLaunchHistoryJsonObject.cs`:

```csharp
using Newtonsoft.Json;

namespace Game.Orbit
{
    public class RocketLaunchHistoryJsonObject
    {
        [JsonProperty("hasLaunchedOnce")] public bool HasLaunchedOnce;
    }
}
```

- [ ] **Step 4: DI に登録する**

`Server.Boot/Composition/OrbitServiceRegistration.cs`（`GameplayServiceRegistration.cs` は202行で既に上限超えのため足さない）:

```csharp
using Game.Orbit;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.Composition
{
    // 軌道到着と初打ち上げ履歴をワールドに1つずつ置く
    // Registers one orbital arrival service and one launch history per world
    internal static class OrbitServiceRegistration
    {
        internal static void Register(ServiceCollection services)
        {
            services.AddSingleton<IRocketLaunchHistory, RocketLaunchHistory>();
            services.AddSingleton<IOrbitalArrivalService, OrbitalArrivalService>();
        }
    }
}
```

`MoorestechServerDIContainerGenerator.cs` の `GameplayServiceRegistration.Register(...)` の次の行に `OrbitServiceRegistration.Register(services);` を足す。各 asmdef に `"Game.Orbit"` を足す。

- [ ] **Step 5: テストを書く**

`Tests/CombinedTest/Game/RocketLaunchPad/OrbitalArrivalServiceTest.cs`:

```csharp
using System.Collections.Generic;
using Game.Context;
using Game.Orbit;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class OrbitalArrivalServiceTest
    {
        [Test]
        public void 衛星の到着で成果を分割して返し到着を通知する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 成果150はスタック100を超えるので2スタックに分かれる
            // An output of 150 exceeds the stack of 100, so it splits into two stacks
            var outputs = service.Arrive(ForUnitTestItemId.SatellitePayloadId, 1);

            Assert.AreEqual(2, outputs.Count);
            Assert.AreEqual(150, outputs[0].Count + outputs[1].Count);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, outputs[0].Id);
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(ForUnitTestItemId.SatellitePayloadId, arrivals[0].PayloadItemId);
        }

        [Test]
        public void 成果の無い積荷は空を返し通知はする()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            Assert.AreEqual(0, service.Arrive(ForUnitTestItemId.CablePayloadId, 10).Count);
            Assert.AreEqual(10, arrivals[0].PayloadCount);
        }

        [Test]
        public void 成果の先読みは到着と同じ量を返し通知しない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 先読みは通知せず、到着と同じスタックの並びを返す
            // The preview publishes nothing and returns the same stacks as the arrival
            var preview = service.PreviewArrivalOutputs(ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.AreEqual(0, arrivals.Count);
            var outputs = service.Arrive(ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.AreEqual(outputs.Count, preview.Count);
            for (var i = 0; i < outputs.Count; i++)
            {
                Assert.AreEqual(outputs[i].Id, preview[i].Id);
                Assert.AreEqual(outputs[i].Count, preview[i].Count);
            }
            Assert.Throws<System.ArgumentException>(() => service.PreviewArrivalOutputs(ForUnitTestItemId.CablePayloadId, 3));
        }

        [Test]
        public void 表に無い積荷と半端な個数は例外()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();

            Assert.IsFalse(service.IsOrbitalPayload(ForUnitTestItemId.ComputeDataId));
            Assert.Throws<System.InvalidOperationException>(() => service.Arrive(ForUnitTestItemId.ComputeDataId, 1));
            Assert.Throws<System.ArgumentException>(() => service.Arrive(ForUnitTestItemId.CablePayloadId, 3));
        }

        [Test]
        public void 初打ち上げの通知は1回だけ()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var history = ServerContext.GetService<IRocketLaunchHistory>();
            var count = 0;
            history.OnFirstLaunch.Subscribe(_ => count++);

            history.RecordLaunch();
            history.RecordLaunch();

            Assert.IsTrue(history.HasLaunchedOnce);
            Assert.AreEqual(1, count);
        }
    }
}
```

（`ForUnitTestItemId` の各IDが `ItemId` を返す形かは既存の書式に合わせる。`MoorestechServerDIContainerGenerator().Create` の戻り値の使い方は既存 CombinedTest に合わせる。）

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Tests.CombinedTest.Game.RocketLaunchPad.OrbitalArrivalServiceTest"`
Expected: ErrorCount 0 / 5件 PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Orbit moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef moorestech_server/Assets/Scripts/Tests
git commit -m "feat(orbit): 軌道到着サービスと初打ち上げ履歴を足す"
```

- [ ] **Step 8: 両repoを push して S3 へ渡す**

S3 はこの push を取り込んでからエレベーター拠点に着手する（Global Constraints）。

```bash
git push origin feat/tier9-compute-data-loop
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" push origin feat/tier9-compute-data-loop
git rev-parse HEAD
git -C "$MASTER_WT" rev-parse HEAD
bd note moorestech-xyuaf.1 "Game.Orbitと積荷表をpush済み（S3取り込み可）: moorestech <上のハッシュ> / moorestech_master <上のハッシュ>"
```

Expected: 両方の push が成功し、`.moorestech-external-revisions.json` の `moorestech_master.commitHash` が moorestech_master 側の HEAD と一致する。

---

### Task 3: ワールドセーブへの初打ち上げ履歴と V6→V7 移行

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（`CurrentVersion = 7`、ctor 引数 `RocketLaunchHistoryJsonObject rocketLaunchHistory` を `cleanRoomRooms` の次に、プロパティ `[JsonProperty("rocketLaunchHistory")] public RocketLaunchHistoryJsonObject RocketLaunchHistory { get; }`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`（ctor に `IRocketLaunchHistory`、`Capture()` で `_rocketLaunchHistory.GetSaveData()`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveDataRestorer.cs`（ctor に `IRocketLaunchHistory`、`LoadUnlockState` の次で `LoadSaveData`、必須欠損の検査へ `rocketLaunchHistory`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveRequiredFieldGuard.cs`（`WorldSaveDataRestorer` の `ThrowIfRequiredFieldMissing` を移す。194行のファイルを200行以内に保つため）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`（`SectionsWithoutMasterPruning` の「マスタguidを持たない」行に `"rocketLaunchHistory"`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV6ToV7.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs:116`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs:37`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs:137`（配列の末尾に `new SaveMigrationStepV6ToV7()`）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV6ToV7Test.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchHistorySaveLoadTest.cs`

**Interfaces:**
- Consumes: Task 2 の `IRocketLaunchHistory`・`RocketLaunchHistoryJsonObject`
- Produces: セーブの `rocketLaunchHistory: { hasLaunchedOnce: bool }`（版7で必須）、`SaveMigrationStepV6ToV7`（`FromVersion => 6`）

- [ ] **Step 1: moorestech-save-migration スキルを読み、移行ステップを書く**

`SaveMigrationStepV6ToV7.cs`:

```csharp
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V6→V7移行: ロケットの初打ち上げ履歴を未打ち上げで足す（ADR 0088）
    // V6→V7 migration: add the rocket launch history as not yet launched (ADR 0088)
    public sealed class SaveMigrationStepV6ToV7 : ISaveMigrationStep
    {
        public int FromVersion => 6;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // 版6に履歴は無い。あるなら想定外の形なので変換しない
            // V6 has no launch history; its presence is an unexpected shape, so refuse
            if (save["rocketLaunchHistory"] is JToken existing && existing.Type != JTokenType.Null) return Fail("版6のセーブに rocketLaunchHistory が既にある");

            // 版6以前に発射台は存在しないので、未打ち上げが唯一の正しい値
            // No launch pad existed before V6, so not-yet-launched is the only correct value
            save["rocketLaunchHistory"] = new JObject { ["hasLaunchedOnce"] = false };
            Debug.Log("セーブを版6から版7へ変換しました。ロケットの初打ち上げ履歴=未打ち上げ");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string reason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版6から版7へ変換できません: {reason}");
                return SaveMigrationStepResult.Failed(reason);
            }

            #endregion
        }
    }
}
```

3か所の配列の末尾へ `new SaveMigrationStepV6ToV7()` を足す。

- [ ] **Step 2: 必須欠損の検査を移して `rocketLaunchHistory` を足す**

`WorldSaveRequiredFieldGuard.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.SaveLoad.Json.WorldVersions;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    // 現在版のセーブに必須の節が欠けていれば理由付きで止める
    // Stops with a reason when a current-version save lacks a required section
    public static class WorldSaveRequiredFieldGuard
    {
        public static void ThrowIfMissing(WorldSaveAllInfo load)
        {
            var missing = new List<string>();
            if (!load.CurrentTick.HasValue) missing.Add("currentTick");
            if (load.RandomState == null) missing.Add("randomState");
            if (load.MiningCooldowns == null) missing.Add("miningCooldowns");
            if (load.BackfilledFields == null) missing.Add("backfilledFields");
            if (load.Players == null) missing.Add("players");
            if (load.RocketLaunchHistory == null) missing.Add("rocketLaunchHistory");
            if (missing.Count == 0) return;

            var reason = $"セーブに {string.Join(" / ", missing)} がありません。版が古いセーブはマイグレーション連鎖（Game.SaveLoad/Migration）が補填するため、現在版のセーブで欠けているのは手編集による破損です";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
```

`WorldSaveDataRestorer.Restore` の `ThrowIfRequiredFieldMissing();` を `WorldSaveRequiredFieldGuard.ThrowIfMissing(load);` に置き換え、ローカル関数 `ThrowIfRequiredFieldMissing` を消す。`_gameUnlockStateDataController.LoadUnlockState(...)` の次に追加する:

```csharp
            // 発射台がロード直後の初回Updateで参照するので、ブロックより先に戻す
            // Launch pads read it on their first update after load, so restore it before blocks
            _rocketLaunchHistory.LoadSaveData(load.RocketLaunchHistory);
```

`WorldSaveAllInfo` の ctor 引数・代入・プロパティ、`AssembleSaveJsonText` の ctor・`Capture()` の引数（`_cleanRoomDatastore.GetSaveData(),` の次に `_rocketLaunchHistory.GetSaveData(),`）を足す。`WorldSaveAllInfo` を `new` している他の箇所（`git grep -n "new WorldSaveAllInfo("`）も引数を足す。`MissingMasterPruner` の一覧に `"rocketLaunchHistory"` を足す。

- [ ] **Step 3: テストを書く**

`Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV6ToV7Test.cs`:

```csharp
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV6ToV7Test
    {
        [Test]
        public void 版6のセーブに未打ち上げの履歴が足され他の節は残る()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"world\":[{\"blockGuid\":\"x\"}],\"cleanRoomRooms\":[]}");

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsFalse(result.Save["rocketLaunchHistory"]["hasLaunchedOnce"].Value<bool>());
            Assert.AreEqual("x", result.Save["world"][0]["blockGuid"].Value<string>());
        }

        [Test]
        public void 履歴が既にある版6は変換しない()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"rocketLaunchHistory\":{\"hasLaunchedOnce\":true}}");
            LogAssert.Expect(LogType.Warning, new Regex("版6から版7へ変換できません"));

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsFalse(result.IsConverted);
        }
    }
}
```

`Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchHistorySaveLoadTest.cs`: `CleanRoomSaveLoadTest` と `SaveLoadJsonTestHelper` の書き方に合わせ、(a) `RecordLaunch()` 後にセーブ→新しいDIでロードして `HasLaunchedOnce == true`、(b) 版6のセーブJSON（`SaveLoadJsonTestHelper` が作る現在版のJSONから `rocketLaunchHistory` を消し `worldVersion` を6にしたもの）をロードして `HasLaunchedOnce == false` になることを確かめる2件を書く。

既存テストやフィクスチャが「現在版=6」を前提にしていれば（`git grep -n "worldVersion\\\\\":6\|CurrentVersion" -- moorestech_server/Assets/Scripts/Tests`）、7へ直す。

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|SaveLoad|RocketLaunchHistory|MissingMasterPruner"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests
git commit -m "feat(save): 初打ち上げ履歴をセーブに入れ版6から7への移行を足す"
```

---

### Task 4: 発射台のスロット並び・インベントリ・状態の型

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadSlotLayout.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadInventoryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketCargo.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/RocketLaunchPadPhase.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/RocketLaunchPadWaitReason.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/RocketLaunchPadCommand.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/RocketLaunchPadCommandResult.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/RocketLaunchPadStateDetail.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadInventoryTest.cs`（Task 5 のテンプレート登録後に実行する。このタスクではコンパイルまで）

**Interfaces:**
- Consumes: Task 1 の `RocketLaunchPadBlockParam`・`Items.OrbitalPayloads`
- Produces:
  - `RocketLaunchSlotRequirement`（readonly struct: `public readonly ItemId ItemId; public readonly int Count;`）
  - `RocketLaunchPadSlotLayout`（ctor `(RocketLaunchPadBlockParam param)`、`IReadOnlyList<RocketLaunchSlotRequirement> Materials`・`Payloads`、`int OutputSlotStart`・`OutputSlotCount`・`TotalSlotCount`、`int PayloadSlot(int payloadIndex)`、`bool IsOutputSlot(int slot)`、`bool TryGetInputSlot(ItemId itemId, out int slot)`、`RocketLaunchSlotRequirement RequirementAt(int inputSlot)`）
  - `RocketLaunchPadInventoryComponent : IOpenableBlockInventoryComponent`（ctor `(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)`）
  - `RocketCargo`（`public readonly IItemStack Payload; public readonly IReadOnlyList<IItemStack> Materials; bool IsLoaded`、ctor `(IItemStack payload, IReadOnlyList<IItemStack> materials)`、`static RocketCargo CreateNone()`、`IEnumerable<IItemStack> AllItems()`）
  - enum `RocketLaunchPadPhase { Idle, AwaitingBuildCommand, Building, AwaitingLaunchCommand, Launching }`
  - enum `RocketLaunchPadWaitReason { None, MissingMaterials, MissingPayload, OutputPending, NoPower }`
  - enum `RocketLaunchPadCommand { Build, Launch }`
  - enum `RocketLaunchPadCommandResult { Accepted, BlockNotFound, NotRocketLaunchPad, UnknownCommand, NotAwaitingBuildCommand, NotAwaitingLaunchCommand, CargoNotReady }`
  - `RocketLaunchPadStateDetail`（`const string BlockStateDetailKey = "RocketLaunchPad"`、`[Key(0)] Phase`・`[Key(1)] WaitReason`・`[Key(2)] float Progress`、`static BlockStateDetail CreateState(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)`）

- [ ] **Step 1: スロット並びを書く**

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 発射台のスロット並び。資材→積荷→出力の順で、入れ口は1アイテムにつき1スロット
    // Slot order of a launch pad: materials, then payloads, then outputs; one input slot per item
    public class RocketLaunchPadSlotLayout
    {
        public readonly IReadOnlyList<RocketLaunchSlotRequirement> Materials;
        public readonly IReadOnlyList<RocketLaunchSlotRequirement> Payloads;
        public readonly int OutputSlotStart;
        public readonly int OutputSlotCount;
        public int TotalSlotCount => OutputSlotStart + OutputSlotCount;

        public RocketLaunchPadSlotLayout(RocketLaunchPadBlockParam param)
        {
            // 資材はブロックパラメータ、積荷は積荷表の並びのまま並べる
            // Materials follow the block parameter and payloads follow the payload table order
            Materials = param.LaunchMaterials.Select(material => new RocketLaunchSlotRequirement(MasterHolder.ItemMaster.GetItemId(material.ItemGuid), material.Count)).ToList();
            Payloads = MasterHolder.ItemMaster.Items.OrbitalPayloads.Select(payload => new RocketLaunchSlotRequirement(MasterHolder.ItemMaster.GetItemId(payload.PayloadItemGuid), payload.CountPerLaunch)).ToList();
            OutputSlotStart = Materials.Count + Payloads.Count;
            OutputSlotCount = param.OutputSlotCount;
        }

        public int PayloadSlot(int payloadIndex)
        {
            return Materials.Count + payloadIndex;
        }

        public bool IsOutputSlot(int slot)
        {
            return slot >= OutputSlotStart;
        }

        public RocketLaunchSlotRequirement RequirementAt(int inputSlot)
        {
            return inputSlot < Materials.Count ? Materials[inputSlot] : Payloads[inputSlot - Materials.Count];
        }

        // 資材と積荷が同じアイテムにならないことはマスタ検証が保証する
        // Master validation guarantees no item is both a material and a payload
        public bool TryGetInputSlot(ItemId itemId, out int slot)
        {
            for (var i = 0; i < OutputSlotStart; i++)
            {
                if (RequirementAt(i).ItemId != itemId) continue;
                slot = i;
                return true;
            }
            slot = 0;
            return false;
        }
    }

    public readonly struct RocketLaunchSlotRequirement
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public RocketLaunchSlotRequirement(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
```

- [ ] **Step 2: インベントリを書く**

`CleanRoomAirFilterInventoryComponent` と同じく全メンバーを `OpenableInventoryItemDataStoreService` へ委譲し、`CheckDestroy(this)` を各メンバー先頭で呼ぶ。違いは次の4メンバーだけ:

```csharp
        // 入れ口は割り当てたアイテムだけ、出力口は取り出し専用
        // Input slots accept only their assigned item; output slots are take-out only
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            if (itemStack.Count == 0) return true;
            if (_layout.IsOutputSlot(slot)) return false;
            return _layout.RequirementAt(slot).ItemId == itemStack.Id;
        }

        public IItemStack InsertItem(IItemStack itemStack) => InsertIntoInputSlot(itemStack);
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) => InsertIntoInputSlot(itemStack);
        public IItemStack InsertItem(ItemId itemId, int count) => InsertIntoInputSlot(ServerContext.ItemStackFactory.Create(itemId, count));

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return itemStacks.Select(InsertIntoInputSlot).Where(remainder => remainder.Count > 0).ToList();
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);

            // 書き込まずに、各スタックが自分の入れ口へ収まりきるかを順に試す
            // Without writing, try whether each stack fully fits its own input slot in order
            var simulated = new Dictionary<int, IItemStack>();
            foreach (var itemStack in itemStacks)
            {
                if (!_layout.TryGetInputSlot(itemStack.Id, out var slot)) return false;
                var current = simulated.TryGetValue(slot, out var stack) ? stack : _slots.GetItem(slot);
                var result = current.AddItem(itemStack);
                if (result.RemainderItemStack.Count > 0) return false;
                simulated[slot] = result.ProcessResultItemStack;
            }
            return true;
        }

        private IItemStack InsertIntoInputSlot(IItemStack itemStack)
        {
            CheckDestroy(this);

            // silent-ok: 入れ口の無い品は呼び出し元へそのまま返り、ベルト上や手元に残る / プレイヤーが入らないことを目視で気づく
            if (!_layout.TryGetInputSlot(itemStack.Id, out var slot)) return itemStack;
            var result = _slots.GetItem(slot).AddItem(itemStack);
            _slots.SetItem(slot, result.ProcessResultItemStack);
            return result.RemainderItemStack;
        }
```

ctor は `(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)` で2つを保持する。`IsDestroy`/`Destroy()` は前例どおり。

- [ ] **Step 3: 積荷と状態の型を書く**

`RocketCargo.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Context;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 建造中・発射の合図待ちのロケットに積んだ積荷と資材。打ち上げで消費される
    // Payload and materials loaded into a rocket being built or awaiting launch; consumed by the launch
    public class RocketCargo
    {
        public readonly IItemStack Payload;
        public readonly IReadOnlyList<IItemStack> Materials;
        public bool IsLoaded => Payload.Count > 0;

        public RocketCargo(IItemStack payload, IReadOnlyList<IItemStack> materials)
        {
            Payload = payload;
            Materials = materials;
        }

        public static RocketCargo CreateNone()
        {
            return new RocketCargo(ServerContext.ItemStackFactory.CreatEmpty(), Array.Empty<IItemStack>());
        }

        public IEnumerable<IItemStack> AllItems()
        {
            return IsLoaded ? Materials.Prepend(Payload) : Enumerable.Empty<IItemStack>();
        }
    }
}
```

`State/RocketLaunchPadPhase.cs`（`AwaitingBuildCommand`・`AwaitingLaunchCommand` は初打ち上げ前だけ現れる、と各値に日英コメント）、`State/RocketLaunchPadWaitReason.cs`、`State/RocketLaunchPadCommand.cs`、`State/RocketLaunchPadCommandResult.cs` は Interfaces 節の値をそのまま並べた enum（名前空間 `Game.Block.Blocks.RocketLaunchPad.State`）。

`State/RocketLaunchPadStateDetail.cs`（`TrainPlatformTransferStateDetail` と同形）:

```csharp
using System;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Blocks.RocketLaunchPad.State
{
    // 発射台の段階・待ち理由・進み具合をクライアントへ送る
    // Sends the launch pad's phase, wait reason and progress to the client
    [MessagePackObject]
    public class RocketLaunchPadStateDetail
    {
        public const string BlockStateDetailKey = "RocketLaunchPad";

        [Key(0)] public RocketLaunchPadPhase Phase { get; set; }
        [Key(1)] public RocketLaunchPadWaitReason WaitReason { get; set; }
        [Key(2)] public float Progress { get; set; }

        public RocketLaunchPadStateDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)
        {
            Phase = phase;
            WaitReason = waitReason;
            Progress = progress;
        }

        public static BlockStateDetail CreateState(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)
        {
            var stateDetail = new RocketLaunchPadStateDetail(phase, waitReason, progress);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RocketLaunchPadStateDetail() { }
    }
}
```

- [ ] **Step 4: インベントリのテストを書く（実行は Task 5 Step 6）**

`Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadInventoryTest.cs`。`ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.RocketLaunchPadId, ...)` で台を置き（既存 CombinedTest の設置書式に合わせる）、`IBlockInventory` と `IOpenableBlockInventoryComponent` を取って次を確かめる:
- `InsertItem(TestRocketFuel×5)` が資材スロット1（2番目）に入り、余りが空。
- `InsertItem(TestComputeData×1)` はそのまま返る（入れ口が無い）。
- `IsAllowedToPlace(出力スロット, 任意の品)` が false、`IsAllowedToPlace(0, TestRocketFuel)` が false（スロット0は筐体素材専用）、`IsAllowedToPlace(0, TestRocketCase)` が true。
- `InsertionCheck([TestRocketCase×99, TestRocketCase×2])` が false（スタック100を超える）、`[TestRocketCase×99, TestSatellitePayload×1]` が true。
- スロット数が `3（資材）+2（積荷）+2（出力）=7`。

- [ ] **Step 5: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad
git commit -m "feat(block): 発射台のスロット並びとインベントリと状態の型を足す"
```

---

### Task 5: 発射台の進行・電力・保留・セーブとテンプレート

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadCargoLoader.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadOutputBuffer.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadProcessorComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadElectricComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/RocketLaunchPadSaveJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/State/RocketLaunchPadSaveConverter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`（`BlockTypesDictionary` に `RocketLaunchPad`）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs:121-126`（`IGetRefundItemsInfo` を全件合算）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadRemoveRefundTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadLaunchFlowTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadSaveLoadTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadTestUtil.cs`

**Interfaces:**
- Consumes: Task 2 の `IOrbitalArrivalService`・`IRocketLaunchHistory`、Task 4 の全型
- Produces:
  - `RocketLaunchPadProcessorComponent`（`RocketLaunchPadPhase Phase { get; private set; }`・`RocketLaunchPadWaitReason WaitReason { get; private set; }`・`float RequestPower`・`void SupplyPower(float power)`・`RocketLaunchPadCommandResult RequestBuild()`・`RocketLaunchPadCommandResult RequestLaunch()`、`IUpdatableBlockComponent`・`IBlockStateObservable`・`IBlockSaveState`・`IGetRefundItemsInfo`）
  - `RocketLaunchPadElectricComponent : IElectricConsumer, IElectricTickPostHandler`
  - `VanillaRocketLaunchPadTemplate : IBlockTemplate`
  - テスト用 `RocketLaunchPadTestUtil`（`static IBlock PlacePoweredPad(Vector3Int position, float generatePower)`・`static void FillOneLaunch(IBlock pad, ItemId payloadItemId, int payloadCount)`・`static bool RunUntilPhase(IBlock pad, RocketLaunchPadPhase until, int maxTicks)`。Task 6 のテストも使う）

- [ ] **Step 1: 積み込みを書く**

`RocketLaunchPadCargoLoader.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.RocketLaunchPad.State;
using Game.Context;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 入れ口から1便ぶんの資材と積荷を確かめ、取り出してロケットに積む
    // Checks and takes one launch worth of materials and payload from the input slots into a rocket
    public class RocketLaunchPadCargoLoader
    {
        public int NextPayloadIndex { get; private set; }

        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;

        public RocketLaunchPadCargoLoader(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            _layout = layout;
            _slots = slots;
        }

        public void SetNextPayloadIndex(int nextPayloadIndex)
        {
            // 積荷表の行数がセーブ時から変わっても範囲内に収める
            // Keep it in range even if the payload table changed since the save
            NextPayloadIndex = nextPayloadIndex % _layout.Payloads.Count;
        }

        public RocketLaunchPadWaitReason CheckReady()
        {
            for (var i = 0; i < _layout.Materials.Count; i++)
                if (!HasRequirement(i, _layout.Materials[i])) return RocketLaunchPadWaitReason.MissingMaterials;
            return TryFindReadyPayloadIndex(out _) ? RocketLaunchPadWaitReason.None : RocketLaunchPadWaitReason.MissingPayload;
        }

        public RocketCargo LoadCargo()
        {
            // 揃っていないのに呼ばれたら呼び出し側の段階管理の誤り
            // Being called while not ready is a phase-management bug in the caller
            if (!TryFindReadyPayloadIndex(out var payloadIndex) || CheckReady() != RocketLaunchPadWaitReason.None)
                throw new InvalidOperationException("1便ぶんの資材と積荷が揃っていない発射台で建造が始まりました");

            var materials = new List<IItemStack>();
            for (var i = 0; i < _layout.Materials.Count; i++) materials.Add(TakeRequirement(i, _layout.Materials[i]));
            var payload = TakeRequirement(_layout.PayloadSlot(payloadIndex), _layout.Payloads[payloadIndex]);

            // 次の便は今回の次の積荷から探し、特定の積荷だけが飛び続けないようにする
            // Search the next launch from the payload after this one so no single payload starves the others
            NextPayloadIndex = (payloadIndex + 1) % _layout.Payloads.Count;
            return new RocketCargo(payload, materials);
        }

        private bool TryFindReadyPayloadIndex(out int payloadIndex)
        {
            var count = _layout.Payloads.Count;
            for (var offset = 0; offset < count; offset++)
            {
                var index = (NextPayloadIndex + offset) % count;
                if (!HasRequirement(_layout.PayloadSlot(index), _layout.Payloads[index])) continue;
                payloadIndex = index;
                return true;
            }
            payloadIndex = 0;
            return false;
        }

        private bool HasRequirement(int slot, RocketLaunchSlotRequirement requirement)
        {
            var stack = _slots.GetItem(slot);
            return stack.Id == requirement.ItemId && stack.Count >= requirement.Count;
        }

        private IItemStack TakeRequirement(int slot, RocketLaunchSlotRequirement requirement)
        {
            _slots.SetItem(slot, _slots.GetItem(slot).SubItem(requirement.Count));
            return ServerContext.ItemStackFactory.Create(requirement.ItemId, requirement.Count);
        }
    }
}
```

- [ ] **Step 2: 成果の保留を書く**

`RocketLaunchPadOutputBuffer.cs`:

```csharp
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 打ち上げ成果を出力口へ入りきるまで預かり、出力口の中身を接続先へ流す
    // Holds arrival outputs until they fit the output slots and pushes output slot contents to connected inventories
    public class RocketLaunchPadOutputBuffer
    {
        public bool HasPending => _pending.Count > 0;
        public IReadOnlyList<IItemStack> PendingItems => _pending;

        private readonly List<IItemStack> _pending = new();
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly IBlockInventoryInserter _beltOutput;

        public RocketLaunchPadOutputBuffer(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput)
        {
            _layout = layout;
            _slots = slots;
            _beltOutput = beltOutput;
        }

        public void AddPending(IEnumerable<IItemStack> items)
        {
            foreach (var item in items)
                if (item.Count > 0) _pending.Add(item);
        }

        public void Flush()
        {
            MovePendingIntoOutputSlots();

            // 出力口の中身は機械の出力と同じく毎tick接続先へ流す
            // Push output slot contents to connected inventories every tick, as machine outputs do
            for (var slot = _layout.OutputSlotStart; slot < _layout.TotalSlotCount; slot++)
                _slots.SetItem(slot, _beltOutput.InsertItem(_slots.GetItem(slot)));
        }

        private void MovePendingIntoOutputSlots()
        {
            if (_pending.Count == 0) return;

            // 先に届いた成果から順に出力口の空きへ詰める
            // Fill the output slots with the earliest pending outputs first
            var remaining = new List<IItemStack>();
            foreach (var item in _pending)
            {
                var rest = item;
                for (var slot = _layout.OutputSlotStart; slot < _layout.TotalSlotCount && rest.Count > 0; slot++)
                {
                    var result = _slots.GetItem(slot).AddItem(rest);
                    _slots.SetItem(slot, result.ProcessResultItemStack);
                    rest = result.RemainderItemStack;
                }
                if (rest.Count > 0) remaining.Add(rest);
            }
            _pending.Clear();
            _pending.AddRange(remaining);
        }
    }
}
```

- [ ] **Step 3: 進行を書く**

`RocketLaunchPadProcessorComponent.cs`（200行以内。セーブ形の組み立ては Step 4 の `RocketLaunchPadSaveConverter` へ出す）:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.RocketLaunchPad.State;
using Game.Block.Blocks.Util;
using Game.Block.Interface.Component;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    /// <summary>
    ///     資材と積荷からロケットを建造して打ち上げ、成果を出力口へ流す
    ///     Builds a rocket from materials and payload, launches it, and routes the arrival outputs to the output slots
    /// </summary>
    public class RocketLaunchPadProcessorComponent : IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo
    {
        public RocketLaunchPadPhase Phase { get; private set; }
        public RocketLaunchPadWaitReason WaitReason { get; private set; }
        public float RequestPower => Phase == RocketLaunchPadPhase.Building ? _requiredPower : 0f;
        public IObservable<Unit> OnChangeBlockState => _changeState;
        public string SaveKey { get; } = typeof(RocketLaunchPadProcessorComponent).FullName;
        public bool IsDestroy { get; private set; }

        private readonly Subject<Unit> _changeState = new();
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly RocketLaunchPadCargoLoader _cargoLoader;
        private readonly RocketLaunchPadOutputBuffer _outputBuffer;
        private readonly IOrbitalArrivalService _arrivalService;
        private readonly IRocketLaunchHistory _launchHistory;
        private readonly IDisposable _firstLaunchSubscription;
        private readonly float _requiredPower;
        private readonly uint _buildTotalTicks;
        private readonly uint _launchTotalTicks;

        private RocketCargo _cargo = RocketCargo.CreateNone();
        private uint _remainingTicks;
        private float _suppliedPower;

        public RocketLaunchPadProcessorComponent(RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, IRocketLaunchHistory launchHistory)
        {
            _layout = layout;
            _slots = slots;
            _cargoLoader = new RocketLaunchPadCargoLoader(layout, slots);
            _outputBuffer = new RocketLaunchPadOutputBuffer(layout, slots, beltOutput);
            _arrivalService = arrivalService;
            _launchHistory = launchHistory;
            _requiredPower = param.RequiredPower;
            _buildTotalTicks = GameUpdater.SecondsToTicks(param.BuildSeconds);
            _launchTotalTicks = GameUpdater.SecondsToTicks(param.LaunchSeconds);

            // ワールドの初打ち上げで、発射の合図を待っている台も自動で打ち上げる
            // On the world's first launch, pads awaiting a launch command launch automatically too
            _firstLaunchSubscription = launchHistory.OnFirstLaunch.Subscribe(_ => OnWorldFirstLaunch());
        }

        public RocketLaunchPadProcessorComponent(Dictionary<string, object> componentStates, RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, IRocketLaunchHistory launchHistory)
            : this(param, layout, slots, beltOutput, arrivalService, launchHistory)
        {
            // セーブの段階・積荷・スロット・保留を戻し、段階と積荷の食い違いはここで止める
            // Restore phase, cargo, slots and pending outputs, and stop here on a phase/cargo mismatch
            var saveData = BlockComponentStateReader.Read<RocketLaunchPadSaveJsonObject>(componentStates, SaveKey);
            Phase = saveData.Phase;
            _remainingTicks = saveData.RemainingTicks;
            _cargo = RocketLaunchPadSaveConverter.RestoreCargo(saveData);
            _cargoLoader.SetNextPayloadIndex(saveData.NextPayloadIndex);
            _outputBuffer.AddPending(RocketLaunchPadSaveConverter.RestoreSlots(saveData, _layout, _slots));
            RocketLaunchPadSaveConverter.ThrowIfInconsistent(Phase, _remainingTicks, _cargo);
        }

        public void SupplyPower(float power)
        {
            // 複数の電力網から供給され得るため加算し、次のUpdateで使い切る
            // Accumulate since several networks may supply this pad, then consume it on the next update
            _suppliedPower += power;
        }

        public void Update()
        {
            CheckDestroy(this);
            var previousPhase = Phase;
            var previousReason = WaitReason;
            var tickPower = _suppliedPower;
            _suppliedPower = 0f;

            // 保留中の成果を出力口へ、出力口の中身を接続先へ流す
            // Move pending outputs into the output slots and push output contents onward
            _outputBuffer.Flush();

            switch (Phase)
            {
                case RocketLaunchPadPhase.Idle:
                case RocketLaunchPadPhase.AwaitingBuildCommand:
                    UpdateWaitingForCargo();
                    break;
                case RocketLaunchPadPhase.Building:
                    UpdateBuilding(tickPower);
                    break;
                case RocketLaunchPadPhase.Launching:
                    UpdateLaunching();
                    break;
            }

            // 段階か待ち理由が変わったとき、進行中は毎tick状態を流す（機械の進捗と同じ）
            // Publish on a phase or reason change, and every tick while progressing (as machine progress does)
            var isProgressing = Phase is RocketLaunchPadPhase.Building or RocketLaunchPadPhase.Launching;
            if (previousPhase != Phase || previousReason != WaitReason || isProgressing) _changeState.OnNext(Unit.Default);
        }

        public RocketLaunchPadCommandResult RequestBuild()
        {
            CheckDestroy(this);
            if (Phase != RocketLaunchPadPhase.AwaitingBuildCommand) return Reject(RocketLaunchPadCommandResult.NotAwaitingBuildCommand);
            if (_cargoLoader.CheckReady() != RocketLaunchPadWaitReason.None) return Reject(RocketLaunchPadCommandResult.CargoNotReady);
            StartBuilding();
            _changeState.OnNext(Unit.Default);
            return RocketLaunchPadCommandResult.Accepted;
        }

        public RocketLaunchPadCommandResult RequestLaunch()
        {
            CheckDestroy(this);
            if (Phase != RocketLaunchPadPhase.AwaitingLaunchCommand) return Reject(RocketLaunchPadCommandResult.NotAwaitingLaunchCommand);
            StartLaunching();
            _changeState.OnNext(Unit.Default);
            return RocketLaunchPadCommandResult.Accepted;
        }

        private void UpdateWaitingForCargo()
        {
            // 成果が出力口に入りきらない間は次の便を建造しない
            // Do not build the next rocket while arrival outputs are still pending
            WaitReason = _outputBuffer.HasPending ? RocketLaunchPadWaitReason.OutputPending : _cargoLoader.CheckReady();
            if (WaitReason != RocketLaunchPadWaitReason.None)
            {
                Phase = RocketLaunchPadPhase.Idle;
                return;
            }

            // 初打ち上げ前のワールドではプレイヤーの建造の合図を待つ
            // Before the world's first launch, wait for the player's build command
            if (_launchHistory.HasLaunchedOnce) StartBuilding();
            else Phase = RocketLaunchPadPhase.AwaitingBuildCommand;
        }

        private void UpdateBuilding(float tickPower)
        {
            // 電力の供給率だけ建造を進める
            // Advance the build by the power supply rate
            WaitReason = tickPower <= 0f ? RocketLaunchPadWaitReason.NoPower : RocketLaunchPadWaitReason.None;
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(tickPower, _requiredPower);
            _remainingTicks -= Math.Min(subTicks, _remainingTicks);
            if (_remainingTicks > 0) return;

            // 初打ち上げ前は発射の合図を待ち、以後は続けて打ち上げる
            // Before the first launch wait for the launch command, otherwise launch right away
            WaitReason = RocketLaunchPadWaitReason.None;
            if (_launchHistory.HasLaunchedOnce) StartLaunching();
            else Phase = RocketLaunchPadPhase.AwaitingLaunchCommand;
        }

        private void UpdateLaunching()
        {
            _remainingTicks--;
            if (_remainingTicks == 0) Phase = RocketLaunchPadPhase.Idle;
        }

        private void StartBuilding()
        {
            _cargo = _cargoLoader.LoadCargo();
            _remainingTicks = _buildTotalTicks;
            Phase = RocketLaunchPadPhase.Building;
            WaitReason = RocketLaunchPadWaitReason.None;
        }

        private void StartLaunching()
        {
            // 打ち上げ開始が軌道到着（CONTEXT.md）。段階を先に変え、初打ち上げ通知で自分が二重に打ち上げないようにする
            // Launch start is orbital arrival (CONTEXT.md); switch phase first so the first-launch notice cannot relaunch this pad
            Phase = RocketLaunchPadPhase.Launching;
            WaitReason = RocketLaunchPadWaitReason.None;
            _remainingTicks = _launchTotalTicks;
            var cargo = _cargo;
            _cargo = RocketCargo.CreateNone();
            _outputBuffer.AddPending(_arrivalService.Arrive(cargo.Payload.Id, cargo.Payload.Count));
            _launchHistory.RecordLaunch();
        }

        private void OnWorldFirstLaunch()
        {
            // 建造の合図待ちは次のUpdateで自動建造へ進むので、ここでは発射の合図待ちだけを動かす
            // Pads awaiting a build command auto-build on their next update, so only awaiting-launch pads move here
            if (Phase != RocketLaunchPadPhase.AwaitingLaunchCommand) return;
            StartLaunching();
            _changeState.OnNext(Unit.Default);
        }

        private RocketLaunchPadCommandResult Reject(RocketLaunchPadCommandResult result)
        {
            Debug.LogWarning($"ロケット発射台への操作を拒否しました 理由:{result} 段階:{Phase} 待ち理由:{WaitReason}");
            return result;
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            CheckDestroy(this);
            return new[] { RocketLaunchPadStateDetail.CreateState(Phase, WaitReason, CalculateProgress()) };

            #region Internal

            float CalculateProgress()
            {
                // 建造中・打ち上げ中だけ0〜1の進み具合を持ち、発射の合図待ちは建造完了として1
                // Only building and launching carry 0..1 progress; awaiting launch counts as a finished build
                return Phase switch
                {
                    RocketLaunchPadPhase.Building => 1f - _remainingTicks / (float)_buildTotalTicks,
                    RocketLaunchPadPhase.Launching => 1f - _remainingTicks / (float)_launchTotalTicks,
                    RocketLaunchPadPhase.AwaitingLaunchCommand => 1f,
                    _ => 0f,
                };
            }

            #endregion
        }

        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            // 積んだまま打ち上げていないロケットの中身と、出力口に入りきらない成果を返す
            // Refund the contents of a not-yet-launched rocket and outputs that never fit the output slots
            return _cargo.AllItems().Concat(_outputBuffer.PendingItems).ToList();
        }

        public object GetSaveState()
        {
            CheckDestroy(this);
            return RocketLaunchPadSaveConverter.Capture(Phase, _remainingTicks, _cargo, _cargoLoader.NextPayloadIndex, _outputBuffer.PendingItems, _layout, _slots);
        }

        public void Destroy()
        {
            IsDestroy = true;
            _firstLaunchSubscription.Dispose();
        }
    }
}
```

（200行を超えたら、`GetBlockStateDetails` の進み具合計算を `State/RocketLaunchPadProgress.cs` の static メソッドへ出す。）

- [ ] **Step 4: セーブ形と変換を書く**

`State/RocketLaunchPadSaveJsonObject.cs`:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Game.Block.Blocks.RocketLaunchPad.State
{
    // 入れ口はアイテムで、出力口は並びで保存する。積荷表が変わってもスロットの意味がずれないようにするため
    // Inputs are saved by item and outputs by order, so slot meaning survives a payload table change
    public class RocketLaunchPadSaveJsonObject
    {
        [JsonProperty("phase"), JsonConverter(typeof(StringEnumConverter))] public RocketLaunchPadPhase Phase;
        [JsonProperty("remainingTicks")] public uint RemainingTicks;
        [JsonProperty("cargoPayload")] public ItemStackSaveJsonObject CargoPayload;
        [JsonProperty("cargoMaterials")] public List<ItemStackSaveJsonObject> CargoMaterials;
        [JsonProperty("nextPayloadIndex")] public int NextPayloadIndex;
        [JsonProperty("inputItems")] public List<ItemStackSaveJsonObject> InputItems;
        [JsonProperty("outputItems")] public List<ItemStackSaveJsonObject> OutputItems;
        [JsonProperty("pendingOutputs")] public List<ItemStackSaveJsonObject> PendingOutputs;
    }
}
```

`State/RocketLaunchPadSaveConverter.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.State
{
    // 発射台のセーブ形と実行時の状態を相互に変換する
    // Converts between the launch pad's save shape and its runtime state
    public static class RocketLaunchPadSaveConverter
    {
        public static RocketLaunchPadSaveJsonObject Capture(RocketLaunchPadPhase phase, uint remainingTicks, RocketCargo cargo, int nextPayloadIndex, IReadOnlyList<IItemStack> pending, RocketLaunchPadSlotLayout layout, IOpenableInventory slots)
        {
            var items = slots.InventoryItems;
            return new RocketLaunchPadSaveJsonObject
            {
                Phase = phase,
                RemainingTicks = remainingTicks,
                CargoPayload = new ItemStackSaveJsonObject(cargo.Payload),
                CargoMaterials = cargo.Materials.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                NextPayloadIndex = nextPayloadIndex,
                InputItems = items.Take(layout.OutputSlotStart).Where(item => item.Count > 0).Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputItems = items.Skip(layout.OutputSlotStart).Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                PendingOutputs = pending.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
        }

        public static RocketCargo RestoreCargo(RocketLaunchPadSaveJsonObject saveData)
        {
            return new RocketCargo(saveData.CargoPayload.ToItemStack(), saveData.CargoMaterials.Select(item => item.ToItemStack()).ToList());
        }

        // スロットをイベント無しで戻し、今の並びに置き場の無い品を保留として返す
        // Restore slots without events and return items that have no place in the current layout as pending
        public static List<IItemStack> RestoreSlots(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            var displaced = saveData.PendingOutputs.Select(item => item.ToItemStack()).ToList();
            foreach (var input in saveData.InputItems.Select(item => item.ToItemStack()))
            {
                if (layout.TryGetInputSlot(input.Id, out var slot)) slots.SetItemWithoutEvent(slot, input);
                else displaced.Add(input);
            }
            var outputs = saveData.OutputItems.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < outputs.Count; i++)
            {
                if (i < layout.OutputSlotCount) slots.SetItemWithoutEvent(layout.OutputSlotStart + i, outputs[i]);
                else displaced.Add(outputs[i]);
            }

            // マスタ変更で入れ口・出力口が減ったときは品を消さず出力の保留へ回す
            // When a master change removed slots, keep the items by moving them to pending outputs
            var movedCount = displaced.Count - saveData.PendingOutputs.Count;
            if (movedCount > 0) Debug.LogWarning($"発射台のスロット構成がセーブ時と違うため {movedCount} 件の品を出力の保留へ移しました");
            return displaced.Where(item => item.Count > 0).ToList();
        }

        public static void ThrowIfInconsistent(RocketLaunchPadPhase phase, uint remainingTicks, RocketCargo cargo)
        {
            // 積荷を持つのは建造中と発射の合図待ちだけ、残りtickを持つのは建造中と打ち上げ中だけ
            // Only building and awaiting-launch hold cargo; only building and launching hold remaining ticks
            var expectsCargo = phase is RocketLaunchPadPhase.Building or RocketLaunchPadPhase.AwaitingLaunchCommand;
            var expectsTicks = phase is RocketLaunchPadPhase.Building or RocketLaunchPadPhase.Launching;
            if (cargo.IsLoaded == expectsCargo && (remainingTicks > 0) == expectsTicks) return;

            var reason = $"発射台のセーブが壊れています 段階:{phase} 積荷あり:{cargo.IsLoaded} 残りtick:{remainingTicks}";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
```

- [ ] **Step 5: 電力とテンプレートを書く**

`RocketLaunchPadElectricComponent.cs`（`VanillaElectricMachineComponent` と同形）:

```csharp
using Game.Block.Interface;
using Game.EnergySystem;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 建造中だけ電力を要求し、供給された量を進行へ渡す
    // Requests power only while building and hands the supplied amount to the processor
    public class RocketLaunchPadElectricComponent : IElectricConsumer, IElectricTickPostHandler
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_processor.RequestPower);
        public bool IsDestroy { get; private set; }

        private readonly RocketLaunchPadProcessorComponent _processor;

        public RocketLaunchPadElectricComponent(BlockInstanceId blockInstanceId, RocketLaunchPadProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            _processor = processor;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            CheckDestroy(this);
            _processor.SupplyPower(_processor.RequestPower * statistics.PowerRate);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

`Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs`:

```csharp
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.Service;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Rocket
{
    /// <summary>
    ///     ロケット発射台（資材・積荷・出力スロットと電力）のテンプレート
    ///     Template for the rocket launch pad with material, payload and output slots and power
    /// </summary>
    public class VanillaRocketLaunchPadTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static IBlock Create(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (RocketLaunchPadBlockParam)blockMasterElement.BlockParam;
            var layout = new RocketLaunchPadSlotLayout(param);

            // 全スロットを1つの保管に持ち、入れ口と出力口の制限はインベントリ側で掛ける
            // Keep every slot in one store and apply input/output restrictions in the inventory component
            var slots = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, layout.TotalSlotCount);
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var beltOutput = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector);
            var arrivalService = ServerContext.GetService<IOrbitalArrivalService>();
            var launchHistory = ServerContext.GetService<IRocketLaunchHistory>();

            var processor = componentStates == null
                ? new RocketLaunchPadProcessorComponent(param, layout, slots, beltOutput, arrivalService, launchHistory)
                : new RocketLaunchPadProcessorComponent(componentStates, param, layout, slots, beltOutput, arrivalService, launchHistory);
            var electric = new RocketLaunchPadElectricComponent(blockInstanceId, processor);
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, electric, componentStates);

            var components = new List<IBlockComponent>
            {
                connector,
                new RocketLaunchPadInventoryComponent(layout, slots),
                electric,
                wireConnector,
                processor,
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);

            #region Internal

            void InvokeEvent(int slot, IItemStack itemStack)
            {
                var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
                blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(blockInstanceId, slot, itemStack));
            }

            #endregion
        }
    }
}
```

（`using` の名前空間は既存テンプレート `VanillaCleanRoomAirFilterTemplate`・機械テンプレートの実物に合わせる。`ConnectingInventoryListPriorityInsertItemService` の名前空間は `Game.Block.Blocks.Service`。）

`VanillaIBlockTemplates.cs` の `BlockTypesDictionary` 構築に、既存の追加行と同じ書式で `RocketLaunchPad` → `new VanillaRocketLaunchPadTemplate()` を足す。

`RemoveBlockProtocol.cs` の `GetRefundItems` ローカル関数で、`TryGetComponent(out IGetRefundItemsInfo refundInfo)` の if ブロック（121〜126行）を次へ置き換える。発射台は電線（`ElectricWireConnectorComponent` も `IGetRefundItemsInfo`）と進行の両方が返却を持つため、先頭1件だけでは片方が消える。既存テンプレートで返却を持つ部品が2つ以上あるブロックは無い（電線つきテンプレートとベルト・チェーンポール・フィルター分配器・クリーンルーム境界のテンプレートは重ならない）ので、既存ブロックの返却量は変わらない:

```csharp
                // 返却を持つ部品すべてを合算する。電線と発射台の進行のように1ブロックに複数あり得る
                // Sum every component that holds refunds; a block may have several, such as wires and a launch pad processor
                foreach (var refundInfo in block.ComponentManager.GetComponents<IGetRefundItemsInfo>())
                {
                    result.AddRange(refundInfo.GetRefundItems());
                }
```

- [ ] **Step 6: 進行のテストを書く**

`RocketLaunchPadTestUtil.cs`（テスト用。`ElectricWireTestUtil.WirePower(consumerPos, polePos, generatePower)` で給電する）:

```csharp
using Core.Master;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Tests.Module.TestMod;
using Tests.Util.EnergySystem;
using UnityEngine;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public static class RocketLaunchPadTestUtil
    {
        public static IBlock PlacePoweredPad(Vector3Int position, float generatePower)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.RocketLaunchPadId, position, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var pad);
            ElectricWireTestUtil.WirePower(position, position + new Vector3Int(3, 0, 0), generatePower);
            return pad;
        }

        public static void FillOneLaunch(IBlock pad, ItemId payloadItemId, int payloadCount)
        {
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.InsertItem(ForUnitTestItemId.RocketCaseId, 2);
            inventory.InsertItem(ForUnitTestItemId.RocketFuelId, 3);
            inventory.InsertItem(ForUnitTestItemId.RocketComputerId, 1);
            inventory.InsertItem(payloadItemId, payloadCount);
        }

        // 指定の段階になるまで tick を回し、なったかを返す
        // Run ticks until the given phase and report whether it was reached
        public static bool RunUntilPhase(IBlock pad, RocketLaunchPadPhase until, int maxTicks)
        {
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            for (var i = 0; i < maxTicks; i++)
            {
                if (processor.Phase == until) return true;
                GameUpdater.RunFrames(1);
            }
            return processor.Phase == until;
        }
    }
}
```

（`TryAddBlock` の引数・`GameUpdater.RunFrames` の実名・`ElectricWireTestUtil.WirePower` の引数は既存 CombinedTest の実物に合わせる。tickを回す既存ヘルパが別名ならそれを使う。）

`RocketLaunchPadLaunchFlowTest.cs` に次のテストを書く（各テストは DI を作り直す。`IRocketLaunchHistory` は `ServerContext.GetService` で取る）:

1. `初打ち上げ前は建造と発射の合図を待つ`: 給電した台に1便ぶん入れて `RunUntilPhase(AwaitingBuildCommand, 5)` が true、資材が減っていない。`RequestBuild()` が `Accepted`、資材と積荷が消える。`RunUntilPhase(AwaitingLaunchCommand, 40)` が true。`RequestLaunch()` が `Accepted`、`HasLaunchedOnce` が true、出力スロット2つに TestComputeData が合計150（100＋50）入り、保留は空。
2. `合図待ちでない台への操作は拒否する`: 空の台で `RequestBuild()` が `NotAwaitingBuildCommand`、`RequestLaunch()` が `NotAwaitingLaunchCommand`（`LogAssert.Expect(LogType.Warning, new Regex("ロケット発射台への操作を拒否しました"))` を2回）。合図待ちの台から資材を1つ抜いて `RequestBuild()` が `CargoNotReady`。
3. `初打ち上げ後は自動で建造し打ち上げる`: `history.RecordLaunch()` してから1便ぶん入れ、`RunUntilPhase(Launching, 40)` が true（`AwaitingBuildCommand` と `AwaitingLaunchCommand` を経ない）。
4. `別の台の初打ち上げで発射の合図待ちの台が打ち上がる`: 台Aと台Bを置き、両方を `AwaitingLaunchCommand` まで進め、Aで `RequestLaunch()`。同じtick内でBの `Phase` が `Launching`。
5. `電力が半分なら建造が倍かかる`: 要求100に対し発電50の台と発電100の台で、`Building` から `Launching` までのtick数の比が 1.6〜2.4（確率的な丸めのため幅を持たせる）。発電0では40tick後も `Building` のまま `WaitReason == NoPower`。
6. `成果が入りきらない間は次の便を建造しない`: 自動モードで衛星を2便ぶん入れ、1便目の成果150のうち出力2スロットに入りきって保留が0のことを確認、続けて2便目が打ち上がると出力が満杯（100＋100）で保留100（成果150のうち50だけが2つ目の出力スロットに入る）が残り、`Launching` 終了後 `Idle` かつ `WaitReason == OutputPending` で3便目を建造しない。出力スロットを空にすると次のtickで保留が流れ、建造が始まる。
7. `揃った積荷は交互に選ぶ`: 自動モードで衛星1便ぶんとケーブル1便ぶん（5個）を同時に入れ、1便目と2便目で `OnOrbitalArrival` の積荷が異なる。
8. `撤去で積んだ資材と保留を返す`: 合図待ちで `RequestBuild()` した台の `GetRefundItems()` に資材3種と積荷が入る。打ち上げ中（`Launching`）の台の `GetRefundItems()` は保留だけ。
9. `資材が無いときの最小構成`: 空の台は `Idle`・`MissingMaterials`。資材だけ入れると `MissingPayload`。

`Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadRemoveRefundTest.cs`（`RemoveBlockRefundTest` の `CreateServer`・`GetInventory`・`GetItemCount`・`CreateRemovePayload` を同ファイル内に写す）:
- `建造中の台を撤去すると積んだ資材と積荷が戻る`: `PlacePoweredPad`・`FillOneLaunch(pad, SatellitePayloadId, 1)` → `RunUntilPhase(AwaitingBuildCommand, 5)` → `RequestBuild()` の後、`RemoveBlockProtocol` で撤去し、プレイヤーのインベントリに TestRocketCase×2・TestRocketFuel×3・TestRocketComputer×1・TestSatellitePayload×1 が入る。テンプレートの部品順は電線が進行より前なので、修正前の `TryGetComponent`（先頭1件）では電線の返却しか読まれずこのテストが落ちる。
- `保留中の成果も撤去で戻る`: 自動モードで出力2スロットを満杯にして保留を作った台を撤去し、出力スロットの中身と保留の両方がインベントリに入る（インベントリの空きが足りるよう、テスト前にプレイヤーのインベントリを空にしておく）。

`RocketLaunchPadSaveLoadTest.cs`: `CleanRoomSaveLoadTest` の書き方で、(a) `Building` 途中、(b) `AwaitingLaunchCommand`、(c) 保留あり、の3状態でセーブ→ロードし、段階・残りtick・積荷・入れ口・出力口・保留・`NextPayloadIndex` が同じであること、ロード後に進行が続くこと。さらに (d) 保存した `inputItems` に入れ口の無いアイテム（TestComputeData）を書き足したJSONをロードすると、そのアイテムが保留に入り `LogAssert.Expect(LogType.Warning, new Regex("スロット構成がセーブ時と違う"))` が出ること。(e) `phase` を `Idle` にして `cargoPayload` を残した壊れたJSONのロードが `InvalidOperationException` になること。

- [ ] **Step 7: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.CombinedTest\.Game\.RocketLaunchPad\.|RocketLaunchPadRemoveRefundTest|RemoveBlockRefundTest|RemoveBlockProtocolTest|BeltConveyorRemovalTest"`
Expected: ErrorCount 0 / Task 4・5 の全テストと既存の撤去返却テストが PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket
git commit -m "feat(block): ロケット発射台の建造と打ち上げとセーブを足し、撤去返却を全部品で合算する"
```

---

### Task 6: 建造・発射の合図のプロトコル

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RocketLaunchPadCommandProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（`SetTrainPlatformTransferModeProtocol` の登録の次）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef`（`Game.Orbit` が要れば）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Requests/BlockRequestApi.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadCommandProtocolTest.cs`

**Interfaces:**
- Consumes: Task 5 の `RocketLaunchPadProcessorComponent.RequestBuild/RequestLaunch`、Task 4 の `RocketLaunchPadCommand`・`RocketLaunchPadCommandResult`
- Produces:
  - `RocketLaunchPadCommandProtocol.ProtocolTag = "va:rocketLaunchPadCommand"`
  - `RocketLaunchPadCommandProtocol.RocketLaunchPadCommandRequest`（`[Key(2)] Vector3IntMessagePack Position`・`[Key(3)] RocketLaunchPadCommand Command`、ctor `(Vector3Int position, RocketLaunchPadCommand command)`）
  - `RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse`（`[Key(2)] RocketLaunchPadCommandResult Result`）
  - `BlockRequestApi.SendRocketLaunchPadCommand(Vector3Int position, RocketLaunchPadCommand command, CancellationToken ct)` → `UniTask<RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse>`

- [ ] **Step 1: creating-server-protocol スキルを読み、プロトコルを書く**

```csharp
using System;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.State;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 発射台への建造・発射の合図。結果の表示はブロック状態の更新を正とする
    // Build/launch commands for a launch pad; the displayed result follows the block state update
    public class RocketLaunchPadCommandProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:rocketLaunchPadCommand";

        public RocketLaunchPadCommandProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RocketLaunchPadCommandRequest>(payload);
            return new RocketLaunchPadCommandResponse(Execute());

            #region Internal

            RocketLaunchPadCommandResult Execute()
            {
                // 指定座標の発射台を取り出す。無ければ理由を残して拒否する
                // Fetch the launch pad at the position, rejecting with a logged reason when absent
                var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
                if (block == null) return Reject(RocketLaunchPadCommandResult.BlockNotFound);
                if (!block.ComponentManager.TryGetComponent<RocketLaunchPadProcessorComponent>(out var processor)) return Reject(RocketLaunchPadCommandResult.NotRocketLaunchPad);

                // 合図の種類ごとに発射台の入口へ渡す
                // Hand each command kind to the pad's matching entry point
                return request.Command switch
                {
                    RocketLaunchPadCommand.Build => processor.RequestBuild(),
                    RocketLaunchPadCommand.Launch => processor.RequestLaunch(),
                    _ => Reject(RocketLaunchPadCommandResult.UnknownCommand),
                };
            }

            RocketLaunchPadCommandResult Reject(RocketLaunchPadCommandResult result)
            {
                Debug.LogWarning($"ロケット発射台への合図を拒否しました 理由:{result} 座標:{request.Position.Vector3Int}");
                return result;
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class RocketLaunchPadCommandRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public RocketLaunchPadCommand Command { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RocketLaunchPadCommandRequest()
            {
            }

            public RocketLaunchPadCommandRequest(Vector3Int position, RocketLaunchPadCommand command)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Command = command;
            }
        }

        [MessagePackObject]
        public class RocketLaunchPadCommandResponse : ProtocolMessagePackBase
        {
            [Key(2)] public RocketLaunchPadCommandResult Result { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RocketLaunchPadCommandResponse()
            {
            }

            public RocketLaunchPadCommandResponse(RocketLaunchPadCommandResult result)
            {
                Tag = ProtocolTag;
                Result = result;
            }
        }

        #endregion
    }
}
```

`PacketResponseCreator.cs` に `_packetResponseDictionary.Add(RocketLaunchPadCommandProtocol.ProtocolTag, new RocketLaunchPadCommandProtocol(serviceProvider));`。

`BlockRequestApi.cs` に追加（`using Game.Block.Blocks.RocketLaunchPad.State;`）:

```csharp
        // 発射台へ建造・発射の合図を送る
        // Send a build or launch command to a rocket launch pad
        public async UniTask<RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse> SendRocketLaunchPadCommand(
            Vector3Int position, RocketLaunchPadCommand command, CancellationToken ct)
        {
            var request = new RocketLaunchPadCommandProtocol.RocketLaunchPadCommandRequest(position, command);
            return await _packetExchangeManager.GetPacketResponse<RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse>(request, ct);
        }
```

- [ ] **Step 2: テストを書く**

`Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadCommandProtocolTest.cs`。`SetTrainPlatformTransferModeProtocolTest` と同じ書式で、`packet.GetPacketResponse(payload, BoundPacketContext.Bind(1))[0]` を使う（パケット送受の実物は既存 PacketTest に合わせる）。台は `RocketLaunchPadTestUtil.PlacePoweredPad`・`FillOneLaunch` で用意する:
- 合図待ちの台へ `Build` → `Accepted`、`Phase == Building`。
- `AwaitingLaunchCommand` まで進めて `Launch` → `Accepted`、`Phase == Launching`。
- 空き座標 → `BlockNotFound`、チェストへ → `NotRocketLaunchPad`、空の台へ `Build` → `NotAwaitingBuildCommand`（それぞれ `LogAssert.Expect(LogType.Warning, ...)`）。

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RocketLaunchPadCommandProtocolTest"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_client/Assets/Scripts/Client.Network moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket
git commit -m "feat(protocol): 発射台の建造と発射の合図を足す"
```

---

### Task 7: ロケット本体の表示と上昇

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Rocket/RocketLaunchPadStateChangeProcessor.cs`
- Modify（uloop経由）: `moorestech_client/Assets/AddressableResources/Block/Rocket.prefab`（ルートへ `RocketLaunchPadStateChangeProcessor` を追加し `body` を割り当てる）

**Interfaces:**
- Consumes: Task 4 の `RocketLaunchPadStateDetail`・`RocketLaunchPadPhase`、Task 1 の `RocketLaunchPadBlockParam.LaunchSeconds`
- Produces: なし（見た目のみ）

- [ ] **Step 1: 状態処理を書く**

```csharp
using Client.Game.InGame.Block;
using Game.Block.Blocks.RocketLaunchPad.State;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.Rocket
{
    // 発射台の上のロケット本体を建造完了で出し、打ち上げで昇らせて消す
    // Shows the rocket body on build completion, raises it during launch, and hides it afterwards
    public class RocketLaunchPadStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private Transform body;
        [SerializeField] private float riseHeight = 80f;

        private Vector3 _bodyRestLocalPosition;
        private float _launchSeconds;
        private float _launchElapsedSeconds;
        private bool _isLaunching;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _bodyRestLocalPosition = body.localPosition;
            _launchSeconds = ((RocketLaunchPadBlockParam)blockGameObject.BlockMasterElement.BlockParam).LaunchSeconds;
            body.gameObject.SetActive(false);
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            var detail = blockState.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);

            // 本体は発射の合図待ちと打ち上げ中だけ見せる
            // Show the body only while awaiting launch and while launching
            body.gameObject.SetActive(detail.Phase is RocketLaunchPadPhase.AwaitingLaunchCommand or RocketLaunchPadPhase.Launching);

            // 打ち上げの途中から受け取っても、サーバーの進み具合から上昇位置を合わせる
            // Even when received mid-launch, align the rise with the server's progress
            var wasLaunching = _isLaunching;
            _isLaunching = detail.Phase == RocketLaunchPadPhase.Launching;
            if (_isLaunching && !wasLaunching) _launchElapsedSeconds = detail.Progress * _launchSeconds;
            if (!_isLaunching) body.localPosition = _bodyRestLocalPosition;
        }

        private void Update()
        {
            if (!_isLaunching) return;

            // 加速して昇って見えるよう経過割合の二乗で上げる
            // Raise by the square of elapsed ratio so the ascent reads as accelerating
            _launchElapsedSeconds = Mathf.Min(_launchElapsedSeconds + Time.deltaTime, _launchSeconds);
            var ratio = _launchElapsedSeconds / _launchSeconds;
            body.localPosition = _bodyRestLocalPosition + Vector3.up * (riseHeight * ratio * ratio);
        }
    }
}
```

（`Initialize` のシグネチャ・`BlockGameObject.BlockMasterElement` の参照は `GearStateChangeProcessor` と `IBlockGameObjectInnerComponent` の実物に合わせる。）

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 3: Prefab に付ける（uloop execute-dynamic-code）**

まず階層を読む:

```csharp
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/AddressableResources/Block/Rocket.prefab");
foreach (var t in prefab.GetComponentsInChildren<UnityEngine.Transform>(true)) UnityEngine.Debug.Log(UnityEditor.AnimationUtility.CalculateTransformPath(t, prefab.transform));
```

`Rocket.fbx` のノードは `Rocket`（モデルの根）・`base`・`body` なので、期待するパスは `Rocket/body`。ログのパスが違えば下の `Find` の引数をログの値に直してから、ルートへコンポーネントを足して割り当てる:

```csharp
var path = "Assets/AddressableResources/Block/Rocket.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var processor = root.AddComponent<Client.Game.InGame.BlockSystem.StateProcessor.Rocket.RocketLaunchPadStateChangeProcessor>();
var serialized = new UnityEditor.SerializedObject(processor);
serialized.FindProperty("body").objectReferenceValue = root.transform.Find("Rocket/body");
serialized.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
```

保存後にもう一度読み、`body` が null でないことをログで確かめる。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Rocket moorestech_client/Assets/AddressableResources/Block/Rocket.prefab
git commit -m "feat(client): 発射台のロケット本体の表示と上昇を足す"
```

（Unity が生成した `.meta` は一緒にコミットしてよい。）

---

### Task 8: 発射台の Web UI

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/Rocket/RocketLaunchPadDetailDto.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/Rocket/RocketLaunchPadDetailDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryDtos.cs`（`public RocketLaunchPadDetailDto RocketLaunchPad;`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`（`TrainStationDetailDtoBuilder.Apply` の次に1行）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Rocket/RocketLaunchPadCommandActionHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`（列車PFのアクション登録の次に1行）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_rocket_launch_pad.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/RocketLaunchPadWireContractTest.cs`
- Modify: `Localization/localization.csv`
- Create: `moorestech_web/webui/src/bridge/contract/schemas/train/index.ts`（`inventory.ts` から列車の schema を移す）
- Create: `moorestech_web/webui/src/bridge/contract/schemas/beltThroughput/index.ts`（`inventory.ts` からベルト統計の schema を移す）
- Create: `moorestech_web/webui/src/bridge/contract/schemas/rocketLaunchPad/index.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`、`schemas/index.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`（fixture 一覧）
- Create: `moorestech_web/webui/src/features/blockInventory/views/RocketLaunchPadInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/rocket/RocketLaunchPadSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/rocket/rocketLaunchPadLogic.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/rocket/rocketLaunchPadLogic.test.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx`
- Modify: `moorestech_web/webui/src/features/blockInventory/registry/registeredBlockTypes.ts`、`registry/blockComponentRegistry.ts`

**Interfaces:**
- Consumes: Task 4 の `RocketLaunchPadStateDetail`・`RocketLaunchPadSlotLayout`、Task 6 の `BlockRequestApi.SendRocketLaunchPadCommand`
- Produces:
  - wire `rocketLaunchPad`: `{ phase: "idle"|"awaitingBuildCommand"|"building"|"awaitingLaunchCommand"|"launching", waitReason: "none"|"missingMaterials"|"missingPayload"|"outputPending"|"noPower", progress: number(0..1), materialSlotCount: int, payloadSlotCount: int, outputSlotCount: int }`
  - action `rocket_launch_pad.command`、payload `{ command: "build" | "launch" }`
  - testid: `rocket-launch-pad-view`・`rocket-launch-pad-material-slots`・`rocket-launch-pad-payload-slots`・`rocket-launch-pad-output-slots`・`rocket-launch-pad-status`・`rocket-launch-pad-progress`・`rocket-launch-pad-build`・`rocket-launch-pad-launch`

- [ ] **Step 1: ref-webui-design スキルを読み、inventory.ts を分ける**

`inventory.ts`（215行）から、`TrainPlatformModeSchema`〜`TrainStationDetailSchema`（124〜152行）を `schemas/train/index.ts` へ、`BeltThroughputWindowDataSchema`・`BeltThroughputDataSchema`（156〜169行）を `schemas/beltThroughput/index.ts` へそのまま移し（`const` だった `TrainTimetableStateSchema`・`TrainStationDetailSchema` は `export const` にする）、`inventory.ts` は import に置き換える。`schemas/index.ts` に `export * from "./train";`・`export * from "./beltThroughput";`・`export * from "./rocketLaunchPad";` を足す。挙動は変えない。

Run: `cd moorestech_web/webui && pnpm vitest run src/bridge` → 全 PASS（移しただけで壊れていないこと）

- [ ] **Step 2: wire schema とアクション契約を足す**

`schemas/rocketLaunchPad/index.ts`:

```ts
import { z } from "zod";

// 発射台の段階・待ち理由・進み具合とスロット群の数。C# の RocketLaunchPadDetailDtoBuilder と1対1
// Launch pad phase, wait reason, progress and slot group sizes; mirrors C#'s RocketLaunchPadDetailDtoBuilder one-to-one
export const RocketLaunchPadPhaseSchema = z.enum(["idle", "awaitingBuildCommand", "building", "awaitingLaunchCommand", "launching"]);
export const RocketLaunchPadWaitReasonSchema = z.enum(["none", "missingMaterials", "missingPayload", "outputPending", "noPower"]);
export const RocketLaunchPadDataSchema = z.object({
  phase: RocketLaunchPadPhaseSchema,
  waitReason: RocketLaunchPadWaitReasonSchema,
  progress: z.number().min(0).max(1),
  materialSlotCount: z.number().int().nonnegative(),
  payloadSlotCount: z.number().int().nonnegative(),
  outputSlotCount: z.number().int().nonnegative(),
}).strict();
export type RocketLaunchPadData = z.infer<typeof RocketLaunchPadDataSchema>;
```

`inventory.ts` の `BlockInventoryOpenSchema` に `rocketLaunchPad: RocketLaunchPadDataSchema.optional(),` を足し、`superRefine` に「`rocketLaunchPad` は `blockType === "RocketLaunchPad"` のときだけ」を既存の変換機と同じ書式で足す。

`actionContract.ts` の payload 型表に `"rocket_launch_pad.command": { command: "build" | "launch" };`、`ACTION_TYPES` に `"rocket_launch_pad.command",` を足す。`action_names.json` にも同じ名前を足す。

- [ ] **Step 3: C# の DTO・アクションを書く**

`RocketLaunchPadDetailDto.cs`（名前空間 `Client.WebUiHost.Game.Topics.BlockDetail.Rocket`。既存DTOと同じ public フィールド）:

```csharp
namespace Client.WebUiHost.Game.Topics.BlockDetail.Rocket
{
    public class RocketLaunchPadDetailDto
    {
        public string Phase;
        public string WaitReason;
        public float Progress;
        public int MaterialSlotCount;
        public int PayloadSlotCount;
        public int OutputSlotCount;
    }
}
```

`RocketLaunchPadDetailDtoBuilder.cs`:

```csharp
using System;
using Client.Game.InGame.Block;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail.Rocket
{
    /// <summary>
    /// 発射台の状態とスロット群の数をDTOへ変換する
    /// Converts the launch pad state and its slot group sizes into the DTO
    /// </summary>
    public static class RocketLaunchPadDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            if (param is not RocketLaunchPadBlockParam padParam) return;
            var state = block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);
            // silent-ok: 初回の状態を受け取る前だけ詳細を省き、受信後の再配信で埋まる / ボタンと段階表示が出ないことでパネル上で気づく
            if (state == null) return;

            // スロット群の数はサーバーと同じ並びの規則から引く
            // Slot group sizes come from the same layout rule the server uses
            var layout = new RocketLaunchPadSlotLayout(padParam);
            dto.RocketLaunchPad = new RocketLaunchPadDetailDto
            {
                Phase = ToWirePhase(state.Phase),
                WaitReason = ToWireWaitReason(state.WaitReason),
                Progress = state.Progress,
                MaterialSlotCount = layout.Materials.Count,
                PayloadSlotCount = layout.Payloads.Count,
                OutputSlotCount = layout.OutputSlotCount,
            };
        }

        private static string ToWirePhase(RocketLaunchPadPhase phase)
        {
            return phase switch
            {
                RocketLaunchPadPhase.Idle => "idle",
                RocketLaunchPadPhase.AwaitingBuildCommand => "awaitingBuildCommand",
                RocketLaunchPadPhase.Building => "building",
                RocketLaunchPadPhase.AwaitingLaunchCommand => "awaitingLaunchCommand",
                RocketLaunchPadPhase.Launching => "launching",
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "未知の発射台の段階"),
            };
        }

        private static string ToWireWaitReason(RocketLaunchPadWaitReason reason)
        {
            return reason switch
            {
                RocketLaunchPadWaitReason.None => "none",
                RocketLaunchPadWaitReason.MissingMaterials => "missingMaterials",
                RocketLaunchPadWaitReason.MissingPayload => "missingPayload",
                RocketLaunchPadWaitReason.OutputPending => "outputPending",
                RocketLaunchPadWaitReason.NoPower => "noPower",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の発射台の待ち理由"),
            };
        }
    }
}
```

`BlockDetailDtoBuilder.Apply` の `TrainStationDetailDtoBuilder.Apply(dto, block, param);` の次に `RocketLaunchPadDetailDtoBuilder.Apply(dto, block, param);`。

`Actions/Rocket/RocketLaunchPadCommandActionHandler.cs`（失敗コードは `ActionErrorCodeWireContractTest` の既存集合から選ぶ）:

```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.RocketLaunchPad.State;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.Rocket
{
    /// <summary>
    /// 開いている発射台へ建造・発射の合図を送る
    /// Sends a build or launch command to the open rocket launch pad
    /// </summary>
    public class RocketLaunchPadCommandActionHandler : IActionHandler
    {
        public string ActionType => "rocket_launch_pad.command";

        private readonly SubInventoryState _subInventoryState;

        public RocketLaunchPadCommandActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            var command = ParseCommand(payload["command"]);
            if (command == null) return ActionResult.Fail("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");
            if (!source.TryGetBlockIdentity(out _, out var blockTypeName) || blockTypeName != "RocketLaunchPad") return ActionResult.Fail("block_not_open");

            // 表示の正本は後続のブロック状態。ここでは受理されたかだけ返す
            // The later block state is the display source of truth; only acceptance is reported here
            var response = await ClientContext.VanillaApi.Response.Block.SendRocketLaunchPadCommand(source.BlockPosition, command.Value, CancellationToken.None);
            if (response == null || response.Result != RocketLaunchPadCommandResult.Accepted) return ActionResult.Fail("invalid_state");
            return ActionResult.Success();
        }

        private static RocketLaunchPadCommand? ParseCommand(JToken token)
        {
            if (token is not JValue { Type: JTokenType.String } value) return null;
            return (string)value switch
            {
                "build" => RocketLaunchPadCommand.Build,
                "launch" => RocketLaunchPadCommand.Launch,
                _ => null,
            };
        }
    }
}
```

`WebUiGameBinder.cs` に、`TrainPlatformSetTransferModeActionHandler` を登録している行と同じ書式で `RocketLaunchPadCommandActionHandler` を登録する。

- [ ] **Step 4: 文言を足す**

`Localization/localization.csv` の `ui.blockInventory.trainPlatformUnload` の次に追加し（列: key,Source,english,japanese,german,korean）、`cd moorestech_web/webui && pnpm gen:i18n` を実行する:

```csv
ui.blockInventory.rocketMaterials,Materials,Materials,資材,Materialien,자재
ui.blockInventory.rocketPayloads,Payloads,Payloads,積荷,Nutzlasten,적재물
ui.blockInventory.rocketOutputs,Output,Output,出力,Ausgabe,출력
ui.blockInventory.rocketBuild,Build,Build,建造,Bauen,건조
ui.blockInventory.rocketLaunch,Launch,Launch,発射,Starten,발사
ui.blockInventory.rocketStatusIdle,Waiting for cargo,Waiting for cargo,積み込み待ち,Wartet auf Fracht,적재 대기
ui.blockInventory.rocketStatusAwaitingBuild,Ready to build,Ready to build,建造できます,Bereit zum Bauen,건조 가능
ui.blockInventory.rocketStatusBuilding,Building rocket,Building rocket,ロケットを建造中,Rakete wird gebaut,로켓 건조 중
ui.blockInventory.rocketStatusAwaitingLaunch,Ready to launch,Ready to launch,発射できます,Startbereit,발사 가능
ui.blockInventory.rocketStatusLaunching,Launching,Launching,打ち上げ中,Startet,발사 중
ui.blockInventory.rocketWaitMissingMaterials,Not enough materials,Not enough materials,資材が足りません,Nicht genug Material,자재 부족
ui.blockInventory.rocketWaitMissingPayload,No payload loaded,No payload loaded,積荷がありません,Keine Nutzlast,적재물 없음
ui.blockInventory.rocketWaitOutputPending,Output is full,Output is full,出力がいっぱいです,Ausgabe ist voll,출력이 가득 참
ui.blockInventory.rocketWaitNoPower,No power,No power,電力がありません,Kein Strom,전력 없음
```

- [ ] **Step 5: パネルを書く**

`details/rocket/rocketLaunchPadLogic.ts`（表示の分岐を純関数に集める）:

```ts
import type { RocketLaunchPadData } from "@/bridge";
import { L } from "@/shared/i18n";

// 段階と待ち理由から状態文のキーを選ぶ。待ち理由があればそちらを優先する
// Choose the status text key from phase and wait reason, preferring the wait reason when present
export function rocketStatusKey(detail: RocketLaunchPadData): string {
  switch (detail.waitReason) {
    case "missingMaterials": return L.ui.blockInventory.rocketWaitMissingMaterials;
    case "missingPayload": return L.ui.blockInventory.rocketWaitMissingPayload;
    case "outputPending": return L.ui.blockInventory.rocketWaitOutputPending;
    case "noPower": return L.ui.blockInventory.rocketWaitNoPower;
    case "none": break;
  }
  switch (detail.phase) {
    case "idle": return L.ui.blockInventory.rocketStatusIdle;
    case "awaitingBuildCommand": return L.ui.blockInventory.rocketStatusAwaitingBuild;
    case "building": return L.ui.blockInventory.rocketStatusBuilding;
    case "awaitingLaunchCommand": return L.ui.blockInventory.rocketStatusAwaitingLaunch;
    case "launching": return L.ui.blockInventory.rocketStatusLaunching;
  }
}

// 合図のボタンは初打ち上げ前の合図待ちだけに出る
// Command buttons appear only while awaiting a command before the first launch
export function rocketCommandButton(detail: RocketLaunchPadData): "build" | "launch" | null {
  if (detail.phase === "awaitingBuildCommand") return "build";
  if (detail.phase === "awaitingLaunchCommand") return "launch";
  return null;
}

// itemSlots をサーバーの並び（資材→積荷→出力）で3群に分けた絶対添字
// Absolute indices of itemSlots split into the server order: materials, payloads, outputs
export function rocketSlotGroups(detail: RocketLaunchPadData): { materials: number[]; payloads: number[]; outputs: number[] } {
  const range = (start: number, count: number) => Array.from({ length: count }, (_, i) => start + i);
  const payloadStart = detail.materialSlotCount;
  const outputStart = payloadStart + detail.payloadSlotCount;
  return {
    materials: range(0, detail.materialSlotCount),
    payloads: range(payloadStart, detail.payloadSlotCount),
    outputs: range(outputStart, detail.outputSlotCount),
  };
}
```

`details/rocket/RocketLaunchPadSection.tsx`（`TrainPlatformSection`・`GeneratorSection` の書式。スロット描画は `MachineInventoryBody` の `slotAt` と同じく絶対添字で `useBlockSlotGestures` に渡す）:

```tsx
import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { GaugeBar, ItemSlot, PanelActionButton, SlotGrid } from "@/shared/ui";
import { useBlockSlotGestures } from "../../useBlockSlotGestures";
import { rocketCommandButton, rocketSlotGroups, rocketStatusKey } from "./rocketLaunchPadLogic";

export default function RocketLaunchPadSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const gestures = useBlockSlotGestures();
  const detail = data.rocketLaunchPad;
  if (!detail) return null;
  const groups = rocketSlotGroups(detail);
  const command = rocketCommandButton(detail);

  // 絶対添字のスロットをジェスチャ付きで描く
  // Render a slot by absolute index with gestures attached
  const slotAt = (index: number) => {
    const slot = data.itemSlots[index];
    return (
      <ItemSlot
        key={index}
        itemId={slot.itemId}
        count={slot.count}
        onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
        onRightDown={() => gestures.onRightDown(index)}
        onDoubleClick={() => gestures.onDoubleClick(index)}
      />
    );
  };
  const group = (labelKey: string, indices: number[], testId: string) => (
    <Stack gap={4}>
      <Text size="sm">{t(labelKey)}</Text>
      <SlotGrid cols={Math.min(9, Math.max(1, indices.length))} testId={testId}>{indices.map(slotAt)}</SlotGrid>
    </Stack>
  );

  return (
    <Stack gap="sm">
      {group(L.ui.blockInventory.rocketMaterials, groups.materials, "rocket-launch-pad-material-slots")}
      {group(L.ui.blockInventory.rocketPayloads, groups.payloads, "rocket-launch-pad-payload-slots")}
      <Text size="sm" data-testid="rocket-launch-pad-status">{t(rocketStatusKey(detail))}</Text>
      <GaugeBar value={detail.progress} testId="rocket-launch-pad-progress" />
      {command === "build" ? (
        <PanelActionButton testId="rocket-launch-pad-build" onClick={() => { void dispatchAction("rocket_launch_pad.command", { command: "build" }); }}>
          {t(L.ui.blockInventory.rocketBuild)}
        </PanelActionButton>
      ) : null}
      {command === "launch" ? (
        <PanelActionButton testId="rocket-launch-pad-launch" onClick={() => { void dispatchAction("rocket_launch_pad.command", { command: "launch" }); }}>
          {t(L.ui.blockInventory.rocketLaunch)}
        </PanelActionButton>
      ) : null}
      {group(L.ui.blockInventory.rocketOutputs, groups.outputs, "rocket-launch-pad-output-slots")}
    </Stack>
  );
}
```

（ボタンの置き場は ref-webui-design スキル components.md §8.6 の titleAction 配置規則を読み、そちらが指す位置に合わせる。`Text` の色・余白は同スキルのホワイトリストから選ぶ。）

`views/RocketLaunchPadInventory.tsx`:

```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import RocketLaunchPadSection from "../details/rocket/RocketLaunchPadSection";

export default function RocketLaunchPadInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm" data-testid="rocket-launch-pad-view">
      <RocketLaunchPadSection data={data} />
    </Stack>
  );
}
```

`registeredBlockTypes.ts` に `"RocketLaunchPad",`、`blockComponentRegistry.ts` に `RocketLaunchPad: RocketLaunchPadInventory,`（import も）。

- [ ] **Step 6: テストを書く**

`rocketLaunchPadLogic.test.ts`: (a) 待ち理由 `outputPending` は段階より優先、(b) `awaitingBuildCommand` で `"build"`、`awaitingLaunchCommand` で `"launch"`、その他の4段階で `null`、(c) 資材3・積荷6・出力4で群の添字が `[0,1,2]`・`[3..8]`・`[9..12]`、(d) 積荷0の最小構成で `payloads` が空・`outputs` が `[3..]`。

`views/__tests__/RocketLaunchPadInventory.test.tsx`（`TrainPlatformInventory.test.tsx` と同じ mock 構成）: `awaitingBuildCommand` のデータで `rocket-launch-pad-build` があり `rocket-launch-pad-launch` が無い、`building` でどちらも無い、`dispatchAction` を mock してボタン押下で `("rocket_launch_pad.command", { command: "build" })` が呼ばれる。

`WireFixtures/block_inventory_rocket_launch_pad.json`:

```json
{
  "open": true,
  "source": "block",
  "blockType": "RocketLaunchPad",
  "identifier": "(8, 0, 4)",
  "blockGuid": "88888888-8888-4888-8888-888888888888",
  "itemSlots": [
    { "itemId": 3, "count": 20 },
    { "itemId": 0, "count": 0 },
    { "itemId": 0, "count": 0 }
  ],
  "fluidSlots": [],
  "rocketLaunchPad": {
    "phase": "awaitingBuildCommand",
    "waitReason": "none",
    "progress": 0.0,
    "materialSlotCount": 1,
    "payloadSlotCount": 1,
    "outputSlotCount": 1
  }
}
```

`Client.Tests/WebUi/BlockDetail/RocketLaunchPadWireContractTest.cs`: `WireContractPhaseB2Test.TrainPlatformFixtureMatchesDto` と同じ形で同じ値の `BlockInventoryDto` を作り、`WebUiJson.Serialize` の結果が fixture と `JToken.DeepEquals` で一致することを確かめる（`AssertMatchesFixture` は同ファイル内に写す）。`progress` の `0.0` と `0` の差で一致しなければ fixture を C# の出力に合わせる。`wireContract.test.ts` の block detail fixtures 一覧に `"block_inventory_rocket_launch_pad.json"` を足す。

- [ ] **Step 7: コンパイル・テストを実行する**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RocketLaunchPadWireContractTest|WireContractActionNamesTest|ActionErrorCodeWireContractTest"
cd moorestech_web/webui && pnpm vitest run && pnpm tsc --noEmit && pnpm lint
```
Expected: ErrorCount 0 / 全 PASS（`pnpm lint` が無いスクリプト名なら package.json の lint 系を使う）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi Localization/localization.csv moorestech_web/webui/src
git commit -m "feat(webui): ロケット発射台のパネルと建造発射の操作を足す"
```

---

### Task 9: マスタデータ（発射台・レシピ・研究）とバランス

**Files（すべて moorestech_master `server_v8/mods/moorestechAlphaMod_8/master/`）:**
- Modify: `blocks.json`（ロケット発射台）
- Modify: `machineRecipes.json`（ロケット筐体素材・ロケット燃料）
- Modify: `research.json`（ロケット技術）
- Modify（本repo）: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 1 のスキーマ・計算データとGPU衛星のアイテム・「宇宙」サブカテゴリ
- Produces: 発射台・レシピ2本・研究「ロケット技術」のマスタ行（GUID は Global Constraints）

- [ ] **Step 1: recipe-chain-machine-count スキルで量を決める**

agent前提の目標（判断記録に書く）: 計算データのループ（打ち上げ→研究）は Tier9 の15〜25時間のうち4〜6時間。1台の発射台で1便あたり建造120秒＋打ち上げ15秒。1便の資材を「ロケット筐体素材20・ロケット燃料20・コンピュータ5」とし、同スキルで「1便/135秒を1台で回すのに要る電気汎用工作装置・化学プラントの台数」を出し、上流（鋼鉄板・プラスチック・ガソリン・固形燃料）が既存の工場規模から外れないことを確かめる（作者メモ「それ以外の要素はそこまで変な工場建設は要求されないように」）。外れる場合は資材の数を下げる。結果の表を PR 本文に貼る。

- [ ] **Step 2: blocks.json の発射台を埋める**

ロケット発射台 `01a162b0-7769-7419-af24-bb35528b93d2` の行を次の値にする（`name`・`imagePath`・`initialUnlocked: false`・`placementsPerCost: 1` は今の値のまま。電力・建設費は判断記録 D12 の仮値。既存の最大要求電力は石油蒸留機の150なので、その上の200に置く）:

```json
"blockType": "RocketLaunchPad",
"category": "生産",
"subCategory": "宇宙",
"sortPriority": 1400,
"blockSize": [10, 21, 10],
"blockPrefabAddressablesPath": "Vanilla/Block/Rocket",
"openable": true,
"requiredItems": [
  { "itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 200 },
  { "itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 50 },
  { "itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 20 }
],
"blockParam": {
  "requiredPower": 200,
  "buildSeconds": 120,
  "launchSeconds": 15,
  "launchMaterials": [
    { "itemGuid": "01a162a4-9334-7286-a8ba-7daf9c0f407a", "count": 20 },
    { "itemGuid": "01a162a8-e435-73d0-bddb-08533819b80e", "count": 20 },
    { "itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 5 }
  ],
  "outputSlotCount": 4,
  "inventoryConnectors": {
    "inputConnects": [
      { "offset": [0, 0, 5], "directions": [[-1, 0, 0]], "connectorGuid": "aacb7e4e-f4b2-4b7e-9f04-6ac871daf753", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" },
      { "offset": [5, 0, 0], "directions": [[0, 0, -1]], "connectorGuid": "ebe860a8-0aff-4c83-93b7-e82a60c1c104", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" }
    ],
    "outputConnects": [
      { "offset": [9, 0, 5], "directions": [[1, 0, 0]], "connectorGuid": "95386744-9e2e-476d-ac75-ab3e3cc0fef3", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" },
      { "offset": [5, 0, 9], "directions": [[0, 0, 1]], "connectorGuid": "a98a689d-08af-4263-973e-6907670d6d64", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" }
    ]
  },
  "maxWireConnectionCount": 2,
  "connectionRange": 30,
  "connectionHeightRange": 20
}
```

（鋼鉄板・コンピュータ・制御モータ、ロケット筐体素材・ロケット燃料・コンピュータの順。`launchMaterials` の数は Step 1 の結果で下げてよい。入力は西辺と南辺の中央、出力は東辺と北辺の中央。書式は化学プラント・EUV露光式半導体製造装置の `inventoryConnectors` と同じ。）

- [ ] **Step 3: レシピと研究を書く**

`machineRecipes.json` に2本（既存の電気汎用工作装置・化学プラントのレシピの書式を写す。流体入力の書式は化学プラントの既存レシピに合わせる）:
- `01a167cf-afa0-7add-8407-eec71456f4f3`: 電気汎用工作装置、鋼鉄板×4＋プラスチック×2 → ロケット筐体素材×1、10秒
- `01a167cf-afa3-7c4f-82b7-c0d4f6d513c6`: 化学プラント、ガソリン100＋固形燃料×2 → ロケット燃料×1、8秒

`research.json` に「ロケット技術」`01a167cf-af9d-7f31-817c-3069b4f6e499`:
- `prevResearchNodeGuids: ["019e3ad9-46e2-73fd-9555-8c1d0245d26d"]`（半導体製造）
- `consumeItems`: 鋼鉄板×1000・コンピュータ×300・ICチップLv1（`f6dd7835-0845-4877-87d0-b719d037ae7d`）×100（agent前提・S1待ち）
- `clearedActions`: `unlockBlock`（発射台）、`unlockMachineRecipe`（上の2本）、`unlockItemRecipeView`（ロケット筐体素材・ロケット燃料・計算データ・GPU衛星Lv1〜Lv5）
- `graphViewSettings.UIPosition` は半導体製造（4080, -200）の右隣、`IconItem` はロケット燃料

「軌道エレベーターの建設」ノードは触らない（S3）。

- [ ] **Step 4: push しピンを向け、読込を確かめる**

```bash
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" add server_v8/mods/moorestechAlphaMod_8/master
git -C "$MASTER_WT" commit -m "data(v8): ロケット発射台・ロケット筐体素材と燃料のレシピ・ロケット技術を足す"
git -C "$MASTER_WT" push origin feat/tier9-compute-data-loop
git -C "$MASTER_WT" rev-parse HEAD
```

本repoの `.moorestech-external-revisions.json` をそのハッシュへ向け、Editor でマスタを読み直して（RepositorySync がピンへ checkout する）Console に `[ItemMaster]` `[BlockMaster]` `MooresmasterLoaderException` が無いことを `uloop get-logs --project-path ./moorestech_client --log-type Error` で確かめる。

- [ ] **Step 5: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: マスタのピンをロケット発射台のデータへ向ける"
```

---

### Task 10: S1待ちの判定（GPU衛星レシピ・半導体のアップグレード研究）

**Files:**
- Modify（条件付き）: moorestech_master `machineRecipes.json`・`research.json`
- 記録: bd `moorestech-xyuaf.1.1`

**Interfaces:**
- Consumes: S1 の GPU アイテム（levelFamily）とレベル継承レシピの仕組み（`.decisions/2026-10-23-チップ製品のレベルは使ったチップのレベルを継承する.md`、S1 ブランチ `feat/tier9-cleanroom-playable`）
- Produces: なし

- [ ] **Step 1: S1 の到着を確かめる**

```bash
git fetch origin
git log --oneline origin/master | head -20
git grep -n "levelInherit\|InheritLevel\|継承" origin/master -- VanillaSchema | head
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" fetch origin
git -C "$MASTER_WT" show origin/master:server_v8/mods/moorestechAlphaMod_8/master/items.json | grep -n "01a167ed-2700-7371-9f95-168966ff60bc\|01a167ed-2703-7014-98e4-11bd15f38571"
git grep -n "gameActionType" origin/master -- VanillaSchema | head
```

- [ ] **Step 2: 分岐する**

- **origin/master に GPU Lv1〜Lv5 の levelFamily と、レベル継承レシピのスキーマ・サーバー実装の両方がある場合:** GPU衛星のレシピ（GPU LvN（Global Constraints の S1 の GPU）＋鋼鉄板×10＋コンピュータ×5 → GPU衛星 LvN、電気汎用工作装置、30秒。入力と出力が別ファミリーでも1本で書ける S1 の継承レシピの書式に従う）を足し、継承の上限を研究で上げる仕組みがあれば「半導体のアップグレード研究」（計算データを consumeItems に持ち、モジュールの上位レベルの上限を上げる研究。段数と量は S1 の裁定値に従う）を足す。マスタを push しピンを更新、Task 9 Step 4 と同じ確認をしてコミットする。
- **どちらかが無い場合:** 何も足さない。`bd note moorestech-xyuaf.1.1 "S2の実装時点（<日付>）で origin/master に <無かったもの> が無いため、GPU衛星レシピ／半導体のアップグレード研究は未投入。S1マージ後に足す"` を残し、PR本文の「S1待ち」節に同じ内容を書く（Task 13）。

---

### Task 11: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/rocket-launch-pad.cs`

**Interfaces:**
- Consumes: Task 1〜9 の全成果、testid（Task 8）
- Produces: 録画・スクショ・result.json

- [ ] **Step 1: unity-playmode-recorded-playtest スキルを読み、シナリオを書く**

`cleanroom-v2.cs`・`ui-place-then-open-machine.cs` の書式で:

```csharp
using System;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.Fluid;
using Game.Orbit;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("rocket-launch-pad", options, async p =>
{
    await p.SetupFlatGround();

    // 発射台と発電機を置き、電線で給電する
    // Place a launch pad and a generator, then wire power
    var padPos = new Vector3Int(0, 32, 0);
    var pad = p.PlaceBlockDirect("ロケット発射台", padPos, BlockDirection.North);
    var generator = p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -15), BlockDirection.North);
    var gasolineId = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
    generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(4000, gasolineId), FluidContainer.Empty);
    var freeCost = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var genConn = generator.GetComponent<IElectricWireConnector>();
    var padConn = pad.GetComponent<IElectricWireConnector>();
    p.Assert(ElectricWireSystemUtil.TryConnectBothSides(genConn, padConn, freeCost), "wire gen->pad");
    p.ServerService<IElectricWireNetworkDatastore>().RebuildAround(genConn, padConn);

    // 1便ぶんの資材とGPU衛星Lv1を入れる
    // Insert one launch worth of materials and a GPU satellite Lv1
    Core.Inventory.IOpenableInventory inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット筐体素材"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット燃料"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("コンピュータ"), 5);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("GPU衛星Lv1"), 1);
    var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.AwaitingBuildCommand, 5f, "awaiting build command");
    await p.Screenshot("01-awaiting-build");

    // 発射台を開いて建造ボタンを押す
    // Open the pad and press the build button
    p.WarpPlayer(new Vector3(-6f, 33f, -6f));
    await p.WaitSeconds(1f);
    await p.AimAt(pad.BlockPositionInfo.OriginalPos + new Vector3(5f, 1f, 5f));
    await p.PressInteract();
    await p.UntilWebUiElement("rocket-launch-pad-build", 10f);
    await p.Screenshot("02-panel-build-button");
    await p.ClickWebUi("rocket-launch-pad-build");
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Building, 5f, "building");
    await p.Screenshot("03-building");

    // 建造完了で本体が出て、発射ボタンで打ち上げる
    // On build completion the body appears; launch with the launch button
    await p.UntilWebUiElement("rocket-launch-pad-launch", 180f);
    await p.Screenshot("04-awaiting-launch");
    await p.ClickWebUi("rocket-launch-pad-launch");
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Launching, 5f, "launching");
    await p.WaitSeconds(5f);
    await p.Screenshot("05-rising");

    // 計算データが出力口に入り、初打ち上げ済みになる
    // Compute data lands in the output slots and the world is marked as launched
    var computeDataId = PlaytestItemOps.ResolveItemId("計算データ");
    await p.Until(() => CountInOutputs() == 10, 5f, "compute data delivered");
    p.Assert(p.ServerService<IRocketLaunchHistory>().HasLaunchedOnce, "first launch recorded");
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Idle, 20f, "launch finished");
    await p.Screenshot("06-output");

    // 2便目は合図なしで建造が始まる
    // The second launch starts building without commands
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット筐体素材"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット燃料"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("コンピュータ"), 5);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("GPU衛星Lv1"), 1);
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Building, 5f, "auto building");
    await p.Screenshot("07-auto-building");

    int CountInOutputs()
    {
        var total = 0;
        foreach (var stack in inventory.InventoryItems)
            if (stack.Id == computeDataId) total += stack.Count;
        return total;
    }
});
```

（ブロック名・アイテム名はマスタの `name` と一致させる。照準・`PressInteract` の距離条件は `ui-place-then-open-machine.cs` を見て、10×10 の大きさでインタラクトが届く位置へワープする。建造120秒を待つのが長すぎれば、シナリオ側で台の `buildSeconds` を変えずに、開始前に `SendCommand` で時間を進める手段が DSL に無いことを確かめ、そのまま待つ。）

- [ ] **Step 2: 実行する**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/rocket-launch-pad.cs "$(cd ../../moorestech_master/tier9-compute-data-loop && pwd)"`
Expected: result.json が success、全 Assert 通過。録画とスクショ7枚。`05-rising` でロケット本体が台より上にある、`01`〜`03` で本体が見えない、`04` で本体が見えることを画像で確かめる。

- [ ] **Step 3: 録画を共有しコミットする**

`share-put <録画のディレクトリ> -n rocket-launch-pad-playtest` で URL を得て PR 本文に貼る。

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/rocket-launch-pad.cs
git commit -m "test(playtest): ロケット発射台の初打ち上げと自動打ち上げの録画シナリオを足す"
```

---

### Task 12: moores-code-review による全ブランチレビュー

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 11 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする。

---

### Task 13: PR作成

- [ ] **Step 1: セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**

- moorestech_master（`feat/tier9-compute-data-loop` → master）の PR も作り、moorestech の PR 本文から相互にリンクする。`.moorestech-external-revisions.json` のピンがその PR の head と一致していること。
- PR 本文に入れる節: 概要／S3向けの口（`Game.Orbit` の API と GUID）／S1待ち（Task 10 の結果）／バランス表（Task 9 Step 1）／録画の URL（Task 11）／セーブ版 6→7（S3 は 7→8）。
- 全実装タスク完了＝完了ではない。PR未作成のまま終わるのはplan未完了である。

---

## 判断記録（ADR）

設計セッションの裁定:
- `docs/adr/0088-orbital-arrival-service-shared-by-launch-sites.md`
- `.decisions/2026-10-23-計算データはGPU衛星の打ち上げごとに一括で得る.md`
- `.decisions/2026-10-23-GPU衛星はレベル別アイテムとして製造してから打ち上げる.md`
- `.decisions/2026-10-23-ロケットの手動建造発射はワールドで最初の1回だけ.md`
- `.decisions/2026-10-23-ロケットは打ち上げごとに筐体素材燃料コンピュータペイロードを消費する.md`
- `.decisions/2026-10-23-発射台は届いた積荷で打ち上げ内容を自動で選ぶ.md`
- `.decisions/2026-10-23-ロケットは独立研究にし計算データは半導体アップグレードと軌道エレベーターの両方で使う.md`
- `.decisions/2026-10-23-半導体のアップグレード研究は計算データでモジュールの上位を解放する.md`
- `.decisions/2026-10-23-発射台は電力を使い時間をかけてロケットを建造する.md`
- `.decisions/2026-10-23-発射台は既存Rocketモデルを使いロケット本体だけを上昇させる.md`
- `.decisions/2026-10-23-計算データは打ち上げた設備の出力口に出す.md`
- 用語: `CONTEXT.md`「ロケットと計算データ」

planning 中の判断:

| # | 判断 | 出所 |
|---|---|---|
| D1 | 積荷表は items.yml のトップレベル配列（新規トップレベルスキーマを作らない） | agent前提（edit-schema スキルの規約） |
| D2 | 出力口の空きの事前問い合わせ口は作らず、打ち上げ口が成果を保留して次便を止める。S3 も同じ約束で実装する | agent前提（ADR 0088 改訂、S3 合意 2026-10-23） |
| D3 | 軌道到着は打ち上げ開始の時点。昇っている `launchSeconds` の間は次の便を建造しない | agent前提（CONTEXT.md「軌道到着」） |
| D4 | 資材・積荷は建造開始で取り込む。撤去時は建造中・発射の合図待ちの中身を返し、打ち上げ後は返さない | agent前提（`.decisions/…4つとも毎回消費` の「打ち上げごとに消費」を建造開始＝取り込み時点と解釈） |
| D5 | 積荷1種につき入れ口1スロット。1便の個数はレベル1のスタック上限以下をマスタ検証で強制（S3 のケーブルは専用スタックテーブルで1000本/便） | agent前提（S3 合意 2026-10-23） |
| D6 | 揃った積荷が複数あるときは前回の次から順に選ぶ（積荷表の並び順の巡回） | agent前提（同点選択。どれを選んでも1便の消費・成果は表で同型に決まるため、偏りの防止だけを目的に巡回にする） |
| D7 | 電力は建造中だけ要求し、供給は機械と同じく加算→次の Update で使い切る | agent前提（`VanillaMachineProcessorComponent.SupplyExternalPower` の前例） |
| D8 | 状態は段階・待ち理由が変わったときと、建造中・打ち上げ中は毎tick流す | agent前提（`VanillaMachineProcessorComponent.Update` の前例） |
| D9 | 初打ち上げ後、発射の合図待ちの台は `OnFirstLaunch` の購読で打ち上げ、建造の合図待ちの台は次の Update の判定で自動建造へ進む | agent前提（AGENTS.md「状態変化の検知は購読で」。建造待ちは毎tickの揃い判定と同じ判定点に置く） |
| D10 | 入れ口のセーブはアイテム単位、出力口は並び。積荷表の行が変わったら置き場の無い品を保留へ移して警告 | agent前提（テスターのワールドがマスタ更新を跨ぐため） |
| D11 | `inventory.ts`（215行）は列車とベルト統計の schema を別ファイルへ移してから足す。`GameplayServiceRegistration.cs`（202行）には足さず `OrbitServiceRegistration` を新設。`WorldSaveDataRestorer.cs`（194行）は必須欠損の検査を `WorldSaveRequiredFieldGuard` へ移す | agent前提（AGENTS.md 200行規則） |
| D12 | 計算データの成果 Lv1=10・Lv2=20・Lv3=40・Lv4=80・Lv5=160（倍々）。ロケット技術の研究コスト、1便の資材数、建造120秒・打ち上げ15秒も仮値 | agent前提（GPUのレベルが計算データへどう効くかは S2 が決める: S1 からの連絡 2026-10-23「GPU の Lv が計算データにどう効くかは S2 で決めてください」。倍々は S1 裁定「大部分のレベル1と少数のレベル2チップ程度でクリアできるようにしたい」の下で Lv1 だけでも研究が進み、上位ほど報われる形にするため。研究コストは S1 の Tier9 研究コストの裁定が出るまでの仮値） |
| D13 | GPU衛星レシピと半導体のアップグレード研究は S1 の仕組みが origin/master にあるときだけ入れる | agent前提（依頼原文「GPU のアイテム定義とレベルの効き方は S1 が決める」・「S1 の領域のコードは変更しない」、S1 からの連絡 2026-10-23「GPU のアイテム定義とレシピは S1 が作ります」） |
| D14 | マスタの `stackLevelTableGuid` が空の Tier9 アイテム7件へ Default を入れる | agent前提（`ItemMasterUtil.StackLevelTableValidation` で読込が止まるため。S3 の担当アイテムも含むが、値は S3 にも共有済み） |
| D15 | Web UI の失敗コードは既存集合（`invalid_payload`・`block_not_open`・`invalid_state`）から選ぶ | agent前提（`ActionErrorCodeWireContractTest` の集合を広げない） |
| D16 | unityプレイ録画テストを入れる（ランタイム挙動・UI・3D表示に触れるため） | agent前提（moores-writing-plans の録画テスト規則） |
| D17 | Game.Orbit 一式と積荷表（スキーマ・全JSON・計算データとGPU衛星の行）を Task 1〜2 に置き、Task 2 の最後で両repoを push する。以後 force-push しない | agent前提（S3 からの依頼 2026-10-23「Game.Orbit 一式と orbitalPayloads のスキーマ／マスタ（GPU衛星行込み）を最初の実装タスクにし、できた時点で両 repo へ push」「その後もブランチは force-push しない」） |
| D18 | `RemoveBlockProtocol` の返却を `IGetRefundItemsInfo` 全件の合算に直す。発射台の部品順は電線を進行より前に置き、修正前のコードでは返却が落ちることをテストで確かめる | agent前提（S3 からの指摘 2026-10-23。`RemoveBlockProtocol.cs:123` の `TryGetComponent` が先頭1件だけを読むことをコードで確認。原因: 推測（根拠: コード読解。未再現。Task 5 の撤去返却テストが修正前に落ちることで実測に変わる）） |
| D19 | ビルドメニュー「生産」の下に「宇宙」（`d2000000-0000-4000-8000-000200000005`）を S2 が作り、発射台を置く。S3 は宇宙エレベーター拠点を同じ場所へ足す | agent前提（S3 からの提案 2026-10-23。既存の「半導体」サブカテゴリと同じ書式） |
| D21 | `VanillaSchema/blocks.yml` に blockType `RocketLaunchPad` と case を足す。全キー必須（`optional` を使わず default だけ置く）で、`IInventoryConnectors`・`IElectricWireConnectParam` を実装させる | agent前提（edit-schema スキル・AGENTS.md「optional: true で吸収するのは設計の敗北」、前例 `CleanRoomMachine` の case） |
| D22 | 積荷表の検証は `Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs`（新設ディレクトリ。`ItemMasterUtil.cs` は244行のため足さない）に置き、`Core.Master/ItemMaster.cs` の `Validate` でアイテム検証の後に連ねる。発射台パラメータの検証は `Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs` に置き、`Core.Master/Validator/BlockMasterUtil.cs` の `Validate` に1行足す | agent前提（前例 `Core.Master/Validator/Block/BlockParamValidator.cs` と `BlockMasterUtil.Validate` の `errorLogs +=` 連結） |
| D23 | 発射台の組み立ては `Game.Block/Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs`（New/Load とも `Create` へ集める）に置き、`Game.Block/Factory/VanillaIBlockTemplates.cs` の `BlockTypesDictionary` へ登録する。サービスは `ServerContext.GetService<T>()` で取る | agent前提（前例 `VanillaCleanRoomAirFilterTemplate.cs`） |
| D24 | 建造・発射の合図は1本のプロトコル `Server.Protocol/PacketResponse/RocketLaunchPadCommandProtocol.cs`（enum `RocketLaunchPadCommand` で分岐、結果は enum で返す）にし、`Server.Protocol/PacketResponseCreator.cs` へ登録、クライアントは `Client.Network/API/Requests/BlockRequestApi.cs` に1メソッドだけ足す。`Server.Protocol/Server.Protocol.asmdef` へは `Game.Orbit` を参照が要るときだけ足す（プロトコル本体は Game.Orbit を使わない見込み） | agent前提（creating-server-protocol スキル「1プロトコル=1メソッド」「enum をそのまま使う」、前例 `SetTrainPlatformTransferModeProtocol.cs`・`BlockRequestApi` の既存メソッド） |
| D20 | 「軌道エレベーターの建設」の研究ノードの変更（発射台・筐体素材・燃料の解放を外し、計算データを消費に足す）は S3 が行い、S2 は触らない | agent前提（ユーザー裁定 2026-10-23「ロケット独立＋両方で消費」の実施分担。ノードの持ち主は S3） |
