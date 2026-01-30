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

## Refactor Policy (EN)
- RailSegment Save/Load target: include RailSegmentId, RailItemId, explicit length, and controlPointStrength in save data.
- Length is normally auto-calculated, but some environments force it to 0, so persist the stored length per segment.
- controlPointStrength conceptually belongs to the segment, not a node; persist it on the segment.
- RailNode Save/Load refactor: stop binding nodes to blocks by coordinates; use GUID linkage instead.
- Block save data should store RailNode GUID(s); rail save data should store RailNodes and connections keyed by GUID.
- On load, first restore blocks, then bind RailNodes by GUID and build the rail graph after block load to avoid order issues.
- connectNodes stays as the runtime/pathfinding cache (int,int) and must not be reshaped; save data should not depend on it.
- This section is a policy only; do not implement yet.

## リファクタ方針 (JP)
- RailSegment のセーブ/ロード対象は RailSegmentId / RailItemId / 明示的な length / controlPointStrength を含める。
- length は基本自動計算だが、環境によって 0 強制があるため区間ごとに保存する。
- controlPointStrength は点ではなく区間に紐づくため、セグメント側で保存する。
- RailNode のセーブ/ロードは座標で block に紐づけるのをやめ、GUID 連携に変更する。
- block 側は RailNode の GUID を保存し、レール側は GUID をキーに RailNode と接続情報を保存する。
- ロード時は block の復元後に GUID で紐づけ、block ロード中に RailNode グラフを構築しない。
- connectNodes は探索用キャッシュとして維持し、セーブデータは connectNodes に依存しない。
- ここは方針記載のみで、実装はまだ行わない。

## Pending Work (EN)
- Save/Load: persist RailItemId per segment in train rail save data and restore on load.
- Save/Load: include explicit length and controlPointStrength per segment.
- Save/Load: refactor RailNode persistence to GUID-based linkage with blocks and deferred graph build.
- UI: add rail item type selection on client (currently auto-picks first placeable item).
- Protocol: update any remaining tooling/tests that assume no RailItemId in rail messages.
- Tests: add coverage for segment item type persistence and removal/restore.

## 今後の実装項目 (JP)
- セーブ/ロード: レール区間ごとの RailItemId を保存し、ロード時に復元する。
- セーブ/ロード: 区間ごとの length / controlPointStrength を保存する。
- セーブ/ロード: RailNode の保存を GUID 紐づけに変更し、グラフ構築をロード後へ移動する。
- UI: レール種別の選択UIを実装（現状は所持可能な先頭を自動選択）。
- プロトコル/ツール: RailItemId 追加に追従できていないツールやテストの更新。
- テスト: レール種別の保存/復元と削除/再作成のテスト追加。
