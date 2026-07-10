# Satisfactory式設置システム プラン5: 破壊的クリーンアップ 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 旧ホットバー設置プロトコル（`PlaceBlockFromHotBarProtocol`）と「ブロック/車両アイテム」概念を完全に削除する — blocks.yml/train.ymlの`itemGuid`フィールド削除、items.jsonからのブロック/車両アイテム削除、`RemoveBlock`/`RemoveTrainCar`のitemGuidフォールバック削除、アイコン解決のBlockId/車両Guidキー化。

**Architecture:** ブロック→アイテムの変換基盤は`BlockMasterUtil`が`blockElement.ItemGuid`から構築する`_itemIdToBlockId`辞書ただ1つであり、`IsBlock(ItemId)`/`GetBlockId(ItemId)`/`GetItemId(BlockId)`の3APIがそこに乗っている。プラン4完了時点でこの3APIの実行時利用は「アイコン解決」「DisplayEnergizedRange」「返却フォールバック」「旧プロトコル」に減っているため、まず利用側をBlockId/車両Guidキーへ付け替え（アイコンは起動時スクリーンショットの登録キーを変えるだけ）、その後スキーマ・API・マスタデータを一括削除する。

**Tech Stack:** Unity C# / MessagePack / uloop CLI / Python3（マスタ削除スクリプト）

**Spec:** `docs/superpowers/specs/2026-07-03-satisfactory-style-placement-design.md`
**前提:** プラン4（特殊システム縦切り）完了。`docs/superpowers/plans/2026-07-05-satisfactory-placement-plan4-special-systems.md`

## 合意済み・本プランでの設計判断（ユーザーレビュー注目点）

- **木のシャフトのアイテムは「素材アイテム」として存続させる**（推奨案）。機械レシピ`0322f7c7-323a-4102-8ba1-adcfcd9ad151`（原始的な加工機→鉄のロッド）が材料参照しており、削除すると材料置換（バランス変更）が必要になる。存続なら置換不要で、既に「craftコスト==建設コスト」の往復中立が確認済み。blocks.ymlのitemGuid削除後は「ブロックとの関連を持たないただの素材」になる
- **ブロックアイコンは起動時スクリーンショット方式のまま、登録キーをItemId→BlockIdへ変更**（`InitializeScenePipeline.TakeBlockItemImage`が既にプレハブのスクショで生成している。blocks.ymlへのimagePath整備は不要 — 申し送りの「別のアイコン解決」を採用）
- **車両アイコンは車両プレハブ（addressablePath）のスクリーンショットで新設**（車両アイテム削除でアイテム画像経路が消えるため。ブロックと同じスクショリグを流用）
- **既存セーブの互換は取らない**（開発段階・AGENTSの後方互換不要方針。ブロックアイテムを所持した旧セーブはロードエラーになり得る。新規セーブを正とする）
- **レール素材アイテム（5be3a22c）・電線・チェーンは敷設素材として存続**（スペック§1どおり）
- **ホットバーからの設置はこのプランで完全消滅する**（決定済み。ホットバーは素材・道具用として存続）

## Global Constraints

