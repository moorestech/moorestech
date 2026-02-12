# Train Tick Simulation Strategy (Draft)

## 目的

- `TrainUpdateService.UpdateTrains()` のtick処理順を明確化し、クライアントが同じtick境界で列車シミュレーションできるようにする。
- `masconLevel` を「そのtickのシミュレーション前に必須な入力(pre-sim)」として扱い、`trainunit` ごとの差分のみを1イベントに集約して送る。
- 今後追加する「シミュレーション後に反映すべき情報(post-sim)」と、pre-simをクライアント側で分離して処理できる形にする。

## 現状(As-Is)

- サーバーは `TrainUpdateService.UpdateTrains()` で `_executedTick++` の後に hash event を発火し、その後 `trainUnit.Update()` を実行している。
- クライアントは `TrainUnitHashVerifier` で hash/tick を受信し、`TrainUnitFutureMessageBuffer.EnqueueHash()` に積む。
- クライアントのevent側は `TrainUnitFutureMessageBuffer.Enqueue()` に一系統で積み、`TrainUnitClientSimulator` 内の `FlushBySimulatedTick()` で処理している。
- `va:event` はポーリング取得であり、1回のレスポンスに複数イベントがまとまる前提。

## 目標フロー(To-Be)

### サーバー `UpdateTrains()` の順序

1. `_executedTick++` の前に hash 生成と `_onHashEvent.OnNext(_executedTick)` を行う。
2. `_executedTick++` を実行する。
3. `trainunit` シミュレーションを実行する。
4. シミュレーション内で `masconLevel` が変化した `trainunit` だけ抽出する。
5. `tick + changedTrainUnits[]` を1つのバッチイベントとして配信する。

`masconLevel` 差分イベントは「pre-sim入力」として扱い、クライアントで当該tickシミュレーション前に適用する。

### クライアントの受信・適用

- tick受信は現状どおり2系統を維持する。
- hash系: hash/tickキュー(進行許可ゲート)。
- event系: tick付きイベントキュー。
- event系は位相を分離する。
  - pre-sim: 当該tickシミュレーション前に適用 (`masconLevel` 差分)。
  - post-sim: 当該tickシミュレーション後に適用 (今後追加予定)。

## `masconLevel` 差分イベント案

- EventTag: `va:event:trainMasconDiff` (案)
- Payload:
  - `Tick`
  - `Changes[]` (`TrainId`, `MasconLevel`)

要件:

- 1tickで変化があった `trainunit` だけ `Changes[]` に入れる。
- 変化がなければイベント自体を送らない。
- 1tick内の全 `trainunit` 差分を1メッセージにまとめる。

## 実装ステップ

1. サーバー: `mascon` 差分用MessagePack (`Tick + Changes[]`) を追加する。
2. サーバー: `TrainUpdateService.UpdateTrains()` の順序を「hash先行 -> tick++ -> シミュレーション」に変更する。
3. サーバー: 各tickで `masconLevel` の変化trainのみ収集し、1イベントで `AddBroadcastEvent` する経路を追加する。
4. クライアント: `va:event:trainMasconDiff` 用のNetworkHandlerを追加し、tick付きでeventキューに積む。
5. クライアント: `TrainUnitFutureMessageBuffer` を pre-sim/post-sim の2位相で扱えるよう拡張する。
6. クライアント: `TrainUnitClientSimulator` のtick進行で、`pre-sim apply -> hash gate -> simulate -> post-sim apply` の順序を保証する。
7. 検証: 「hash到着tickまで進行可」「mascon差分が同tickのシミュ前に反映される」「未変化trainが送信されない」をテストで確認する。

## 既存実装との整合メモ

- `GetTrainUnitSnapshotsProtocol` は `ServerTick` を返却済み。
- `TrainUnitCreatedEventMessagePack` は `ServerTick` を保持済み。
- `TrainDiagramEventMessagePack` は `Tick` を保持済み。
- したがって、今回の追加も「tickを必ず同梱する」方針で揃えられる。
