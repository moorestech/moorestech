# TrainCarPlacement 要件1〜4 実装プラン

## 目的

`TrainCarPlacementDetector` で要件1〜4を必ず順番に評価し、要件3の「長さ不足で未到達だった経路のみを抽出」を正しく実装する。  
同時に、重複判定で使う `OverlapIndex` は使い回せる箇所を最初から整理して、重複実装や無駄な再計算を減らす。

## 前提方針

- 要件1→2→3→4の処理順は `TrainCarPlacementDetector` に残す。
- `OverlapIndex` のようなキャッシュも `TrainCarPlacementDetector` に残す。
- 低レベル処理（DFS実行、経路結合、近傍距離計算）はヘルパー化してよいが、最終判定フローは detector 側で読むだけで分かる形にする。
- 既存 `Game.Train` ロジック（`RailPathTracer`, `RailPositionOverlapDetector`）を再利用し、同等機能の二重実装はしない。

## 現状課題

- 要件3に必要な「指定距離まで届かなかった経路だけを列挙」が、現行 `RailPathTracer.TryTraceForwardRoutesByDfs` では取得できない。
- 要件1/4で既存 TrainUnit 全体との重複判定を複数回行うが、`OverlapIndex` 再利用の整理が不足している。
- detector 内に要件処理と下位処理が混在し、今後要件2/3を足すと肥大化しやすい。

## `RailPathTracer` 拡張（要件3専用）

### 追加方針

既存 API は維持し、要件3専用の新規 API を追加する。

- 追加メソッド案  
  `TryTraceForwardUnreachedRoutesByDfs(...)`
- 返却情報案（未到達のみ）  
  `RailPosition Route`  
  `int ReachedDistance`（開始点から実際に進めた距離）

### 挙動

- 指定距離に到達できた経路は返さない。  
- 進行先がなく、残距離がある状態で打ち切られた経路だけを返す。  
- 要件3の用途に限定し、汎用化はしない。

### 要件3での使い方

1. center から前後に `trainLength/2` で DFS（未到達経路のみ取得）  
2. 取得結果の中から center に最も近い端点（`ReachedDistance` 最小）を採用  
3. 採用端点から反対側を DFS して U 候補を列挙  
4. U と既存 TrainUnit 全体の重複を除外して U' を作る  
5. U' を `selectionStep` で選択

## `TrainCarPlacementDetector` 内の責務整理

### detector に残すもの

- 要件1→2→3→4の順次評価フロー
- `selectionStep` と route 選択状態
- 既存 TrainUnit 全体の `OverlapIndex` キャッシュ生成/再利用
- 各要件の結果を `TrainCarPlacementHit` に変換する最終責務

### detector から切り出してよいもの

- 前後 DFS の実行詳細
- route の結合・向き正規化
- 距離比較の下位ロジック
- 要件1の近傍比較で使う計算補助

## `OverlapIndex` 再利用計画

### 共通キャッシュ（1回の `TryDetect` 内）

- `allTrainUnitRailPositions`
- `allTrainUnitOverlapIndex`（既存 TrainUnit 全体）

`allTrainUnitOverlapIndex` は要件1〜4で共通利用する。

### 要件別

- 要件1  
  `requirement1ProbeIndex`（N'+M'）を作成し、まず多:多で `allTrainUnitOverlapIndex` と前判定。  
  ヒット時のみ多:1再調査へ進む。
- 要件2  
  T/T' の除外で `allTrainUnitOverlapIndex` を再利用。
- 要件3  
  U/U' の除外で `allTrainUnitOverlapIndex` を再利用。
- 要件4  
  V 生成後、最終選択 `v` の 1:多判定で `allTrainUnitOverlapIndex` を再利用。  
  必要なら V 全体の前判定（多:多）も同 index で実施。

## 実装ステップ

1. `RailPathTracer` に未到達経路専用 DFS API を追加  
2. 同 API の単体テスト追加（到達経路/未到達経路/分岐）  
3. `TrainCarPlacementDetector` を「要件フロー + 共通キャッシュ管理」に整理  
4. 要件3を新 API で実装  
5. 要件1/4の重複判定で `allTrainUnitOverlapIndex` 再利用を明示  
6. 最後に可読性調整（region とローカル関数配置）

## QA観点（バグ狩り）

- 要件1で候補ありなのに `Attach` 情報（対象unit/endpoint/facing）が欠落しないか
- 要件3で「未到達経路のみ」を抽出できているか
- 要件3の「近い端点」選択が前後で逆転しないか
- 要件4で重複時に必ず `isPlaceable=false` になるか
- Rキー切り替え時に route 選択と reverse の偶奇が崩れないか
- 既存 TrainUnit 側が動く状況で `OverlapIndex` 再利用が古いデータを参照しないか

## 作業メモ

- [x] `RailPathTracer` に未到達経路専用API `TryTraceForwardUnreachedRoutesByDfs(...)` を追加
- [x] `RailPathTracerForwardDfsTest` に未到達経路APIのテストを追加
- [x] `TrainCarPlacementDetector` で要件1→2→3→4の順次処理を実装
- [x] `TrainCarPlacementDetector` で `allTrainUnitOverlapIndex` を1回構築して再利用
- [x] 要件1の S 候補に対して既存TrainUnit重複除外（S'）を実装
- [x] 要件2の駅nodeスナップ（T/T'）を実装
- [x] 要件3の未到達経路ベース端点スナップ（U/U'）を実装
- [x] 要件4で選択経路 `v` の重複時に設置不可を実装
- [ ] Unity上で実挙動確認（要件1/2/3/4とRキー遷移）
- [x] 仕様確認: 要件2の「駅node」は `StationRef.HasStation`（駅 + 貨物プラットフォーム）扱いで確定
