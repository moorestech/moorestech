# BPを通常ブロックと同じ体験で置き、配線ごと貼り付ける Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ブループリント（BP）を外接箱1個の大きなブロックとして通常ブロックと同じ規則で置けるようにし、内部の電線・歯車チェーンを保存・復元し、素材不足をプレビューとビルドメニューに出し、大きいBPのちらつきを直す。

**Architecture:** 保存形式はオフセット基準を外接箱最小角へ正規化し配線（wires/chains）を足す（版4→5の移行付き）。貼り付けの判定（地形・重なり・解放・総素材・配線解決）はサーバーとクライアントが共有する純ロジック `BlueprintPastePlanner`（`Server.Protocol/PacketResponse/Util/Blueprint/Planning/`）に一本化し、状態を確定してから不変の結果を作る。クライアントはプレビュー色・ツールチップに、サーバーは既存 `va:blueprint` に足す `Paste` 操作の一括判定と実行に使う。1セルの設置（財布の計画→追加→支払い確定・無料設置は支払いだけ免除）は `PlaceBlockProtocol` と共用する実行器へ抽出する。置き位置はクライアントが通常設置のセル解決 `PlaceSystemUtil.CalcPlacePointBySize` を外接箱寸法で呼び、側面だけ縦を最下段揃えに直し、地面では ADR 0047 の地形最高点で決める。

**Tech Stack:** Unity C# / NUnit / MessagePack / Newtonsoft.Json / UniRx / VContainer、Web UI（TypeScript・通知表と辞書の再生成のみ）

## Requirements

設計ADR: [docs/adr/0077-blueprint-placement-as-one-large-block-with-wiring.md](../../adr/0077-blueprint-placement-as-one-large-block-with-wiring.md)

- R1 保存: BPの各ブロックのオフセットは外接箱最小角（全ブロック `OriginalPos` の成分最小）基準。受け入れ: 作成したBPのオフセット成分最小が (0,0,0)。
- R2 移行: 版4のセーブのBPが版5へ変換され、オフセットが成分最小0へ平行移動し `wires`/`chains` が空配列で付く。変換できない形（blocks 要素が JObject でない・offset 欠損）は `Failed(reason)`。受け入れ: 移行テストが通り、本番・テスト双方の `SaveMigrationChain` 構築で例外が出ない。
- R3 配線保存: 作成時、両端ともコピー対象内の電線・歯車チェーン接続を BP内ブロック index 2つ＋線種Guid で保存する（同じペアは1本）。外へ伸びる接続は保存しない。受け入れ: 電柱2本＋チェーンの配線つき範囲を作成すると wires/chains が期待本数。
- R4 置き位置（地面）: 外接箱をXZでカーソル中心に置き、底は外接箱底面フットプリントの地形最高点を含むセル（ADR 0047）＋Q/E。地形が取れないBPは `GroundNotFound` 状態（赤・送らない・既存の理由行）。受け入れ: 置き位置純ロジックテスト・プランナーテスト・録画。
- R5 置き位置（側面）: 既存ブロックの側面ヒットで外接箱が面の外側へ接し、面に平行な横はカーソル中心、縦は最下段＝カーソル段＋Q/E。上下面は通常どおり。受け入れ: 純ロジックテスト・録画で既存ブロックにめり込まない。
- R6 回転: R で回転しても外接箱最小角が原点に一致する（多セルブロックを含む）。受け入れ: 計算テスト。
- R7 自動配線なし: BP貼り付けで置いた電気ブロックは周辺自動配線をしない。受け入れ: プロトコルテストで保存配線以外の線が無い。
- R8 配線復元: 両端が置けた保存配線を復元し素材を消費する（回転後も正しい端点）。片端が重なりで置けなければその線は張らない。受け入れ: プロトコルテスト（回転0・1）。
- R9 素材不足（単体）: 重ならず置けるブロック分＋復元配線分の総素材が1つでも足りなければ、そのBPのゴースト全体が赤・クリックで何も置かない・不足素材をカーソルtooltip（`名前 所持/必要`）へ。サーバーも同じ判定でそのBPを丸ごと拒否し、ログ＋通知を出す。受け入れ: プロトコルテスト・録画。
- R10 素材不足（列）: ドラッグ列では始点側から賄えるBPの個数までを丸ごと青、以降のBPは丸ごと赤で置かない。受け入れ: プランナーテスト・プロトコルテスト。
- R11 重なり: 既存ブロックと重なるブロックは飛ばし残りを置く（現行維持）。全ブロック重なりなら既存の理由行。受け入れ: プロトコルテスト。
- R12 配線ゴースト: 復元予定の電線・チェーンをブロックゴーストと同じ青/赤で線表示する。受け入れ: 録画で線が見える。
- R13 ビルドメニュー: BPエントリの必要素材は、貼り付けと同じ財布込みの計算（回転0・重なりなしのBP1個）で出す。所持/不足表示は ADR 0041 の仕組みに乗り、財布の残りで賄える分は不足にしない。無料設置中は支払い免除。受け入れ: `BuildMenuEntryDtoFactoryTest` 追加ケース。
- R14 ちらつき: 大きいBPの貼り付けプレビューが点滅しない。原因は実測で確定してから直す。受け入れ: 録画の連続フレームでゴースト表示が途切れない・計測ログ。
- R15 解放: 未解放ブロック・未解放の線種を含むBPは置かない（サーバーはBP丸ごと拒否＋通知、クライアントは赤）。線種の解放は無料設置でも免除しない。BP機能未解放なら `va:blueprint` が拒否する（ADR 0015）。
- R16 設置経路の一本化: 1セル設置（財布の計画→`TryAddBlock`→支払い確定、無料設置は支払いだけ免除）は `PlaceBlockProtocol` とBP貼り付けが同じ実行器を使う。受け入れ: 既存 `PlaceBlockProtocolTest` 系が無変更で通る。
- R17 リクエスト検証: Paste の `RotationStep` が 0〜3 以外、`Origins` が空または上限超過なら、ログを出して `InvalidRequest` で拒否する。受け入れ: プロトコルテスト。

やらないこと:
- 範囲選択（コピー側）の表示を外接箱へ縮めること（ADR 0077 棄却案C）。
- BP外の既存電力網・チェーンへの自動接続（ADR 0077 決定5）。
- 複数BP間（列の隣同士）の配線。保存配線は各BPの内部だけを復元する。
- 判断記録末尾「本PR外のリファクタ提案（第3バケツ）」に挙げた既存重複の解消（本PRで解消と注記したものを除く）。

## Global Constraints

- 1ファイル200行未満。超えるなら責務で分割。partial 禁止。
- 1ディレクトリの新規コードは10ファイルまで（超えるならサブディレクトリ）。
- `Func<>` 禁止。デフォルト引数禁止（引数追加時は全呼び出し側を変更）。
- イベント発火は UniRx。単純な getter/setter プロパティ禁止（`{ get; }` と `{ get; private set; }` は可）。
- コメントは主要処理に「// 日本語 → // English」の2行セット（各1行）。`#region Internal` はメソッド内ローカル関数をまとめる用途のみ。
- try-catch 禁止（外部境界のみ）。fail-closed の拒否・縮退は理由を必ずログへ出す（無音禁止）。
- null チェックは外部データ・非同期ロード結果にのみ。
- サーバーのゲームロジックで実時間APIを使わない。
- セーブ形式変更は `WorldSaveAllInfo.CurrentVersion` を1上げ、`ISaveMigrationStep` を足して本番 `SaveAndEventServiceRegistration` とテスト側2か所（`SaveLoadPreparerTestFixture`・`SaveMigrationChainTest.CurrentVersionChain`）の `SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] {...})` へ登録し、旧版変換テストを同梱する（AGENTS.md・moorestech-save-migration スキル）。
- `.meta` を手で作らない。Prefab/Scene を手で編集しない。
- .cs を書いたら必ず `uloop compile --project-path ./moorestech_client`。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。
- スキーマ・JSON の optional 化やフォールバック補完は使わない（BPの `wires`/`chains` は必須）。
- 辞書行追加後は `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の再生成印を更新し、`moorestech_web/webui` で `npm run gen:i18n` を実行して生成物をコミットする（前例 9d87bcecc6・22fc0659be）。
- 通知キーは既存 `denied.blueprint.<BlueprintFailureReason>` の形にそろえる。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 判定共有ロジックは Server.Protocol Util（評価器の前例と同層）、Paste は既存 va:blueprint の操作（1プロトコル1ドメインの前例）、設置1セル実行器は PlaceBlockProtocol と共用（D3）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強3・弱4・第3バケツ7 / 強は不変化・GroundNotFound状態・財布込みビルドメニューで解消、弱②③④採用

---

## File Structure

| ファイル | 責務 |
|---|---|
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintJsonObject.cs`（変更） | BP本体・ブロック・配線行（`BlueprintLineJsonObject`）の保存型 |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs`（変更） | 最小角アンカーでの作成。配線収集は下へ委譲 |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintLineCollector.cs`（新規） | コピー対象内の電線・チェーン接続を index 化して返す |
| `moorestech_server/Assets/Scripts/Game.Blueprint/Game.Blueprint.asmdef`（変更） | `Game.EnergySystem` 参照を追加 |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPlacementElement.cs`（変更） | `BlockIndex` を追加 |
| `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPasteCalculator.cs`（変更） | 回転後の外接箱最小角を原点へ平行移動 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV4ToV5.cs`（新規） | BPオフセットの正規化と配線リスト追加 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（変更） | `CurrentVersion = 5` |
| `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs`（変更） | 移行ステップ登録（本番） |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs`・`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`（変更） | 移行ステップ登録（テスト側の本番同構成連鎖） |
| `moorestech_server/Assets/Scripts/Game.Construction/Materials/ConstructionMaterialAccounting.cs`（変更） | `MatchRequirements`（要求と所持の突き合わせ）の唯一の定義 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs`（変更） | 突き合わせを `MatchRequirements` へ委譲 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BlockCellPlacementExecutor.cs`（新規） | 1セル設置（財布の計画→追加→支払い確定・無料は支払いだけ免除）の唯一の実行器 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BlockCellPlacement.cs`（新規） | 1セル分の計画（支払い可否・消費素材・財布計画） |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`（変更） | 1セル設置を実行器へ委譲 |
| `.../Util/Blueprint/Planning/BlueprintPasteOrigin.cs`（新規） | 列の原点1つと地形の有無 |
| `.../Util/Blueprint/Planning/BlueprintPasteLine.cs`（新規） | 解決済み配線1本 |
| `.../Util/Blueprint/Planning/BlueprintPasteCopyDraft.cs`（新規） | 状態判定前のBP1個分（要素・重なりフラグ・配線） |
| `.../Util/Blueprint/Planning/BlueprintPasteCopyBuilder.cs`（新規） | 原点からドラフトを作る |
| `.../Util/Blueprint/Planning/BlueprintPasteCopyPlan.cs`（新規） | 状態確定済みの不変なBP1個分と「置く対象」の述語 |
| `.../Util/Blueprint/Planning/BlueprintPastePlan.cs`（新規） | 列全体の不変な結果・不足素材・支払い免除 |
| `.../Util/Blueprint/Planning/IBlueprintPasteWorld.cs`（新規） | 重なり・解放・支払い免除の問い合わせ口 |
| `.../Util/Blueprint/Planning/BlueprintPasteCostCalculator.cs`（新規） | ドラフト群の財布込み総素材 |
| `.../Util/Blueprint/Planning/BlueprintPastePlanner.cs`（新規） | 状態判定の唯一の定義 |
| `.../Util/Blueprint/BlueprintPasteExecutor.cs`（新規・サーバー専用） | 置く対象を設置し配線を復元 |
| `.../Util/Blueprint/ServerBlueprintPasteWorld.cs`（新規・サーバー専用） | `IBlueprintPasteWorld` のサーバー実装 |
| `.../Util/Blueprint/BlueprintPlacementCreateParams.cs`（新規） | 設定辞書→`BlockCreateParam[]` |
| `.../Util/Blueprint/BlueprintPasteOperationHandler.cs`（新規・サーバー専用） | `va:blueprint` の Paste 操作の実処理 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs`（変更） | `Paste` 操作をハンドラへ委譲 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintRequest.cs`（新規・`BlueprintPacketDto.cs` から移設） | Request（Paste 用フィールドと factory 追加） |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPacketDto.cs`（変更） | `BlueprintMessagePack` に Wires/Chains、`BlueprintOperation.Paste`、`BlueprintFailureReason` 拡張 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintLineMessagePack.cs`（新規） | BP一覧DTOの配線行 |
| `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`（変更） | `PasteBlueprint` 送信口（`BlueprintRequest.CreatePasteRequest`） |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs`（新規） | カーソル→BP原点（地面/面/側面）・地形追従 |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/ClientBlueprintPasteWorld.cs`（新規） | `IBlueprintPasteWorld` のクライアント実装 |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilder.cs`（変更） | 列の原点列（`BlueprintPasteOrigin`）を返す |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs`（変更） | plan の状態から青/赤と要素ごとのゴースト |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteLinePreview.cs`（新規） | 復元予定の線ゴースト |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPastePlaceSender.cs`（変更） | Paste 送信と Undo 記録 |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteFeedbackReporter.cs`（`BlueprintPasteOverlapReasonReporter.cs` から改名） | plan の状態から理由行 |
| `moorestech_client/.../PlaceSystem/Blueprint/Paste/BlueprintPasteSystem.cs`（変更） | 上を束ねる |
| `moorestech_client/.../PlaceSystem/Common/ElectricWireAutoConnect/PreviewWireLine.cs`（新規・`AutoConnectWirePreviewRenderer` から抽出） | 半透明カテナリー線1本 |
| `moorestech_client/.../PlaceSystem/Targets/BlueprintPlacementTarget.cs`・`PlacementTargetFactory.cs`・`PlacementTargetResolver.cs`（変更） | BPターゲットがBP本体を持つ |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`・`BuildMenuMaterialAvailability.cs`（変更） | BPの必要素材を財布込みで集約 |
| `Localization/localization.csv`・`moorestech_web/webui/src/features/notification/notificationMessages.ts`（変更） | 拒否通知の辞書と通知ID |

`...` は `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse`、`moorestech_client/...` は `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem`。`Util/Blueprint/Planning/` は9ファイル、`Util/Blueprint/` 直下は4ファイル、`Util/Construction/` は10ファイル。

---

### Task 1: 大きいBPのちらつきを再現・計測し原因を確定して直す

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-large-paste-flicker.cs`
- Modify（計測中のみ・コミット前に戻す）: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteSystem.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`
- Modify（仮説確定時の修正）: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs`

