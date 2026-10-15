"""적 투명 atlas 18장 → 런타임 Resources 패키지(A1 · 2026-10-09).

원본 제작 패키지(Assets/Art/EnemyAnimationProduction)는 읽기만 한다. 산출:

- Assets/Resources/ArtIntegration/Enemies/{atlas}.png   — 원본 sprite-sheet-alpha.png 바이트 그대로 복사
- 같은 이름 .png.meta                                     — Sprite(Single) · Point · 무압축 · 밉맵 없음 · NPOT 그대로
- Assets/Resources/ArtIntegration/Enemies/enemy_atlas_index.json (+ .meta) — 런타임 색인
- 폴더 .meta 두 개

색인에 들어가는 값(전부 결정적):
- 프레임 사각형: manifest.frame_layout 그대로(위→아래 좌표). 아래→위 변환은 런타임이 sheetHeight 로 한다.
- footY: 프레임 칸 안의 불투명 픽셀 최하단(위에서 잰 배타 경계). 프레임마다 다르다 — 추출기가
  프레임마다 안전 영역(12..112)에 맞춰 가운데 배치했기 때문이다.
- pixelsPerUnit: 기존 화면 크기 보존. 기존 그림(이동 첫 칸)의 보이는 높이(렌더러 로컬 유닛)에
  새 이동 첫 칸의 보이는 높이를 맞춘다. 비율이 32 의 ±10% 안이면 32 로 고정(정수 픽셀 격자 유지).

사용: python -I Tools/ArtIntegration/build_enemy_atlas_runtime.py  (저장소 루트에서)
"""

import hashlib
import json
import os
import re
import shutil
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Art", "EnemyAnimationProduction")
OUT_PARENT = os.path.join(ROOT, "Assets", "Resources", "ArtIntegration")
OUT = os.path.join(OUT_PARENT, "Enemies")
PREFABS = os.path.join(ROOT, "Assets", "Prefabs", "Enemies")
OLD_SPRITES = os.path.join(ROOT, "Assets", "Art", "Sprites", "Enemies")

BASE_PPU = 32.0
SNAP_TOLERANCE = 0.10
INDEX_NAME = "enemy_atlas_index"

