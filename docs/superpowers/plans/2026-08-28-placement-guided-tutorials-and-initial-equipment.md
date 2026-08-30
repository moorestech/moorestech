# 設置システム案内チュートリアル基盤と初期装備 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ADR 0038 決定3（鉱脈限定設置チュートリアル・相対座標プレビュー・歯車接続の常設明示・完了判定2種）と決定1の「石の斧を装備済みで開始」を、本repo（スキーマ＋サーバー＋クライアント）に実装する。マスタJSONの序盤圧縮そのものは姉妹plan `2026-08-28-early-game-compression-master-data.md` が担い、本planのマージ後に実行する。

**Architecture:** チャレンジ完了判定は `ChallengeFactory` に2種（`blockPlaceOnVein` / `gearConnectedBlock`）を足し、既存の「イベントで候補を積みティック境界で判定」型（`EquipItemChallengeTask`）に揃える。クライアントの鉱脈限定設置は「チュートリアルが共有状態 `VeinRestrictedPlacementState` へ書く → `CommonBlockPlaceSystem` の reporter 列と `PlaceBlockState` の鉱脈表示が読む」の書き手1人追加で実現し、設置不可は既存どおり `PlaceInfo.Placeable=false`（赤表示＋送信抑止）で表す。歯車接続の常設明示は電線の `ElectricWireAutoConnectPreview` 三点組（collector/preview/renderer）を歯車へ写した `GearConnectPairResolver`/`GearConnectPreview`/`GearConnectPreviewRenderer` で行い、判定はサーバー共有の `BlockConnectorConnectPositionCalculator`＋`GearConnectJudge` をそのまま呼ぶ。初期装備は `items.yml` ルートの `initialEquipmentItems` を新設し、`PlayerInventoryDataStore` が新規プレイヤーのインベントリ生成時に `EquipmentInventoryData.RestoreFromSave` で無イベント投入する。

**Tech Stack:** Unity 6000.3 / C# / UniRx / VContainer / Mooresmaster SourceGenerator（YAMLスキーマ→生成）/ uloop CLI / NUnit（EditMode）/ Python3（JSONフィクスチャ更新）/ gh CLI。

## Requirements

設計ADR: `docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md`。裁定: `.decisions/2026-08-27-*.md` 4件。

- R1 `items.yml` ルートに必須配列 `initialEquipmentItems[{itemGuid(FK items), itemCount default 1}]` を追加し、全JSON（server ForUnitTest / client EditModeInPlayingTestMod / mooresmaster.SandBox / `../moorestech_master` の6 mod）を一括更新する。受け入れ: 生成物 `Items.InitialEquipmentItems` がコンパイルを通り、`ItemMasterUtil.Validate` が未知itemGuidをエラーにする。
- R2 新規プレイヤーのインベントリ生成時に `initialEquipmentItems` を装備スロットへ無イベントで入れる。受け入れ: ForUnitTest（`initialEquipmentItems=[Test1×1]`）で `GetInventoryData(0).EquipmentInventory.GetItem(0).Id == Test1`、`GetSelectedItem().Id == Test1`。セーブからロードしたプレイヤーには再投入しない。
- R3 チャレンジ完了判定 `blockPlaceOnVein{blockGuid, veinGuid}` を追加。指定ブロックが指定鉱脈の上（採掘機ならドリルセル、それ以外は占有セルのいずれか）に設置されたらティック境界で完了。チャレンジ開始前に置かれていた分も初回ティックで回収。受け入れ: サーバーテストで鉱脈上設置→完了、鉱脈外→未完了。
- R4 チャレンジ完了判定 `gearConnectedBlock{blockGuid}` を追加。指定ブロックのいずれかが `IGearEnergyTransformer.CurrentRpm > 0` になったらティック境界で完了。受け入れ: 発電機＋シャフトで完了、シャフト単体で未完了。
- R5 チュートリアルtype `veinRestrictedPlacement{veinGuid, blockGuid}` を追加。適用中、対象ブロックの設置プレビューでは対象鉱脈だけを強調表示し（他鉱脈は非表示）、対象鉱脈外のセルは `Placeable=false`＋ツールチップ理由1行。完了で解除。受け入れ: reporter単体テスト・鉱脈表示テスト・TutorialManagerディスパッチテストが通る。
- R6 チュートリアルtype `relativeBlockPlacePreview{anchorBlockGuid, blockGuid, offset, blockDirection, message}` を追加。最寄りのアンカーブロック原点＋offset にゴーストを出し、その座標に対象ブロックが置かれたら完了。アンカー不在時はゴーストを隠す。受け入れ: ディスパッチテストとEditModeInPlayingTestで、アンカー設置後にゴースト座標が `anchor.OriginalPos + offset` になる。
- R7 歯車系ブロック（`IGearConnectors` を持つ全blockType）の設置プレビューで、カーソルセルの各コネクタが接続する隣接ブロックのコネクタセルへ線を描く（常設・チュートリアル非依存）。判定はサーバーと同じ「位置一致→形状表→GearConnectJudge」。受け入れ: `GearConnectPairResolverTest`（発電機(0,0,0)＋シャフト(0,0,1)で1組、離れていれば0組）。
- R8 `GearConnectorView`（コネクタ位置の線）を `Shaft` / `SmallGear` / `Ore_Crusher` / `Fuel_powered_windmill` prefab へ付け、`Directions==null` で残りコネクタを落とす `return` を `continue` に直す。受け入れ: 4 prefab に `GearConnectorView` 子が1つずつあり、Editorで設置モードに入ると線が出る。
- R9 `ChallengeMasterUtil` の TaskParam / Tutorial 検証に新4型のcaseを足す（未知型は `default:` でエラー扱いのため必須）。`MasterSourceTextCollector` の switch に新2 tutorial型を足す。
- R10 クライアント `Localization/localization.csv` に `ui.tooltip.placeOutsideTutorialVein` を追加。
- R11 変更後、`../moorestech_master` へ items.json（6 mod）の追随をpush・PRし、`.moorestech-external-revisions.json` のピンをそのコミットへ更新する。
- やらないこと: マスタの研究・チャレンジ再構成（姉妹plan）／鉱脈名の常設ラベル（裁定で保留）／サーバー側での鉱脈外設置拒否（採掘機前例どおりクライアント限定）／`blockPlace` の `itemCount` 未使用の是正／Web UI（webui）側の変更（新typeはUnity側3D表示のみ・ワールドピンは既存汎用スキーマで配信）。

## Global Constraints

- 作業場所: `moores-wt new feature/placement-guided-tutorials --dir placement-guided-tutorials --from master --fetch` で作る worktree `~/hermes-agent/data/repos/moorestech-worktrees/placement-guided-tutorials`（以下 `$WT`）。メインワークツリーでは作業しない（CLAUDE.local.md）。PR作成後は `moores-wt rm placement-guided-tutorials`。
- マスタrepoの作業場所: `~/hermes-agent/data/repos/moorestech-master-worktrees/placement-guided-tutorials`（branch `feature/initial-equipment-items-field`、Task 1 で作成。以下 `$MW`）。
- スキーマ規約（edit-schema）: `optional: true` 禁止、新フィールドは `default` 付き必須＋全JSON一括更新、`Mooresmaster.Model.*` の手書き禁止、foreignKey追加時は対応 `*MasterUtil` に検証case追加、生成トリガは `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` 書き換え。
- コード規約（AGENTS.md）: `partial` 禁止・`Func<>` 禁止・try-catch は外部境界のみ・1ファイル200行以内・1ディレクトリ10ファイル以内・イベントはUniRx・`[SerializeField]` は `_` 無し lowerCamel・`#region Internal` はメソッド内ローカル関数のみ・日英2行コメント・デフォルト引数禁止・`{ get; private set; }` 可。
- Prefab/シーンの編集は `uloop execute-dynamic-code` 経由のみ（`Write`/`Edit` での直接編集禁止）。`.meta` は手で作らない。
- コンパイル: `uloop compile --project-path ./moorestech_client`。localization.csv を触った後は `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` を1回挟む（CS0117 の偽陽性回避）。
- テスト: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。180秒タイムアウトは失敗ではなく結果は `.uloop/outputs/TestResults` のXML。ドメインリロード中エラーは45秒待って再試行。
- 時間計測はサーバーでは `GameUpdater` ティックのみ。
- コミットメッセージ末尾: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` と `Claude-Session: https://claude.ai/code/session_01Ts2pLxAukyhiJyiiqk4bXs`。
- zsh: `--include=*.cs` はクォートする。区切り `echo` は避ける。
- 生成クラス名は Mooresmaster の規則に従う（`when: blockPlaceOnVein` → `BlockPlaceOnVeinTaskParam`、`when: veinRestrictedPlacement` → `VeinRestrictedPlacementTutorialParam`、items ルート配列 `initialEquipmentItems` → 要素型 `InitialEquipmentItemsElement`、定数 `TutorialsElement.TutorialTypeConst.veinRestrictedPlacement`）。planの名前と生成結果が違ったら生成結果を正とし差分を記録する。

---

## File Structure

スキーマ・生成トリガ:
- Modify: `VanillaSchema/items.yml` — ルート末尾に `initialEquipmentItems`
- Modify: `VanillaSchema/challenges.yml` — `taskCompletionType` 選択肢2件＋`taskParam` case2件、`tutorialType` 選択肢2件＋`tutorialParam` case2件
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — `dummyText`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs` — 新4型のcase
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ItemMasterUtil.cs` — `initialEquipmentItems` のitemGuid実在検証

JSONフィクスチャ（items ルート `initialEquipmentItems`、challenges 新チャレンジ2件）:
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`、同 `challenges.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/items.json`
- Modify: `mooresmaster/mooresmaster.SandBox/TestMod/items.json`
- Modify（`$MW`）: `server/mods/moorestechAlphaMod_3/master/items.json`、`server_v4/.../moorestechAlphaMod_4/master/items.json`、`server_v5/...`、`server_v6/...`、`server_v7/...`、`server_v8/mods/moorestechAlphaMod_8/master/items.json`

サーバー:
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/InitialEquipmentMasterUtil.cs` — マスタ→初期装備スタック列（読むだけ）
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory/PlayerInventoryDataStore.cs:47-59` — 新規生成時の投入
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/BlockPlaceOnVeinChallengeTask.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/GearConnectedBlockChallengeTask.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs`、`ChallengeFactory.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/InitialEquipmentTest.cs`、`BlockPlaceOnVeinChallengeTaskTest.cs`、`GearConnectedBlockChallengeTaskTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetChallengeInfoProtocolTest.cs:48,73,115` — 初期チャレンジ件数 5→7 / 5→7 / 3→5

クライアント（鉱脈限定設置）:
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinAabb.cs` — `VeinGuid`
- Modify: `.../Map/MapVein/MapVeinAabbRegistry.cs` — `IsInsideVein(Vector3Int, Guid)`
- Modify: `.../Map/MapVein/IMapVeinRangeView.cs`、`MapVeinRangeViewService.cs`、`MapVeinRangeBoxMaterials.cs` — 強調鉱脈モード
- Create: `.../BlockSystem/PlaceSystem/VeinRestriction/VeinRestrictedPlacementState.cs` — 共有状態（書き手: チュートリアル、読み手: reporter と PlaceBlockState）
- Create: `.../BlockSystem/PlaceSystem/VeinRestriction/VeinRestrictedPlacementReporter.cs`
- Modify: `.../BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs` — reporter呼び出し＋歯車プレビュー呼び出し
- Modify: `.../UI/UIState/State/PlaceBlockState.cs` — 強調鉱脈のプッシュ
- Create: `.../Tutorial/PlacementGuide/VeinRestrictedPlacementTutorialManager.cs`
- Create: `.../Tutorial/PlacementGuide/RelativeBlockPlacePreviewTutorialManager.cs`
- Modify: `.../Tutorial/TutorialManager.cs` — ctor引数2件追加
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`、`Registration/MainGameInteractionRegistration.cs` — SerializeField/登録
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs:123-133`
- Modify: `Localization/localization.csv`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/VeinRestrictedPlacementReporterTest.cs`、`Client.Tests/Map/MapVeinRangeViewHighlightTest.cs`、`Client.Tests/UnitTest/Tutorial/PlacementGuideTutorialDispatchTest.cs`、`Client.Tests/UIState/Fakes/FakeMapVeinRangeView.cs`（メソッド追加）、`Client.Tests/UnitTest/Tutorial/VeinPinTutorialTest.cs:70-79`（ctor引数追加）、`Client.Tests/EditModeInPlayingTest/RelativeBlockPlacePreviewTest.cs`

クライアント（歯車接続の常設明示）:
- Create: `.../BlockSystem/PlaceSystem/Common/GearConnect/GearConnectPairResolver.cs` — 純関数（テスト対象）
- Create: `.../BlockSystem/PlaceSystem/Common/GearConnect/GearConnectPreview.cs` — 近傍収集＋描画駆動
- Create: `.../BlockSystem/PlaceSystem/Common/GearConnect/GearConnectPreviewRenderer.cs`
- Modify: `.../BlockSystem/PlaceSystem/Common/PreviewObject/GearConnectorView.cs:28` — `return`→`continue`
- Prefab（uloop経由）: `moorestech_client/Assets/AddressableResources/Block/Shaft.prefab`、`SmallGear.prefab`、`Ore_Crusher.prefab`、`Fuel_powered_windmill.prefab` に `Block/Util/GearConnectorView.prefab` を子として追加
- Test: `Client.Tests/PlaceSystem/GearConnect/GearConnectPairResolverTest.cs`

本体repo設計文書:
- Add: `docs/adr/0038-...md`、`.decisions/2026-08-27-*.md` 4件、本plan、姉妹plan

### 配置と前例（spec-architecture-review）

