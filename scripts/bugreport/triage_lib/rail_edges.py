from . import cell
from .train_timeline import snapshots
from .validation import required, objects


def summarize(box):
    latest = None
    for latest in snapshots(box):
        pass
    lines = ['## レール']
    if latest is None:
        return lines + ['スナップショットが無い']
    tick, snapshot = latest
    path = box / f'snapshots/tick_{tick}.json'
    edges = required(snapshot, 'railSegments', list, path, '')
    objects(edges, path, 'railSegments')
    lines.append(f'tick={tick} / レール: {len(edges)} 件')
    for index, edge in enumerate(edges):
        field = f'railSegments[{index}].'
        for key in ('A', 'B'):
            if key not in edge or not isinstance(edge[key], (str, dict)):
                raise ValueError(f'{path}: {field}{key}: ノードが必要')
        required(edge, 'Length', int, path, field)
        required(edge, 'RailTypeGuid', str, path, field)
        required(edge, 'IsDrawable', bool, path, field)
        lines.append(f'- {cell(edge["A"])} → {cell(edge["B"])} '
                     f'({edge["Length"]}, {edge["RailTypeGuid"][:8]}, {edge["IsDrawable"]})')
    return lines
