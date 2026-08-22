# pollerの系統判別はLiteralと解決関数へ一本化する

- 日付: 2026-08-22
- 決定: `poller.py` に `Kind = Literal["pr","issue"]` と `state_dir_of(number, kind)` を導入し、5箇所へ複製されていた三項式を畳む。未知値は `ValueError` で即死。gh サブコマンドの兼務は `GH_SUBCOMMAND` 対応表へ分離し、`workspace_name` の二重判別は `WORKSPACE_LABELS[(kind, prefix)]` の対応表へ畳む。既定値 `kind = "pr"` は残し、既存のPR側呼び出しは1行も変更しない。
- 棄却案: `PR_TRACK` / `REPAIR_TRACK` を持つ frozen dataclass (`Track`) へ置換し既定値を廃する案。AGENTS.md の「デフォルト引数は基本使用禁止」に沿うが、既存PR呼び出し数十箇所の一括更新を伴い「PR系統に触っていない」ことの目視確認が難しくなる。
- 理由: poller.py はリポジトリ管理外の常駐サービスでリポジトリ規約の適用範囲が未裁定である一方、最重要ゲート（既存PRレビュー系統の挙動を1バイトも変えない）は絶対に守る必要がある。採用案は既存呼び出しを触らずに「三項式の複製」「サブコマンド兼務」「未知値の静かな fall-through」の3つを同時に消せる。
- 関連: moores-code-review 2026-08-22-1742 の C-10
