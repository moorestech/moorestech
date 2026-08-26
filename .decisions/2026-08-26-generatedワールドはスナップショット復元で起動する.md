決定: generatedワールドのworldIdを `seed:生成マスタ指紋:generator版` から導き、共有キャッシュをワールドスナップショット（world.json/map.json/terrain/visual）として新規作成時にコピー復元する。ビルドにはvisual込み（+1.2GB）で同梱する。共有キャッシュ内の現在IDと異なる旧ワールドは起動時に自動削除する。

棄却案: world本体（map.json+terrain 約80MB）だけ同梱してvisualは初回に焼く案（先焼き40sが初回に残る）。旧キャッシュを消さず別タスクへ送る案（55GBリーク継続）。

理由: seed196固定・マスタ同一なのにcreatedAt由来IDで毎回ミスしていた。実測 pass-1≈17s / 先焼き≈40s が丸ごと消える。

出所: ユーザー裁定 2026-08-26 原文「このまま両方サクッと実装して」「急いでるから重要なやつだけ」→ 選択「visualも同梱（+1.2GB）」「起動時に自動削除」

リンク: docs/adr/0037-generated-world-content-id-and-snapshot-restore.md
