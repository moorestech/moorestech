import struct
from . import cell, read_json
from .validation import required, objects


def snapshots(box):
    files = sorted((box / 'snapshots').glob('tick_*.json'),
                   key=lambda path: int(path.stem.removeprefix('tick_')))
    for path in files:
        yield int(path.stem.removeprefix('tick_')), read_json(path)


def decode(bits, field):
    # 実箱はInt64のdoubleビット列。小さい値もfloat32へ推測変換しない
    # Real boxes store Int64 double bits; small values must not imply float32
    if not isinstance(bits, int) or isinstance(bits, bool) or not -(2 ** 63) <= bits < 2 ** 63:
        raise ValueError(f'{field}: Int64 の速度/距離ビット列が必要: {bits!r}')
    return struct.unpack('<d', struct.pack('<q', bits))[0]


def summarize(box):
    lines = ['## 列車', '| train | tick | IsAutoRun | speed | distance | Diagram | 先頭ノード |',
             '| --- | --- | --- | --- | --- | --- | --- |']
    count = 0
    for tick, snapshot in snapshots(box):
        path = box / f'snapshots/tick_{tick}.json'
        trains = required(snapshot, 'trainUnits', list, path, '')
        objects(trains, path, 'trainUnits')
        for index, train in enumerate(trains):
            field = f'trainUnits[{index}].'
            required(train, 'TrainUnitInstanceId', str, path, field)
            required(train, 'IsAutoRun', bool, path, field)
            for key in ('CurrentSpeedBits', 'AccumulatedDistanceBits'):
                decode(required(train, key, int, path, field), f'{path}: {field}{key}')
            if 'Diagram' not in train:
                raise ValueError(f'{path}: {field}Diagram: 必須キーが無い')
            diagram = train['Diagram']
            if diagram is not None:
                if not isinstance(diagram, dict):
                    raise ValueError(f'{path}: {field}Diagram: object または null が必要')
                objects(required(diagram, 'Entries', list, path, field + 'Diagram.'), path, field + 'Diagram.Entries')
                required(diagram, 'CurrentIndex', int, path, field + 'Diagram.')
            position = required(train, 'railPositionSaveData', dict, path, field)
            nodes = required(position, 'RailSnapshot', list, path, field + 'railPositionSaveData.')
            objects(nodes, path, field + 'railPositionSaveData.RailSnapshot')
            summary = ('なし' if diagram is None else
                       f'登録{len(diagram["Entries"])} / index={diagram["CurrentIndex"]}')
            values = (train['TrainUnitInstanceId'], tick, str(train['IsAutoRun']),
                      decode(train['CurrentSpeedBits'], f'{path}: {field}CurrentSpeedBits'),
                      decode(train['AccumulatedDistanceBits'], f'{path}: {field}AccumulatedDistanceBits'),
                      summary, nodes[0] if nodes else 'ノードなし')
            lines.append('| ' + ' | '.join(cell(value) for value in values) + ' |')
            count += 1
    return lines + [f'列車時系列: {count} 行']
