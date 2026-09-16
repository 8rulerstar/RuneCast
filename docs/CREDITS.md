> **공개본 안내** — 이 저장소에는 아래 에셋 파일이 **들어 있지 않습니다.**
> 대부분이 "게임 번들 가능 / 재배포 금지" 조건이라, 빌드된 게임(itch)으로만 배포합니다.
> 이 문서는 무엇을 어디서 가져와 어떻게 가공했는지를 남겨 두는 개발 기록으로 그대로 옮겼습니다.

---

# 에셋 출처

배포 전에 각 항목의 라이선스 원문을 이 파일 옆에 보관할 것.
(팩에 라이선스 텍스트가 동봉되지 않은 경우가 있어, 다운로드 페이지의 약관을 저장해 두는 편이 안전하다.)

## 음악 — `Assets/Resources/Audio/BGM/`

**Abstraction — "Music Loop Bundle"** (Ben Burnes / Tallbeard Studios)
CC-0 (퍼블릭 도메인). 크레딧 불필요, 상업 이용 가능.

- `bgm_wave1~8.ogg`, `bgm_cloak1~3.ogg`, `bgm_menu.ogg` (12곡)

어느 곡이 어울리는지는 들어봐야 알 수 있어서 후보를 넓게 두고,
게임 중 **M키로 다음 곡**으로 넘길 수 있게 했다. 현재 곡 이름은 화면 좌하단에 표시된다.
**곡을 두 무리로 나눴다** (2026-08-01):

- `MenuBgm` — `bgm_menu`, `bgm_cloak1~3`. 타이틀·스테이지 선택·대장장이.
  고르는 화면이라 잔잔한 것들로. 여기서 몰아붙일 이유가 없다.
- `BattleBgm` — `bgm_wave1~8`. 판이 시작될 때 무작위로 하나.

배경을 메뉴/전투로 갈라 놓고 음악이 그대로면 **화면이 바뀐 게 귀로는 안 읽힌다.**
같은 무리 안에서는 곡을 안 갈아끼운다 — 스테이지 선택 → 대장장이 → 설정을 오갈 때마다
곡이 처음부터 다시 시작하면 같은 도입부만 반복해서 듣게 된다.

곡 수를 줄일 때는 두 무리를 각각 줄일 것. 무리 하나가 비면 그 화면이 무음이 된다.

## 효과음 — `Assets/Resources/Audio/SFX/`

**Helton Yan's Pixel Combat**

파일명은 `사건이름_변형번호.wav` 형식이다 (`hit_melee_1.wav` …).
사건 ↔ 원본 매핑은 `Core/AudioManager.FileOf`에 있다.

**원본을 그대로 넣지 않았다.** 원본이 96kHz·24bit·스테레오 스튜디오 마스터라
파일당 2MB, 필요한 소리를 다 넣으면 200MB가 넘는다.
Python으로 **모노 44.1kHz 16bit로 변환 + 꼬리 무음 제거**해서 5MB로 줄였다.
(변환 스크립트는 일회성이라 저장소에 두지 않았다. 다시 필요하면 같은 방식으로 하면 된다:
24bit LE → float 모노 평균 → 무음 트림 → 선형보간 리샘플 → 16bit 기록)

**소리마다 변형을 2~4개 두고 재생할 때마다 무작위로 고르며, 피치도 ±6% 흔든다.**
이 게임은 타격이 초당 여러 번 나서, 변형이 없으면 몇 초 만에 기계음처럼 들린다.

FilmCow SFX는 이 팩으로 전량 교체되어 제거됨.

## 캐릭터 — `Assets/Resources/Sprites/Units/`

**Tiny Swords (Free Pack)** — Pixel Frog · 192×192 가로 스트립 · **아군 전원**

`Units/Blue Units`에서 가져왔다. 파랑을 쓴 이유는 초록(오크)·붉은빛(해골 계열)과
겹치지 않아 전선에서 편이 색으로 갈리기 때문이다.