**Interfaces:**
- Produces: `BlockPreviewObject.SetRaycastIgnored()`（仮説確定時のみ）。後続タスクは `BlueprintPastePreviewController` を拡張する。

- [ ] **Step 1: 計測ログを一時的に入れる**

`BlueprintPasteSystem.UpdatePastePreview` の `PlacementUnitCellResolver.TryGetCursorCell` 呼び出しの直前に、レイが何に当たったかを毎フレーム出す（計測専用・Step 5 で消す）。

```csharp
// 計測: レイの当たり先とゴースト表示を毎フレーム記録する（ちらつき原因の特定用・コミットしない）
// Probe: log the ray's hit and ghost visibility each frame to locate the flicker (never committed)
var probeRay = _mainCamera.ScreenPointToRay(Client.Game.InGame.Control.AimPointProvider.GetAimScreenPoint());
var probeHit = Physics.Raycast(probeRay, out var probeInfo, float.PositiveInfinity, Client.Common.LayerConst.Without_Player_MapObject_Block_LayerMask);
Debug.Log($"[BlueprintPasteProbe] system=blueprint frame={Time.frameCount} hit={probeHit} collider={(probeHit ? probeInfo.collider.name : "-")} layer={(probeHit ? LayerMask.LayerToName(probeInfo.collider.gameObject.layer) : "-")} isTrigger={(probeHit && probeInfo.collider.isTrigger)} isGhost={(probeHit && probeInfo.collider.GetComponentInParent<Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController.BlockPreviewObject>() != null)}");
```

あわせて `_previewController.Hide()` を呼ぶ3か所の直前に `Debug.Log($"[BlueprintPasteProbe] frame={Time.frameCount} hide reason=<その分岐の理由>");` を置く。

比較用に、同じ4行（`system=normal` に変える）を `CommonBlockPlaceSystem` のレイ解決直前にも入れる（通常設置のゴーストも同じマスクのレイを受けうるのに点滅しない理由を確かめるため）。

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
// コピー: 始点(2,32,2)→終点(9,32,9)→名前確定（手順は blueprint-copy-paste-via-ui.cs の始点/終点/名前確定と同じ呼び出し）
// 貼り付け: BPカテゴリから作成したBPを選ぶ（同ファイルの BlueprintCategory 選択と同じ呼び出し）
await p.AimAt(new Vector3(6f, 32f, 20f));
for (var i = 0; i < 6; i++)
{
    await UniTask.DelayFrame(10);
    await p.Screenshot($"hold-{i:00}");
}
// 比較: 木のチェストをビルドメニューから持ち、同じ地点で60フレーム静止する
// Compare: hold a wooden chest from the build menu and hold still at the same point for 60 frames
```

- [ ] **Step 3: 実行してログを読む**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-large-paste-flicker.cs`
→ `uloop get-logs --project-path ./moorestech_client --search-text "BlueprintPasteProbe"`
Expected: `system=blueprint` で静止中に当たり先が「地面」と「ゴースト（isGhost=True）」で交互になる、または `hide reason=cursor has no placement surface` がフレームおきに出る一方、`system=normal` は isGhost=False が続く（または通常ゴーストがカーソル直下に来ない）＝主仮説確定。別の振る舞いなら観測値（どの行がどの周期で出るか、通常設置との差）をそのまま報告ファイルへ書いて **このタスクを止める**（推測で直さない）。

- [ ] **Step 4: 仮説確定時のみ修正する**

`BlueprintPastePreviewController.UpdatePreview` でプールから取り出したゴーストの collider をレイから外す。

```csharp
var previewObject = _pool.GetObject(placement.BlockId);
// ゴースト自身がカーソルのレイを受けると照準が毎フレーム外れて点滅するため、レイの対象から外す
// A ghost catching the cursor ray drops the aim every other frame and flickers, so keep it out of raycasts
previewObject.SetRaycastIgnored();
```

`BlockPreviewObject` に追加する:

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

Step 1 のログ行を2ファイルからすべて消す。
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
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintLineMessagePack.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintRequest.cs`（`BlueprintPacketDto.cs` の `BlueprintRequest` をそのまま移設。200行対策）
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

`BlueprintJsonObject.cs` の `BlueprintJsonObject` に配線2リストを足し、コンストラクタを5引数にする。空コンストラクタでも両リストを new する。`BlueprintBlockJsonObject` のオフセット注記を「外接箱最小角（全ブロックの OriginalPos の成分最小）からの相対」へ直す。同ファイル末尾に配線行の型を足す（Game.Blueprint の .cs を10本以内に保つ）。

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

`BlueprintRequest` クラスを `BlueprintPacketDto.cs` から `BlueprintRequest.cs` へそのまま移す（Task 6 で Paste 用フィールドを足すため、`BlueprintPacketDto.cs` を200行未満に保つ）。

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
        private static readonly string[] OffsetKeys = { "offsetX", "offsetY", "offsetZ" };

        public int FromVersion => 4;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // BPを1件も持たないセーブは節自体が無いことがある
            // A save without blueprints may lack the section entirely
            var blueprintsToken = save["blueprints"];
            if (blueprintsToken == null || blueprintsToken.Type == JTokenType.Null) return SaveMigrationStepResult.Converted(save);
            if (!(blueprintsToken is JArray blueprints)) return Fail($"blueprintsが配列ではありません。 type={blueprintsToken.Type}");

            // 変換前に全BPの形を検証し、途中まで書き換えた状態を残さない
            // Validate every blueprint before rewriting, so no half-converted state is left behind
            foreach (var blueprintToken in blueprints)
            {
                var invalidReason = FindInvalidReason(blueprintToken);
                if (invalidReason != null) return Fail(invalidReason);
            }

            foreach (var blueprintToken in blueprints)
            {
                var blueprint = (JObject)blueprintToken;
                ShiftToMinCorner((JArray)blueprint["blocks"]);
                blueprint["wires"] = new JArray();
                blueprint["chains"] = new JArray();
            }

            Debug.Log($"セーブを版4から版5へ変換しました。BP={blueprints.Count}件");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            string FindInvalidReason(JToken blueprintToken)
            {
                if (!(blueprintToken is JObject blueprint)) return $"blueprints要素がオブジェクトではありません。 type={blueprintToken.Type}";
                if (!(blueprint["blocks"] is JArray blocks)) return $"BPのblocksが配列ではありません。 guid={blueprint["guid"]}";
                foreach (var blockToken in blocks)
                {
                    if (!(blockToken is JObject block)) return $"BPのblocks要素がオブジェクトではありません。 guid={blueprint["guid"]} type={blockToken.Type}";
                    foreach (var key in OffsetKeys)
                    {
                        if (block[key]?.Type != JTokenType.Integer) return $"BPのブロックに整数の{key}がありません。 guid={blueprint["guid"]}";
                    }
                }

                return null;
            }

            void ShiftToMinCorner(JArray blocks)
            {
                if (blocks.Count == 0) return;
                foreach (var key in OffsetKeys)
                {
                    var min = int.MaxValue;
                    foreach (var block in blocks) min = Mathf.Min(min, block[key].Value<int>());
                    foreach (var block in blocks) block[key] = block[key].Value<int>() - min;
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

`WorldSaveAllInfo.CurrentVersion` を `5` にし、次の3か所の配列末尾へ `new SaveMigrationStepV4ToV5()` を足す:
- `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs:114`
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs:37`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs` の `CurrentVersionChain()`

（`SaveMigrationChain.ForCurrentVersion` は FromVersion 1..CurrentVersion-1 の欠番を構築時に例外にするため、1か所でも漏れると該当テストが構築で落ちる。）

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
    Assert.IsFalse(new SaveMigrationStepV4ToV5().Migrate(save).IsConverted);
}

[Test]
public void blocks要素がオブジェクトでなければ失敗理由を返すTest()
{
    LogAssert.Expect(LogType.Warning, new Regex("blocks要素がオブジェクトではありません"));
    var save = new JObject { ["blueprints"] = new JArray(new JObject { ["blocks"] = new JArray(1) }) };
    Assert.IsFalse(new SaveMigrationStepV4ToV5().Migrate(save).IsConverted);
}

[Test]
public void offsetが欠けていれば失敗理由を返し書き換えないTest()
{
    LogAssert.Expect(LogType.Warning, new Regex("整数のoffsetYがありません"));
    var broken = Block(1, 0, 1);
    broken.Remove("offsetY");
    var save = new JObject { ["blueprints"] = new JArray(new JObject { ["blocks"] = new JArray(Block(5, 5, 5), broken) }) };
    var result = new SaveMigrationStepV4ToV5().Migrate(save);
    Assert.IsFalse(result.IsConverted);
    Assert.AreEqual(5, save["blueprints"][0]["blocks"][0]["offsetX"].Value<int>());
}

private static JObject Block(int x, int y, int z) => new() { ["offsetX"] = x, ["offsetY"] = y, ["offsetZ"] = z, ["blockGuid"] = Guid.Empty.ToString(), ["direction"] = 0, ["settings"] = new JObject() };
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
    Assert.IsTrue(ElectricWireSystemUtil.TryConnect(new Vector3Int(0, 0, 0), new Vector3Int(3, 0, 0), PlayerId, ElectricWireConnectToolGuid, false, out _));
    Assert.IsTrue(ElectricWireSystemUtil.TryConnect(new Vector3Int(3, 0, 0), new Vector3Int(20, 0, 0), PlayerId, ElectricWireConnectToolGuid, false, out _));
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

`PlaceWithItems` は `TryAddBlock` で置き、プレイヤーインベントリへ電線・チェーン素材を入れる（`ElectricWireSaveLoadTest` の素材投入と同じ手順）。`ElectricWireConnectToolGuid = c0000000-0000-0000-0000-000000000001`、`PlayerId = 0`。電線の距離が範囲外なら電柱の間隔を `ElectricWireSaveLoadTest` が使う間隔へ合わせる。

既存テストの `new BlueprintJsonObject(name, blocks, guid)` は全て `new BlueprintJsonObject(name, blocks, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), guid)` へ直す（Files 節の追随一覧）。

- [ ] **Step 7: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Blueprint|SaveMigration|SaveLoadPreparer"`
Expected: ErrorCount 0 / 全 PASS（`SaveMigrationChainConstructionTest`・`SaveMigrationChainTest`・`SaveLoadPreparer` を使うテストを含む。いずれも構築時の欠番例外が出ない）