- 作業開始時に必ず`pwd`確認。作業ディレクトリは `/Users/katsumi/moorestech`
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client`（エラー0件）
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。Domain Reloadエラーは45秒待ちリトライ。180秒超はクラス分割
- partial絶対禁止 / 1ファイル200行以下 / 1ディレクトリ10ファイルまで / デフォルト引数禁止 / try-catch禁止 / イベントはUniRx / 日英2行セットコメント
- スキーマ編集はedit-schemaスキルの手順（`_CompileRequester.cs`のdummyText変更、**プロパティ削除時は全JSON配置先のgrep確認**: Tests.Module ForUnitTest / Client.Tests EditModeInPlayingTest / ../moorestech_master / mooresmaster.SandBox）
- moorestech_master編集時はmooreseditor.app停止（`pgrep -fl mooreseditor`）。コミット後は`.moorestech-external-revisions.json`のピン更新
- 各タスク完了時にコミット

## 配置と前例（spec-architecture-review）

| 配置決定 | 層 | 前例 |
|---|---|---|
| `BlockImageContainer`（BlockIdキーのアイコン辞書） | Client.Game/InGame/Context | `ItemImageContainer`（同ディレクトリ・同構造） |
| 車両アイコンのスクショ生成 | Client.Starter/InitializeScenePipeline | `TakeBlockItemImage`（既存のスクショリグ） |
| `_itemIdToBlockId`と3APIの削除 | Core.Master | 生成源が`BlockMasterElement.ItemGuid`のみであることを調査で確認済み（`BlockMasterUtil.cs:458`） |
| 返却フォールバック削除 | RemoveBlock/RemoveTrainCarプロトコル | プラン1/4がフォールバックに「プラン5で削除予定」コメントを残している |
| マスタ削除スクリプト | moorestech_master/tools/plan5_migration | plan2/plan4のmigrate.py（dry-run既定・検証つき） |

---

### Task 1: 返却フォールバック削除とGetBlockMaster二重呼び整理

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`（L40の`itemId`先読み・`GetRefundItems`のelse分岐L101-104・L96の二重`GetBlockMaster`）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveTrainCarProtocol.cs`（`BuildRefundItems`のフォールバック分岐）
- Test: `RemoveBlockProtocolTest.cs`（L62等のItemGuid依存アサーション更新）、`RemoveTrainCarProtocolTest.cs`

**Interfaces:**
- Consumes: プラン4完了時点で本番・テストマスタの全設置可能ブロック/車両にrequiredItemsが投入済みであること（フォールバック経路が実データで死んでいる）
- Produces: 返却は常に`ConstructionCostService.CreateRefundItems(ToItemCounts(...))`のみ。requiredItems未定義は「返却なし」（内部インベントリと`IGetRefundItemsInfo`は現行維持）

- [ ] **Step 1: requiredItems未定義ブロックの実在確認**

Run: `python3 -c "import json; d=json.load(open('/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json')); print([b['name'] for b in d['data'] if not b.get('requiredItems')])"`

Expected: 空リスト（残っていればプラン4の追補漏れ。先に投入してから進む）。ForUnitTest/EditModeInPlayingTestのblocks.jsonも同様に確認し、テストで設置・破壊されるブロックにrequiredItems漏れがないか棚卸しする。

- [ ] **Step 2: テストを「フォールバックなし」仕様へ更新（RED）**

`RemoveBlockProtocolTest.cs`の「requiredItems未定義ブロックはアイテム1個返却」系アサーションを「返却なし（内部インベントリ・IGetRefundItemsInfoのみ）」へ書き換える。`RemoveTrainCarProtocolTest.cs`はプラン4で全額返却検証済みのため変更不要の見込み（フォールバック経路を踏むテストがあれば同様に更新）。

- [ ] **Step 3: フォールバックを削除**

- `RemoveBlockProtocol.cs`: L40の`itemId`先読みを削除し、`GetRefundItems`冒頭の`GetBlockMaster`呼び出しに一本化（二重呼び解消）。else分岐（アイテム1個返却）を削除
- `RemoveTrainCarProtocol.cs`: `BuildRefundItems`のelse分岐（`car.TrainCarMasterElement.ItemGuid`から1個返却）を削除

- [ ] **Step 4: コンパイル＋テスト＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoveBlock|RemoveTrainCar"`
Expected: 全PASS

```bash
git add moorestech_server/
git commit -m "feat: ブロック・車両撤去のアイテム返却フォールバックを削除"
```

---

### Task 2: PlaceBlockFromHotBarProtocolの削除

**Files:**
- Delete: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockFromHotBarProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:39`（登録削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:47-51`（`PlaceHotBarBlock`削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:138-144`（`SendPlaceProtocol`削除）
- Delete: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceHotBarBlockProtocolTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest.cs:19,158`（`SendPlaceHotBarBlockProtocolMessagePack`→`PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack`へ移行）

**Interfaces:**
- Consumes: プラン4 Task 10で`SendPlaceProtocol`/`PlaceHotBarBlock`のクライアント参照がゼロであること（残っていれば先に移行）
- Produces: `va:palceHotbarBlock`タグの消滅。設置プロトコルは`va:placeBlock`＋特殊5プロトコルのみ
- 注意: `PlacePacketDto.cs`（`PlaceInfo`/`PlaceInfoMessagePack`/`BlockCreateParamMessagePack`）は`PlaceBlockProtocol`等が使う共有DTOのため**削除しない**

- [ ] **Step 1: 参照ゼロ確認**

Run: `grep -rn "PlaceBlockFromHotBarProtocol\|PlaceHotBarBlock\|SendPlaceProtocol\b" moorestech_server moorestech_client --include='*.cs'`

削除対象（上記Files）以外のヒットがあれば先に移行する。

