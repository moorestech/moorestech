---
name: master-explicit-over-derived
description: マスターデータは導出せず明示設定する — blockSize等からの値導出案はユーザーが却下する
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 86aafeba-45b1-4274-a5e2-d2f5904b3afd
---

ベルト長尺バリアント設計で「長さはblockSize.zから導出（冗長性排除）」と提案したところ、「こういうことはやらない。マスタで長さを設定する」と即却下された。

**Why:** マスターデータは設定の一次ソース。他フィールドからの導出は設定の意図を暗黙化し、mooreseditorでの編集性・可読性を損なう。冗長でも明示フィールドを持たせ、食い違いはバリデータで検出するのがこのプロジェクトの流儀。

**How to apply:** スキーマ設計時、値が他フィールドから計算可能でも専用フィールドを設ける。整合性はC#バリデータ（[[core-master-layer-boundary]]のValidator層）でエラー検出する。
