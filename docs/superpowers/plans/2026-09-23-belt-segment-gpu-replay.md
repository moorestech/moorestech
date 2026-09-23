# Belt Segment GPU Replay Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モードは同スキルの規模ゲートに従う。ステップはチェックボックスのまま保持し、進捗は報告ファイルと台帳に記録する。

**Goal:** 共通Coreと同じ整数搬送状態をGPUで再現し、全状態構築とCPU確定差分だけで継続できる描画用計算を作る。

**Architecture:** Client.Gameの内部実装がBeltReplaySnapshotからGPU配線・列・bufferを構築し、BeltReplayTickを1回の差分uploadと段階別compute dispatchで再現する。CPU Coreは正本のまま、GPUから物理結果を読み戻さない。UnityテストだけがGPU状態をreadbackし、CPUのCaptureSnapshotと比較する。

**Tech Stack:** Unity6000.3.8f1、GraphicsBuffer、ComputeShader/HLSL、既存Game.BeltSegment、NUnit EditMode。

Base: `codex/belt-segment-replay` の `08e7c01c803cb6ed33d558cff8af3c0df8e76774`。2026-09-23にfetchしorigin/master `3fecf603f17897c96a34bd3a84863c220c993b64` を含むことを確認。作業branch `codex/belt-segment-gpu-replay`、ユーザー指定のメインcheckoutで続行する。

**現在の検証と全体進捗（2026-09-23）:** CEF 0.6.1の通常Editor環境は復旧済み。source修正後の計算単位の焦点EditMode実GPUテストは26/26成功・skip0（`postfix-gpu-tests.json`、完了2026-09-23T14:24:28.7736446Z）。同source修正後の単独増分compileはerror0/warning0（`postfix-compile.json`）。その後のコメントのみのC#短縮を受けた最新の単独増分compileは成功・error0/warning14（`postfix-comment-compile.json`）。14件はすべてGPU変更対象外パスで、BlockPreviewBoundingBox 4件、SkitManager 1件、GearBeltConveyorStateChangeProcessor 1件、UnitGenerator 5件、EditModeInPlayingTestUtil 3件。さらに以前のTask1 full compileはerror0/warning39で、39件は変更対象外パスの既存警告として別の履歴に残す。これらはGPU上の整数再現の検証であり、Core→World/機械→tick/seq維持のclient replay→GPUインスタンシングとトポロジー全再構築を通した新規ワールドの実プレイは未完成。今後のshader修正後も実GPU照合を再実行する。

## Requirements

- R1: CPU snapshotのNormal/Merge/Branch、容量・速度・順序付き接続・列の種類/距離・buffer占有・RRから全GPU状態を構築し、空graphも生成/破棄できる。
- R2: Links→Outputs→Inputs、各配列内の登録順をCPU Graphと一致させる。異なるsegment ID順、同時ready/accept、非0RRで固定期待値を検査する。
- R3: CPU確定tickの速度変更・外部ready集合・成功output集合・境界搬入を差分uploadする。毎tickの全列/全配線uploadをせず、外部フラグは当tickにresetする。重複positive IDは冪等、速度変更は同IDの最後の値が勝つ。
- R4: clear/apply→Normal入口空き保存→Collect→Reserve→Transfer→AdvanceNormal→Normal commit→外部搬入の順でdispatchを完了する。0速度、同tickbuffer搬入後の前進、RRの開始index+1をCoreと一致させる。
- R5: D11/D12のNormal接続はtick前の実空きで全長の受入を判定し、全Normal前進後の現在状態へ搬入する。自己接続32→992と満杯停止を固定値で検査する。
- R6: 種類ID0を空扱いしない。buffer占有とNormal staged長を別状態にし、GPUに個体GUIDや機械インベントリを追加しない。列の種類と距離の順序をCPUと比較する。
- R7: トポロジー変更のGPU操作は旧ownerを破棄して新snapshotから全生成する。partial rebuildを追加しない。再生成直後とその後の連続tickがCPUと一致する。
- R8: 有効なCore構成で各phaseの書込先が一意となることを記述し、self link/輪/合流競合を実GPUで検査する。test用readbackを製品の物理判断へ使わない。
- R9: graphics資源と複製したComputeShaderをownerが破棄する。論理0件に対する物理bufferは最低1要素とし、0group dispatchを行わない。thread余剰分は範囲外に書かない。
- R10: このPRはGPU計算単位。実World・機械接続・新形式save/restore・wire/seq・client購読・画像テクスチャ付き箱のGPU instancing・20Hz確定tick位置描画・旧Entity切替・実ゲーム検証は全体完了の残作業として維持する。D14〜D21を後続統合へ適用し、旧セーブ変換、滑らかな補間、カスタム3Dモデル対応、実ゲーム並列化は次回とする。