- [ ] **Step 2: ElectricWireAutoConnectPlaceTestを新プロトコルへ移行**

`SendPlaceHotBarBlockProtocolMessagePack(playerId, hotBarSlot, placePositions)`による設置準備を`PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(playerId, blockId, placePositions)`へ置換（ホットバーへのアイテム配置準備は建設素材投入へ変更。既存の`PlaceBlockProtocolTest`の準備コードに合わせる）。

- [ ] **Step 3: 本体・登録・クライアントAPI・テストを削除**

ファイル削除は`git rm`で行い（.metaも一緒に`git rm`）、`PacketResponseCreator.cs:39`の登録行、`VanillaApiSendOnly.PlaceHotBarBlock`、`PlaceSystemUtil.SendPlaceProtocol`（と不要になったusing）を削除する。

- [ ] **Step 4: コンパイル＋テスト＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockProtocol|ElectricWireAutoConnect"`
Expected: 全PASS

```bash
git add -A moorestech_server/ moorestech_client/
git commit -m "feat: 旧ホットバー設置プロトコルを削除"
```

---

### Task 3: アイコン解決のBlockId/車両Guidキー化

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockImageContainer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/TrainCarImageContainer.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Mod/Texture/ItemTextureLoader.cs`（`ItemViewData`にブロック/車両用コンストラクタ追加、または表示名+テクスチャの共通形へ）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（`TakeBlockItemImage` L291-318の登録先変更＋車両スクショ追加）
- Modify: `ClientContext.cs`（コンテナ公開。`ItemImageContainer`の公開方法に合わせる）
- Modify（消費側の付け替え）: `UI/BuildMenu/BuildMenuEntryCatalog.cs`（ブロック/車両エントリのアイコン）、`RecipeTabView.cs:55-56`、`MachineRecipeView.cs:136`、`CraftTreeEditorNodeItem.cs:146`、`ElectricWireAutoConnectPreview.cs:65`

**Interfaces:**
- Produces:
  - `ClientContext.BlockImageContainer.GetBlockView(BlockId blockId)` → `ItemViewData`（ブロックのスクショ＋ブロック名）
  - `ClientContext.TrainCarImageContainer.GetTrainCarView(Guid trainCarGuid)` → `ItemViewData`（車両のスクショ＋表示名）
  - `ItemViewData`の追加コンストラクタ: `(Texture2D texture, string displayName)`（`ItemMasterElement`非依存。既存フィールドの使われ方を現物確認し、`ItemId`はEmptyで安全なことをgrepで確認する）
- Consumes: `InitializeScenePipeline.TakeBlockItemImage`のスクショリグ、車両の`addressablePath`ロード（Addressables直列プリロードの罠に注意 — 既存の直列ロード方式に合わせる）

- [ ] **Step 1: ItemViewDataの現物確認と共通化**

`ItemTextureLoader.cs`の`ItemViewData`定義（L55-71）と、`ItemSlotView.SetItem`等での利用フィールドをgrepで確認する。`ItemMasterElement`から取っている値が表示名だけなら`(Texture2D, string displayName)`コンストラクタを追加。`ItemId`フィールドを外部が参照している場合はその用途を洗い、ブロック/車両ビューでは`ItemMaster.EmptyItemId`で問題ないことを確認して報告する。

- [ ] **Step 2: BlockImageContainer / TrainCarImageContainerを作成**

`ItemImageContainer`（同ディレクトリ）と同じ構造で、キーを`BlockId`/`Guid`にした辞書＋`Add`/`Get`を実装する（`GetItemView`のnull時ログ挙動も踏襲）。

- [ ] **Step 3: InitializeScenePipelineの登録先を変更＋車両スクショを追加**

- `TakeBlockItemImage`: `GetItemId(blockId)`によるItemIdキー登録（L299,313-314）を`BlockImageContainer.Add(blockId, new ItemViewData(tex, blockMaster.Name))`へ変更（メソッド名も`TakeBlockImage`へ）
- 車両: `MasterHolder.TrainUnitMaster.Train.TrainCars`を列挙し、`addressablePath`のプレハブを同じスクショリグで撮影して`TrainCarImageContainer.Add(trainCarGuid, ...)`（表示名は車両マスタにnameが無いため、当面`addressablePath`末尾または"車両"+index。プラン4で車両ツールチップに使ったアイテム名は消えるため、**車両name追加は別件**として報告に含める）
- Addressablesロードは既存の直列プリロード方式（`InitializeScenePipeline`内の前例）に合わせる

- [ ] **Step 4: 消費側5箇所を付け替える**

- `BuildMenuEntryCatalog`: ブロックエントリの`IconItemId`をやめ、`BuildMenuEntry`へ`ItemViewData IconView`を持たせる形へ変更（車両・接続ツールも同様。接続ツールのみ`ItemImageContainer.GetItemView(iconItemGuid)`を継続使用 — 敷設素材アイテムは存続するため）。`BuildMenuView`のスロット生成を`entry.IconView`使用へ
- `RecipeTabView.cs` / `MachineRecipeView.cs` / `CraftTreeEditorNodeItem.cs` / `ElectricWireAutoConnectPreview.cs`: `GetItemId(blockId)`→`GetItemView(itemId)`の2段を`ClientContext.BlockImageContainer.GetBlockView(blockId)`へ

- [ ] **Step 5: コンパイル＋動作確認＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
uloopでPlayMode起動し、ビルドメニュー・機械レシピ画面のアイコンが表示されることを確認（uloop-screenshotで記録）。

```bash
git add moorestech_client/
git commit -m "feat: ブロック・車両アイコンをBlockIdと車両Guidキーの専用コンテナへ移行"
```

---

### Task 4: クライアント残存の「アイテム→ブロック変換」掃除

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/.../DisplayEnergizedRange.cs`（L120,122の`IsBlock(ItemId)`/`GetBlockId(ItemId)` — パスはgrepで確定）
- Modify: grepで見つかる他のクライアント残存参照

