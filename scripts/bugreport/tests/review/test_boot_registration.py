from pathlib import Path
import shutil
import subprocess

import pytest

REPO = Path(__file__).resolve().parents[4]
SOURCE = REPO / 'moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs'


@pytest.mark.parametrize('defines,registered', [([], False), (['UNITY_EDITOR'], True), (['DEVELOPMENT_BUILD'], True)])
def test_unattended_distribution_has_no_boot_upload_entry(defines, registered):
    clang = shutil.which('clang')
    if clang is None:
        pytest.skip('clang が無いためdefineなし検証は未確認')
    # 配布版の未同意起動でも共有される登録を、実プリプロセッサで検査する
    # Use the real preprocessor to check registration shared by unattended unconsented distribution boots
    text = '\n'.join(line for line in SOURCE.read_text().splitlines()
                     if not line.lstrip().startswith(('#region', '#endregion')))
    result = subprocess.run([clang, '-E', '-P', '-x', 'c', '-Wno-invalid-pp-token',
                             *('-D' + name for name in defines), '-'],
                            input=text, capture_output=True, text=True, check=True)
    assert ('RegisterEntryPoint<PlaytestBootUploadTrigger>()' in result.stdout) == registered
