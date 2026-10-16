from . import cell, read_json


def summarize(box):
    manifest = read_json(box / 'manifest.json')
    lines = ['## manifest']
    for key in ('kind', 'createdAt', 'description', 'steamId', 'isEditor', 'platform', 'reportTick',
                'repository', 'masterData', 'buildInfo'):
        lines.append(f'- {key}: {cell(manifest.get(key))}')
    # 識別子欠損は理由と一緒に示し、missingは省略しない
    # Show the identity omission reason and retain every missing entry
    missing = manifest.get('missing', [])
    if not manifest.get('steamId'):
        reasons = [item.get('reason') for item in missing if item.get('item') == 'steamId']
        lines.append(f'- steamId missing 理由: {cell(reasons or "manifestに理由なし")}')
    lines.append('- missing: ' + cell(missing))
    return lines