- [ ] **Step 8: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintLineMessagePack.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintRequest.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPacketDto.cs moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint
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

多セルの機械を混ぜ、回転で `MaxPos` の軸入れ替えが効く形にする。

```csharp
[Test]
public void 多セルを含むBPを回転しても外接箱最小角が原点に一致するTest()
{
    new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
    var machineGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid.ToString();
    var blueprint = new BlueprintJsonObject("r", new List<BlueprintBlockJsonObject>
    {
        new(new Vector3Int(0, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
        new(new Vector3Int(3, 1, 0), machineGuid, (int)BlockDirection.East, new Dictionary<string, string>()),
    }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid());
    var origin = new Vector3Int(10, 5, 10);

    for (var rotation = 0; rotation < 4; rotation++)
    {
        var placements = BlueprintPasteCalculator.CalculatePlacements(blueprint, origin, rotation);
        var min = BlueprintPlacementElementUtil.ToPositionInfo(placements[0]).MinPos;
        foreach (var placement in placements) min = Vector3Int.Min(min, BlueprintPlacementElementUtil.ToPositionInfo(placement).MinPos);
        Assert.AreEqual(origin, min, $"rotation={rotation}");
        Assert.AreEqual(new[] { 0, 1 }, placements.Select(p => p.BlockIndex).ToArray());

        // 機械とチェストが重ならない（回転で外形がつぶれない）
        // The machine and chest never overlap (rotation never collapses the extent)
        Assert.IsFalse(BlueprintPlacementElementUtil.ToPositionInfo(placements[0]).IsOverlap(BlueprintPlacementElementUtil.ToPositionInfo(placements[1])), $"rotation={rotation}");
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

### Task 4: 1セル設置の実行器を抽出し PlaceBlockProtocol をそれへ委譲する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BlockCellPlacement.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BlockCellPlacementExecutor.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`
- Test（回帰）: 既存 `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTest.cs` と `ElectricWireAutoConnectPlaceTest` 系・無料設置系（無変更で通す）

**Interfaces:**
- Produces:
  - `public readonly struct BlockCellPlacement { public readonly IConstructionPlacementPlan WalletPlan; public readonly bool IsPaymentWaived; public readonly bool IsAffordable; public IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume => WalletPlan.ItemsToConsume; }`
  - `public class BlockCellPlacementExecutor { public BlockCellPlacementExecutor(ConstructionWalletService wallet); public BlockCellPlacement PlanCell(BlockId blockId, int playerId, IOpenableInventory inventory, bool isPaymentWaived); public bool TryPlaceCell(BlockCellPlacement placement, BlockId blockId, Vector3Int position, BlockDirection direction, BlockCreateParam[] createParams, IOpenableInventory inventory, out IBlock block); public void FlushRemainingCountChanges(); }`

- [ ] **Step 1: 実行器を書く**

```csharp
public class BlockCellPlacementExecutor
{
    private readonly ConstructionWalletService _wallet;

    public BlockCellPlacementExecutor(ConstructionWalletService wallet)
    {
        _wallet = wallet;
    }

    // 財布へ問い合わせ、このセルを払えるかと消費素材を一度に決める。無料設置は払える扱い（#1486: 支払いだけ免除）
    // Ask the wallet and decide affordability and consumption together; free placement counts as affordable (#1486: payment waived only)
    public BlockCellPlacement PlanCell(BlockId blockId, int playerId, IOpenableInventory inventory, bool isPaymentWaived)
    {
        var walletPlan = _wallet.PlanPlacement(blockId, playerId);
        var isAffordable = isPaymentWaived || ConstructionCostService.HasRequiredItems(walletPlan.ItemsToConsume, inventory.InventoryItems);
        return new BlockCellPlacement(walletPlan, isPaymentWaived, isAffordable);
    }

    // 追加に成功したときだけ支払いを確定する。無料設置は支払わず支払者記録も残さない
    // Commit the payment only when the add succeeds; free placement pays nothing and keeps no payer record
    public bool TryPlaceCell(BlockCellPlacement placement, BlockId blockId, Vector3Int position, BlockDirection direction, BlockCreateParam[] createParams, IOpenableInventory inventory, out IBlock block)
    {
        if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, direction, createParams, out block)) return false;
        if (!placement.IsPaymentWaived) _wallet.CommitPlacement(placement.WalletPlan, inventory, block.BlockInstanceId);
        return true;
    }

    // ドラッグ・列でセル数分に増幅させないため、財布の変更通知は呼び出しの最後に1通へ集約する
    // Collapse wallet notifications into one at the end of a call so drags and runs never amplify them
    public void FlushRemainingCountChanges()
    {
        _wallet.FlushRemainingCountChanges();
    }
}
```

- [ ] **Step 2: PlaceBlockProtocol を委譲へ書き換える**

コンストラクタで `_cellPlacementExecutor = new BlockCellPlacementExecutor(_constructionWallet);` を作り、`PlaceBlock` を次の形にする（解放判定・自動配線の事前検証・通知・Undo ログは現状どおり）。

```csharp
var inventory = inventoryData.MainOpenableInventory;
var cellPlacement = _cellPlacementExecutor.PlanCell(placeBlockId, requesterPlayerId, inventory, isFreePlacement);
if (!cellPlacement.IsAffordable) { costShortageCount++; LogRestoreSkip(placeInfo, "construction cost shortage"); return; }

// （自動接続の事前検証は cellPlacement.ItemsToConsume を予約として渡す以外は現状のまま）
// (Auto-connect pre-validation stays as-is except it reserves cellPlacement.ItemsToConsume)

if (!_cellPlacementExecutor.TryPlaceCell(cellPlacement, placeBlockId, placeInfo.Position, placeInfo.Direction, createParams, inventory, out var block)) { CountRestoreFailure(placeInfo, "TryAddBlock failed"); return; }
if (isAutoConnectElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
```

末尾の `_constructionWallet.FlushRemainingCountChanges()` は `_cellPlacementExecutor.FlushRemainingCountChanges()` にする。

- [ ] **Step 3: コンパイルと回帰テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockProtocol|ElectricWireAutoConnectPlace|FreePlacement|ConstructionWallet|Undo"`
Expected: ErrorCount 0 / 全 PASS（テストは無変更）

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs
git commit -m "refactor: 1セル設置（財布計画→追加→支払い確定）を実行器へ抽出しPlaceBlockProtocolを委譲する"
```

---

### Task 5: 共有の貼り付け判定 `BlueprintPastePlanner` を作る

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteOrigin.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteLine.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyDraft.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyBuilder.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyPlan.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPastePlan.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/IBlueprintPasteWorld.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCostCalculator.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPastePlanner.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/Materials/ConstructionMaterialAccounting.cs`（`MatchRequirements` を移設）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs`（`CalculateRequirements(IReadOnlyList<(ItemId,int)>, ...)` を `MatchRequirements` への委譲にする）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Blueprint/BlueprintPastePlannerTest.cs`

**Interfaces:**
- Consumes: Task 2 の `BlueprintJsonObject.Wires/Chains`、Task 3 の `CalculatePlacements` と `BlueprintPlacementElement.BlockIndex`
- Produces:
  - `public readonly struct BlueprintPasteOrigin { public readonly Vector3Int Position; public readonly bool IsGroundFound; public BlueprintPasteOrigin(Vector3Int position, bool isGroundFound); }`
  - `public enum BlueprintPasteLineKind { ElectricWire, GearChain }`
  - `public readonly struct BlueprintPasteLine { public readonly BlueprintPasteLineKind Kind; public readonly int ElementIndexA; public readonly int ElementIndexB; public readonly Vector3Int PositionA; public readonly Vector3Int PositionB; public readonly Guid ConnectToolGuid; public readonly IReadOnlyList<ConnectToolMaterialCost> Materials; }`
  - `public class BlueprintPasteCopyDraft { public Vector3Int Origin { get; } public bool IsGroundFound { get; } public IReadOnlyList<BlueprintPlacementElement> Elements { get; } public IReadOnlyList<bool> NonOverlapFlags { get; } public IReadOnlyList<BlueprintPasteLine> Lines { get; } }`
  - `public static class BlueprintPasteCopyBuilder { public static BlueprintPasteCopyDraft Build(BlueprintJsonObject blueprint, BlueprintPasteOrigin origin, int rotationStep, IBlueprintPasteWorld world); public static BlueprintPasteCopyDraft BuildUnobstructed(BlueprintJsonObject blueprint); }`
  - `public enum BlueprintPasteCopyState { Placeable, GroundNotFound, AllOverlapped, NotUnlocked, MaterialShortage }`
  - `public class BlueprintPasteCopyPlan { public BlueprintPasteCopyPlan(BlueprintPasteCopyDraft draft, BlueprintPasteCopyState state); public BlueprintPasteCopyDraft Draft { get; } public BlueprintPasteCopyState State { get; } public bool IsPlaced => State == BlueprintPasteCopyState.Placeable; public IEnumerable<BlueprintPlacementElement> EnumerateElementsToPlace(); }`（不変。`SetState` は持たない）
  - `public class BlueprintPastePlan { public IReadOnlyList<BlueprintPasteCopyPlan> Copies { get; } public bool IsPaymentWaived { get; } public IReadOnlyList<(ItemId itemId, int held, int required)> ShortageRequirements { get; } public IEnumerable<BlueprintPasteCopyPlan> EnumerateCopiesToPlace(); public int CountCopies(BlueprintPasteCopyState state); }`
  - `public interface IBlueprintPasteWorld { bool IsOverlapping(BlockPositionInfo positionInfo); bool IsBlockUnlocked(Guid blockGuid); bool IsConnectToolUnlocked(Guid connectToolGuid); bool IsPaymentWaived { get; } }`
  - `public static class BlueprintPasteCostCalculator { public static List<(ItemId itemId, int count)> CalcRequiredItems(IReadOnlyList<BlueprintPasteCopyDraft> drafts, ConstructionWalletQuery wallet, bool isPaymentWaived); }`
  - `public static class BlueprintPastePlanner { public static BlueprintPastePlan Plan(BlueprintJsonObject blueprint, IReadOnlyList<BlueprintPasteOrigin> origins, int rotationStep, IBlueprintPasteWorld world, ConstructionWalletQuery wallet, IReadOnlyDictionary<ItemId, int> heldByItem); }`
  - `ConstructionMaterialAccounting.MatchRequirements(IReadOnlyList<(ItemId itemId, int count)> required, IReadOnlyDictionary<ItemId, int> heldByItem) : List<(ItemId itemId, int held, int required)>`

