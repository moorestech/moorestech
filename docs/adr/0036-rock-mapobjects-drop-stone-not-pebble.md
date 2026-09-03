# ADR 0036: 岩系mapObjectのドロップを小石から石へ移す

## Status
Accepted (2026-08-26)

## Context
`map.json` の mapObjects 195件のうち89件が「小石」(`582040ec-093b-4c8e-8fe3-f4ec030cf1ca`) をドロップしている。
内訳は `miningType=PickUp` の4件（`小石` / `Pebble1`〜`3`、hp1・1個）と、`miningType=Mining` の85件（hp100・1〜4個・石器/石の斧で採掘）。
Mining側85件のうち84件は `Vanilla/Environment/Rock/**` 配下の岩で、残り1件 `CowSkull` のみ `Vanilla/Environment/Prop/MesaDesert` 配下。
「石」(`44aaddd6-e3c0-4131-a159-9140d3e2e33b`) は現状 `石鉱脈` とブロック採掘からのみ得られ、大型の岩を殴っても小石しか出ないため、見た目の大きさと入手物が噛み合っていない。
なお `石` の `initialUnlocked:false` はレシピ表示のアンロック（原始研究1の `unlockItemRecipeView`）であって取得可否ではないため、序盤に岩から石を得ても不整合は起きない。

## Decision
- `miningType=Mining` かつ小石をドロップする85件全部（Rock配下84件 + `CowSkull`）の `earnItems.itemGuid` を 小石 → 石 に置換する。
  出所: ユーザー裁定 2026-08-26 原文「採掘できる岩系は全部石が出るようにして」→ 選択「CowSkullも含めて全部を石に」（提示時の件数表記は86件だったが、実データは85件。guid出現回数に鉱脈1件を混ぜた数え間違いで、対象範囲の意味は同一）
- `miningType=PickUp` の4件（`小石` / `Pebble1`〜`3`）は小石のまま据え置く。これにより小石の入手経路は小石mapObjectのみになる。
  出所: ユーザー裁定 2026-08-26 原文「小石は小石のmapobejctからしか出ないようにしたい」
- ドロップ個数（岩1〜4個・小石1個）は据え置く。出所: agent前提（裁定「今回はドロップ変更のみ」に含意）
- 未参照の死にデータ `小石鉱脈`(`d48d49b5-a5e2-4f44-a1a6-8d7b9c1f4e50`) を map.json から削除する。
  出所: ユーザー裁定 2026-08-26 選択「削除する」
- チャレンジ7「石を5個採掘する（石鉱脈から石を5個採掘しよう）」の文言・順序は変更しない。
  出所: ユーザー裁定 2026-08-26 選択「今回はドロップ変更のみ。導線は別件」

## Considered Options（実提示・棄却）
- Rock配下84件のみ石にし `CowSkull` の earnItems を空にする — 棄却（ユーザー裁定）
- Rock配下84件のみ石にし `CowSkull` は小石のまま — 「小石は小石mapObjectからのみ」を満たせない。棄却（ユーザー裁定）
- チャレンジ7の文言を「岩や石鉱脈を採掘して」に直す — 棄却（ユーザー裁定、別件へ送る）
- 岩からの石を1個固定にして鉱脈の優位性を保つ — 棄却（ユーザー裁定、別件へ送る）
- `小石鉱脈` を残す／石鉱脈へ寄せて方針をADRに明記 — 棄却（ユーザー裁定、単純削除を採択）

## Consequences
- 石器（小石3個）の材料は小石mapObjectのPickUpのみから集めることになる。`小石`/`Pebble1`〜`3` は generation.json から生成されており供給は確保されている。
- チャレンジ7が石鉱脈を見つけずとも岩の採掘で達成可能になり、原始研究1より前に達成され得る。導線の是正は別タスクとする。
- 小石の供給がPickUpの4種のみになるため、石器（小石3個）を作り直したい局面で周囲の小石を拾い尽くしていると詰まり得る。石器を失う経路と小石の再生成有無は未調査であり、実プレイで問題が出たら供給側（小石mapObjectの生成密度）で調整する。
- 変更は `moorestech_master` リポジトリの `server_v8/mods/moorestechAlphaMod_8/master/map.json` のみ。本repoの `.moorestech-external-revisions.json` のピン更新が必要。
- 関連: [[2026-08-26-岩系mapObjectは石を落としPickUp小石だけが小石を落とす]]
