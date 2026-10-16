import struct
from . import cell, read_json


def snapshots(box):
    files = sorted((box / 'snapshots').glob('tick_*.json'),
                   key=lambda path: int(path.stem.removeprefix('tick_')))
    for path in files:
        yield int(path.stem.removeprefix('tick_')), read_json(path)


def decode(bits):
    # 実箱はInt64のdoubleビット列。小さい値もfloat32へ推測変換しない
    # Real boxes store Int64 double bits; small values must not imply float32
    if not isinstance(bits, int) or isinstance(bits, bool):
        raise ValueError(f'速度/距離ビット列は整数が必要: {bits!r}')
    return struct.unpack('<d', struct.pack('<q', bits))[0]


def summarize(box):
    lines = ['## 列車', '| train | tick | IsAutoRun | speed | distance | Diagram | 先頭ノード |',
             '| --- | --- | --- | --- | --- | --- | --- |']
    count = 0
    for tick, snapshot in snapshots(box):
        for train in snapshot.get('trainUnits', []):
            diagram = train.get('Diagram')
            summary = ('なし' if diagram is None else
                       f'登録{len(diagram["Entries"])} / index={diagram["CurrentIndex"]}')
            nodes = train['railPositionSaveData']['RailSnapshot']
            values = (train['TrainUnitInstanceId'], tick, str(train['IsAutoRun']),
                      decode(train['CurrentSpeedBits']), decode(train['AccumulatedDistanceBits']),
                      summary, nodes[0] if nodes else 'ノードなし')
            lines.append('| ' + ' | '.join(cell(value) for value in values) + ' |')
            count += 1
    return lines + [f'列車時系列: {count} 行']
