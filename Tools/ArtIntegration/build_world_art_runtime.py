"""Package selected world PNGs unchanged; inspect alpha offline, never at runtime."""
from pathlib import Path
import hashlib
import json
import uuid
import shutil
from PIL import Image
from build_ui_art_runtime import TEXTURE_META

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/ArtIntegration/World'
ENTRIES = []

def add(key, source, full=False):
    ENTRIES.append((key, source, full))

for stage in (2, 3):
    for layer in (['sky'] if stage == 2 else []) + ['far', 'mid', 'near', 'ground', 'platform']:
        version = 2 if layer in ('sky', 'far') else 1
        add(f'stage{stage}/{layer}', f'Assets/Art/UI/GameImageRemaining/stage{stage}/{layer}-v{version}.png', layer not in ('ground', 'platform'))
    version = 1 if stage == 2 else 2
    add(f'stage{stage}/boss', f'Assets/Art/UI/GameImageInventory/environment/stage{stage}-background-v{version}.png', True)

for event, prefix, states in [('broken_altar','broken-altar',('ready','spent')),('sealed_door','sealed-door',('closed','open')),('forgotten_cache','cache',('closed','open')),('hollow_crown','crown',('ready','spent')),('oath_stone','oath-stone',('ready','spent')),('abyssal_spring','spring',('ready','spent'))]:
    for state, suffix in zip(('ready','spent'),states):
        add(f'event/{event}/{state}', f'Assets/Art/UI/GameImageRemaining/props/{prefix}-{suffix}-v1.png')
for name in ['descending-stairs','return-stairs']:
    add('exit/'+name, f'Assets/Art/UI/GameImageRemaining/exits/{name}-v1.png')
for name in ['hit-spark','critical-impact','block-impact','just-guard','landing-dust','slash-arc']:
    add('fx/'+name, f'Assets/Art/UI/GameImageRemaining/vfx/{name}-v1.png')
for category, count in [('decor',5),('landmark',4)]:
    for i in range(1,count+1):
        add(f'stage1/{category}_{i}', f'Assets/Art/Environment/stage1_rift_entrance/prop_{category}_{i}.png')

def meta(path, body):
    target = Path(str(path)+'.meta')
    if not target.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'abyss-world-art/'+path.relative_to(ROOT).as_posix()).hex
        target.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+body, encoding='utf-8', newline='\n')

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    meta(OUT, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    records=[]
    for key, source, full in ENTRIES:
        src=ROOT/source
        name=key.replace('/','_')+'.png'
        dst=OUT/name
        shutil.copyfile(src,dst)
        with Image.open(src) as im:
            rgba=im.convert('RGBA'); w,h=rgba.size
            box=(0,0,w,h) if full else rgba.getchannel('A').point(lambda a: 255 if a>=8 else 0).getbbox()
        x,y,right,bottom=box
        sha=hashlib.sha256(src.read_bytes()).hexdigest()
        records.append(dict(key=key,resource='ArtIntegration/World/'+dst.stem,source=source,sourceSha256=sha,srcW=w,srcH=h,x=x,y=y,w=right-x,h=bottom-y))
        target=Path(str(dst)+'.meta')
        if not target.exists():
            guid=uuid.uuid5(uuid.NAMESPACE_URL,'abyss-world-art/'+dst.relative_to(ROOT).as_posix()).hex
            target.write_text(TEXTURE_META.format(guid=guid,max_size=2048),encoding='utf-8',newline='\n')
    index=OUT/'world_art_index.json'
    index.write_text(json.dumps({'entries':records},ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    meta(index,'TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    print(f'Packaged {len(records)} unchanged PNGs')

if __name__=='__main__': main()