**Interfaces:**
- Consumes: `PlacementSelection.SelectedBlockId`（プラン4）
- Produces: クライアントから`IsBlock(ItemId)`/`GetBlockId(ItemId)`/`GetItemId(BlockId)`の参照ゼロ（Task 5の削除前提）

- [ ] **Step 1: 残存参照の棚卸し**

Run: `grep -rn "IsBlock(\|GetBlockId(.*ItemId\|GetItemId(" moorestech_client/Assets/Scripts --include='*.cs' | grep -v "GetItemId(.*Guid"`

（`GetItemId(Guid)`はItemMasterの正規APIなので除外。ヒット行の型を目視で判定）

- [ ] **Step 2: DisplayEnergizedRangeを選択駆動へ**

「手持ちアイテムがブロックか」で通電範囲を表示している判定を、`PlacementSelection.SelectedBlockId`（DIで注入。前例: `PlaceSystemStateController`）による判定へ置換する。挙動: ビルドメニューで電気系ブロックを選択中のみ範囲表示（現行の「電気系ブロックのアイテムを持っているとき」と等価な移行）。

- [ ] **Step 3: その他の残存を同様に処置し、コンパイル＋回帰＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests"`

```bash
git add moorestech_client/
git commit -m "refactor: クライアントのアイテム由来ブロック解決を選択駆動へ置換"
```

---

### Task 5: スキーマからitemGuidを削除しCore.Masterを整理

**Files:**
- Modify: `VanillaSchema/blocks.yml`（L113-118のブロック本体`itemGuid`削除。**requiredItems内・blockParam内のitemGuidは対象外**）
- Modify: `VanillaSchema/train.yml`（L15-20の`trainCars[].itemGuid`削除。`railItems[].itemGuid`は対象外）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（SourceGenerator起動）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/BlockMaster.cs`（`IsBlock`/`GetBlockId(ItemId)`/`GetItemId(BlockId)`削除）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs`（`_itemIdToBlockId`構築L458と重複検査L462、バリデーションL32,35の削除）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs`（`GetBlockItemId`拡張メソッド — 呼び出し元なしのデッドコード削除）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/TrainUnitMaster.cs`（`TryGetTrainCarMaster(ItemId)`と`_trainCarMastersByItemId`削除）＋`Validator/TrainUnitMasterUtil.cs`（同辞書構築とItemGuidバリデーション削除）
- Modify: テストマスタJSON（ForUnitTest/EditModeInPlayingTestの`blocks.json`/`train.json`から`itemGuid`キー削除）
- Modify: テストコード（`TrainTestCarFactory.cs:114`、`TrainHugeAutoRunSaveLoadConsistencyTest.cs:365`、`TrainDiagramSaveLoadTest.cs:167`、`TrainCarDefaultContainerTest.cs:59`、`GearChainPoleExtendProtocolTest.cs:41`、`GearChainPlacementEvaluatorTest.cs:24`ほかgrepで全件）

