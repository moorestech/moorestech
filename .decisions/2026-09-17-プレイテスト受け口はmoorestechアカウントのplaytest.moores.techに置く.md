# プレイテスト受け口は moorestech アカウントの playtest.moores.tech に置く

決定: プレイテスト受け口（Cloudflare Worker `moorestech-playtest-receiver` と R2 `moorestech-playtest`）は、sakastudio@moores.tech の Cloudflare アカウントに置き、固定ドメインを `https://playtest.moores.tech` とする。ADR 0061 / plan D・E・H で `playtest.tar-atari.com` と書いていた箇所はすべてこれに置き換える。tar-atari.com（taratari610@gmail.com のアカウント）側には受け口を作らない。

理由: moorestech のサービスは moorestech のアカウントとドメインに寄せたい（ユーザー裁定 2026-09-17）。Workers の custom domain はゾーンを持つアカウントの Worker にしか付けられないため、アカウントを移すとドメインも移すことになる。Task 9 は未デプロイだったので、Cloudflare 上に消すものは無かった（Worker・DNS・R2 とも tar-atari 側に存在しないことを確認済み）。

運用: この Mac mini では wrangler を認証プロファイルで叩く。`tools/playtest-receiver` は `moorestech` プロファイルに `wrangler auth activate` で紐付ける。`CLOUDFLARE_API_TOKEN` がシェルにあるとプロファイルが効かない。
