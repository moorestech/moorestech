# Belt Segment Connections Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 正本のベルコン同士の接続を既存の在庫コネクタへ反映し、設置・撤去・ロードで同じ接続集合に収束させる。

**Architecture:** `BlockConnectorComponent` は座標の購読と接続集合を所有し、ドメインの置換処理をインスタンス単位のcontextへ渡す。`BeltConnectionOverride` が周辺のベルコンの面を比較し、確定したベルコン宛ての集合をプッシュする。撤去前通知は存続させ、撤去完了後の座標通知で実際のワールドを再評価する。

**Tech Stack:** C#、既存Game.Block / Game.World / Game.World.Interface、UniRx、Unity6000.3.8f1、NUnit。

## Requirements

- 正本はユーザー指定 `E:\Dropbox\seg\images\08b_vertex_patterns_ruled.png` と `08_vertex_patterns_generator/generate_vertex_patterns.py`。PythonのINPUTは頂点へ入る側、つまり上流ブロックの出力port群であり Down > Flat > Up。PythonのOUTPUTは頂点を出る側、つまり下流ブロックの入力port群であり Up > Flat > Down。この順で候補を選び、Down→UpとUp→Downを拒否する。拒否後に下位候補へ繰り下げない。図の25ケースと空を含む4voxelの256ケースを検証する。
- 接続は在庫コネクタの実際の `ConnectedTargets` に反映する。純粋な判定関数を追加するだけでは完了しない。通常・歯車ベルコン、方位4方向、分岐の複数出力に適用する。
- 設置順に依存しない。上側候補の追加で下側が外れ、撤去で下側が再評価される。各候補の入出力面が同じ境界で向き合う場合だけ候補とする。異なる高さ・面・向きを混同しない。
- 自分自身のWorld登録前に走るfactory初期化で、未登録の自分をWorldから引かない。位置・slope・コネクタ定義をcontextのctorへ明示する。
- 撤去前の `OnBlockRemoveEvent` の意味・呼び順は変えない。座標辞書・block辞書の削除後に新しい撤去完了通知を発火し、その購読内でWorldから撤去ブロックが見えないことを検証する。
- 機械などベルコン以外へ向く接続は今回の置換処理の対象外。D9の機械→segment／segment→機械、機械内部の競合を対象外とする回答に従い、既存ポート位置による接続経路を利用する。歯車・流体の汎用コネクタにベルコンの語彙を入れない。
- 図で保証される範囲は1cell、connector offset=0、東西南北の方位、非nullの方向配列と水平単位面。実マスタの16種類はコネクタ条件を満たすが、単一cell設置のShift回転は縦向きにもなる。Q7が未回答のため、この段階の新規則は保証範囲内のベルコン同士だけに適用し、それ以外の組は既存接続経路を維持する。混在境界全体の上位1組保証や縦向きsegment完成を主張しない。
- tick/seq、アイテム搬送Core、セーブ形式、クライアント同期、描画はこの接続段階では変更しない。全体goalは未完了。ADR0069のQ2はD9で回答済み、その他の未回答事項を推測で確定しない。
- `uloop compile`、焦点EditModeテスト、実ゲーム起動のEditModeInPlayingTestを通す。旧搬送テストを新接続仕様の期待値にしない。

## Global Constraints

- 作業先はユーザー明示の `C:\Users\5080\Documents\GitHub\moorestech`。CoreのDraft PR1399を基点とする `codex/belt-segment-connections`、開始HEAD `05d133d497d247567cd7f3473357e69b509d1aad`。fetch済みorigin/masterは `d6a4d7d3189ce245786bca1dc64a050c8bee6b2c`。
- 新C#は200行以下、1ディレクトリのコード10ファイル以下。partial / Func / デフォルト引数禁止、コメントは日英。Unity YAMLと.metaは手書きしない。Library削除禁止。
- PR1134の指定commitは配置の参考。疑似関数・static context・初期化前self参照を含むため直接適用しない。
- UnityはクライアントEditorが起動中。CLIは `C:\Users\5080\Documents\ChatGPT\uloop-native-3.2.2\uloop.exe`。外部repoのrevisionは本repoのpinへ復旧済み。CEF復旧作業の再実行は不要。
- 作業開始時に存在する `_CompileRequester.cs` 2件、runner-pin、ShaderGraphSettingsの変更はこのplanの対象外。TestsのConnectOverrideフォルダを利用する場合、そのフォルダの既にUnityが生成した.metaは追加対象になる。Game.Block.Interface/Component/ConnectOverride.metaは今回の実装先とは異なるため追加しない。無関係なファイルをまとめてstageしない。