**Interfaces:**
- Consumes: Task 1-4で実行時参照がゼロになっていること
- Produces: `BlockMasterElement`/`TrainCarMasterElement`から`ItemGuid`が消える（生成コンストラクタの引数が1つ減る — 手動`new`箇所は全件引数削除が必要）。ブロック⇔アイテムの変換APIが消滅

- [ ] **Step 1: 参照ゼロの最終確認**

Run: `grep -rn "\.ItemGuid" moorestech_server moorestech_client --include='*.cs' | grep -v "requiredItem\|RequiredItem\|ItemMaster\|itemMaster"`
Run: `grep -rn "IsBlock(\|_itemIdToBlockId\|TryGetTrainCarMaster(.*ItemId\|GetBlockItemId" moorestech_server moorestech_client --include='*.cs'`

ヒットがTask対象（削除するAPI・テスト・バリデータ）のみであることを確認。

- [ ] **Step 2: スキーマ削除→SourceGenerator→コンパイルエラーを潰す**

blocks.yml/train.ymlの該当ブロックを削除し、`_CompileRequester.cs`のdummyTextを変更。コンパイルして出るCSエラー（生成コンストラクタ引数減による`new BlockMasterElement(`/`new TrainCarMasterElement(`手動構築箇所＝主にテスト）を全件修正する:

Run: `grep -rn "new BlockMasterElement(\|new TrainCarMasterElement(" moorestech_server moorestech_client --include='*.cs' | grep -v '/obj/'`

- [ ] **Step 3: Core.MasterのAPI・辞書・バリデーションを削除**

Files欄のとおり。テストコードのItemGuid依存（車両ファクトリ等）は`TrainCarGuid`/`BlockGuid`ベースへ書き換える。

- [ ] **Step 4: テストマスタJSONからitemGuidキーを削除**

```bash
grep -rln '"itemGuid"' moorestech_server/Assets/Scripts/Tests.Module moorestech_client/Assets/Scripts/Client.Tests --include='blocks.json' --include='train.json'
```

各ファイルのブロック本体・trainCars直下の`itemGuid`のみ削除（requiredItems内は残す）。pythonワンライナーで機械的に処理してよい。

- [ ] **Step 5: コンパイル＋全回帰＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.UnitTest|Tests.CombinedTest"`（分割実行）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests"`
Expected: 全PASS

```bash
git add VanillaSchema/ moorestech_server/ moorestech_client/
git commit -m "feat: ブロック・車両のitemGuidをスキーマとCore.Masterから削除"
```

---

### Task 6: 本番マスタのブロック/車両アイテム削除（moorestech_master）

**Files:**
- Create: `/Users/katsumi/moorestech_master/tools/plan5_migration/migrate_plan5.py`
- Modify（スクリプト実行で）: `items.json` / `blocks.json` / `train.json`（v8 mod）

**Interfaces:**
- Consumes: Task 5完了（スキーマにitemGuidが無い＝ローダーはJSONの残存キーを無視するが、整合のためデータも削除する）
- Produces: items.jsonからブロック67−1（木のシャフト除く）＋車両3の**69アイテム削除**、blocks.json/train.jsonの`itemGuid`キー削除。残存参照ゼロの検証つき

- [ ] **Step 1: mooreseditor停止・ブランチ確認**

Run: `pgrep -fl mooreseditor || echo "not running"` / `git -C /Users/katsumi/moorestech_master status --short`

- [ ] **Step 2: 削除スクリプトを作成・dry-run**

`migrate_plan5.py`（dry-run既定・`--apply`で書き込み）:

```python
#!/usr/bin/env python3
"""プラン5: ブロック/車両アイテムをitems.jsonから削除し、blocks/trainのitemGuidキーを落とす。
Plan5: delete block/train-car items from items.json and drop itemGuid keys from blocks/train."""
import json
import sys
from pathlib import Path

MASTER = Path(__file__).resolve().parent.parent.parent / 'server_v8/mods/moorestechAlphaMod_8/master'
WOOD_SHAFT = '24a63965-fc83-4eff-b0b0-00e264d23c1f'  # 素材として存続 / kept as a material
APPLY = '--apply' in sys.argv


def load(name):
    return json.loads((MASTER / name).read_text(encoding='utf-8'))


def save(name, data):
    (MASTER / name).write_text(json.dumps(data, ensure_ascii=False, indent=4) + '\n', encoding='utf-8')


