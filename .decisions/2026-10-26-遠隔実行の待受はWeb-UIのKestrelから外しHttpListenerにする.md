# 遠隔実行の待受は Web UI の Kestrel から外し、HttpListener にする

決定: 遠隔実行の待受を Web UI の Kestrel から切り離し、`Client.RemoteExec` が持つ `HttpListener` にする。Windows の配布ビルド（Mono・stripping 既定）で動くことは確かめた（遠隔実行レーンの Task 4）。ADR 0072 の「入口は既存の Web UI サーバーに 1 本足す。専用の待ち受けの新設は棄却」を覆し、同じ変更で ADR 0072 を改訂する。トークン・起動オプション・結果の 7 種は変えない。
棄却案: 遠隔実行専用の最小 Kestrel（Kestrel と AspNetCore 系の NuGet が遠隔実行のためだけに残る）／専用の TCP 待受
理由: 今は Web ホストの起動に失敗すると遠隔実行も開かず、検証機で CEF の不具合を調べる手段まで失う。Web 用の Kestrel を段階 5 で消せるようになり、保守面積も減る。
出所: ユーザー 2026-10-26 14:25「1 進める、2 ok」（moorestech-f2 指揮セッション）。問いの原文: 14:01 に 5 件を表（1 移行中の届け方／2 最初に移す画面／3 遠隔実行の口の置き場／4 経過時間の規約を UI のアニメーションにも当てるか／5 素の見た目のままの 4 画面）で推奨とともに説明し、意思決定事項「① ②の 5 件を推奨どおりにしてよいか（変えたいものだけ番号で）」。14:06 以降の再掲は「② 計画 §0 の 5 件は推奨どおりでよいか」。「2 ok」は 5 件を束ねた問いへの答えなので、5 件の一括承認と読む。
リンク: `docs/adr/0072-remote-exec-in-distributed-builds.md`、`docs/uitk-migration/plan.md` §0・§7.1 の裁定 3・§2.1 の 0-3、bd `moorestech-albof.2`
