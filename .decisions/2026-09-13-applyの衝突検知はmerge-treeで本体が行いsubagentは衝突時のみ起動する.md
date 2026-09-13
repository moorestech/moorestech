# applyの衝突検知はmerge-treeで本体が行いsubagentは衝突時のみ起動する

日付: 2026-09-13
出所: ユーザー裁定 指示ファイル剪定監査（PRレビュー系）の裁定3点のうち1。質問「pr-adjudicated-apply Step 3.5 の subagent 無条件発火を見直すか」→ 選択「やる（中間案）」

## 決定
- pr-adjudicated-apply Step 3.5 は、本体が `git merge-tree --write-tree` で origin/master との衝突有無だけを判定する
- 衝突なしなら subagent を起動せず Step 4 へ進む。衝突ありなら従来どおり subagent（opus）へ解消を委譲する
- 2026-08-19裁定の実質（外部リビジョンピンは ours・_CompileRequester.cs は theirs・記録類は機械的解消・ピンを理由に中止しない）は変えない

## 棄却案
- 現状維持（無条件で subagent 発火）: 無衝突PRでも毎回 subagent 1体分のコストを払う。「本体のコンテキストを守る」動機は旧モデルのコスト回避で、検知だけなら merge-tree の出力は数行で済む
- 監査案（subagent 廃止・全部 merge-tree で本体が解消）: 解消には Unity 生成物やスキーマ差分の判断が要り、本体で行うと本来の apply 作業のコンテキストを圧迫する

リンク: [[2026-08-19-applyのピン衝突はPR側を採って続行する]]
