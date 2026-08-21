決定: MapObjectDatastore.LoadMapObjectは保存済みinstanceIdがマップに存在しない場合、KeyNotFoundExceptionをthrowせず警告ログを出してスキップする。マップから消えたmapObjectのセーブ状態は無効データとして捨てる（ホットバー無効割当の「ロード時に削除」裁定と同型）。vein手掘りplanにタスクとして追加する。
棄却案:
- throw維持で旧セーブ非互換を受け入れる（進行中のmoorestech-9vt「7/23バックアップセーブ実ロード」と正面衝突する）
- セーブJSONから該当エントリを削除する移行スクリプト（ランタイム厳格維持だが、マップ側データの変更のたびに移行が要る）
理由: 鉱脈mapObject4種・インスタンス100件の削除で既存セーブ全部が即クラッシュになるため。データ破損検知としてのthrowより、マップ改訂に対するセーブの頑健性を優先する。
リンク: docs/superpowers/plans/2026-08-04-vein-hand-mining.md、bd moorestech-9vt
