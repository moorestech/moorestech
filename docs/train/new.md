# Train Rail Segment Update Notes / レールセグメント更新メモ

## Changed Digest (EN)
- Added RailSegmentId (canonical pair id) and RailSegment (segment + rail item type) on server and client.
- Replaced client rail object id (ulong) usage with RailSegmentId + RailSegmentCarrier for raycast lookup.
- Added RailItemId to rail-connection snapshot/event payloads and connection edit request.
- Updated rail graph hash to include rail segment + rail item type.
- Added rail segment mapping to server/client caches, and kept connectNodes unchanged.
- Default rail item is used when a segment is missing a registered rail item type.

## 変更ダイジェスト (JP)
- サーバー/クライアントに RailSegmentId（正規化ID）と RailSegment（区間 + レール種別）を追加。
- クライアントの rail object id (ulong) を廃止し、RailSegmentId + RailSegmentCarrier に置換。
- レール接続のスナップショット/イベント/編集リクエストに RailItemId を追加。
- ハッシュ計算に RailSegment と RailItemId を追加。
- サーバー/クライアントのキャッシュにレール区間マップを追加し、connectNodes は変更なし。
- RailItemId 未登録の区間はデフォルトレール種別で補完。

## Pending Work (EN)
- Save/Load: persist RailItemId per segment in train rail save data and restore on load.
- Save/Load: ensure backward compatibility policy is not required, but verify existing saves still load or add migration if needed.
- UI: add rail item type selection on client (currently auto-picks first placeable item).
- Protocol: update any remaining tooling/tests that assume no RailItemId in rail messages.
- Tests: add coverage for segment item type persistence and removal/restore.

## 今後の実装項目 (JP)
- セーブ/ロード: レール区間ごとの RailItemId を保存し、ロード時に復元する。
- セーブ/ロード: 後方互換性は不要方針だが、既存セーブが読み込めるか確認またはマイグレーションを検討。
- UI: レール種別の選択UIを実装（現状は所持可能な先頭を自動選択）。
- プロトコル/ツール: RailItemId 追加に追従できていないツールやテストの更新。
- テスト: レール種別の保存/復元と削除/再作成のテスト追加。