データフロー地図（鉱脈限定設置）:
```
チャレンジ開始 → TutorialManager → VeinRestrictedPlacementTutorialManager ─書く→ [VeinRestrictedPlacementState]
  → 読む: CommonBlockPlaceSystem.GroundClickControl の reporter 列（MinerVeinPlacementReporter と同位置）→ PlaceInfo.Placeable
  → 読む: PlaceBlockState（OnTargetChanged と State.OnChanged の購読）→ IMapVeinRangeView.SetHighlightedVein
```
新規コンポーネントは「書き手1人」。既存の設置パイプライン（Placeable→色→送信抑止）と鉱脈表示（PlaceBlockStateからのプッシュ）を無傷で使う。交差点（bool戻り・直接セッター）は足さない。

| 項目 | 層 | 前例 | 判定 |
|---|---|---|---|
| `InitialEquipmentMasterUtil`（マスタ読取のstatic util） | Game.PlayerInventory.Interface | `PlayerInventorySlotLevelMasterUtil`（層マップ「マスタ値のドメイン解釈」） | ok |
| `PlayerInventoryDataStore` 新規生成時の投入・`RestoreFromSave` 再利用 | Game.PlayerInventory | 同ファイル `LoadPlayerInventory` の装備復元 | ok（無イベント投入は接続前の送信を避けるため。agent前提） |
| `items.yml` ルート配列（必須・default付き） | スキーマ | `equipmentSlotCount`（必須）。`playerInventorySlotLevels` の `optional: true` は前例にしない（規約違反の残骸） | ok |
| `BlockPlaceOnVein/GearConnected` タスク（候補積み＋ティック判定） | Game.Challenge | `EquipItemChallengeTask`（ユーザー裁定 2026-08-23 の完了カスケード規則） | ok |
| 鉱脈判定 `ServerContext.ItemMapVeinDatastore.GetOverVeins` | Game.Challenge から Game.Map.Interface | `VanillaMinerProcessorComponent.SetMiningItem` | ok |
| `VeinRestrictedPlacementState`（共有状態・UniRx通知） | Client.Game PlaceSystem | `PlacementSelection`（共有選択モデル1本化の裁定） | ok |
| `VeinRestrictedPlacementReporter`（static reporter・cursorIndex・1行理由） | Client.Game PlaceSystem/Common 隣接 | `MinerVeinPlacementReporter` | ok |
| `IMapVeinRangeView.SetHighlightedVein` をPlaceBlockStateからプッシュ | Client.Game UIState | `SetVisibleVeinKind` のプッシュ（PlaceBlockState ctor購読） | ok |
| チュートリアルmanager（MonoBehaviour＋`[Inject]`＋SerializeField登録＋TutorialManager ctor引数） | Client.Game Tutorial | `BlockPlacePreviewTutorialManager` | ok。`ITutorialWorldPin` を名乗って登録を横取りする案は役割不一致で棄却 |
| `GearConnectPairResolver`（サーバー共有の位置計算＋判定をクライアントで実行） | Client.Game PlaceSystem/Common/GearConnect | `ClientElectricWireAutoConnectCollector`（`Server.Protocol...ElectricWire` の共有ロジック呼び出し） | ok |
| `GearConnectPreviewRenderer`（GameObject＋LineRenderer） | Client.Game | `AutoConnectWirePreviewRenderer` | ok |
| prefabへの `GearConnectorView` 追加 | AddressableResources | `GearChainPole.prefab` | ok（uloop経由） |

機能パリティ（死活表）: 設置モードの全操作（R回転・Q/E高さ・Tab・B・G・中クリック・Ctrl+Z・V）は不変。採掘機の鉱脈外不可（`MinerVeinPlacementReporter`）は不変。鉱脈範囲の種別表示は、チュートリアル非適用時は従来どおり（強調モードは `null` で解除）。開幕スキット・装備ドラッグの説明は姉妹planの範囲。

---

### Task 0: worktree 作成と設計文書のコミット

**Files:**
- Add: `docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md`、`.decisions/2026-08-27-序盤圧縮はレシピ構成を変えず研究削除と要求数変更で行う.md`、`.decisions/2026-08-27-石器ラインは削除し開幕スキットは木の伐採へ付け替える.md`、`.decisions/2026-08-27-木の鉱脈チュートリアルは対象鉱脈だけハイライトし設置もそれに限定する.md`、`.decisions/2026-08-27-歯車接続は常設で明示し風車と粉砕機の接続チュートリアルを入れる.md`、`docs/superpowers/plans/2026-08-28-placement-guided-tutorials-and-initial-equipment.md`、`docs/superpowers/plans/2026-08-28-early-game-compression-master-data.md`

- [ ] **Step 1: worktree を作る（Editor 起動込み）**

```bash
cd ~/hermes-agent/data/repos/moorestech
moores-wt new feature/placement-guided-tutorials --dir placement-guided-tutorials --from master --fetch
```
Expected: `~/hermes-agent/data/repos/moorestech-worktrees/placement-guided-tutorials` が作られ Library コピーと `uloop launch` が走る。

- [ ] **Step 2: メインに未追跡で置かれた設計文書をコピーしてコミットする**

```bash
WT=~/hermes-agent/data/repos/moorestech-worktrees/placement-guided-tutorials
cd ~/hermes-agent/data/repos/moorestech
cp docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md $WT/docs/adr/
cp .decisions/2026-08-27-*.md $WT/.decisions/
cp docs/superpowers/plans/2026-08-28-placement-guided-tutorials-and-initial-equipment.md docs/superpowers/plans/2026-08-28-early-game-compression-master-data.md $WT/docs/superpowers/plans/
cd $WT && git add docs .decisions && git commit -m "docs: ADR 0038 序盤圧縮と設置システム案内チュートリアルの設計と裁定を記録する"
```
Expected: 7ファイルのコミット。

---

### Task 1: スキーマ拡張（items / challenges）とJSON一括更新、生成トリガ

**Files:**
- Modify: `VanillaSchema/items.yml`（末尾）、`VanillaSchema/challenges.yml:70-77`（taskCompletionType）、`:104-133`（taskParam cases）、`:143-152`（tutorialType）、`:249-`（tutorialParam cases）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: 上記 File Structure のJSON 9ファイル（本体3＋`$MW` 6）

**Interfaces:**
- Produces: 生成型 `Items.InitialEquipmentItems : InitialEquipmentItemsElement[]`（`ItemGuid : Guid`, `ItemCount : int`）、`BlockPlaceOnVeinTaskParam`（`BlockGuid`, `VeinGuid`）、`GearConnectedBlockTaskParam`（`BlockGuid`）、`VeinRestrictedPlacementTutorialParam`（`VeinGuid`, `BlockGuid`）、`RelativeBlockPlacePreviewTutorialParam`（`AnchorBlockGuid`, `BlockGuid`, `Offset : Vector3Int`, `BlockDirection : string`, `Message : string`）、`TutorialsElement.TutorialTypeConst.veinRestrictedPlacement / relativeBlockPlacePreview`

- [ ] **Step 1: マスタ worktree を作る**

```bash
git -C ~/hermes-agent/data/repos/moorestech_master fetch -q origin
git -C ~/hermes-agent/data/repos/moorestech_master worktree add -b feature/initial-equipment-items-field ~/hermes-agent/data/repos/moorestech-master-worktrees/placement-guided-tutorials origin/master
git -C ~/hermes-agent/data/repos/moorestech-master-worktrees/placement-guided-tutorials log --oneline -1
```
Expected: origin/master の HEAD（`9b09966` 以降）。以降 `$MW` と呼ぶ。

- [ ] **Step 2: items.yml ルートへ追記する**

`VanillaSchema/items.yml` の末尾（`- key: equipmentSlotCount` / `type: integer` の直後、同じインデント）に追加:

```yaml
- key: initialEquipmentItems
  type: array
  items:
    type: object
    properties:
    - key: itemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
    - key: itemCount
      type: integer
      default: 1
```

- [ ] **Step 3: challenges.yml へ完了判定2種を追記する**

`taskCompletionType` の `options:` に `- blockPlaceOnVein` と `- gearConnectedBlock` を追加（`- inInventoryItem` の後）。`taskParam` の `cases:` に `- when: equipItem` ブロックの後で追加:

```yaml
          - when: blockPlaceOnVein
            type: object
            properties:
            - key: blockGuid
              type: uuid
              foreignKey:
                schemaId: blocks
                foreignKeyIdPath: /data/[*]/blockGuid
                displayElementPath: /data/[*]/name
            - key: veinGuid
              type: uuid
              foreignKey:
                schemaId: map
                foreignKeyIdPath: /mapVeins/[*]/veinGuid
                displayElementPath: /mapVeins/[*]/veinName
          - when: gearConnectedBlock
            type: object
            properties:
            - key: blockGuid
              type: uuid
              foreignKey:
                schemaId: blocks
                foreignKeyIdPath: /data/[*]/blockGuid
                displayElementPath: /data/[*]/name
```

- [ ] **Step 4: challenges.yml へチュートリアルtype 2種を追記する**

`tutorialType` の `options:` 末尾に `- veinRestrictedPlacement` と `- relativeBlockPlacePreview` を追加。`tutorialParam` の `cases:` 末尾（`- when: uiDragGuide` ブロックの後）に追加:

```yaml
              - when: veinRestrictedPlacement
                type: object
                properties:
                - key: veinGuid
                  type: uuid
                  foreignKey:
                    schemaId: map
                    foreignKeyIdPath: /mapVeins/[*]/veinGuid
                    displayElementPath: /mapVeins/[*]/veinName
                - key: blockGuid
                  type: uuid
                  foreignKey:
                    schemaId: blocks
                    foreignKeyIdPath: /data/[*]/blockGuid
                    displayElementPath: /data/[*]/name
              - when: relativeBlockPlacePreview
                type: object
                openedByDefault: true
                properties:
                - key: anchorBlockGuid
                  type: uuid
                  foreignKey:
                    schemaId: blocks
                    foreignKeyIdPath: /data/[*]/blockGuid
                    displayElementPath: /data/[*]/name
                - key: blockGuid
                  type: uuid
                  foreignKey:
                    schemaId: blocks
                    foreignKeyIdPath: /data/[*]/blockGuid
                    displayElementPath: /data/[*]/name
                - key: offset
                  type: vector3Int
                  default:
                  - 0
                  - 0
                  - 0
                - key: blockDirection
                  type: enum
                  default: North
                  options:
                  - UpNorth
                  - UpEast
                  - UpSouth
                  - UpWest
                  - North
                  - East
                  - South
                  - West
                  - DownNorth
                  - DownEast
                  - DownSouth
                  - DownWest
                - key: message
                  type: string
                  default: Place the block next to the highlighted one
```

- [ ] **Step 5: JSON 9ファイルの items ルートへ `initialEquipmentItems` を足す**

```bash
cd $WT && python3 - <<'EOF'
import json, os
MW=os.path.expanduser('~/hermes-agent/data/repos/moorestech-master-worktrees/placement-guided-tutorials')
targets = {
 'moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json': [{'itemGuid':'00000000-0000-0000-1234-000000000001','itemCount':1}],
 'moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/items.json': [],
 'mooresmaster/mooresmaster.SandBox/TestMod/items.json': [],
 f'{MW}/server/mods/moorestechAlphaMod_3/master/items.json': [],
 f'{MW}/server_v4/mods/moorestechAlphaMod_4/master/items.json': [],
 f'{MW}/server_v5/mods/moorestechAlphaMod_5/master/items.json': [],
 f'{MW}/server_v6/mods/moorestechAlphaMod_6/master/items.json': [],
 f'{MW}/server_v7/mods/moorestechAlphaMod_7/master/items.json': [],
 f'{MW}/server_v8/mods/moorestechAlphaMod_8/master/items.json': [{'itemGuid':'4c5fefbd-60a4-42ea-b70a-38a83b96e25e','itemCount':1}],
}
for path, value in targets.items():
    raw = open(path, encoding='utf-8').read()
    d = json.loads(raw)
    assert 'initialEquipmentItems' not in d, path
    d['initialEquipmentItems'] = value
    indent = 4 if raw.lstrip().startswith('{\n    ') else 2
    trailing_newline = raw.endswith('\n')
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(d, f, ensure_ascii=False, indent=indent)
        if trailing_newline: f.write('\n')
    print('ok', path)
EOF
```
Expected: 9行の `ok`。`git diff --stat`（本体・`$MW` とも）が各ファイル数行の追加だけで全文差分になっていないこと（なっていたら indent 判定を実ファイルに合わせ `git checkout -- <file>` からやり直す）。石の斧の GUID `4c5fefbd-60a4-42ea-b70a-38a83b96e25e` は v8 items.json の `石の斧` と一致していること（`grep -c 4c5fefbd $MW/server_v8/mods/moorestechAlphaMod_8/master/items.json` が 1 以上）。

- [ ] **Step 6: 生成トリガを更新してコンパイルする**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を任意の新しいGUID文字列（例: `python3 -c "import uuid;print(uuid.uuid4())"` の出力）へ書き換える。

```bash
cd $WT && uloop compile --project-path ./moorestech_client
```
Expected: 成功（新型は未使用なので参照エラーは出ない）。`Items.InitialEquipmentItems` 等の生成名は `uloop execute-dynamic-code` 等で確認せず、次タスクのコンパイルで確定させる。

- [ ] **Step 7: コミットする（本体・マスタ）**

```bash
cd $WT && git add VanillaSchema moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Tests.Module moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData mooresmaster/mooresmaster.SandBox/TestMod/items.json && git commit -m "schema: items.initialEquipmentItems と challenges の blockPlaceOnVein/gearConnectedBlock/veinRestrictedPlacement/relativeBlockPlacePreview を追加する"
cd $MW && git add -A server server_v4 server_v5 server_v6 server_v7 server_v8 && git commit -m "data: items.json に initialEquipmentItems を追加し v8 は石の斧を初期装備にする (moorestech ADR 0038)"
```

---

