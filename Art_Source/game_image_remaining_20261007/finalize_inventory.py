"""Inspect originals and record production evidence; does not alter image pixels."""
import csv
import hashlib
import json
import re
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent
project = root.parent.parent
tasks = json.loads((root / 'tasks.json').read_text(encoding='utf-8-sig'))
assert len(tasks) == 40 and all(t['status'] == 'image_produced' for t in tasks)
tasks[0]['prompt'] = (root / 'sky-revision-prompt.txt').read_text(encoding='utf-8-sig')
tasks[34]['orientation_note'] = 'Generated bracket opens left (right-side bracket); mirror at presentation for left-side bracket.'
guid_files = {}
for path in (project / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([a-f0-9]+)$', path.read_text(encoding='utf-8-sig', errors='replace'), re.M)
    if match:
        guid_files.setdefault(match[1], []).append(str(path.relative_to(project)))
checks = []
for t in tasks:
    art, asset, source = [Path(t[k]) if k == 'source_path' else project / t[k] for k in ('art_path', 'unity_path', 'source_path')]
    digests = [hashlib.sha256(p.read_bytes()).hexdigest() for p in (art, asset, source)]
    assert len(set(digests)) == 1 and digests[0] == t['sha256'], t['id']
    assert len(guid_files[t['guid']]) == 1, (t['id'], guid_files[t['guid']])
    im = Image.open(art)
    if t['transparent_background']:
        assert im.mode == 'RGBA' and im.getchannel('A').getextrema()[0] == 0
    if t['name'].startswith('portraits/'):
        assert not t['dense_art_touches_canvas_edge'], t['id']
    if t['id'] == 'EXT-038':
        assert im.getchannel('A').getpixel((im.width // 2, im.height // 2)) == 0
    checks.append({'id': t['id'], 'copies_match': True, 'guid_unique_in_Assets': True,
                   'size': list(im.size), 'alpha_min': t['alpha_min'],
                   'alpha_max': t['alpha_max'], 'edge_contact': t['dense_art_touches_canvas_edge']})
pairs = []
for i in range(11, 23, 2):
    a, b = tasks[i:i+2]
    pairs.append({'ready': a['id'], 'changed': b['id'],
                  'equal_canvas': (a['width'], a['height']) == (b['width'], b['height']),
                  'ready_bbox': a['dense_bbox_top_left'], 'changed_bbox': b['dense_bbox_top_left'],
                  'pixel_geometry_identical': 'not guaranteed; Unity alignment review pending'})
assert all(p['equal_canvas'] for p in pairs)
report = {'selected_count': 40, 'generated_count': len(list(root.rglob('*.png'))),
          'transparent_count': sum(t['transparent_background'] for t in tasks),
          'image_processing': 'none; source and Unity PNGs copied byte-identically',
          'checks': checks, 'state_pairs': pairs,
          'game_tests_run': False, 'Unity_import_and_play': 'user validation pending'}
(root / 'QA.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
(root / 'tasks.json').write_text(json.dumps(tasks, ensure_ascii=False, indent=2), encoding='utf-8')
(root / 'manifest.json').write_text(json.dumps({'generator': 'built-in image_gen', 'planned': 40,
    'produced': 40, 'generated_including_archives': report['generated_count'],
    'runtime_connected': False, 'unity_validation': 'pending', 'assets': tasks},
    ensure_ascii=False, indent=2), encoding='utf-8')
with (root / 'inventory.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=['id', 'label', 'art_path', 'unity_path', 'width', 'height', 'guid', 'status', 'runtime_connected', 'unity_validation'])
    writer.writeheader()
    writer.writerows({k: t.get(k, '') for k in writer.fieldnames} for t in tasks)
page = (root / 'gallery.html').read_text(encoding='utf-8')
page = page.replace('Abyss 게임 전체 이미지 목록', 'Abyss 추가 이미지 40종')
page = page.replace('<a href="IMAGE_LIST.md">전체 조사 목록</a>', '<a href="../game_image_inventory_20261007/gallery.html">이전 전체 이미지 45종</a> · <a href="IMAGE_LIST.md">추가 제작 목록</a> · <a href="README.md">연결 인계와 확인 항목</a>')
(root / 'gallery.html').write_text(page, encoding='utf-8')
print(json.dumps({k: report[k] for k in ('selected_count', 'generated_count', 'transparent_count')}, ensure_ascii=False))
