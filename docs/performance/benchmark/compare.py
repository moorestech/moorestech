#!/usr/bin/env python3
"""ベンチマーク結果 JSON を label ごとにまとめて比較し、差分表（Markdown）を出す。

usage: compare.py <結果.json> [<結果.json> ...]
  - 同じ label の結果は1グループにまとめ、平均と（最小〜最大）を出す。
  - 最初に現れた label を基準とし、他のグループの平均との差を % で出す。
  - 基準と比較対象の最小〜最大の範囲が重ならないときだけ ✅（改善）/ ⚠️（悪化）を付ける。
    同じコードでも1回ごとに数%〜数十%ぶれるので、範囲が重なる差はノイズとみなす。
  - 計測条件（ベースラインワールド・最適化モード・品質・解像度・GPU）が基準と違う結果には警告を出す。
例: compare.py results/*_baseline_*.json results/*_dedupe-envelope_*.json
"""
import json
import sys
from collections import OrderedDict

FIXED_MARKERS = [
    "PlayerLoop",
    "UniTaskLoopRunnerYieldUpdate",
    "PacketExchange.DispatchReceivedPackets",
    "PacketExchange.Deserialize.ProtocolBase",
    "PacketExchange.ReadPacketHeader",
    "PacketExchange.Deserialize.EventStream",
    "PacketExchange.RouteEvent",
    "PacketExchange.RouteResponse",
    "BlockState.Deserialize",
    "BlockState.FluidPipe.GetStateDetail",
    "BlockState.FluidPipe.ApplyWaterLevel",
    "RenderPlayModeViewCameras",
    "EditorLoop",
    "GC.Collect",
]
PRESENT_WAIT_MARKER = "GfxDeviceD3D12.WaitForLastPresentation.WaitForGPU"
CONDITION_KEYS = [
    ("baselineWorld", "saveJsonSha256"),
    ("environment", "codeOptimization"),
    ("environment", "quality"),
    ("environment", "cameraPixels"),
    ("environment", "gpu"),
]


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def marker(result, name, column="totalMs"):
    return result["profiler"]["markers"].get(name, {}).get(column)


def per_event_us(result):
    total = marker(result, "PacketExchange.DispatchReceivedPackets")
    response = marker(result, "PacketExchange.RouteResponse") or 0
    calls = marker(result, "PacketExchange.RouteEvent", "calls")
    if total is None or not calls:
        return None
    return (total - response) / calls * 1000


def stats(values):
    values = [v for v in values if v is not None]
    if not values:
        return None
    return sum(values) / len(values), min(values), max(values)


def cell(stat, digits, base_stat=None, lower_is_better=True):
    if stat is None:
        return "—"
    mean, low, high = stat
    text = f"{mean:,.{digits}f}"
    if low != high:
        text += f"<br><sub>{low:,.{digits}f}〜{high:,.{digits}f}</sub>"
    if base_stat is None or base_stat[0] == 0:
        return text
    ratio = (mean - base_stat[0]) / base_stat[0] * 100
    separated = high < base_stat[1] or low > base_stat[2]
    mark = ""
    if separated:
        mark = "✅" if (ratio < 0) == lower_is_better else "⚠️"
    return f"{text} ({'+' if ratio >= 0 else ''}{ratio:.0f}%{mark})"


def main(paths):
    groups = OrderedDict()
    for p in paths:
        r = load(p)
        groups.setdefault(r["label"], []).append((p, r))
    labels = list(groups)
    base_label = labels[0]
    base_first = groups[base_label][0][1]

    for label in labels:
        for p, r in groups[label]:
            for group, key in CONDITION_KEYS:
                if r[group].get(key) != base_first[group].get(key):
                    print(f"> ⚠️ {p}: 計測条件 {group}.{key} が基準と違う（{base_first[group].get(key)} → {r[group].get(key)}）。比較は参考値")

    # 表示状態で fps が最大1.6倍変わった実績があるため、画面に表示していなかった疑いのある回を知らせる
    for label in labels:
        for p, r in groups[label]:
            if r["environment"].get("editorWindowMinimized"):
                print(f"> ⚠️ {p}: 計測中に Editor ウィンドウが最小化されていた")
            if PRESENT_WAIT_MARKER not in r["profiler"]["markers"]:
                print(f"> ⚠️ {p}: 表示待ち（{PRESENT_WAIT_MARKER}）が記録されていない。画面オフや非表示で描画の表示を省いていた疑いがある")

    headers = []
    for label in labels:
        commits = sorted({r["commit"][:10] for _, r in groups[label]})
        headers.append(f"{label}（{len(groups[label])}回）<br>{', '.join(commits)}")
    print("| 指標 | " + " | ".join(headers) + " |")
    print("|---|" + "---:|" * len(labels))

    def row(title, getter, digits=1, lower_is_better=True):
        base_stat = stats([getter(r) for _, r in groups[base_label]])
        cells = [cell(base_stat, digits)]
        for label in labels[1:]:
            cells.append(cell(stats([getter(r) for _, r in groups[label]]), digits, base_stat, lower_is_better))
        print(f"| {title} | " + " | ".join(cells) + " |")

    row("fps（Profiler なし）", lambda r: r["fps"]["fps"], 2, lower_is_better=False)
    row("フレーム ms 平均（Profiler なし）", lambda r: r["fps"]["frameMsAvg"])
    row("フレーム ms p95（Profiler）", lambda r: r["profiler"]["frameMs"]["p95"])
    row("受信イベント 件/秒", lambda r: r["events"]["eventsPerSecond"], 0)
    # 表示状態に左右されにくい指標。ワールド定期取得（RouteResponse）を除いたイベント1件あたりの受信処理
    row("受信処理 µs/件（応答を除く）", lambda r: per_event_us(r))
    row("受信 payload KB/秒", lambda r: r["events"]["payloadBytesPerSecond"] / 1024, 0)
    for name in FIXED_MARKERS:
        row(f"`{name}` ms", lambda r, n=name: marker(r, n))
    row("`GC.Alloc` 回/フレーム", lambda r: marker(r, "GC.Alloc", "calls"), 0)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    main(sys.argv[1:])
