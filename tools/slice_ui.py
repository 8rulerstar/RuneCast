# -*- coding: utf-8 -*-
"""
바깥 에셋 시트에서 UI 조각과 아이콘을 잘라 Resources 아래에 넣는다.

**왜 도구로 만드나:** 손으로 자르면 좌표가 어디서 왔는지 아무 데도 안 남는다.
색을 바꾸거나 다른 조각을 쓰고 싶을 때 시트를 다시 뒤져야 하고, 그때
"이건 왜 이 크기지"를 아무도 대답할 수 없다. 좌표를 코드에 적어 두면
잘못 잘랐을 때 그 줄만 고쳐 다시 돌리면 된다.

**원본은 저장소에 안 넣는다.** 에셋 라이브러리는 밖에 둔다는 규칙을 따른다
(AGENTS.md 5절). 대신 잘라 낸 결과물만 들어가고, 출처는 CREDITS.md에 적는다.

  python tools/slice_ui.py                     기본 위치(~/Downloads)에서
  python tools/slice_ui.py --src /경로/에셋     다른 위치에서

.meta의 GUID는 **경로에서 만든 고정값**이다. Unity가 새로 만들게 두면
파일을 다시 자를 때마다 GUID가 바뀌어 참조가 끊긴다 (AGENTS.md 3절).
"""
import hashlib
import io
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.stderr.write("Pillow가 필요합니다: pip install pillow\n")
    raise SystemExit(1)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "RuneCast", "Assets", "Resources", "Sprites")

HOME = os.path.expanduser("~")
DEFAULT_SRC = os.path.join(HOME, "Downloads")

# ── 원본 시트 ──────────────────────────────────────────────
#
# UI 키트는 낱개 조각이 격자 없이 흩어져 있어서 좌표를 하나씩 적었다.
# 아이콘 팩은 32×32 격자라 (행, 열)로 적는다.
UI_SHEET = "all.png"
ICON_SHEET = os.path.join("Free - Raven Fantasy Icons",
                          "Free - Raven Fantasy Icons",
                          "Full Spritesheet", "32x32.png")

# (파일명, x, y, w, h) — UI 키트에서 잘라 오는 조각.
UI_PARTS = [
    # 얇은 사각 테두리. 가운데가 비어 있어서 **판 위에 겹쳐** 강조에 쓴다.
    # 모서리만 금빛이고 변 가운데는 어두워서, 9-slice로 늘리면
    # 어두운 변에 금빛 모서리가 남는다 — 그게 원하는 그림이다.
    ("frame_sel", 452, 20, 24, 24),
    # 소제목 띠. 아래로 좁아지는 사다리꼴이라 **세로로는 늘리지 않는다**
    # (늘리면 빗변 계단이 뭉개진다). 가로만 9-slice.
    ("ribbon", 0, 168, 80, 18),
]

# 통째로 복사하는 것 — Tiny Swords의 바다 배경 재료 (메뉴 배경용).
# 출처가 ~/Assets 라이브러리라 저장소 밖이고, 여기 적어 두면 어디서 왔는지 남는다.
TS_DIR = os.path.join(HOME, "Assets", "Tiny Swords (Free Pack)", "Terrain")
TS_COPY = [
    ("water_bg", os.path.join("Tileset", "Water Background color.png")),
    ("cloud_1", os.path.join("Decorations", "Clouds", "Clouds_01.png")),
    ("cloud_2", os.path.join("Decorations", "Clouds", "Clouds_04.png")),
    ("cloud_3", os.path.join("Decorations", "Clouds", "Clouds_07.png")),
    # 물바위는 64×64 프레임 16장짜리 가로 스트립 — 일렁이는 애니메이션이다
    ("water_rock_1", os.path.join("Decorations", "Rocks in the Water", "Water Rocks_01.png")),
    ("water_rock_2", os.path.join("Decorations", "Rocks in the Water", "Water Rocks_02.png")),
    ("water_rock_3", os.path.join("Decorations", "Rocks in the Water", "Water Rocks_03.png")),
    ("water_rock_4", os.path.join("Decorations", "Rocks in the Water", "Water Rocks_04.png")),
]