## Global Constraints

- 仕様の正本は `E:/Dropbox/seg/mock/8/Core`、既存Game.BeltSegmentとD9〜D21。Coreの計算変更をこのPRへ混ぜない。D14の全liveベルト同一固定速度・歯車供給非依存・占有による負荷切替なしは後続World側の方針であり、数値速度や負荷ゼロを仮定せず、この計算単位の汎用明示速度API・GPU parity fixtureを削除しない。D10はD14で失効した履歴。
- D15に従い、挙動・残す機能・試作範囲・工数や手戻りを大きく変えるアーキテクチャの分岐は相談する。通常の実装選択は自律的に進め、動く一気通貫の新規ワールド試作と次回へ残す知見を優先する。
- D16〜D20の後続統合: 独立した旧フィルター分岐器を廃止し、新Core Branch/Mergeを維持する。新規ワールドと新形式save/restoreを対象にし、旧セーブ変換は次回、旧ファイル保持と明示的version境界を要する。ベルトの設置は水平4方向と坂に絞り、真上・真下のベルト設置だけを入力・preview・サーバー検証から除く。描画は20Hzの確定tick位置とアイテム画像テクスチャ付き箱を使い、滑らかな補間・カスタム3Dモデルは次回へ送る。
- D21の後続統合: 試作のゲームWorld runnerは `Tick(false)` で逐次実行する。実ゲームの並列化と機械在庫との並列同期は次回へ送る。Coreの並列API、Normal接続で前tick状態を参照するD11規則、既存GPU parity fixture（並列Core比較を含む）は維持する。この計算単位でゲームWorldの実行や統合完了を主張しない。
- 参考実装はMyBeltConvSegmentの確認済み `2ae915273fc0005f5b8bc759ee07470e92875219` の `uni/Assets/BeltLab`。現在のMoveLast/Loop種別は採用しない。過去1bbe206は現checkoutで解決できない。
- 参照の保存済みexport: `C:/Users/5080/Documents/ChatGPT/segment-normal-diagnostics/MyBeltConvSegment-2ae9152-reference.tar`、SHA256 `E4185B374C286158EF913AD648AE9AF0B73A1E52CA910ED0ABA24BF754AD1D04`。参考のみで製品依存にしない。
- CPU状態はCaptureの公開値から作り、Core private reflectionをしない。BeltItem.Positionをこの計算単位で更新しない。
- Client.Game/AssemblyInfo.csには既にInternalsVisibleTo("Client.Tests")がある。実装型はinternal、テストだけのpublic APIを作らない。
- 1ファイル200行以下・各ディレクトリ10コードファイル以下。partial/Funcは禁止。主要処理のコメントは簡潔な日英ペア。
- .metaはUnity生成のみ。shaderテキストとasmdef(JSON)は編集可能、Unity YAML/Prefab/Sceneは直接編集禁止。Library削除禁止。
- 既存dirty5（uloop pin、client/server CompileRequester、ShaderGraphSettings、ConnectOverride.meta）をstage/revertしない。
- UnityのCEF0.6.1環境は通常Editorで復旧済み。RTX 5090 / Direct3D12 level 12.2でsource修正後の焦点EditMode実GPUテスト26/26成功・skip0（`postfix-gpu-tests.json`）。単独増分compileはsource修正後0error/0warning（`postfix-compile.json`）、続くコメントのみのC#短縮後の最新実行は0error/14warning（`postfix-comment-compile.json`、全件GPU変更対象外パス）。以前のTask1 full compileの0error/39warningは別の履歴として保持する。GPUコード変更後と全体統合時の実GPU検証は引き続き必要。

## File Structure / Interfaces

全て新規型は `Client.Game.InGame.BeltSegment.Gpu` namespace、テストは `Client.Tests.BeltSegment` namespace。Create対象の.metaと新ディレクトリ.metaはUnityが生成後に同梱する。