| 반입 이름 | 원본 | 프레임 |
|---|---|---|
| `Warrior-Idle/Walk/Attack/Guard` | `Warrior/Warrior_Idle/Run/Attack1/Guard` | 8 / 6 / 4 / 6 |
| `Archer-Idle/Walk/Shoot` | `Archer/Archer_Idle/Run/Shoot` | 6 / 4 / 8 |
| `Monk-Idle/Walk/Heal` | `Monk/Idle/Run/Heal` | 6 / 4 / **10** |

- **아군을 Tiny RPG 병사에서 이쪽으로 옮겼다.** 아군은 판마다 넷다섯이 한 줄로
  같이 서 있어서 화풍이 섞이면 화면에서 제일 크게 드러난다. 후반 적(주술사·놀·
  거미·트롤)이 이미 이 팩이라, 옮기는 쪽이 오히려 이질적인 유닛을 줄인다.
- **`Monk/Heal.png`는 원본이 11프레임 2112px으로 임포터 한도(2048)를 넘는다.**
  트롤과 같은 함정이라 **10프레임(1920px)으로 잘라서** 넣었다. 회복 동작이라
  마지막 한 장이 빠져도 동작이 끊겨 보이지 않는다.
- **`Lancer`는 안 가져왔다.** 320px 프레임에 8방향 시트라 혼자만 배선 비용이 두 배인데,
  창병은 전사와 역할이 겹친다. 필요해지면 `Lancer_Idle.png`(3840px)를 절반으로
  줄이는 것부터 해야 한다.
- 이 팩 유닛은 **Death·Hurt 클립이 없다.** `UnitAnimator`가 없는 클립을 건너뛰고
  시체는 마지막 0.4초 동안 흐려진다 — 기존 Tiny Swords 적과 같은 처리다.
- 색이 다섯 벌(Blue/Red/Black/Yellow/Purple) 있다. 같은 유닛을 색만 바꿔 쓸 수 있다.

**Tiny RPG Character Asset Pack v1.03 — Free Soldier & Orc**
100×100 가로 스트립 시트. 무료 배포판.

- `Orc-*.png` (적), `Arrow.png` (32×32 발사체)
- `Soldier-*.png` — **더는 안 쓴다.** 아군이 위 Tiny Swords로 옮겨 갔다.
  `UnitVisuals`의 폴백 경로가 아직 이 이름을 가리키므로 파일은 남겨 둔다.
- 프레임 수: Idle 6, Walk 8, Attack01 6, Hurt 4, Death 4
- **"with shadows" 버전을 쓴다.** 그림자가 있어야 유닛이 배경에 떠 보이지 않는다.

**Enemy_Animations_Set** — 32×32 가로 스트립

- `Skeleton-*.png`(skeleton1), `Bonelord-*.png`(skeleton2), `Vampire-*.png`
- 원본 파일명이 `enemies-skeleton1_idle` 형식이라 반입할 때 `이름-클립` 규칙으로 통일했다.
  단 **공격 시트 이름만 팩마다 다르다**(`Attack01` vs `Attack`) — `Battle/UnitVisuals.cs`에 명시.

**프레임 크기가 팩마다 다르다** (Tiny RPG 100px, Enemy Set 32px).
`UnitVisuals`가 종류별로 프레임 크기와 배율을 들고 있고,
배율은 픽셀 밀도가 아니라 **화면에서 읽히는 크기**로 맞췄다 —
32px 스프라이트를 픽셀 밀도에 맞춰 축소하면 너무 작아서 뭔지 안 보인다.

## 이펙트 — `Assets/Resources/Sprites/VFX/`

**brackeys_vfx_bundle / predrawn — CC0**

2026-08-01 추가로 세 개를 더 가져왔다. 새 룬 셋(고양·부활·봉화)이 각각
보호막·회복·별똥별의 이펙트를 빌려 쓰고 있어서, 아홉 룬 중 셋이 다른 룬처럼
보였다. 같은 팩에서 고른 이유는 화풍이 같아야 섞이지 않아서다 —
Pipoya VFX(시계 문자판이 그려진 마법진)도 검토했지만 시간 마법 테마인 데다
매끈한 글로우라 픽셀 화풍과 어긋났다.

- `wavy_purple_6x5` — 고양. 회복(`wavy_blue`)과 같은 모양에 색만 달라
  "회복이 아니라 강화"가 색으로 읽힌다
