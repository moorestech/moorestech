---
name: no-blocking-wait-loops
description: User forbids foreground until/sleep polling loops waiting on background agents/tasks — rely on auto notifications instead
metadata: 
  node_type: memory
  type: feedback
  originSessionId: f7979586-c3fe-4f15-9e93-062e16b1da3b
---

Foreground Bash wait loops (`until ...; do sleep N; done` polling a background agent's output file) must NOT be used to wait for background subagents or background Bash tasks.

**Why:** The user interrupted these repeatedly (2026-06-10): they block the conversation turn and stall task progress; background tasks/agents already deliver automatic completion notifications.

**How to apply:** After dispatching a background agent (SendMessage resume) or background Bash task, just end the turn or do other useful work — the harness re-invokes on completion. Foreground polling is acceptable only for short waits on external state with no notification mechanism (e.g. waiting for Unity to finish startup compile when actively mid-step), and should be capped to a few minutes.
