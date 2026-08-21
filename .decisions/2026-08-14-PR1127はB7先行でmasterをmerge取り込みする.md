決定: PR #1127 の CONFLICTING 解消は A の実装より先に行う。順序は B7（moores-code-review 3ファイルとugui廃止計画をPRから除外）→ master を merge で取り込み → moorestech_master pin 統合 → A → B残り → C → D → 検証。方式は rebase ではなく merge
棄却案: ①rebase で取り込む ②A〜D を全部やってから最後に取り込む
理由: git merge-tree の実測で衝突は7ファイルのみ。master 側は Mining|Outcrop|Skit|Challenge|MapObject|Pin を1ファイルも触っておらず手掘り実装との意味的衝突が無い。①は32コミット×74コミット差で同じ解消判断が繰り返される。②は書き換え後のOutcrop系ファイルの上で解消することになり実装判断と解消判断が混ざる。B7を先頭に置くと SKILL.md と model_map.json の衝突2件が消えて7→5件になる
リンク: MapVeinObjectDatastore.cs の modify/delete は master の AABBフォールバック修正(5c81dcabd)が OutcropGameObjectDatastore.SelectOutcropPosition に移植済みのため削除採用 / [[2026-08-14-PR1127の残り7件は独立レビュー推奨案どおりとする]] / [[2026-08-04-露頭の地表未解決はAABB高さフォールバックで設置する]]