- `big_hit_6x5` — 부활. 밝은 방사 폭발이라 회복과 확실히 갈린다
- `fire_ring_6x5` — 봉화. 원형 불바다라는 모양 그대로다
(원작자 크레딧은 `BRACKEYS_LICENSE.txt`에 동봉. 스프라이트시트는 CodeManu.)

파일명 뒤의 `_6x5`가 격자 크기(가로×세로 프레임 수)다. 격자 정보는
`Core/VfxLibrary.cs`에 명시해 뒀다 — 파일명을 바꿨을 때 조용히 깨지는 것보다 낫다.

**그런데 그 이름 두 개가 틀려 있었다** (2026-08-05). `star_explosion`은 6×5가 아니라
**7×6**, `impact_white`는 6×4가 아니라 **6×5**였다. 파일명과 코드가 같이 틀려 있어서
서로를 검증해 주지 못했다.

증상이 격자 문제로 안 보인다는 게 고약하다. 폭발이 제자리에서 터지지 않고
**화면을 가로질러 흘러갔다.** 실제 프레임 폭이 120인데 140씩 잘라 읽으니
프레임을 넘길 때마다 창이 20px씩 밀린 것이다.

**표식은 나누어떨어지지 않는 것이었다** — 654 ÷ 5 = 130.8, 1505 ÷ 4 = 376.25.
나머지 열 개는 전부 정수로 나뉘고 칸 경계선에 내용이 없다. 이 두 가지가
격자를 확인하는 방법이다:

1. 시트 크기가 격자 수로 **정수로 나뉘는가**
2. 칸 경계선 위에 **불투명 픽셀이 없는가** (있으면 내용이 칸을 넘고 있다)

| 파일 | 쓰이는 곳 |
|---|---|
| `wavy_blue_6x5` | 힐 물결 |
| `charge_7x6` | 보호막 |
| `impact_white_6x5` | 화살 명중 / 가르기 피격 |
| `lightstreaks_6x5` | 가르기 섬광 |
| `electric_ring_6x5` | 연쇄 번개 |
| `explosion_6x5` | 별똥별 착탄 |
| `star_explosion_7x6` | 완벽 등급 추가 폭발 · 클리어 · 싹쓸이 |
| `vortex_6x5` | 소용돌이 (반복 재생) |
| `fire_point_6x5` | 잡타격 |

나머지 파일(`big_hit`, `blood_impact`, `dithered_fire`, `fire_ring`, `wavy_purple`)은
아직 안 쓰지만 같은 CC0라 그대로 두었다.

**주의:** 이 시트들은 원본이 최대 3342px인데 Unity 임포터 Max Size 기본값이 2048이라
자동으로 줄어든다. 그래서 `SpriteSheet.LoadGrid`는 프레임 크기를 상수로 받지 않고
**실제 텍스처 크기 ÷ 격자 수**로 계산한다.

## 이펙트 / 배경 — `Assets/Resources/Sprites/FX/`

- `burst_gold.png`, `burst_teal.png` — WCF 프로젝트에서 반입. **384×64 = 64px 프레임 6장짜리 시트**
## 지형 — `Assets/Resources/Sprites/Terrain/`

**Tiny Swords (Enemy Pack)** — Pixel Frog

- `Shaman-Idle/Walk/Attack.png` — `Enemies/Goblin Raiders/Hex Shaman`의
  `Idle`/`Run`/`Attack`. 지팡이를 든 형상이라 "때리는 놈이 아니다"가 실루엣에서 읽힌다.
- `Gnoll-Idle/Walk/Throw/Hurt.png` — `Enemies/Gnoll`의 `Idle`/`Walk`/`Throw`/`Hit`.
- `Spider-Idle/Walk/Attack.png` — `Enemies/Spider`의 `Idle`/`Run`/`Attack`.
- `Troll-Idle/Walk/Attack/Death.png` — `Enemies/Troll`. **보스로 쓴다.**
  원본이 384px 프레임에 시트 폭이 4608px까지 가는데, Unity 임포터의 Max Size
  기본값이 2048이라 그대로 넣으면 자동 축소되면서 프레임이 어긋난다
  (캐릭터 반쪽이 걸어다닌다). **정확히 절반(384→192)으로 미리 줄여서** 넣었다 —
  2배 축소는 픽셀이 깨끗하게 합쳐진다. Idle만 12→10프레임으로 잘랐다
  (12×192 = 2304로 한도를 넘어서).
  어미(거미)와 새끼(새끼 거미)가 **같은 시트를 크기만 달리해서** 쓴다 —
  축소판이라 "저게 아까 그것에서 나왔다"가 설명 없이 읽힌다.
