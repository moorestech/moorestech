---
verifier: comparison-operator
model: sonnet
---

# Verifier: C# 比較演算子候補の検証

## あなたの役割
`deterministic_checks.py` が字句解析で抽出した **`>` / `>=` 候補**（JSON の `candidates.comparison_operator`）を 1 件ずつ検証し、真の二項比較だけを Critical として返す。プロジェクト規約: 比較は常に `<` / `<=` で「小さい値 → 大きい値」と数直線通りに揃える。

**候補リストの外は見ない。** 自分で diff から `>` を再探索しない（それはスクリプトの仕事）。候補 0 件ならこの verifier は起動されない前提。

## 入力契約（起動 prompt 4 行）
```
Read this : <このファイルの絶対パス>
Candidates : <deterministic_checks 出力 JSON の絶対パス>
Patch path : <patch の絶対パス>
User prompt : <4 カテゴリ context の絶対パス>
```

## 検証手順
1. Candidates JSON を Read し `candidates.comparison_operator` を得る。
2. 各候補の `file:line` を patch（必要なら実ファイル）で確認し、次を判定する:
   - **真の二項比較か** — 稀にスクリプトを抜けるジェネリクス断片・ドキュメント文字列等なら破棄。
   - **書き換え式** — `a > b` → `b < a`、`a >= b` → `b <= a`。複合条件は各項を入れ替える。定数下限チェックは `0 < x` / `0 <= x` の形にする。
3. 書き換えで**意味が変わらないこと**（オペランドに副作用のある呼び出しが無いか、評価順が問題にならないか）を確認する。副作用で順序が効くケースは `要判断` ラベルにする。

## 出力フォーマット
Critical が 1 件でもあれば:
```
Critical: あり

修正方針:
- <ファイル:行>: `<NG式>` を `<OK式>` に書き換え [機械的]
- ...
```
0 件（全候補が誤検出）なら:
```
Critical: なし
```
