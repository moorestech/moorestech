# 反映diffへの重複reviewer当ては、特定指摘の再発防止でなく設計欠陥一般の網として入れる

2026-09-20 ユーザー裁定。PR #1219 レビューコメント r3837450405（scatter/cluster 両caseに bands が二重定義された件）のポストモーテムの検証結果を受けた再裁定。

## 決定

`pr-adjudicated-apply` Step 5 と `moores-code-review` Step 7 の「設計判断を反映した diff へ `core-cs-centralization-duplication` を1体当てる」工程は入れる。
ただし**主張を差し替える** — 「bands 二重定義の再発防止」ではなく「反映diffに残る設計欠陥一般への網」として位置づけ、bands に対しては不発（4回中1回）であることをスキル本文・PR本文・registry に明記する。

案文規則（`できあがる形:` / `覆す決定:`）は文言として残すが、**効く証拠は無い**と integration-rules 本文に明記する。

## 測定（後知恵なし・opus 各1体・実行ごとの揺れを見るため条件を2×2で埋めた）

| run | reviewer版 | diff | bands二重定義 | applier（本番レビューが見逃した実在Critical） |
|---|---|---|---|---|
| run2 | 現行 | 間引き37F | 検出 | 検出 |
| run9 | 当時版 | 間引き37F | 未検出 | 検出 |
| run6 | 当時版 | 実物49F | 未検出 | 検出 |
| run8 | 現行 | 実物49F | 未検出 | 検出 |

bands は 1/4、applier は 4/4。条件差（reviewer版・diffのノイズ量）では説明できず、run2 の検出は実行ごとの揺れと判定した。

案文規則側は run3（追記版）・run4（現行版）・run5（出荷版）・run7（**事故前の当時版**）の4本すべてが2観点単離で合格。規則が無い状態でも合格するため、このハーネスに識別力は無い。26観点フルスケール（run1）は設計判断ごと出力から消えて判定不能。

## 棄却した案

- **対策Bも取り下げる**: 本件の再発防止にならない以上ポストモーテムの成果物として出さない、という案。applier を 4/4 で拾う実績があり、その applier は本番レビューが見逃してマージ済みの実在欠陥だったため、工程としては元が取れていると判断して棄却。
- **PRごとクローズして仕切り直し**: 手当てが何も残らないため棄却。
- **ノイズ除去（.meta・データJSONを落としてから reviewer に渡す）を足す**: run9 で否定されたため不要。
- **bands を拾える観点を探して追加する**（schema-design 等を反映diffに当てる）: 観点を増やす前に、フルスケールで設計判断が落ちる側（run1 の現象）を疑うべきと判断して見送り。

## リンク

- 検証の入力・出力: `../moorestech_logs/harness/postmortem/2026-09-20-pr1219-band-duplication/`（results.md / results2.md）
- bd: moorestech-0e24（本件）・moorestech-o30m（bands 集約の本修正）・moorestech-hzyx（applier・run6/8/9 で n=3 裏取り済み）
- PR: #1387