- 둘 다 192px 가로 스트립. **Death 클립이 없다** — 그래서 Unit이 시체를
  마지막 0.4초 동안 흐리게 지운다.
- 기존 32px 팩과 화풍이 다르다. 완전한 애니메이션 세트를 갖춘 유일한 출처라
  택했다 — 결이 맞는 팩(2D Pixel Dungeon)은 idle 프레임뿐이고,
  걸으면서 안 움직이는 쪽이 화풍 차이보다 더 티가 난다.

**Tiny Swords (Free Pack)** — Pixel Frog

- `grass_tile.png` — `Terrain/Tileset/Tilemap_color1.png`(576×384, 64px 격자)의 **(열1, 행1) 칸**을
  잘라낸 것. 그 칸이 테두리 없는 순수 풀밭이라 이음매 없이 반복된다.
  잘라내기 전에 3×3으로 반복시켜 이음매를 눈으로 확인했다.
- `rock_1~4.png` — `Terrain/Decorations/Rocks`
- `grass_tile_ch2.png` — `Terrain/Tileset/Tilemap_color5.png`의 (열1, 행1) 칸.
  1장 타일(`color1`)과 **같은 타일셋의 색 변주**라 화풍이 어긋나지 않으면서
  분위기만 달라진다. 2장이 "밤의 군세"인데 배경이 1장과 같은 초원이면
  장이 넘어간 게 화면에서 안 읽힌다. 잘라낸 뒤 3×3으로 반복시켜 이음매를
  확인했다(색차 합계 567 — 노이즈 수준).

배경 구성은 `Core/Backdrop.cs`. 한때 Pixel Adventure의 `Background/Purple.png`를
반복 타일로 깔았다가 뺐는데, 그 파일이 2×2 체커 패턴이라 체스판처럼 보였다.
**규칙적인 격자무늬는 이 게임과 근본적으로 안 맞는다** — 화면 위에 손으로 도형을 그리는데
배경에 직선 격자가 깔리면 그 선들과 궤적이 섞인다.
풀 텍스처를 고른 것도 무늬가 불규칙해서 반복돼도 선으로 읽히지 않기 때문이다.

바위는 전투 구간(세로 ±2.7유닛) 바깥에만 놓는다. 장식이 유닛과 겹치면
뭐가 적이고 뭐가 배경인지 순간적으로 헷갈린다.

### 메뉴 배경 — 바다 (Tiny Swords Free Pack)

`tools/slice_ui.py`가 통째로 복사한다. 구성은 `Core/MenuBackdrop.cs`.

- `water_bg.png` — `Tileset/Water Background color.png`. 단색 청록 타일.
  **지금은 안 쓴다** — 색이 차분해서 물이 죽어 보였고, 쨍한 남빛 그라디언트
  (코드 생성)로 갈았다. 팩 원색으로 되돌릴 때를 위해 남겨 둔다.
- `cloud_1/2/3.png` — `Decorations/Clouds/Clouds_01/04/07`. 물그림자까지 그려져 있다.
- `water_rock_1~4.png` — `Decorations/Rocks in the Water`. **64×64 프레임 16장짜리
  일렁임 애니메이션 스트립** — 메뉴가 정지화면으로 안 보이는 건 이것 덕이다.

메뉴 배경은 다섯 번 갈아엎었다: 어두운 그라디언트 → 밤하늘 → 크림빛 →
색 그라디언트 → 초원 → **바다.** 각각 왜 죽었는지는 MenuBackdrop.cs 머리 주석에.
핵심 교훈: **전투와 같으면 복사고, 다르되 같은 팩이면 "같은 세계의 다른 장소"다.**