### Task 2: マスタ検証（ChallengeMasterUtil / ItemMasterUtil）と MasterSourceTextCollector

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs:84-96`（TaskParam）、`:175-198`（Tutorial）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ItemMasterUtil.cs`（`Validate`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs:123-133`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Challenge/ChallengeMasterValidationTest.cs`（既存。フィクスチャ更新で通ることを確認）

- [ ] **Step 1: ChallengeMasterUtil に TaskParam の case を足す**

`case EquipItemTaskParam equipItem: {...}` の直後、`default:` の前に挿入:

```csharp
                            case BlockPlaceOnVeinTaskParam blockPlaceOnVein:
                            {
                                if (MasterHolder.BlockMaster.GetBlockIdOrNull(blockPlaceOnVein.BlockGuid) == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.BlockGuid:{blockPlaceOnVein.BlockGuid}\n";
                                }
                                if (MasterHolder.MapVeinMaster.GetElementOrNull(blockPlaceOnVein.VeinGuid) == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.VeinGuid:{blockPlaceOnVein.VeinGuid}\n";
                                }
                                break;
                            }
                            case GearConnectedBlockTaskParam gearConnectedBlock:
                            {
                                if (MasterHolder.BlockMaster.GetBlockIdOrNull(gearConnectedBlock.BlockGuid) == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.BlockGuid:{gearConnectedBlock.BlockGuid}\n";
                                }
                                break;
                            }
```

- [ ] **Step 2: ChallengeMasterUtil に Tutorial の case を足す**

`case BlockPlacePreviewTutorialParam blockPlacePreview: {...}` の直後に挿入:

```csharp
                                case VeinRestrictedPlacementTutorialParam veinRestricted:
                                {
                                    if (MasterHolder.MapVeinMaster.GetElementOrNull(veinRestricted.VeinGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.VeinGuid:{veinRestricted.VeinGuid}\n";
                                    }
                                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(veinRestricted.BlockGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.BlockGuid:{veinRestricted.BlockGuid}\n";
                                    }
                                    break;
                                }
                                case RelativeBlockPlacePreviewTutorialParam relativePreview:
                                {
                                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(relativePreview.AnchorBlockGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.AnchorBlockGuid:{relativePreview.AnchorBlockGuid}\n";
                                    }
                                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(relativePreview.BlockGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.BlockGuid:{relativePreview.BlockGuid}\n";
                                    }
                                    break;
                                }
```

- [ ] **Step 3: ItemMasterUtil.Validate に初期装備の検証を足す**

`ItemMasterUtil.Validate(Items items, out string errorLogs)` の既存ログ連結の末尾（`errorLogs` を確定する直前）に、既存のローカル関数群と同じ形で追加する:

```csharp
            // 初期装備は未定義アイテムを指せない。装備スロット数を超える分は投入時に捨てられるためここで弾く
            // Initial equipment must reference defined items; entries beyond the equipment slot count would be dropped on grant, so reject them here
            string InitialEquipmentValidation()
            {
                var logs = "";
                var itemGuids = new HashSet<Guid>();
                foreach (var element in items.Data) itemGuids.Add(element.ItemGuid);
                foreach (var initial in items.InitialEquipmentItems)
                {
                    if (!itemGuids.Contains(initial.ItemGuid)) logs += $"[ItemMaster] initialEquipmentItems has invalid itemGuid:{initial.ItemGuid}\n";
                    if (initial.ItemCount <= 0) logs += $"[ItemMaster] initialEquipmentItems itemGuid:{initial.ItemGuid} has non-positive itemCount:{initial.ItemCount}\n";
                }
                if (items.EquipmentSlotCount < items.InitialEquipmentItems.Length) logs += $"[ItemMaster] initialEquipmentItems count:{items.InitialEquipmentItems.Length} exceeds equipmentSlotCount:{items.EquipmentSlotCount}\n";
                return logs;
            }
```
`Validate` 本体で `errorLogs += InitialEquipmentValidation();` を他の検証呼び出しと同じ位置に足す（`Validate` の構造がローカル関数群でない場合は同ファイルの既存検証と同じ形式に合わせ、`errorLogs` に連結する）。`using System.Collections.Generic;` が無ければ足す。

- [ ] **Step 4: MasterSourceTextCollector の switch を拡張する**

`moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs:130` の `BlockPlacePreviewTutorialParam blockPlacePreview => blockPlacePreview.Message,` の直後に追加:

```csharp
                    RelativeBlockPlacePreviewTutorialParam relativePreview => relativePreview.Message,
                    VeinRestrictedPlacementTutorialParam => null,
```

- [ ] **Step 5: コンパイルし、既存の検証テストを回す**

```bash
cd $WT && uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ChallengeMasterValidationTest|MasterSourceTextCollectorTest|EquipmentSlotCountTest"
```
Expected: 全件PASS。

- [ ] **Step 6: コミットする**

```bash
cd $WT && git add moorestech_server/Assets/Scripts/Core.Master/Validator moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs && git commit -m "master: 新チャレンジ判定・チュートリアル型と initialEquipmentItems の検証を追加する"
```

---

### Task 3: 初期装備の投入（サーバー）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/InitialEquipmentMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory/PlayerInventoryDataStore.cs:47-59`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/InitialEquipmentTest.cs`

**Interfaces:**
- Produces: `public static class InitialEquipmentMasterUtil { public static List<IItemStack> CreateInitialEquipmentStacks(IItemStackFactory itemStackFactory) }`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class InitialEquipmentTest
    {
        private const int PlayerId = 0;
        // ForUnitTest items.json の initialEquipmentItems は Test1×1
        // ForUnitTest items.json declares initialEquipmentItems = Test1×1
        private static readonly Guid Test1Guid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 新規プレイヤーの装備スロット0に初期装備が入り選択済みになる()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();

            var equipment = inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory;

            var expectedId = MasterHolder.ItemMaster.GetItemId(Test1Guid);
            Assert.AreEqual(expectedId, equipment.GetItem(0).Id);
            Assert.AreEqual(1, equipment.GetItem(0).Count);
            Assert.AreEqual(expectedId, equipment.GetSelectedItem().Id);
        }

        [Test]
        public void セーブからロードしたプレイヤーには再投入しない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();

            // 初期装備を空にしてセーブし、ロード後に復活しないこと
            // Empty the initial equipment, save, and confirm it does not come back on load
            var equipment = inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory;
            equipment.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            var saved = inventoryDataStore.GetSaveJsonObject();

            inventoryDataStore.LoadPlayerInventory(saved);

            Assert.AreEqual(0, inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory.GetItem(0).Count);
        }
    }
}
```
`using Game.Context;` を追加する。空スタック生成メソッドの実名（`CreatEmpty`）は `moorestech_server/Assets/Scripts/Core.Item.Interface/IItemStackFactory.cs` で確認し、違えばその名前に合わせる。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "InitialEquipmentTest"
```
Expected: 1件目 FAIL（スロット0が空）。

- [ ] **Step 3: マスタ読取 util を書く**

`moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/InitialEquipmentMasterUtil.cs`:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     items.json ルートの initialEquipmentItems を装備スロット順のスタック列へ解決する（マスタは読むだけ）
    ///     Resolves items.json root initialEquipmentItems into a slot-ordered stack list (master is read only)
    /// </summary>
    public static class InitialEquipmentMasterUtil
    {
        public static List<IItemStack> CreateInitialEquipmentStacks(IItemStackFactory itemStackFactory)
        {
            var stacks = new List<IItemStack>();
            foreach (var element in MasterHolder.ItemMaster.Items.InitialEquipmentItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(element.ItemGuid);
                stacks.Add(itemStackFactory.Create(itemId, element.ItemCount));
            }
            return stacks;
        }
    }
}
```

- [ ] **Step 4: PlayerInventoryDataStore の新規生成分岐で投入する**

`PlayerInventoryDataStore.GetInventoryData` を次に置き換える:

```csharp
        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _slotLevelDataStore.CurrentSlotCount);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent);
                var equipment = new EquipmentInventoryData(playerId, _equipmentInventoryUpdateEvent);

                // 新規プレイヤーだけが通る分岐。接続前にイベントを飛ばさないため無イベント復元と同じ経路で入れる
                // Only brand-new players reach here; use the event-free restore path so nothing is sent before the client connects
                var initialStacks = InitialEquipmentMasterUtil.CreateInitialEquipmentStacks(ServerContext.ItemStackFactory);
                var overflow = equipment.RestoreFromSave(initialStacks, 0);
                foreach (var stack in overflow)
                {
                    if (stack.Count == 0) continue;
                    Debug.LogError($"初期装備が装備スロットに収まりません playerId:{playerId} itemId:{stack.Id} count:{stack.Count}");
                }

                _playerInventoryData.Add(playerId, new PlayerInventoryData(main, grab, equipment));
            }

            return _playerInventoryData[playerId];
        }
```
`using Game.Context;` を追加する。`RestoreFromSave(List<IItemStack>, int)` は `EquipmentInventoryData.cs:75` の既存メソッド。

- [ ] **Step 5: テストを実行して通ることを確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "InitialEquipmentTest|EquipmentInventory|EquipItemChallengeTaskTest|PlayerInventory"
```
Expected: 全件PASS。`EquipItemChallengeTaskTest` が初期装備（Test1）で挙動を変えていないこと（equipItem のフィクスチャ対象は Test1 なので `AlreadyEquippedCompletesOnFirstTick` 系が変わらず通る。落ちる場合はそのテストの前提「開始時は非装備」が初期装備で崩れているので、テスト側で `SetItem(0, empty)` を先に呼ぶ形に直す）。

- [ ] **Step 6: コミットする**

```bash
cd $WT && git add moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/InitialEquipmentMasterUtil.cs moorestech_server/Assets/Scripts/Game.PlayerInventory/PlayerInventoryDataStore.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/InitialEquipmentTest.cs && git commit -m "feat(inventory): 新規プレイヤーへマスタの初期装備を無イベントで投入する"
```

---

### Task 4: `blockPlaceOnVein` 完了判定（サーバー）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/BlockPlaceOnVeinChallengeTask.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs`、`ChallengeFactory.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json`（Category1 に `…103`）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetChallengeInfoProtocolTest.cs:48,73,115`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlockPlaceOnVeinChallengeTaskTest.cs`

**Interfaces:**
- Produces: `VanillaChallengeType.BlockPlaceOnVeinTask = "blockPlaceOnVein"`、`VanillaChallengeType.GearConnectedBlockTask = "gearConnectedBlock"`（Task 5 が使う）

- [ ] **Step 1: フィクスチャに新チャレンジを足す（103 = 鉱脈上設置、104 = 歯車接続。104 は Task 5 で使う）**

```bash
cd $WT && python3 - <<'EOF'
import json
p='moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json'
raw=open(p,encoding='utf-8').read(); d=json.loads(raw)
cat=d['data'][0]; assert cat['categoryName']=='Category1'
base=cat['challenges'][2]; assert base['taskCompletionType']=='blockPlace'
def clone(guid,title,task,param):
    c=json.loads(json.dumps(base)); c['challengeGuid']=guid; c['title']=title; c['summary']=title
    c['taskCompletionType']=task; c['taskParam']=param; c['tutorials']=[]; c['prevChallengeGuids']=[]
    return c
cat['challenges'].append(clone('00000000-0000-0000-4567-000000000103','鉱脈上に設置する','blockPlaceOnVein',
    {'blockGuid':'00000000-0000-0000-0000-000000000001','veinGuid':'11111111-0000-0000-0000-000000000001'}))
cat['challenges'].append(clone('00000000-0000-0000-4567-000000000104','シャフトを回す','gearConnectedBlock',
    {'blockGuid':'00000000-0000-0000-0000-00000000000e'}))
indent=4 if raw.lstrip().startswith('{\n    ') else 2
open(p,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=indent)+('\n' if raw.endswith('\n') else ''))
print('ok', len(cat['challenges']))
EOF
```
Expected: `ok 9`。ブロック `…0001` は既存 `テスト3` が使う1x1x1ブロック、鉱脈 `11111111-…0001` は `Tests.Module/TestMod/ForUnitTest/map/map.json` で (0,5,0)〜(0,5,0) の item 鉱脈、`…000e` は `ForUnitTestModBlockId.Shaft`。

- [ ] **Step 2: GetChallengeInfoProtocolTest の初期件数を更新する**

`:48` と `:73` の `Assert.AreEqual(5, ...)` を `7` に、`:115` の `Assert.AreEqual(3, ...)` を `5` に変え、`:47` のコメントを `// 最初は7件（1,2,3,101,102,103,104）` に直す。

- [ ] **Step 3: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlockPlaceOnVeinChallengeTaskTest.cs`:

```csharp
using System;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Challenge;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlockPlaceOnVeinChallengeTaskTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000103");
        private static readonly BlockId TargetBlockId = ForUnitTestModBlockId.BlockId; // 00000000-0000-0000-0000-000000000001
        // ForUnitTest map.json の item 鉱脈 11111111-…0001 は (0,5,0) の1セル
        // The ForUnitTest item vein 11111111-…0001 occupies the single cell (0,5,0)
        private static readonly Vector3Int VeinCell = new(0, 5, 0);
        private static readonly Vector3Int OutsideCell = new(3, 3, 3);

        [Test]
        public void 指定鉱脈の上に設置したら次のティックで完了する()
        {
            var challengeDatastore = CreateAndStart();

            ServerContext.WorldBlockDatastore.TryAddBlock(TargetBlockId, VeinCell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.IsFalse(IsCompleted(challengeDatastore), "completed inside the placement event instead of on the tick");

            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        [Test]
        public void 鉱脈外に設置しても完了しない()
        {
            var challengeDatastore = CreateAndStart();

            ServerContext.WorldBlockDatastore.TryAddBlock(TargetBlockId, OutsideCell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.UpdateOneTick();

            Assert.IsFalse(IsCompleted(challengeDatastore));
        }

        [Test]
        public void チャレンジ開始前に置かれたブロックも初回ティックで回収する()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(TargetBlockId, VeinCell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        private static ChallengeDatastore CreateAndStart()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            return challengeDatastore;
        }

        private static bool IsCompleted(ChallengeDatastore challengeDatastore)
        {
            return challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid);
        }
    }
}
```
`ForUnitTestModBlockId.BlockId` はGUID `…0001` のアクセサ名を `Tests.Module/TestMod/ForUnitTestModBlockId.cs` で確認して置き換える（`テスト3` が指すブロック）。

- [ ] **Step 4: テストを実行して失敗を確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BlockPlaceOnVeinChallengeTaskTest"
```
Expected: `KeyNotFoundException`（ファクトリ未登録）で FAIL。

