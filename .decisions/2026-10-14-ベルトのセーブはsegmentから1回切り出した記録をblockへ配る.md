# ベルトのセーブはsegmentから1回切り出した記録をblockへ配る

日付: 2026-10-14
文脈: セーブ時に block ごとに所属 segment 全体を CaptureItems していた（O(L²)）うえ、BeltTransportSnapshot と同じ「出口までの距離→マスとマス内進行量」の射影を SaveStateBuilder が二重実装していた。満載の長い直線ベルトでは 30 秒ごとのセーブで tick スレッドに大量コピーが走る（moores-code-review C21 / D5）。

決定: 既存 BeltTransportSnapshot を block で引ける形にし、セーブ時は segment ごとに1回だけ Capture して各 block の JSON を切り出す。SaveStateBuilder の独自射影と距離↔マス換算の第3実装は削除し、換算は BeltCellLocation/BeltCellLocator 側の1対に寄せる。出所: ユーザー裁定 2026-10-14「blockから逆引きじゃなくて先にsegmentからアイテムごとの座標を割り出しblockに適応」（design.md の案B）
棄却案: SaveStateBuilder に segment 単位の Capture 結果キャッシュを持たせ、Datastore が tick・再構築・押し込みで無効化する（Capture は1回になるが射影の二重実装が残る）。
理由: Capture の1回化に加えて射影が1実装になり、クライアントの座標計算と同じ向き（segment→アイテム→block）で揃う。
リンク: moorestech_logs/harness/moores-code-review/runs/2026-10-13-2340-belt-segment-client-sync-22de6217/design.md
