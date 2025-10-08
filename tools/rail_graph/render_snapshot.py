#!/usr/bin/env python3
"""Render rail graph snapshots to PNG using Matplotlib.

This utility reads the JSON produced by
`RailGraphDatastore.WriteJson(RailGraphDatastore.CreateSnapshot(), path)`
and renders a simple top-down visualization. Node labels are rendered
alongside a scatter plot of node positions, and edge distances are drawn
at the midpoint of each edge.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
from typing import Dict, Iterable, Tuple

import matplotlib.pyplot as plt


def _extract_position(node: Dict) -> Tuple[float, float]:
    station = node.get("station") or {}
    component = node.get("component") or {}

    for source in (station, component):
        position = source.get("position")
        if position:
            return float(position.get("x", 0.0)), float(position.get("z", position.get("y", 0.0)))

    # Fall back to a placeholder; the caller will overwrite this when computing the layout.
    return math.nan, math.nan


def _prepare_layout(nodes: Iterable[Dict]) -> Dict[int, Tuple[float, float]]:
    positions: Dict[int, Tuple[float, float]] = {}
    missing: Dict[int, Dict] = {}

    for node in nodes:
        x, y = _extract_position(node)
        node_id = int(node["id"])
        if math.isnan(x) or math.isnan(y):
            missing[node_id] = node
        else:
            positions[node_id] = (x, y)

    if missing:
        count = len(missing)
        radius = max(1.0, math.sqrt(count))
        for index, node_id in enumerate(sorted(missing.keys())):
            angle = 2 * math.pi * index / count
            positions[node_id] = (
                radius * math.cos(angle),
                radius * math.sin(angle),
            )

    return positions


def render(snapshot_path: Path, output_path: Path, dpi: int = 150) -> None:
    with snapshot_path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)

    nodes = payload.get("nodes", [])
    edges = payload.get("edges", [])

    if not nodes:
        raise SystemExit("snapshot contains no nodes")

    positions = _prepare_layout(nodes)

    fig, ax = plt.subplots(figsize=(8, 8), dpi=dpi)

    # Draw edges first so nodes appear on top.
    for edge in edges:
        source = positions.get(int(edge["source"]))
        target = positions.get(int(edge["target"]))
        if not source or not target:
            continue

        xs = (source[0], target[0])
        ys = (source[1], target[1])
        ax.plot(xs, ys, color="#6c6f73", linewidth=1.5, zorder=1)

        midpoint = ((xs[0] + xs[1]) / 2.0, (ys[0] + ys[1]) / 2.0)
        ax.text(
            midpoint[0],
            midpoint[1],
            str(edge.get("distance", "")),
            fontsize=8,
            color="#2d3436",
            ha="center",
            va="center",
            bbox=dict(boxstyle="round,pad=0.2", fc="white", ec="none", alpha=0.7),
            zorder=3,
        )

    xs = []
    ys = []
    labels = []
    for node in nodes:
        position = positions[int(node["id"])]
        xs.append(position[0])
        ys.append(position[1])
        labels.append(node.get("label") or str(node["id"]))

    ax.scatter(xs, ys, s=80, color="#0984e3", edgecolor="white", linewidth=0.8, zorder=4)

    for (x, y), label in zip(zip(xs, ys), labels):
        ax.text(
            x,
            y,
            label,
            fontsize=9,
            color="white",
            ha="center",
            va="center",
            zorder=5,
            bbox=dict(boxstyle="round,pad=0.3", fc="#0984e3", ec="#0652DD", alpha=0.95),
        )

    ax.set_aspect("equal", adjustable="datalim")
    ax.axis("off")
    ax.set_title("Rail Graph Snapshot", fontsize=14)
    fig.tight_layout()

    output_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(output_path, dpi=dpi, bbox_inches="tight", pad_inches=0.1)
    plt.close(fig)


def main() -> None:
    parser = argparse.ArgumentParser(description="Render a rail graph snapshot JSON to a PNG image")
    parser.add_argument("snapshot", type=Path, help="Path to the snapshot JSON produced by RailGraphDatastore.WriteJson")
    parser.add_argument("output", type=Path, help="Destination PNG file path")
    parser.add_argument("--dpi", type=int, default=150, help="Output DPI for the rendered image")

    args = parser.parse_args()
    render(args.snapshot, args.output, dpi=args.dpi)


if __name__ == "__main__":
    main()
