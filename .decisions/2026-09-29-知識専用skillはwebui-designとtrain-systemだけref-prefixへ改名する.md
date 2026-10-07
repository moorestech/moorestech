# 知識専用skillは webui-design と train-system だけ ref- prefix へ改名する

- 決定: 副作用のない参照知識skill `webui-design`・`train-system` を `ref-webui-design`・`ref-train-system` へ改名する。生きた参照（ルール・レビュー digest・webui のコメント・現行の評価基準doc）は新名へ追従させ、ADR・plans/specs・.decisions・golden fixture・計測記録など過去の記録は当時の名前のまま残す。
- 棄却案: 全skillを5 prefix（ref-/run-/wrap-/assign-/delegate-）へ一括改名する（参照数とpoller起動名の書き換え費用に見合わない）。グローバルと同名の8本の整理は今回は見送り。
- 理由: 名前から「読むだけで何も変えない」契約が読めるようにする。参照数が少なく改名費用が小さい2本に限った。
- リンク: note記事「Agent Skill大全」の prefix 命名規範を参考にした棚卸し（2026-09-29 セッション）