- [ ] **Step 1: 突き合わせを1か所へ移す**

`ConstructionCostShortageCalculator.CalculateRequirements(IReadOnlyList<(ItemId itemId, int count)>, IReadOnlyDictionary<ItemId,int>)` の本体を `ConstructionMaterialAccounting.MatchRequirements` として `Game.Construction` へ移し、クライアント側の同名メソッドは `return ConstructionMaterialAccounting.MatchRequirements(requiredItems, heldByItem);` だけにする（同じ算術を2か所に持たない）。

- [ ] **Step 2: 型を書く**

`BlueprintPasteCopyPlan`（不変・置く対象の述語を持つ）:

```csharp
public class BlueprintPasteCopyPlan
{
    public BlueprintPasteCopyDraft Draft { get; }
    public BlueprintPasteCopyState State { get; }

    // 置くかどうかはこの状態1本からだけ導く（表示・送信・実行で同じ判定を繰り返さない）
    // Whether to place is derived only from this state (never re-judged in display, send, or execution)
    public bool IsPlaced => State == BlueprintPasteCopyState.Placeable;

    public BlueprintPasteCopyPlan(BlueprintPasteCopyDraft draft, BlueprintPasteCopyState state)
    {
        Draft = draft;
        State = state;
    }

    // 重ならない要素だけが置く対象
    // Only non-overlapping elements are placed
    public IEnumerable<BlueprintPlacementElement> EnumerateElementsToPlace()
    {
        for (var i = 0; i < Draft.Elements.Count; i++)
        {
            if (Draft.NonOverlapFlags[i]) yield return Draft.Elements[i];
        }
    }
}
```

`BlueprintPastePlan.EnumerateCopiesToPlace()` は `Copies.Where(c => c.IsPlaced)`、`CountCopies(state)` は該当状態の数、`IsPaymentWaived` は `world.IsPaymentWaived` の写し（実行器はこれを読み、別引数で受けない）。

- [ ] **Step 3: ドラフトの組み立てを書く**

```csharp
public static class BlueprintPasteCopyBuilder
{
    public static BlueprintPasteCopyDraft Build(BlueprintJsonObject blueprint, BlueprintPasteOrigin origin, int rotationStep, IBlueprintPasteWorld world)
    {
        var elements = BlueprintPasteCalculator.CalculatePlacements(blueprint, origin.Position, rotationStep);
        var nonOverlapFlags = elements.Select(e => !world.IsOverlapping(BlueprintPlacementElementUtil.ToPositionInfo(e))).ToList();
        return Create(blueprint, origin, elements, nonOverlapFlags);
    }

    // ビルドメニュー用: 回転0・原点0・重なりなしのBP1個
    // For the build menu: one copy at rotation 0 and origin 0 with nothing overlapping
    public static BlueprintPasteCopyDraft BuildUnobstructed(BlueprintJsonObject blueprint)
    {
        var elements = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 0);
        return Create(blueprint, new BlueprintPasteOrigin(Vector3Int.zero, true), elements, elements.Select(_ => true).ToList());
    }

    private static BlueprintPasteCopyDraft Create(BlueprintJsonObject blueprint, BlueprintPasteOrigin origin, List<BlueprintPlacementElement> elements, List<bool> nonOverlapFlags)
    {
        // BP内indexから要素indexを引けるようにする（マスタ欠損ブロックは要素に無い）
        // Map blueprint block indices to element indices (blocks missing from the master have no element)
        var elementIndexByBlockIndex = new Dictionary<int, int>();
        for (var i = 0; i < elements.Count; i++) elementIndexByBlockIndex[elements[i].BlockIndex] = i;

        var lines = new List<BlueprintPasteLine>();
        ResolveLines(BlueprintPasteLineKind.ElectricWire, blueprint.Wires);
        ResolveLines(BlueprintPasteLineKind.GearChain, blueprint.Chains);
        return new BlueprintPasteCopyDraft(origin.Position, origin.IsGroundFound, elements, nonOverlapFlags, lines);

        #region Internal

        void ResolveLines(BlueprintPasteLineKind kind, List<BlueprintLineJsonObject> savedLines)
        {
            foreach (var saved in savedLines)
            {
                // 端点ブロックがマスタから消えた線は張れない
                // A line whose endpoint block vanished from the master cannot be drawn
                if (!elementIndexByBlockIndex.TryGetValue(saved.BlockIndexA, out var indexA) || !elementIndexByBlockIndex.TryGetValue(saved.BlockIndexB, out var indexB))
                {
                    Debug.LogWarning($"[BlueprintPaste] line skipped: endpoint missing kind={kind} a={saved.BlockIndexA} b={saved.BlockIndexB} blueprint={blueprint.BlueprintGuid}");
                    continue;
                }

                // 片端が重なりで置けない線は張らない（ADR 0077 決定4: 両端が置けた線だけ）
                // Skip lines whose endpoint is blocked by an overlap (ADR 0077 decision 4)
                if (!nonOverlapFlags[indexA] || !nonOverlapFlags[indexB]) continue;

                var positionA = elements[indexA].Position;
                var positionB = elements[indexB].Position;
                if (!ConnectToolCostCalculator.TryCalculate(saved.ConnectToolGuid, Vector3Int.Distance(positionA, positionB), out var materials))
                {
                    Debug.LogWarning($"[BlueprintPaste] line skipped: unknown connect tool {saved.ConnectToolGuid} blueprint={blueprint.BlueprintGuid}");
                    continue;
                }

                lines.Add(new BlueprintPasteLine(kind, indexA, indexB, positionA, positionB, saved.ConnectToolGuid, materials));
            }
        }

        #endregion
    }
}
```

- [ ] **Step 4: 総素材計算を書く**

```csharp
public static List<(ItemId itemId, int count)> CalcRequiredItems(IReadOnlyList<BlueprintPasteCopyDraft> drafts, ConstructionWalletQuery wallet, bool isPaymentWaived)
{
    // ブロックは種類ごとのセル数を数え、財布の残りを考慮したコストセット数で払う。無料設置ではブロックと電線は払わない
    // Count cells per block kind and pay the wallet-aware cost sets; free placement pays for neither blocks nor wires
    var cellCounts = new Dictionary<BlockId, int>();
    var lineMaterials = new List<ConnectToolMaterialCost>();
    foreach (var draft in drafts)
    {
        for (var i = 0; i < draft.Elements.Count; i++)
        {
            if (!draft.NonOverlapFlags[i]) continue;
            cellCounts.TryGetValue(draft.Elements[i].BlockId, out var count);
            cellCounts[draft.Elements[i].BlockId] = count + 1;
        }

        // 無料設置でもチェーンは払う（#1486 は電線ツールだけを無料にした）
        // Chains are paid even in free placement (#1486 made only the electric wire tool free)
        foreach (var line in draft.Lines)
        {
            if (isPaymentWaived && line.Kind == BlueprintPasteLineKind.ElectricWire) continue;
            lineMaterials.AddRange(line.Materials);
        }
    }

    var required = new Dictionary<ItemId, int>();
    if (!isPaymentWaived)
    {
        foreach (var (blockId, cellCount) in cellCounts)
        {
            var sets = wallet.GetRequiredCostSets(blockId, cellCount);
            foreach (var (itemId, count) in ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems)) Add(itemId, count * sets);
        }
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

- [ ] **Step 5: プランナーを書く**

```csharp
public static BlueprintPastePlan Plan(BlueprintJsonObject blueprint, IReadOnlyList<BlueprintPasteOrigin> origins, int rotationStep, IBlueprintPasteWorld world, ConstructionWalletQuery wallet, IReadOnlyDictionary<ItemId, int> heldByItem)
{
    var drafts = origins.Select(origin => BlueprintPasteCopyBuilder.Build(blueprint, origin, rotationStep, world)).ToList();
    var states = new BlueprintPasteCopyState[drafts.Count];
    var shortageRequirements = new List<(ItemId itemId, int held, int required)>();

    // 地形・重なり・解放はBPごとに独立して決まる
    // Terrain, overlap and unlock are decided per copy independently
    for (var i = 0; i < drafts.Count; i++) states[i] = JudgeIndependentState(drafts[i]);

    // 素材は始点側から累計し、最初に足りなくなったBP以降を丸ごと不足にする（BP1個単位・ADR 0077 決定6b）
    // Accumulate materials from the run start; the first short copy and every later one become shortages (per copy, ADR 0077 decision 6b)
    MarkShortages();

    // 状態を確定してから不変の結果を作る
    // Build the immutable result only after every state is final
    var copies = drafts.Select((draft, i) => new BlueprintPasteCopyPlan(draft, states[i])).ToList();
    return new BlueprintPastePlan(copies, world.IsPaymentWaived, shortageRequirements);

    #region Internal

    BlueprintPasteCopyState JudgeIndependentState(BlueprintPasteCopyDraft draft)
    {
        if (!draft.IsGroundFound) return BlueprintPasteCopyState.GroundNotFound;
        if (draft.NonOverlapFlags.All(flag => !flag)) return BlueprintPasteCopyState.AllOverlapped;
        if (!IsUnlocked(draft)) return BlueprintPasteCopyState.NotUnlocked;
        return BlueprintPasteCopyState.Placeable;
    }

    bool IsUnlocked(BlueprintPasteCopyDraft draft)
    {
        foreach (var element in draft.Elements)
        {
            if (!world.IsBlockUnlocked(MasterHolder.BlockMaster.GetBlockMaster(element.BlockId).BlockGuid)) return false;
        }
        return draft.Lines.All(line => world.IsConnectToolUnlocked(line.ConnectToolGuid));
    }

    void MarkShortages()
    {
        var accepted = new List<BlueprintPasteCopyDraft>();
        var isShort = false;
        for (var i = 0; i < drafts.Count; i++)
        {
            if (states[i] != BlueprintPasteCopyState.Placeable) continue;
            if (isShort) { states[i] = BlueprintPasteCopyState.MaterialShortage; continue; }

            accepted.Add(drafts[i]);
            var requirements = ConstructionMaterialAccounting.MatchRequirements(BlueprintPasteCostCalculator.CalcRequiredItems(accepted, wallet, world.IsPaymentWaived), heldByItem);
            if (requirements.All(r => r.required <= r.held)) continue;

            isShort = true;
            states[i] = BlueprintPasteCopyState.MaterialShortage;
            shortageRequirements.AddRange(requirements);
        }
    }

    #endregion
}
```

- [ ] **Step 6: テストを書く**

`BlueprintPastePlannerTest.cs` はテスト用 `IBlueprintPasteWorld`（重なり座標の集合・未解放 Guid 集合・`IsPaymentWaived` を持つ小クラス）を同ファイル内 private class で持つ。財布は `new ConstructionWalletQuery(<残り0を返す IRemainingPlacementCountReader のテスト実装>)`。BPは Task 3 のテストと同じ組み立て方（`ChestId`・`ElectricPoleId`）。

```csharp
[Test] public void 素材が1BP分だけあれば列の1個目だけ置けるTest()       // origins 3個・所持=1BP分 → [Placeable, MaterialShortage, MaterialShortage]、ShortageRequirements に不足素材、EnumerateCopiesToPlace は1個
[Test] public void 一部重なるBPは重ならない分の素材だけ要求するTest()     // 1ブロック重ね → NonOverlapFlags にfalse1つ、EnumerateElementsToPlace が1個少ない、要求数が1ブロック分減る
[Test] public void 全ブロック重なりはAllOverlappedTest()
[Test] public void 地形が取れない原点はGroundNotFoundで素材計算に入らないTest() // origins[0].IsGroundFound=false・所持=1BP分 → [GroundNotFound, Placeable]
[Test] public void 未解放ブロックを含むBPはNotUnlockedTest()
[Test] public void 未解放線種は無料設置でもNotUnlockedTest()
[Test] public void 片端が重なる配線は解決されないTest()                    // 電柱2本と線1本、片方を重ねる → Lines.Count==0
[Test] public void 配線素材も総素材に入るTest()                             // 電線素材を0にすると MaterialShortage
[Test] public void 支払い免除なら建設素材と電線素材が無くても置けるTest()
[Test] public void 生成後のCopyPlanは状態を変える口を持たないTest()        // typeof(BlueprintPasteCopyPlan) に public set/Set* メソッドが無いことを反射で確認
```

`ConstructionCostShortageCalculator` の既存テスト（`ConstructionCostShortageCalculatorTest`・`ConstructionMaterialShortageReporterTest`）が委譲後も通ることを確認する。

- [ ] **Step 7: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPastePlanner|ConstructionCostShortage|ConstructionMaterialShortage|ConstructionMaterialAccounting"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning moorestech_server/Assets/Scripts/Game.Construction/Materials/ConstructionMaterialAccounting.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs moorestech_server/Assets/Scripts/Tests
git commit -m "feat: BP貼り付けの地形・重なり・解放・総素材をBP1個単位で判定し不変の結果を返す共有プランナーを追加"
```

