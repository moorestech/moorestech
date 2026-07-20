# Web UI Topic 規約

Web UI の状態同期 Topic は本規約に従う。Topic は Unity 側の状態を正とし、Web 側は受信した状態を表示用に保持する。

## Wire envelope と revision

状態 Topic の snapshot と event は共通 envelope で送信する。

```json
{
  "op": "snapshot",
  "topic": "ui_state.current",
  "revision": 12,
  "data": { "state": "PlayerInventory" }
}
```

- `revision` は非負整数で、Unity ホストの生存期間中に Topic ごとに単調増加する。
- snapshot は現在の revision、状態変更 event は revision を1増やした値を持つ。
- Web は保持済み revision 以下の snapshot/event を破棄する。同一配信の重複も破棄対象とする。
- ホスト再起動では revision が0へ戻り得る。Web は再接続の復元開始時に revision gate を世代リセットし、新ホストの snapshot を受理する。
- revision は wire envelope の責務であり、個別 payload DTO へ重複して追加しない。

## 購読と snapshot の原子性

Unity は subscribe を受けたら、先に接続を購読者として登録し、その後 snapshot を生成・送信する。この順序により snapshot 生成中の event を取りこぼさない。

event が snapshot より先に到着することは許容する。両方が同じ送信キューを通り、Web の revision gate が古い snapshot による上書きを防ぐ。購読登録前に snapshot を生成してはならない。

再接続時、Web は参照カウントが1以上の全 Topic を一括で再購読する。全 Topic の有効な snapshot が揃うまで接続状態を `restoring` とし、保持中の画面状態を表示しながら再接続オーバーレイで操作を遮断する。

## 配信頻度

- 離散状態は状態変更通知を購読して配信する。同一フレームに複数回変わる場合はフレーム末の最終状態へ集約してよい。
- 進捗、RPM、トルクなどの連続変動値は固定間隔でサンプリングする。変更のたびや毎フレーム送信してはならない。
- サンプリング間隔は Topic 実装内で明示する。`ui.progress` の標準は100msで、各区間の最終値だけを送る。
- snapshot はサンプリング周期を待たず、購読時点の現在値を返す。

## Action の冪等性

- Action は可能な限り `set`、`select`、`respond(id, result)` のような目標状態指定にする。再送で効果が重複する `increment` 形式を避ける。
- 会話送り、選択、完了処理など進行を伴う Action は対象の安定 ID または revision を payload に含める。
- Unity は現在状態の ID/revision と照合し、古い要求や処理済み要求を副作用なしで拒否する。
- Action の成功応答は状態反映の完了通知ではない。画面更新は必ず後続 Topic event/snapshot を正とする。

## Heartbeat と復元

Web は WS 接続中に5秒間隔で `{ "op": "ping" }` を送り、Unity は `{ "op": "pong" }` を返す。15秒を超えて pong が無い場合、Web は socket を閉じて通常の指数バックオフ再接続へ合流する。

切断中は Topic の表示値を消去しない。再接続後は全購読 Topic の snapshot で置換し、全件復元後に `open` へ戻す。Action の保留 Promise は切断時に失敗させ、自動再送しない。

## 新規 Topic テンプレート

1. `ITopicHandler.GetSnapshotJsonAsync()` は現在状態を副作用なしで返す。
2. 状態変化通知を購読し、`WebSocketHub.Publish(topic, json)` から event を送る。
3. 連続値なら固定周期サンプラーを置き、周期をコードと本書に明示する。
4. `protocol.ts` の `Topics`、`TopicPayloads`、zod schema registry を追加する。
5. payload fixture と、共通 revision envelope を通す両側契約テストを追加する。
6. snapshot/event の逆転、重複 revision、切断後の再 snapshot をテストする。
7. 操作がある場合は Action を冪等化し、状態反映は Topic のみに流す。
8. **e2e mock host（`e2e/mock-host/wsHandler.ts` の `topicData`）へ既定 snapshot を必ず追加する。**
   常時購読 Topic の snapshot が欠けると、再接続復元の `restoring` ゲートが解除されず
   再接続オーバーレイが全操作をブロックし、e2e スイート全体がタイムアウトする。
9. nullable なフィールドは使わない。実ホストは `NullValueHandling.Ignore` で null キーを
   ワイヤから省略するため、zod スキーマは `.optional()` が正（`.nullable()` は検証落ちする）。
