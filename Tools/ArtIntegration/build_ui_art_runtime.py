"""채택 UI 그림 44장 → 런타임 Resources 패키지(A2 · 2026-10-09).

원본(Assets/Art/UI/...)은 읽기만 한다. 산출:

- Assets/Resources/ArtIntegration/UI/{name}.png   — 원본 PNG 바이트 그대로 복사(픽셀 편집 없음)
- 같은 이름 .png.meta                               — Sprite(Single) · Point · 무압축 · 밉맵 없음 · 읽기 불가 · NPOT 그대로
- Assets/Resources/ArtIntegration/UI/ui_art_index.json (+ .meta) — 런타임 색인
- 폴더 .meta 하나(UI)

색인 값(전부 결정적, 오프라인 읽기 전용 알파 분석):
- 보이는 사각형(x,y,w,h · 위→아래 · 원본 픽셀): 알파 >= 8 인 픽셀의 경계. 투명 여백을 런타임 Sprite rect 에서 잘라낸다.
- inner*(프레임만): 보이는 사각형 기준 0..1 · 아래→위. 중심에서 가로·세로 한 줄씩 알파 >= 128 을 만날 때까지
  나아가 얻은 개구부 — 내용(게이지·아이콘·글자)을 넣을 자리.
- slot*(통화·대화 프레임만): 개구부 왼쪽 칸(구분선 왼쪽). 통화 아이콘 자리.
- 런타임은 실제 텍스처 크기/원본 크기 비율로 사각형을 환산한다 — maxTextureSize 로 줄어도 같은 자리를 자른다.

0번 열(키)에 들어가는 ID 는 데이터에서 확인한다: 보스 enemyId · 상점 shopId 가 실제 에셋에 없으면 중단한다(지어내지 않는다).

사용: python -I Tools/ArtIntegration/build_ui_art_runtime.py  (저장소 루트에서)
A1 도구(build_enemy_atlas_runtime.py)와 산출 폴더·guid 이름공간이 다르다.
"""

import hashlib
import json
import os
import re
import shutil
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "Art", "UI")
OUT_PARENT = os.path.join(ROOT, "Assets", "Resources", "ArtIntegration")
OUT = os.path.join(OUT_PARENT, "UI")
INDEX_NAME = "ui_art_index"

VISIBLE_ALPHA = 8
OPAQUE_ALPHA = 128

# 범주별 maxTextureSize — 화면에서 쓰는 크기의 2배 안팎. 원본 1254px 를 그대로 올리면 장당 6MB 다.
MAX_SIZE = {"icon": 256, "portrait": 512, "frame": 1024, "frame_small": 512}

