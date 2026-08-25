決定: topic ui.tooltip の契約を単一行（textKey+textParams）から行配列 lines[{textKey,textParams}] へ拡張する。Web側は行ごとに辞書解決して縦に並べる。既存の単一行呼び出し（採掘・クラフト・削除）は1要素配列へ一括更新し、契約スキーマ・WireContractテスト・mock-hostも同時更新する（後方互換は取らない）。
棄却案: 契約は無変更のまま、Unity側でLocalize.Getした各行を改行連結しパススルーキー({p0})で渡す案。
理由: 「tooltipは辞書キーとパラメータのみを受け取り、生の表示文字列を受け付けない」契約理念（schemas/ui.ts・ADR0019）を守る。
リンク: [[2026-08-21-設置不可理由は成立分を全て行で並べる]] / [[docs/adr/0019-webui-cursor-tooltip-typography-owned-by-web.md]] / TooltipTopic.cs / CursorTooltip.tsx

出所: ユーザー裁定 2026-08-21 選択「行配列へ拡張: lines[{textKey,textParams}]」