- [ ] **Step 5: タスクを実装する**

`VanillaChallengeType.cs` に追加:

```csharp
        public const string BlockPlaceOnVeinTask = "blockPlaceOnVein";
        public const string GearConnectedBlockTask = "gearConnectedBlock";
```

`ChallengeFactory` ctor に追加:

```csharp
            _taskCreators.Add(VanillaChallengeType.BlockPlaceOnVeinTask,BlockPlaceOnVeinChallengeTask.Create);
```

`BlockPlaceOnVeinChallengeTask.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;

namespace Game.Challenge.Task
{
    /// <summary>
    ///     指定ブロックが指定鉱脈の上に置かれた時に達成する（採掘機はドリルセル、他は占有セルのいずれかで判定）
    ///     Completes when the block is placed over the vein (drill cell for miners, any footprint cell otherwise)
    /// </summary>
    public class BlockPlaceOnVeinChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        // イベントは判定対象ブロックを積むだけで、判定と発火はティックで行う（前例: EquipItemChallengeTask）
        // Events only enqueue blocks to check; the check and completion fire on the tick (precedent: EquipItemChallengeTask)
        private readonly List<IBlock> _blocksToCheck = new();

        private readonly Guid _targetBlockGuid;
        private readonly Guid _targetVeinGuid;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new BlockPlaceOnVeinChallengeTask(challengeMasterElement);
        }

        private BlockPlaceOnVeinChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            var param = (BlockPlaceOnVeinTaskParam)challengeMasterElement.TaskParam;
            _targetBlockGuid = param.BlockGuid;
            _targetVeinGuid = param.VeinGuid;

            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }

        public void ManualUpdate()
        {
            if (_completed) return;

            EnqueueInitialCheckOnce();

            foreach (var block in _blocksToCheck)
            {
                if (!IsOverTargetVein(block)) continue;
                _completed = true;
                break;
            }
            _blocksToCheck.Clear();

            if (_completed) _onChallengeComplete.OnNext(this);

            #region Internal

            // チャレンジ開始前から置かれていた対象ブロックを初回ティックで回収する
            // Recover target blocks placed before this challenge started, on the first tick
            void EnqueueInitialCheckOnce()
            {
                if (_initialCheckDone) return;
                _initialCheckDone = true;
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    if (data.Block.BlockGuid == _targetBlockGuid) _blocksToCheck.Add(data.Block);
                }
            }

            bool IsOverTargetVein(IBlock block)
            {
                foreach (var cell in CellsToTest(block))
                {
                    foreach (var vein in ServerContext.ItemMapVeinDatastore.GetOverVeins(cell))
                    {
                        if (vein.VeinGuid == _targetVeinGuid) return true;
                    }
                }
                return false;
            }

            // 採掘機は実際に掘るドリルセルだけを見る（VanillaMinerProcessorComponent と同じ基準）
            // A miner is judged by its actual drill cell only (same rule as VanillaMinerProcessorComponent)
            IEnumerable<Vector3Int> CellsToTest(IBlock block)
            {
                var positionInfo = block.BlockPositionInfo;
                if (MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).BlockParam is IMinerParam minerParam)
                {
                    yield return positionInfo.ConvertBlockLocalToWorldCell(minerParam.DrillLocalPosition);
                    yield break;
                }
                for (var x = positionInfo.MinPos.x; x <= positionInfo.MaxPos.x; x++)
                for (var y = positionInfo.MinPos.y; y <= positionInfo.MaxPos.y; y++)
                for (var z = positionInfo.MinPos.z; z <= positionInfo.MaxPos.z; z++)
                    yield return new Vector3Int(x, y, z);
            }

            #endregion
        }

        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _blocksToCheck.Add(block);
        }
    }
}
```

- [ ] **Step 6: テストを実行して通ることを確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BlockPlaceOnVeinChallengeTaskTest|GetChallengeInfoProtocolTest|ChallengeSaveLoadTest"
```
Expected: 全件PASS（`ChallengeSaveLoadTest` に件数依存があれば同様に +2 する）。

- [ ] **Step 7: コミットする**

```bash
cd $WT && git add moorestech_server/Assets/Scripts/Game.Challenge moorestech_server/Assets/Scripts/Tests moorestech_server/Assets/Scripts/Tests.Module && git commit -m "feat(challenge): 鉱脈上設置で完了する blockPlaceOnVein 判定を追加する"
```

---

### Task 5: `gearConnectedBlock` 完了判定（サーバー）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/GearConnectedBlockChallengeTask.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/GearConnectedBlockChallengeTaskTest.cs`

**Interfaces:**
- Consumes: Task 4 のフィクスチャ `…104`（`gearConnectedBlock{blockGuid: Shaft}`）と `VanillaChallengeType.GearConnectedBlockTask`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Challenge;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class GearConnectedBlockChallengeTaskTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000104");

        [Test]
        public void 発電機に繋がったシャフトが回ると完了する()
        {
            var challengeDatastore = CreateAndStart();
            var world = ServerContext.WorldBlockDatastore;

            // GearNetworkTest と同じ配置。発電機(0,0,0)の隣にシャフト(0,0,1)
            // Same layout as GearNetworkTest: generator at (0,0,0), shaft at (0,0,1)
            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            GameUpdater.UpdateOneTick();
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        [Test]
        public void 動力の無いシャフトでは完了しない()
        {
            var challengeDatastore = CreateAndStart();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(10, 0, 10), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            GameUpdater.UpdateOneTick();
            GameUpdater.UpdateOneTick();

            Assert.IsFalse(IsCompleted(challengeDatastore));
        }

        private static ChallengeDatastore CreateAndStart()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            return challengeDatastore;
        }

        private static bool IsCompleted(ChallengeDatastore challengeDatastore)
        {
            return challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GearConnectedBlockChallengeTaskTest"
```
Expected: `KeyNotFoundException` で FAIL。

- [ ] **Step 3: タスクを実装する**

`ChallengeFactory` ctor に追加:

```csharp
            _taskCreators.Add(VanillaChallengeType.GearConnectedBlockTask,GearConnectedBlockChallengeTask.Create);
```

`GearConnectedBlockChallengeTask.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    /// <summary>
    ///     指定ブロックのいずれかが歯車動力を受けて回り始めた時に達成する
    ///     Completes when any placed block of the given type starts receiving gear power
    /// </summary>
    public class GearConnectedBlockChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        // 監視対象は設置で増え撤去で減る。RPM は歯車NWの整定後にティックで読む
        // Candidates grow on placement and shrink on removal; RPM is read on the tick after the gear network settles
        private readonly HashSet<IBlock> _candidates = new();

        private readonly Guid _targetBlockGuid;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new GearConnectedBlockChallengeTask(challengeMasterElement);
        }

        private GearConnectedBlockChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            _targetBlockGuid = ((GearConnectedBlockTaskParam)challengeMasterElement.TaskParam).BlockGuid;

            var worldEvent = ServerContext.WorldBlockUpdateEvent;
            worldEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
            worldEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }

        public void ManualUpdate()
        {
            if (_completed) return;

            EnqueueInitialCheckOnce();

            foreach (var block in _candidates)
            {
                if (!block.TryGetComponent<IGearEnergyTransformer>(out var transformer)) continue;
                if (transformer.CurrentRpm.AsPrimitive() <= 0f) continue;
                _completed = true;
                break;
            }

            if (_completed) _onChallengeComplete.OnNext(this);

            #region Internal

            void EnqueueInitialCheckOnce()
            {
                if (_initialCheckDone) return;
                _initialCheckDone = true;
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    if (data.Block.BlockGuid == _targetBlockGuid) _candidates.Add(data.Block);
                }
            }

            #endregion
        }

        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            var block = properties.BlockData.Block;
            if (block.BlockGuid == _targetBlockGuid) _candidates.Add(block);
        }

        private void OnBlockRemove(BlockRemoveProperties properties)
        {
            _candidates.Remove(properties.BlockData.Block);
        }
    }
}
```
`BlockRemoveProperties` のプロパティ名は `Game.World.Interface/DataStore/BlockRemoveProperties.cs` で確認する（`BlockData` でなければその名前に合わせる）。`RPM.AsPrimitive()` が無ければ `RPM` の暗黙変換 `(float)transformer.CurrentRpm` に置き換える。

- [ ] **Step 4: テストを実行して通ることを確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GearConnectedBlockChallengeTaskTest|GearNetworkTest"
```
Expected: 全件PASS。

- [ ] **Step 5: コミットする**

```bash
cd $WT && git add moorestech_server/Assets/Scripts/Game.Challenge moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/GearConnectedBlockChallengeTaskTest.cs && git commit -m "feat(challenge): 歯車動力を受けたら完了する gearConnectedBlock 判定を追加する"
```

---

### Task 6: 鉱脈台帳の GUID 対応と鉱脈範囲表示の強調モード（クライアント）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinAabb.cs`、`MapVeinAabbRegistry.cs`、`IMapVeinRangeView.cs`、`MapVeinRangeViewService.cs`、`MapVeinRangeBoxMaterials.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/Fakes/FakeMapVeinRangeView.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/MapVeinRangeViewHighlightTest.cs`（`MapVeinRangeViewMaterialReuseTest.cs` の SetUp/ヘルパを写す）

**Interfaces:**
- Produces: `MapVeinAabb.VeinGuid : Guid`、`MapVeinAabbRegistry.IsInsideVein(Vector3Int cell, Guid veinGuid) : bool`、`IMapVeinRangeView.SetHighlightedVein(Guid? veinGuid)`、`MapVeinRangeBoxMaterials.HighlightMaterial`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/Map/MapVeinRangeViewHighlightTest.cs`（`MapVeinRangeViewMaterialReuseTest.cs` の `CreateService()` / `CountVisibleBoxes(root)` ヘルパと同じ台帳（item鉱脈2・fluid鉱脈1）を使う。ヘルパはコピーせず、同ファイル内に同名 private static で再掲する）:

```csharp
using System;
using Client.Game.InGame.Map.MapVein;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map
{
    public class MapVeinRangeViewHighlightTest
    {
        private static readonly Guid ItemVeinA = Guid.Parse("11111111-0000-0000-0000-000000000001");
        private static readonly Guid ItemVeinB = Guid.Parse("11111111-0000-0000-0000-000000000004");

        [Test]
        public void 強調鉱脈を指定すると種別を問わずその鉱脈だけ表示する()
        {
            var (service, root) = CreateService();
            service.SetVisibleVeinKind(MapVeinKind.Item);
            Assert.AreEqual(2, CountVisibleBoxes(root));

            service.SetHighlightedVein(ItemVeinA);

            Assert.AreEqual(1, CountVisibleBoxes(root), "highlight mode must show exactly the target vein");
            var box = root.GetComponentInChildren<MeshRenderer>(false);
            StringAssert.Contains("Highlight", box.sharedMaterial.name);
        }

        [Test]
        public void 強調を解除すると種別表示へ戻る()
        {
            var (service, root) = CreateService();
            service.SetVisibleVeinKind(MapVeinKind.Item);
            service.SetHighlightedVein(ItemVeinB);

            service.SetHighlightedVein(null);

            Assert.AreEqual(2, CountVisibleBoxes(root));
        }

        [Test]
        public void 台帳はGUID指定の内包判定を返す()
        {
            var registry = CreateRegistry();
            Assert.IsTrue(registry.IsInsideVein(new Vector3Int(0, 0, 0), ItemVeinA));
            Assert.IsFalse(registry.IsInsideVein(new Vector3Int(0, 0, 0), ItemVeinB));
        }

        // CreateService / CreateRegistry / CountVisibleBoxes は MapVeinRangeViewMaterialReuseTest と同じ手順:
        // ForUnitTest サーバー生成 → VeinLayoutMessagePack 3件（ItemVeinA (0,0,0)-(2,2,2)、fluid (20,0,20)、ItemVeinB (30,0,30)-(31,0,31)）→ InitialHandshakeResponse → MapVeinAabbRegistry → new MapVeinRangeViewService(registry, camera at origin)
        // CreateService / CreateRegistry / CountVisibleBoxes follow MapVeinRangeViewMaterialReuseTest exactly with the three layouts listed above
    }
}
```
上のコメント部分は実コードに展開する: `MapVeinRangeViewMaterialReuseTest.cs` の `CreateService`/`CountVisibleBoxes` を開き、鉱脈レイアウトを ItemVeinA `(0,0,0)-(2,2,2)`、Fluid `11111111-…0002` `(20,0,20)`、ItemVeinB `11111111-…0004` `(30,0,30)-(31,0,31)` の3件にして同ファイルへ写す（`MinerVeinPlacementReporterTest.CreateRegistry` と同じ `ResponseMapDataMessagePack`/`InitialHandshakeResponse` 組み立て）。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client
```
Expected: `SetHighlightedVein` / `IsInsideVein(Vector3Int, Guid)` 未定義でコンパイルエラー。

- [ ] **Step 3: MapVeinAabb と Registry に GUID を通す**

