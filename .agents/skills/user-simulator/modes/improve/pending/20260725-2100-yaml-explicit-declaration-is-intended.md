# 改善ハンドオフ: yaml-explicit-declaration-is-intended

- 種別: FN（枠付けの外し。選択肢の方向は的中）
- 対象doc: /Users/katsumi/moorestech-worktrees/tree1/docs/superpowers/plans/2026-07-25-electric-wire-param-interface-and-shared-selector.md（当時のcommit: 未コミット時点・spec側は 96b472d13 直後）
- 判事の予測レポート: （要旨）「PR1057指摘①『blocks.ymlの3キーコピペ』は現行生成器では解消不能 — yaml上の重複は残る。どう扱うか: (a)今回はinterface付与＋resolver共通化のみで完了としyaml重複は**容認** (b)生成器にdefineInterfaceプロパティ注入機能を追加する別タスクを起こす。予測: (a)＋(b)を別issue/後日として先送り、が過去裁定傾向からの本命」
- ユーザーの指摘（原文）: 「yaml上の3キー×8箇所 これがあるべき姿なのでこれで問題ない」「はい、それがmasterのあるべき姿です」
- 漏れ/誤検知の原因仮説: 知識不足。判事（とメイン）はyaml上のキー重複を「技術的負債・容認するコスト」と枠付け、(b)生成器拡張を将来の選択肢として提示した。ユーザーの価値観は「各ブロック種がプロパティを明示宣言するのがマスタスキーマのあるべき姿」であり、重複解消の需要自体が不存在。DRYをスキーマ定義に無条件適用しない（明示性＞DRY）というmoorestechマスタ設計の価値観が知識化されていない。
- 文脈: PR1057セルフレビュー2指摘（blocks.yml 3キーのinterface化・選定ロジック二重実装）の対応plan作成中。simulatorのC1（生成器はinterfaceプロパティを注入しない）は的中しplan/spec修正済み。その帰結の「残ったyaml重複をどうするか」の裁定枠付けで外した。
