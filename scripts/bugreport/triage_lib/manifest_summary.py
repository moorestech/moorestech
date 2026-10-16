from . import cell, read_json
from .validation import required, objects


def summarize(box):
    manifest = read_json(box / 'manifest.json')
    lines = ['## manifest']
    for key in ('kind', 'createdAt', 'description', 'steamId', 'isEditor', 'platform', 'reportTick',
                'repository', 'masterData', 'buildInfo'):
        lines.append(f'- {key}: {cell(manifest.get(key))}')
    # 識別子欠損は理由と一緒に示し、missingは省略しない
    # Show the identity omission reason and retain every missing entry
    path = box / 'manifest.json'
    missing = required(manifest, 'missing', list, path, '')
    objects(missing, path, 'missing')
    for index, item in enumerate(missing):
        required(item, 'item', str, path, f'missing[{index}].')
        required(item, 'reason', str, path, f'missing[{index}].')
    if not manifest.get('steamId'):
        reasons = [item.get('reason') for item in missing if item.get('item') == 'steamId']
        lines.append(f'- steamId missing 理由: {cell(reasons or "manifestに理由なし")}')
    lines.append('- missing: ' + cell(missing))
    return lines
