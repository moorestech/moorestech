---
name: skill-result-viewer
description: skill execution resultsをローカルブラウザで読むための再利用ビューアテンプレート集。run-skill-iter-improve と run-skill-live-trial の結果ディレクトリを一覧・比較する。
---

# Skill Result Viewer

This skill stores reusable local viewer templates for skill execution results.

## Templates

- `templates/iter-improve`: reads a `scratchpad/skill-iter` style directory.
- `templates/live-trial`: reads one `.mso/live-trial/<trial>` directory or a parent containing multiple trials.

## Usage

Copy or run a template directly from this directory.

```bash
node .claude/skills/skill-result-viewer/templates/iter-improve/server.mjs --dir /path/to/scratchpad/skill-iter --port 4980
node .claude/skills/skill-result-viewer/templates/live-trial/server.mjs --dir /path/to/.mso/live-trial --port 4981
```

Both templates are dependency-free and serve a local browser UI plus JSON API.