# (키, 원본(Assets/Art/UI 기준), 패키지 파일 이름, 범주, 개구부 계산, 왼쪽 칸 계산)
ENTRIES = [
    ("boss/boss_abyss_keeper", "CodexBossHud/bosses/abyss-keeper-portrait-v2.png", "boss_abyss_keeper_portrait", "portrait", False, False),
    ("boss/boss_flame_serpent", "CodexBossHud/bosses/flame-serpent-portrait-v1.png", "boss_flame_serpent_portrait", "portrait", False, False),
    ("boss/midboss_sentinel", "CodexBossHud/bosses/sentinel-portrait-v1.png", "midboss_sentinel_portrait", "portrait", False, False),
    ("boss/midboss_throne_warden", "CodexBossHud/bosses/throne-warden-portrait-v1.png", "midboss_throne_warden_portrait", "portrait", False, False),
    ("boss/boss_thronebound", "CodexBossHud/bosses/thronebound-portrait-v1.png", "boss_thronebound_portrait", "portrait", False, False),
    ("codex/entry_frame", "CodexBossHud/codex/entry-frame-v1.png", "codex_entry_frame", "frame_small", True, False),
    ("codex/portrait_frame", "CodexBossHud/codex/portrait-frame-v1.png", "codex_portrait_frame", "frame_small", True, False),
    ("codex/tab_form", "CodexBossHud/codex/tab-form-v1.png", "codex_tab_form", "icon", False, False),
    ("codex/tab_skill", "CodexBossHud/codex/tab-skill-v1.png", "codex_tab_skill", "icon", False, False),
    ("codex/tab_enemy", "CodexBossHud/codex/tab-enemy-v1.png", "codex_tab_enemy", "icon", False, False),
    ("codex/tab_boss", "CodexBossHud/codex/tab-boss-v1.png", "codex_tab_boss", "icon", False, False),
    ("codex/tab_relic", "CodexBossHud/codex/tab-relic-v1.png", "codex_tab_relic", "icon", False, False),
    ("codex/tab_records", "CodexBossHud/codex/tab-records-v1.png", "codex_tab_records", "icon", False, False),
    ("codex/undiscovered", "CodexBossHud/codex/undiscovered-v1.png", "codex_undiscovered", "icon", False, False),
    ("hud/player_health_frame", "CodexBossHud/hud/player-health-frame-v1.png", "hud_player_health_frame", "frame", True, False),
    ("hud/boss_health_frame", "CodexBossHud/hud/boss-health-frame-v1.png", "hud_boss_health_frame", "frame", True, False),
    ("hud/currency_frame", "CodexBossHud/hud/currency-frame-v1.png", "hud_currency_frame", "frame", True, True),
    ("hud/form_slot_frame", "CodexBossHud/hud/form-slot-frame-v1.png", "hud_form_slot_frame", "frame_small", True, False),
    ("hud/skill_slot_frame", "CodexBossHud/hud/skill-slot-frame-v1.png", "hud_skill_slot_frame", "frame_small", True, False),
    ("hud/synergy_frame", "CodexBossHud/hud/synergy-frame-v1.png", "hud_synergy_frame", "frame_small", True, False),
    ("currency/abyss_shards", "GameImageInventory/common/abyss-shards-v1.png", "currency_abyss_shards", "icon", False, False),
    ("currency/gold_shards", "GameImageInventory/common/gold-shards-v1.png", "currency_gold_shards", "icon", False, False),
    ("common/reroll", "GameImageInventory/common/reroll-v1.png", "common_reroll", "icon", False, False),
    ("common/save_record", "GameImageInventory/common/save-record-v1.png", "common_save_record", "icon", False, False),
    ("route/combat", "GameImageInventory/routes/combat-v1.png", "route_combat", "icon", False, False),
    ("route/elite", "GameImageInventory/routes/elite-v1.png", "route_elite", "icon", False, False),
    ("route/rest", "GameImageInventory/routes/rest-v1.png", "route_rest", "icon", False, False),
    ("route/shop", "GameImageInventory/routes/shop-v1.png", "route_shop", "icon", False, False),
    ("synergy/fire", "GameImageInventory/synergy/fire-v1.png", "synergy_fire", "icon", False, False),
    ("synergy/abyss", "GameImageInventory/synergy/abyss-v1.png", "synergy_abyss", "icon", False, False),
    ("synergy/guard", "GameImageInventory/synergy/guard-v1.png", "synergy_guard", "icon", False, False),
    ("synergy/blood_pact", "GameImageInventory/synergy/blood-pact-v1.png", "synergy_blood_pact", "icon", False, False),
    ("synergy/frost", "GameImageRemaining/synergy/frost-reserved-v1.png", "synergy_frost_reserved", "icon", False, False),
    ("synergy/soul", "GameImageRemaining/synergy/soul-reserved-v1.png", "synergy_soul_reserved", "icon", False, False),
    ("ui/dialogue_frame", "GameImageInventory/ui/dialogue-frame-v1.png", "ui_dialogue_frame", "frame", True, True),
    ("ui/tutorial_frame", "GameImageInventory/ui/tutorial-frame-v1.png", "ui_tutorial_frame", "frame", True, False),
    ("ui/run_result_emblem", "GameImageInventory/ui/run-result-emblem-v1.png", "ui_run_result_emblem", "icon", False, False),
    ("shop/shop_ash_trader", "GameImageRemaining/portraits/ash-trader-v2.png", "shop_ash_trader_portrait", "portrait", False, False),
    ("shop/shop_grave_robber", "GameImageRemaining/portraits/grave-dealer-v2.png", "shop_grave_robber_portrait", "portrait", False, False),
    ("shop/shop_wandering_peddler", "GameImageRemaining/portraits/run-peddler-v1.png", "shop_wandering_peddler_portrait", "portrait", False, False),
    ("ui/focus_bracket", "GameImageRemaining/ui/focus-bracket-v1.png", "ui_focus_bracket", "frame_small", False, False),
    ("ui/input_key_frame", "GameImageRemaining/ui/input-key-frame-v1.png", "ui_input_key_frame", "frame_small", True, False),
    ("ui/interaction_marker", "GameImageRemaining/ui/interaction-marker-v1.png", "ui_interaction_marker", "icon", False, False),
    ("ui/objective_complete", "GameImageRemaining/ui/objective-complete-v1.png", "ui_objective_complete", "icon", False, False),
]

