失効: 2026-08-23に[[2026-08-23-レビューダイジェストの公開は裁定サイトへ一本化する]]で上書き済み。以下は当時の記録であり、現行フローではない

決定: pr-independent-review のダイジェストHTMLの外部公開は、ローカルHTTPサーバ＋`cloudflared tunnel --url` のクイックトンネル（trycloudflare.comのランダムURL）で行い、これをskillの確定フローに書き込む
棄却案: ①Artifact（claude.ai・既定private・永続・常駐不要） ②named tunnelで review.tar-atari.com 等の固定サブドメインを割り当てsupervisorへ常駐登録
理由: プロセスを止めれば即失効する寿命が「一時的に見せる」用途と一致し、DNS・ingress・supervisorの恒久設定を増やさない。②は固定URLと引き換えに常時公開・恒久設定が要る。①は最も安全だが公開先がclaude.ai側になる。なおクイックトンネルのURLは認証なしで、URLを知る全員がprivateリポジトリのソース抜粋を閲覧できる点は受け入れる
リンク: 出所=ユーザー裁定 2026-08-05（AskUserQuestion「公開方式」＝Cloudflare クイックトンネル）
