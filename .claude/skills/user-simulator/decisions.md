# user-simulator 判断記録（ADR）— スキル自身の進化台帳（追記型）

知識・エージェント・プロトコルの変更は必ずここに行を持つ。行の書き換え禁止（覆ったら新行＋旧行に「→ #N で変更」注記）。

**この表は索引。** 検討の背景・代替案の議論・経緯の詳細は `decisions/YYYY-MM-DD-<topic>.md`（1判断群=1ファイル・不変。moores-code-review records/ と同運用）に書き、行の「詳細」列から参照する。

| # | 判断 | 採用 | 却下案と理由 | 出所 | 詳細 |
|---|---|---|---|---|---|
| 1 | スキルの位置づけ | ユーザーのシミュレーター（指摘・裁定の予測器）。観点別並列レンズは廃止し知識ファイルへ降格 | 観点エージェント増設路線: 文脈不足由来の外し（新yaml流儀・仮置き知識）は観点追加では拾えず、コストだけ増える | ユーザー裁定 2026-07-24 | [v2再構成](decisions/2026-07-24-v2-redesign.md) |
| 2 | 実行体制 | 単一Fable判事＋sonnet圧縮斥候＋opus反証役。参照選択は判事自身が行う（斥候は圧縮のみ） | sonnetに「読むべきか」を判断させる: docの文脈を持たない側の選択は浅く誤ルートが増える／並列レンズ維持: 発見の合流が起きない | ユーザー裁定 2026-07-24（subagentのFable指定はモデルコスト方針の明示的例外） | [v2再構成](decisions/2026-07-24-v2-redesign.md) |
| 3 | 改善ループ | 外し即時ハンドオフ発行→新セッションにコピペで改善（`/user-simulator improve <id>`）。ゴールデン再演を採用ゲートに | 3件バッチ改善: ユーザーが覚えていられない／即時fork改善のみ: 単一事例過学習は再演ゲート＋同根分析で抑止 | ユーザー裁定 2026-07-24 | [v2再構成](decisions/2026-07-24-v2-redesign.md) |
| 4 | preanswerを基本モードに | C型質問は判事の予測を通し、確信高は前提宣言へ降格・中は予測注記付き質問。採点はmisses.mdが自動で持つ | 質問をそのまま出す: ユーザーの読む量・答える量が純増する | ユーザー裁定 2026-07-24 | [v2再構成](decisions/2026-07-24-v2-redesign.md) |
| 5 | 構成 | SKILL.md薄く・本質はagents/・モード別modes/・共有知識knowledge/・スキル自身のADR=本ファイル | 全部SKILL.mdに書く: 常時ロードが太る。モード横断資産をmodes/配下に重複配置: 共有物はagents/knowledge/に一本化 | ユーザー裁定 2026-07-24 | [v2再構成](decisions/2026-07-24-v2-redesign.md) |
| 6 | 知識の複製禁止 | moorestech-principles等の既存成文はindex.mdからポインタ参照 | knowledge/へコピー: 二重管理でズレる（single-source原則） | 原則(B: 情報一元化) | [v2再構成](decisions/2026-07-24-v2-redesign.md) |
| 7 | reviewの強制発動 | brainstorming/writing-plansのfrontmatter hooks（発動セッション限定）で追跡＋Stop関所。misses.md追記で解除・自前カウンタ2回でフェイルオープン・スキップはmisses.md記録で通過 | settings.json常設フック: 無関係セッションを巻き込む／preanswerの強制: AskUserQuestionは裁定以外にも使い誤ブロックが多い（指示ベース維持） | ユーザー裁定 2026-07-24 | scripts/sim-gate.sh |
| 8 | shadowモード新設 | 設計セッションのtranscriptから質問・実回答を抽出し、opus予測体（逐次文脈・実回答遮断・subagent起動禁止）で盲検再予測→ベースライン比較・確信校正つきで採点しmisses.mdへ記録。独立スキルにはせずuser-simulator配下に統合 | 独立スキルsim-shadow-scoreとして配置: スキルが散らばりuser-simに閉じない（ユーザー指摘で撤回）／インライン予測(preanswer)の常時挟み込み: grillの1問1答で遅延が積む | ユーザー裁定 2026-07-28「基本的にuser-simに統合しておきたい」 | modes/shadow/protocol.md |
| 9 | shadow初回で逸脱7問的中0（ベースライン割れ73%>69%） | 逸脱5パターン＋FP抑止1本を知識化。浅層=adjudications.md「推奨追従と逸脱の境界」（抽象原則のみ）・深層=knowledge/deviation-cases.md（具体事例・検知シグナル・確信度規律）。過学習は深層に隔離して許容する段階的開示方針をimprove protocolに成文化 | 7問の個別暗記のみ: 汎化せず次セッションで再発／抽象原則のみ: 検知シグナルが伝わらず逸脱を再び拾えない | ユーザー裁定 2026-07-28「すこしくらい過学習をしても問題ない。ただしコンテキストの段階的開示にすること」 | modes/improve/applied/20260728-0300-shadow-deviation-patterns.md |
