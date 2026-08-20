# 石の斧の手持ちモデルはStoneToolと同方式でAddressable登録する

決定: `Assets/Dependencies/Sketchfab/StoneAxe/StoneAxe.prefab` を元に、`AddressableResources/Item/StoneAxe.prefab`（手持ちオフセット/スケール焼き込みのラッパー）を Unity Editor 経由で作成し `Vanilla/Item/StoneAxe` で Vanilla Asset Group に登録、マスタ items.json の石の斧に `addressablePaths.handGrabModel` を設定する。PlayModeスクリーンショットで見た目を確認してから設定する。

棄却案:
- 今回は見送り別タスク化（石器だけモデルが出て斧が出ない状態が続く）
- 既存アドレス（StoneHammer等）の仮置き（見た目が別物）

理由: ユーザー裁定 2026-08-20「同じ方式で StoneAxe を登録してマスタに設定」。

リンク: docs/research/2026-08-20-tutorial-master-rewrite-feasibility.md