## UI — `Assets/Resources/Sprites/UI/`

**Complete UI Essential Pack (Free)** — Crusenho Agus Hennihuno
**CC BY 4.0** — 상업 이용 가능, **출처 표기 필수**.
<https://crusenho.itch.io> · <https://creativecommons.org/licenses/by/4.0/>

> **출시할 때 게임 내 크레딧 화면에 위 이름과 링크를 반드시 넣어야 한다.**
> 이 프로젝트에서 표기 의무가 있는 유일한 에셋이다 (나머지는 CC-0이거나 크레딧 불필요).

| 프로젝트 파일 | 원본 | 9-slice 테두리 (L R T B) |
|---|---|---|
| `btn_normal/hover/active/on.png` | `Button02a_1/2/4/3` | 4 4 8 10 |
| `panel.png` | `Frame01a` (96×64) | 3 3 5 5 |
| `panel_accent.png` | `Frame02a` | 3 3 5 5 |
| `panel_gold.png` | `Frame03a` | 3 3 3 3 |
| `slot.png` / `slot_on.png` | `FrameSlot01a` / `03a` | 2 2 3 3 / 2 2 2 2 |
| `bar_bg.png` | `Bar05a` (32×10) | 3 3 3 4 |
| `bar_fill.png` | `BarFill01f` (크림색 — 색은 GUI.color로 입힌다) | — |
| `banner.png` | `Banner04a` (64×20) | 4 3 3 3 |

처음엔 각진 `Button01a`와 테두리 없는 `Bar01a`를 썼는데, 둥근 `Button02a`와
테두리 있는 `Bar05a`로 바꾸니 인상이 확 달라졌다. 팩에 100장이 들어 있는데
8장만 쓰고 있었던 것이다.

**버튼 테두리를 4/4/8/10으로 잡은 이유:** 이 버튼은 상태마다 그림이 위아래로 움직인다
(뜬 상태 T8/B6 → 완전히 눌림 T4/B10). GUIStyle은 상태별로 테두리를 따로 못 주므로
**가장 큰 값**을 써야 한다. 작게 잡으면 눌린 상태에서 그림자 띠가 늘어나 뭉개진다.

9-slice 테두리 값은 **스프라이트 픽셀을 실측해서** 정했다(`Core/UiSkin.cs`).
버튼은 위 4줄이 비어 있고 아래에 그림자 띠가 있어 아래 테두리만 더 크다.

### 추가 UI 조각 (같은 폴더)

**Pixel UI kit** — 개인 소장 시트(`all.png`)에서 `tools/slice_ui.py`로 잘라 넣음.
좌표는 그 스크립트에 있다. 원본 시트는 저장소 밖.

| 프로젝트 파일 | 용도 | 9-slice |
|---|---|---|
| `frame_sel.png` (24×24) | 고른 탭·룬에 덧그리는 금테 | 8 8 8 8 |
| `ribbon.png` (80×18) | 소제목 띠 ("궤적 스킨"·"업적") | 18 18 0 0 — **가로만** (사다리꼴이라 세로로 늘리면 빗변이 뭉개진다) |

---

## 아이콘 — `Assets/Resources/Sprites/Icons/`

**Raven Fantasy Icons (Free)** — Caio (Clockwork Raven Studios)
무료판 배포 페이지 기준 상업 이용 가능. 재배포 금지 — 잘라 낸 결과물만 저장소에 넣는다.
<https://clockworkraven.itch.io> · <https://www.patreon.com/clockworkravenstudios>

`tools/slice_ui.py`가 32×32 시트에서 (행, 열)로 잘라 넣는다. 투명 여백은 떼어 낸다 —
칸 안에서 아이콘이 제각기 다른 자리에 그려져 있어, 그대로 쓰면 나란히 놓았을 때
크기가 들쭉날쭉해 보인다. 그리는 쪽은 `UiSkin.DrawIcon`이 비율을 지켜 맞춘다.