`MapVeinAabb`: フィールド `public readonly Guid VeinGuid;` を追加し、ctor を `MapVeinAabb(Guid veinGuid, Vector3Int minCell, Vector3Int maxCell, MapVeinKind kind)` にして `VeinGuid = veinGuid;` を代入（`using System;`）。
`MapVeinAabbRegistry` ctor の `_veins.Add(new MapVeinAabb(minCell, maxCell, kind));` を `_veins.Add(new MapVeinAabb(veinGuid, minCell, maxCell, kind));` に変え、メソッドを追加:

```csharp
        /// <summary>
        ///     指定セルがその鉱脈（GUID）に入っているか。チュートリアルの「この鉱脈にだけ置く」制限が使う
        ///     Whether the cell sits inside that specific vein; used by the tutorial's "place only on this vein" restriction
        /// </summary>
        public bool IsInsideVein(Vector3Int cell, Guid veinGuid)
        {
            foreach (var vein in _veins)
                if (vein.VeinGuid == veinGuid && vein.ContainsCell(cell))
                    return true;

            return false;
        }
```

- [ ] **Step 4: 表示側に強調モードを足す**

`IMapVeinRangeView` に追加:

```csharp
        // 強調したい鉱脈の変化時にだけ呼ぶ。指定中は種別を問わずその鉱脈だけを描く。nullで種別表示へ戻る
        // Called only when the highlighted vein changes; while set, only that vein is drawn regardless of kind; null returns to kind view
        void SetHighlightedVein(Guid? veinGuid);
```
（`using System;`）

`MapVeinRangeBoxMaterials`: `private static readonly Color HighlightVeinColor = new(0.3f, 0.95f, 0.35f, 1f);`、`public readonly Material HighlightMaterial;`、ctor で `HighlightMaterial = CreateTranslucentMaterial("Highlight", HighlightVeinColor);`、`Dispose` で `Destroy(HighlightMaterial)`。先頭コメントの「2枚」を「3枚（item/fluid/highlight）」に直す。

`MapVeinRangeViewService`:
- `VeinRangeEntry` に `public readonly Guid VeinGuid;` を追加し ctor を `(Guid veinGuid, MapVeinKind kind, Bounds bounds, Material material)` にする。ctor の `_entries.Add(new VeinRangeEntry(vein.VeinGuid, vein.Kind, vein.Bounds, material));`
- フィールド `private Guid? _highlightedVeinGuid;` を追加。
- メソッド追加:

```csharp
        public void SetHighlightedVein(Guid? veinGuid)
        {
            _highlightedVeinGuid = veinGuid;
            ManualUpdate();
        }
```
- `ManualUpdate` の可視判定を置き換える:

```csharp
                var isVisible = IsTargetVein(entry) && IsWithinVisibleRadius(entry.Bounds, cameraPosition);
                if (isVisible) ShowEntry(entry);
                else HideEntry(entry);
```
とローカル関数:

```csharp
            // 強調中はその鉱脈だけ、通常は表示種別の鉱脈だけを対象にする
            // While highlighting, only that vein qualifies; otherwise only veins of the visible kind do
            bool IsTargetVein(VeinRangeEntry entry)
            {
                if (_highlightedVeinGuid.HasValue) return entry.VeinGuid == _highlightedVeinGuid.Value;
                return entry.Kind == _visibleVeinKind;
            }
```
- `ShowEntry` は強調中なら `HighlightMaterial` を使う。既存ボックスを再利用しないため、マテリアル切替時に一度畳む:

```csharp
            void ShowEntry(VeinRangeEntry entry)
            {
                var material = _highlightedVeinGuid.HasValue ? _boxMaterials.HighlightMaterial : entry.Material;
                if (entry.ViewObject != null)
                {
                    // 強調⇔通常の切替はマテリアル差し替えだけで済ませ、位置は動かさない
                    // Swapping highlight/normal only replaces the material; the box never moves
                    entry.ViewObject.GetComponent<MeshRenderer>().sharedMaterial = material;
                    return;
                }
                entry.ViewObject = RentBox(entry.Bounds, material);
            }
```

`FakeMapVeinRangeView` に追加:

```csharp
        public readonly List<Guid?> HighlightPushes = new();
        public void SetHighlightedVein(Guid? veinGuid) => HighlightPushes.Add(veinGuid);
```
（`using System;`）

