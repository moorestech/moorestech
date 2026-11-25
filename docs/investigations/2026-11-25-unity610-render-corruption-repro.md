# Unity 6000.6.10f1 版上げ後の描画崩れ 再現調査（第2回）

調査日: 2026-11-24〜25 ／ タスク: `moorestech-v5oa7` ／ 前回: [2026-11-23-electric-wire-render-corruption.md](2026-11-23-electric-wire-render-corruption.md)

## 結論と現状

**崩れは再現できず、原因は未確定のまま。コードと設定は変更していない。**

ユーザーの録画（2026-11-24 15:59、world_1）にある巨大な黒白の三角形と髪の伸びを、Mac mini（M4）でも MacBook（M5）でも再現できなかった。再現できないため、どの修正案も効いたかどうかを判定できない。調査はここで打ち切った。

## 症状（ユーザー録画より）

| 項目 | 内容 |
|---|---|
| 見た目 | 画面を覆う黒と白の巨大な三角形、キャラクターの髪が伸びる |
| 崩れていた物 | 電線（実行時生成メッシュ、位置と法線のみ、URP/Lit）と、手に持つ斧（MeshRenderer）。髪（SkinnedMesh）は崩れの見え方から関与を推測しただけで未確認 |
| 環境 | MacBook（Apple M5）、Metal、Unity 6000.6.10f1、URP 17.6 |
| ワールド | `world_1`（ユーザー提供の zip） |

## 試したことと結果

すべて world_1 を zip から展開した複製で行い、ユーザーのセーブと設定は触っていない。

| 機種 | 条件 | 結果 |
|---|---|---|
| Mac mini（M4） | PlayMode 起動、各方向の見回し | 崩れなし |
| Mac mini（M4） | GPU Resident Drawer オンで起動し、セーブ上のプレイヤー位置（570, 84, 517）で16方向を見回して録画（48秒） | 崩れなし、エラーログ0件 |
| MacBook（M5） | 起動直後 | 崩れなし |
| MacBook（M5） | 高所からの見下ろし | 崩れなし |
| MacBook（M5） | 2990x1922 で39枚の連続撮影 | 崩れなし（画面の黒・白の割合を機械判定） |
| MacBook（M5） | PlayMode の開始と停止を繰り返す | 1〜2周目は崩れなし。3周目で画面スリープにより Unity が固まり、4周目は撮影が一部欠けた |

## 原因の仮説（すべて推測）

| 仮説 | 手がかり | 反する材料 |
|---|---|---|
| GPU Resident Drawer（版上げと同時の 35b4f8e9fb で有効化）が M5 で描画データを壊す | 版上げ直後から発生。M5 でだけ出る | 前回調査で実行中に GRD をオフにしても板は残った（形が毎フレーム変わった）。起動時からオフの比較はしていない |
| Graphics Ring Buffer の枯渇 | 前回 `Ran out of Graphics Ring Buffer space` が1回出た | 崩れとの前後関係は未確認 |
| Unity 6000.6.10f1 と Metal（M5）の不具合 | M4 で一度も出ない | 一致する公開不具合は見つからなかった（外部AI相談でも同じ結論） |

## 次に再開するときの手順

1. 崩れが出たときの操作を特定する。ユーザーが普段の遊び方で再現したときの直前操作（飛行モード、長時間プレイ、ウィンドウ操作など）とログを取ってもらうのが最短。
2. M5 で再現したら、起動時から GPU Resident Drawer をオフにした状態と比べる。オフは URP アセット `UniversalRP-{High,Medium,Low}Quality.asset` の `m_GPUResidentDrawerMode` を 0 にする（Editor 経由で変更すること）。
3. オフで消えたら、全体を切るか、電線と斧だけを GRD の対象から外すかを決める。

## 注意（やってはいけないこと）

- `-gfx-ring-buffer-size 1` で Unity を起動すると、メモリを 37GB まで使って他のアプリが止まった。小さい値で試さない。
- MacBook で長時間の自動操作をするときは、画面のスリープを止める（`caffeinate -u -d`）。止めないと Unity が固まる。

## 参照先

| 種類 | 場所 |
|---|---|
| タスク台帳 | `moorestech-v5oa7` |
| 前回の調査 | [2026-11-23-electric-wire-render-corruption.md](2026-11-23-electric-wire-render-corruption.md) |
| GRD 有効化 | 35b4f8e9fb（PR #1700） |
| 電線メッシュの生成 | `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/CatenaryWireMeshBuilder.cs` |
