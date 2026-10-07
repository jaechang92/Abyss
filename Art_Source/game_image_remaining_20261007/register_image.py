"""Record and copy generated PNGs without editing their pixels."""
import hashlib
import html
import json
import re
import shutil
import sys
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

root = Path(__file__).resolve().parent
project = root.parent.parent
queue_path = root / 'tasks.json'
tasks = json.loads(queue_path.read_text(encoding='utf-8-sig'))
task = next(t for t in tasks if t['id'] == sys.argv[1])
source = Path(sys.argv[2]).resolve()
generated_root = Path(r'C:/Users/JaeChang/.codex/generated_images').resolve()
assert source.is_relative_to(generated_root) and source.suffix == '.png'
dest = root / (task['name'] + '.png')
dest.parent.mkdir(parents=True, exist_ok=True)
if dest.exists():
    assert dest.read_bytes() == source.read_bytes(), 'Do not overwrite existing art'
else:
    shutil.copy2(source, dest)
im = Image.open(dest)
arr = np.array(im)
alpha = arr[:, :, 3] if im.mode == 'RGBA' else np.full((im.height, im.width), 255)
ys, xs = np.where(alpha > 192)
bbox = [int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1)]
edge = bool(np.any(alpha[0] > 192) or np.any(alpha[-1] > 192)
            or np.any(alpha[:, 0] > 192) or np.any(alpha[:, -1] > 192))
if task['transparent_background']:
    assert im.mode == 'RGBA' and int(alpha.min()) == 0, 'Alpha transparency required'
asset_root = project / 'Assets/Art/UI/GameImageRemaining'
asset = asset_root / (task['name'] + '.png')
asset.parent.mkdir(parents=True, exist_ok=True)
if asset.exists():
    assert asset.read_bytes() == dest.read_bytes()
else:
    shutil.copy2(dest, asset)
meta_path = Path(str(asset) + '.meta')
if meta_path.exists():
    guid = re.search(r'^guid: (\w+)', meta_path.read_text(), re.M).group(1)
else:
    template = (project / 'Assets/Resources/UI/DraftImages/card-frame-v1.png.meta').read_text(encoding='utf-8-sig')
    guid = uuid.uuid4().hex
    template = re.sub(r'^guid: .*$', 'guid: ' + guid, template, flags=re.M)
    template = template.replace('  spriteMode: 0', '  spriteMode: 1').replace('  textureType: 0', '  textureType: 8')
    template = template.replace('  spriteGenerateFallbackPhysicsShape: 1', '  spriteGenerateFallbackPhysicsShape: 0')
    template = re.sub(r'    spriteID: .*', '    spriteID: ' + uuid.uuid4().hex, template)
    meta_path.write_text(template, encoding='utf-8', newline='\n')
for folder in [asset_root, asset.parent]:
    folder_meta = Path(str(folder) + '.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex
                              + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')
task.update(status='image_produced', source_path=str(source),
            art_path=dest.relative_to(project).as_posix(),
            unity_path=asset.relative_to(project).as_posix(), guid=guid,
            width=im.width, height=im.height, mode=im.mode,
            alpha_min=int(alpha.min()), alpha_max=int(alpha.max()),
            center_alpha=int(alpha[im.height // 2, im.width // 2]),
            dense_bbox_top_left=bbox, dense_art_touches_canvas_edge=edge,
            sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),
            runtime_connected=False)
queue_path.write_text(json.dumps(tasks, ensure_ascii=False, indent=2), encoding='utf-8')
(root / 'manifest.json').write_text(json.dumps({'generator': 'built-in image_gen',
    'pixel_processing': 'none; byte-identical copies', 'planned': len(tasks),
    'produced': sum(t['status'] == 'image_produced' for t in tasks),
    'unity_validation': 'pending', 'assets': [t for t in tasks if t['status'] == 'image_produced']},
    ensure_ascii=False, indent=2), encoding='utf-8')
listing = root / 'IMAGE_LIST.md'
content = listing.read_text(encoding='utf-8')
content = content.replace('| ' + task['id'] + ' | ' + task['label'] + ' | '
    + task['name'] + '.png | ' + task['evidence'] + ' | 제작대기 |',
    '| ' + task['id'] + ' | ' + task['label'] + ' | ' + task['name'] + '.png | '
    + task['evidence'] + ' | 제작 / 연결·검증대기 |')
listing.write_text(content, encoding='utf-8')
cards = []
for t in tasks:
    filename = t['name'] + '.png'
    preview = ('<a href="' + filename + '"><img src="' + filename + '" alt="' + html.escape(t['label']) + '"></a>') if t['status'] == 'image_produced' else '<span>제작대기</span>'
    cards.append('<article><div class="preview">' + preview + '</div><h2>' + t['id'] + ' · '
        + html.escape(t['label']) + '</h2><p>' + html.escape(t['status']) + '</p></article>')
page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Abyss 전체 이미지 제작</title><style>body{margin:0;padding:28px;background:#111820;color:#e0ddce;font-family:system-ui}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:16px}article{background:#1b2632;border:1px solid #526170;padding:12px}.preview{height:240px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#26333f 0% 25%,#32404c 0% 50%) 0/24px 24px}.preview a{display:contents}.preview img{max-width:100%;max-height:100%;object-fit:contain;image-rendering:pixelated}h1{font-size:24px}h2{font-size:17px}p{font-size:13px;color:#acb6c2}</style><h1>Abyss 게임 전체 이미지 목록</h1><p>2026-10-07 · 이미지 클릭: 원본 · 제작 상태와 게임 연결/검증은 별도</p><p><a href="../ui_codex_boss_hud_20261007/gallery.html">기존 도감·보스·HUD 20종</a> · <a href="IMAGE_LIST.md">전체 조사 목록</a></p><div class="grid">''' + ''.join(cards) + '</div></html>'
(root / 'gallery.html').write_text(page, encoding='utf-8')
print(task['id'], task['label'], 'saved', im.size, 'alpha', task['alpha_min'], task['alpha_max'], 'edge', edge)