| ファイル | 責務 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Gpu/GpuBeltData.cs` | HLSLと一致するint-only構造体、port/event定数 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Gpu/GpuBeltLayout.cs` | snapshotからimmutable配線とNormalリンク一覧を構築 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Gpu/GpuBeltInitialState.cs` | head0の走行列・gap・密着端点・buffer・速度へ変換 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Gpu/GpuBeltBuffers.cs` | GraphicsBuffer生成、初期upload、所有/破棄 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Gpu/GpuBeltTickUpload.cs` | 再利用配列へ確定tickの差分をpack、冪等/最終速度の集約 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Gpu/GpuBeltSimulation.cs` | shader複製、kernelのbinding、段階順序と範囲dispatch |
| `moorestech_client/Assets/Resources/BeltSegment/BeltGpuReplay.compute` | kernel宣言とhelper include |
| `moorestech_client/Assets/Resources/BeltSegment/BeltGpuData.hlsl` | int-only ABIとbuffer宣言 |
| `moorestech_client/Assets/Resources/BeltSegment/BeltGpuQueue.hlsl` | Enqueue/Dequeue/Advanceの整数列操作 |
| `moorestech_client/Assets/Resources/BeltSegment/BeltGpuPorts.hlsl` | offer/receiveとsource readiness |
| `moorestech_client/Assets/Resources/BeltSegment/BeltGpuExternal.hlsl` | clear/applyとtick後の外部搬入 |
| `moorestech_client/Assets/Resources/BeltSegment/BeltGpuJunction.hlsl` | Collect/Reserve/Transfer |
| `moorestech_client/Assets/Resources/BeltSegment/BeltGpuNormal.hlsl` | 入口snapshot/通常前進/staged commit |
| `moorestech_client/Assets/Scripts/Client.Tests/BeltSegment/GpuBeltLayoutTest.cs` | packing、登録順、0種類/0graphの固定期待値 |
| `moorestech_client/Assets/Scripts/Client.Tests/BeltSegment/GpuBeltReplayTest.cs` | 実GPUの固定距離/RR/段階境界 |
| `moorestech_client/Assets/Scripts/Client.Tests/BeltSegment/GpuBeltReplayBoundaryTest.cs` | 合流登録順・RR・部分groupの固定期待値（既存要件を200行内のファイルへ分割） |
| `moorestech_client/Assets/Scripts/Client.Tests/BeltSegment/GpuBeltReplayScenario.cs` | 実ポートの確定結果を作る連続運転fixture |
| `moorestech_client/Assets/Scripts/Client.Tests/BeltSegment/GpuBeltReplayReadback.cs` | テストのみのGetDataと論理列比較 |

Modify: `moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef` と `moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef` に `Game.BeltSegment` referenceを追加する。他の参照/設定は保持する。

### ABI

すべて `[StructLayout(LayoutKind.Sequential)]` のintフィールド。bool/nullable/managed参照をGPUへ送らない。Layoutの配列とlogical countはinternal readonlyとしてbuffer ownerが消費する。

```csharp
internal struct GpuBeltTopology
{
    public int Offset, Capacity, Kind, FirstInput;
    public int InputCount, FirstOutput, OutputCount, NormalLinkIndex;
}
internal struct GpuBeltPort { public int Kind, Id, Direction, Unused; }
internal struct GpuBeltState { public int Head, Count, TotalGap, PriorityIndex; }
internal struct GpuBeltBufferState { public int HasItem, ItemKind, PriorityIndex, Unused; }
internal struct GpuBeltNormalLink { public int Source, Target, InputDirection, Unused; }
internal struct GpuBeltNormalState { public int AvailableSpace, Length, ItemKind, Unused; }
internal struct GpuBeltEvent { public int Kind, Id, Value, Extra; }
```

strideはTopology32、他16、gap/block/item/speed/予約/外部フラグ4。CPU enum `BeltSegmentKind` のNormal/Merge/Branch数値とGPU定数の対応をpackingテストで確認する。port KindはSegment=0、Buffer=1、External=2。inputは搬入元を表し、outputは搬出先（内部はSegment、機械edgeはExternal）。Directionはinput側ならInputDirection、output側ならOutputDirection。

Event KindはReadyInput=0、SuccessfulOutput=1、Speed=2、Insertion=3。Idはそれぞれinput index/output index/segment index/input index、Valueは1/1/speed/length、Extraは0/0/0/item.ItemId。buffer占有はHasItemで判定し、ItemKind=0を許す。

`GpuBeltLayout(BeltReplaySnapshot snapshot)` は `GpuBeltTopology[] Topology`、`GpuBeltPort[] InputPorts/OutputPorts/ExternalInputs`、`GpuBeltNormalLink[] NormalLinks`、`int TotalCapacity/OutputCount` をinternal readonly fieldとして所有する。ExternalInputsはinputId順で、Kind=Segment、Id=target segment index、Direction=input directionとする。外部input数はExternalInputs.Length、segment数はTopology.Lengthから得る。`GpuBeltInitialState(BeltReplaySnapshot snapshot, GpuBeltLayout layout)` は `GpuBeltState[] States`、`GpuBeltBufferState[] Buffers`、`int[] Gaps/Blocks/Items/Speeds` をinternal readonly fieldとして所有する。

製品計算ownerの署名:

```csharp
internal sealed class GpuBeltSimulation : IDisposable
{
    internal GpuBeltSimulation(BeltReplaySnapshot snapshot, ComputeShader shader);
    internal void ApplyTick(BeltReplayTick tick);
    public void Dispose();
}
```

