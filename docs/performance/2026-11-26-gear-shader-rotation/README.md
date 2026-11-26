# 歯車の回す部品をロード時にまとめ、シェーダーで回す（2026-11-26、Windows）

歯車の回転（`GearRotationTicker`）は、[前回](../2026-11-22-gear-rotation/README.md)のまとめ方の後も、半径内の部品を毎フレーム `Transform.Rotate` していて、1フレーム 2.6〜3.3ms かかっていた。これを、ロード時に prefab を加工して回す部品を1つのメッシュへまとめ、頂点シェーダーで回す形にした。

## 結論
- **歯車にかかるメインスレッドの時間は 2.6〜3.3ms → 0.14〜0.15ms**（Tick ごとの Stopwatch、2窓ずつ）。残りは、CPU で回し続ける13個の歯車（`GearRotationTicker` 0.04〜0.05ms）と、表を GPU へ送る処理（`GearRotationGpuUploader` 0.09〜0.11ms）。別の回の確認では送る処理が 0.13〜0.19ms だった。
- ワールド全体の回す部品 28,708 のうち 28,644 がシェーダー回転に移った。まとめた Renderer は 29,343、GPU の表の行（ブロック）は 10,077。
- fps は同じコードでも窓ごとに 9.8〜15.7 とぶれ、変更前（13.7・14.2）との差は読めない。メインスレッドの他の重さ（`TrainUnitClientSimulator` 6〜11ms など）の揺れが大きいため（推測）。
- GPU で回っていることは、動いている歯車（RPM 10）の正面に一時カメラを置き、シェーダーの時刻だけを 0.25 秒ずらして2回描いて確かめた。512×512 のうち 1,267 画素が変わり、同じ時刻で2回描くと 0 画素（`results/after/render.txt`、`gear-a.png`・`gear-b.png`）。

| 項目（1フレーム） | 変更前 `e7fed35c45` | 変更後 `18bf2c26c9` |
|---|---:|---:|
| `GearRotationTicker` | 2.597 / 3.345 ms | 0.052 / 0.041 ms |
| `GearRotationGpuUploader` | ― | 0.092 / 0.110 ms |
| VContainer の Tick 合計 | 9.64 / 11.29 ms | 6.81 / 8.74 ms |
| fps（Profiler なし10秒） | 13.73 / 14.24 | 15.72 / 14.73（同じコードの別の回では 9.82 / 10.38） |

## 仕組み
- **後処理の流れ**（`Client.Common/PrefabPostProcess`）：読み込んだ prefab を、後処理のどれかが対象にしたときだけ非アクティブの隠しルートへ複製し、登録順に加工して、以後はその複製を prefab として使う。元の asset は書き換えない。部品として、メッシュをまとめる `MeshMerger`、マテリアルの別版を作る `MaterialVariantCache`、Renderer を差し替えて LODGroup を付け替える `MergedRendererInstaller` を持つ。ブロック用の並びは `Client.Starter/Initialization/BlockPrefabPostProcessorRegistration` で決める。
- **歯車の後処理**（`GearRotationPrefabPostProcessor`）：回す Transform ごとに、同じ姿勢・大きさ1の置き場所を作り、下の Renderer をその空間で1つのメッシュへまとめる（大きさは頂点へ焼き込む）。UV6 に軸×速さ×反転、UV7 に部品から見たブロック原点と向き固定の印を書く。マテリアルは URP Lit と同じ項目を持つ `Moorestech/GearRotatingLit` へ替え、部品に「CPU では回さない」印を付ける。
- **回転の状態**：ブロック原点（×2 を丸めた整数）をキーにした開番地法のハッシュ表を GPU に置く（`GearRotationGpuTable`）。歯車が状態を受け取ったときに、今の角度まで進めてから速さを替えた行を書く。シェーダーは自分の行列からブロック原点を出して表を引き、`角度 = 基準時の角度 + 角速度 × 経過時間` で頂点・法線・接線を回す。ワールド符号（`GearWorldRotationSign`）もシェーダーで同じ規約で求める。

## まとめられず CPU に残るもの
- `Ore_Crusher` の石2つ（下に LODGroup がある）。全ブロックで、これ以外は残らないことを `GearRotationPrefabPostProcessorTest` で確かめている。
- 回す部品の下で元から表示していない Renderer は、まとめずにそのまま残す（今も描かれていない）。
- まとめるために、歯車の FBX 5つ（`EngineGenerator`・`gear beltconveyor`・`gear beltconveyor slope`・`mining_machine_primitive`・`Temporary Big Gear`）の Read/Write をオンにした。

## 前提と注意
- ブロック原点（ルートの位置）は置いた後に動かない前提。動くと表を引けず、その間は回転が止まって見える。
- プレビューなどでマテリアルを差し替えると、その部品はシェーダー回転でなくなり止まって見える。当たり判定（コライダー）は回らなくなった（ほぼ円柱で見た目は同じ。風車の羽根のクリック判定は止まったまま）。
- 回転版のシェーダーは URP 17.6.0 の Lit.shader を元にしている。URP を上げたら同じ差分を当て直す。
- 角度は 3600 度で畳む。速さの倍率が 0.1 の倍数でないと、畳む瞬間に見た目が飛ぶ。

## 条件
| 項目 | 値 |
|---|---|
| マシン | Windows／i7-12700／RTX 3060 Ti |
| 実行形態 | Unity Editor 6000.6.10f1 の PlayMode、D3D12、グラフィックスジョブは既定、コード最適化 Debug |
| ワールド・視点 | ベースライン `2026-10-22-world_1`、`benchmark/snippets/place-player.cs` の位置と視点 |

## 再計測
`snippets/run.sh`（`BENCH_HOST=<ssh先> MEASURE=1 run.sh`）。変更前のコミットでは `BASELINE_RUN=1` を付ける（歯車の確認スニペットを送らない）。

| ファイル | 内容 |
|---|---|
| `snippets/gear-gpu-check.cs` | シェーダー回転へ移った部品・まとめた Renderer・表の行の数 |
| `snippets/gear-gpu-near.cs` | 画面に写る動いている歯車と、表に行があるか |
| `snippets/gear-gpu-render.cs` | 一時カメラで、シェーダーの時刻をずらして2回描き、差の画素数を数える |
| `snippets/gear-postprocess-dryrun.cs` | Editor で全 prefab に後処理をかけ、CPU に残る部品と理由を挙げる |
| `snippets/tick-probe-*.cs` | VContainer の Tick を1つずつ Stopwatch で測る（[2026-11-23](../2026-11-23-main-thread-breakdown/) と同じ） |
