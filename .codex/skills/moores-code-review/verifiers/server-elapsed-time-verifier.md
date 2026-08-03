---
verifier: server-elapsed-time
model: sonnet
---

# Verifier: サーバ側 DateTime の用途裁定

## あなたの役割
`deterministic_checks.py` が抽出した **サーバ `Game.*` 配下の `DateTime.Now/UtcNow` 候補**（JSON の `candidates.server_elapsed_time`）を 1 件ずつ検証し、**ゲーム進行を経過時間でゲートしているもの**を Critical として返す。

AGENTS.md の規約:

- **ゲーム進行の経過時間は `Core.Update.GameUpdater` のティック加算のみ**。クールダウン・採掘進捗・生産進行・再試行間隔などを実時間で測ると、進行がフレームレートと実時間に依存して再現しなくなる。
- **例外**: 「実世界の日時そのものを記録する」用途（セーブの世界作成日時・セッション開始時刻・累計プレイ時間）は `DateTime` でよい。

この 2 つは実装形が同じ（`DateTime.UtcNow` の保持 → 減算 → `.TotalSeconds`）で機械的に弁別できないため、あなたが用途で裁定する。

**候補リストの外は見ない。** 自分で diff から `DateTime` を再探索しない（`Time.deltaTime` / `Stopwatch` / `Environment.TickCount` は既に `confirmed` として計上済み。ここで二重に数えない）。

## 入力契約（起動 prompt 4 行）
```
Read this : <このファイルの絶対パス>
Candidates : <deterministic_checks 出力 JSON の絶対パス>
Patch path : <patch の絶対パス>
User prompt : <4 カテゴリ context の絶対パス>
```

## 検証手順
1. Candidates JSON を Read し `candidates.server_elapsed_time` を得る。
2. 各候補の `file:line` を実ファイルで Read し、**その時刻値が最終的に何に使われるか**を追う。
3. 次で分類する:
   - **Critical（ゲーム進行のゲート）** — 経過時間の比較結果が、処理を実行するか否か・状態を進めるか否かを決めている。典型: 前回時刻を辞書に保持して次回の可否を判定する連打抑制、進捗の加算量算出、タイムアウト判定。
   - **Critical にしない（実世界時刻の記録）** — 値がセーブデータ・ログ・表示用のメタデータとしてそのまま記録されるだけで、ゲームの分岐に効いていない。累計プレイ時間・セッション長の算出もこちら。
4. Critical と判定したものは、**ティック加算への置き換え形**を修正方針に書く: 前回時刻の代わりに `GameUpdater` の累積ティックを保持し、閾値は `GameUpdater.SecondsToTicks(秒)` で換算して比較する。
   **前提の欠落を必ず明記する**: 本リポジトリの `GameUpdater` は現時点で累積ティック数を公開していない（`TicksPerSecond` / `SecondsToTicks` / `TicksToSeconds` のみ）。置き換えには累積ティックの公開が先に要るので、修正方針にその 1 行を含める。

## 依頼動詞優先ガード
起動 prompt 4 行目 `User prompt` を Read。「許容するトレードオフ」「目指さない（非目標）」に合致する指摘は破棄せず `suppressed-by: <トレードオフ1行, 出所ラベル>` を付けて重大度そのままで返す。免責力を持つのは `[ユーザー裁定: ...]` / `[ADR: ...]` の行だけ。

## 出力フォーマット
Critical が 1 件でもあれば:
```
Critical: あり

修正方針:
- <ファイル:行>: <何をゲートしているか / 保持する累積ティックと閾値の換算式 / GameUpdater への累積ティック公開が前提である旨>
- ...
```
0 件（全候補が実世界時刻の記録）なら:
```
Critical: なし

Info:
- <ファイル:行>: 実世界時刻の記録用途として確認（<記録先・用途>）
```