# 키 접두어 → 실제 ID 를 찾을 데이터 폴더와 필드. 없는 ID 는 만들지 않는다.
ID_SOURCES = {
    "boss/": (os.path.join(ROOT, "Assets", "Resources", "Data", "Enemies"), "enemyId"),
    "shop/": (os.path.join(ROOT, "Assets", "Data", "Shops"), "shopId"),
}

TEXTURE_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationMethod: 0
  spriteTessellationDetail: -1
  spriteGeometrySubdivision: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def stable_guid(relative_path):
    """경로에서 파생한 고정 guid(A2 이름공간) — 다시 돌려도 같은 값."""
    return hashlib.md5(("abyss-art-integration-a2-ui/" + relative_path).encode("utf-8")).hexdigest()


def write_lf(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def existing_guids():
    found = set()
    pattern = re.compile(r"^guid: ([0-9a-f]{32})", re.MULTILINE)
    for base in (os.path.join(ROOT, "Assets"), os.path.join(ROOT, "Packages")):
        for folder, _, files in os.walk(base):
            if folder.startswith(OUT):
                continue
            for name in files:
                if not name.endswith(".meta"):
                    continue
                if os.path.join(folder, name) == OUT + ".meta":
                    continue
                try:
                    with open(os.path.join(folder, name), encoding="utf-8", errors="ignore") as handle:
                        match = pattern.search(handle.read(400))
                except OSError:
                    continue
                if match:
                    found.add(match.group(1))
    return found


def known_ids(folder, field):
    ids = set()
    pattern = re.compile(r"^\s*" + field + r":\s*(\S+)\s*$", re.MULTILINE)
    for name in os.listdir(folder):
        if not name.endswith(".asset"):
            continue
        with open(os.path.join(folder, name), encoding="utf-8", errors="ignore") as handle:
            ids.update(pattern.findall(handle.read()))
    return ids


def scan(mask, x, y, dx, dy, box):
    """box 안에서 (x,y)부터 불투명을 만날 때까지 나아간다. 만난 칸의 좌표(없으면 box 경계)."""
    width, height = mask.size
    px = mask.load()
    while box[0] <= x < box[2] and box[1] <= y < box[3] and px[x, y] == 0:
        x += dx
        y += dy
    return x, y


def opening(mask, cx, cy, box):
    """(cx,cy) 에서 가로·세로 한 줄 스캔으로 얻은 투명 개구부(위→아래 · 배타 경계)."""
    left = scan(mask, cx, cy, -1, 0, box)[0] + 1
    right = scan(mask, cx, cy, 1, 0, box)[0]
    top = scan(mask, cx, cy, 0, -1, box)[1] + 1
    bottom = scan(mask, cx, cy, 0, 1, box)[1]
    return left, top, right, bottom


def normalized(rect, visible):
    """위→아래 원본 사각형 → 보이는 사각형 기준 0..1 · 아래→위."""
    vx, vy, vw, vh = visible
    left, top, right, bottom = rect
    return {
        "x": round((left - vx) / vw, 5),
        "y": round((vy + vh - bottom) / vh, 5),
        "w": round((right - left) / vw, 5),
        "h": round((bottom - top) / vh, 5),
    }


def analyse(path, wants_inner, wants_slot):
    rgba = Image.open(path).convert("RGBA")
    alpha = rgba.split()[3]
    visible_box = alpha.point(lambda v: 255 if v >= VISIBLE_ALPHA else 0).getbbox()
    if visible_box is None:
        sys.exit(f"빈 그림: {path}")
    vx, vy = visible_box[0], visible_box[1]
    visible = (vx, vy, visible_box[2] - vx, visible_box[3] - vy)

    record = {"srcW": rgba.width, "srcH": rgba.height,
              "x": visible[0], "y": visible[1], "w": visible[2], "h": visible[3],
              "inner": None, "slot": None}
    if not wants_inner:
        return record

    mask = alpha.point(lambda v: 255 if v >= OPAQUE_ALPHA else 0)
    cx = (visible_box[0] + visible_box[2]) // 2
    cy = (visible_box[1] + visible_box[3]) // 2
    if mask.getpixel((cx, cy)) != 0:
        sys.exit(f"프레임 중심이 불투명: {path}")
    inner = opening(mask, cx, cy, visible_box)
    if inner[2] - inner[0] < 8 or inner[3] - inner[1] < 8:
        sys.exit(f"개구부가 너무 작다: {path} {inner}")
    record["inner"] = normalized(inner, visible)

    if wants_slot:
        sx = (visible_box[0] + inner[0]) // 2
        slot = opening(mask, sx, cy, visible_box)
        if slot[2] - slot[0] < 8 or slot[3] - slot[1] < 8:
            sys.exit(f"왼쪽 칸이 너무 작다: {path} {slot}")
        record["slot"] = normalized(slot, visible)
    return record


def main():
    ids = {prefix: known_ids(folder, field) for prefix, (folder, field) in ID_SOURCES.items()}
    os.makedirs(OUT, exist_ok=True)
    taken = existing_guids()

    keys = set()
    names = set()
    records = []
    for key, source, name, category, wants_inner, wants_slot in ENTRIES:
        if key in keys or name in names:
            sys.exit(f"중복 키/이름: {key} {name}")
        keys.add(key)
        names.add(name)

        for prefix, valid in ids.items():
            if key.startswith(prefix) and key[len(prefix):] not in valid:
                sys.exit(f"데이터에 없는 ID: {key}")

        source_path = os.path.join(ART, source.replace("/", os.sep))
        with open(source_path, "rb") as handle:
            digest = hashlib.sha256(handle.read()).hexdigest()
        target_path = os.path.join(OUT, name + ".png")
        shutil.copyfile(source_path, target_path)

        record = analyse(source_path, wants_inner, wants_slot)
        entry = {
            "key": key,
            "resource": "ArtIntegration/UI/" + name,
            "source": "Assets/Art/UI/" + source,
            "sourceSha256": digest,
            "srcW": record["srcW"], "srcH": record["srcH"],
            "x": record["x"], "y": record["y"], "w": record["w"], "h": record["h"],
            "hasInner": record["inner"] is not None,
            "innerX": 0.0, "innerY": 0.0, "innerW": 0.0, "innerH": 0.0,
            "hasSlot": record["slot"] is not None,
            "slotX": 0.0, "slotY": 0.0, "slotW": 0.0, "slotH": 0.0,
        }
        if record["inner"] is not None:
            entry.update(innerX=record["inner"]["x"], innerY=record["inner"]["y"],
                         innerW=record["inner"]["w"], innerH=record["inner"]["h"])
        if record["slot"] is not None:
            entry.update(slotX=record["slot"]["x"], slotY=record["slot"]["y"],
                         slotW=record["slot"]["w"], slotH=record["slot"]["h"])
        records.append((entry, category))

    index = {"version": 1, "entries": [entry for entry, _ in records]}
    write_lf(os.path.join(OUT, INDEX_NAME + ".json"), json.dumps(index, ensure_ascii=False, indent=1) + "\n")

    metas = [
        ("Assets/Resources/ArtIntegration/UI", OUT + ".meta", FOLDER_META, None),
        (f"Assets/Resources/ArtIntegration/UI/{INDEX_NAME}.json",
         os.path.join(OUT, INDEX_NAME + ".json.meta"), TEXT_META, None),
    ]
    for entry, category in records:
        file_name = entry["resource"].split("/")[-1] + ".png"
        metas.append((f"Assets/Resources/ArtIntegration/UI/{file_name}",
                      os.path.join(OUT, file_name + ".meta"), TEXTURE_META, MAX_SIZE[category]))

    for relative, meta_path, template, max_size in metas:
        guid = stable_guid(relative)
        if guid in taken:
            sys.exit(f"guid 충돌: {relative} {guid}")
        taken.add(guid)
        write_lf(meta_path, template.format(guid=guid, max_size=max_size))

    for entry, category in records:
        inner = f" inner({entry['innerX']},{entry['innerY']},{entry['innerW']},{entry['innerH']})" if entry["hasInner"] else ""
        slot = f" slot({entry['slotX']},{entry['slotY']},{entry['slotW']},{entry['slotH']})" if entry["hasSlot"] else ""
        print(f"{entry['key']}: {entry['srcW']}x{entry['srcH']} visible({entry['x']},{entry['y']},{entry['w']},{entry['h']}){inner}{slot}")
    print(f"UI 그림 {len(records)}장")


if __name__ == "__main__":
    main()