実装では上の宣言を本体付きにする。buffer ownerはinternal readonly fieldとして計算と後続描画が共有する。test-only Capture/Readbackメソッドをこの型に足さない。Testsは既存friend境界から実bufferをGetDataする。shaderは呼出側がResources.Load<ComputeShader>("BeltSegment/BeltGpuReplay")でロードして渡し、ownerが複製したinstanceだけを破棄する。

## Task 1: snapshotをGPUの配線と整数列へ変換する

**Files:** 上表のGpuBeltData.cs、GpuBeltLayout.cs、GpuBeltInitialState.cs、GpuBeltLayoutTest.csをCreate、Client.Game/Client.Tests asmdefをModify。

**Consumes:** `BeltReplaySnapshot`、kind factory、`BeltItemState.Item/DistanceToExit`、公開enum `BeltDirection`。Core内部の `BeltDirections` は参照しない。

**Produces:** 上記ABIのCPU配列。Task2はこのlayoutとinitial stateをそのままuploadする。

- [ ] **Step 1: ABIと配線を実装する**

segment indexをそのままGPU IDとする。容量の累積でlane Offsetを確定し、未登録NormalLinkIndexは-1。各segmentのinput/outputリストを構築時だけ持ち、Linksの順でsource/targetのportを追加、次にOutputs、最後にInputsを追加する。リストをsegment index順にflattenしFirstInput/FirstOutputとCountへ変換する。外部input表は元配列のindexを保持する。

```csharp
foreach (var link in snapshot.Links)
{
    var sourceKind = snapshot.Segments[link.SourceSegmentId].Kind;
    int sourcePortKind = sourceKind == BeltSegmentKind.Normal ? 0 : 1;
    int inputDirection = (int)link.OutputDirection ^ 1;
    sourceOutputs[link.SourceSegmentId].Add(new GpuBeltPort
        { Kind = 0, Id = link.TargetSegmentId, Direction = (int)link.OutputDirection });
    targetInputs[link.TargetSegmentId].Add(new GpuBeltPort
        { Kind = sourcePortKind, Id = link.SourceSegmentId,
          Direction = inputDirection });
    if (sourceKind == BeltSegmentKind.Normal && snapshot.Segments[link.TargetSegmentId].Kind == BeltSegmentKind.Normal)
    {
        topology[link.SourceSegmentId].NormalLinkIndex = normalLinks.Count;
        normalLinks.Add(new GpuBeltNormalLink { Source = link.SourceSegmentId,
            Target = link.TargetSegmentId, InputDirection = inputDirection });
    }
}
```

OutputsはKind2/Id=output indexをsourceOutputsへ、InputsはKind2/Id=input indexをtargetInputsへ加える。Normal outputは最大1、Merge buffer outputは1、Branchは2〜3というCoreのvalid graph契約を維持する。decoderの未実装をここで承認しない。

- [ ] **Step 2: logical snapshotからhead0の列を作る**

物理ring headの元の値は不要。Itemsの先頭順にslot offset+iへ置く。最初のgapはdistance、それ以降はdistance−previousDistance−256。gap和をTotalGapへ、CountをItems.Lengthへ。密着ブロックのstartとendへsizeを書き、不要な内部セルは0のままでよい。MergeのState.PriorityIndexはsnapshot値、BranchのBuffer.PriorityIndexはsnapshot値、その他は0。

```csharp
int previousDistance = -BeltConstants.ItemWidth;
int blockStart = 0;
int blockSize = 0;
for (int i = 0; i < state.Items.Length; i++)
{
    var item = state.Items[i];
    int gap = item.DistanceToExit - previousDistance - BeltConstants.ItemWidth;
    gaps[offset + i] = gap;
    items[offset + i] = item.Item.ItemId;
    totalGap += gap;
    if (i == 0 || gap != 0) { blockStart = i; blockSize = 1; }
    else blockSize++;
    blocks[offset + blockStart] = blockSize;
    blocks[offset + i] = blockSize;
    previousDistance = item.DistanceToExit;
}
```

HasItemはBufferedItem.HasValue、ItemKindはそのValue.ItemId、空なら0だが占有判定には使わない。Speedsはsnapshot.Speed。snapshotやPositionの配列/参照を変更しない。

- [ ] **Step 3: packingの独立期待値を検査する**

容量4・距離[32,288,800]・種類[0,7,9]ならhead0/count3/gaps[32,0,256,0]/totalGap288、密着端点[2,2,1,0]。容量0graphはすべてlogical0。2Normals→MergeをLinks逆ID順で作りInputPortsがその順になること、BranchのLinks内出力がOutputsより先であること、外部Inputsが内部linkより後であること、Normal self linkが1件抽出されることを検査する。

