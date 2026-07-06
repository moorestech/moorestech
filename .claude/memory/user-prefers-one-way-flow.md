---
name: user-prefers-one-way-flow
description: ユーザーは一方通行のデータフローを強く好む。Contextハブ・コールバック逆流・Viewが判定を返す構造は「見通しが悪い」と却下される
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 7f5298d0-91b6-498d-865e-9aeb0432daf6
---

ユーザーは処理フローが「行って戻る」設計（共有可変Contextハブ、下層コールバックによる上層状態の書き戻し、Viewの戻り値が判断材料になる構造）を嫌い、一方通行のコードを明確に好む（2026-07-03、GearChainPoleConnectで表明）。

**Why:** 状態の所有者と依存方向が一目で追えないコードは読解コストが高い、というのがユーザーの一貫した価値観。3案提示では最小修正案より「MVU風フレームパイプライン（完全一方向）」を選択した。

**How to apply:** クライアントの毎フレーム系システムを設計・改修するときは「取込→集める(環境読取をCollectorに集約)→決める(純関数Decide)→映す/送る(出力適用)→反映(状態更新は最上位のみ)」の型を第一候補にする。非同期応答はコールバックでなくTryConsume系ポーリングでループ先頭に取り込む。実例: `Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/`（commit d5da55210）。