---

### Task 6: `va:blueprint` に Paste 操作を足し、一括判定・設置・配線復元する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintRequest.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPacketDto.cs`（`BlueprintOperation.Paste`・`BlueprintFailureReason` 拡張）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteOperationHandler.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteExecutor.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/ServerBlueprintPasteWorld.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPlacementCreateParams.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/ConstructionWalletService.cs`（`GetQuery` を public へ）
- Modify: `Localization/localization.csv`, `moorestech_web/webui/src/features/notification/notificationMessages.ts`, `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`, `moorestech_web/webui/src/shared/i18n/generated/*`（再生成物）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest/BlueprintPasteProtocolTest.cs`

**Interfaces:**
- Consumes: Task 4 `BlockCellPlacementExecutor`、Task 5 `BlueprintPastePlanner.Plan`・`BlueprintPastePlan.EnumerateCopiesToPlace`・`BlueprintPasteCopyPlan.EnumerateElementsToPlace`
- Produces:
  - `BlueprintOperation.Paste`、`BlueprintRequest` に `[Key(7)] int RotationStep`・`[Key(8)] List<Vector3IntMessagePack> Origins`、`public static BlueprintRequest CreatePasteRequest(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins)`
  - `BlueprintFailureReason` に `PasteCostShortage = 7, PasteNotUnlocked = 8, PasteLineFailed = 9`
  - 通知キー `denied.blueprint.PasteCostShortage`・`denied.blueprint.PasteNotUnlocked`・`denied.blueprint.PasteLineFailed`（p0=件数）
  - `public class BlueprintPasteOperationHandler { public BlueprintPasteOperationHandler(ServiceProvider serviceProvider); public void Handle(BlueprintRequest request, int requesterPlayerId); }`
  - `public static class BlueprintPasteExecutor { public static int Execute(BlueprintPastePlan plan, int playerId, BlockCellPlacementExecutor cellExecutor, IOpenableInventory inventory); }`
  - `public static class BlueprintPlacementCreateParams { public static BlockCreateParam[] From(Dictionary<string, string> settings); }`
  - `VanillaApiSendOnly.PasteBlueprint(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins)`

- [ ] **Step 1: テストを書く**

`BlueprintPasteProtocolTest.cs`（`PlaceBlockProtocolTest` の DI 生成・インベントリ投入と、`BlueprintIdentityProtocolTest` の `BlueprintRequest` 送信の手順を流用。BP解放は既存BPテストと同じ方法で立てる）:

```csharp
[Test] public void 素材が足りればBPと保存配線が置かれ自動配線はされないTest()
// 電柱2本＋線1本のBPを登録 → 原点(10,0,10)・回転0で送信 → 2本とも存在・互いに接続・それ以外の接続0・電線素材が線1本分減る
[Test] public void 回転1でも保存配線が回転後の位置へ張られるTest()
// 同じBPを回転1で送信 → 電柱は回転後の位置にあり、その2本が互いに接続
[Test] public void 素材が1つでも足りなければそのBPは何も置かれず通知されるTest()
// 建設素材を1BP分-1にして送る → ブロック0個・denied.blueprint.PasteCostShortage
[Test] public void 列は賄える個数までBP単位で置かれるTest()
// 原点3個・素材2BP分 → 2個分だけ置かれる・PasteCostShortage(p0=1)
[Test] public void 重なるブロックは飛ばし残りと両端の残る配線だけ置くTest()
[Test] public void BP機能が未解放なら拒否するTest()
[Test] public void 回転が範囲外なら拒否しログを出すTest()          // RotationStep=4 → 何も置かれない・LogAssert で "[BlueprintPaste] invalid request"
[Test] public void 原点が空または上限超過なら拒否しログを出すTest()  // Origins=[] / Origins=MaxOrigins+1 件
```

- [ ] **Step 2: Request と enum を足す**

`BlueprintRequest` の private コンストラクタに `int rotationStep, List<Vector3IntMessagePack> origins` を足し、既存3 factory は `0, null` を渡す（デフォルト引数は使わない）。

```csharp
// Paste専用。列の各BPの原点（外接箱最小角）と回転
// Paste only: each copy's origin (extent min corner) in the run and the rotation
[Key(7)] public int RotationStep { get; set; }
[Key(8)] public List<Vector3IntMessagePack> Origins { get; set; }

public static BlueprintRequest CreatePasteRequest(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins)
{
    return new BlueprintRequest(BlueprintOperation.Paste, null, null, null, blueprintGuid.ToString(), rotationStep, origins.ConvertAll(o => new Vector3IntMessagePack(o)));
}
```

- [ ] **Step 3: サーバー側の世界実装**

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

    // 無料設置は解放判定も免除する（#1486 後の PlaceBlockProtocol と同じ第3引数）
    // Free placement waives the unlock check too (same third argument as PlaceBlockProtocol after #1486)
    public bool IsBlockUnlocked(Guid blockGuid) => _catalog.IsBlockUnlocked(blockGuid, _unlockState, IsPaymentWaived);

    // 線種の解放は無料設置でも免除しない（PlacementTargetCatalog・TryConnect と同じ規則。免除すると判定は通って実行で失敗する）
    // Connect-tool unlock is never waived, even in free placement (same rule as PlacementTargetCatalog and TryConnect)
    public bool IsConnectToolUnlocked(Guid connectToolGuid) => ElectricWireSystemUtil.IsConnectToolUnlocked(connectToolGuid);
}
```

- [ ] **Step 4: 実行器**

```csharp
public static class BlueprintPasteExecutor
{
    // 置く対象のBPだけを1セル実行器で置き、保存配線を張る。戻り値は張れなかった線の本数
    // Place only the copies to place through the cell executor and redraw their saved lines; returns the failed line count
    public static int Execute(BlueprintPastePlan plan, int playerId, BlockCellPlacementExecutor cellExecutor, IOpenableInventory inventory)
    {
        var failedLines = 0;
        foreach (var copy in plan.EnumerateCopiesToPlace())
        {
            foreach (var element in copy.EnumerateElementsToPlace()) PlaceElement(element);
            foreach (var line in copy.Draft.Lines)
            {
                if (!ConnectLine(line)) failedLines++;
            }
        }
        cellExecutor.FlushRemainingCountChanges();
        return failedLines;

        #region Internal

        void PlaceElement(BlueprintPlacementElement element)
        {
            // 自動配線はしない。支払えるかはプランナーが判定済み
            // No auto-connect; affordability was already settled by the planner
            var cellPlacement = cellExecutor.PlanCell(element.BlockId, playerId, inventory, plan.IsPaymentWaived);
            if (cellExecutor.TryPlaceCell(cellPlacement, element.BlockId, element.Position, element.Direction, BlueprintPlacementCreateParams.From(element.Settings), inventory, out _)) return;
            Debug.LogWarning($"[BlueprintPaste] TryAddBlock failed pos={element.Position} block={element.BlockId} player={playerId}");
        }

        // 電線は #1486 の無料扱い（コスト0で記録）に従い、チェーンは常に払う
        // Wires follow #1486's free handling (recorded at cost 0); chains are always paid
        bool ConnectLine(BlueprintPasteLine line)
        {
            var connected = line.Kind == BlueprintPasteLineKind.ElectricWire
                ? ElectricWireSystemUtil.TryConnect(line.PositionA, line.PositionB, playerId, line.ConnectToolGuid, plan.IsPaymentWaived, out _)
                : GearChainSystemUtil.TryConnect(line.PositionA, line.PositionB, playerId, line.ConnectToolGuid, out _);
            if (!connected) Debug.LogWarning($"[BlueprintPaste] line restore failed kind={line.Kind} a={line.PositionA} b={line.PositionB} player={playerId}");
            return connected;
        }

        #endregion
    }
}
```

`BlueprintPlacementCreateParams.From` は設定辞書を UTF8 バイトの `BlockCreateParam[]` にする（null 設定は空配列）。クライアントの `BlueprintPastePlaceSender` も Task 8 でこれを呼ぶ。

- [ ] **Step 5: ハンドラとプロトコル**

