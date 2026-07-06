# run-skill-iter-improve Result Viewer Template

`run-skill-iter-improve` の結果ディレクトリをブラウザで比較するテンプレート。

## Expected Input

Pass the directory that contains:

- `iter-log.md`
- `repro-context.md`
- `rubric.md`
- `improvement-diff.txt`
- `run-*/plan.md`
- `run-*/eval-output.json`

## Run

```bash
node server.mjs --dir /absolute/path/to/scratchpad/skill-iter --port 4980
```

Open `http://localhost:4980`.
