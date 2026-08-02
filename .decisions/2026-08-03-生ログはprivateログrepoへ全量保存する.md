決定: ../moorestech_logs (private兄弟repo)を新設し、Claude/CodexのセッションJSONLをhookで自動退避する。過去分（moorestech関連Claude約2.6GB+cwd一致Codex rollout）も全量backfillする
棄却案:
- 今後の分のみ保存 (今日までの意思決定の生ログがローカルJSONL頼みのまま。ディスク故障・ログローテで消える)
- 過去分はgzipアーカイブ/orphanブランチ隔離 (grep性が落ちる。private repoなので容量以外のデメリットが小さい)
- 対話のみ抽出して保存 (tool payload=AIが編集した内容こそ考古学の主材料。抽出は情報を落とす)
- 匿名化・gzip (private前提で不要。plainの方がgit delta圧縮とgrepが効く)
理由: 蒸留層のみで始めた前段の裁定を、フルバイブコーディング移行を機に解除。致命的不具合の事後検証材料を最大化する。
リンク: [[2026-08-02-考古学基盤は蒸留層のみから始める]]（保留していた生ログ層を導入する更新）
