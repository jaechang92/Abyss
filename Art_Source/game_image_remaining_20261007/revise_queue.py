import json
from pathlib import Path

root = Path(__file__).resolve().parent
tasks = json.loads((root / 'tasks.json').read_text(encoding='utf-8-sig'))
revisions = {
    1: ('Preserve the palette, but replace this full opaque background with ONLY a low distant band of indistinct oxidized iron silhouettes along the bottom quarter. Entire upper three quarters MUST be true alpha-zero transparency. Remove every cloud and the sky, remove foreground buildings, no floor or arch. A separate existing sky will show behind this. Very low contrast charcoal/rust gray pixels, no glow.', True),
    6: ('Replace this scenic composition with ONLY an opaque very low contrast distant architectural plane: uniform pale yellowed gray haze, a narrow bottom band of small unidentifiable worn vertical silhouettes. REMOVE ALL near foreground columns, masonry platforms, recognizable arches, buildings and the floor. No clouds or sky gradient, no palace, castle, throne, flags or crown. Quiet flat gray background, no foreground details.', False),
    26: ('Preserve this exact character identity and outfit. Zoom out so the WHOLE bust silhouette including cap and both shoulders sits inside the canvas with at least 10 percent alpha-zero transparent margin on ALL four sides. Finish the lower bust contour naturally. No part clipped or touching an edge, no backdrop or ground shadow. Crisp pixel clusters.', True),
    27: ('Preserve this exact character identity and outfit. Zoom out so the WHOLE bust silhouette including hair, satchel and both shoulders sits inside the canvas with at least 10 percent alpha-zero transparent margin on ALL four sides. Finish the lower bust contour naturally. No part clipped or touching an edge, no backdrop or ground shadow. Crisp pixel clusters.', True),
}
listing = (root / 'IMAGE_LIST.md').read_text(encoding='utf-8')
for idx, (prompt, transparent) in revisions.items():
    t = tasks[idx]
    old = t['name']
    t.setdefault('archived_variants', []).append(dict(t, archived_variants=[]))
    t['reference_path'] = str(root / (old + '.png'))
    t['name'] = old.replace('-v1', '-v2')
    t['prompt'] = prompt
    t['transparent_background'] = transparent
    t['status'] = 'waiting'
    listing = listing.replace(old + '.png', t['name'] + '.png')
    for key in ['art_path', 'unity_path', 'source_path']:
        t.pop(key, None)
(root / 'tasks.json').write_text(json.dumps(tasks, ensure_ascii=False, indent=2), encoding='utf-8')
(root / 'IMAGE_LIST.md').write_text(listing, encoding='utf-8')
