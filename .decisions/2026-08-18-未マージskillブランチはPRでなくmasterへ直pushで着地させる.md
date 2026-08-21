決定: 13日間未マージだった `chore/pr-review-digest-tunnel-flow`（2026-08-05裁定3件の実装）と、ローカルmasterに直接載っていたcodex結論回収コミットは、PRを立てずmasterへ直接pushして着地させる
棄却案: ①両方をPRにして独立レビューを通す ②branchのみPR・ローカルmasterのコミットだけ直push ③pushせずローカル保留
理由: 対象がskill定義（.agents/skills）とその衝突解消に限られ、プロダクトコードを含まない。レビュー往復のコストに対して得られるものが薄く、放置期間が長いほど再衝突が増える
リンク: 出所=ユーザー裁定 2026-08-18（AskUserQuestion「着地方法」＝master直push）