## 配置と前例

| ファイル | 責務・前例 |
|---|---|
| `moorestech_server/Assets/Scripts/Game.Block/Component/ConnectOverride/IConnectorConnectionOverride.cs` | 一般的な接続集合の置換契約と内部の何もしない実装。受益者は既存connectorとベルコンcontext。Game.Block内に閉じ、WorldからBlockへの循環参照を作らない |
| `moorestech_server/Assets/Scripts/Game.Block/Component/ConnectOverride/BeltConnectionOverride.cs` | 自分のdescriptorとWorld読み取りを使う具体的な接続集合の所有者。PR1134 InventoryContextの役割を担当 |
| `moorestech_server/Assets/Scripts/Game.Block/Component/ConnectOverride/BeltConnectionGeometry.cs` | コネクタの向きとslopeから整数座標の面を構成。既存BlockDirection.ConvertLocalCellへ回転を委譲 |
| `moorestech_server/Assets/Scripts/Game.Block/Component/ConnectOverride/BeltConnectionPort.cs` | block・入出力コネクタ・境界・外向き法線・slopeを持つ不変値。整数座標で境界を照合 |
| `moorestech_server/Assets/Scripts/Game.Block/Component/ConnectOverride/BeltConnectionSelector.cs` | 片側ごとの上側選択→禁止ペア判定。選択後に形状互換性を確認し、失敗しても下位候補へ戻らない |
| `moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorComponent.cs` | 現行購読と接続集合を保持。contextをインスタンスで受け取り、追加観測座標と撤去完了を購読 |
| `moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorCandidateMatcher.cs` | 既存のペア照合をそのまま抽出しconnectorを200行以下に保つ。gear previewも現在の静的TryJudgeConnectから委譲 |
| `moorestech_server/Assets/Scripts/Game.World.Interface/DataStore/IWorldBlockUpdateEvent.cs` | 撤去完了のグローバル・座標別IObservableを追加 |
| `moorestech_server/Assets/Scripts/Game.World/WorldBlockUpdateEvent.cs` | 既存Subject辞書方式で撤去完了を配信・最終購読解除時に座標を除去 |
| `moorestech_server/Assets/Scripts/Game.World/DataStore/WorldBlockDatastore.cs` | 既存の撤去通知・Destroy・辞書削除の後に完了を発火 |
| `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Transport/VanillaBeltConveyorTemplate.cs` | 通常ベルコンのcontextを明示生成して渡す |
| `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Transport/VanillaGearBeltConveyorTemplate.cs` | 歯車ベルコンの在庫contextだけを生成。歯車コネクタは現在の経路 |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/ConnectOverride/` | 判定表、ワールド設置撤去・順序、撤去完了通知の3テストファイルと期待値fixture |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BeltConnectionOverrideInPlayingTest.cs` | 実ゲームを起動し、上位候補の設置・撤去でサーバーの実接続を検証 |

購読が既に接続の駆動元であるため、その機構に参加する。現行ペアに対するフィルタだけでは、監視していない上側候補の追加・撤去を受け取れず、下側への再接続も構成できない。周辺座標をcontextから渡し、集合の再構成まで同じcontextへ閉じる。既存コネクタを別の並行システムで止める方式は採らない。

## 操作の死活表

| 操作 | この段階の結果 | 根拠 |
|---|---|---|
| ベルコン設置・撤去・既存ロード | 新しい接続集合を反映 | 同じfactoryと座標購読を通る |
| 機械搬入出 | 既存の接続経路を継続 | contextはベルコン宛ての集合だけを置換 |
| 歯車・流体接続とgear preview | 現行規則を継続 | default contextと既存matcherを使用 |
| セーブ・tick・seq | 現行経路を継続 | このplanは形式・tick駆動を変更しない |
| 新segment搬送・描画 | 全体goalの後続作業 | PR1399 Coreはまだワールドへ接続されていない |

### Task 1: 在庫コネクタに正本の面判定と再評価を実装する

**Files:** 上のFile StructureのC#各パス、同テストディレクトリの `BeltConnectionRuleTest.cs`、`BeltConnectionWorldTest.cs`、`BlockRemovalCompletedEventTest.cs`、期待値 `BeltConnectionExpectedCases.json`。

**Interfaces:**

