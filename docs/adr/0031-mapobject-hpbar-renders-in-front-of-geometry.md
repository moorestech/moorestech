# ADR 0031: mapObjectのHPバーは3Dジオメトリの手前に描画する

- Status: Accepted
- Date: 2026-08-23
- 関連: bd moorestech-nen3 / bd moorestech-ditv（バー高さの是正・別PR）

## Context

mapObjectのHPバー（`Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab`）はWorld Space Canvas
（`m_RenderMode: 2`）で、Image3枚とTMP_Textはいずれも既定マテリアルを使っている。World Space Canvasでは
Unityが `unity_GUIZTestMode` を `LEqual` に設定するため、バーは常に深度テストで負け、樹冠や岩の陰に沈む。
Canvasの `sortingOrder` は3Dジオメトリとの前後関係に効かない（深度テストの問題であってソート順の問題ではない）。

HPバーは `MapObjectGameObject` がフォーカス時のみ `SetActive(true)` にするため、画面に出るのは常に1体分だけである。

## Decision

HPバーの全構成要素（Image3枚＋TMP_Text）を `ZTest Always` のマテリアルで描画し、全ての3Dジオメトリの手前に出す。

- Imageは自作UIシェーダ `Assets/Asset/Common/Shader/UI/UIOverlay.shader`（UI/Default相当＋`ZTest Always`）と
  それを使うマテリアル1枚で描画する
- TMP_Textはプロジェクト同梱の `TextMeshPro/Distance Field Overlay`（`ZTest Always`）を使うフォントマテリアル
  プリセットで描画する
- `MapObjectHpBar.prefab` は203件のprefabからネストprefabとして参照されているため、正本1件の差し替えで全体に伝播する
- バーの高さ（見た目の外接頂部 +0.5m）は本ADRでは変更しない

出所: ユーザー裁定 2026-08-23 原文「map objectのhpバーをgame objectの上にレンダリングする」
→ 選択「全部貫通でよい」「今回は触らない（高さ）」「UIシェーダ自作+マテリアル」

## Considered Options

### 採択: UIシェーダ自作＋マテリアル差し替え

影響範囲がこのprefabに閉じる。TMPはOverlayシェーダが同梱済みで自作不要。

### 棄却: URP Render Objects feature

専用レイヤーとURP Rendererへのfeature追加で実現できシェーダ自作は不要だが、レイヤー枠を消費し、
URPレンダラー設定の変更が他のUIへどう波及するか読みにくい。
出所: ユーザー裁定 2026-08-23（選択肢として提示し不採択）

### 棄却: 自オブジェクトのみ貫通（ステンシル運用）

手前に立つ別の木やプレイヤーには隠れる挙動。フォーカス中の1体しか表示されない以上、
全貫通で実害が無いのに機構だけが増える。
出所: ユーザー裁定 2026-08-23（選択肢として提示し不採択）

## Consequences

- フォーカス中のmapObjectのHPバーは、手前の別オブジェクトやプレイヤーキャラを貫通して見える
- 半透明UIが常に最前面に出るため、将来HPバーを常時表示にする場合はこの帰結を再評価する必要がある
