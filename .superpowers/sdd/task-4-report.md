# Task 4 レポート: サーバーCombinedTest（リプレース設置）

## Status
COMPLETE — 6ケース実装・全PASS・コミット済み。

## Commits
- `88a079e96` test(server): リプレース設置のCombinedTest

## 成果物
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ReplaceBlockPlaceTest.cs`（正常系: Step1-3）
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ReplaceBlockPlaceEdgeTest.cs`（異常系: Step4-6）
- `PlaceBlockProtocolTestSupport.cs` にヘルパ追加（`CreateReplacePayload` / `AssertRequiredItemsCount` / `OccupyAllInventorySlots`）

ブリーフは1ファイル指定だったが、6テスト＋ローカル関数で200行を超えるため正常系/異常系の2ファイルに分割。共有ヘルパは既存の `PlaceBlockProtocolTestSupport` に集約した。

## テストケースと検証内容
| Step | テスト | 実assert |
|---|---|---|
| 1 同型向き変え | `同型向き変えで素材増減なくDirectionだけ変わる` | GearBelt North→East。BlockId不変・Direction==East・必要素材1セット不変（返却+再消費で相殺） |
| 2 進行率維持 | `搬送品の進行率がリプレース後も維持される` | BeltにItemId2投入→5tick進行→East置換。新Beltに同ItemIdが存在し進行率差が1スロット(0.25)以内 |
| 3 異種差額精算 | `異種置き換えで旧コスト返却と新コスト消費が精算される` | SmallGearBelt(X=0)→GearBelt(Y): Y消費でインベントリ空。続けてGearBelt→SmallGearBelt: Y返却で素材復帰。消費・返却の両方向を実assert |
| 4 非ファミリー拒否 | `非ファミリーブロックへのリプレースは拒否される` | MachineIdへ置換要求→BlockId==Machine不変・必要素材1セット不変（未消費） |
| 5 満杯失敗 | `インベントリ満杯時は旧ブロックと搬送品が保持される` | 全スロットをItemId1で占有→置換失敗。旧BlockのDirection==North不変・搬送品ItemId2が旧Beltに残存・コスト素材未増加 |
| 6 通常設置不変 | `通常設置は既存ブロックをスキップして挙動が変わらない` | IsReplace=falseで別ブロックを同セルへ→既存BeltConveyorが不変・素材未消費 |

## テスト実行結果
`uloop run-tests --filter-value "ReplaceBlockPlace"` → TestCount 6 / Passed 6 / Failed 0。
コンパイル: Success（Errors 0）。

## 実装バグ検出
なし。Task 1-3の実装（`BlockReplaceService` / `PlaceBlockProtocol`）は6ケース全てで期待通り動作。テスト側の自己修正のみ:
- `IBlock.GetComponent<T>` は拡張メソッド（`Game.Block.Interface.Extension`）・`ItemId` は `Core.Master` の using 追加漏れ→修正
- Step6 で `CreatePlaceBlockPayload` が `Vector3Int(x,y)`（z=0）を生成し設置座標(z=64)と不一致になる罠を発見→非リプレースPlaceInfoを直接構築し座標一致を保証

## 設計上の留意（記録）
- テスト用リプレースファミリーで RequiredItems 定義があるのは GearBelt系のみ（コスト item3+item4）。他はコスト0。差額精算(Step3)は「コスト0↔コストあり」の双方向で消費・返却の両経路を通しており、両方非0の差額ケースは対象Modに素材差のある別ベルトが無いためカバー外（ブリーフ許容範囲）。将来検証には新テストベルト追加が必要。
- Step1は「返却→再消費の往復で正味0」を検証する。会計の厳密性は消費・返却を個別assertするStep3が担保。
- DebugParameters は `DebugParametersIsolationScope` によりテスト中は隔離され `FreeBlockPlacement` は既定false。テストは非依存。

## 自己レビュー結論
各テストは「replaceが何もしなければ落ちる」assert（Direction/BlockId変化・素材増減）を持ち、assertなし・トートロジー的PASSは無し。
