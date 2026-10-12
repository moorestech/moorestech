# Codex ホストでの実行（Workflow の代替ランタイム）

Codex（Workflow ツールも Agent ツールも無いホスト）で moores-code-review を実行するときの手順。Step 0〜2 と Step 7 は SKILL.md のとおり自分で行い、Step 3.5〜6.5 だけをこの手順で回す。選ばれた系統を自分で代行・要約・領域分割に置き換えるのは禁止（発火数が Claude 実行と一致しなくなる）。

Codex には Workflow も Agent も無いため、同じ `$RUNDIR/review_workflow.js` を `scripts/codex_workflow_runner/run.mjs` で実行する。runner はスクリプト本文をそのまま評価し、`agent()` 1回を `codex exec` 1プロセス（`--output-schema` で構造化出力・`-o` で結論）に置き換えるだけなので、系統の選択・発火数・再起動・統合・適用は Claude の Workflow 実行と一致する（`tests/test_codex_workflow_runner.py` が模擬実行と突き合わせる）。モデル階層（opus/fable/sonnet/haiku）は `codex_model_map.json` で codex の推論強度へ写す。

1. Step 2-2 は Codex 3本のプロンプトを `$RUNDIR/codex-<名前>.md` に書くところまで行い、起動しない（runner が未起動のものだけ切り離して起動する）。
2. 子の codex exec が対象リポジトリと `$RUNDIR` へ書き、さらに codex を起動するため、本体はサンドボックス外（`danger-full-access`）で動いている必要がある。サンドボックスで子の起動が拒否されたら縮退せず止め、その旨を報告する。
3. 前景で1コマンド実行し、終わるまで待つ（所要は系統数と同時数次第で数十分。バックグラウンドに回してポーリングしない）:

       node .agents/skills/moores-code-review/scripts/codex_workflow_runner/run.mjs <$RUNDIRの実値>/review_workflow.js

   オプションは付けない（同時数は Workflow と同じ `min(16, CPU-2)` が既定。下げても発火数は変わらず所要だけ伸びる。codex が PATH に無いときだけ `--codex-bin <codex_preflight.py の codex>` を足す）。

4. 標準出力（＝`$RUNDIR/codex-runner/result.json`）が Workflow の返り値にあたる。`fireCount`・`failedFires` と `result.systems` を「回収時の突合」と同じ規則で見て、Step 7 の報告冒頭に「runner: 発火 N 体（失敗 M）」を書く。終了コード 3 は Workflow 本文の例外（`error` に理由）で、`$RUNDIR` を残したまま同じコマンドで再実行してよい（キャッシュは無いので全系統が再発火する）。
5. Step 7 の AskUserQuestion が使えない非対話実行（`codex exec`）では、設計判断と `[解釈]` Warning を選択肢付きで報告に列挙して止める（自分で裁定しない）。
