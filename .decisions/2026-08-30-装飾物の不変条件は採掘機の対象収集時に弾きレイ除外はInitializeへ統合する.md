# 装飾物の不変条件は採掘機の対象収集時に弾き、レイ除外は Initialize へ統合する

- 日付: 2026-08-30 / 出所: moores-code-review 設計判断 D1〜D4（PR #1294, ADR 0043）
- 決定:
  - D1: 出荷マスタ生成器 `gen_map_master.py` に装飾軸を宣言データ（decoration-species.json → species_catalog → species-inventory の interactionClass）として載せる。棄却: 別PRへ先送り（再生成で38件が Mining へ巻き戻る検査が無いまま残るため）
  - D2: 「装飾物は削れない」不変条件は `VanillaGearMapObjectMinerProcessorComponent` の対象収集時に除外し、判定は `MapObjectMiningService.IsDecoration(IMapObject)` に集約。棄却: 現状維持（マスタ運用の約束だけに依存）／モデル側 `Attack` で吸収（採掘機が永久空振り）／マスタ検証で mineSettings を弾く（コード経路が無防備のまま）
  - D3: `MapObjectRayTarget.Initialize(mapObjectGameObject, bool interactable)` に統合し `SetInteractable` を廃止。棄却: マーカー層 `MiningTargetObject => IsAvailable ? … : null`（装飾物のトリガーコライダーがレイを受け続け背後を狙えなくなる＋nullガードが要る）／現状維持
  - D4: `ChallengeMasterUtil` の mapObject 直指定ピンに vein 同型の「None を指したらログ」検査を追加。棄却: master 作者の責任とする
- 理由: いずれも前例同形・最小差分で不変条件を型/データで固定でき、運用の約束への依存を消せる
- リンク: docs/adr/0043-non-interactive-decoration-map-objects.md / moorestech_logs/harness/moores-code-review/runs/2026-08-30-0745/design.md
