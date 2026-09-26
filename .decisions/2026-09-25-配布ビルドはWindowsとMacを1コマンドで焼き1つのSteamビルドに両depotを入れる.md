決定: release-playtest.sh は同じコミットから Windows と Mac を焼き、1回の run_app_build（ビルドID 1つ）に両 depot を入れて playtest-staging へ上げる。片方でも失敗したら何も上げない。
棄却案: OSごとに別コマンド・別アップロード／既定は両方で --only windows|mac の絞り込みを持つ
理由: 「このラベル＝このコミット」を両OSで成り立たせ、playtest への手動反映も1回にするため。
リンク: docs/adr/0071-playtest-distribution-adds-apple-silicon-mac-depot.md
出所: ユーザー裁定 2026-09-25 原文「いまwindowsだけでやってるsteamデプロイパイプラインをmac版でもビルド、デプロイするようにして」→ 選択「A：1コマンドで両OSを焼き、1回のSteamアップロードに両depotを入れる」
