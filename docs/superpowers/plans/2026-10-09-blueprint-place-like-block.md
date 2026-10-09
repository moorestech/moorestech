# BPを通常ブロックと同じ体験で置き、配線ごと貼り付ける Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ブループリント（BP）を外接箱1個の大きなブロックとして通常ブロックと同じ規則で置けるようにし、内部の電線・歯車チェーンを保存・復元し、素材不足をプレビューとビルドメニューに出し、大きいBPのちらつきを直す。

**Architecture:** 保存形式はオフセット基準を外接箱最小角へ正規化し配線（wires/chains）を足す（版4→5の移行付き）。貼り付けの判定（重なり・解放・総素材・配線解決）はサーバーとクライアントが共有する純ロジック `BlueprintPastePlanner`（`Server.Protocol/PacketResponse/Util/Blueprint/`）に一本化し、クライアントはプレビュー色・ツールチップに、サーバーは新プロトコル `va:pasteBlueprint` の一括判定と実行に使う。置き位置はクライアントが通常設置のセル解決 `PlaceSystemUtil.CalcPlacePointBySize` を外接箱寸法で呼び、側面だけ縦を最下段揃えに直し、地面では ADR 0047 の地形最高点で決める。

**Tech Stack:** Unity C# / NUnit / MessagePack / Newtonsoft.Json / UniRx / VContainer、Web UI（TypeScript・通知表と辞書の再生成のみ）

## Requirements

設計ADR: [docs/adr/0077-blueprint-placement-as-one-large-block-with-wiring.md](../../adr/0077-blueprint-placement-as-one-large-block-with-wiring.md)

- R1 保存: BPの各ブロックのオフセットは外接箱最小角（全ブロック `OriginalPos` の成分最小）基準。受け入れ: 作成したBPのオフセット成分最小が (0,0,0)。
- R2 移行: 版4のセーブのBPが版5へ変換され、オフセットが成分最小0へ平行移動し `wires`/`chains` が空配列で付く。受け入れ: 移行テストが通り、`SaveMigrationChain` 構築で例外が出ない。
- R3 配線保存: 作成時、両端ともコピー対象内の電線・歯車チェーン接続を BP内ブロック index 2つ＋線種Guid で保存する（同じペアは1本）。外へ伸びる接続は保存しない。受け入れ: 電柱2本＋機械の配線つき範囲を作成すると wires が期待本数。
- R4 置き位置（地面）: 外接箱をXZでカーソル中心に置き、底は外接箱底面フットプリントの地形最高点を含むセル（ADR 0047）＋Q/E。受け入れ: 置き位置純ロジックテスト・録画で中心に出る。
- R5 置き位置（側面）: 既存ブロックの側面ヒットで外接箱が面の外側へ接し、面に平行な横はカーソル中心、縦は最下段＝カーソル段＋Q/E。上下面は通常どおり。受け入れ: 純ロジックテスト・録画で既存ブロックにめり込まない。
- R6 回転: R で回転しても外接箱最小角が原点に一致する（共有計算）。受け入れ: 計算テスト。
- R7 自動配線なし: BP貼り付けで置いた電気ブロックは周辺自動配線をしない。受け入れ: プロトコルテストで保存配線以外の線が無い。
- R8 配線復元: 両端が置けた保存配線を復元し素材を消費する。片端が重なりで置けなければその線は張らない。受け入れ: プロトコルテスト。
- R9 素材不足（単体）: 重ならず置けるブロック分＋復元配線分の総素材が1つでも足りなければ、そのBPのゴースト全体が赤・クリックで何も置かない・不足素材をカーソルtooltip（`名前 所持/必要`）へ。サーバーも同じ判定でそのBPを丸ごと拒否し、ログ＋通知を出す。受け入れ: プロトコルテスト・録画。
- R10 素材不足（列）: ドラッグ列では始点側から賄えるBPの個数までを丸ごと青、以降のBPは丸ごと赤で置かない。受け入れ: プランナーテスト・プロトコルテスト。
- R11 重なり: 既存ブロックと重なるブロックは飛ばし残りを置く（現行維持）。全ブロック重なりなら既存の理由行。受け入れ: プロトコルテスト。
- R12 配線ゴースト: 復元予定の電線・チェーンをブロックゴーストと同じ青/赤で線表示する。受け入れ: 録画で線が見える。
- R13 ビルドメニュー: BPエントリの必要素材に BP全体（全ブロック＋全保存配線）の合計を出し、所持/不足表示は ADR 0041 の仕組みに乗る。受け入れ: `BuildMenuEntryDtoFactoryTest` 追加ケース。
- R14 ちらつき: 大きいBPの貼り付けプレビューが点滅しない。原因は実測で確定してから直す。受け入れ: 録画の連続フレームでゴースト表示が途切れない・計測ログ。
- R15 解放: 未解放ブロック・未解放の線種を含むBPは置かない（サーバーはBP丸ごと拒否＋通知、クライアントは赤）。BP機能未解放ならプロトコルが拒否する（ADR 0015）。

やらないこと:
- 範囲選択（コピー側）の表示を外接箱へ縮めること（ADR 0077 棄却案C）。
- BP外の既存電力網・チェーンへの自動接続（ADR 0077 決定5）。
- 複数BP間（列の隣同士）の配線。保存配線は各BPの内部だけを復元する。
- 無料設置デバッグの経路一本化そのもの（並行の `fix/free-placement-single-path` の担当）。

## Global Constraints

- 1ファイル200行未満。超えるなら責務で分割。partial 禁止。
- 1ディレクトリの新規コードは10ファイルまで（超えるならサブディレクトリ）。
- `Func<>` 禁止。デフォルト引数禁止（引数追加時は全呼び出し側を変更）。
- イベント発火は UniRx。単純な getter/setter プロパティ禁止（`{ get; private set; }` は可）。
- コメントは主要処理に「// 日本語 → // English」の2行セット（各1行）。`#region Internal` はメソッド内ローカル関数をまとめる用途のみ。
- try-catch 禁止（外部境界のみ）。fail-closed の拒否・縮退は理由を必ずログへ出す（無音禁止）。
- null チェックは外部データ・非同期ロード結果にのみ。
- サーバーのゲームロジックで実時間APIを使わない。
- セーブ形式変更は `WorldSaveAllInfo.CurrentVersion` を1上げ、`ISaveMigrationStep` を足して `SaveAndEventServiceRegistration` の `SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] {...})` へ登録し、旧版変換テストを同梱する（AGENTS.md・moorestech-save-migration スキル）。
- `.meta` を手で作らない。Prefab/Scene を手で編集しない。
- .cs を書いたら必ず `uloop compile --project-path ./moorestech_client`。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。
- スキーマ・JSON の optional 化やフォールバック補完は使わない（BPの `wires`/`chains` は必須）。
- 辞書行追加後は `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の再生成印を更新し、`moorestech_web/webui` で `npm run gen:i18n` を実行して生成物をコミットする（前例 9d87bcecc6・22fc0659be）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| ファイル | 責務 |
|---|---|
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintJsonObject.cs`（変更） | BP本体・ブロック・配線行（`BlueprintLineJsonObject`）の保存型。オフセット基準の注記を外接箱最小角へ |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs`（変更） | 最小角アンカーでの作成。配線収集は下へ委譲 |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintLineCollector.cs`（新規） | コピー対象内の電線・チェーン接続を index 化して返す |
| `moorestech_server/Assets/Scripts/Game.Blueprint/Game.Blueprint.asmdef`（変更） | `Game.EnergySystem` 参照を追加（`IElectricWireConnector`） |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPlacementElement.cs`（変更） | `BlockIndex` を追加（マスタ欠損スキップ後も配線の index を解決するため） |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPasteCalculator.cs`（変更） | 回転後の外接箱最小角を原点へ平行移動 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV4ToV5.cs`（新規） | BPオフセットの正規化と配線リスト追加 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（変更） | `CurrentVersion = 5` |
| `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs`（変更） | 移行ステップ登録 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteLine.cs`（新規） | 解決済み配線1本（種別・端点位置・端点要素 index・線種） |
| `.../Util/Blueprint/BlueprintPasteCopyPlan.cs`（新規） | BP1個分の判定結果（要素・重なりフラグ・配線・状態） |
| `.../Util/Blueprint/BlueprintPastePlan.cs`（新規） | 列全体の判定結果と不足素材 |
| `.../Util/Blueprint/IBlueprintPasteWorld.cs`（新規） | 重なり・解放の問い合わせ口（サーバー/クライアントが実装） |
| `.../Util/Blueprint/BlueprintPasteCostCalculator.cs`（新規） | BP群の総素材（財布込みブロック分＋配線分） |
| `.../Util/Blueprint/BlueprintPastePlanner.cs`（新規） | 列のBPごとに重なり→解放→総素材を判定する共有の唯一の定義 |
| `.../Util/Blueprint/BlueprintPasteExecutor.cs`（新規・サーバー専用） | 判定済みBPを設置し配線を復元する |
| `.../Util/Blueprint/ServerBlueprintPasteWorld.cs`（新規・サーバー専用） | `IBlueprintPasteWorld` のサーバー実装 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPasteProtocol.cs`（新規） | `va:pasteBlueprint` |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintLineMessagePack.cs`（新規） | BP一覧DTOの配線行 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPacketDto.cs`（変更） | `BlueprintMessagePack` に Wires/Chains |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（変更） | プロトコル登録 |
| `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`（変更） | `PasteBlueprint` 送信口 |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs`（新規） | カーソル→BP原点（地面/面/側面） |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/ClientBlueprintPasteWorld.cs`（新規） | `IBlueprintPasteWorld` のクライアント実装 |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilder.cs`（変更） | 列の原点列（地形追従込み）を返す |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs`（変更） | BP単位の青/赤と要素ごとのゴーストを返す |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteLinePreview.cs`（新規） | 復元予定の電線・チェーンの線ゴースト |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPastePlaceSender.cs`（変更） | 新プロトコル送信と Undo 記録 |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteSystem.cs`（変更） | 上を束ねる |
| `moorestech_client/.../PlaceSystem/Targets/BlueprintPlacementTarget.cs`・`PlacementTargetFactory.cs`・`PlacementTargetResolver.cs`（変更） | BP全体の必要素材を持たせる |
| `Localization/localization.csv`・`moorestech_web/webui/src/features/notification/notificationMessages.ts`（変更） | 拒否通知の辞書と通知ID |

