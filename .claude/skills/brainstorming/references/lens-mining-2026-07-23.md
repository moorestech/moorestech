# specレンズ第一弾の採掘根拠（2026-07-23）

セッションログ（~/.claude/projects/-Users-katsumi-moorestech/、`docs/superpowers/specs` を含む81セッション）から
ユーザーの生発言のみを抽出し、spec・ブレスト段階での修正・却下の実例をカタログ化。
既存採録（spec-architecture-review / design-question-triage）と重複する類型は除外した残りを `lenses/` 化した。
レンズを追加・改稿する際は同手法（type==userの生発言抽出→修正シグナルでフィルタ）で再採掘する。

| 実例（セッション） | 内容 | レンズ |
|---|---|---|
| 314bf747 | 見た目Configのクライアント側管理→「絶対ダメ、マスタ+Addressablesに統合」 | ssot-data-placement |
| 86aafeba | ベルト長をblockSize.zから導出→「マスタで設定する。導出はやらない」 | ssot-data-placement |
| 1eaa0293ほか5本 | 「向きはコピーしない（YAGNI）」→毎回「向きもピックする」と却下 | scope-resolution |
| 68697493 (PR1045) | 戻り値の複雑性・型switch・DI分割混入→「封じ込めたい」「revertして」 | complexity-containment |
| f0f967eb | 共有インスタンス経由のデータ授受→「データフローをわかりやすくしたい」 | complexity-containment |
| d34c8a76 | 接続判定2方式並置→「常に1方式だけ。ユークリッド距離は全面非採用」 | scope-resolution |
| 1c2b0f15 | 旧プロトコル存続の部分移行→「全部消したい。一括で移行したい」 | scope-resolution |
| 2026b9ac | uGUI→web一律移行→対象ごとに「残す/消す/作り直す」で分類し直し | scope-resolution |

ADR新設の根拠（棚卸し）: 却下案の理由・ユーザー裁定はコミットメッセージ（「ユーザー裁定3件を反映」等）と
diffにしか残らず、次のレビュアーが再検証できない。「配置と前例」表が実在するspecは build-undo-ctrl-z のみだった。
