# RayTargetは前例どおりconvex MeshColliderにする

2026-08-18裁定。

## 決定
mapObjectラッパープレハブのレイターゲット（採掘・照準の当たり判定）は、既存 `Tree.prefab` の前例に合わせ幹に沿った convex MeshCollider（isTrigger）にする。

## 棄却した案
- **全レンダラー合成boundsのBoxCollider**（plan 2026-08-17-mapmaking-visual-parity-v2 Task 4 の明示指定）: 採掘距離2.5m＋カメラ背後3.5mに対し、水平12m超の大型種16件（Sequoia1〜5は21〜30m）はカメラが常にBox内部に入り、Unity仕様でレイがコライダーを検出できず採掘フォーカスが一度も立たない（実測）。plan自身の「新樹種・岩の照準・採掘はTask 4のRayTargetで担保」と矛盾するため、字面でなく役割を優先した
- Boxのまま水平サイズを幹相当にクランプ: 前例から外れた機構を維持するだけの対症療法
- BK側の既存コライダーを流用: コライダーを持たない種でフォールバックが要る

## 理由
「前例は機構でなく役割で選ぶ」（AGENTS.md 設計原則）。レイターゲットという役割の前例は Tree.prefab の convex MeshCollider であり、planのBox指定はその役割要件を満たさない。

## リンク
- plan: docs/superpowers/plans/2026-08-17-mapmaking-visual-parity-v2.md Task 4
- レビュー: Task 4 レビュー C-1