`...` は `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse`、`moorestech_client/...` は `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem`。

---

### Task 1: 大きいBPのちらつきを再現・計測し原因を確定して直す

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-large-paste-flicker.cs`
- Modify（計測中のみ・コミット前に戻す）: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteSystem.cs`
- Modify（仮説確定時の修正）: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs`

**Interfaces:**
- Produces: `BlueprintPastePreviewController` が生成するゴースト配下の collider をレイ対象外にする（仮説確定時のみ）。後続タスクは同クラスを拡張する。

- [ ] **Step 1: 計測ログを一時的に入れる**

`BlueprintPasteSystem.UpdatePastePreview` の `PlacementUnitCellResolver.TryGetCursorCell` 呼び出しの直前に、レイが何に当たったかを毎フレーム出す（計測専用・Step 5 で消す）。

```csharp
// 計測: レイの当たり先とゴースト表示を毎フレーム記録する（ちらつき原因の特定用・コミットしない）
// Probe: log the ray's hit and ghost visibility each frame to locate the flicker (never committed)
var probeRay = _mainCamera.ScreenPointToRay(Client.Game.InGame.Control.AimPointProvider.GetAimScreenPoint());
var probeHit = Physics.Raycast(probeRay, out var probeInfo, float.PositiveInfinity, Client.Common.LayerConst.Without_Player_MapObject_Block_LayerMask);
Debug.Log($"[BlueprintPasteProbe] frame={Time.frameCount} hit={probeHit} collider={(probeHit ? probeInfo.collider.name : "-")} layer={(probeHit ? LayerMask.LayerToName(probeInfo.collider.gameObject.layer) : "-")} isGhost={(probeHit && probeInfo.collider.GetComponentInParent<Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController.BlockPreviewObject>() != null)}");
```

あわせて `_previewController.Hide()` を呼ぶ3か所の直前に `Debug.Log($"[BlueprintPasteProbe] frame={Time.frameCount} hide reason=<その分岐の理由>");` を置く。

- [ ] **Step 2: 大きいBPの録画シナリオを書く**

既存 `scenarios/building/blueprint-copy-paste-via-ui.cs` の準備部分（入力設定・`SetupDebugEnvironment`・`SkipOpeningSkitIfPlaying`・`Hotbar.UnlockBlueprint`・`BlueprintCopyPasteScenarioChecks`）を写し、次を行うシナリオ `blueprint-large-paste-flicker` を作る。

```csharp
// 8x8 のチェスト群（64ブロック）を置き、1つのBPとしてコピーしてから、地面へカーソルを向けて60フレーム静止する
// Place an 8x8 chest field (64 blocks), copy it as one blueprint, then hold the cursor on the ground for 60 frames
for (var x = 0; x < 8; x++)
for (var z = 0; z < 8; z++)
    p.PlaceBlockDirect("木のチェスト", new Vector3Int(2 + x, 32, 2 + z), BlockDirection.North);
await p.WaitBlockGameObject(new Vector3Int(9, 32, 9));
p.WarpPlayer(new Vector3(6f, 36f, -8f));
// （コピー: 始点(2,32,2)→終点(9,32,9)→名前確定。手順は blueprint-copy-paste-via-ui.cs と同じ）
// （貼り付け: BPカテゴリから作成したBPを選ぶ）
await p.AimAt(new Vector3(6f, 32f, 20f));
for (var i = 0; i < 6; i++)
{
    await UniTask.DelayFrame(10);
    await p.Screenshot($"hold-{i:00}");
}
```

- [ ] **Step 3: 実行してログを読む**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-large-paste-flicker.cs`
→ `uloop get-logs --project-path ./moorestech_client --search-text "BlueprintPasteProbe"`
Expected: 静止中に `hit=` の当たり先が「地面」と「ゴースト（isGhost=True）」で交互になる、または `hide reason=cursor has no placement surface` がフレームおきに出る＝主仮説確定。別の振る舞いなら観測値（どの行がどの周期で出るか）をそのまま報告ファイルへ書いて **このタスクを止める**（推測で直さない）。

- [ ] **Step 4: 仮説確定時のみ修正する**

ゴーストはトリガー collider を持ち、レイのマスク（`Without_Player_MapObject_Block_LayerMask`）は Block/BlockBoundingBox 以外を通すため、ゴーストの collider がレイを受けうる。`BlueprintPastePreviewController` がプールから取り出したゴーストの collider をレイから外す。

```csharp
var previewObject = _pool.GetObject(placement.BlockId);
// ゴースト自身がカーソルのレイを受けると照準が毎フレーム外れて点滅するため、レイの対象から外す
// A ghost catching the cursor ray drops the aim every other frame and flickers, so keep it out of raycasts
previewObject.SetRaycastIgnored();
```

`BlockPreviewObject` に追加する（`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs`）:

```csharp
public void SetRaycastIgnored()
{
    // 子のcolliderをIgnore Raycastレイヤーへ移しカーソル判定から外す
    // Move child colliders to the Ignore Raycast layer so cursor raycasts skip them
    var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
    foreach (var childCollider in GetComponentsInChildren<Collider>(true)) childCollider.gameObject.layer = ignoreRaycastLayer;
}
```

再実行し、静止中の `isGhost=True` と `hide reason` が消えることを確認する。

- [ ] **Step 5: 計測ログを消してコンパイル・コミット**

Step 1 のログ行をすべて消す。
Run: `uloop compile --project-path ./moorestech_client` → Expected: ErrorCount 0

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-large-paste-flicker.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs
git commit -m "fix: 大きいBPの貼り付けゴーストがレイを遮って点滅するのを止める（実測: <Step 3 のログ要約>）"
```

---

### Task 2: BP保存形式を外接箱最小角＋配線へ変え、版4→5の移行を足す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintLineCollector.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/Game.Blueprint.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV4ToV5.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintLineMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPacketDto.cs`
- Modify（コンストラクタ変更の追随）: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest/BlueprintIdentityProtocolTest.cs`, `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintDatastoreTest.cs`, `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintPasteCalculatorTest.cs`, `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Blueprint/BlueprintFootprintCalculatorTest.cs`, `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/HotbarSaveLoadTest.cs`, `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/HotbarAssignmentDatastoreTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilderTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintCreateServiceTest.cs`（変更）, `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Blueprint/BlueprintLineCollectorTest.cs`（新規）, `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/BlueprintMigration/SaveMigrationStepV4ToV5Test.cs`（新規）

**Interfaces:**
- Produces:
  - `public class BlueprintLineJsonObject { [JsonProperty("blockIndexA")] public int BlockIndexA; [JsonProperty("blockIndexB")] public int BlockIndexB; [JsonProperty("connectToolGuid")] public string ConnectToolGuidStr; [JsonIgnore] public Guid ConnectToolGuid; public BlueprintLineJsonObject(); public BlueprintLineJsonObject(int blockIndexA, int blockIndexB, Guid connectToolGuid); }`
  - `BlueprintJsonObject` に `[JsonProperty("wires")] public List<BlueprintLineJsonObject> Wires;` と `[JsonProperty("chains")] public List<BlueprintLineJsonObject> Chains;`、コンストラクタ `BlueprintJsonObject(string name, List<BlueprintBlockJsonObject> blocks, List<BlueprintLineJsonObject> wires, List<BlueprintLineJsonObject> chains, Guid blueprintGuid)`
  - `public static class BlueprintLineCollector { public static (List<BlueprintLineJsonObject> wires, List<BlueprintLineJsonObject> chains) Collect(IReadOnlyList<IBlock> copyTargets); }`
  - `BlueprintMessagePack` に `[Key(3)] List<BlueprintLineMessagePack> Wires`、`[Key(4)] List<BlueprintLineMessagePack> Chains`
  - `public sealed class SaveMigrationStepV4ToV5 : ISaveMigrationStep { public int FromVersion => 4; }`

- [ ] **Step 1: 保存型を変える**

`BlueprintJsonObject.cs` の `BlueprintJsonObject` に配線2リストを足し、コンストラクタを5引数にする。空コンストラクタでも両リストを new する。`BlueprintBlockJsonObject` のオフセット注記を「外接箱最小角（全ブロックの OriginalPos の成分最小）からの相対」へ直す。同ファイル末尾に配線行の型を足す（ファイル内の型を増やして Game.Blueprint の .cs を10本以内に保つ）。

```csharp
public class BlueprintLineJsonObject
{
    // 端点はBP内ブロックのindex。座標は貼り付け時に回転後の位置から解決する
    // Endpoints are block indices inside the blueprint; positions are resolved after rotation at paste time
    [JsonProperty("blockIndexA")] public int BlockIndexA;
    [JsonProperty("blockIndexB")] public int BlockIndexB;
    [JsonProperty("connectToolGuid")] public string ConnectToolGuidStr;
    [JsonIgnore] public Guid ConnectToolGuid => Guid.Parse(ConnectToolGuidStr);

    public BlueprintLineJsonObject() { }