```csharp
internal interface IConnectorConnectionOverride<TTarget> where TTarget : IBlockComponent
{
    IReadOnlyList<Vector3Int> ObservationPositions { get; }
    void ApplyTo(Dictionary<TTarget, ConnectedInfo> connectedTargets);
}
// 既存3引数ctorは内部default contextへ委譲。default引数は増やさない。
internal BlockConnectorComponent(
    IReadOnlyList<IBlockConnector> inputs, IReadOnlyList<IBlockConnector> outputs,
    BlockPositionInfo position, IConnectorConnectionOverride<TTarget> connectionOverride);

// 具体contextはfactoryと同じGame.Blockアセンブリ内に閉じる。
// Keep the concrete context inside the same Game.Block assembly as its factories.
internal BeltConnectionOverride(BlockPositionInfo position,
    BeltConveyorSlopeType slope, InventoryConnects connectors);

// 既存interfaceに追加。remove前通知はそのまま。
IObservable<BlockRemoveProperties> OnBlockRemovalCompleted { get; }
IObservable<BlockRemoveProperties> GetBlockRemovalCompletedEvent(Vector3Int subscribePos);
```

- [ ] **Step 1: 独立した期待値と焦点テストを作る。** 25ケースは正本図の採用ペアを定数fixtureとして書く。256ケースは上左・下左・上右・下右をEmpty/Flat/Up/Downとした中央境界に限定し、正本Pythonの `classify_pattern` をoracleにして期待値を生成・固定する。生成元pathと分類規則をfixtureコメントまたはテストの説明に残す。描画コードと旧Demoの搬送テストは実行しない。

  中央境界の高さを1、上セルy=1・下セルy=0とする。上流候補は上セルFlat/Downと下セルUp、下流候補は上セルFlat/Upと下セルDown。片側が空の場合は非接続であり、Emptyを受け付けないPython関数へ渡さない。期待値生成で実装側geometryを呼ばない。

```csharp
// 代表ケース。各行を4方位、通常/歯車、設置順で展開する。
// Example cases, expanded across orientation, belt kind and placement order.
[TestCase("Down+Up", "Up+Down", "none")]
[TestCase("Flat+Up", "Flat+Down", "Flat->Flat")]
[TestCase("Up", "Down", "none")]
[TestCase("Up", "Flat+Down", "Up->Flat")]
```

  ワールドテストはServerDIと実際のfactoryで生成する。既存 `ForUnitTestModBlockId.GearBeltConveyor`、`TestGearBeltConveyorUp`、`TestGearBeltConveyorDown` を使い、必要な通常slope fixtureは新しい期待値と同じ条件で構築する。上側候補追加→下側切断、上側候補撤去→下側接続、禁止上側候補あり→下側へfallbackしない、接続した後に無関係ブロックを置いても集合不変、撤去済みcomponentが集合に残らない、双方の設置順を検証する。分岐は3面を独立に検証する。

  上側がshape不適合で下側が適合する場合も非接続になることを実Worldで検証する。Flat+Up→Flat+Downの4台を上側先／下側先で設置し、実際のsave/loadを通した別Worldで全接続集合が一致すること、ロード後の上側撤去で下側が再接続することを検証する。既存flat→機械だけの保存テストでは代用しない。200行制約に合わせ、テストはRule/World/SaveLoad/Fixture/Eventの責務別に分けてよい（各ディレクトリ10ファイル以内）。

- [ ] **Step 2: 境界の選択を実装する。** 方向配列の高さ違いは同じ物理面として重複を除去し、元のconnector識別子を保持する。平坦の面高さはセル底面、上りは入力0/出力1、下りは入力1/出力0。高さはcell基準。水平境界中心の2倍座標で厳密に照合する。