```csharp
Assert.That(initial.Gaps, Is.EqualTo(new[] { 32, 0, 256, 0 }));
Assert.That(initial.Blocks, Is.EqualTo(new[] { 2, 2, 1, 0 }));
Assert.That(initial.States[0].TotalGap, Is.EqualTo(288));
Assert.That(initial.Items, Is.EqualTo(new[] { 0, 7, 9, 0 }));
```

bufferに種類0を入れたfixtureはHasItem1/ItemKind0、空はHasItem0。Marshal.SizeOfのstrideを固定値で検査し、packingした値をHLSLが同じoffsetで読む前提を明示する。公開BeltDirectionのFront/Back/Left/Rightは0/1/2/3、予約なしNoneは-1であること、4方向それぞれが正しい反対方向へpackされることを固定期待値で検査する。Core internal helperを公開へ変更しない。

- [ ] **Step 4: compileと絞ったUnityテストを行いcommitする**

```powershell
& 'C:/Users/5080/AppData/Local/Programs/uloop/bin/uloop.exe' compile --project-path ./moorestech_client
& 'C:/Users/5080/AppData/Local/Programs/uloop/bin/uloop.exe' run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type class --filter-value 'Client.Tests.BeltSegment.GpuBeltLayoutTest'
```

ErrorCount0と全件成功、Unity生成metaを確認してTask1のFilesだけをcommitする。

## Task 2: GPU段階処理とCPU確定差分を実装し実GPUで照合する

**Files:** 上表の残るbuffer/upload/simulationの3C#、7shader/helper、ReplayTest/Scenario/Readbackの3test C#をCreate。Task1のABIを一方的に変更しない。必要な変更は同じtask内で対応するpackingテストも更新する。

**Consumes:** Task1のlayout/initial、`BeltReplayTick`、実CPU `BeltSimulationGraph` / `BeltReplaySimulation`。

**Produces:** 生成/ApplyTick/Dispose可能なGpuBeltSimulationと、実GPUの状態一致検証。

- [ ] **Step 1: bufferと差分packを実装する**

GraphicsBuffer.Target.Structuredで上記strideを使い、allocation count=Math.Max(1,logicalCount)。空配列をSetDataしない。初期配線/状態はconstructorでだけuploadする。SpeedsはTopologyから分離し、tickごとの変更はEventからGPU上で書く。

GpuBeltTickUploadは構築時にinput/output/segment数からbool seen配列とspeedValues配列、最大 `2*inputCount+outputCount+segmentCount` のEvent配列を確保する。Prepareごとにseenをclear、ReadyInputs/SuccessfulOutputsを各ID一度だけ記録。SpeedChangesは順にspeedValuesへ代入し、dirty index順を保持して最終値を各segment一度だけ記録する。Insertionsは入力順で記録する。Frame DTO/配線を毎tickcloneせず、新しいList/ToArrayを作らない。

```csharp
// speedValuesとspeedIds/seenSpeedsはconstructorで確保した再利用配列。
foreach (var change in tick.SpeedChanges)
{
    if (!seenSpeeds[change.SegmentId])
    {
        seenSpeeds[change.SegmentId] = true;
        speedIds[speedCount++] = change.SegmentId;
    }
    speedValues[change.SegmentId] = change.Speed;
}
for (int i = 0; i < speedCount; i++)
{
    int id = speedIds[i];
    events[eventCount++] = new GpuBeltEvent { Kind = 2, Id = id, Value = speedValues[id] };
}
```

frameはCPUが受理した完全なtickという契約。同じtargetへの成功搬入は最大1回: 長さL(1〜256)搬入後の占有長はcapacity*256−L+256、残るoffer=L−256<=0となり、搬入段階には前進/搬出がない。CPUの順序付き記録は維持し、このvalid frame不変条件によりGPU insertionの書込先は一意となる。無効frameを黙って受理するdecoderは作らない。

- [ ] **Step 2: Coreの整数列操作をHLSLへ移す**

`Game.BeltSegment/Simulation/BeltItemQueue.cs` のSetPhysicalGap/EnqueueTail/DequeueHead/Advanceを整数演算のままGPU配列offsetへ移す。参考 `uni/Assets/BeltLab/Resources/BeltSimulation.compute` のSetGap/Enqueue/Dequeue/Advanceは構造の参考になるが、採用判定は現在のCore全文と照合する。余剰threadは書く前にlogical countでreturnする。queueはItemsのGUIDでなくItemKindだけを移動し、buffer.HasItemを占有判定にする。

