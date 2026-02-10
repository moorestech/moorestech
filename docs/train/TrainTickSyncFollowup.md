# Train Tick Sync Follow-up TODO

このメモは、サーバー/クライアントtick完全同期に向けた後続タスクを記録するためのものです。
現PRでは実装せず、後続PRで対応します。

## TODO: RailGraph/TrainUnit hash検証の分離と統合運用

- 1回のhash通知サイクルで、`RailGraph` と `TrainUnit` を別hashとして同時に検証できる形にする
- `RailGraph` 側hash不一致時は、`RailGraph` 用のスナップショット取得経路で再同期する
- `TrainUnit` 側hash不一致時は、`TrainUnit` 用のスナップショット取得経路で再同期する
- 片方のみ不一致でも、もう片方の正常系進行は止めすぎないように制御する

## TODO: RailGraphのtick追従適用

- `TrainUnit` と同様に `RailGraph` も「目標tickに到達した時点で適用」へ寄せる
- 即時適用ではなく、tick境界で適用できるようにイベント適用ポイントを整理する
- hash検証とtick進行の責務を分離し、再同期時に破綻しない状態遷移にする

## Notes

- 実装時は既存の `TrainUnit` 側tick追従実装との責務対称性を優先する
- 実装順は「状態モデル整理 -> hash検証経路整理 -> 適用タイミング統一」の順を推奨する