    public BlueprintLineJsonObject(int blockIndexA, int blockIndexB, Guid connectToolGuid)
    {
        BlockIndexA = blockIndexA;
        BlockIndexB = blockIndexB;
        ConnectToolGuidStr = connectToolGuid.ToString();
    }
}
```

- [ ] **Step 2: 配線収集を書く**

`Game.Blueprint.asmdef` の references に `"Game.EnergySystem"` を足す（`Game.EnergySystem` は `Game.Blueprint` を参照しないので循環しない）。

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Blueprint
{
    /// <summary>
    ///     コピー対象内で両端が閉じた電線・歯車チェーンをindex化する
    ///     Indexes wires and gear chains whose both ends lie inside the copy targets
    /// </summary>
    public static class BlueprintLineCollector
    {
        public static (List<BlueprintLineJsonObject> wires, List<BlueprintLineJsonObject> chains) Collect(IReadOnlyList<IBlock> copyTargets)
        {
            var indexById = new Dictionary<BlockInstanceId, int>();
            for (var i = 0; i < copyTargets.Count; i++) indexById[copyTargets[i].BlockInstanceId] = i;

            var wires = new List<BlueprintLineJsonObject>();
            var chains = new List<BlueprintLineJsonObject>();
            for (var i = 0; i < copyTargets.Count; i++)
            {
                CollectWires(i);
                CollectChains(i);
            }

            return (wires, chains);

            #region Internal

            // 同じ線を両端から二重に拾わないよう、自分より後ろのindexの相手だけ採る
            // Take only partners with a larger index so each line is collected once
            void CollectWires(int selfIndex)
            {
                if (!copyTargets[selfIndex].ComponentManager.TryGetComponent<IElectricWireConnector>(out var connector)) return;
                foreach (var (partnerId, connection) in connector.WireConnections)
                {
                    if (!indexById.TryGetValue(partnerId, out var partnerIndex) || partnerIndex <= selfIndex) continue;
                    wires.Add(new BlueprintLineJsonObject(selfIndex, partnerIndex, connection.Record.ConnectToolGuid));
                }
            }

            // チェーンは接続一覧を公開しないため、BP内の後方ブロックとの記録を引く
            // Chains expose no connection list, so look up the record against later blocks in the blueprint
            void CollectChains(int selfIndex)
            {
                if (!copyTargets[selfIndex].ComponentManager.TryGetComponent<IGearChainPole>(out var pole)) return;
                for (var partnerIndex = selfIndex + 1; partnerIndex < copyTargets.Count; partnerIndex++)
                {
                    if (!pole.TryGetChainConnectionRecord(copyTargets[partnerIndex].BlockInstanceId, out var record)) continue;
                    chains.Add(new BlueprintLineJsonObject(selfIndex, partnerIndex, record.ConnectToolGuid));
                }
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: 作成を最小角アンカーにして配線を保存する**

`BlueprintCreateService.TryCreateFromArea` の `CalcAnchor` を成分最小へ置き換え、ブロック列の順序で配線を収集して渡す。

```csharp
// 外接箱の最小角（全ブロック原点の成分最小）をアンカーにし、オフセットを非負にそろえる
// Anchor at the extent's min corner (component-wise min of block origins) so offsets are non-negative
var anchor = CalcMinCorner(targets);
var blocks = new List<BlueprintBlockJsonObject>();
foreach (var data in targets) blocks.Add(CreateBlockJson(data, anchor));

// 配線は保存したブロック順のindexで記録する
// Lines are recorded by the index order of the saved blocks
var (wires, chains) = BlueprintLineCollector.Collect(targets.ConvertAll(data => data.Block));
blueprint = new BlueprintJsonObject(name, blocks, wires, chains, GameRandom.NextGuid());
return true;
```

```csharp
Vector3Int CalcMinCorner(List<WorldBlockData> copyTargets)
{
    var min = copyTargets[0].Block.BlockPositionInfo.MinPos;
    foreach (var data in copyTargets) min = Vector3Int.Min(min, data.Block.BlockPositionInfo.MinPos);
    return min;
}
```

- [ ] **Step 4: DTO に配線を載せる**

`BlueprintLineMessagePack.cs`:

```csharp
using System;
using Game.Blueprint;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    [MessagePackObject]
    public class BlueprintLineMessagePack
    {
        [Key(0)] public int BlockIndexA { get; set; }
        [Key(1)] public int BlockIndexB { get; set; }
        [Key(2)] public string ConnectToolGuidStr { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintLineMessagePack() { }

        public BlueprintLineMessagePack(BlueprintLineJsonObject jsonObject)
        {
            BlockIndexA = jsonObject.BlockIndexA;
            BlockIndexB = jsonObject.BlockIndexB;
            ConnectToolGuidStr = jsonObject.ConnectToolGuidStr;
        }

        public BlueprintLineJsonObject ToJsonObject()
        {
            return new BlueprintLineJsonObject(BlockIndexA, BlockIndexB, Guid.Parse(ConnectToolGuidStr));
        }
    }
}
```

`BlueprintMessagePack` に `[Key(3)] public List<BlueprintLineMessagePack> Wires { get; set; }` と `[Key(4)] public List<BlueprintLineMessagePack> Chains { get; set; }` を足し、コンストラクタで `jsonObject.Wires.Select(w => new BlueprintLineMessagePack(w)).ToList()`（Chains も同様）、`ToJsonObject` で `new BlueprintJsonObject(Name, Blocks.Select(b => b.ToJsonObject()).ToList(), Wires.Select(w => w.ToJsonObject()).ToList(), Chains.Select(c => c.ToJsonObject()).ToList(), BlueprintGuid)` にする。

- [ ] **Step 5: 移行ステップを書く**

```csharp
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V4→V5移行（ADR0077）: BPのオフセットを外接箱最小角基準へ平行移動し、配線リストを空で足す
    // V4→V5 migration (ADR 0077): shift blueprint offsets to the extent's min corner and add empty line lists
    // 最小角は各ブロック原点(=MinPos)の成分最小なのでマスタを引かない
    // The min corner is the component-wise min of block origins (= MinPos), so the master is never read
    public sealed class SaveMigrationStepV4ToV5 : ISaveMigrationStep
    {
        public int FromVersion => 4;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // BPを1件も持たないセーブは節自体が無いことがある
            // A save without blueprints may lack the section entirely
            var blueprintsToken = save["blueprints"];
            if (blueprintsToken == null || blueprintsToken.Type == JTokenType.Null) return SaveMigrationStepResult.Converted(save);
            if (!(blueprintsToken is JArray blueprints)) return Fail($"blueprintsが配列ではありません。 type={blueprintsToken.Type}");

            foreach (var blueprintToken in blueprints)
            {
                if (!(blueprintToken is JObject blueprint)) return Fail($"blueprints要素がオブジェクトではありません。 type={blueprintToken.Type}");
                if (!(blueprint["blocks"] is JArray blocks)) return Fail($"BPのblocksが配列ではありません。 guid={blueprint["guid"]}");
                ShiftToMinCorner(blocks);
                blueprint["wires"] = new JArray();
                blueprint["chains"] = new JArray();
            }

            Debug.Log($"セーブを版4から版5へ変換しました。BP={blueprints.Count}件");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            void ShiftToMinCorner(JArray blocks)
            {
                if (blocks.Count == 0) return;
                var minX = int.MaxValue; var minY = int.MaxValue; var minZ = int.MaxValue;
                foreach (var block in blocks)
                {
                    minX = Mathf.Min(minX, block["offsetX"].Value<int>());
                    minY = Mathf.Min(minY, block["offsetY"].Value<int>());
                    minZ = Mathf.Min(minZ, block["offsetZ"].Value<int>());
                }

                foreach (var block in blocks)
                {
                    block["offsetX"] = block["offsetX"].Value<int>() - minX;
                    block["offsetY"] = block["offsetY"].Value<int>() - minY;
                    block["offsetZ"] = block["offsetZ"].Value<int>() - minZ;
                }
            }

            SaveMigrationStepResult Fail(string failureReason)
            {
                Debug.LogWarning($"セーブを版4から版5へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
```

`WorldSaveAllInfo.CurrentVersion` を `5` にし、`SaveAndEventServiceRegistration.cs:114` の配列末尾へ `new SaveMigrationStepV4ToV5()` を足す。

- [ ] **Step 6: テストを書く**

`SaveMigrationStepV4ToV5Test.cs`（`SaveMigrationStepV3ToV4Test` と同じ JObject 組み立て方）:

```csharp
[Test]
public void BPのオフセットが最小角基準へ移り配線リストが空で付くTest()
{
    var save = new JObject
    {
        ["worldVersion"] = 4,
        ["blueprints"] = new JArray(new JObject
        {
            ["name"] = "a", ["guid"] = Guid.NewGuid().ToString(),
            ["blocks"] = new JArray(Block(-2, 0, -1), Block(1, 2, 3)),
        }),
    };

    var result = new SaveMigrationStepV4ToV5().Migrate(save);

    Assert.IsTrue(result.IsConverted, result.FailureReason);
    var blocks = result.Save["blueprints"][0]["blocks"];
    Assert.AreEqual(0, blocks[0]["offsetX"].Value<int>());
    Assert.AreEqual(0, blocks[0]["offsetZ"].Value<int>());
    Assert.AreEqual(3, blocks[1]["offsetX"].Value<int>());
    Assert.AreEqual(2, blocks[1]["offsetY"].Value<int>());
    Assert.AreEqual(4, blocks[1]["offsetZ"].Value<int>());
    Assert.AreEqual(0, result.Save["blueprints"][0]["wires"].Count());
    Assert.AreEqual(0, result.Save["blueprints"][0]["chains"].Count());

    JObject Block(int x, int y, int z) => new() { ["offsetX"] = x, ["offsetY"] = y, ["offsetZ"] = z, ["blockGuid"] = Guid.Empty.ToString(), ["direction"] = 0, ["settings"] = new JObject() };
}

[Test]
public void BP節が無いセーブもそのまま変換されるTest()
{
    var result = new SaveMigrationStepV4ToV5().Migrate(new JObject { ["worldVersion"] = 4 });
    Assert.IsTrue(result.IsConverted, result.FailureReason);
}

[Test]
public void blocksが配列でなければ失敗理由を返すTest()
{
    LogAssert.Expect(LogType.Warning, new Regex("版4から版5へ変換できません"));
    var save = new JObject { ["blueprints"] = new JArray(new JObject { ["blocks"] = 1 }) };
    var result = new SaveMigrationStepV4ToV5().Migrate(save);
    Assert.IsFalse(result.IsConverted);
}
```

`BlueprintCreateServiceTest` の `AreaExtractionTest` の期待アンカーを「チェスト(0,0,0) と機械(3,0,4) の原点の成分最小 = (0,0,0)」へ直し、`chestBlock.Offset == (0,0,0)`、`machineBlock.Offset == (3,0,4)` とする。`AnchorFollowsBlockExtentNotBoxTest` は余白付きでも `(0,0,0)` のまま。

`BlueprintLineCollectorTest.cs`（`ElectricWireSaveLoadTest` と同じ DI 生成と `ElectricWireSystemUtil.TryConnect`、`GearChainPoleExtendTestHelper` の線種Guidを使う）:

```csharp
[Test]
public void 範囲内で閉じた電線とチェーンだけを保存するTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    // 電柱3本: 2本は範囲内、1本は範囲外。範囲内同士と範囲外への線を張る
    // Three poles: two inside, one outside; wire inside-inside and inside-outside
    PlaceWithItems(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0));
    PlaceWithItems(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(3, 0, 0));
    PlaceWithItems(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(20, 0, 0));
    Assert.IsTrue(ElectricWireSystemUtil.TryConnect(new Vector3Int(0, 0, 0), new Vector3Int(3, 0, 0), PlayerId, ElectricWireConnectToolGuid, out _));
    Assert.IsTrue(ElectricWireSystemUtil.TryConnect(new Vector3Int(3, 0, 0), new Vector3Int(20, 0, 0), PlayerId, ElectricWireConnectToolGuid, out _));
    // チェーンポール2本を範囲内に置いて1本張る
    // Two chain poles inside the box, one chain between them
    PlaceWithItems(ForUnitTestModBlockId.GearChainPole, new Vector3Int(0, 0, 5));
    PlaceWithItems(ForUnitTestModBlockId.GearChainPole, new Vector3Int(2, 0, 5));
    Assert.IsTrue(GearChainSystemUtil.TryConnect(new Vector3Int(0, 0, 5), new Vector3Int(2, 0, 5), PlayerId, GearChainPoleExtendTestHelper.ConnectToolGuid, out _));

    Assert.IsTrue(BlueprintCreateService.TryCreateFromArea("lines", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 6), out var blueprint));

    Assert.AreEqual(1, blueprint.Wires.Count);
    Assert.AreEqual(ElectricWireConnectToolGuid, blueprint.Wires[0].ConnectToolGuid);
    Assert.AreEqual(1, blueprint.Chains.Count);
    Assert.AreEqual(GearChainPoleExtendTestHelper.ConnectToolGuid, blueprint.Chains[0].ConnectToolGuid);
}
```

`PlaceWithItems` は `TryAddBlock` で置き、プレイヤーインベントリへ電線・チェーン素材を入れる（`ElectricWireSaveLoadTest` の素材投入と同じ手順）。`ElectricWireConnectToolGuid = c0000000-0000-0000-0000-000000000001`、`PlayerId = 0`。

既存テストの `new BlueprintJsonObject(name, blocks, guid)` は全て `new BlueprintJsonObject(name, blocks, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), guid)` へ直す（Files 節の追随一覧）。

- [ ] **Step 7: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Blueprint|SaveMigration"`
Expected: ErrorCount 0 / 全 PASS（`SaveMigrationChainConstructionTest` を含む）

