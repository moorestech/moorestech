# ブロックの並び順は研究解放と nodeGraph から独立算出する

決定: blocks.json の sortPriority と配列順は、master-refine 操作 A が items と同じ規則（initialUnlocked 先頭→研究 depth→nodeGraph の研究の行(y)・行内 x→各研究内の列(x)・列内 y→孤立は末尾、閾値 100、100 始まり 10 刻み）で、blockGuid と研究の unlockBlock の対象から items とは独立に算出する。複数の研究が解放するブロックは研究順で最初の研究にだけ置く。block→item の参照や同名の推測は使わない。tier9 の apply は既存行の sortPriority を保持し、refine の結果を再 apply で壊さない。
棄却案: A（block の並び順を spec の明示値で手管理し、apply の keep-sort を外す。各 spec で手管理になり研究順と食い違う）。C（発射台・核融合炉・拠点の 0 を維持する。新施設が建築メニューの先頭に出る未達を認めることになる）。旧規則（block の itemGuid が指す item の値を写す）は、全ブロックが itemGuid を持たなくなり全件が再計算されないため置き換える。
理由: 建築メニューの順を研究で解放される順と nodeGraph の配置に揃えるという既存の上位目的と、並び順を master-refine が所有する形を保てる。
出所: ユーザー裁定 2026-10-26。controller の問い「Bで進めてよいですか？研究解放順とnodeGraph配置からblocksも独立算出する」への回答「はい」。この承認は共有 master clone の HEAD の復旧や Task20 起動時の共有 HEAD 移動の incident の免除を意味しない。
リンク: .superpowers/sdd/task-19-open-question.md（選択肢 A/B/C）、.superpowers/sdd/task-19-rereview.md（I1）、.agents/skills/master-refine/SKILL.md 操作 A