```csharp
public class BlueprintPasteOperationHandler
{
    // 1回の貼り付けで並べられるBPの上限（外部入力の定義域を閉じる）
    // Upper bound of copies per paste, closing the external input's domain
    public const int MaxOrigins = 64;

    // （コンストラクタ: IBlueprintDatastore / IPlayerInventoryDataStore / IGameUnlockStateDataController / NotificationService / PlacementTargetCatalog を取り、new BlockCellPlacementExecutor(serviceProvider.GetService<ConstructionWalletService>()) と ConstructionWalletService を保持）

    public void Handle(BlueprintRequest request, int requesterPlayerId)
    {
        if (!IsValid()) return;
        var blueprint = _blueprintDatastore.Blueprints.FirstOrDefault(b => b.BlueprintGuidStr == request.BlueprintGuidStr);
        if (blueprint == null)
        {
            Debug.LogWarning($"[BlueprintPaste] blueprint not found guid={request.BlueprintGuidStr} player={requesterPlayerId}");
            Notify(BlueprintFailureReason.NotFound, 0);
            return;
        }

        // サーバーは地形を知らないので、送られた原点は地形解決済みとして扱う
        // The server does not know terrain, so received origins are treated as terrain-resolved
        var isPaymentWaived = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
        var inventory = _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;
        var world = new ServerBlueprintPasteWorld(_placementTargetCatalog, _gameUnlockStateDataController, isPaymentWaived);
        var origins = request.Origins.ConvertAll(o => new BlueprintPasteOrigin(o.Vector3Int, true));
        var plan = BlueprintPastePlanner.Plan(blueprint, origins, request.RotationStep, world, _constructionWallet.GetQuery(requesterPlayerId), ConstructionMaterialAccounting.TallyHeld(inventory.InventoryItems));

        var failedLines = BlueprintPasteExecutor.Execute(plan, requesterPlayerId, _cellExecutor, inventory);
        NotifyIfAny(BlueprintFailureReason.PasteCostShortage, plan.CountCopies(BlueprintPasteCopyState.MaterialShortage));
        NotifyIfAny(BlueprintFailureReason.PasteNotUnlocked, plan.CountCopies(BlueprintPasteCopyState.NotUnlocked));
        NotifyIfAny(BlueprintFailureReason.PasteLineFailed, failedLines);

        #region Internal

        bool IsValid()
        {
            var isValid = 0 <= request.RotationStep && request.RotationStep < 4 && request.Origins != null && 0 < request.Origins.Count && request.Origins.Count <= MaxOrigins;
            if (!isValid) Debug.LogWarning($"[BlueprintPaste] invalid request rotation={request.RotationStep} origins={request.Origins?.Count} player={requesterPlayerId}");
            return isValid;
        }

        void NotifyIfAny(BlueprintFailureReason reason, int count)
        {
            if (count <= 0) return;
            Debug.Log($"[BlueprintPaste] rejected {reason} count={count} player={requesterPlayerId}");
            Notify(reason, count);
        }

        void Notify(BlueprintFailureReason reason, int count)
        {
            _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.blueprint.{reason}", new[] { count.ToString() }));
        }

        #endregion
    }
}
```

`BlueprintProtocol` は switch に `case BlueprintOperation.Paste: return HandlePaste(request);` を足す。`HandlePaste` は BP機能未解放なら既存 `NotUnlockedResponse()`、そうでなければ `_pasteHandler.Handle(req, requesterPlayerId); return null;`（Paste は SendOnly で送るので応答を返さない。`PlaceBlockProtocol` と同じ）。`_pasteHandler` はコンストラクタで `new BlueprintPasteOperationHandler(serviceProvider)`。`ConstructionWalletService.GetQuery` を public にする（窓口の答えを読むだけで、呼び出し側は正規化・算術をしない）。

`VanillaApiSendOnly.PasteBlueprint` は `_packetSender.Send(BlueprintRequest.CreatePasteRequest(blueprintGuid, rotationStep, origins));`。

- [ ] **Step 6: 辞書と通知IDを足して再生成**

`Localization/localization.csv` に3行（英日独韓）を足す。
- `ui.notification.blueprintPasteCostShortage`: 「素材が足りないためブループリント{p0}個を設置しませんでした」/ `{p0} blueprints were not placed: not enough materials`
- `ui.notification.blueprintPasteNotUnlocked`: 「未解放のブロックまたは線を含むためブループリント{p0}個を設置しませんでした」/ `{p0} blueprints were not placed: they contain locked blocks or lines`
- `ui.notification.blueprintPasteLineFailed`: 「配線{p0}本を復元できませんでした」/ `{p0} lines could not be restored`

（独韓は既存の同種行の訳調に合わせる。）`denied.blueprint.NotFound` が辞書に無ければ `ui.notification.blueprintNotFound`（「ブループリントが見つかりません」）も足す。`notificationMessages.ts` に `["denied.blueprint.PasteCostShortage", L.ui.notification.blueprintPasteCostShortage]` 等を足し、`_CompileRequester.cs` の印を更新、`cd moorestech_web/webui && npm run gen:i18n`、`npm test -- notification` で coverage テストが通ることを確認する。

- [ ] **Step 7: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPasteProtocol|BlueprintProtocol|BlueprintIdentity|PlaceBlockProtocol"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs Localization/localization.csv moorestech_web/webui/src
git commit -m "feat: va:blueprint にPaste操作を足しBP単位の一括判定のうえ設置し保存配線を復元する"
```

---

### Task 7: クライアントの置き位置（地面中心・側面接し・地形最高点）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilder.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolverTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilderTest.cs`

**Interfaces:**
- Consumes: `PlaceSystemUtil.TryRaycastPlacementSurface`（internal・同アセンブリ）、`PlaceSystemUtil.CalcPlacePointBySize`、`GroundHeightQuantization.StepOf`、`PlacementGroundCellResolver.TryResolveCellFromGround`、Task 5 `BlueprintPasteOrigin`
- Produces:
  - `public static class BlueprintPasteOriginResolver { public static bool TryResolveCursorOrigin(Camera camera, Vector3Int footprintSize, int heightOffset, out Vector3Int origin, out PlacementHitSurfaceKind surfaceKind); public static Vector3Int ResolveOrigin(Vector3Int footprintSize, Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset); }`
  - `BlueprintPasteRunBuilder.BuildOrigins(Vector3Int startOrigin, Vector3Int cursorOrigin, Vector3Int footprintSize, PlacementHitSurfaceKind surfaceKind, int heightOffset) : List<BlueprintPasteOrigin>`

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

（期待値は `PlaceSystemUtil.CalcPlacePointBySize` の既存式で手計算済み。）`BlueprintPasteRunBuilderTest` は `BuildOrigins` が列の原点を外形寸法ずつ並べ、`surfaceKind=BlockFace` では全て `IsGroundFound=true` を返すことを確かめる形に書き換える（地形を引く経路は録画テストで確認する）。

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
```

`BlueprintPasteRunBuilder.BuildOrigins`:

```csharp
public static List<BlueprintPasteOrigin> BuildOrigins(Vector3Int startOrigin, Vector3Int cursorOrigin, Vector3Int footprintSize, PlacementHitSurfaceKind surfaceKind, int heightOffset)
{
    var run = PlacementRunPositionCalculator.Calculate(startOrigin, cursorOrigin, footprintSize);

    // 地面ヒットで列が水平なら各BPを外接箱底面フットプリントの地形最高点へ合わせる（ADR 0047・PlacementGroundFollowStep と同じ条件）
    // On a ground hit with a horizontal run, fit each copy to the terrain max over its footprint (ADR 0047, same condition as PlacementGroundFollowStep)
    var followsGround = surfaceKind == PlacementHitSurfaceKind.Ground && run.Axis != PlacementRunAxis.Y;
    var origins = new List<BlueprintPasteOrigin>(run.Positions.Count);
    foreach (var position in run.Positions)
    {
        if (!followsGround) { origins.Add(new BlueprintPasteOrigin(position, true)); continue; }
        var found = PlacementGroundCellResolver.TryResolveCellFromGround(position, BlockDirection.North, footprintSize, heightOffset, out var resolved);
        origins.Add(new BlueprintPasteOrigin(found ? resolved : position, found));
    }

    return origins;
}
```

（地形が取れない原点は `IsGroundFound=false` のままプランナーへ渡し、`GroundNotFound` 状態として扱う。既存の `Build`（フラットな要素列を返す版）は削除する。）

- [ ] **Step 3: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPasteOriginResolver|BlueprintPasteRunBuilder"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste
git commit -m "feat: BPの置き位置を外接箱で解決し側面は面に接して最下段をカーソル段にそろえる"
```

---