- [ ] **Step 8: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintLineMessagePack.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPacketDto.cs moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint
git commit -m "feat: BPを外接箱最小角基準で保存し内部の電線・チェーンを記録する（セーブ版4→5移行付き）"
```

---

### Task 3: 貼り付け計算を「回転後の外接箱最小角＝原点」にし、要素へBP内indexを持たせる

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPlacementElement.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPasteCalculator.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintPasteCalculatorTest.cs`, `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Blueprint/BlueprintFootprintCalculatorTest.cs`

**Interfaces:**
- Produces:
  - `BlueprintPlacementElement(int blockIndex, Vector3Int position, BlockDirection direction, BlockId blockId, Dictionary<string, string> settings)` と `public readonly int BlockIndex;`
  - `BlueprintPasteCalculator.CalculatePlacements(BlueprintJsonObject blueprint, Vector3Int origin, int rotationStep)` — 戻り値の要素群の `MinPos` 成分最小が `origin` に一致（マスタ欠損ブロックは除外のまま）

- [ ] **Step 1: テストを書く**

```csharp
[Test]
public void 回転しても外接箱最小角が原点に一致するTest()
{
    new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
    var blueprint = new BlueprintJsonObject("r", new List<BlueprintBlockJsonObject>
    {
        new(new Vector3Int(0, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
        new(new Vector3Int(3, 1, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
    }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid());
    var origin = new Vector3Int(10, 5, 10);

    for (var rotation = 0; rotation < 4; rotation++)
    {
        var placements = BlueprintPasteCalculator.CalculatePlacements(blueprint, origin, rotation);
        var min = placements[0].Position;
        foreach (var placement in placements) min = Vector3Int.Min(min, BlueprintPlacementElementUtil.ToPositionInfo(placement).MinPos);
        Assert.AreEqual(origin, min, $"rotation={rotation}");
        Assert.AreEqual(new[] { 0, 1 }, placements.Select(p => p.BlockIndex).ToArray());
    }
}
```

- [ ] **Step 2: 実装**

`BlueprintPlacementElement` に `BlockIndex` を先頭引数で足す。`CalculatePlacements` は `for (var i = 0; i < blueprint.Blocks.Count; i++)` で回し、要素を作った後に平行移動する。

```csharp
// 回転後の外接箱最小角が原点に来るよう全要素を平行移動する
// Translate every element so the rotated extent's min corner lands on the origin
if (result.Count == 0) return result;
var rotatedMin = result[0].Position;
foreach (var element in result) rotatedMin = Vector3Int.Min(rotatedMin, element.Position);
var shift = origin - rotatedMin;
for (var i = 0; i < result.Count; i++)
{
    var element = result[i];
    result[i] = new BlueprintPlacementElement(element.BlockIndex, element.Position + shift, element.Direction, element.BlockId, element.Settings);
}
return result;
```

`CalcElement` 内の `pasteAnchor + newOrigin` は `newOrigin` だけにする（原点は最後の平行移動で載せる）。`element.Position` は回転後の各ブロックの MinPos なので成分最小＝外接箱最小角。

- [ ] **Step 3: 既存テストの期待値を新しい基準へ直す**

`BlueprintPasteCalculatorTest` の既存ケースは「アンカー相対」前提なので、期待位置を「最小角＝原点」へ書き直す。`BlueprintFootprintCalculatorTest` は寸法のみなので変わらないことを確認する。

- [ ] **Step 4: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPasteCalculator|BlueprintFootprint|BlueprintPasteRunBuilder"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint moorestech_server/Assets/Scripts/Tests/CombinedTest/Game moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint
git commit -m "feat: BP貼り付け計算を回転後の外接箱最小角を原点にそろえる形へ変える"
```

---

### Task 4: 共有の貼り付け判定 `BlueprintPastePlanner` を作る

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteLine.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteCopyPlan.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPastePlan.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/IBlueprintPasteWorld.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteCostCalculator.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPastePlanner.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Blueprint/BlueprintPastePlannerTest.cs`

**Interfaces:**
- Consumes: Task 2 の `BlueprintJsonObject.Wires/Chains`、Task 3 の `CalculatePlacements` と `BlueprintPlacementElement.BlockIndex`
- Produces:
  - `public enum BlueprintPasteLineKind { ElectricWire, GearChain }`
  - `public readonly struct BlueprintPasteLine { public readonly BlueprintPasteLineKind Kind; public readonly int ElementIndexA; public readonly int ElementIndexB; public readonly Vector3Int PositionA; public readonly Vector3Int PositionB; public readonly Guid ConnectToolGuid; public readonly IReadOnlyList<ConnectToolMaterialCost> Materials; }`
  - `public enum BlueprintPasteCopyState { Placeable, AllOverlapped, NotUnlocked, MaterialShortage }`
  - `public class BlueprintPasteCopyPlan { public Vector3Int Origin { get; } public IReadOnlyList<BlueprintPlacementElement> Elements { get; } public IReadOnlyList<bool> NonOverlapFlags { get; } public IReadOnlyList<BlueprintPasteLine> Lines { get; } public BlueprintPasteCopyState State { get; private set; } public void SetState(BlueprintPasteCopyState state); }`
  - `public class BlueprintPastePlan { public IReadOnlyList<BlueprintPasteCopyPlan> Copies { get; } public IReadOnlyList<(ItemId itemId, int held, int required)> ShortageRequirements { get; } }`（`ShortageRequirements` は最初に不足したBPまでの累計要求と所持。不足が無ければ空）
  - `public interface IBlueprintPasteWorld { bool IsOverlapping(BlockPositionInfo positionInfo); bool IsBlockUnlocked(Guid blockGuid); bool IsConnectToolUnlocked(Guid connectToolGuid); bool IsPaymentWaived { get; } }`
  - `public static class BlueprintPasteCostCalculator { public static List<(ItemId itemId, int count)> CalcRequiredItems(IReadOnlyList<BlueprintPasteCopyPlan> copies, ConstructionWalletQuery wallet); public static List<(ItemId itemId, int count)> CalcFullRequiredItems(BlueprintJsonObject blueprint); }`
  - `public static class BlueprintPastePlanner { public static BlueprintPastePlan Plan(BlueprintJsonObject blueprint, IReadOnlyList<Vector3Int> origins, int rotationStep, IBlueprintPasteWorld world, ConstructionWalletQuery wallet, IReadOnlyDictionary<ItemId, int> heldByItem); }`

- [ ] **Step 1: 型を書く**

`BlueprintPasteCopyPlan` は状態だけ可変（`SetState`）。`BlueprintPasteLine` は `Materials` を `ConnectToolCostCalculator.TryCalculate(connectToolGuid, distance, out materials)` の結果で持つ（距離は `Vector3Int.Distance(PositionA, PositionB)`。サーバーの `ElectricWireSystemUtil.TryConnect` / `GearChainSystemUtil.TryConnect` と同じ式）。

