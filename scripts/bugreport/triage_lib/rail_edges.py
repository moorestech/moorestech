from . import cell
from .train_timeline import snapshots


def summarize(box):
    latest = None
    for latest in snapshots(box):
        pass
    lines = ['## レール']
    if latest is None:
        return lines + ['スナップショットが無い']
    tick, snapshot = latest
    edges = snapshot.get('railSegments', [])
    lines.append(f'tick={tick} / レール: {len(edges)} 件')
    for edge in edges:
        lines.append(f'- {cell(edge["A"])} → {cell(edge["B"])} '
                     f'({edge["Length"]}, {edge["RailTypeGuid"][:8]}, {edge["IsDrawable"]})')
    return lines