### Task 8: 貼り付けシステムをプランナー・Paste 送信・配線ゴーストへつなぐ

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/ClientBlueprintPasteWorld.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteLinePreview.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/PreviewWireLine.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/AutoConnectWirePreviewRenderer.cs`（`WireLine` を `PreviewWireLine` へ抽出して使う）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePlaceSender.cs`
- Rename/Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOverlapReasonReporter.cs` → `BlueprintPasteFeedbackReporter.cs`（`git mv`。.meta は Unity に任せる）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/PlacementTargetResolver.cs`（`IsConnectToolUnlocked` が無ければ足す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/BlueprintPasteOverlapReasonReporterTest.cs` → `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Paste/BlueprintPasteFeedbackReporterTest.cs`

**Interfaces:**
- Consumes: Task 5 `BlueprintPastePlanner.Plan`・`BlueprintPastePlan`、Task 6 `VanillaApiSendOnly.PasteBlueprint`・`BlueprintPlacementCreateParams.From`、Task 7 `BlueprintPasteOriginResolver`・`BuildOrigins`
- Produces:
  - `ClientBlueprintPasteWorld(BlockGameObjectDataStore, PlacementTargetResolver)`（`IsPaymentWaived` は `DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)`。`IsBlockUnlocked` は `PlacementTargetResolver.IsBlockUnlocked`。`IsConnectToolUnlocked` は `PlacementTargetResolver` 経由で接続ツールの解放を引く（無料設置でも免除しない。サーバーと同じ規則）。該当メソッドが無ければ `PlacementTargetResolver.IsConnectToolUnlocked(Guid)` を `IsBlockUnlocked` と同形で足す）
  - `BlueprintPastePreviewController.UpdatePreview(BlueprintPastePlan plan) : IReadOnlyList<IReadOnlyList<BlockPreviewObject>>`（BPごと・要素ごとのゴースト。色は `copy.IsPlaced && copy.Draft.NonOverlapFlags[i]` の1式だけで決める）
  - `BlueprintPasteLinePreview.Show(BlueprintPastePlan plan, IReadOnlyList<IReadOnlyList<BlockPreviewObject>> ghosts)` / `Hide()`
  - `BlueprintPastePlaceSender.Send(Guid blueprintGuid, int rotationStep, BlueprintPastePlan plan)`
  - `BlueprintPasteFeedbackReporter.Report(BlueprintPastePlan plan, PlacementFeedback feedback)`

- [ ] **Step 1: 報告器のテストを書き換える**

```csharp
[Test] public void 全ブロック重なりは既存ブロック理由を出すTest()       // 全BPが AllOverlapped → AddBlockedByExistingBlock 行
[Test] public void 地形が取れないBPがあれば地面なし理由を出すTest()     // GroundNotFound を含む → AddGroundNotFound 行
[Test] public void 素材不足のBPがあれば不足素材行を出すTest()          // ShortageRequirements に (鉄板,2,5) → PlaceMaterialShortage 行・params "2","5"
[Test] public void すべて置けるなら行を出さないTest()
```

- [ ] **Step 2: 報告器を実装する**

```csharp
public static void Report(BlueprintPastePlan plan, PlacementFeedback feedback)
{
    // 理由行は plan の状態だけから出す（呼び出し側で別の判定をしない）
    // Reason lines come only from the plan's states (callers make no separate judgement)
    if (0 < plan.Copies.Count && plan.CountCopies(BlueprintPasteCopyState.AllOverlapped) == plan.Copies.Count) feedback.AddBlockedByExistingBlock();
    if (0 < plan.CountCopies(BlueprintPasteCopyState.GroundNotFound)) feedback.AddGroundNotFound();
    if (0 < plan.ShortageRequirements.Count) feedback.AddMaterialShortages(ConstructionCostShortageCalculator.ToShortages(plan.ShortageRequirements));
}
```

- [ ] **Step 3: 貼り付けシステムをつなぐ**

`BlueprintPasteSystem.ManualUpdate` の流れ（ローカル関数で200行未満に保つ）:

```csharp
// 外接箱寸法で原点を解決し、列の原点を作り、共有プランナーで判定する
// Resolve origins from the extent, build the run, and judge it with the shared planner
if (!BlueprintPasteOriginResolver.TryResolveCursorOrigin(_mainCamera, _footprintSize, _heightOffset.Value, out var cursorOrigin, out var surfaceKind)) { HideAll(); return false; }
if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(cursorOrigin, surfaceKind);
var origins = BlueprintPasteRunBuilder.BuildOrigins(_dragState.ResolveDragStartCell(cursorOrigin), cursorOrigin, _footprintSize, _dragState.ResolveSurfaceKind(surfaceKind), _heightOffset.Value);
plan = BlueprintPastePlanner.Plan(_currentBlueprint, origins, _rotationStep, _pasteWorld, _walletQuery, ConstructionMaterialAccounting.TallyHeld(_localPlayerInventory));
var ghosts = _previewController.UpdatePreview(plan);
_linePreview.Show(plan, ghosts);
BlueprintPasteFeedbackReporter.Report(plan, feedback);
```

距離判定（`IsPlaceableFromPlayer(cursorOrigin)`）は既存どおり残す。送信は既存の `_dragState.TryConsumeSendableRelease` の後で `BlueprintPastePlaceSender.Send(_currentBlueprintGuid, _rotationStep, plan)`。`Rotate()` と `ResolveBlueprint` の `_footprintSize` 計算は既存を流用。コンストラクタに `ConstructionWalletQuery`・`ILocalPlayerInventory`・`PlacementTargetResolver` を足す（VContainer の自動解決。`MainGameInteractionRegistration.cs:113` の型登録は変更不要。`ClientBlueprintPasteWorld` はコンストラクタ内で new）。

`BlueprintPastePlaceSender.Send`:

```csharp
public static void Send(Guid blueprintGuid, int rotationStep, BlueprintPastePlan plan)
{
    // 置く対象のBPだけの原点を送る。サーバーが同じプランナーで再判定する
    // Send only the origins of copies to place; the server re-judges with the same planner
    var copiesToPlace = plan.EnumerateCopiesToPlace().ToList();
    if (copiesToPlace.Count == 0)
    {
        Debug.Log("[BlueprintPaste] release skipped: no placeable blueprint copies");
        return;
    }

    ClientContext.VanillaApi.SendOnly.PasteBlueprint(blueprintGuid, rotationStep, copiesToPlace.ConvertAll(copy => copy.Draft.Origin));

    // Undo は置く予定のブロックを記録する（既存のBP貼り付けと同じ粒度）
    // Undo records the blocks expected to be placed, at the same granularity as before
    var placeInfos = copiesToPlace.SelectMany(copy => copy.EnumerateElementsToPlace()).Select(ToPlaceInfo).ToList();
    var record = PlaceOperationRecord.CreateFrom(placeInfos);
    if (record.HasCells) ClientDIContext.BuildOperationHistory.Push(record);
    SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
}
```

`ToPlaceInfo(element)` は `CreateParams = BlueprintPlacementCreateParams.From(element.Settings)` の `PlaceInfo`。旧 `SendPlaceable`・`EmptySettings` は削除する。

- [ ] **Step 4: 配線ゴースト**

`AutoConnectWirePreviewRenderer` の private `WireLine` を同ディレクトリの public `PreviewWireLine`（`SetActive`・`SetColor(bool isFailure)`・`Draw(Vector3 start, Vector3 end)`）へそのまま抽出し、レンダラーはそれを使う。`BlueprintPasteLinePreview` は電線に `PreviewWireLine`、チェーンに `GearChainPoleExtendPreviewObject` と同じ2本 `LineRenderer` 表現（幅 0.05・間隔 0.1）を使う。端点は電線が `ElectricWireEndpointResolver.ResolveFromGhost(ghost, placeInfo, master)`、チェーンが `GearChainPoleExtendPreviewCalculator.GetPoleCenter(position)`。色は `copy.IsPlaced` なら可色、それ以外は不可色。線オブジェクトはプールして毎フレーム作り直さない。

- [ ] **Step 5: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPaste|AutoConnectWirePreview|ElectricWireAutoConnect"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint
git commit -m "feat: BP貼り付けプレビューをBP単位の青赤・不足tooltip・配線ゴーストにしPaste操作で送る"
```

---

### Task 9: ビルドメニューにBPの必要素材を財布込みで出す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/BlueprintPlacementTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/PlacementTargetFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/PlacementTargetResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuMaterialAvailability.cs`
- Modify（呼び出し側追随）: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetFactoryTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/UIState/Models/BuildMenuSelectionTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/PlacementHeightOffsetSingleSourceTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Shared/PlaceSystemStateControllerHeightResetTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`

**Interfaces:**
- Consumes: Task 5 `BlueprintPasteCopyBuilder.BuildUnobstructed`・`BlueprintPasteCostCalculator.CalcRequiredItems`
- Produces:
  - `BlueprintPlacementTarget(Guid blueprintGuid, string displayName, BlueprintJsonObject blueprint)`（`Blueprint` は `{ get; }`。ライブラリに無ければ null を持たせず、ターゲットを作らない側で扱う）
  - `PlacementTargetFactory.Create(PlacementTargetEntry entry, ClientBlueprintLibrary blueprintLibrary)`
  - `BuildMenuMaterialAvailability.CreateRequiredItemDtos(IReadOnlyList<(ItemId itemId, int count)> requiredItems, IReadOnlyDictionary<ItemId, int> heldByItem)`（ItemId 版の overload）

- [ ] **Step 1: テストを書く**

`BuildMenuEntryDtoFactoryTest` に次を足す（既存 157 行目の BP ケースを書き換える）:

```csharp
[Test] public void BPエントリの必要素材はBP全ブロック分で所持0なら不足Test()          // チェスト2個のBP → RequiredItems がチェスト1個分×2・Lacking=true
[Test] public void 財布の残りで賄えるセルはBPエントリでも要求しないTest()           // 財布制ブロック（ForUnitTestModBlockId.GearBeltConveyor・PlacementsPerCost=3）を3セル含むBP、残り3 → そのブロックの必要素材が0（行が出ない）
[Test] public void 無料設置中のBPエントリは支払い免除Test()                         // FreeBlockPlacement ON → PaymentWaived=true（既存ブロックエントリと同じ契約。Lacking は素材の事実のまま）
```

- [ ] **Step 2: 実装**

`PlacementTargetFactory.Create` に `ClientBlueprintLibrary` を足し、BP の場合はライブラリからBP本体を引いて渡す。引けない（一覧と本体の同期ずれ）なら `Debug.LogWarning` で理由を出し、`PlacementTargetResolver` 側でそのエントリを列挙から外す（`Create` は `TryCreate(entry, library, out target)` へ変える。2か所の呼び出しと2つのテストを追随）。

`BuildMenuEntryDtoFactory.CreateDtos` の財布問い合わせの集約点で、BP の必要素材も決める:

```csharp
// 財布へは1エントリの集約点でだけ問い合わせる。BPは貼り付けと同じ財布込み計算（回転0・重なりなしのBP1個）で要求を作る
// Ask the wallet only at this per-entry aggregation point; blueprints use the paste's wallet-aware calculation (one unobstructed copy at rotation 0)
var blueprint = target as BlueprintPlacementTarget;
var requiredItemDtos = blueprint == null
    ? BuildMenuMaterialAvailability.CreateRequiredItemDtos(target, heldByItem)
    : BuildMenuMaterialAvailability.CreateRequiredItemDtos(BlueprintPasteCostCalculator.CalcRequiredItems(new[] { BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint.Blueprint) }, walletQuery, false), heldByItem);

// 無料設置デバッグはブロック設置（BPを含む）だけを免除する
// The free-placement debug flag waives block placement only, blueprints included
var paymentWaived = (freeBlockPlacement && (block != null || blueprint != null)) || (walletStatus?.CoversNextPlacement() ?? false);
```

`BuildMenuMaterialAvailability` には ItemId 版 overload を足し、既存の Guid 版はそこへ委譲する（`MatchRequirements` 経由。判定は1か所）。`BlueprintPlacementTarget.CreateRequiredItems()` は空配列のまま（BP の必要数はこの集約点だけが出す。理由をコメントに書く）。

- [ ] **Step 3: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildMenu|PlacementTargetFactory|PlacementTargetResolver|PlacementHeightOffset|PlaceSystemStateController"`
Expected: ErrorCount 0 / PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat: ビルドメニューのBPエントリに財布込みの必要素材と不足を出す"
```

---

### Task 10: unityプレイ録画テストで通し検証する

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-place-like-block.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-copy-paste-via-ui.cs`（貼り付けの期待位置を新しい原点規則へ直す）

- [ ] **Step 1: シナリオを書く**

`blueprint-copy-paste-via-ui.cs` の準備を写し、次を順に確かめる（各段でスクリーンショット）:
1. 電柱2本＋機械1台を電線でつないだ範囲と、空気の余白を含む大きめの範囲をコピーする。
2. 地面へ向ける → ゴーストの外接箱XZ中心がカーソルセル、底が地形最高点セル（ゴーストの最小座標をアサート）。
3. 既存チェストの +X 面へ向ける → 外接箱の最小 x が面の x、最小 y がカーソル段（めり込まない）。
4. E を2回 → 最小 y が +2。
5. 配線ゴースト（`PreviewWireLine` の線オブジェクト）が表示されている。
6. 素材を持たせずに貼る（`FreeBlockPlacement` OFF）→ ゴーストが不可色・クリックしても置かれない・tooltip に不足行。
7. 素材を持たせて貼る → ブロックと電線がサーバーに出る（`p.ServerService<IWorldBlockDatastore>()` で確認し、電柱同士が接続済み）。
8. ドラッグで3個並べる・素材は2個分 → 2個だけ置かれる。
9. ビルドメニューのBPエントリにホバー → 詳細サイドバーに必要素材が出る。
10. 大きいBPで60フレーム静止 → 点滅が無い（スクリーンショット6枚でゴーストが全て出ている）。