- [ ] **Step 2: 総素材計算を書く**

```csharp
public static List<(ItemId itemId, int count)> CalcRequiredItems(IReadOnlyList<BlueprintPasteCopyPlan> copies, ConstructionWalletQuery wallet)
{
    // ブロックは種類ごとのセル数を数え、財布の残りを考慮したコストセット数で払う
    // Count cells per block kind and pay the cost sets the wallet remainder leaves
    var cellCounts = new Dictionary<BlockId, int>();
    var lineMaterials = new List<ConnectToolMaterialCost>();
    foreach (var copy in copies)
    {
        for (var i = 0; i < copy.Elements.Count; i++)
        {
            if (!copy.NonOverlapFlags[i]) continue;
            cellCounts.TryGetValue(copy.Elements[i].BlockId, out var count);
            cellCounts[copy.Elements[i].BlockId] = count + 1;
        }
        foreach (var line in copy.Lines) lineMaterials.AddRange(line.Materials);
    }

    var required = new Dictionary<ItemId, int>();
    foreach (var (blockId, cellCount) in cellCounts)
    {
        var sets = wallet.GetRequiredCostSets(blockId, cellCount);
        foreach (var (itemId, count) in ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems)) Add(itemId, count * sets);
    }

    // 配線素材は既存の合算定義へ委ねる
    // Line materials go through the existing summation definition
    foreach (var (itemId, count) in ConstructionMaterialAccounting.SumRequiredByItem(lineMaterials, Array.Empty<ConnectToolMaterialCost>())) Add(itemId, count);
    return required.Select(kv => (kv.Key, kv.Value)).ToList();

    void Add(ItemId itemId, int count)
    {
        required.TryGetValue(itemId, out var current);
        required[itemId] = current + count;
    }
}
```

`CalcFullRequiredItems(blueprint)` はビルドメニュー用。財布の残りを見ず、ブロック種ごとのセル数 `n` を `ceil(n / PlacementsPerCost)` セット（`PlacementsPerCost` が財布を使わない値なら `n` セット。判定は `ConstructionWalletUtil.UsesWallet`）で払い、配線は各線の素材（距離は `CalculatePlacements(blueprint, Vector3Int.zero, 0)` の要素位置から）を足す。

- [ ] **Step 3: プランナーを書く**

```csharp
public static BlueprintPastePlan Plan(BlueprintJsonObject blueprint, IReadOnlyList<Vector3Int> origins, int rotationStep, IBlueprintPasteWorld world, ConstructionWalletQuery wallet, IReadOnlyDictionary<ItemId, int> heldByItem)
{
    var copies = origins.Select(BuildCopy).ToList();

    // 解放と重なりはBPごとに独立して決まる
    // Unlock and overlap are decided per copy independently
    foreach (var copy in copies)
    {
        if (copy.NonOverlapFlags.All(flag => !flag)) copy.SetState(BlueprintPasteCopyState.AllOverlapped);
        else if (!IsUnlocked(copy)) copy.SetState(BlueprintPasteCopyState.NotUnlocked);
    }

    // 素材は始点側から累計し、最初に足りなくなったBP以降を丸ごと不足にする（BP1個単位）
    // Accumulate materials from the run start; the first copy that falls short and every later one become shortages
    var shortageRequirements = new List<(ItemId, int, int)>();
    if (!world.IsPaymentWaived) MarkShortages();
    return new BlueprintPastePlan(copies, shortageRequirements);

    #region Internal

    BlueprintPasteCopyPlan BuildCopy(Vector3Int origin) { /* CalculatePlacements → 重なりフラグ（world.IsOverlapping(ToPositionInfo)）→ 両端が重ならない保存配線を BlueprintPasteLine へ解決（BlockIndex→要素indexの辞書で引く。index が欠けた線・素材を算出できない線は Debug.LogWarning で理由を出して捨てる） */ }

    bool IsUnlocked(BlueprintPasteCopyPlan copy)
    {
        foreach (var element in copy.Elements)
        {
            if (!world.IsBlockUnlocked(MasterHolder.BlockMaster.GetBlockMaster(element.BlockId).BlockGuid)) return false;
        }
        return copy.Lines.All(line => world.IsConnectToolUnlocked(line.ConnectToolGuid));
    }

    void MarkShortages()
    {
        var accepted = new List<BlueprintPasteCopyPlan>();
        var isShort = false;
        foreach (var copy in copies)
        {
            if (copy.State != BlueprintPasteCopyState.Placeable) continue;
            if (isShort) { copy.SetState(BlueprintPasteCopyState.MaterialShortage); continue; }

            accepted.Add(copy);
            var requirements = ConstructionMaterialAccounting.MatchRequirements(BlueprintPasteCostCalculator.CalcRequiredItems(accepted, wallet), heldByItem);
            if (requirements.All(r => r.required <= r.held)) continue;

            isShort = true;
            copy.SetState(BlueprintPasteCopyState.MaterialShortage);
            shortageRequirements.AddRange(requirements);
        }
    }

    #endregion
}
```

`BuildCopy` は上のコメントの手順を実コードで書く（このメソッドが30行を超えるならローカル関数 `ResolveLines(kind, lines)` へ分ける）。`ConstructionMaterialAccounting.MatchRequirements` が無ければ、クライアントの `ConstructionCostShortageCalculator.CalculateRequirements(IReadOnlyList<(ItemId,int)>, IReadOnlyDictionary<ItemId,int>)` と同じ突き合わせ（`(itemId, held, required)` を返す）を `Game.Construction/Materials/ConstructionMaterialAccounting.cs` に `public static List<(ItemId itemId, int held, int required)> MatchRequirements(IReadOnlyList<(ItemId itemId, int count)> required, IReadOnlyDictionary<ItemId, int> heldByItem)` として移し、クライアント側の同名処理はこれを呼ぶ形に置き換える（同じ算術を2か所に持たない）。

- [ ] **Step 4: テストを書く**

`BlueprintPastePlannerTest.cs` はテスト用 `IBlueprintPasteWorld`（重なり座標の集合・未解放 Guid 集合・`IsPaymentWaived` を持つ小クラス）を同ファイル内 private class で持つ。ケース:

```csharp
[Test] public void 素材が1BP分だけあれば列の1個目だけ置けるTest()   // origins 3個・所持=1BP分 → [Placeable, MaterialShortage, MaterialShortage]、ShortageRequirements に不足素材
[Test] public void 一部重なるBPは重ならない分の素材だけ要求するTest() // 1ブロック重ね → NonOverlapFlags にfalse1つ、要求数が1ブロック分減る
[Test] public void 全ブロック重なりはAllOverlappedTest()
[Test] public void 未解放ブロックを含むBPはNotUnlockedTest()
[Test] public void 片端が重なる配線は解決されないTest()                // 電柱2本と線1本、片方を重ねる → Lines.Count==0
[Test] public void 配線素材も総素材に入るTest()                         // 電線素材を0にするとMaterialShortage
[Test] public void 支払い免除なら素材が無くても置けるTest()
```

各ケースのBPは Task 3 のテストと同じ組み立て方（`ChestId`・`ElectricPoleId`）。財布は `new ConstructionWalletQuery(<残り0を返す IRemainingPlacementCountReader のテスト実装>)`。

- [ ] **Step 5: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPastePlanner|ConstructionCostShortage|ConstructionMaterialAccounting"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint moorestech_server/Assets/Scripts/Game.Construction moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs
git commit -m "feat: BP貼り付けの重なり・解放・総素材をBP1個単位で判定する共有プランナーを追加"
```

---

### Task 5: サーバーの `va:pasteBlueprint` で一括判定・設置・配線復元する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPasteProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteExecutor.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/ServerBlueprintPasteWorld.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/ConstructionWalletService.cs`（`GetQuery` を public へ）
- Modify: `Localization/localization.csv`, `moorestech_web/webui/src/features/notification/notificationMessages.ts`, `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`, `moorestech_web/webui/src/shared/i18n/generated/*`（再生成物）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest/BlueprintPasteProtocolTest.cs`

**Interfaces:**
- Consumes: Task 4 の `BlueprintPastePlanner.Plan`・`IBlueprintPasteWorld`
- Produces:
  - `BlueprintPasteProtocol.ProtocolTag = "va:pasteBlueprint"`、`BlueprintPasteProtocol.RequestMessagePack(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins)`（`[Key(2)] string BlueprintGuidStr`, `[Key(3)] int RotationStep`, `[Key(4)] List<Vector3IntMessagePack> Origins`）
  - `VanillaApiSendOnly.PasteBlueprint(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins)`
  - 通知キー `denied.blueprintPasteCostShortage`・`denied.blueprintPasteNotUnlocked`・`denied.blueprintPasteLineFailed`（引数 p0=件数）

- [ ] **Step 1: テストを書く**

`BlueprintPasteProtocolTest.cs`（`PlaceBlockProtocolTest` の DI 生成・インベントリ投入・`MessagePackSerializer.Serialize` → `packet.GetPacketResponse` の手順を流用）:

```csharp
[Test] public void 素材が足りればBPと保存配線が置かれ自動配線はされないTest()
// 電柱2本＋線1本のBPを登録 → 原点(10,0,10) で送信 → 2本とも存在・両者が互いに接続・それ以外の接続0・電線素材が線1本分減る
[Test] public void 素材が1つでも足りなければそのBPは何も置かれず通知されるTest()
// 建設素材を1BP分-1にして送る → ブロック0個・NotificationService へ denied.blueprintPasteCostShortage
[Test] public void 列は賄える個数までBP単位で置かれるTest()
// 原点3個・素材2BP分 → 2個分だけ置かれる
[Test] public void 重なるブロックは飛ばし残りと両端の残る配線だけ置くTest()
[Test] public void BP機能が未解放なら拒否するTest()
```

- [ ] **Step 2: サーバー側の世界実装**

```csharp
public class ServerBlueprintPasteWorld : IBlueprintPasteWorld
{
    private readonly PlacementTargetCatalog _catalog;
    private readonly IGameUnlockStateDataController _unlockState;
    public bool IsPaymentWaived { get; }

