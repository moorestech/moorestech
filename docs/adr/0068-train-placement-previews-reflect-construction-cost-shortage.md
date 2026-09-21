# 0068. 鉄道系の設置プレビューは建設コスト不足を設置不可として表示する

日付: 2026-09-21
状態: 採択

## Context

鉄道系の設置で、クライアントのプレビューが建設コスト不足を見ていない箇所が3つあった。サーバーはいずれも不足で拒否するため、プレイヤーには「緑表示なのにクリックしても何も起きず、理由も出ない」状態になっていた。

- (A) 橋脚の単体設置（`TrainRailPlaceSystem`／`TrainRailPlaceSystemService`）: `PlaceInfo.Placeable = true` 固定で、落とすのは地面干渉だけ。
- (B) レール接続モードの橋脚ゴースト（`TrainRailConnectSystem`）: 曲線は `TrainRailConnectPreviewData.IsPlaceable` で赤くなり送信も止まるが、橋脚ゴーストの色は (A) と同じサービスが塗るため緑のまま。
- (C) 車両設置（`TrainCarPlaceSystem`。新規編成・既存編成への追加の両経路）: `hit.IsPlaceable` は幾何判定のみ。サーバーの `PlaceTrainCarOnRailProtocol`／`AttachTrainCarToUnitProtocol` は `RequiredItems` 不足で拒否する。

## Decision

- **(A)(B)(C) の3箇所すべてを直す。**
  出所: ユーザー裁定 2026-09-21 原文「鉄道がアイテム不足でも設置可能な状態として表示される問題がある」→ 質問「修正範囲はどこまでにしますか？」→ 選択「A+B+C 全部」
  棄却案: A+B（レール/橋脚のみ。車両が緑のまま無反応で残る）／Cのみ（橋脚が緑のまま残り、接続モードで曲線が赤・橋脚が緑の食い違いが続く）
  実装判断: 不足時は赤表示・送信停止・不足素材のツールチップ行の3点を揃える。出所: agent前提（サーバーの不足時拒否と既存の設置不可理由表示に合わせる）。

- **レール接続モードの橋脚ゴーストの色は、操作全体の可否に連動させる。** 橋脚コスト不足・レール素材不足・長さ超過・曲率NG・地面干渉のどれか1つでも当てはまれば、橋脚ゴーストを赤にする。曲線は従来の曲線側判定で色を決める。橋脚自体は置ける状況（レール素材だけ不足）でも橋脚は赤く見えることを受け入れる。
  出所: ユーザー裁定 2026-09-21 質問「レール接続モードの橋脚ゴーストの色は、何に連動させますか？」→ 選択「全体の可否に連動」
  棄却案: 橋脚自身の理由だけ（橋脚が緑でもクリックで何も起きないケースが残る）／逆方向も連動させる（橋脚が地面干渉のとき曲線も赤にする）
  補足: 逆方向（地面干渉→曲線も赤）は棄却されたため、曲線の色の決定は現状のまま変えない。

- **橋脚のコスト判定は `ConstructionWalletQuery` を通す（`GetRequiredCostSets`／`GetAffordablePlacementCount`）。** 具体側の `TrainRailPlaceSystem` が財布窓口と所持インベントリを受け取り、単体設置では `TrainRailPierPlaceability` を通じて `ConstructionMaterialShortageReporter.ReportShortages` → `ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable` の順で不足報告と可否更新を行う。
  出所: agent前提（`BeltConveyorPlaceSystem` の前例・`ConstructionMaterialShortageReporter` の「Placeableを落とす前に呼ぶ」契約）

- **接続モードでは橋脚の不足行を二重に積まない。** 橋脚の不足行は既に `TrainRailPlacementFailureTooltipKey.Report` が `PierMaterialShortages` から積んでいるため、接続モードのサービス呼び出しでは不足行を積まず、色だけを `previewData.IsPlaceable` と合成して塗り直す。
  出所: agent前提（`TrainRailPlacementFailureTooltipKey` の既存実装）

- **車両のコスト判定は財布を通さず、`trainCarMaster.RequiredItems` 1セット分を `ConstructionCostShortageCalculator.Calculate` で所持と突き合わせる。** 不足行は `PlacementFeedback.AddMaterialShortages` で積み、プレビューは設置不可色、送信はしない。新規編成・既存編成への追加の両経路に同じ関門を掛ける。
  出所: agent前提（サーバー `PlaceTrainCarOnRailProtocol`／`AttachTrainCarToUnitProtocol` が `ConstructionCostService.HasRequiredItems` を直接呼んでおり財布を使っていない実装・`ElectricWirePoleGhostPart` の前例）

- **デバッグの `FreeBlockPlacement` 中のスキップは、既存の共有ユーティリティ（Reporter／Marker）が持つ分だけに従い、車両側へ新たなスキップは足さない。** サーバーの車両プロトコルがこのフラグを見ていないため、クライアントだけ緑にすると同じ不具合を再生産する。
  出所: agent前提（サーバー実装との一致。実装時にサーバー側がフラグを見ていると分かった場合はそれに合わせる）

- **検証は EditMode テスト（不足時に Placeable が落ちること・不足行が積まれること）で行う。** 色そのものはアセット側の表現なのでテストしない。
  出所: agent前提（`PlacementFeedbackMaterialShortageTest`／`TrainRailPierReservationTest` の前例）

## Consequences

- `TrainRailPlaceSystem` のコンストラクタに財布窓口と所持インベントリを追加し、VContainerが既存登録から解決する。`TrainRailPlaceSystemService` には `UpdatePreviewColor(PlaceInfo)` だけを追加する。
- 接続モードで、レール素材だけが足りないときも橋脚ゴーストが赤くなる。原因の見分けはツールチップ行が担う。
