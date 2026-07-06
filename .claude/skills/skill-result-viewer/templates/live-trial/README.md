# run-skill-live-trial Result Viewer Template

`run-skill-live-trial` の結果ディレクトリをブラウザで読むテンプレート。

## Expected Input

Pass either one trial directory or a parent directory that contains multiple trials.

Each trial can contain:

- `report.md`
- `task.md`
- `workflow.md`
- `pane.txt`
- `out/status.json`
- `transcript.jsonl`

## Run

```bash
node server.mjs --dir /absolute/path/to/.mso/live-trial --port 4981
```

Open `http://localhost:4981`.