```hlsl
int Offer(int target, int direction)
{
    GpuBeltTopology t = _Topology[target];
    if (t.Kind == 1 && _Reservations[target] != direction) return 0;
    GpuBeltState s = _States[target];
    return t.Capacity * 256 - s.TotalGap - s.Count * 256;
}
bool Receive(int target, int direction, int length, int itemKind)
{
    int offer = Offer(target, direction);
    if (offer < length) return false;
    GpuBeltTopology t = _Topology[target];
    GpuBeltState s = _States[target];
    Enqueue(t, s, offer - length, itemKind);
    if (t.Kind == 1) s.PriorityIndex = (s.PriorityIndex + 1) % t.InputCount;
    _States[target] = s;
    return true;
}
```

Normal搬出はspeed−headGapの全長をTryReceiveし、target offerへclampしない。buffer搬出だけがmin(speed,offer)。Buffer sourceのreadyはHasItemと最優先方向だけを見て、speed0でもready照会自体はtrueになり得る。実buffer Transferはspeed0なら搬出しない。この違いを保つ。

- [ ] **Step 3: 段階と書込所有を実装する**

| kernel | 論理件数 | 読取・書込と規則 |
|---|---:|---|
| ClearExternal | max(input,output) | 当tickのready/success flagsを0へ |
| ApplyExternal | eventCount | Kind0/1/2を反映。集約済みなので同一書込先の競合なし |
| CaptureNormalOffers | normalLinkCount | targetの前進前offerを保存、staged Length0へ |
| Collect | segmentCount | Merge/Branchだけ自身のqueueをAdvance(false)、空bufferへ出口headを回収 |
| Reserve | segmentCount | Mergeだけ予約をNone=-1へreset、空ならPriorityIndexからInputPortsを走査し最初のready方向を予約 |
| Transfer | segmentCount | occupiedbufferかつspeed>0、出力をRR順に試す。成功時HasItem0、開始PriorityIndex=(旧+1)%OutputCount |
| AdvanceNormal | segmentCount | Normalだけ。内部Normal先は保存offerに全長が収まれば自身のstageへ記録。それ以外はreservedMergeか成功外部edgeを使う。自身queueをAdvance(sent) |
| CommitNormal | normalLinkCount | Length>0の記録を現在のtarget入口へ一度挿入 |
| InsertExternal | eventCount | Kind3だけExternalInputsでtarget/方向を解決し、記録長/種類を搬入 |

外部output offerは成功通知あり256/無し0。TryReceiveは成功通知のedgeだけ成功する。buffer Transferで内部候補が先なら先に試す。Reserveのbuffer sourceは、そのsourceのOutputPorts[PriorityIndex]のdirectionと要求input方向の反対が一致するかを見る。

有効なgraphでNormal/Branchのinputは1つ、Mergeは予約方向だけ受入。buffer Transferでtargetへの書込は一意。自己接続でも自身のbufferとrunning queueは別状態領域。Normal→Normalだけは別dispatchでcommitし、相手が自身を同時前進中に書かない。

```csharp
internal void ApplyTick(BeltReplayTick tick)
{
    int count = upload.Prepare(tick);
    if (count != 0) buffers.Events.SetData(upload.Events, 0, 0, count);
    shader.SetInt("_EventCount", count);
    Dispatch(clearExternal, Math.Max(inputCount, outputCount));
    Dispatch(applyExternal, count);
    Dispatch(captureNormalOffers, normalLinkCount);
    Dispatch(collect, segmentCount);
    Dispatch(reserve, segmentCount);
    Dispatch(transfer, segmentCount);
    Dispatch(advanceNormal, segmentCount);
    Dispatch(commitNormal, normalLinkCount);
    Dispatch(insertExternal, count);
}
```

Dispatchは64threads/group。0ならreturnし、65535groupsを超える範囲は `_DispatchOffset` を伴う複数dispatchへ分ける（[D3D11 Dispatch上限](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-dispatch)）。各phaseの全範囲をdispatchし終えてから次phaseへ進む。HLSLではindex=_DispatchOffset+tid.xを使う。kernel別に実際に参照するbufferだけをbindし、全bufferを全kernelへ一律にbindしない（特にRW slot数）。Build/renderer用資源はこのownerに追加しない。

Disposeは各GraphicsBufferを破棄し、ownerがInstantiateしたshaderのみDestroyする。EditModeテストではUnity Editor条件付きの末尾helperでDestroyImmediateを使用し、元のResources assetは破棄しない。

- [ ] **Step 4: 実GPUの固定期待値を検査する**

テスト開始時にSystemInfo.supportsComputeShadersがtrueであることをAssertし、未対応でsuccess/skip扱いにしない。Resourcesロードと `UnityEditor.ShaderUtil.GetComputeShaderMessages(shader)` の各severityを確認する（[Unity ShaderUtil](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/shaderutil)）。Editor APIを使うhelperはファイル末尾の `#if UNITY_EDITOR` 内に置き、各メッセージをテスト出力へ保存しErrorがあれば失敗にする。ComputeShaderにhasErrorプロパティがあるとは仮定しない。GPUreadbackはTestsのhelper内にだけ置く。physical headから順にgap+256を積算しlogical DistanceToExitを得る。CPU CaptureSnapshotと種類/距離/個数/buffer.HasItem/種類/必要RR/速度を比較する。ring headや無効slot・密着ブロック内部の古い値はCoreとの物理一致条件にせず、有効な密着runの両端sizeを独立検査する。

