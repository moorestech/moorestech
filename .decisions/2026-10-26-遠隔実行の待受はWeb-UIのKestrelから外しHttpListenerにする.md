# 遠隔実行の待受は Web UI の Kestrel から外し、HttpListener にする

決定: 遠隔実行の待受を Web UI の Kestrel から切り離し、`Client.RemoteExec` が持つ `HttpListener` にする。Windows の配布ビルド（Mono・stripping 既定）で動くことは確かめた（遠隔実行レーンの Task 4）。ADR 0072 の「入口は既存の Web UI サーバーに 1 本足す。専用の待ち受けの新設は棄却」を覆し、同じ変更で ADR 0072 を改訂する。トークン・起動オプション・結果の 7 種は変えない。
棄却案: 遠隔実行専用の最小 Kestrel（Kestrel と AspNetCore 系の NuGet が遠隔実行のためだけに残る）／専用の TCP 待受
理由: 今は Web ホストの起動に失敗すると遠隔実行も開かず、検証機で CEF の不具合を調べる手段まで失う。Web 用の Kestrel を段階 5 で消せるようになり、保守面積も減る。
出所: ユーザー 2026-10-26「2 ok」（指揮セッションが平易に説明した計画 §0 の残り 5 件〈届け方・最初に移す画面・遠隔実行の口・UI の演出の時間・素の見た目の 4 画面〉を推奨どおりにするかの問いへの回答）
リンク: `docs/adr/0072-remote-exec-in-distributed-builds.md`、`docs/uitk-migration/plan.md` §0・§7.1 の裁定 3・§2.1 の 0-3、bd `moorestech-albof.2`
