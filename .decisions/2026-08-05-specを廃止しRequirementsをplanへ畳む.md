決定: superpowers系spec文書を廃止する。grillの成果はADR+用語集のみとし、要件列挙（受け入れ基準・スコープ境界）はplan先頭の必須セクション『## Requirements』へ移す。grill→writing-plansは同一セッション接続を出口規約とする（HARD GATEと出口の一本化のみ上流brainstormingから採用）。ledger-gateはplan自身の『## 判断記録（ADR）』を読む（spec:は旧plan互換で連結）
棄却案: ①薄いspec（要件契約書）を独立ファイルとして維持 ②specユーザーレビュー関所 ③設計のセクション分割提示＋承認 ④細部質問前のスコープ分解判定（grillingの依存順原則で自然カバーのため未配線）
理由: specの実質はADR（決定）とplan（詳細）に分解でき、独立文書は二重管理でユーザーも読まない。要件チェックリスト機能だけが不可欠で、それはplan内Requirementsの同一ファイル照合の方が確実。grillの1問ずつ裁定形式は変えない（文書レビューへの逆戻り防止）
リンク: 上流の全文写しと採否記録は moores-grill-with-docs/references/superpowers-brainstorming-upstream.md。関連=[[2026-08-05-brainstormingアーカイブを削除しgrillへ一本化]]
