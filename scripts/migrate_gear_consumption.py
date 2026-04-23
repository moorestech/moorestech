#!/usr/bin/env python3
"""
Gear系ブロックの blockParam を gearConsumption ネストオブジェクトに移行するスクリプト
Migrates Gear-type block blockParam keys to the new gearConsumption nested object.

Usage:
    python3 scripts/migrate_gear_consumption.py <blocks.json> [<blocks.json> ...]
"""

import json
import sys
import os

# 移行対象のblockType一覧
# List of blockTypes to migrate
GEAR_TYPES = {
    'Gear',
    'Shaft',
    'GearChainPole',
    'GearMachine',
    'GearMiner',
    'GearMapObjectMiner',
    'GearPump',
    'GearBeltConveyor',
    'GearElectricGenerator',
}

# 削除対象の旧キー一覧
# Old keys to remove from blockParam
OLD_KEYS = {'requireTorque', 'requiredRpm', 'requireTorquePerRpm', 'requiredTorque'}


def migrate_block_param(block_type: str, param: dict) -> int:
    """
    blockParamを新フォーマットに変換する。変換した場合1、対象外は0を返す
    Converts blockParam to new format. Returns 1 if migrated, 0 if skipped.
    """
    if block_type not in GEAR_TYPES:
        return 0

    # 既にgearConsumptionが存在する場合はスキップ
    # Skip if gearConsumption already exists
    if 'gearConsumption' in param:
        return 0

    gear_consumption = {}

    if block_type == 'Gear':
        # baseTorque = requireTorque、baseRpm/minimumRpmはデフォルト値5/5
        # baseTorque = requireTorque; baseRpm/minimumRpm use defaults 5/5
        gear_consumption = {
            'baseTorque': param.pop('requireTorque', 0),
        }

    elif block_type == 'Shaft':
        # baseTorque = requireTorque。旧来のrequiredRpmも存在すれば削除する
        # baseTorque = requireTorque. Also remove legacy requiredRpm if present.
        gear_consumption = {
            'baseTorque': param.pop('requireTorque', 0),
        }
        param.pop('requiredRpm', None)

    elif block_type == 'GearChainPole':
        # 消費は強制ゼロ。requireTorqueがあれば削除する
        # Consumption forced to zero; remove requireTorque if present
        param.pop('requireTorque', None)
        gear_consumption = {
            'baseTorque': 0,
        }

    elif block_type in ('GearMachine', 'GearMiner', 'GearMapObjectMiner', 'GearPump'):
        # baseRpm = requiredRpm、minimumRpm = requiredRpm、baseTorque = requireTorque
        # baseRpm = requiredRpm, minimumRpm = requiredRpm, baseTorque = requireTorque
        required_rpm = param.pop('requiredRpm', 0)
        require_torque = param.pop('requireTorque', 0)
        gear_consumption = {
            'baseRpm': required_rpm,
            'minimumRpm': required_rpm,
            'baseTorque': require_torque,
        }

    elif block_type == 'GearBeltConveyor':
        # 固定値: baseRpm=500、minimumRpm=500、baseTorque=1
        # Fixed values: baseRpm=500, minimumRpm=500, baseTorque=1 (normalized at 500 RPM rating)
        param.pop('requireTorquePerRpm', None)
        param.pop('requireTorque', None)
        gear_consumption = {
            'baseRpm': 500,
            'minimumRpm': 500,
            'baseTorque': 1,
        }

    elif block_type == 'GearElectricGenerator':
        # requiredTorque（スペルに注意）とrequiredRpmを使用
        # Uses requiredTorque (note spelling) and requiredRpm
        required_rpm = param.pop('requiredRpm', 0)
        required_torque = param.pop('requiredTorque', 0)
        gear_consumption = {
            'baseRpm': required_rpm,
            'minimumRpm': required_rpm,
            'baseTorque': required_torque,
        }

    # gearConsumptionをblockParamに追加する
    # Add gearConsumption to blockParam
    param['gearConsumption'] = gear_consumption
    return 1


def migrate_file(file_path: str) -> None:
    """
    1ファイルを読み込み、Gear系ブロックを移行して書き戻す
    Loads one file, migrates Gear-type blocks, and writes back.
    """
    if not os.path.exists(file_path):
        print(f'[SKIP] File not found: {file_path}')
        return

    with open(file_path, encoding='utf-8') as f:
        try:
            data = json.load(f)
        except json.JSONDecodeError as e:
            print(f'[ERROR] Failed to parse JSON in {file_path}: {e}')
            return

    # data["data"]配列が存在しない場合はスキップ
    # Skip if data["data"] array is missing
    if 'data' not in data or not isinstance(data['data'], list):
        print(f'[SKIP] No "data" array in {file_path}')
        return

    migrated_count = 0
    for block in data['data']:
        block_type = block.get('blockType', '')
        param = block.get('blockParam')
        if param is None or not isinstance(param, dict):
            continue
        migrated_count += migrate_block_param(block_type, param)

    # JSON書き戻し。indent=2、日本語をそのまま保持する
    # Write back JSON with indent=2, preserving Japanese characters
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write('\n')

    print(f'[OK] {file_path}: {migrated_count} Gear-type block(s) migrated')


def main() -> None:
    if len(sys.argv) < 2:
        print(f'Usage: {sys.argv[0]} <blocks.json> [<blocks.json> ...]')
        sys.exit(1)

    for path in sys.argv[1:]:
        migrate_file(path)


if __name__ == '__main__':
    main()