- [ ] **Step 5: テストを実行して通ることを確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapVeinRangeView|MinerVeinPlacementReporterTest|PlacementVeinViewKindResolverTest|PlaceBlockState"
```
Expected: 全件PASS（`MapVeinRangeViewMaterialReuseTest` の「種別ごとに1枚共有」は強調未使用時の挙動なので不変）。

- [ ] **Step 6: コミットする**

```bash
cd $WT && git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein moorestech_client/Assets/Scripts/Client.Tests/Map moorestech_client/Assets/Scripts/Client.Tests/UIState/Fakes/FakeMapVeinRangeView.cs && git commit -m "feat(vein): 鉱脈台帳にGUIDを持たせ範囲表示に単一鉱脈の強調モードを足す"
```

---

### Task 7: 鉱脈限定設置の共有状態と reporter、PlaceBlockState への接続

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/VeinRestriction/VeinRestrictedPlacementState.cs`
- Create: `.../PlaceSystem/VeinRestriction/VeinRestrictedPlacementReporter.cs`
- Modify: `.../PlaceSystem/Common/CommonBlockPlaceSystem.cs`（ctor引数追加・reporter呼び出し）
- Modify: `.../UI/UIState/State/PlaceBlockState.cs`（ctor引数追加・強調プッシュ）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`（`VeinRestrictedPlacementState` 登録）
- Modify: `Localization/localization.csv`（`ui.tooltip.placeOutsideTutorialVein`）
- Test: `Client.Tests/PlaceSystem/VeinRestrictedPlacementReporterTest.cs`、既存 `Client.Tests/UIState/*PlaceBlockState*`（ctor引数追加に追随）

**Interfaces:**
- Produces: `VeinRestrictedPlacementState { Guid? VeinGuid {get; private set;} BlockId? BlockId {get; private set;} IObservable<Unit> OnChanged; void SetRestriction(Guid veinGuid, BlockId blockId); void Clear(); bool IsRestrictedBlock(BlockId blockId) }`、`VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(List<PlaceInfo>, BlockMasterElement, int cursorIndex, MapVeinAabbRegistry, VeinRestrictedPlacementState, PlacementFeedback)`、`LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein`

- [ ] **Step 1: localization.csv に理由行を足す**

`Localization/localization.csv` の `ui.tooltip.placeMinerOutsideVein` 行（214行目）の直後に追加:

```
ui.tooltip.placeOutsideTutorialVein,Place it on the highlighted vein,Place it on the highlighted vein,ハイライトされた鉱脈の上に設置してください,Platziere es auf der hervorgehobenen Erzader
```

```bash
cd $WT && uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true && uloop compile --project-path ./moorestech_client
```
Expected: 成功。

- [ ] **Step 2: 失敗するテストを書く**

`Client.Tests/PlaceSystem/VeinRestrictedPlacementReporterTest.cs`（`MinerVeinPlacementReporterTest` の `CreatePlaceInfo`/`CreateRegistry`/`CreateServer` を同ファイルへ再掲。台帳は ItemVein `11111111-…0001` `(0,0,0)-(2,2,2)` と ItemVeinB `11111111-…0004` `(30,0,30)-(31,0,31)` の2件）:

```csharp
        [Test]
        public void 対象鉱脈外のセルだけ不可にしカーソルセルに理由を出す()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(Guid.Parse(ItemVeinBGuid), ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(new Vector3Int(30, 0, 30), BlockDirection.North),
                CreatePlaceInfo(new Vector3Int(0, 0, 0), BlockDirection.North),
            };
            var feedback = new PlacementFeedback();

            VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(placeInfos, minerMaster, 1, CreateRegistry(), state, feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a cell over the target vein was rejected");
            Assert.IsFalse(placeInfos[1].Placeable, "a cell over another vein stayed placeable");
            CollectionAssert.AreEqual(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein) }, feedback.Lines);
        }

        [Test]
        public void 制限対象でないブロックは素通しする()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(Guid.Parse(ItemVeinBGuid), ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(0, 0, 0), BlockDirection.North) };
            var feedback = new PlacementFeedback();

            VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateRegistry(), state, feedback);

            Assert.IsTrue(placeInfos[0].Placeable);
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 制限が無ければ何もしない()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(50, 0, 50), BlockDirection.North) };
            var feedback = new PlacementFeedback();

            VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(placeInfos, minerMaster, 0, CreateRegistry(), new VeinRestrictedPlacementState(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable);
        }

        [Test]
        public void 状態の変更はOnChangedで通知されClearで消える()
        {
            var state = new VeinRestrictedPlacementState();
            var notified = 0;
            state.OnChanged.Subscribe(_ => notified++);

            state.SetRestriction(Guid.Parse(ItemVeinBGuid), ForUnitTestModBlockId.ElectricMinerId);
            state.Clear();

            Assert.AreEqual(2, notified);
            Assert.IsNull(state.VeinGuid);
            Assert.IsFalse(state.IsRestrictedBlock(ForUnitTestModBlockId.ElectricMinerId));
        }
```
`ForUnitTestModBlockId.ChestId` は `MinerVeinPlacementReporterTest.採掘機以外は鉱脈外でも素通しする` が使うチェストのアクセサ名に合わせる。

- [ ] **Step 3: テストを実行して失敗を確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client
```
Expected: 未定義型でコンパイルエラー。

- [ ] **Step 4: 共有状態を書く**

`VeinRestrictedPlacementState.cs`:

```csharp
using System;
using Core.Master;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction
{
    /// <summary>
    ///     「このブロックはこの鉱脈にしか置けない」という設置制限の共有状態。書き手はチュートリアル、読み手は設置判定と鉱脈表示
    ///     Shared "this block may only go on this vein" restriction; written by the tutorial, read by placement checks and the vein view
    /// </summary>
    public class VeinRestrictedPlacementState
    {
        public Guid? VeinGuid { get; private set; }
        public BlockId? BlockId { get; private set; }

        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();

        public void SetRestriction(Guid veinGuid, BlockId blockId)
        {
            VeinGuid = veinGuid;
            BlockId = blockId;
            _onChanged.OnNext(Unit.Default);
        }

        public void Clear()
        {
            VeinGuid = null;
            BlockId = null;
            _onChanged.OnNext(Unit.Default);
        }

        public bool IsRestrictedBlock(BlockId blockId)
        {
            return VeinGuid.HasValue && BlockId.HasValue && BlockId.Value == blockId;
        }
    }
}
```

- [ ] **Step 5: reporter を書く**

`VeinRestrictedPlacementReporter.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction
{
    /// <summary>
    ///     チュートリアルの鉱脈限定中、対象ブロックを対象鉱脈の外に置けなくする（クライアント側の設置制限。サーバーは弾かない）
    ///     While a tutorial restricts placement to a vein, blocks the target block outside that vein (client-side only; the server does not reject it)
    /// </summary>
    public static class VeinRestrictedPlacementReporter
    {
        public static void MarkOutsideTargetVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState state, PlacementFeedback feedback)
        {
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            if (!state.IsRestrictedBlock(holdingBlockId)) return;
            var targetVeinGuid = state.VeinGuid.Value;

            // 判定セルは採掘機ならドリル、他は原点。MinerVeinPlacementReporter と同じ導出
            // The judged cell is the drill for miners and the origin otherwise, derived the same way as MinerVeinPlacementReporter
            var lastDirection = (BlockDirection?)null;
            var judgeOffsetFromOrigin = Vector3Int.zero;

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                if (lastDirection != placeInfo.Direction)
                {
                    lastDirection = placeInfo.Direction;
                    judgeOffsetFromOrigin = ResolveJudgeOffset(placeInfo.Direction);
                }

                if (veinAabbRegistry.IsInsideVein(placeInfo.Position + judgeOffsetFromOrigin, targetVeinGuid)) continue;

                placeInfo.Placeable = false;
                if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein));
            }

            #region Internal

            Vector3Int ResolveJudgeOffset(BlockDirection direction)
            {
                if (holdingBlockMaster.BlockParam is not IMinerParam minerParam) return Vector3Int.zero;
                var originPositionInfo = new BlockPositionInfo(Vector3Int.zero, direction, holdingBlockMaster.BlockSize);
                return originPositionInfo.ConvertBlockLocalToWorldCell(minerParam.DrillLocalPosition);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 6: CommonBlockPlaceSystem に組み込む**

- ctor 引数末尾に `VeinRestrictedPlacementState veinRestrictedPlacementState` を追加し `private readonly VeinRestrictedPlacementState _veinRestrictedPlacementState;` に保持（`using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;`）。
- `GroundClickControl` の `MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(...)` の直後に追加:

```csharp
                // チュートリアルの鉱脈限定は採掘機制限の直後に重ねる。理由行は両方出ても1セル1行ずつ
                // The tutorial vein restriction stacks right after the miner restriction; each adds at most one reason line for the cursor cell
                VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(_currentPlaceInfos, holdingBlockMaster, cursorIndex, _veinAabbRegistry, _veinRestrictedPlacementState, feedback);
```
- `CommonBlockPlaceSystem` を `new` している箇所（`grep -rn "new CommonBlockPlaceSystem(" moorestech_client/Assets/Scripts`）に引数を足す。DI経由（`PlaceSystemSelector` 等）なら `MainGameInteractionRegistration` に `builder.Register<VeinRestrictedPlacementState>(Lifetime.Singleton);` を `builder.Register<MapVeinAabbRegistry>(Lifetime.Singleton);` の直後に追加するだけで解決する。

- [ ] **Step 7: PlaceBlockState から強調をプッシュする**

ctor 引数末尾に `VeinRestrictedPlacementState veinRestrictedPlacementState` を追加・保持し、ctor 内の `OnTargetChanged` 購読を次に置き換える:

```csharp
            // 設置対象か制限が変わった時だけ表示種別と強調鉱脈をプッシュする。毎フレームの再導出はしない
            // Push the vein kind and the highlighted vein only when the target or the restriction changes; never re-derive per frame
            _placeSystemStateController.OnTargetChanged.Subscribe(target => PushVeinView(target));
            _veinRestrictedPlacementState.OnChanged.Subscribe(_ => PushVeinView(_placeSystemStateController.CurrentTarget));

            #region Internal

            void PushVeinView(IPlacementTarget target)
            {
                _mapVeinRangeView.SetVisibleVeinKind(PlacementVeinViewKindResolver.Resolve(target));
                _mapVeinRangeView.SetHighlightedVein(ResolveHighlightedVein(target));
            }

            // 制限対象ブロックを持っている間だけ対象鉱脈を強調する
            // Highlight the target vein only while the restricted block is the placement target
            Guid? ResolveHighlightedVein(IPlacementTarget target)
            {
                if (target is not BlockPlacementTarget blockTarget) return null;
                return _veinRestrictedPlacementState.IsRestrictedBlock(blockTarget.BlockId) ? _veinRestrictedPlacementState.VeinGuid : null;
            }

            #endregion
```
`PlaceSystemStateController.CurrentTarget` の型と `BlockPlacementTarget.BlockId` の有無は `PlaceSystem/PlaceSystemStateController.cs` と `PlaceSystem/Targets/BlockPlacementTarget.cs` で確認し、`BlockGuid` しか無ければ `MasterHolder.BlockMaster.GetBlockId(blockTarget.BlockGuid)` を使う。`using Client.Game.InGame.BlockSystem.PlaceSystem.Targets; using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;`。`PlaceBlockState` を `new` するテスト（`grep -rn "new PlaceBlockState(" moorestech_client/Assets/Scripts/Client.Tests`）に `new VeinRestrictedPlacementState()` を足す。

- [ ] **Step 8: テストを実行して通ることを確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "VeinRestrictedPlacementReporterTest|MinerVeinPlacementReporterTest|PlaceBlockState|PlacementFeedback"
```
Expected: 全件PASS。

- [ ] **Step 9: コミットする**

```bash
cd $WT && git add Localization/localization.csv moorestech_client/Assets/Scripts && git commit -m "feat(place): チュートリアルの鉱脈限定設置を共有状態と reporter で実装し鉱脈強調へ接続する"
```

---

### Task 8: チュートリアル manager 2種（鉱脈限定・相対座標プレビュー）と TutorialManager 登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/VeinRestrictedPlacementTutorialManager.cs`
- Create: `.../Tutorial/PlacementGuide/RelativeBlockPlacePreviewTutorialManager.cs`
- Modify: `.../Tutorial/TutorialManager.cs:20-35`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:91-96,166-172`
- Modify: `Client.Tests/UnitTest/Tutorial/VeinPinTutorialTest.cs:70-79`
- Test: `Client.Tests/UnitTest/Tutorial/PlacementGuideTutorialDispatchTest.cs`
- Scene（uloop）: `MainGame` シーンの Tutorial 系オブジェクト配下に2つの GameObject を追加し `MainGameStarter` の新フィールドへ結線

**Interfaces:**
- Consumes: `VeinRestrictedPlacementState`（Task 7）、`TutorialsElement.TutorialTypeConst.veinRestrictedPlacement / relativeBlockPlacePreview`（Task 1）
- Produces: `TutorialManager(IReadOnlyList<ITutorialWorldPin>, UIHighlightTutorialManager, KeyControlTutorialManager, ItemViewHighLightTutorialManager, BlockPlacePreviewTutorialManager, UiDragGuideTutorialManager, VeinRestrictedPlacementTutorialManager, RelativeBlockPlacePreviewTutorialManager)`

- [ ] **Step 1: 失敗するテストを書く**

`PlacementGuideTutorialDispatchTest.cs`（`VeinPinTutorialTest` の SetUp/TearDown/`SetChallengeMaster` と「challenges.json を読み `tutorials[0]` を差し替えて ChallengeMaster を作る」手順を再掲。差し替え内容だけ変える）:

```csharp
        [Test]
        public void veinRestrictedPlacementは専用managerへdispatchされ状態へ書く()
        {
            SetTutorial(TutorialsElement.TutorialTypeConst.veinRestrictedPlacement, new JObject
            {
                ["veinGuid"] = "11111111-0000-0000-0000-000000000001",
                ["blockGuid"] = "00000000-0000-0000-0000-000000000006",
            });
            var state = new VeinRestrictedPlacementState();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(state);
            var manager = CreateTutorialManager(veinRestricted, _root.AddComponent<RelativeBlockPlacePreviewTutorialManager>());

            manager.ApplyTutorial(ChallengeGuid);
            Assert.IsTrue(state.IsRestrictedBlock(ForUnitTestModBlockId.ElectricMinerId));

            manager.CompleteChallenge(ChallengeGuid);
            Assert.IsNull(state.VeinGuid);
        }

        [Test]
        public void relativeBlockPlacePreviewは専用managerへdispatchされる()
        {
            SetTutorial(TutorialsElement.TutorialTypeConst.relativeBlockPlacePreview, new JObject
            {
                ["anchorBlockGuid"] = "00000000-0000-0000-0000-000000000010",
                ["blockGuid"] = "00000000-0000-0000-0000-00000000000e",
                ["offset"] = new JArray(0, 0, 1),
                ["blockDirection"] = "North",
                ["message"] = "テスト",
            });
            var relative = _root.AddComponent<RelativeBlockPlacePreviewTutorialManager>();
            var manager = CreateTutorialManager(_root.AddComponent<VeinRestrictedPlacementTutorialManager>(), relative);

            manager.ApplyTutorial(ChallengeGuid);

            Assert.IsTrue(relative.IsApplied);
        }

        private TutorialManager CreateTutorialManager(VeinRestrictedPlacementTutorialManager veinRestricted, RelativeBlockPlacePreviewTutorialManager relative)
        {
            return new TutorialManager(
                new List<ITutorialWorldPin>(),
                _root.AddComponent<UIHighlightTutorialManager>(),
                _root.AddComponent<KeyControlTutorialManager>(),
                _root.AddComponent<ItemViewHighLightTutorialManager>(),
                _root.AddComponent<BlockPlacePreviewTutorialManager>(),
                _root.AddComponent<UiDragGuideTutorialManager>(),
                veinRestricted,
                relative);
        }
```
`SetTutorial(type, param)` は `VeinPinTutorialTest.SetUp` の JSON 差し替え（`data[0].challenges[0].tutorials[0]` の `tutorialType`/`tutorialParam` を上書き→`new ChallengeMaster(json)`→`Initialize()`→`SetChallengeMaster`）をメソッド化したもの。`RelativeBlockPlacePreviewTutorialManager` は `Construct(BlockGameObjectDataStore)` を持つため、テストでは `ClientDIContext` 不在で `Update` が走らないよう `IsApplied` の判定だけにする（`Update` は `_currentParam == null || _blockGameObjectDataStore == null` で早期return）。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client
```
Expected: 未定義型でコンパイルエラー。

- [ ] **Step 3: 鉱脈限定 manager を書く**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     チャレンジ中だけ「このブロックはこの鉱脈にしか置けない」制限を共有状態へ書く。表示と判定は設置側が読む
    ///     Writes the "this block only on this vein" restriction into the shared state for the challenge's lifetime; placement reads it
    /// </summary>
    public class VeinRestrictedPlacementTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        private VeinRestrictedPlacementState _state;

        [Inject]
        public void Construct(VeinRestrictedPlacementState state)
        {
            _state = state;
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (VeinRestrictedPlacementTutorialParam)tutorial.TutorialParam;
            var blockId = MasterHolder.BlockMaster.GetBlockId(param.BlockGuid);
            _state.SetRestriction(param.VeinGuid, blockId);
            return this;
        }

        public void CompleteTutorial()
        {
            _state.Clear();
        }
    }
}
```

- [ ] **Step 4: 相対座標プレビュー manager を書く**

```csharp
using System;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Player;
using Client.Game.InGame.Tutorial.TutorialBlock;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     最寄りのアンカーブロック原点＋offset にゴーストを出し、そこへ対象ブロックが置かれたら完了する
    ///     Shows a ghost at nearest-anchor origin + offset and completes when the target block lands there
    /// </summary>
    public class RelativeBlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        private const string WebPinId = "relative-block-place-preview-pin";

        public bool IsApplied => _currentParam != null;

        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private TutorialBlockPreviewObject _previewObject;
        private RelativeBlockPlacePreviewTutorialParam _currentParam;
        private BlockId _anchorBlockId;
        private BlockId _targetBlockId;
        private BlockDirection _direction;
        private Vector3Int? _targetCell;
        private IDisposable _blockPlacedDisposable;
        private string _pinTutorialGuid = "";

        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            _currentParam = (RelativeBlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            _pinTutorialGuid = tutorial.TutorialGuid.ToString("D");
            _anchorBlockId = MasterHolder.BlockMaster.GetBlockId(_currentParam.AnchorBlockGuid);
            _targetBlockId = MasterHolder.BlockMaster.GetBlockId(_currentParam.BlockGuid);
            _direction = Enum.Parse<BlockDirection>(_currentParam.BlockDirection);
            _targetCell = null;

            if (_blockGameObjectDataStore != null) SubscribePlacementEvent();
            return this;
        }

        private void Update()
        {
            if (_currentParam == null || _blockGameObjectDataStore == null) return;

            // アンカーは撤去や増設で変わるため毎フレーム最寄りを取り直す（VeinPin と同じ追従）
            // The anchor can be removed or duplicated, so re-pick the nearest one every frame (same tracking as VeinPin)
            var anchor = FindNearestAnchor();
            if (anchor == null) { HidePreview(); return; }

            var cell = anchor.BlockPosInfo.OriginalPos + _currentParam.Offset;
            if (_targetCell != cell)
            {
                _targetCell = cell;
                ShowPreviewAsync(cell).Forget();
            }
            PublishWebPin();

            #region Internal

            BlockGameObject FindNearestAnchor()
            {
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                BlockGameObject nearest = null;
                var nearestSqr = float.MaxValue;
                foreach (var block in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
                {
                    if (block.BlockId != _anchorBlockId) continue;
                    var sqr = (block.transform.position - playerPosition).sqrMagnitude;
                    if (sqr >= nearestSqr) continue;
                    nearestSqr = sqr;
                    nearest = block;
                }
                return nearest;
            }

            void PublishWebPin()
            {
                if (!WebUiScreenGate.IsWebUiMode || _previewObject == null) return;
                var camera = CameraManager.MainCamera.Camera;
                if (!camera) return;
                var projection = WorldPinScreenProjection.Project(camera, _previewObject.transform.position);
                WorldPinStateStore.Instance.SetPin(WebPinId, _pinTutorialGuid, projection);
            }

            #endregion
        }

        private async UniTaskVoid ShowPreviewAsync(Vector3Int cell)
        {
            if (_previewObject == null || _previewObject.BlockMasterElement.BlockGuid != _currentParam.BlockGuid)
            {
                if (_previewObject != null) _previewObject.DestroyPreview();
                _previewObject = await TutorialPreviewBlockCreator.CreateAsync(_targetBlockId);
                _previewObject.transform.SetParent(transform);
            }

            var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(cell, _direction, _targetBlockId);
            _previewObject.SetTransform(position, _direction.GetRotation());
            _previewObject.SetPlaceableColor(true);
            _previewObject.SetActive(true);
        }

        private void SubscribePlacementEvent()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = _blockGameObjectDataStore.OnBlockPlaced.Subscribe(block =>
            {
                if (block.BlockId != _targetBlockId) return;
                if (_targetCell == null || block.BlockPosInfo.OriginalPos != _targetCell.Value) return;
                CompleteTutorial();
            });
        }

        private void HidePreview()
        {
            _targetCell = null;
            if (_previewObject != null) _previewObject.SetActive(false);
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }

        public void CompleteTutorial()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;
            HidePreview();
            _currentParam = null;
        }

        private void OnDestroy()
        {
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }
    }
}
```
`_currentParam.Offset` の生成型が `Vector3Int` でなければ（`Vector3IntElement` 等）`new Vector3Int(x,y,z)` へ変換する。`using` は `BlockPlacePreviewTutorialManager.cs` と同じ集合に揃える（`CameraManager`/`WorldPinScreenProjection`/`WorldPinStateStore` の namespace はそちらを正とする）。

- [ ] **Step 5: TutorialManager・MainGameStarter・既存テストを更新する**

`TutorialManager` ctor 引数末尾に `VeinRestrictedPlacementTutorialManager veinRestrictedPlacementTutorialManager, RelativeBlockPlacePreviewTutorialManager relativeBlockPlacePreviewTutorialManager` を追加し、登録:

```csharp
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.veinRestrictedPlacement, veinRestrictedPlacementTutorialManager);
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.relativeBlockPlacePreview, relativeBlockPlacePreviewTutorialManager);
```
（`using Client.Game.InGame.Tutorial.PlacementGuide;`）

`MainGameStarter`: `[SerializeField] private UiDragGuideTutorialManager uiDragGuideTutorialManager;` の直後に
```csharp
        [SerializeField] private VeinRestrictedPlacementTutorialManager veinRestrictedPlacementTutorialManager;
        [SerializeField] private RelativeBlockPlacePreviewTutorialManager relativeBlockPlacePreviewTutorialManager;
```
`builder.RegisterComponent(uiDragGuideTutorialManager);` の直後に
```csharp
            builder.RegisterComponent(veinRestrictedPlacementTutorialManager);
            builder.RegisterComponent(relativeBlockPlacePreviewTutorialManager);
```

`VeinPinTutorialTest.cs:70-79` の `new TutorialManager(...)` に `_root.AddComponent<VeinRestrictedPlacementTutorialManager>(), _root.AddComponent<RelativeBlockPlacePreviewTutorialManager>()` を末尾追加（他に `new TutorialManager(` があれば同様: `grep -rn "new TutorialManager(" moorestech_client/Assets/Scripts`）。

- [ ] **Step 6: シーンに GameObject を追加して結線する（uloop）**

```bash
cd $WT && uloop execute-dynamic-code --project-path ./moorestech_client --code '
using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement;
using Client.Starter; using Client.Game.InGame.Tutorial; using Client.Game.InGame.Tutorial.PlacementGuide;
var starter = Object.FindFirstObjectByType<MainGameStarter>(FindObjectsInactive.Include);
if (starter == null) return "MainGameStarter not found: open the MainGame scene first";
var existingPin = Object.FindFirstObjectByType<VeinPin>(FindObjectsInactive.Include);
var parent = existingPin.transform.parent;
var so = new SerializedObject(starter);
GameObject Make(string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
var a = Make("VeinRestrictedPlacementTutorialManager").AddComponent<VeinRestrictedPlacementTutorialManager>();
var b = Make("RelativeBlockPlacePreviewTutorialManager").AddComponent<RelativeBlockPlacePreviewTutorialManager>();
so.FindProperty("veinRestrictedPlacementTutorialManager").objectReferenceValue = a;
so.FindProperty("relativeBlockPlacePreviewTutorialManager").objectReferenceValue = b;
so.ApplyModifiedPropertiesWithoutUndo();
EditorSceneManager.MarkSceneDirty(starter.gameObject.scene);
EditorSceneManager.SaveScene(starter.gameObject.scene);
return "wired: " + starter.gameObject.scene.path;
'
```
Expected: `wired: Assets/.../MainGame.unity`。MainGame シーンが開いていなければ先に `uloop execute-dynamic-code` で `EditorSceneManager.OpenScene(<MainGameのパス>)` を実行する（パスは `grep -rl "MainGameStarter" moorestech_client/Assets --include="*.unity"`）。`git status` でシーンファイルだけが変更されていることを確認。

- [ ] **Step 7: テストを実行して通ることを確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGuideTutorialDispatchTest|VeinPinTutorialTest|TutorialManagerTest|UIHighlightTutorialManagerTest|KeyControlTutorialManagerTest|UiDragGuideTutorialManagerTest|ItemViewHighLightTutorialManagerTest"
```
Expected: 全件PASS。

- [ ] **Step 8: コミットする**

```bash
cd $WT && git add moorestech_client/Assets/Scripts moorestech_client/Assets/Scenes && git commit -m "feat(tutorial): 鉱脈限定設置と相対座標ゴーストのチュートリアルmanagerを追加しシーンへ結線する"
```
（シーンのパスが `Assets/Scenes` でなければ `git status` の実パスを `add` する。）

---

### Task 9: 歯車接続ペアの解決と設置プレビューでの常設表示

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/GearConnect/GearConnectPairResolver.cs`
- Create: `.../GearConnect/GearConnectPreview.cs`
- Create: `.../GearConnect/GearConnectPreviewRenderer.cs`
- Modify: `.../PlaceSystem/Common/CommonBlockPlaceSystem.cs`（フィールド・ctor・`GroundClickControl`・`Disable`・早期return）
- Modify: `.../PlaceSystem/Common/PreviewObject/GearConnectorView.cs:28`
- Test: `Client.Tests/PlaceSystem/GearConnect/GearConnectPairResolverTest.cs`

**Interfaces:**
- Produces: `readonly struct GearConnectPair(Vector3Int SelfConnectorCell, Vector3Int TargetConnectorCell)`、`GearConnectPairResolver.Resolve(BlockId selfBlockId, BlockPositionInfo selfPositionInfo, IReadOnlyList<(BlockId blockId, BlockPositionInfo positionInfo)> neighbours) : List<GearConnectPair>`、`GearConnectPreview.Apply(List<PlaceInfo>, BlockId, int cursorIndex)` / `Hide()`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearConnect
{
    public class GearConnectPairResolverTest
    {
        [Test]
        public void 発電機の隣のシャフトは1組の接続を返す()
        {
            CreateServer();
            // GearNetworkTest で実際に回る配置: 発電機(0,0,0)・シャフト(0,0,1)
            // The layout GearNetworkTest proves rotates: generator at (0,0,0), shaft at (0,0,1)
            var shaft = new BlockPositionInfo(new Vector3Int(0, 0, 1), BlockDirection.North, BlockSize(ForUnitTestModBlockId.Shaft));
            var generator = (ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new BlockPositionInfo(new Vector3Int(0, 0, 0), BlockDirection.North, BlockSize(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator)));

            var pairs = GearConnectPairResolver.Resolve(ForUnitTestModBlockId.Shaft, shaft, new List<(BlockId, BlockPositionInfo)> { generator });

            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 1), pairs[0].SelfConnectorCell);
            Assert.AreEqual(new Vector3Int(0, 0, 0), pairs[0].TargetConnectorCell);
        }

        [Test]
        public void 離れたブロックとは接続しない()
        {
            CreateServer();
            var shaft = new BlockPositionInfo(new Vector3Int(5, 0, 5), BlockDirection.North, BlockSize(ForUnitTestModBlockId.Shaft));
            var generator = (ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new BlockPositionInfo(new Vector3Int(0, 0, 0), BlockDirection.North, BlockSize(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator)));

            var pairs = GearConnectPairResolver.Resolve(ForUnitTestModBlockId.Shaft, shaft, new List<(BlockId, BlockPositionInfo)> { generator });

            Assert.AreEqual(0, pairs.Count);
        }

        [Test]
        public void 歯車を持たないブロックは空を返す()
        {
            CreateServer();
            var chest = new BlockPositionInfo(new Vector3Int(0, 0, 1), BlockDirection.North, BlockSize(ForUnitTestModBlockId.ChestId));
            var pairs = GearConnectPairResolver.Resolve(ForUnitTestModBlockId.ChestId, chest, new List<(BlockId, BlockPositionInfo)>());
            Assert.AreEqual(0, pairs.Count);
        }

        private static Vector3Int BlockSize(BlockId blockId) => MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client
```
Expected: 未定義型でコンパイルエラー。

- [ ] **Step 3: resolver を書く（サーバーの3段判定を写す）**

```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect
{
    public readonly struct GearConnectPair
    {
        public readonly Vector3Int SelfConnectorCell;
        public readonly Vector3Int TargetConnectorCell;

        public GearConnectPair(Vector3Int selfConnectorCell, Vector3Int targetConnectorCell)
        {
            SelfConnectorCell = selfConnectorCell;
            TargetConnectorCell = targetConnectorCell;
        }
    }

    /// <summary>
    ///     設置予定の歯車ブロックが隣接ブロックのどのコネクタと繋がるかを、サーバーと同じ「位置一致→形状表→GearConnectJudge」で解く
    ///     Resolves which neighbour connectors a gear block about to be placed will mesh with, using the server's position/shape/judge rule
    /// </summary>
    public static class GearConnectPairResolver
    {
        private static readonly GearConnectJudge Judge = new();

        public static List<GearConnectPair> Resolve(BlockId selfBlockId, BlockPositionInfo selfPositionInfo, IReadOnlyList<(BlockId blockId, BlockPositionInfo positionInfo)> neighbours)
        {
            var pairs = new List<GearConnectPair>();
            if (MasterHolder.BlockMaster.GetBlockMaster(selfBlockId).BlockParam is not IGearConnectors selfGear) return pairs;

            // 自コネクタが向く先セル → (自コネクタセル, コネクタ) の一覧
            // Target cell each own connector faces → (own connector cell, connector)
            var selfTargets = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(selfGear.Gear.GearConnects, selfPositionInfo);

            foreach (var (neighbourId, neighbourPositionInfo) in neighbours)
            {
                if (MasterHolder.BlockMaster.GetBlockMaster(neighbourId).BlockParam is not IGearConnectors neighbourGear) continue;
                var neighbourInputs = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(neighbourGear.Gear.GearConnects, neighbourPositionInfo);

                foreach (var (targetCell, candidates) in selfTargets)
                {
                    if (!neighbourInputs.TryGetValue(targetCell, out var accepted)) continue;
                    foreach (var (selfCell, selfConnector) in candidates)
                    {
                        // 方向無制限（connector null）は位置一致だけで通す。制限付きは受け入れ元セルが自コネクタセルと一致すること
                        // Unrestricted (null connector) passes on position alone; restricted ones must accept from the own connector cell
                        if (accepted.connector != null && accepted.position != selfCell) continue;
                        if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(selfConnector?.ShapeGuid, accepted.connector?.ShapeGuid)) continue;
                        if (!Judge.CanConnect(new ConnectJudgeContext(selfConnector, accepted.connector, selfPositionInfo, neighbourPositionInfo))) continue;

                        pairs.Add(new GearConnectPair(selfCell, targetCell));
                        break;
                    }
                }
            }
            return pairs;
        }
    }
}
```
`CalculateConnectPosToConnector` の戻り値 `(Vector3Int position, IBlockConnector connector)` の `position` が「受け入れ元セル」か「自コネクタセル」かは `moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorConnectPositionCalculator.cs:50-` の実装で確認し、`accepted.position != selfCell` の比較対象をそれに合わせる（Step 1 のテスト1件目が通ることが基準）。`Client.Game.asmdef` に `Game.Block` / `Game.Gear` の参照があることを確認（無ければ追加）。

- [ ] **Step 4: renderer と preview を書く**

`GearConnectPreviewRenderer.cs`（`AutoConnectWirePreviewRenderer` と同型・直線）:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect
{
    /// <summary>
    ///     設置予定歯車の各コネクタから接続先コネクタセルへ、半透明の直線を描く
    ///     Draws translucent straight lines from each connector of the gear to be placed to the connector cell it meshes with
    /// </summary>
    public class GearConnectPreviewRenderer
    {
        private const string RootName = "GearConnectPreview";
        private const float LineWidth = 0.08f;
        private static readonly Color LineColor = new(0.3f, 0.95f, 0.35f, 0.8f);
        private static readonly Vector3 CellCenter = new(0.5f, 0.5f, 0.5f);

        private readonly Transform _root;
        private readonly List<LineRenderer> _lines = new();

        public GearConnectPreviewRenderer()
        {
            _root = new GameObject(RootName).transform;
            _root.gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<GearConnectPair> pairs)
        {
            _root.gameObject.SetActive(true);
            while (_lines.Count < pairs.Count) _lines.Add(CreateLine());
            for (var i = 0; i < _lines.Count; i++)
            {
                var visible = i < pairs.Count;
                _lines[i].gameObject.SetActive(visible);
                if (!visible) continue;
                _lines[i].SetPosition(0, pairs[i].SelfConnectorCell + CellCenter);
                _lines[i].SetPosition(1, pairs[i].TargetConnectorCell + CellCenter);
            }
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        private LineRenderer CreateLine()
        {
            var line = new GameObject("GearConnectLine").AddComponent<LineRenderer>();
            line.transform.SetParent(_root, false);
            line.positionCount = 2;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Sprites/Default")) { color = LineColor };
            line.startColor = LineColor;
            line.endColor = LineColor;
            return line;
        }
    }
}
```

`GearConnectPreview.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect
{
    /// <summary>
    ///     通常設置プレビュー中、歯車系ブロックのカーソルセルについて接続先を解決し線で示す（常設・チュートリアル非依存）
    ///     During normal placement preview, resolves and draws the gear connections of the cursor cell (always on, not tutorial-bound)
    /// </summary>
    public class GearConnectPreview
    {
        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly GearConnectPreviewRenderer _renderer = new();

        public GearConnectPreview(BlockGameObjectDataStore blockDataStore)
        {
            _blockDataStore = blockDataStore;
        }

        public void Apply(List<PlaceInfo> placeInfos, BlockId blockId, int cursorIndex)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is not Mooresmaster.Model.BlocksModule.IGearConnectors || placeInfos.Count == 0) { Hide(); return; }

            var cursor = placeInfos[cursorIndex];
            var selfPositionInfo = new BlockPositionInfo(cursor.Position, cursor.Direction, blockMaster.BlockSize);
            var pairs = GearConnectPairResolver.Resolve(blockId, selfPositionInfo, CollectNeighbours(selfPositionInfo));
            _renderer.Show(pairs);

            #region Internal

            // 占有範囲を1セル膨らませた殻の中にあるブロックを候補にする。歯車は隣接セルとしか繋がらない
            // Candidates are blocks touching the one-cell shell around the footprint; gears only mesh with adjacent cells
            List<(BlockId, BlockPositionInfo)> CollectNeighbours(BlockPositionInfo positionInfo)
            {
                var found = new HashSet<BlockGameObject>();
                var min = positionInfo.MinPos - Vector3Int.one;
                var max = positionInfo.MaxPos + Vector3Int.one;
                for (var x = min.x; x <= max.x; x++)
                for (var y = min.y; y <= max.y; y++)
                for (var z = min.z; z <= max.z; z++)
                {
                    if (!_blockDataStore.TryGetBlockGameObject(new Vector3Int(x, y, z), out var block)) continue;
                    found.Add(block);
                }
                var neighbours = new List<(BlockId, BlockPositionInfo)>(found.Count);
                foreach (var block in found) neighbours.Add((block.BlockId, block.BlockPosInfo));
                return neighbours;
            }

            #endregion
        }

        public void Hide()
        {
            _renderer.Hide();
        }
    }
}
```
`BlockGameObjectDataStore.TryGetBlockGameObject(Vector3Int, out BlockGameObject)` が占有セル全体で引けるか（原点のみか）は `Client.Game/InGame/Block/BlockGameObjectDataStore.cs:42` で確認し、原点のみなら `BlockGameObjectDictionary.Values` を走査して `BlockPosInfo` の Min/Max が殻と交差するものを集める形に置き換える。

- [ ] **Step 5: CommonBlockPlaceSystem に組み込み、GearConnectorView を直す**

- フィールド `private readonly GearConnectPreview _gearConnectPreview;`、ctor で `_gearConnectPreview = new GearConnectPreview(blockGameObjectDataStore);`（`using Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect;`）。
- `GroundClickControl` の2つの早期return（ray不一致・距離外）で `_autoConnectPreview.Hide();` の隣に `_gearConnectPreview.Hide();` を足す。`Disable()` の `_autoConnectPreview.Hide();` の後にも足す。
- `var wirePlaceable = _autoConnectPreview.ApplyAutoConnect(...)` の直後に:

```csharp
                // 歯車はどの座標同士が噛み合うかを線で示す。設置可否には関与しない
                // Gears show which cells mesh with which via lines; this never affects placeability
                _gearConnectPreview.Apply(_currentPlaceInfos, target.BlockId, cursorIndex);
```
- `GearConnectorView.cs:28` の `if (gearConnect.Directions == null) return;` を `if (gearConnect.Directions == null) continue;` に変える。

- [ ] **Step 6: テストを実行して通ることを確認する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GearConnectPairResolverTest|CommonBlockPlacePointCalculatorTest|ElectricWireAutoConnect"
```
Expected: 全件PASS。

- [ ] **Step 7: コミットする**

```bash
cd $WT && git add moorestech_client/Assets/Scripts && git commit -m "feat(place): 歯車系ブロックの設置プレビューで接続先コネクタを常設表示する"
```

---

### Task 10: 歯車 prefab 4種へ GearConnectorView を付ける（uloop）

**Files:**
- Prefab: `moorestech_client/Assets/AddressableResources/Block/Shaft.prefab`、`SmallGear.prefab`、`Ore_Crusher.prefab`、`Fuel_powered_windmill.prefab`（子に `Block/Util/GearConnectorView.prefab`）

- [ ] **Step 1: 現状を確認する（失敗するチェック）**

```bash
cd $WT && for p in Shaft SmallGear Ore_Crusher Fuel_powered_windmill; do echo "$p: $(grep -c 'GearConnectorView' moorestech_client/Assets/AddressableResources/Block/$p.prefab)"; done
```
Expected: 全て `0`（`grep -c` の非一致 exit 1 は無視）。prefab が別名（例 `Wooden_Shaft`）なら `ls moorestech_client/Assets/AddressableResources/Block | grep -i shaft` で実名を使う。

- [ ] **Step 2: uloop で子prefabを追加する**

```bash
cd $WT && uloop execute-dynamic-code --project-path ./moorestech_client --code '
using UnityEngine; using UnityEditor;
var child = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddressableResources/Block/Util/GearConnectorView.prefab");
var log = "";
foreach (var name in new[]{"Shaft","SmallGear","Ore_Crusher","Fuel_powered_windmill"}) {
  var path = $"Assets/AddressableResources/Block/{name}.prefab";
  var root = PrefabUtility.LoadPrefabContents(path);
  if (root.GetComponentInChildren<Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject.GearConnectorView>(true) == null) {
    var inst = (GameObject)PrefabUtility.InstantiatePrefab(child);
    inst.transform.SetParent(root.transform, false);
    PrefabUtility.SaveAsPrefabAsset(root, path);
    log += name + ":added ";
  } else log += name + ":exists ";
  PrefabUtility.UnloadPrefabContents(root);
}
AssetDatabase.SaveAssets();
return log;
'
```
Expected: `Shaft:added SmallGear:added Ore_Crusher:added Fuel_powered_windmill:added`。

- [ ] **Step 3: 確認する**

```bash
cd $WT && for p in Shaft SmallGear Ore_Crusher Fuel_powered_windmill; do echo "$p: $(grep -c 'GearConnectorView' moorestech_client/Assets/AddressableResources/Block/$p.prefab)"; done; git status --short moorestech_client/Assets/AddressableResources/Block | head
```
Expected: 各1以上。変更は4 prefab のみ（`.meta` の新規生成が無いこと）。

- [ ] **Step 4: コミットする**

```bash
cd $WT && git add moorestech_client/Assets/AddressableResources/Block && git commit -m "asset(block): 歯車系4prefabにGearConnectorViewを付けて設置モードでコネクタ位置を見せる"
```

---

### Task 11: EditModeInPlayingTest による相対座標ゴーストの実機確認

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/RelativeBlockPlacePreviewTest.cs`

- [ ] **Step 1: テストを書く（`MapVeinOutcropAndRangeViewTest` の骨格を写す）**

```csharp
using System.Collections;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Tests.EditModeInPlayingTest.Util;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    public class RelativeBlockPlacePreviewTest
    {
        [UnityTest]
        public IEnumerator アンカー設置後にゴーストがアンカー原点プラスoffsetへ出る()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        private static async UniTask Body()
        {
            await LoadMainGame();
            var manager = Object.FindFirstObjectByType<RelativeBlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(manager, "the scene has no RelativeBlockPlacePreviewTutorialManager (Task 8 Step 6 not applied)");

            // EditModeInPlayingTestMod に存在する歯車ブロック名を使う（blocks.json の name）
            // Use gear block names that exist in EditModeInPlayingTestMod (blocks.json name)
            var anchorPos = new Vector3Int(10, 0, 10);
            PlaceBlock("燃料式風車", anchorPos, BlockDirection.North);
            await WaitBlockGameObjectSpawn(anchorPos);

            manager.ApplyTutorial(CreateTutorial("燃料式風車", "木のシャフト", new Vector3Int(-1, 0, 2), "East"));
            await UniTask.Delay(500);

            var ghost = manager.GetComponentInChildren<Client.Game.InGame.Tutorial.TutorialBlock.TutorialBlockPreviewObject>(false);
            Assert.IsNotNull(ghost, "no ghost shown");
            Assert.AreEqual(anchorPos + new Vector3Int(-1, 0, 2), Vector3Int.FloorToInt(ghost.transform.position));
        }
    }
}
```
`CreateTutorial(anchorName, blockName, offset, direction)` は `TutorialsElement` を手で組む代わりに、`MasterHolder.ChallengeMaster` を `VeinPinTutorialTest` と同じJSON差し替えで作り、その `Tutorials[0]` を返すヘルパとして同ファイルに書く（ブロックGUIDは `MasterHolder.BlockMaster` から name で引く）。`燃料式風車`/`木のシャフト` が EditModeInPlayingTestMod に無ければ、その mod の `blocks.json` にある `FuelGearGenerator` と `Shaft` の name を使う。ゴースト座標の比較は `SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition` の原点補正（ブロック中心寄せ）に合わせて `Vector3Int.FloorToInt` で丸める。

- [ ] **Step 2: 実行する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "RelativeBlockPlacePreviewTest"
```
Expected: PASS（PlayMode遷移のためドメインリロードエラーが出たら45秒待って再試行）。

- [ ] **Step 3: コミットする**

```bash
cd $WT && git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/RelativeBlockPlacePreviewTest.cs && git commit -m "test: 相対座標ゴーストがアンカー原点+offsetに出ることをPlayMode遷移テストで確認する"
```

---

### Task 12: マスタ側 push・PR とピン更新、全体テスト

**Files:**
- Modify: `.moorestech-external-revisions.json`（`moorestech_master.commitHash`）

- [ ] **Step 1: マスタを push して PR を作る**

```bash
cd $MW && git push -u origin feature/initial-equipment-items-field
gh pr create --repo moorestech/moorestech_master --base master --head feature/initial-equipment-items-field --title "data: items.json に initialEquipmentItems を追加し v8 は石の斧を初期装備にする" --body "moorestech ADR 0038（決定1「石の斧を装備済みで開始」）に伴うスキーマ追随。本体PR: <本体PRのURLを後で追記>

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01Ts2pLxAukyhiJyiiqk4bXs"
git rev-parse HEAD
```
Expected: PR URL と push済みコミットハッシュ。

- [ ] **Step 2: ピンを更新する**

```bash
cd $WT && python3 - <<'EOF'
import json,subprocess,os
MW=os.path.expanduser('~/hermes-agent/data/repos/moorestech-master-worktrees/placement-guided-tutorials')
sha=subprocess.check_output(['git','-C',MW,'rev-parse','HEAD']).decode().strip()
p='.moorestech-external-revisions.json'; d=json.load(open(p))
for r in d['repositories']:
    if r['key']=='moorestech_master': r['commitHash']=sha
json.dump(d,open(p,'w'),indent=2); open(p,'a').write('\n'); print(sha)
EOF
git diff .moorestech-external-revisions.json
```
Expected: `commitHash` だけが変わる差分。Editor が同ファイルを書き戻していたら `git checkout -- .moorestech-external-revisions.json` の後にやり直す。

- [ ] **Step 3: 全体EditModeテストを回す**

```bash
cd $WT && uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Challenge|Equipment|Inventory|Vein|Gear|Tutorial|PlaceSystem|Localization|MasterSource"
```
Expected: 全件PASS。`.uloop/outputs/TestResults` の最新XMLで `failed="0"` を確認。

- [ ] **Step 4: コミットして本体 PR を作る**

```bash
cd $WT && git add .moorestech-external-revisions.json && git commit -m "chore: moorestech_master ピンを initialEquipmentItems 追随コミットへ更新する"
git push -u origin feature/placement-guided-tutorials
gh pr create --base master --title "feat: 鉱脈限定設置・相対座標ゴースト・歯車接続の常設表示と初期装備 (ADR 0038)" --body "$(cat <<'EOF'
## Summary
- チャレンジ完了判定 `blockPlaceOnVein` / `gearConnectedBlock` を追加
- チュートリアル `veinRestrictedPlacement` / `relativeBlockPlacePreview` を追加（設置システム側で対象鉱脈だけ強調・設置限定、アンカー相対ゴースト）
- 歯車系ブロックの設置プレビューで接続先コネクタを常設表示、歯車4prefabに GearConnectorView
- `items.initialEquipmentItems` で新規プレイヤーに石の斧を装備済み投入
- 姉妹PR（moorestech_master）: <URL>。マスタの研究・チャレンジ再構成は次のPR（plan 2026-08-28-early-game-compression-master-data）

ADR: docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01Ts2pLxAukyhiJyiiqk4bXs
EOF
)"
```
Expected: 本体 PR URL。マスタPR本文の `<本体PRのURLを後で追記>` を `gh pr edit` で置き換える。

---

### Task 13: 最終レビュー（省略不可）

- [ ] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

`moores-code-review` スキルを `feature/placement-guided-tutorials` の master との差分全体に対して実行し、指摘を修正してコミット・pushする。記録は `../moorestech_logs/harness/` へ（本repoにコミットしない）。

- [ ] **Step 2: 撤収**

```bash
moores-wt rm placement-guided-tutorials
git -C ~/hermes-agent/data/repos/moorestech_master worktree remove ~/hermes-agent/data/repos/moorestech-master-worktrees/placement-guided-tutorials
```

---

## 判断記録（ADR）

設計ADR: `docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md`。裁定: `.decisions/2026-08-27-序盤圧縮は…`, `…石器ラインは削除し…`, `…木の鉱脈チュートリアルは…`, `…歯車接続は常設で明示し…`。

planning中の判断:
- **plan分割**: コード（本plan）→マスタ（姉妹plan）の順に実行する。マスタの新チャレンジは本planの新tutorialType/taskType に依存するため。出所: agent前提（writing-plans Scope Check）。
- **初期装備の担い手**: `items.yml` ルート `initialEquipmentItems`（必須配列）を `PlayerInventoryDataStore` の新規生成分岐で `RestoreFromSave` 経由で投入。`giveItem` gameAction は `WorldInitialize` 時点でプレイヤーが0人のため使えず、装備スロットにも書けない。出所: agent前提（`WorldLoaderFromJson.WorldInitialize` と `GameActionExecutor.GiveItem` の調査）。
- **鉱脈上判定のセル**: 採掘機はドリルセル、それ以外は占有セルのいずれか。サーバー（`VanillaMinerProcessorComponent`）とクライアント（`MinerVeinPlacementReporter`）の採掘機基準に揃える。出所: agent前提。
- **完了カスケードはティック境界**: `EquipItemChallengeTask` の規則（ユーザー裁定 2026-08-23）に従い、新2タスクはイベントで候補を積みティックで判定する。
- **鉱脈限定はクライアント限定**: サーバーは弾かない（採掘機の鉱脈限定 `.decisions/2026-08-25-採掘機の設置可否はドリル位置が鉱脈に重なるかで決める.md` と同じ線引き）。出所: agent前提（前例一致）。
- **強調モードの表示規則**: 強調中は対象鉱脈のみ描画（他鉱脈は種別を問わず非表示）、色は緑系、距離カリングは維持。出所: ユーザー裁定 2026-08-28「その時設置してほしい鉱脈だけハイライト」＋ agent前提（色・カリング）。
- **相対座標の解釈**: offset はアンカーブロック原点からのワールドセル差分（アンカーの向きで回転しない）。姉妹planでデータを組む際はこの前提で座標を決め、PlayMode で接続を検証する。出所: agent前提。
- **歯車接続表示の範囲**: カーソルセルのみ・接続成立ペアのみ線描画（電線プレビューと同じ「カーソルセルだけ」方針）。未接続コネクタの位置は prefab 側 `GearConnectorView` が担う。出所: agent前提。
- **TutorialManager の登録方式**: 新manager は `ITutorialWorldPin` を名乗らず ctor 引数を増やす（役割不一致の前例引用を避ける。spec-architecture-review 検査2）。出所: agent前提。
- **フィクスチャの初期チャレンジ件数**: ForUnitTest Category1 に `…103`/`…104` を初期解放で追加し `GetChallengeInfoProtocolTest` の件数を +2。出所: agent前提。
- **Web UI は変更しない**: 新tutorialType は Unity 側3D表示のみで、ワールドピンは既存の汎用 `tutorial.world_pins` に載る。出所: agent前提（webui に tutorialType の列挙が無いことを確認）。
