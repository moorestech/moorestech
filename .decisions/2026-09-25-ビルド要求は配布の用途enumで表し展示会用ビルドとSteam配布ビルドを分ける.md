決定: PlayerBuildRequest は BuildPurpose { Ci, LocalDevelopment, Exhibition, SteamPlaytest } を持ち、strict・ゲームデータ同梱・展示会用スクリプト同梱を用途から1か所で導く。bool は IsDevelopmentBuild だけ残す。展示会用スクリプトは Exhibition のときだけ入る。
棄却案: 4つ目の bool BundleExhibitionLaunchScript を足す／depot 定義の FileExclusion で除外しビルドは変えない／含めたまま配布する／enum は同梱物判断だけに使い strict とゲームデータ同梱の bool は残す
理由: 「Mac なら展示会用スクリプトを同梱」の暗黙分岐が Steam 配布の Mac 版で崩れる。用途ごとに意味の通らない bool の組み合わせを作らせない。
置き換え: [[2026-08-02-PlayerBuildRequestは3boolのまま維持する]]
リンク: docs/adr/0071-playtest-distribution-adds-apple-silicon-mac-depot.md
出所: ユーザー裁定 2026-09-25 原文「だとしたら、展示会モードでビルドと、一般ビルドでビルドそのもののやり方を分けるべきでは？」→ 選択「B：用途enum」→ 選択「A：用途がすべて決めboolはIsDevelopmentBuildだけ残す」