    public ServerBlueprintPasteWorld(PlacementTargetCatalog catalog, IGameUnlockStateDataController unlockState, bool isPaymentWaived)
    {
        _catalog = catalog;
        _unlockState = unlockState;
        IsPaymentWaived = isPaymentWaived;
    }

    public bool IsOverlapping(BlockPositionInfo positionInfo)
    {
        foreach (var position in positionInfo.EnumeratePositions())
        {
            if (ServerContext.WorldBlockDatastore.Exists(position)) return true;
        }
        return false;
    }

    public bool IsBlockUnlocked(Guid blockGuid) => _catalog.IsBlockUnlocked(blockGuid, _unlockState, IsPaymentWaived);
    public bool IsConnectToolUnlocked(Guid connectToolGuid) => IsPaymentWaived || ElectricWireSystemUtil.IsConnectToolUnlocked(connectToolGuid);
}
```

（`IsBlockUnlocked` の第3引数は `PlaceBlockProtocol` が無料設置で解放を見ない前例に合わせる。`IsPaymentWaived` は `DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)`。）

- [ ] **Step 3: 実行器**

```csharp
public static class BlueprintPasteExecutor
{
    // Placeable のBPだけを置き、置いたブロック同士の保存配線を張る。戻り値は張れなかった線の本数
    // Places only Placeable copies and redraws their saved lines; returns how many lines failed
    public static int Execute(BlueprintPastePlan plan, int playerId, ConstructionWalletService wallet, IOpenableInventory inventory, bool isPaymentWaived)
    {
        var failedLines = 0;
        foreach (var copy in plan.Copies)
        {
            if (copy.State != BlueprintPasteCopyState.Placeable) continue;
            for (var i = 0; i < copy.Elements.Count; i++)
            {
                if (copy.NonOverlapFlags[i]) PlaceElement(copy.Elements[i]);
            }
            foreach (var line in copy.Lines)
            {
                if (!ConnectLine(line)) failedLines++;
            }
        }
        wallet.FlushRemainingCountChanges();
        return failedLines;

        #region Internal

        void PlaceElement(BlueprintPlacementElement element)
        {
            // 自動配線はしない。財布は1セルずつ計画・確定する（判定は事前のプランで済んでいる）
            // No auto-connect; plan and commit the wallet per cell (affordability was settled by the plan)
            var createParams = BlueprintPlacementCreateParams.From(element.Settings);
            var placementPlan = wallet.PlanPlacement(element.BlockId, playerId);
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(element.BlockId, element.Position, element.Direction, createParams, out var block))
            {
                Debug.LogWarning($"[BlueprintPaste] TryAddBlock failed pos={element.Position} block={element.BlockId} player={playerId}");
                return;
            }
            if (!isPaymentWaived) wallet.CommitPlacement(placementPlan, inventory, block.BlockInstanceId);
        }

        bool ConnectLine(BlueprintPasteLine line)
        {
            var connected = line.Kind == BlueprintPasteLineKind.ElectricWire
                ? ElectricWireSystemUtil.TryConnect(line.PositionA, line.PositionB, playerId, line.ConnectToolGuid, out var wireReason)
                : GearChainSystemUtil.TryConnect(line.PositionA, line.PositionB, playerId, line.ConnectToolGuid, out var chainReason);
            if (!connected) Debug.LogWarning($"[BlueprintPaste] line restore failed kind={line.Kind} a={line.PositionA} b={line.PositionB} player={playerId}");
            return connected;
        }

        #endregion
    }
}
```

`BlueprintPlacementCreateParams.From` は `Dictionary<string,string>` の設定を UTF8 バイトの `BlockCreateParam[]` にする（クライアント `BlueprintPastePlaceSender.ToPlaceInfo` と同じ変換。null 設定は空）。同じ変換をクライアントも使うよう、この静的クラスを `Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPlacementCreateParams.cs` に置き、クライアントの `ToPlaceInfo` から呼ぶ（Util/Blueprint の .cs は9本）。

- [ ] **Step 4: プロトコル**

```csharp
public class BlueprintPasteProtocol : IPacketResponse
{
    public const string ProtocolTag = "va:pasteBlueprint";
    // （コンストラクタ: IBlueprintDatastore / IPlayerInventoryDataStore / IGameUnlockStateDataController / IGameUnlockStateData / NotificationService / ConstructionWalletService / PlacementTargetCatalog を serviceProvider から取る）

    public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
    {
        var request = MessagePackSerializer.Deserialize<RequestMessagePack>(payload);
        // BP機能未解放の変更操作は拒否する（ADR 0015）
        // Reject mutating operations while blueprints are locked (ADR 0015)
        if (!_gameUnlockStateData.IsBlueprintUnlocked) return Deny("denied.blueprint.NotUnlocked", "blueprint feature locked");
        var blueprint = _blueprintDatastore.Blueprints.FirstOrDefault(b => b.BlueprintGuidStr == request.BlueprintGuidStr);
        if (blueprint == null) return Deny("denied.blueprint.NotFound", $"blueprint {request.BlueprintGuidStr} not found");

        var isPaymentWaived = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
        var inventory = _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;
        var world = new ServerBlueprintPasteWorld(_placementTargetCatalog, _gameUnlockStateDataController, isPaymentWaived);
        var plan = BlueprintPastePlanner.Plan(blueprint, request.Origins.Select(o => o.Vector3Int).ToList(), request.RotationStep, world, _constructionWallet.GetQuery(requesterPlayerId), ConstructionMaterialAccounting.TallyHeld(inventory.InventoryItems));

        var failedLines = BlueprintPasteExecutor.Execute(plan, requesterPlayerId, _constructionWallet, inventory, isPaymentWaived);
        NotifyRejections(plan, failedLines);
        return null;
    }
}
```

`NotifyRejections` は `MaterialShortage` の個数 > 0 で `denied.blueprintPasteCostShortage`、`NotUnlocked` の個数 > 0 で `denied.blueprintPasteNotUnlocked`、`failedLines > 0` で `denied.blueprintPasteLineFailed` を `_notificationService.Notify(playerId, NotificationMessagePack.CreateOperationDenied(key, new[] { count.ToString() }))` で出し、同時に `Debug.Log($"[BlueprintPaste] rejected {state} copies={count} player={playerId}")` を出す。`Deny` は `Debug.LogWarning` を出して null を返す。`denied.blueprint.NotFound` / `denied.blueprint.NotUnlocked` の辞書行が無ければ足す（既存の `denied.blueprint.NotUnlocked` は再利用）。`PacketResponseCreator` に `_packetResponseDictionary.Add(BlueprintPasteProtocol.ProtocolTag, new BlueprintPasteProtocol(serviceProvider));` を足す。`VanillaApiSendOnly.PasteBlueprint` は `PlaceBlock` と同じ送信形で書く。

- [ ] **Step 5: 辞書と通知IDを足して再生成**

`Localization/localization.csv` に3行（英日独韓）を足す。例:
`ui.notification.blueprintPasteCostShortage,{p0} blueprints were not placed: not enough materials,{p0} blueprints were not placed: not enough materials,素材が足りないためブループリント{p0}個を設置しませんでした,{p0} Blaupausen wurden nicht gesetzt: zu wenig Materialien,재료가 부족해 블루프린트 {p0}개를 설치하지 않았습니다`
（NotUnlocked:「未解放のブロックまたは線を含むためブループリント{p0}個を設置しませんでした」、LineFailed:「配線{p0}本を復元できませんでした」）。`notificationMessages.ts` に `["denied.blueprintPasteCostShortage", L.ui.notification.blueprintPasteCostShortage]` 等3行を足し、`_CompileRequester.cs` の印を更新、`cd moorestech_web/webui && npm run gen:i18n`、`npm test -- notification` で coverage テストが通ることを確認する。

- [ ] **Step 6: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPasteProtocol|BlueprintProtocol|PlaceBlockProtocol"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs Localization/localization.csv moorestech_web/webui/src
git commit -m "feat: va:pasteBlueprint でBPをBP単位の一括判定のうえ設置し保存配線を復元する"
```

---

### Task 6: クライアントの置き位置（地面中心・側面接し・地形最高点）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilder.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolverTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilderTest.cs`

**Interfaces:**
- Consumes: `PlaceSystemUtil.TryRaycastPlacementSurface`（internal・同アセンブリ）、`PlaceSystemUtil.CalcPlacePointBySize`、`GroundHeightQuantization.StepOf`、`PlacementGroundCellResolver.TryResolveCellFromGround`
- Produces:
  - `public static class BlueprintPasteOriginResolver { public static bool TryResolveCursorOrigin(Camera camera, Vector3Int footprintSize, int heightOffset, out Vector3Int origin, out PlacementHitSurfaceKind surfaceKind); public static Vector3Int ResolveOrigin(Vector3Int footprintSize, Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset); public static bool TryFollowGround(Vector3Int origin, Vector3Int footprintSize, int heightOffset, out Vector3Int resolved); }`
  - `BlueprintPasteRunBuilder.BuildOrigins(Vector3Int startOrigin, Vector3Int cursorOrigin, Vector3Int footprintSize, PlacementHitSurfaceKind surfaceKind, int heightOffset) : List<(Vector3Int origin, bool groundFound)>`

- [ ] **Step 1: テストを書く**

