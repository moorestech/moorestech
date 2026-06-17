# ベルトコンベア自動立体交差 設計ドキュメント

作成日: 2026-06-17
対象: moorestech クライアント側ベルトコンベア設置（ドラッグで引く処理）

## 1. 目的

ベルトコンベアをドラッグで引くとき、経路上に交差するベルトコンベアやその他の機械があれば、
自動で立体交差（オーバーパス）を生成して障害物の上を跨ぎ、目的地へ到達できるようにする。
複数の障害物が任意の配置で並んでいても、立体交差で目的地に到達できるなら交差せずに引けるようにする。

## 2. 合意した要件

- **上下方向**: 上に跨ぐのみ（下にくぐる挙動は実装しない）。
- **跨ぐ高さ**: 跨ぐ障害物（連続する障害物スタック）の上端 +1 まで上げる。どんな高さの機械でも跨げる。
- **水平経路**: 現状の L 字経路（最初のドラッグ方向で一軸 → 直角 → もう一軸）を保つ。
  水平方向に迷路探索で回り込むことはしない。高さだけで跨ぐ。
- **対象ケース**: 始点と終点の高さが違うドラッグでも対応する。
- **跨げない場合**: 当該区間を設置不可表示（`Placeable=false`）とする。
  既存のプレビュー赤表示・設置時スキップの仕組みに乗せる。

## 3. 現状把握

### 3.1 既存の経路計算

- `Client.Game.InGame.BlockSystem.PlaceSystem.Common.CommonBlockPlacePointCalculator`
  （`moorestech_client/.../PlaceSystem/Common/CommonBlockPlacePointCalculator.cs`、約 376 行）。
- `CalcPositionsForConveyor()` が XZ 平面の L 字経路を生成し、その後
  **始点と終点の高さが違う場合にのみ** Y を上下させるランプ処理を行う。
  「角は水平」「最初のブロックは水平」等の特殊処理があり、既存テストがこの形状に依存している。
- `CalcPlaceDirection()` が各セルの `Direction` / `VerticalDirection` を隣接セルの差分から決定。
  コーナーは強制 Horizontal。
- `CalcPlaceable()` が `_blockGameObjectDataStore.IsOverlapPositionInfo()` で既存ブロックとの
  重複を判定し `Placeable` を設定。
- **始点と終点が同じ高さ（平地、大多数のケース）では現状 Y は完全に水平のまま**で、
  経路上の障害物を検知・回避する機能は存在しない。

### 3.2 サーバー側

- `Server.Protocol.PacketResponse.PlaceBlockFromHotBarProtocol` が `PlaceInfo` のリストを受け取り、
  各 `PlaceInfo`（`Position`=Y含む座標, `Direction`, `VerticalDirection`）ごとに設置する。
- `BlockId.GetVerticalOverrideBlockId(verticalDirection)`（`BlockMasterExtension`）で
  Up / Down / Horizontal バリアントを自動選択。
- **したがって本機能はクライアントの経路計算だけで完結し、サーバー変更は不要。**

### 3.3 ベルトの昇降ジオメトリ

- 既存テスト（`CommonBlockPlacePointCalculatorTest`）より、各ベルトブロックは 1×1×1 で、
  Up / Down は 1 セルあたり Y を 1 マスだけ対角に昇降する。
- プレハブは `_Straight, _L, _R, _T, _Cross, _Up, _Down` のみ。
  「コーナー + 勾配」の複合バリアントは存在しない。

## 4. 核心アルゴリズム: 垂直プロファイル包絡線（envelope）

水平 L 字経路（XZ）で各セル `P[0..n-1]` を求めた後、各セルのベルト高さ `beltY[i]` を
以下の制約を満たすよう決定する。

### 4.1 制約

1. **端点固定**: `beltY[0] = startPoint.y`、`beltY[n-1] = endPoint.y`。
2. **勾配制約**: `|beltY[i+1] - beltY[i]| <= 1`。
3. **障害物クリア下限**: 各セル列で基準 Y から連続する障害物スタックの上端を調べ、
   `needY[i] = 障害物スタック上端 + 1`。障害物が無ければ `needY[i] = 基準Y`。
   - 基準が空で上空に浮遊ブロックがある場合は連続スタックが無いので `needY[i] = 基準Y`
     （上昇せず下を通る。衝突しないので正常）。
4. **下限値の合成**: 各セルの最終下限 = `max(既存ベースライン beltY[i], needY[i])`。
   ここで「ベースライン」は既存パイプライン（`CalcPositionsForConveyor` の
   端点高さランプ処理を含む、変更しない）が出力した Y プロファイル。

### 重要: 冪等な後段レイヤーとして重ねる

包絡線は既存パイプラインを**作り直さず、後段に重ねる**。
既存ベースラインは既に slope≤1 を満たす有効なプロファイルなので、
障害物が無い場合の包絡線出力はベースラインと完全に一致する（恒等変換）。
これにより**既存の挙動・既存テストは値を一切変えずに全て通る**。
障害物が intrude する straight 区間でのみ Y が上昇する。
端点高さ違いとの合成も、下限値を `max(ベースライン, needY)` にするだけで自然に吸収される。

### 4.2 2 パス包絡線

「下限値・隣接 ±1・端点固定」の古典的問題。最小の `beltY` を 2 パスで求める。

- 左 → 右: `Y[i] = max(needY[i], Y[i-1] - 1)`
- 右 → 左: `Y[i] = max(Y[i], Y[i+1] - 1)`