# 기존 화면 크기의 기준 그림. 엘리트 변종 둘은 지금까지 빌려 쓰던 원본과 같은 몸이다(EnemyData.artSourceId).
# 값: (그림 파일 · 기준 칸 사각형(위→아래, None 이면 전체) · 그 그림의 PPU)
OLD_REFERENCE = {
    "melee_grunt": ("melee_grunt/southeast/melee_grunt_move_southeast.png", (0, 0, 148, 148), 32.0),
    "melee_brute": ("melee_brute/southeast/melee_brute_move_southeast.png", (0, 0, 148, 148), 32.0),
    "ranged_archer": ("ranged_archer/southeast/ranged_archer_move_southeast.png", (0, 0, 148, 148), 32.0),
    "bone_archer": ("bone_archer/southeast/bone_archer_move_southeast.png", (0, 0, 148, 148), 32.0),
    "void_caster": ("void_caster/southeast/void_caster_move_southeast.png", (0, 0, 148, 148), 32.0),
    "flame_mortar": ("flame_mortar/southeast/flame_mortar_move_southeast.png", (0, 0, 148, 148), 32.0),
    "elite_hunter": ("elite_hunter/southeast/elite_hunter_move_southeast.png", (0, 0, 148, 148), 32.0),
    "midboss_sentinel": ("midboss_sentinel/southeast/midboss_sentinel_move_southeast.png", (0, 0, 148, 148), 32.0),
    "elite_berserker": ("melee_brute/southeast/melee_brute_move_southeast.png", (0, 0, 148, 148), 32.0),
    "elite_summoner": ("void_caster/southeast/void_caster_move_southeast.png", (0, 0, 148, 148), 32.0),
    "boss_abyss_keeper": ("boss_abyss_keeper.png", None, 16.0),
    "boss_flame_serpent": ("boss_flame_serpent.png", None, 16.0),
    "boss_thronebound": ("boss_thronebound.png", None, 16.0),
    "midboss_throne_warden": ("midboss_throne_warden.png", None, 16.0),
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
  maxTextureSize: 2048
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
  spritePixelsToUnits: 32
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
    maxTextureSize: 2048
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
    """경로에서 파생한 고정 guid — 다시 돌려도 같은 값이라 참조가 흔들리지 않는다."""
    return hashlib.md5(("abyss-art-integration-a1/" + relative_path).encode("utf-8")).hexdigest()


def write_lf(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def existing_guids():
    found = set()
    pattern = re.compile(r"^guid: ([0-9a-f]{32})", re.MULTILINE)
    for base in (os.path.join(ROOT, "Assets"), os.path.join(ROOT, "Packages")):
        for folder, _, files in os.walk(base):
            if folder.startswith(OUT_PARENT):
                continue
            for name in files:
                if not name.endswith(".meta"):
                    continue
                try:
                    with open(os.path.join(folder, name), encoding="utf-8", errors="ignore") as handle:
                        match = pattern.search(handle.read(400))
                except OSError:
                    continue
                if match:
                    found.add(match.group(1))
    return found


def visible_height(image_path, rect):
    image = Image.open(image_path).convert("RGBA")
    if rect is not None:
        x, y, w, h = rect
        image = image.crop((x, y, x + w, y + h))
    box = image.split()[3].getbbox()
    return 0 if box is None else box[3] - box[1]


def atlas_name(enemy_id, run_folder):
    suffix = run_folder[len("pattern-"):] if run_folder.startswith("pattern-") else run_folder
    return f"{enemy_id}_{suffix}"


def main():
    with open(os.path.join(SOURCE, "catalog.json"), encoding="utf-8") as handle:
        catalog = json.load(handle)

    os.makedirs(OUT, exist_ok=True)
    taken = existing_guids()
    enemies = {}
    report = []

    for run in catalog["runs"]:
        enemy_id = run["enemy"]
        run_dir = os.path.join(SOURCE, run["run"].replace("/", os.sep))
        with open(os.path.join(run_dir, "manifest.json"), encoding="utf-8") as handle:
            manifest = json.load(handle)

        source_png = os.path.join(run_dir, manifest["sprite_sheet_alpha"])
        with open(source_png, "rb") as handle:
            digest = hashlib.sha256(handle.read()).hexdigest()
        if digest != run["atlas_sha256"]:
            sys.exit(f"atlas 해시 불일치: {source_png}")

        name = atlas_name(enemy_id, os.path.basename(run_dir))
        target_png = os.path.join(OUT, name + ".png")
        shutil.copyfile(source_png, target_png)

        layout = manifest["frame_layout"]
        alpha = Image.open(source_png).convert("RGBA").split()[3]
        if alpha.size != (layout["sheetWidth"], layout["sheetHeight"]):
            sys.exit(f"sheet 크기 불일치: {source_png}")

        entry = enemies.setdefault(enemy_id, {"enemyId": enemy_id, "rows": []})
        for row_id, rects in layout["rows"].items():
            spec = manifest["animation"]["rows"][row_id]
            frames = []
            for rect in rects:
                box = alpha.crop((rect["x"], rect["y"], rect["x"] + rect["w"], rect["y"] + rect["h"])).getbbox()
                if box is None:
                    sys.exit(f"빈 프레임: {name}/{row_id}")
                frames.append({
                    "x": rect["x"], "y": rect["y"], "w": rect["w"], "h": rect["h"],
                    "footY": box[3], "topY": box[1],
                })
            if len(frames) != spec["frames"]:
                sys.exit(f"프레임 수 불일치: {name}/{row_id}")
            entry["rows"].append({
                "id": row_id,
                "atlas": "ArtIntegration/Enemies/" + name,
                "sheetHeight": layout["sheetHeight"],
                "fps": float(spec["fps"]),
                "loop": bool(spec["loop"]),
                "frames": frames,
            })

    for enemy_id, entry in enemies.items():
        move = next(row for row in entry["rows"] if row["id"] == "move")
        first = move["frames"][0]
        new_height = first["footY"] - first["topY"]
        old_file, old_rect, old_ppu = OLD_REFERENCE[enemy_id]
        old_height = visible_height(os.path.join(OLD_SPRITES, old_file.replace("/", os.sep)), old_rect)
        target_units = old_height / old_ppu
        ppu = new_height / target_units
        if abs(ppu / BASE_PPU - 1.0) <= SNAP_TOLERANCE:
            ppu = BASE_PPU
        entry["pixelsPerUnit"] = round(ppu, 4)
        report.append(f"{enemy_id}: old {old_height}px@{old_ppu:g} = {target_units:.3f}u · new {new_height}px → PPU {entry['pixelsPerUnit']}")

    index = {"version": 1, "enemies": [enemies[key] for key in sorted(enemies)]}
    write_lf(os.path.join(OUT, INDEX_NAME + ".json"), json.dumps(index, ensure_ascii=False, indent=1) + "\n")

    metas = [
        ("Assets/Resources/ArtIntegration", OUT_PARENT + ".meta", FOLDER_META),
        ("Assets/Resources/ArtIntegration/Enemies", OUT + ".meta", FOLDER_META),
        (f"Assets/Resources/ArtIntegration/Enemies/{INDEX_NAME}.json",
         os.path.join(OUT, INDEX_NAME + ".json.meta"), TEXT_META),
    ]
    for file_name in sorted(os.listdir(OUT)):
        if file_name.endswith(".png"):
            metas.append((f"Assets/Resources/ArtIntegration/Enemies/{file_name}",
                          os.path.join(OUT, file_name + ".meta"), TEXTURE_META))

    for relative, meta_path, template in metas:
        guid = stable_guid(relative)
        if guid in taken:
            sys.exit(f"guid 충돌: {relative} {guid}")
        taken.add(guid)
        write_lf(meta_path, template.format(guid=guid))

    print("\n".join(report))
    print(f"atlas {sum(1 for f in os.listdir(OUT) if f.endswith('.png'))} · enemies {len(enemies)} · rows "
          f"{sum(len(e['rows']) for e in enemies.values())}")


if __name__ == "__main__":
    main()