# (파일명, 행, 열) — 아이콘 팩 32×32 격자.
ICONS = [
    ("ic_shard", 1, 9),      # 파편 — 푸른 결정 뭉치
    ("ic_power", 45, 0),     # 주력 — 교차한 검
    ("ic_forge", 7, 12),     # 룬 벼리기 — 모루
    ("ic_glyph", 61, 8),     # 문양 소환 — 반짝임
    ("ic_trophy", 0, 14),    # 기록 — 트로피
    ("ic_rune", 20, 2),      # 각인 — 룬돌
    ("ic_ticket", 19, 3),    # 각인권 — 두루마리
    ("ic_party", 0, 4),      # 부대 편성 — 은빛 투구
    # 등급 보석. 목록에서 **글씨보다 먼저 읽히는 것**이 등급이라 그림을 준다.
    ("ic_gem_common", 10, 7),
    ("ic_gem_rare", 10, 0),
    ("ic_gem_epic", 10, 5),
    ("ic_gem_legend", 10, 3),
]

META = u"""fileFormatVersion: 2
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
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
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
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:{sp}
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:{sp}
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:{sp}
  pSDRemoveMatte: 0
  userData:{sp}
  assetBundleName:{sp}
  assetBundleVariant:{sp}
"""


def guid_for(unity_path):
    """경로에서 만든 고정 GUID. 다시 잘라도 값이 안 변해야 참조가 안 끊긴다."""
    return hashlib.md5(("RuneCast/" + unity_path).encode("utf-8")).hexdigest()


def write(img, out_dir, name):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)

    png = os.path.join(out_dir, name + ".png")
    img.save(png)

    rel = os.path.relpath(png, ROOT).replace(os.sep, "/")
    meta = io.open(png + ".meta", "w", encoding="utf-8", newline="\n")
    meta.write(META.format(guid=guid_for(rel), sp=" "))
    meta.close()
    return png, img.size


def main():
    src = DEFAULT_SRC
    if "--src" in sys.argv:
        src = sys.argv[sys.argv.index("--src") + 1]

    W = sys.stdout.buffer.write

    def say(s):
        W((s + u"\n").encode("utf-8"))

    ui_path = os.path.join(src, UI_SHEET)
    icon_path = os.path.join(src, ICON_SHEET)

    missing = [p for p in (ui_path, icon_path) if not os.path.exists(p)]
    if missing:
        for p in missing:
            say(u"원본이 없습니다: " + p)
        say(u"--src 로 에셋 폴더를 지정하세요.")
        return 1

    ui = Image.open(ui_path).convert("RGBA")
    icons = Image.open(icon_path).convert("RGBA")

    say(u"UI 조각")
    for name, x, y, w, h in UI_PARTS:
        _, size = write(ui.crop((x, y, x + w, y + h)),
                        os.path.join(RES, "UI"), name)
        say(u"  %-12s %dx%d" % (name, size[0], size[1]))

    say(u"아이콘")
    for name, r, c in ICONS:
        cell = icons.crop((c * 32, r * 32, c * 32 + 32, r * 32 + 32))

        # **투명 여백을 잘라 낸다.** 팩 아이콘은 32칸 안에서 제각기 다른
        # 자리에 그려져 있다. 그대로 쓰면 나란히 놓았을 때 크기가 들쭉날쭉
        # 보이고, 가운데 정렬도 어긋난다.
        box = cell.getbbox()
        if box:
            cell = cell.crop(box)

        _, size = write(cell, os.path.join(RES, "Icons"), name)
        say(u"  %-16s %dx%d  (행 %d, 열 %d)" % (name, size[0], size[1], r, c))

    say(u"바다 배경 (Tiny Swords)")
    copied = 0
    for name, rel in TS_COPY:
        src_png = os.path.join(TS_DIR, rel)
        if not os.path.exists(src_png):
            say(u"  %-14s 원본 없음: %s" % (name, src_png))
            continue
        _, size = write(Image.open(src_png).convert("RGBA"),
                        os.path.join(RES, "Terrain"), name)
        say(u"  %-14s %dx%d" % (name, size[0], size[1]))
        copied += 1

    say(u"")
    say(u"%d개 저장 — Unity를 켜면 자동으로 들어옵니다."
        % (len(UI_PARTS) + len(ICONS) + copied))
    return 0


if __name__ == "__main__":
    sys.exit(main())