blocks = load('blocks.json')
train = load('train.json')
items = load('items.json')

# 削除対象 = 全ブロックのitemGuid + 車両3種のitemGuid − 木のシャフト
# Targets = every block itemGuid + the 3 car itemGuids, minus the wood shaft
target_guids = {b['itemGuid'] for b in blocks['data'] if 'itemGuid' in b}
target_guids |= {c['itemGuid'] for c in train['trainCars'] if 'itemGuid' in c}
target_guids.discard(WOOD_SHAFT)

# items.json から削除する
# Delete from items.json
before = len(items['data'])
items['data'] = [i for i in items['data'] if i['itemGuid'] not in target_guids]
deleted = before - len(items['data'])

# blocks/train から itemGuid キー自体を落とす
# Drop the itemGuid keys from blocks/train
for b in blocks['data']:
    b.pop('itemGuid', None)
for c in train['trainCars']:
    c.pop('itemGuid', None)

# 検証: 全マスタJSONに削除guidの残存参照が無いこと
# Validate: no master json still references a deleted guid
dangling = []
for path in sorted(MASTER.glob('*.json')):
    text = path.read_text(encoding='utf-8') if path.name not in ('items.json', 'blocks.json', 'train.json') else json.dumps(locals().get(path.stem, load(path.name)), ensure_ascii=False)
    for guid in target_guids:
        if guid in text:
            dangling.append((path.name, guid))

print(f'delete {deleted} items (expected 69)')
assert deleted == len(target_guids), f'{deleted} != {len(target_guids)}'
assert not dangling, f'dangling refs: {dangling}'

if APPLY:
    save('items.json', items)
    save('blocks.json', blocks)
    save('train.json', train)
    print('APPLIED')
else:
    print('DRY RUN (use --apply to write)')
```

（実行前にインデント・キー名を現物確認。danglingが出た場合は該当参照 — research/challenges等 — の除去方針を判断して報告。プラン4で12件は除去済みのため、想定される残存はゼロ）

- [ ] **Step 3: 適用→起動確認→コミット→ピン更新**

```bash
python3 /Users/katsumi/moorestech_master/tools/plan5_migration/migrate_plan5.py
python3 /Users/katsumi/moorestech_master/tools/plan5_migration/migrate_plan5.py --apply
git -C /Users/katsumi/moorestech_master diff --stat
```

uloopで本番マスタ起動し、ロードエラー・バリデーションエラーがないことを確認（`uloop get-logs --log-type Error`）。

```bash
git -C /Users/katsumi/moorestech_master add tools/plan5_migration/ server_v8/
git -C /Users/katsumi/moorestech_master commit -m "feat: プラン5 ブロック・車両アイテムを削除しitemGuidキーを撤去"
# moorestech側のピン更新
git add .moorestech-external-revisions.json
git commit -m "chore: moorestech_masterのピンをプラン5へ更新"
```

---

### Task 7: 全体回帰とPlayMode実機検証

**Files:** なし（検証のみ）

- [ ] **Step 1: 全回帰**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.UnitTest|Tests.CombinedTest"`（分割）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests"`
Expected: 全PASS

- [ ] **Step 2: PlayMode実機検証（録画付き・新規セーブ）**

`unity-playmode-recorded-playtest`スキルで検証:

1. **起動**: 本番マスタで新規セーブ起動、エラーログなし
2. **アイコン**: ビルドメニューのブロック/接続ツールアイコン、機械レシピ画面の機械アイコンが表示される
3. **設置・破壊の往復**: ブロック設置（素材消費）→破壊（素材全額返却）で所持数が往復一致
4. **クラフトUI**: アイテム一覧にブロック/車両アイテムが出ない。木のシャフト・レール・電線・チェーンは出る
5. **鉄のロッド生産**: 原始的な加工機で木のシャフトを材料に鉄のロッドが作れる（機械レシピ存続確認）
6. **ホットバー**: 素材を持ってもブロック設置モードに入らない（旧経路消滅の確認）

- [ ] **Step 3: 申し送り更新とコミット**

`docs/superpowers/plans/2026-07-05-satisfactory-placement-handoff.md`へプラン5完了・移行プロジェクト完結を追記。残件（車両nameフィールド追加、moorestech_masterブランチ整理、Responses.cs/CommonBlockPlaceSystemの行数棚卸し）を明記する。

```bash
git status --short
git add docs/ && git commit -m "docs: プラン5完了の申し送りを追記"
```