```csharp
[Test]
public void 側面ヒットは面に接し縦は最下段がカーソル段になるTest()
{
    // +X面(x=5)の高さ32.4にヒット。外接箱 3x4x2 → x原点=5、yはカーソル段32、zはカーソル中心
    // Hit the +X face (x=5) at y=32.4 with a 3x4x2 extent → x origin 5, y at the cursor level 32, z centered
    var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(3, 4, 2), new Vector3(5f, 32.4f, 10.3f), PreviewSurfaceType.YZ_X, 0f, 0);
    Assert.AreEqual(new Vector3Int(5, 32, 9), origin);
}

[Test]
public void 側面ヒットのQEは縦へ足されるTest()
{
    var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(3, 4, 2), new Vector3(5f, 32.4f, 10.3f), PreviewSurfaceType.YZ_X, 0f, 2);
    Assert.AreEqual(34, origin.y);
}

[Test]
public void 地面ヒットはXZがカーソル中心Test()
{
    var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(4, 2, 3), new Vector3(10.2f, 32f, 10.7f), null, 0f, 0);
    Assert.AreEqual(new Vector3Int(8, 32, 9), origin);
}

[Test]
public void 天面ヒットは通常どおり上に乗るTest()
{
    var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(2, 3, 2), new Vector3(4.2f, 33f, 4.2f), PreviewSurfaceType.XZ_Y, 0f, 0);
    Assert.AreEqual(33, origin.y);
}
```

（地面の期待値は `CalcPlacePointBySize` の既存式: 偶数幅は +0.5 して floor、`- size/2`。実装前に `PlaceSystemUtil.CalcPlacePointBySize` で手計算して期待値を確かめ、違えばテストの期待値を式に合わせて直す。）

- [ ] **Step 2: 実装**

```csharp
public static Vector3Int ResolveOrigin(Vector3Int footprintSize, Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset)
{
    // 外接箱を1個の大きなブロックとみなし通常設置と同じセル解決を使う
    // Treat the extent as one large block and reuse the normal placement cell rule
    var origin = PlaceSystemUtil.CalcPlacePointBySize(footprintSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
    if (!IsSideFace(surfaceType)) return origin;

    // 側面だけは縦を中心寄せせず、最下段をカーソルのある段へそろえる（ADR 0077 決定3）
    // Only side faces skip vertical centering and put the bottom at the cursor's level (ADR 0077 decision 3)
    return new Vector3Int(origin.x, Mathf.FloorToInt(hitPoint.y) + heightOffset, origin.z);

    bool IsSideFace(PreviewSurfaceType? type) => type is PreviewSurfaceType.YX_Origin or PreviewSurfaceType.YX_Z or PreviewSurfaceType.YZ_Origin or PreviewSurfaceType.YZ_X;
}

public static bool TryResolveCursorOrigin(Camera camera, Vector3Int footprintSize, int heightOffset, out Vector3Int origin, out PlacementHitSurfaceKind surfaceKind)
{
    origin = default;
    surfaceKind = PlacementHitSurfaceKind.Ground;
    if (!PlaceSystemUtil.TryRaycastPlacementSurface(camera, out var hit, out var surface)) return false;

    surfaceKind = surface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace;
    var step = surface == null ? GroundHeightQuantization.StepOf(hit.collider) : 0f;
    origin = ResolveOrigin(footprintSize, hit.point, surface == null ? null : surface.PreviewSurfaceType, step, heightOffset);
    return true;
}

// 地面ヒットのBPは外接箱底面フットプリントの地形最高点へ底を合わせる（ADR 0047）
// A ground-hit blueprint puts its bottom at the terrain max over the extent's footprint (ADR 0047)
public static bool TryFollowGround(Vector3Int origin, Vector3Int footprintSize, int heightOffset, out Vector3Int resolved)
{
    return PlacementGroundCellResolver.TryResolveCellFromGround(origin, BlockDirection.North, footprintSize, heightOffset, out resolved);
}
```

`BlueprintPasteRunBuilder.BuildOrigins` は `PlacementRunPositionCalculator.Calculate(startOrigin, cursorOrigin, footprintSize).Positions` を回し、`surfaceKind == Ground` かつ列軸が Y でなければ各原点へ `TryFollowGround` を適用し、取れなければ `groundFound=false` を返す（呼び出し側がそのBPを赤にし `feedback.AddGroundNotFound()` を出す）。既存の `Build`（フラットな要素列を返す版）は削除し、テストを `BuildOrigins` へ書き換える。

- [ ] **Step 3: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPasteOriginResolver|BlueprintPasteRunBuilder"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste
git commit -m "feat: BPの置き位置を外接箱で解決し側面は面に接して最下段をカーソル段にそろえる"
```

---

### Task 7: 貼り付けシステムをプランナー・新プロトコル・配線ゴーストへつなぐ

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/ClientBlueprintPasteWorld.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteLinePreview.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePlaceSender.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOverlapReasonReporter.cs`（`BlueprintPasteFeedbackReporter` へ改名し責務を拡張）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/BlueprintPasteOverlapReasonReporterTest.cs`（→ `Paste/BlueprintPasteFeedbackReporterTest.cs`）

**Interfaces:**
- Consumes: Task 4 `BlueprintPastePlanner.Plan`、Task 5 `VanillaApiSendOnly.PasteBlueprint`、Task 6 `BlueprintPasteOriginResolver` と `BuildOrigins`
- Produces:
  - `ClientBlueprintPasteWorld(BlockGameObjectDataStore, PlacementTargetResolver)`（`IsPaymentWaived` は `DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)`。`IsConnectToolUnlocked` は `PlacementTargetResolver` 経由で接続ツールの解放を引く。該当メソッドが無ければ `PlacementTargetResolver.IsConnectToolUnlocked(Guid)` を `IsBlockUnlocked` と同形で足す）
  - `BlueprintPastePreviewController.UpdatePreview(BlueprintPastePlan plan) : IReadOnlyList<IReadOnlyList<BlockPreviewObject>>`（BPごと・要素ごとのゴースト。重なり要素は赤、BPが Placeable 以外なら全要素赤）
  - `BlueprintPasteLinePreview.Show(BlueprintPastePlan plan, IReadOnlyList<IReadOnlyList<BlockPreviewObject>> ghosts)` / `Hide()`
  - `BlueprintPastePlaceSender.Send(Guid blueprintGuid, int rotationStep, BlueprintPastePlan plan)`
  - `BlueprintPasteFeedbackReporter.Report(BlueprintPastePlan plan, PlacementFeedback feedback)`

- [ ] **Step 1: 報告器のテストを書き換える**

```csharp
[Test] public void 全ブロック重なりは既存ブロック理由を出すTest()       // 全BPが AllOverlapped → AddBlockedByExistingBlock 行
[Test] public void 素材不足のBPがあれば不足素材行を出すTest()          // ShortageRequirements に (鉄板,2,5) → PlaceMaterialShortage 行・params "2","5"
[Test] public void すべて置けるなら行を出さないTest()
```

- [ ] **Step 2: 実装**

`BlueprintPasteSystem.ManualUpdate` の流れ（ローカル関数で200行未満に保つ）:

```csharp
// 外接箱寸法で原点を解決し、列の原点を作り、共有プランナーで判定する
// Resolve origins from the extent, build the run, and judge it with the shared planner
if (!BlueprintPasteOriginResolver.TryResolveCursorOrigin(_mainCamera, _footprintSize, _heightOffset.Value, out var cursorOrigin, out var surfaceKind)) { HideAll(); return; }
if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(cursorOrigin, surfaceKind);
var runOrigins = BlueprintPasteRunBuilder.BuildOrigins(_dragState.ResolveDragStartCell(cursorOrigin), cursorOrigin, _footprintSize, _dragState.ResolveSurfaceKind(surfaceKind), _heightOffset.Value);
var plan = BlueprintPastePlanner.Plan(_currentBlueprint, runOrigins.Where(o => o.groundFound).Select(o => o.origin).ToList(), _rotationStep, _pasteWorld, _walletQuery, ConstructionMaterialAccounting.TallyHeld(_localPlayerInventory));
var ghosts = _previewController.UpdatePreview(plan);
_linePreview.Show(plan, ghosts);
BlueprintPasteFeedbackReporter.Report(plan, feedback);
if (runOrigins.Any(o => !o.groundFound)) feedback.AddGroundNotFound();
```

距離判定（`IsPlaceableFromPlayer(cursorOrigin)`）は既存どおり残す。送信は既存の `_dragState.TryConsumeSendableRelease` の後で `BlueprintPastePlaceSender.Send(_currentBlueprintGuid, _rotationStep, plan)`。`Rotate()` と `ResolveBlueprint` の `_footprintSize` 計算は既存を流用。コンストラクタに `ConstructionWalletQuery`・`ILocalPlayerInventory`・`PlacementTargetResolver` を足す（VContainer の自動解決。`MainGameInteractionRegistration.cs:113` の登録は型登録なので変更不要。`ClientBlueprintPasteWorld` はコンストラクタ内で new）。

`BlueprintPastePlaceSender.Send`:

```csharp
public static void Send(Guid blueprintGuid, int rotationStep, BlueprintPastePlan plan)
{
    // 置けるBPだけの原点を送る。サーバーが同じプランナーで再判定する
    // Send only placeable copies' origins; the server re-judges with the same planner
    var origins = plan.Copies.Where(c => c.State == BlueprintPasteCopyState.Placeable).Select(c => c.Origin).ToList();
    if (origins.Count == 0)
    {
        Debug.Log("[BlueprintPaste] release skipped: no placeable blueprint copies");
        return;
    }

    ClientContext.VanillaApi.SendOnly.PasteBlueprint(blueprintGuid, rotationStep, origins);

    // Undo は置く予定のブロックを記録する（既存のBP貼り付けと同じ粒度）
    // Undo records the blocks expected to be placed, at the same granularity as before
    var placeInfos = plan.Copies.Where(c => c.State == BlueprintPasteCopyState.Placeable).SelectMany(ToPlaceInfos).ToList();
    var record = PlaceOperationRecord.CreateFrom(placeInfos);
    if (record.HasCells) ClientDIContext.BuildOperationHistory.Push(record);
    SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
}
```

`ToPlaceInfos(copy)` は NonOverlapFlags が true の要素を `PlaceInfo`（`CreateParams = BlueprintPlacementCreateParams.From(element.Settings)`）にする。

`BlueprintPasteLinePreview` は `AutoConnectWirePreviewRenderer` の `WireLine`（カテナリー・半透明・可否色）を電線に、`GearChainPoleExtendPreviewObject` の2本 `LineRenderer` 表現をチェーンに流用する。端点は電線が `ElectricWireEndpointResolver.ResolveFromGhost(ghost, placeInfo, master)`、チェーンが `GearChainPoleExtendPreviewCalculator.GetPoleCenter(position)`。色は BPの State が Placeable なら可色、それ以外は不可色。線オブジェクトはプールして毎フレーム作り直さない。`WireLine` が private なら、`AutoConnectWirePreviewRenderer` から `PreviewWireLine`（同ディレクトリの新規 public クラス）へ取り出して両方から使う。

- [ ] **Step 3: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPaste|AutoConnectWirePreview"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint
git commit -m "feat: BP貼り付けプレビューをBP単位の青赤・不足tooltip・配線ゴーストにし新プロトコルで送る"
```