| 파일 | 그림 | 쓰는 곳 |
|---|---|---|
| `ic_shard` | 푸른 결정 | 파편 잔액 |
| `ic_power` | 교차한 검 | 주력 |
| `ic_forge` | 모루 | 룬 벼리기 탭 |
| `ic_glyph` | 반짝임 | 문양 소환 탭 |
| `ic_trophy` | 트로피 | 기록 탭 |
| `ic_rune` | 룬돌 | 각인 탭 |
| `ic_ticket` | 두루마리 | 각인권 |
| `ic_gem_common/rare/epic/legend` | 등급 보석 | 문양 목록·장착 칸 |

### 검토했지만 쓰지 않은 것 (`D:\Assets`)

- **Ninja Adventure Asset Pack** (CC0) — 이 다운로드본에 `Actor/Animal`만 들어 있고 FX·타일이 없음.
  전체 팩을 받으면 이펙트·타일이 들어 있으니 재검토할 것
- **Pipoya VFX (TimeMagic / Mysterious Object)** — 품질은 좋지만 격자 배열이 파일명에 없어
  시트마다 눈으로 확인해야 함. brackeys 쪽이 파일명에 격자가 박혀 있어 먼저 채택
- **Pixel Holy Spell Effect** — 격자 정보 없음. 위와 같은 이유로 보류
- **2D Pixel Dungeon** / **Pixel Art Top Down Basic** — 바닥 타일용으로 유력하나,
  타일셋에서 이음매 없는 바닥 칸의 좌표를 눈대중으로 찍어야 해서 실패 위험이 큼.
  배경을 실제로 손볼 때 타일 좌표를 확인하고 쓸 것
- **Pixel UI pack 3** — Crusenho 팩을 먼저 채택. 화풍이 겹쳐 둘 다 쓸 이유가 없다
- **UIBundleFree** — 미리보기 시트 8장뿐이고 슬라이스 정보가 없어 보류
  (`brackeys` 때와 같은 이유: 격자를 눈대중으로 찍어야 하면 조용히 깨진다)
- **Sprout Lands** — 농장 화풍이라 안 맞음

배경은 이미지를 쓰지 않는다. `Bootstrap.SetUpBackdrop`이 그라디언트 + 비네트를 만든다 —
화면 위에 밝은 궤적을 그리는 게 핵심이라 배경이 복잡하면 그리는 선이 안 읽힌다.

---

## 글꼴 — `Assets/Resources/Fonts/`

**Galmuri11** — Lee Minseo (quiple)
**SIL Open Font License 1.1** — 상업 이용·번들 자유. 라이선스 원문 동봉 필수(했음).
<https://github.com/quiple/galmuri>

기본 IMGUI 글꼴이 "게임이 아님"을 가장 크게 드러내던 부분이다.
스프라이트를 아무리 손봐도 글씨가 에디터 글꼴이면 도구처럼 보인다.

**한글 완성형을 다 담으면 5.4MB라 실제로 쓰는 글자만 남겨 65KB로 줄였다** (523자).
방금 오디오를 33MB에서 줄여놓고 글꼴로 5MB를 도로 얹는 건 앞뒤가 안 맞는다.
이게 가능한 이유는 화면에 뜨는 글자가 전부 소스에 문자열 리터럴로 박혀 있기 때문이다
(`Loc` 표 한 곳 + 스테이지 이름 + 등급 이름).

> **글을 고치면 `tools/make_font.py`를 다시 돌려야 한다.**
> `Loc`에 문장을 추가하고 이걸 안 돌리면 그 글자가 폰트에 없어서 화면에 네모로 뜬다.

임포터는 **Hinted Raster**(`fontRenderingMode: 2`)로 지정했다. 픽셀 글꼴을
기본값(Smooth)으로 두면 안티에일리어싱이 두 번 먹어 뭉갠다.

**봉화 룬의 표시 글자를 ᛝ(U+16DD)에서 ¶로 바꿨다.** 한글 글꼴에 룬 문자가 없어서
화면에 네모로 떴다. ¶는 고리+자루라 깃발 도형과 그림이 오히려 더 맞는다.

---

## 파티클 — `Assets/Resources/Particles/`

시트 이펙트(BurstFx) **위에 얹는** 광량. 대체가 아니라 겹층이다 — 픽셀 바탕은
시트가 만들고, 파티클이 열기·전기·혼 같은 "물성"을 더한다. 스포너는
`Core/ParticleFx.cs`. **쓰는 프리팹만** 팩에서 Resources로 옮겼다(.meta째로,
GUID 유지) — Resources 아래 있는 것은 전부 빌드에 실리기 때문.