| fixture | 固定期待値 |
|---|---|
| 空graph、0events、Dispose再生成 | 論理0のまま、dispatch errorなし |
| Normal cap2 speed64へtick後長64搬入 | 当tick448、次tick384 |
| Normal cap4自己接続item@32 speed64 | 1tick後992 |
| Normal cap2自己接続@0/@256 speed128 | 満杯停止、順序不変 |
| Normal source@0 speed128、target入口実空き64 | 全長が収まらず拒否。128を64へclampしない |
| Merge/Branch buffer→空Normal cap2 speed64 | buffer搬入448の後、同tickに384へ前進 |
| 走行head@0のBranch speed0、空buffer | buffer回収は行い、搬出しない |
| Branch初期RR0で出力0拒否/1成功/2受入可 | 出力1だけへ移動、次RR1（成功方向+1の2ではない） |
| Merge2入力同時ready、登録逆ID順、RR0/1 | 登録順と開始RRから決まるGUID相当の固有種類を選ぶ |
| output成功→次tick通知なし | successを持ち越さない |
| 速度差分同ID128→0 / duplicate ready/output | 最後の速度0、集合指定は1回分として働く |
| 種類0がbufferと走行列を移動 | 占有/個数を保持 |
| graph129segments | 最後の部分groupで範囲外アクセスなし |

固定値は別GPU/別Graphの計算から生成しない。CPUが失敗を示すframeをGPU比較のexpectedに使わない。

- [ ] **Step 5: 長期CPU/GPU一致と全再生成を検査する**

fixtureは2Normals→Merge→Branch→外部2出力、Mergeへの追加外部入力、独立Normal→外部出力、Normal自己輪を持つ。容量3/2/1/4/3/4、速度0/64/128、実source readyと実receiver acceptをtickごとに変える。BeltSimulationGraphを実側としてtick前ready固定→Tick→成功output記録→同期TryInsertの成功を順に記録する。BeltReplaySimulationとGPUへ同じframeを適用し、CPU三者（実側/再現側/テストdecodedGPU）を毎tick比較する。

1000tick、serverParallel false/trueを各1ケース。毎73tickでGPU ownerをDisposeし、実側CaptureSnapshotから全生成する。種類IDはテスト内で一意な連番を使い、重複/消失も検知する。Core GUIDはCPU所有集合で保持を検査するがGPUへ追加しない。bufferだけ占有、停止→再開、空frameを混ぜる。GPU供給/搬出の成功記録をGPU結果から生成しない。

- [ ] **Step 6: compileと焦点検証を行いcommitする**

```powershell
& 'C:/Users/5080/AppData/Local/Programs/uloop/bin/uloop.exe' compile --project-path ./moorestech_client
& 'C:/Users/5080/AppData/Local/Programs/uloop/bin/uloop.exe' run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value 'Client.Tests.BeltSegment'
dotnet test tools/BeltSegment/Tests/BeltSegment.Tests.csproj -c Release
git diff --check
```

GPU全ケース、既存.NET100件成功、compile0error、compute error0。GraphicsBuffer.SetData/GetDataなどUnity API内の割当を未測定で0と主張しない。shaderの警告/エラーとCPU/GPU比較の先頭不一致を報告へ保存する。新metaはUnity生成を確認しTask2 filesだけをcommitする。

## Task 3: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] Replay base08e7c01に対する全変更をレビューする。Coreとの演算/phase一致、thread書込所有、HLSL/C#ABI、GPU実行証拠、全体未完了C3〜C6をcontextへ渡す。
- [ ] 修正後の対象GPUテストとcompileを行いsourceのRefixを完了する。shader変更後は実GPU比較を再実行する。

## Task 4: セッション終了可能状態にすること

- [ ] pr-createでcodex/belt-segment-replayをbaseとするstacked Draft PRを作成し、commit/pushとmergeabilityを確認する。masterとの競合を解消し必要compileを行う。
- [ ] 全体World/save/通信/GPU描画の実利用・CPU/GPU全再構築・実ゲーム検証は未完了として作業を継続する。

## 既存操作の死活表