- [ ] **Step 2: 実行**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-place-like-block.cs` と既存 `blueprint-copy-paste-via-ui.cs`
Expected: result.json の全アサート PASS。録画を共有できる場所へ置き、PR 本文に載せる。

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building
git commit -m "test: BPの通常ブロック同等の設置・配線復元・素材不足の録画シナリオを追加"
```

---

### Task 11: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] moores-code-review を全ブランチ差分に対して実行し、確定指摘を反映する。判定経路・条件式に触れる反映があれば Task 10 のunityプレイ録画テストを反映後のバイナリで再実施する。

### Task 12: セッション終了可能状態にすること

- [ ] master を取り込んだ後に、#1486 後の無料設置分岐（`PlaceBlockProtocol`・`ElectricWireSystemUtil.TryConnect` の `isFreePlacement`・`PlacementTargetCatalog.IsBlockUnlocked` の第3引数）と本PRの `BlockCellPlacementExecutor`・`ServerBlueprintPasteWorld`・`BlueprintPasteExecutor` の免除の形が一致していることを確認する。食い違えば本PR側を合わせ、Task 4・6 のテストを再実行する。
- [ ] master を取り込んだ後に、セーブ版（`WorldSaveAllInfo.CurrentVersion` と各 `ISaveMigrationStep.FromVersion`）に欠番・重複が無いことを確認する。他PRが同じ版を使っていれば本PRの版を繰り上げ、本番と2つのテスト側の登録・移行テストを直して `SaveMigration` 系テストを再実行する。
- [ ] pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR未作成のまま終わるのはplan未完了である。

---

## 判断記録（ADR）

設計裁定の正本: [ADR 0077](../../adr/0077-blueprint-placement-as-one-large-block-with-wiring.md)（決定1〜9・6b はユーザー裁定。`.decisions/2026-10-09-BP*.md`）。

planning 中の判断:

- D1 判定ロジックの置き場を `Server.Protocol/PacketResponse/Util/Blueprint/Planning/` の共有純ロジック1本にする。クライアントのプレビュー色・tooltip とサーバーの拒否が同じ定義から出るため、表示と結果が食い違わない。クライアントは既に `Server.Protocol`（`PlaceInfo` 等）を参照している。出所: agent前提（ADR 0076「範囲内ブロック数はサーバーと同規則」・`.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md` の判断集約）
- D2 BP貼り付けは既存 `va:blueprint`（`BlueprintProtocol`）に `BlueprintOperation.Paste` を足して載せる。同じBPドメインはプロトコル1本・Operation で分岐する（2026-07-07 BP設計でCRUD3本案が1本化へ修正された前例・`FilterSplitterStateProtocol` の union 型 Request＋static factory と同形）。既存 `va:placeBlock` はブロック単位で払える分だけ置くため BP単位の一括拒否を表せない。200行制約は実処理を `BlueprintPasteOperationHandler` へ委譲して守る。出所: シミュレーター予測→本体適用（moorestech-principles「1プロトコル＝1ドメイン」）
- D3 1セル設置（財布の計画→`TryAddBlock`→支払い確定、無料設置は支払いだけ免除）を `BlockCellPlacementExecutor` へ抽出し、`PlaceBlockProtocol` と `BlueprintPasteExecutor` が共用する（Task 4）。出所: ユーザー裁定 2026-10-09 原文「1486マージした」（#1486 待ちで共通化の前提が満たされた）。[[2026-10-09-BP貼り付けと通常設置は1セル設置の実行器を共用する]]
- D4 配線の復元は `ElectricWireSystemUtil.TryConnect`（#1486 後は `isFreePlacement` を取り、無料設置ではコスト0で記録）と `GearChainSystemUtil.TryConnect`（無料扱いなし＝常に払う）を使う。プランナーの総素材も同じ扱い（無料設置中はブロックと電線を払わず、チェーンだけ払う）にして判定と実行を一致させる。出所: agent前提（`.decisions/2026-10-09-無料設置中は電線ツールの手動配線も素材不要にする.md` と #1486 の実装）
- D5 歯車チェーンの接続一覧は `IGearChainPole` に公開されていないため、作成時は BP内の後方ブロックとの `TryGetChainConnectionRecord` 総当たりで拾う（インターフェースを広げない。BP内のチェーンポール数は小さい）。出所: agent前提
- D6 未解放ブロック・未解放線種を含むBPは「置けない」側（BP丸ごと拒否・赤）に寄せる。ADR 0077 決定6の原子性（中途半端な工場を作らない）と同じ理由。出所: agent前提
- D7 ビルドメニューの必要数は `.decisions/2026-08-28-ビルドメニューの素材不足判定はホストで行い財布残りは不足としない.md` に合わせ、貼り付けと同じ財布込みの `CalcRequiredItems`（回転0・重なりなしのBP1個）で数える。財布への問い合わせは `BuildMenuEntryDtoFactory` の集約点に乗せ、無料設置中はブロック・電線を免除するが、有料チェーンが残るBPでは `PaymentWaived=false` としてチェーンの支払額を表示する。完全免除のBPだけ `PaymentWaived=true` とし通常の必要数を案内する（Lacking は素材の事実のまま）。出所: シミュレーター予測→本体適用（Phase 2.6 強3の解消。ADR 0041 決定「財布制ブロックは残り≥1なら不足なし」との整合）
- D8 地形が取れない原点は `BlueprintPasteCopyState.GroundNotFound` として plan に入れ、判定・表示・送信はこの状態1本から出す。理由行は既存の `AddGroundNotFound` を使う（通常設置の `PlacementGroundFollowStep` と同じ扱い）。出所: シミュレーター予測・確信高→前提宣言（拒否権つき）
- D9 セーブ版 4→5 は他の進行中PRが同じ版番号を使うと起動時に `SaveMigrationChain` が例外で落ちる。Task 12 で master を取り込んだ後に `CurrentVersion` と `FromVersion` の欠番・重複を必ず確認し、衝突していればこちらの版を繰り上げる。出所: agent前提（AGENTS.md セーブ形式の例外規定）
- D10 ちらつきは原因未実測のため、Task 1 は計測→仮説確定時のみ修正、外れたら観測値を報告して止まる。通常設置ゴーストとの比較ログも取る。出所: agent前提（moores-grill-with-docs §1.2「推測の原因の上に直し方を裁定しない」。解消そのものはADR 0077 決定9のユーザー裁定「大きいBPだとチカチカする」）
- D11 unityプレイ録画テストを Task 10 に含める（設置位置・入力・プレビュー表示・サーバー反映の通し確認が必要なランタイム挙動のため）。
- D12 サーバー状態同期の掲載（moores-* reviewer の paths 該当ファイル）: BPの配線は既存の BP一覧同期（イベント＋初期データ＋購読の3点セット）に `BlueprintPacketDto.cs` の `BlueprintMessagePack` へ Wires/Chains を足し、行DTOを `BlueprintLineMessagePack.cs` に置いて相乗りさせる（新規イベントは作らない。BPデータの可変点は作成・削除で既存イベントが既に発火するため）。出所: agent前提（既存 BP一覧同期の前例）
- D13 新設の判定型（`Planning/` 配下9ファイル）は `Server.Protocol/PacketResponse/Util/Blueprint/Planning/` に置き、クライアントとサーバーが同じ定義を使う（D1）。プランナーは状態を確定してから不変の結果（`BlueprintPasteCopyPlan` は `SetState` を持たない）を返し、実行は `BlueprintPasteExecutor.cs` がその状態を読んで行う「指示を返す」形（裁定 `.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md`・[[2026-10-09-BP貼り付け判定の結果は状態確定後に不変で作る]]）。「置く対象」の述語（`IsPlaced`・`EnumerateElementsToPlace`・`EnumerateCopiesToPlace`・`IsPaymentWaived`）は結果側に持たせ、送信・表示・実行で同じ判定を繰り返さない（Phase 2.6 弱③のユーザー裁定）。世界の問い合わせはサーバー実装 `ServerBlueprintPasteWorld.cs` とクライアント実装 `ClientBlueprintPasteWorld.cs` に分ける。出所: agent前提＋ユーザー裁定（不変化・述語集約）
- D14 Paste は既存 `BlueprintProtocol.cs`（`va:blueprint`）に操作を足し、実処理は `BlueprintPasteOperationHandler.cs` へ委譲する。送信口は `VanillaApiSendOnly.PasteBlueprint`（`BlueprintRequest.CreatePasteRequest`）。拒否理由は `BlueprintFailureReason` に `PasteCostShortage`・`PasteNotUnlocked`・`PasteLineFailed` を足し、通知キーは既存 `denied.blueprint.<Reason>` にそろえる。財布の照会を使うため `ConstructionWalletService.cs` の `GetQuery` を public にする（判定の正は財布サービスのまま。呼び出し側で正規化・算術をしない）。出所: シミュレーター予測→本体適用（D2 と同じ）
- D15 外部入力の定義域を閉じる: 移行ステップは blocks 要素の型と offsetX/Y/Z の存在を変換前に全件検証して `Failed(reason)` を返し（途中まで書き換えない）、Paste リクエストは `RotationStep` 0〜3・`Origins` 1〜`MaxOrigins`(64) 以外をログ付きで拒否する。出所: ユーザー裁定 2026-10-09（Phase 2.6 弱②の採用）。上限 64 は agent前提（ドラッグ列の実用長を十分に覆う値）

### 本PR外のリファクタ提案（第3バケツ）

- 既存 `moorestech_client/.../PlaceSystem/Util/ConstructionMaterialShortageReporter.cs`（`GetRequiredCostSets × RequiredItems`）／新設 `BlueprintPasteCostCalculator.CalcRequiredItems` のブロック分 ／ 同じ「セル数→コストセット→必要素材」の式。
- 既存 `Game.Construction/Wallet/ConstructionWalletUtil.CalculateRequiredCostSets` ／ 旧案 `CalcFullRequiredItems` の再実装 ／ 本PRで解消（`CalcFullRequiredItems` を消し財布込み計算1本に統一・D7）。
- 既存 `Game.World/DataStore/WorldBlockDatastore.IsOverlapExistingBlock`（private）／新設 `ServerBlueprintPasteWorld.IsOverlapping` ／ 同じ占有範囲の重なり判定ループ。
- 既存 `PlaceSystemUtil.TryGetRayHitPlacePointBySize` ／新設 `BlueprintPasteOriginResolver.TryResolveCursorOrigin` ／ 同じレイキャスト→セル解決の手順（違いは側面の縦補正だけ）。
- 既存 `Ground/PlacementGroundFollowStep.FollowGround` ／新設 `BlueprintPasteRunBuilder.BuildOrigins` の追従条件 ／ 同じ「地面ヒットかつ列軸がYでない」規則と失敗の扱い。
- 既存 `PlacementTargetCatalog.IsEntryUnlocked` の接続ツール節・`ConnectionLinePickResolver`・`ConnectToolCatalog` ／新設 `ServerBlueprintPasteWorld.IsConnectToolUnlocked`・`PlacementTargetResolver.IsConnectToolUnlocked` ／ 接続ツールの解放判定の規則 ／ 本PRで解消（「無料設置でも免除しない」に統一）。
- 既存 `PlaceBlockProtocol.PlaceBlock`（財布計画→追加→支払い確定・免除分岐）／新設 `BlueprintPasteExecutor.PlaceElement` ／ 同じ1セル設置 ／ 本PRで解消（`BlockCellPlacementExecutor` へ抽出・D3）。
