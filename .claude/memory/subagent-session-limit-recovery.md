---
name: subagent-session-limit-recovery
description: セッション上限でsubagentが無通知死する。到達性確認→SendMessage再開 or クリーン再spawnで復旧。file-based brief/ledgerが復旧を安価にする
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 77c62698-13ed-4bc3-b70d-0a8f9bb49948
---

長時間オーケストレーション中、アカウントのセッション上限（数時間毎リセット）で subagent が3回死んだ。うち1回は failed 通知付きだが、2回は**無通知**（idle通知すら来ない/到達不能になる）。

**Why:** 上限は account 全体で共有され、implementer の作業途中でも即死する。作業ツリーの変更は残るので二重実行や取りこぼしのリスクがある。

**How to apply:**
- 進捗が見えない subagent は `SendMessage` で ping → "No agent named X is reachable" なら死亡と判定
- 作業ツリーに変更**あり** → 同名エージェントに SendMessage で再開指示（コンテキスト保持なら続行できる）。到達不能なら git status で到達点を確認し、検証→コミットは自分で引き取るのが最速
- 変更**ゼロ** → 別名でクリーン再spawn
- brief / report / ledger をファイル渡しにしておくと（superpowers:subagent-driven-development 方式）、死んでも再開コストがほぼゼロ
- 上限リセット時刻はエラーメッセージに出る（例: resets 10:10pm）。直後の再試行は通ることが多い
