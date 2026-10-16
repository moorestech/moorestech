import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory
from pathlib import Path


def summarize(box, out):
    # 外部ディスク操作の失敗を動画節に隔離する
    # Isolate external disk failures within the video section
    try:
        return generate(box, out)
    except OSError as error:
        reason = f'動画調査失敗: {error}。既存contact-sheet.pngは今回の成果物ではありません'
        print(reason, file=sys.stderr)
        return ['## 動画', reason]


def generate(box, out):
    lines = ['## 動画']
    target = out / 'contact-sheet.png'
    target.unlink(missing_ok=True)
    video = box / 'video.mp4'
    if not video.is_file():
        return lines + ['video.mp4 が無いためコンタクトシート未生成']
    ffmpeg = shutil.which('ffmpeg')
    if ffmpeg is None:
        return lines + ['ffmpeg が無いためコンタクトシート未生成']
    # 動画という外部入力の変換失敗を結果へ明示する
    # Surface conversion failures from external video input in the result
    with TemporaryDirectory(prefix='.contact-sheet-', dir=out) as temporary:
        generated = Path(temporary) / 'contact-sheet.png'
        result = subprocess.run([ffmpeg, '-y', '-i', str(video), '-vf',
                                 'fps=1/5,scale=320:-1,tile=4x4', '-frames:v', '1',
                                 str(generated)], capture_output=True, text=True)
        if result.returncode != 0:
            print('ffmpeg 失敗: ' + result.stderr[-2000:], file=sys.stderr)
            return lines + ['ffmpeg 失敗: ' + result.stderr[-2000:]]
        if not generated.is_file():
            return lines + ['動画に対象フレームが無くコンタクトシート未生成']
        generated.replace(target)
    return lines + ['contact-sheet.png を生成']