```csharp
var localOffset = new Vector3Int(horizontalDirection.x, 2 * height - 1, horizontalDirection.z);
var boundary = position.OriginalPos * 2 + Vector3Int.one
    + position.BlockDirection.ConvertLocalCell(localOffset);
// 例: 北向きでcell(0,0,0)のflat出力は(1,0,2)。
// Example: a north-facing flat output at cell(0,0,0) is (1,0,2).
```

  同じ境界で法線が逆向きの出力群・入力群を別々に選択する。候補の有無は相手と接続できるかより先に判定する。図の優先順で選んだ一組だけに山谷拒否とshape互換を適用する。flatの複数入出力は各面別。回転は既存関数に統一する。図が規定するNorth/East/South/Westを必須検証範囲とし、他方向を黙って別の方位へ補正しない。

  上記の検証範囲は図に定義されたNorth/East/South/Westであり、既存の設置操作すべてを指さない。自己と相手の両方が保証範囲内の組だけを置換する。同じeligibility判定を候補列挙・既存集合の削除・追加に共用する。対象外selfのcontextは何も置換しない。対象内selfから対象外targetへの旧接続は残し、対象外portは新優先選択の候補に含めない。対象外と対象内が混在した境界では旧リンクと新リンクの並存があり得るため、その競合仕様はQ7の未完了事項として残す。対象外を新規則の非接続へ黙って変換しない。対象外を示す理由はブロックcontext構築時に一度開発者向けログへ出し、再評価ごとのログにはしない。

  portは `ownerCell / slope / connector / boundary / outwardNormal` を保持する内部不変値にし、inputとoutputは別のコレクションへ入れる。まだWorld未登録のself portへ `IBlock` を要求しない。選ばれた相手のownerCellからWorld上のIBlockとIBlockInventoryを解決し、ConnectedInfoを作る。slopeは現在の `BeltConveyorSlopeType` を使い、この段階で類似enumを増やさない。rule/geometry/portの公開APIをテストのために作らず、factory経由のConnectedTargetsを検証する。

  周辺の列挙は自分・自分の上下と、各出力の水平隣接セルの高さ-1/0/+1だけに限定する。全ワールドを各connectorごとに走査しない。実際の出力面のない向きは観測対象へ増やさない。自己descriptorはctor値を使い、隣のdescriptorはWorldの実ブロックから作る。候補blockは重複排除する。

- [ ] **Step 3: 汎用connectorへcontextを接続する。** `TryAddDefaultTarget` を既存OnPlaceBlockから分離し、追加観測座標が旧出力辞書にない場合でも例外を出さない。`OnPlaceBlock` は次の一経路にする。

```csharp
private void OnPlaceBlock(Vector3Int changedPosition)
{
    TryAddDefaultTarget(changedPosition);
    _connectionOverride.ApplyTo(_connectedTargets);
}
```

  default contextは何もしない。具体contextは自己と相手の両方が保証範囲にある既存ベルコン接続だけを取り除き、正本で確定した組を `ConnectedInfo` として追加する。機械宛てや対象外target宛てを消さない。既存出力の観測座標とpre削除購読は維持し、対象外リンクの撤去も取りこぼさない。初期登録・追加観測座標・撤去完了から同じApplyToを呼ぶ。生成したばかりの自分がWorld未登録でも正しく初期接続でき、Worldに登録後の自座標通知でも同じ結果になるようにする。200行を超える既存matcherは既存gear previewの公開関数を保って抽出する。

  対象外self、対象外target、混在境界で上位の対象外portを候補扱いしないこと、対象外target撤去で旧リンクが消えることを実Worldテストで検証する。非zero offsetのfixtureでも同じ段階分離を検証する。これは任意geometryの完成テストではなく、確定領域の実装が未確定領域を破壊しないための検証である。

- [ ] **Step 4: 撤去完了通知を実装する。** `WorldBlockUpdateEvent` の既存座標Subject方式に従う。`WorldBlockDatastore.RemoveBlock` の既存pre通知・Destroy・3辞書の削除後、return trueの直前に完了通知を呼ぶ。テストではpreで対象が存在し、completedでは存在しないこと、失敗したRemoveBlockは通知しないこと、占有セル別通知と購読解除を検証する。contextの追加監視座標でcompletedを購読し、実Worldから再選択する。`OnRemoveBlock` の既存pre段での対象除去は保持する。

- [ ] **Step 5: 通常/歯車factoryから明示的にcontextを渡す。** slopeの解釈は具体側に置き、共有コネクタはslopeを知らない。templateが保持しているparam・位置・定義を渡す。BlockTemplateUtilや生成schemaへベルコン判定を追加しない。未初期化のselfBlockを渡さない。

- [ ] **Step 6: Unityコンパイル・焦点テスト・自己レビュー後にコミットする。** nativeCLIでplain compileし、regex `BeltConnection|BlockRemovalCompleted|OrderedShapeCandidateConnection|BlockPlaceToConnectionBlock|BlockConnectionSaveLoad` を実行する。gearの公開preview matcherと流体の既存焦点テストも、抽出した共通照合の影響を検証する。変更された仕様に反する旧ベルコン専用の期待値は新仕様に置換し、機械・流体・歯車を巻き込んで削除しない。Unity生成metaを揃える。