---

### Task 8: ビルドメニューにBP全体の必要素材を出す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/BlueprintPlacementTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/PlacementTargetFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/PlacementTargetResolver.cs`
- Modify（呼び出し側追随）: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetFactoryTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/UIState/Models/BuildMenuSelectionTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/PlacementHeightOffsetSingleSourceTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Shared/PlaceSystemStateControllerHeightResetTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`

**Interfaces:**
- Consumes: Task 4 `BlueprintPasteCostCalculator.CalcFullRequiredItems(BlueprintJsonObject)`
- Produces: `BlueprintPlacementTarget(Guid blueprintGuid, string displayName, IReadOnlyList<(Guid itemGuid, int count)> requiredItems)`、`PlacementTargetFactory.Create(PlacementTargetEntry entry, ClientBlueprintLibrary blueprintLibrary)`

- [ ] **Step 1: テストを書く**

`BuildMenuEntryDtoFactoryTest` に、チェスト2個のBPを `ClientBlueprintLibrary` へ載せた状態で BP エントリの `RequiredItems` がチェスト1個分の必要素材×2（所持 0 なら `Lacking=true`）になるケースを足す（既存 157 行目の BP ケースを書き換える）。

- [ ] **Step 2: 実装**

`PlacementTargetFactory.Create` に `ClientBlueprintLibrary` を足し、BP の場合:

```csharp
case PlacementTargetKind.Blueprint:
    // BP全体（全ブロック＋保存配線）の必要素材をビルドメニューへ渡す
    // Hand the build menu the whole blueprint's materials (all blocks plus saved lines)
    return new BlueprintPlacementTarget(entry.Id, entry.MasterDisplayName, ResolveBlueprintRequiredItems(entry.Id, blueprintLibrary));
```

`ResolveBlueprintRequiredItems` は `blueprintLibrary.TryGetBlueprint(id, out var blueprint)` が false なら `Debug.Log` で理由を出して空配列、true なら `BlueprintPasteCostCalculator.CalcFullRequiredItems(blueprint)` を `(MasterHolder.ItemMaster.GetItemMaster(itemId).ItemGuid, count)` へ写す。`PlacementTargetResolver` の2か所の `PlacementTargetFactory.Create(entry)` を `Create(entry, _blueprintLibrary)` にする。テストの `new BlueprintPlacementTarget(guid, "x")` は `new BlueprintPlacementTarget(guid, "x", Array.Empty<(Guid, int)>())` へ直す。

- [ ] **Step 3: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildMenu|PlacementTargetFactory|PlacementHeightOffset|PlaceSystemStateController"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat: ビルドメニューのBPエントリにBP全体の必要素材と不足を出す"
```

---

### Task 9: unityプレイ録画テストで通し検証する

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-place-like-block.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-copy-paste-via-ui.cs`（貼り付けの期待位置を新しい原点規則へ直す）

- [ ] **Step 1: シナリオを書く**

`blueprint-copy-paste-via-ui.cs` の準備を写し、次を順に確かめる（各段でスクリーンショット）:
1. 電柱2本＋機械1台を電線でつないだ範囲と、空気の余白を含む大きめの範囲をコピーする。
2. 地面へ向ける → ゴーストの外接箱XZ中心がカーソルセル、底が地形最高点セル（ゴーストの最小座標をアサート）。
3. 既存チェストの +X 面へ向ける → 外接箱の最小 x が面の x、最小 y がカーソル段（めり込まない）。
4. E を2回 → 最小 y が +2。
5. 配線ゴースト（`AutoConnectWire` 系の線オブジェクト）が表示されている。
6. 素材を持たせずに貼る（`FreeBlockPlacement` OFF）→ ゴーストが不可色・クリックしても置かれない・tooltip に不足行。
7. 素材を持たせて貼る → ブロックと電線がサーバーに出る（`p.ServerService<IWorldBlockDatastore>()` で確認し、電柱同士が接続済み）。
8. ドラッグで3個並べる・素材は2個分 → 2個だけ置かれる。
9. 大きいBPで60フレーム静止 → Task 1 の計測で点滅が無い（スクリーンショット6枚でゴーストが全て出ている）。

- [ ] **Step 2: 実行**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-place-like-block.cs` と既存 `blueprint-copy-paste-via-ui.cs`
Expected: result.json の全アサート PASS。録画を `share-put` で共有できる場所へ置き、PR 本文に載せる。

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building
git commit -m "test: BPの通常ブロック同等の設置・配線復元・素材不足の録画シナリオを追加"
```

---

### Task 10: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] moores-code-review を全ブランチ差分に対して実行し、確定指摘を反映する。判定経路・条件式に触れる反映があれば Task 9 のunityプレイ録画テストを反映後のバイナリで再実施する。

### Task 11: セッション終了可能状態にすること

- [ ] pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR未作成のまま終わるのはplan未完了である。

---

## 判断記録（ADR）

設計裁定の正本: [ADR 0077](../../adr/0077-blueprint-placement-as-one-large-block-with-wiring.md)（決定1〜9・6b はユーザー裁定。`.decisions/2026-10-09-BP*.md` 9件）。

planning 中の判断:

- D1 判定ロジックの置き場を `Server.Protocol/PacketResponse/Util/Blueprint/` の共有純ロジック1本にする。クライアントのプレビュー色・tooltip とサーバーの拒否が同じ定義から出るため、表示と結果が食い違わない。クライアントは既に `Server.Protocol`（`PlaceInfo` 等）を参照している。出所: agent前提（ADR 0076「範囲内ブロック数はサーバーと同規則」・`.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md` の判断集約）
- D2 BP貼り付けは既存の `va:placeBlock` ではなく新プロトコル `va:pasteBlueprint` にする。BP単位の一括拒否（ADR 0077 決定6・6b）はブロック単位で払える分だけ置く `PlaceBlockProtocol` では表せず、Undo 復元の前例（`PlaceBlock(NoAutoConnect)`＋線ごとの `Restore*` 送信）では線の素材を一括判定できない。出所: agent前提（既存 `PlaceBlockProtocol` の部分成功と `VanillaRemovalRestoreSender` の分割送信の比較）
- D3 設置の実処理は `PlaceBlockProtocol` から共通部分を抽出せず、`BlueprintPasteExecutor` が `ConstructionWalletService.PlanPlacement/CommitPlacement`＋`TryAddBlock` を直接呼ぶ。並行の `fix/free-placement-single-path`（`.decisions/2026-10-09-無料設置は支払いだけを免除し設置経路を通常と一本化する.md`）が `PlaceBlockProtocol` を書き換え中で、抽出はマージ衝突と二重作業になるため。その PR のマージ後に、無料設置の「支払いだけ免除」の窓口が入っていれば `BlueprintPasteExecutor` の `isPaymentWaived` 分岐をその窓口へ寄せる（Task 11 のコンフリクト解消時に確認）。出所: agent前提
- D4 配線の復元は `ElectricWireSystemUtil.TryConnect` / `GearChainSystemUtil.TryConnect` を使う（範囲・素材の再検証と消費を既存の唯一の定義に任せる）。無料設置中は現状これらが素材を消費する。並行 PR が「電線ツールも無料（コスト0で記録）」を入れたらそれに従う。出所: agent前提（`.decisions/2026-10-09-無料設置中は電線ツールの手動配線も素材不要にする.md` との整合）
- D5 歯車チェーンの接続一覧は `IGearChainPole` に公開されていないため、作成時は BP内の後方ブロックとの `TryGetChainConnectionRecord` 総当たりで拾う（インターフェースを広げない。BP内のチェーンポール数は小さい）。出所: agent前提
- D6 未解放ブロック・未解放線種を含むBPは「置けない」側（BP丸ごと拒否・赤）に寄せる。ADR 0077 決定6の原子性（中途半端な工場を作らない）と同じ理由。出所: agent前提
- D7 ビルドメニューの必要数は財布の残りを見ない「BP全体を一から置く量」にする。ビルドメニューの DTO は対象ごとに静的な `CreateRequiredItems` で作られ、残りを含めた実際の支払いは貼り付け時の tooltip が正確に出すため。出所: agent前提（ADR 0041 の Lacking 判定の仕組みはそのまま使う）
- D8 地面ヒットで地形が取れない列のBPは赤にして送らず、既存の `AddGroundNotFound` 行を出す（通常設置の `PlacementGroundFollowStep` と同じ扱い）。出所: agent前提（前例 `PlacementGroundFollowStep`）
- D9 セーブ版 4→5 は他の進行中PRが同じ版番号を使うと起動時に `SaveMigrationChain` が例外で落ちる。Task 11 で master をマージする際に `CurrentVersion` と `FromVersion` の欠番・重複を必ず確認し、衝突していればこちらの版を繰り上げる。出所: agent前提（AGENTS.md セーブ形式の例外規定）
- D10 ちらつきは原因未実測のため、Task 1 は計測→仮説確定時のみ修正、外れたら観測値を報告して止まる。出所: ADR 0077 決定9（ユーザー裁定「直し方は実測で原因を確定してから決める」）
- D11 unityプレイ録画テストを Task 9 に含める（設置位置・入力・プレビュー表示・サーバー反映の通し確認が必要なランタイム挙動のため）。
