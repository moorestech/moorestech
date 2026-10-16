from . import cell, read_json
from .train_timeline import snapshot_paths
from .validation import required, objects


def summarize(box):
    paths = snapshot_paths(box)
    lines = ['## レール']
    if not paths:
        return lines + ['スナップショットが無い']
    path = paths[-1]
    tick = int(path.stem.removeprefix('tick_'))
    snapshot = read_json(path)
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