**Cartoon FX Remaster (CFXR)** — Jean Moreno (JMO Assets)
에셋스토어 무료판. 재배포 금지, 게임 번들 가능.

| 파일 | 원본 | 쓰는 곳 |
|---|---|---|
| `fx_meteor` | CFXR3 Fire Explosion B | 별똥별 착탄 |
| `fx_chain` | CFXR3 Hit Electric C (Air) | 연쇄 번개 타격 |
| `fx_heal` | CFXR3 Hit Light B (Air) | 회복된 유닛 |
| `fx_shield` | CFXR Impact Glowing HDR (Blue) | 보호막 받은 유닛 |
| `fx_slash` | CFXR4 Sword Hit PLAIN (Cross) | 가르기 중심 |
| `fx_arrow` | CFXR3 Hit Misc A | 화살 명중 (3발에 1번 — 잦아서) |
| `fx_empower` | CFXR3 Magic Aura A (Runic) | 고양 — **룬 문자가 도는 오라** |
| `fx_revive` | CFXR2 Souls Escape | 소생 — 혼이 올라간다 |
| `fx_pyre` | CFXR2 Firewall A | 봉화 점화 |
| `fx_stars` | CFXR4 Falling Stars | 6마리 이상 정리 |
| `fx_poof` | CFXR Magic Poof | 뽑기 개봉·상급 카드 |
| `fx_firework_big` | CFXR4 Firework 1 Cyan-Purple | 전설 뽑기·3별 |

**Stylized Explosion Package** — Kyeoms. 무료판, URP.
`fx_bossblast` ← FX_Explosion_D_1_air_dust — **보스 강타 전용.** 내 룬(별똥별)과
같은 그림이면 위협과 내 기술이 화면에서 안 갈린다.

**Cartoon VFX 9X FireworksEffect2D** — 무료판.
`fx_firework_a/b` ← Firework3/7 — 결과 화면 3별 폭죽.

소용돌이·잡타격·유닛 사망에는 **일부러 안 얹었다** — 루프 관리가 따로 필요하거나
너무 잦아서, 매 프레임 생성·파괴가 모바일 GC를 깨운다(BurstFx가 풀을 쓰는 이유).

### 들여왔지만 아직 안 쓰는 것 (`Assets/`)

- **GUIPackCartoon** — uGUI 프리팹 팩. 지금 UI는 IMGUI + 픽셀 화풍이라 화풍도
  파이프라인도 안 맞는다. uGUI로 옮기는 날 후보.
  **딱 하나만 쓴다:** `Icons Colored/Other/Hand.png` → `Resources/Sprites/UI/hand.png`.
  튜토리얼에서 "여기에 손을 대라"를 알리는 손 모양이다(`UI/TouchHint.cs`).
  원본은 매끈한 벡터 카툰이라 그대로 쓰면 픽셀 화풍에서 혼자 겉돈다 —
  **32칸으로 낮춰 다시 키워서** 픽셀을 굵게 만들어 넣었다(트롤을 절반으로
  줄여 넣은 것과 같은 처리). 손은 화면 안 세계가 아니라 조작 안내라
  아이콘 하나쯤은 다른 출처여도 어긋나 보이지 않는다.
- **Hero Knight - Pixel Art** — 픽셀 기사. 유닛 교체는 애니메이션 배선이 커서
  화면을 보면서 해야 한다 — 에디터 열고 할 일.
- CFXR·Kyeoms·CartoonVFX9X의 **나머지 프리팹** — 필요할 때 위 표처럼
  Resources로 옮겨 쓴다. 팩 폴더째 Resources에 넣지 말 것(빌드가 붓는다).

---

## 코드로 만든 것 (출처 없음)

체력바, 보호막 링, 사각형 유닛(스프라이트 폴백), 쉴드 영역 테두리, 위기 경고 링은
`Core/PrimitiveSprites.cs`가 런타임에 텍스처로 찍어낸다. 이미지 파일이 없다.
