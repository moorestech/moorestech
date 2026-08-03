決定: 環境変数スクラブは内容ベース（U+FFFD・孤立サロゲート検出）を維持し、PATH/HOME等の必須変数を NeverScrubNames で除去対象外にする折衷を採る。所有者は Client.WebUiHost.Common のまま変えない。
棄却案:
- 名前ベースのリスト（既知の注入変数のみ除去）へ全面変更（誤爆ゼロだが未知の汚染源に効かない）
- 起動時グローバルスクラブの所有者を Client.Common / Client.Starter のboot側へ移す（責務は綺麗になるが今回の実害が無い）
- 現状維持（正当なU+FFFDを含むPATH/HOMEを配布Playerで毎起動・不可逆に削除しうる最悪ケースが残る）
理由: 未知の汚染源への耐性を捨てずに、最悪ケース（必須変数の丸ごと消失）だけを排除できる。変更量も最小。
リンク: docs/plans/pr-1116-independent-review-fix-plan.md B-6 B-7