これにより、障害物の手前で必要なだけ自動的に登り（勾配制約が登り長を強制）、
上を水平に渡り、先で下る、という立体交差プロファイルが
**障害物が複数あっても任意配置でも**自然に生成される。
始点 / 終点の高さ違いも、端点を固定下限にするだけで同じ式に吸収される。

### 4.3 跨げない場合のフォールバック

端点まで勾配を戻しきれない、または登った先のセルにもブロックがある場合、
そのセルは既存の `CalcPlaceable`（重複チェック）で `Placeable=false` となり、
プレビューで赤表示・設置時にスキップされる。

## 5. コーナー制約とエッジケース

### 5.1 コーナー制約

「コーナー + 勾配」の複合バリアントが無いため、**高さ変化はストレート区間でのみ起こす**。
コーナーセル（L 字の曲がり角）は必ず水平で、その前後セルも同じ高さ（平坦な踊り場）にする。

実装方針: 包絡線計算後にコーナー前後 3 セルが平坦でなければ、その 3 セルを共通高さ
（3 つの最大値）に引き上げて踊り場化し、登り / 下りはストレートの手前 / 先に逃がして
再度包絡線を流す。それでも端点へ戻しきれなければ当該セルを `Placeable=false`。

### 5.2 その他のエッジケース（QA 対象）

- 障害物が端点に近すぎてランプ長が足りない → 端点へ戻せないセルは設置不可表示。
- 登った先の高い位置にもブロックがある（オーバーハング）→ `CalcPlaceable` で当該セル設置不可。
- 基準が空で上空に浮遊ブロック → `needY = 基準Y`、上昇せず下を通る。
- 複数高さの機械（2 マス以上）→ スタック上端まで探索し、その分ランプが伸びる。
- 大型ブロック（`isLargeBlock`）→ 既存どおりコンベア配置自体が無効、本機能の対象外。
- L 字の向き（`isStartZDirection`）→ 既存どおり最初のドラッグ方向で決定。
  向きを変えて障害物を避けることはしない。

## 6. ファイル分割と変更点

既存の `CalcPositionsForConveyor` / `CalcPlaceDirection`（端点高さランプの特殊処理を含む）は
**一切変更しない**。障害物回避は後段の冪等レイヤーとして新規追加する。
新規ディレクトリ `Common/ConveyorOverpass/` を作成（1 ディレクトリ 10 ファイル以下）。

| ファイル | 責務 | 概算行数 |
|---|---|---|
| `ConveyorOverpass/ConveyorVerticalEnvelope.cs` | **純粋関数**: `Solve(int[] lowerBounds, int fixedStart, int fixedEnd, int cornerIndex)` → `(int[] beltY, bool[] feasible)`。2 パス包絡線 + コーナー踊り場化。int 演算のみで単体テスト容易 | ~120 |
| `ConveyorOverpass/ConveyorObstacleScanner.cs` | `ComputeLowerBounds(cells, baselineBeltY, isOccupied)` → `int[]`。各列を上方探索し `max(baseline, needY)` を返す | ~80 |
| `ConveyorOverpass/ConveyorOverpassRaiser.cs` | オーケストレーション: scan → solve → 各 `PlaceInfo` の `Position.Y` 更新・raise したセルの `VerticalDirection` 再計算・infeasible セルの `Placeable=false` | ~140 |
| `CommonBlockPlacePointCalculator.cs` | 既存 + 後段に `ConveyorOverpassRaiser` 呼び出しを 1 行追加。`isOccupied` probe を注入 | 既存 +~15 |

`CommonBlockPlacePointCalculator.cs` は現状 376 行で 200 行制限を超過しているが、
今回の追加は最小限（後段呼び出し 1 段）にとどめ、本格的な分割は別タスクとする
（無関係なリファクタを避ける writing-plans 方針に従う）。

### 6.1 シグネチャ変更（デフォルト値なしで全呼び出し側を更新）

- 静的 `CalculatePoint(...)` に占有判定デリゲート `Func<Vector3Int, bool> isOccupied` を追加
  （障害物列スキャン用）。テストが任意の障害物配置を注入できるようにする。
- インスタンス側は `_blockGameObjectDataStore` から `isOccupied`
  （`IsOverlapPositionInfo` を 1×1×1 セルで呼ぶ）と既存 `IsNotExistBlock` の両方を供給する。
- 呼び出し元（`CommonBlockPlaceSystem`、既存テスト）を更新する。
  既存テストは `isOccupied: _ => false`（障害物なし）を渡すだけで、**期待値は変更不要**
  （包絡線が恒等変換になるため）。

### 6.2 サーバー側

変更なし（`PlaceInfo` で完結）。

## 7. テスト

- `ConveyorVerticalEnvelopeTest`（新規・EditMode 純粋ロジック）:
  単一障害物（高さ 1）、複数連続障害物、高さ 2 の機械、端点至近で跨げない → 不可フラグ、
  コーナー上の障害物 → 踊り場、浮遊ブロック、障害物なし → 恒等変換。int 配列で厳密に検証。
- 既存 `CommonBlockPlacePointCalculatorTest`:
  `isOccupied: _ => false` 引数を追加するのみ。**期待値は変更しない**（恒等変換のため全て通る）。
- **ランタイム PlayMode テスト**（EditModeInPlayingTest）:
  機械を跨ぐ立体交差を実際に設置 → サーバー tick → アイテムが始点から終点まで通過することを確認。
  形状が正しくても接続が繋がらなければ無意味なため、これが QA の本丸。

## 8. 実行手順メモ

- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`。
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "..."` で限定実行。
- EditModeInPlayingTest はドメインリロードを起こすため、実行後は待機して TestResults.xml を確認。