### Task 2: 実ゲーム起動で接続の切替を検証する

**Files:** Create `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BeltConnectionOverrideInPlayingTest.cs`。

- [ ] **Step 1: editmode-in-playing-testスキルの既存起動テンプレートに従う。** `EnterPlayModeUtil()`、直下の `EnterPlayMode(expectDomainReload: true)`、`LoadMainGame`、`ExitPlayMode`を用いる。稼働ワールドへ図のFlat+Up→Flat+Downの4ブロックを配置し、実際のConnectedTargetsから上側ペアだけを確認する。上側の片方をRemoveBlockし、実Worldと接続集合の再評価結果を確認する。再配置で元の集合へ戻ることも確認する。移植前アイテムの見た目を新Coreの完成条件として扱わない。

  実ゲームのserver tickは別threadで走るため、設置・撤去・接続集合読み取りを既存 `TickEndPacketQueue` の `ITickEndPacketEntry` としてserver threadへ渡す。結果は完了通知を介してテストへ戻し、Unity側でassertする。テストのためにproductionのpublic APIを増やさない。責務を分ける必要があれば同ディレクトリの `BeltConnectionWorldTestEntry.cs` にテスト専用entryを置いてよい。接続切替は一つのentryで順に実行し、各段階の結果を保持する。待機にはタイムアウトを設ける。実ゲーム用テストmasterには「直進高速ベルトコンベア」「上り高速ベルトコンベア」「下り高速ベルトコンベア」が既にあり、Tests.Moduleのunit-test専用GUIDを流用しない。
- [ ] **Step 2: plain compile後、CLIのEditModeでこのクラスを実行する。** Domain Reload中は45秒待ち、同じ生きた実行ハンドルを監視する。成功をログの静かさで推定しない。ゲーム起動が既存依存の問題で失敗したら実際の例外を調べて復旧し、テストのアサーションを除去して通さない。
- [ ] **Step 3: コンパイル、テスト、git diff --checkの結果を記録してコミットする。**

### Task 3: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] Core PR1399との差分だけを主たる変更範囲としてレビューする。Core既存契約は参照し、完成済みCoreのレビューを最初から重複実行しない。新しい判定経路を修正したらTask2を変更後のバイナリで再検証する。

### Task 4: セッション終了可能状態にすること

- [ ] pr-createスキルで接続PRを作成し、Core PR1399を前提にすることを明記する。レビュー単位を分離するためbaseは `codex/belt-segment-migration`、Coreがmerge済みならmasterとする。masterとの相反変更がないことを確認し、必要な取り込み・コンパイル・pushを行う。全変更をコミット・pushしPRがレビュー可能であることを確認する。全体のsegment移植goalを完了扱いにしない。

## 判断記録（ADR）

- [ADR0069](../../adr/0069-belt-segment-simulation.md) のユーザー裁定D1が図と接続配置、D2が搬送Coreを規定する。本planはD1のうちベルコン同士の接続を実Worldへ反映する独立段階。未回答事項の答えを仮造しない。
- 出所: agent前提（PR1134のInventoryContextという役割、AGENTS.mdのドメイン分離、既存BlockConnectorComponentの座標購読）。汎用部分は観測と集合、具体contextは面の選択を担当する。staticな状態contextは使わない。
- 出所: agent前提（WorldBlockDatastore.RemoveBlockの実際の通知順）。既存pre通知を維持し、completed通知を追加する。World全体を一時的に偽装する除外lookupや次tickのポーリングは導入しない。
- 出所: ユーザー裁定D9（機械の搬入出はsegmentに対して行い、機械内部の競合は考慮しない）。既存ポート位置で接続先を決めるのはagentの実装判断。今回のcontextは機械宛ての要素を編集しない。
- 出所: agent前提（実装の依存関係）。汎用hook単体を成果物にせず、Task1で具体利用・新仕様テストまで一緒に実装する。実ゲーム接続の検証はTask2。
- 出所: agent前提（変更は接続集合であり、入力・画面・GPUを変更しない）。今回は録画付き通し検証の代わりに実ゲームを起動するEditModeInPlayingTestを選ぶ。全体切替時のunityプレイ録画テストは別途必要。
- 出所: agent前提（single-cell pathはShift回転の12方向を保存し、正本図は4方位の面を規定する）。Q7を非同期で質問した。回答が届くまで保証領域内のペアだけを置換する段階に限定する。この段階分離を旧仕様維持のユーザー裁定にしない。