| 操作 | 状態 |
|---|---|
| server/client CPU CoreとReplay | 同じ公開契約・物理処理を維持 |
| 既存Worldの搬送/旧Entity表示 | この単位では既存経路のまま。後続cutoverで置換 |
| GPU整数状態の再現 | このPRの実装/検証対象 |
| 実ゲームのGPU instancingと20Hz確定tick位置描画 | 全体残作業。今回の表示はアイテム画像を貼った箱とし、synthetic compute比較で完成扱いにしない。滑らかな補間とカスタム3Dモデル対応は次回 |

## 判断記録（ADR）

- [ADR0069](../../adr/0069-belt-segment-simulation.md)の「搬送」「実装境界」に従う。以下の型/phase分割はagent実装判断であり追加のユーザー回答ではない。
- GPU計算は表示用Client.Game内部。汎用Core/Server.ProtocolへUnity graphics依存を入れない。役割が同じ既存compute ownerは無く、BezierRailMeshGpuはvertex変形の前例に限る。新しいgraphics資源ownerとして明記する。
- CPU→GpuBeltSimulation→GraphicsBufferが描画用の読取側の流れ。CPUの搬送判定へ結果を戻さず、既存server機構への迂回/第2書込口を作らない。
- 値DTOをネットワーク/セーブschemaへ兼用する判断はしない。完全frame受理後にGPUへ渡すこと、generation/seq/再同期は後続client境界が所有する。
- Frameのpositive setsと最終速度だけをpackする。CPUのCore規則をGPUコードへ翻訳する必要があるため、数学的に同じ演算をGPUへ持つ。CPU側に別の搬送物理実装は作らない。
- GraphicsBufferとshaderはtopology ownerの寿命で破棄する。これはゲーム寿命singletonへの一般的dispose付与ではなく、全再構築時に繰り返し確保されるGPU資源の責務。
- ItemKindとoccupancyを分離し0も保持する。個体GUIDはCPUに残す。readbackはTestsのみ、bufferを後続rendererが読む実アクセス面をfriendテストが検査する。
- int-onlyの判別子/非該当値はGPU ABIの物理表現として閉じる。NormalLinkIndex=-1はリンク不在、NormalState.Length=0は当tick搬送なしを示し、CPUのnullableやmanaged payloadをGraphicsBufferへ持ち込まない。これらをWorld・wire・saveの型選択へ波及させない。C#/HLSLの値対応と非該当状態はpacking/実GPUテストで検査する。
- 正本のPriorityIndexとD11/D12を維持し、参考repoのMoveLast/Loop kernelは移植しない。参考repoは現在解決できる2ae9152と保存済みarchiveを使う。
- unityプレイ録画テスト/EditModeInPlayingTestをこの単位に含めない理由: ゲームWorld/描画切替がまだなく、追加するのはcomputeの整数計算入口。Unity EditModeでも実GPU dispatch/readbackを行える。ゲームへの描画接続後には録画付き実プレイを全体完了ゲートに含める。

## 事前レビュー

自己点検: R1/R2/R6はTask1とTask2固定値、R3/R4/R5/R8はTask2phaseと連続GPU比較、R7/R9は空graph/Dispose/73tick全再生成、R10は境界と閉じタスクへ対応。最小0/1構成、停止の解除、外部通知なし→次tick成功を明記。原稿の構造レビューとuser-simulator reviewを実装派遣前に実施する。

構造レビュー（2026-09-23、agent実装判断）: 強2件のNormalLinkIndex非該当値とEvent種別別フィールドは上記GPU ABIに限る表現として維持する。managed nullable/参照payloadの導入はHLSL対応のint-only境界と両立しないため、Core公開型を変更せずpackingテストで対応を閉じる。弱1件の方向導出重複は局所変数へまとめた。同時にBeltDirectionsがCore内部であることを自己点検で確認し、Clientから参照せず公開方向enumの固定対応を検査する形へ修正した。これらは追加ユーザー裁定ではない。

本PR外のリファクタ提案として報告されたCore Offer/ReceiveとHLSL Offer/Receiveの同役割について: CPU/GPU間で直接C#メソッドへ委譲できないため、GPU翻訳をCoreとの一致テストで検査する。ユーザー指定のGPU再現計算を実装する上での同一演算であり、Coreを別CPU実装へ置換するリファクタは追加しない。

user-simulator review（2026-09-23）: Critical0/Warning0/追加裁定0。Fable代替gpt-6-astra highと斥候gpt-6-sol highで実施。書込先一意の仮候補は正本READMEのNormal/Branch入力最大1・Merge各方向別という有効構成契約と照合して破棄した。反証役はthread limitで起動できず、その制限は報告へ残した。実GPUや実装の検証結果ではなく、ユーザーの承認/的中採点は未確認。4カテゴリcontext・構造レビュー・予測レポートをlogs repoの `harness/user-simulator/datasets/2026-09-23-belt-segment-gpu-replay-plan/` に保存する。
