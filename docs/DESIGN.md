# RuneCast — 설계 문서

손으로 도형을 그려 룬을 시전하는 실시간 제스처 + 오토배틀러. Unity 6 (6000.4.2f1), C#.

---

## 이 문서에 대하여

이 문서의 4장·5장은 **코드에서 직접 읽어낸 것만** 담는다. 모든 수치에는 그 값이 정의된
`파일:라인`을 붙였다. 추측으로 메운 곳은 없다 — 확인되지 않은 것은 쓰지 않고
부록의 질문으로 넘겼다.

"왜 이 값인가"에 해당하는 판단 근거는 두 갈래로 나눴다.

- **코드 주석에 명시된 근거**는 그대로 인용했다. 이 프로젝트는 결정의 이유를 주석에
  남기는 편이라, 상당 부분이 여기에 해당한다.
- **어디에도 적혀 있지 않은 근거**는 지어내지 않고 [부록 A](#부록-a--아직-답이-없는-질문)에
  질문으로 모아 두었다.

---

## 1. 개요

*(미작성)*

## 2. 게임 개요와 핵심 루프

*(미작성)*

## 3. 기술 스택

*(미작성)*

---

## 4. 인식 설계

### 4.1 파이프라인

한 획이 룬이 되기까지 다섯 단계를 지난다.

```
포인터 입력 ──▶ 궤적 수집 ──▶ 정규화 ──▶ 점군 매칭 ──▶ 점수·등급 ──▶ 효과 발동
              TraceCapture   StrokeMath  PointCloud    RuneGrading   RuneCaster
                                         Recognizer
```

인식 코어(`StrokeMath`, `PointCloudRecognizer`, `RuneGrade`)는 **UnityEngine 참조가 없는
순수 C#**이다. `StrokeMath.cs:8` 주석이 그 의도를 밝힌다 — "에디터 없이 테스트 가능".
덕분에 Unity를 켜지 않고 인식 코드만 컴파일해 합성 입력 수천 회를 돌릴 수 있다
(4.8절 참조).

### 4.2 입력 수집 — `TraceCapture`

| 값 | 수치 | 위치 | 역할 |
|---|---|---|---|
| `minPointDistance` | 8f | [TraceCapture.cs:21](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:21) | 이만큼 움직여야 점 추가. 손떨림 제거 |
| `minStrokeLength` | 40f | [TraceCapture.cs:24](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:24) | 이보다 짧으면 클릭으로 보고 폐기 |
| `castTimeScale` | 0.2f | [TraceCapture.cs:29](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:29) | 그리는 동안의 `timeScale` |
| 획 성립 조건 | 점 4개 이상 **그리고** 길이 ≥ `minStrokeLength` | [TraceCapture.cs:354](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:354) | 둘 다 만족해야 `StrokeCompleted` |

**두 임계값은 런타임에 보정된다.** 시작 시 `UiScale.Factor`를 곱하고, 모바일이면
추가로 1.5배 한다 ([TraceCapture.cs:93-98](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:93)).
주석이 이유를 밝힌다 — 픽셀 고정값이면 고해상도 폰에서 필터가 사실상 사라지는데,
"손가락은 마우스보다 굵고 흔들려서 오히려 더 걸러야 하는데 반대로 동작한다."

이 클래스가 다루는 문제 대부분은 인식 정확도가 아니라 **입력 수명 관리**다.

- **두 개의 독립 스위치.** `captureEnabled`(획을 받는가)와 `castingEnabled`(발동하는가)를
  따로 둔다. 주석([TraceCapture.cs:36-39](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:36))에
  따르면 시전만 막았을 때 "룬이 안 나갈 뿐 선은 그대로 그려지고 잉크도 닳는" 문제가 있었다.
- **화면 버튼 영역 차단.** UI가 매 프레임 `BlockScreenRect()`로 자기 자리를 등록하고,
  `Update` 끝에서 목록을 비운다 ([TraceCapture.cs:106-147](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:106)).
  이미 그리는 중이면 차단하지 않는다 — 획이 버튼 위를 지나갈 수 있어야 하므로.
- **down/held/up을 else-if로 묶지 않는다** ([TraceCapture.cs:139-143](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:139)).
  한 프레임 안에서 누르고 떼는 짧은 클릭일 때 `up`이 영영 처리되지 않아 슬로우가 걸린 채
  굳는다.
- **획을 시작한 손가락 하나를 끝까지 추적**한다 ([TraceCapture.cs:198](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:198)).
  `primaryTouch`는 "제일 먼저 닿은 손가락"이라, 가로로 들고 받치던 엄지가 먼저 닿아 있으면
  검지로 그리는 획이 아예 안 읽힌다.
- **터치를 마우스보다 먼저 확인**한다 ([TraceCapture.cs:204-209](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:204)).
  Input System은 실제 마우스가 없어도 `Mouse.current`를 만들어 두는 경우가 있어, 순서가
  반대면 폰에서 터치가 통째로 죽는다.
- **취소 경로가 넷.** `captureEnabled` 해제 / `OnDisable` / `OnApplicationFocus(false)` /
  `OnApplicationPause(true)`. 안드로이드에서 `OnApplicationFocus`가 안 불릴 수 있어
  `OnApplicationPause`까지 건다 ([TraceCapture.cs:162-169](RuneCast/Assets/Scripts/Gesture/TraceCapture.cs:162)).

### 4.3 정규화 — `StrokeMath`

세 단계를 거쳐 표준형을 만든다 ([StrokeMath.cs:143-146](RuneCast/Assets/Scripts/Gesture/StrokeMath.cs:143)).

```
Resample(n=32) ──▶ ScaleToUnit ──▶ TranslateToOrigin
등간격 재추출        긴 변 기준 균일     무게중심을 원점으로
(그리기 속도 무시)   (크기 무시)         (위치 무시)
```

| 값 | 수치 | 위치 |
|---|---|---|
| `ResampleCount` | 32 | [StrokeMath.cs:16](RuneCast/Assets/Scripts/Gesture/StrokeMath.cs:16) |

주석이 근거를 밝힌다 — "매칭 비용이 O(n² · √n)이라 올릴수록 급격히 무거워진다.
32면 별 모양(꼭짓점 10개)까지 형태가 살아남는다."

**균일 스케일을 쓰는 이유**도 명시돼 있다 ([StrokeMath.cs:106](RuneCast/Assets/Scripts/Gesture/StrokeMath.cs:106)):
축별 개별 스케일이면 "납작한 타원과 원이 같아져 버린다."

**`ApexDirection` — 화살 방향 계산** ([StrokeMath.cs:169](RuneCast/Assets/Scripts/Gesture/StrokeMath.cs:169)).
꺾쇠의 꼭짓점이 가리키는 방향을 구한다. 시작점–끝점을 잇는 선에서 수직 거리가 가장 먼 점을
꼭짓점으로 보고, 그 선의 중점에서 꼭짓점으로 향하는 벡터를 쓴다. 주석에 실패한 대안이
적혀 있다 — "중심에서 가장 먼 점"을 쓰면 `>` 모양에서 꼬리 끝 두 개가 꼭짓점보다 멀어
**방향이 뒤집힌다**.

### 4.4 매칭 — $P Point-Cloud Recognizer

Vatavu·Anthony·Wobbrock (2012)의 $P 인식기를 직접 구현했다
([PointCloudRecognizer.cs:35](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:35)).
궤적을 "순서가 느슨한 점 구름"으로 보고 템플릿과 그리디 매칭한다.

원 논문에서 **두 곳을 바꿨다.**

**① 균등 가중치** ([PointCloudRecognizer.cs:163-166](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:163))

원 논문은 앞쪽에서 맺어진 짝에 큰 가중치를 준다. 이 구현은 균등 가중치를 쓴다.
주석에 근거가 있다 — 가중치를 주면 궤적 뒷부분이 사실상 무시되는데, 그러면 네모의
모서리처럼 "뒤에 나오는 결정적 특징"이 반영되지 않는다.

> 실측 (합성 손그림 1400회): 전체 정확도 91.9% → 97.1%, 특히 **네모 45% → 80%**

**② 시작점 4개** ([PointCloudRecognizer.cs:140-143](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:140))

원 논문은 √n개(=6)를 쓴다. `int step = Math.Max(1, n / 4)`로 4개만 시도한다.
정확도 98.3% → 98.2%로 사실상 같은데 속도는 1.8배 빨랐다.

양방향으로 잰다 — `CloudDistance(a,b)`와 `CloudDistance(b,a)` 중 작은 값
([PointCloudRecognizer.cs:148-150](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:148)).

**할당 최적화:** 매칭마다 `bool[n]`을 새로 잡으면 한 획에 수백 번 할당된다. 크기가 늘 같으므로
정적 배열을 재사용한다 ([PointCloudRecognizer.cs:156-157](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:156)).

### 4.5 임계값과 점수 환산

| 값 | 수치 | 위치 |
|---|---|---|
| `RejectDistance` | **0.13** | [PointCloudRecognizer.cs:54](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:54) |
| `PerfectDistance` | **0.035** | [PointCloudRecognizer.cs:57](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:57) |
| 최소 점 개수 | 4 | [PointCloudRecognizer.cs:84](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:84) |

두 값은 **합성 손그림 2400회 실측으로 보정**했다고 주석에 명시돼 있다 (2026-07-31,
[PointCloudRecognizer.cs:43-51](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:43)).
근거가 된 거리 분포:

| 입력 품질 | 거리 중앙값 | 90% 지점 |
|---|---|---|
| 깔끔한 입력 | 0.039 | 0.064 |
| 보통 | 0.057 | 0.081 |
| 대충 | 0.089 | 0.117 |
| 무작위 낙서 | 0.147 | (최소 0.075) |

**이전 값 0.42가 무엇을 망가뜨렸는지**도 같은 주석에 적혀 있다 — 낙서까지 전부 통과시키면서
점수를 항상 0.9대로 고정시켜, "대충 그리면 약하게 나간다"는 설계 자체를 무력화하고 있었다.

**점수 환산** ([PointCloudRecognizer.cs:113-117](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:113)):

```
score = clamp01( (RejectDistance − distance) / (RejectDistance − PerfectDistance) )
      = clamp01( (0.13 − d) / 0.095 )
```

0에서 임계값까지가 아니라 **"최선~거부" 구간에 점수를 편다.** 주석의 근거 — 그래야
잘 그린 것과 대충 그린 것의 차이가 효과 강도로 드러난다.

### 4.6 등급

`score`를 4단계로 나눈다 ([RuneGrade.cs:33-42](RuneCast/Assets/Scripts/Gesture/RuneGrade.cs:33)).

| 등급 | 임계 `score` | `Flourish` (연출 강도) |
|---|---|---|
| PERFECT | ≥ 0.85 | 1.0 |
| EXCELLENT | ≥ 0.65 | 0.6 |
| GREAT | ≥ 0.40 | 0.3 |
| GOOD | 그 미만 | 0.0 |

*(임계값 [RuneGrade.cs:33-35](RuneCast/Assets/Scripts/Gesture/RuneGrade.cs:33), `Flourish` [RuneGrade.cs:57-66](RuneCast/Assets/Scripts/Gesture/RuneGrade.cs:57))*

**등급을 위력 배율과 별도로 두는 이유**가 주석에 있다
([RuneGrade.cs:21-26](RuneCast/Assets/Scripts/Gesture/RuneGrade.cs:21)) — "배율 1.31배와
1.34배는 화면에서 구분되지 않지만, PERFECT와 GREAT는 구분된다." 등급은 연출의 **단위**다.

**최하위 등급이 "GOOD"인 것도 의도다** ([RuneGrade.cs:6-8](RuneCast/Assets/Scripts/Gesture/RuneGrade.cs:6)).
이전엔 "조잡"이었는데, 인식 실패는 따로 표시되므로 등급이 뜬 시점에서는 전부 성공이고
잘한 정도만 다르다.

경계값은 실측 분포에 맞췄다고 적혀 있다 — 깔끔하게 그리면 평균 0.79, 보통이면 0.51
([RuneGrade.cs:25-26](RuneCast/Assets/Scripts/Gesture/RuneGrade.cs:25)).

### 4.7 템플릿 설계

**총 37개 템플릿, 9종 룬.** 모두 `.asset` 파일이 아니라 **코드로 절차 생성**한다
([RuneTemplateLibrary.cs:9-12](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:9)) —
임포트·참조 연결이 필요 없고, 사람이 그린 샘플보다 편향이 없으며, 숫자 몇 개만 고치면
도형이 바뀐다.

| 룬 | 도형 | 템플릿 수 | 변형 |
|---|---|---|---|
| Heal | 원 ○ | 2 | 시계/반시계 |
| Shield | 삼각형 △ | 6 | 위/아래 방향 × 회전 방향, ±0.3rad 기울기 |
| Arrow | 꺾쇠 ＞ | 8 | 45°씩 8방향 |
| Meteor | 별 ☆ | 2 | 정/역방향 |
| Chain | 지그재그 Ｚ | 10 | {0, ±20, ±90}° × 정/역 |
| Vortex | 나선 ◎ | 2 | 시계/반시계 |
| Empower | 무한대 ∞ | 3 | 0°, ±20° 기울기 |
| Revive | 하트 ♡ | 2 | 정/역방향 |
| Pyre | 깃발 ᛝ | 2 | 시계/반시계 |
| ~~Slash~~ | ~~직선 ／~~ | **0 (비활성)** | 블록 전체 주석 처리 |

*(전체 정의 [RuneTemplateLibrary.cs:60-209](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:60);
템플릿 원본 해상도 `Samples = 64`, 이후 `Normalize`가 32로 줄임 —
[RuneTemplateLibrary.cs:258](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:258))*

#### 회전 정규화를 쓰지 않는다

지표각(indicative angle) 정규화 대신 **회전 변형 템플릿을 여러 개 등록**한다.
근거가 [RuneTemplateLibrary.cs:91-93](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:91)에
있다 — 지표각 정규화는 꺾쇠처럼 좌우 비대칭이 약한 도형에서 불안정해서, 변형을 늘리는 쪽이
예측 가능하다. 매칭 비용은 무시할 수준.

여기서 **회전 변형과 기울기 변형을 구분**하는 게 핵심이다
([RuneTemplateLibrary.cs:155-158](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:155)):

> 회전 변형은 **자세**를 늘릴 뿐 **손떨림 여유**를 만들지 못한다.

무한대(∞)에서 방향 4개만 두고 기울기 변형이 없으면, 조금 기울여 그린 입력의 15%가
통째로 인식 실패했다.

#### 도형 선택의 실측 근거

**쉴드: 네모 → 삼각형** ([RuneTemplateLibrary.cs:70-78](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:70))

네모는 점 대부분이 변 위에 있어 원(힐)과 가깝고, 다른 곳은 모서리 4곳뿐이다. 조금만
둥글게 그리면 원 템플릿이 이겨서 **쉴드를 그렸는데 힐이 나갔다.**

| 도형 | 인식률 (흔들림 강도별) |
|---|---|
| 네모 (둥근 모서리 변형 포함) | 84% / 53% / 27.5% |
| **삼각형** | **100% / 99.5% / 77%** |

**탈락한 후보들** ([RuneTemplateLibrary.cs:192-206](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:192)) —
다시 시도하지 않기 위해 주석으로 남겨 뒀다.

| 도형 | 자기 인식률 | 전체 | 탈락 사유 |
|---|---|---|---|
| 아치 ⌒ | 90.8% | 94.3% | 꺾쇠와 6~8% 상호 오염. 둥글게 그린 꺾쇠가 곧 아치 |
| 나비 ⋈ | 73.0% | 92.5% | 23%가 인식 실패. 교차선이 리샘플되면 지그재그와 같아짐 |
| 물결 ∿ | 87.2% | 95.7% | 나선으로 5% 유출 |

주석이 공통점을 짚는다 — "셋 다 기존 도형을 부드럽게 하거나 뒤집은 것이다.
**새 룬은 기존 것의 변형이 아니라 없던 특징을 가져와야 한다.**"

하트를 아래가 뾰족한 형태로 바꿔본 실험도 기록돼 있다. 뾰족한 꼭짓점이 삼각형과 겹쳐
둘 사이 거리가 **0.129 → 0.077**로 무너지고 인식률이 96.7% → 87.0%가 됐다.

**깃발의 회전 변형 제거** ([RuneTemplateLibrary.cs:182-188](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:182)) —
처음엔 90°씩 4방향을 넣었는데, 무작위로 그은 획 4000개 중 **8.4%가 봉화로 갔다.**
"고리 + 꼬리는 손이 아무렇게나 지나간 궤적과 제일 닮은 모양"이라 방향을 늘릴수록
그물만 넓어졌다. 세운 것만 남기니 오발동이 **13.9% → 8.2%**로 떨어지고 인식률은 100% 유지.

**직선(가르기) 비활성화** ([RuneTemplateLibrary.cs:107-122](RuneCast/Assets/Scripts/Gesture/RuneTemplateLibrary.cs:107)) —
템플릿 블록만 주석 처리했고 `RuneType.Slash`와 `SlashWave.cs`는 그대로 남아 있다.
주석의 사유: "제일 싸고(12), 제일 빨리 그려지고, 그은 선 위의 적을 전부 베어서 다른 룬을
쓸 이유가 없어졌다. 도형이 단순할수록 등급도 잘 나오니 이중으로 유리했다."

### 4.8 템플릿 분리도 검사 — `TemplateSeparation`

룬을 추가할 때의 진짜 위험은 새 룬이 아니라 **기존 룬까지 같이 망가지는 것**이다
([TemplateSeparation.cs:10-11](RuneCast/Assets/Scripts/Gesture/TemplateSeparation.cs:10)).
타입이 다른 모든 템플릿 쌍의 점군 거리를 재서, 가까운 순으로 보여주는 진단 도구다.

읽는 법이 주석에 있다 — 두 룬 타입 사이의 최소 거리가 `RejectDistance`(0.13)보다 충분히
커야 한다. 그보다 작으면 한쪽을 어설프게 그렸을 때 다른 쪽으로 판정될 수 있다.

**성능 함정이 하나 기록돼 있다** ([TemplateSeparation.cs:66-75](RuneCast/Assets/Scripts/Gesture/TemplateSeparation.cs:66)).
`Analyze`는 템플릿 37개면 쌍이 666개고, 쌍마다 시작점 4개 × 양방향 × 32×32 비교라
한 번에 수백만 연산이다. 이걸 진단 패널에서 매 프레임 호출하고 있었고, OnGUI는 한 프레임에
두 번(Layout·Repaint) 도니까 실제로는 그 두 배 — F1을 누르면 게임이 멎었다.
`AnalyzeCached`가 템플릿 개수로 캐시 유효성을 판별한다.

### 4.9 성능 최적화 이력

획당 인식 비용 실측 (PLAN.md:45-55의 표, 코드 주석
[PointCloudRecognizer.cs:64-69](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:64)와 일치):

| 구성 | ms/획 | 정확도 |
|---|---|---|
| 이중 패스 + 시작점 6개 (초기) | 8.80 | 98.3% |
| 단일 패스 + 시작점 6개 | 4.60 | 98.3% |
| **단일 패스 + 시작점 4개 (현재)** | **2.20** | **98.2%** |
| 단일 패스 + 시작점 2개 | 1.22 | 97.1% |

**4배 단축의 절반은 중복 제거였다.** 예전에는 `Recognize`와 `DistancesTo`를 따로 불러
점군 매칭을 두 번 돌렸다 — 템플릿 30개 × 양방향 × 시작점 4개 = 한 획에 240번 매칭인데,
그게 두 배였다. 진단용 거리표를 같은 패스에서 채우게 바꿔 해결했다
(`distancesOut` 파라미터, [PointCloudRecognizer.cs:71-72](RuneCast/Assets/Scripts/Gesture/PointCloudRecognizer.cs:71)).

**효과가 없어 채택하지 않은 것** (PLAN.md:41-43, 재시도 방지용 기록):
- 리샘플 32 → 64: 정확도 변화 거의 없음, 오히려 일부 악화
- 매칭 시작점 전수 탐색: 정확도 동일, 5배 느림

### 4.10 인식 정확도 실측

손그림 변형 강도별 (PLAN.md:26-30):

| 변형 강도 | 전체 정확도 | 평균 점수 |
|---|---|---|
| 약함 0.04 | 99.8% | 0.79 |
| 보통 0.08 | 97.7% | 0.51 |
| 심함 0.14 | 58.3% (대부분 *거부*) | 0.26 |

**실패의 종류가 중요하다.** 심하게 흔들렸을 때 "엉뚱한 룬"이 아니라 **거부**로 떨어지는 게
설계 목표다 — 거부는 마나를 안 쓰고 다시 그리면 되지만, 잘못된 룬이 나가는 건 되돌릴 수 없다.

### 4.11 실패 사유 분기

인식이 성공해도 발동까지 네 개의 관문이 더 있다. 순서가 곧 설계다
([RuneCaster.cs:99-169](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:99)).

| 순서 | 사유 | 마나 소모 | 위치 |
|---|---|---|---|
| 1 | `Ink` — 잉크가 떨어져 잘린 획 | ✗ | [RuneCaster.cs:105](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:105) |
| 2 | `Shape` — 인식 실패 (거리 > 0.13) | ✗ | [RuneCaster.cs:118](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:118) |
| 3 | `NoCorpse` — 소생인데 시체 없음 | ✗ | [RuneCaster.cs:135-146](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:135) |
| 4 | `Sealed` — 6장에서 잠긴 룬 | ✗ | [RuneCaster.cs:154](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:154) |
| 5 | `Mana` — 마나 부족 | ✗ | [RuneCaster.cs:161](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:161) |

**전부 마나 소모보다 앞에 있다.** 주석이 각각의 이유를 밝힌다.

- 좌표 환산을 마나 소모보다 먼저 하는 이유 ([RuneCaster.cs:125-127](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:125)):
  소생은 위치를 알아야 대상 유무를 판정할 수 있는데, 그 판정이 마나를 쓴 뒤에 오면
  되살릴 사람이 없을 때 마나만 날아간다.
- 봉인 검사를 마나보다 앞에 두는 이유 ([RuneCaster.cs:151-153](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:151)):
  "잠긴 걸 잊고 그리는 일이 이 규칙의 핵심 경험인데, 거기에 자원까지 뺏으면 규칙이 아니라
  벌이 된다."

**사유마다 다른 소리를 낸다.** `FailShape`(0.7)과 `FailMana`(0.8), 소생 실패는 같은 파일을
피치 0.72로, 봉인은 0.62로 재생한다. 주석의 근거 — 같은 소리면 "못 알아봤나 / 마나가 없나"를
화면 글씨를 읽어야 구분할 수 있는데, 그럴 여유가 있는 상황이 아니다.

### 4.12 커스텀 룬 — 인식기 구조가 그대로 기능이 된 곳

`RuneRegistrar`가 사용자 궤적을 템플릿으로 등록한다. 클래스 주석이 요점을 짚는다
([RuneRegistrar.cs:11-12](RuneCast/Assets/Scripts/Gesture/RuneRegistrar.cs:11)) —
**"인식 엔진이 템플릿 단일 방식이라 여기서 새로 만들 로직이 없다.** 궤적을 받아 저장소에
넘기는 게 전부 — 기본 룬과 완전히 같은 경로를 탄다."

| 값 | 수치 | 위치 |
|---|---|---|
| `SamplesNeeded` | 3 | [RuneRegistrar.cs:18](RuneCast/Assets/Scripts/Gesture/RuneRegistrar.cs:18) |
| `CollideDistance` | `= RejectDistance` (0.13) | [RuneRegistrar.cs:163](RuneCast/Assets/Scripts/Gesture/RuneRegistrar.cs:163) |

**세 샘플은 평균 내지 않고 셋 다 템플릿으로 등록된다**
([RuneRegistrar.cs:196-197](RuneCast/Assets/Scripts/Gesture/RuneRegistrar.cs:196)) —
`Target_HHmmss_0/1/2` 세 개가 `CustomRuneStore`에 들어간다. 4.7절에서 기본 룬이 변형을
여러 개 등록하는 것과 같은 구조다.

**충돌 검사가 이 기능의 핵심 방어선이다** ([RuneRegistrar.cs:149-161](RuneCast/Assets/Scripts/Gesture/RuneRegistrar.cs:149)).
각인은 **대체가 아니라 추가**라서, 원(회복)과 비슷한 도형을 화살에 각인하면 그 뒤로 원을
그릴 때마다 둘 중 무엇이 나갈지 알 수 없어진다. 주석이 지적하는 진짜 문제는 인식률이 아니라
**원인과 증상의 거리**다:

> 플레이어는 자기가 무엇을 부쉈는지 모른다. 각인은 대장장이에서 하고 그 결과는 다음
> 전투에서 나타나므로 (…) "회복이 갑자기 안 나간다"로 겪게 되고, 그러면 게임이 고장 난
> 것으로 읽힌다.

그래서 `CollidesWithOtherRune`이 **다른 룬**의 템플릿과 거리 0.13 미만이면 등록을 거절한다
([RuneRegistrar.cs:210-231](RuneCast/Assets/Scripts/Gesture/RuneRegistrar.cs:210)).
같은 룬의 템플릿과 닮은 것은 정상이므로 통과시킨다. 기준을 `RejectDistance`와 **같은 값**으로
둔 근거도 적혀 있다 — "그보다 가까우면 인식기 입장에서 사실상 같은 도형이다."

**각인권 소모 순서:** 충돌 검사 → 티켓 차감 → 등록
([RuneRegistrar.cs:165-201](RuneCast/Assets/Scripts/Gesture/RuneRegistrar.cs:165)).
모드에 들어갈 때가 아니라 세 번을 다 그린 순간에 쓴다. 주석의 근거 — "들어갈 때 쓰면
구경만 하고 나와도 날아가고, 그리다 마음에 안 들어 그만두는 것까지 벌하게 된다."
거절당한 경우에도 티켓은 차감되지 않는다.

---

## 5. 밸런스 수치

### 5.1 두 개의 자원 축

이 게임의 자원은 마나와 잉크 둘이고, **서로 다른 것을 제한한다**
([InkBudget.cs:8-9](RuneCast/Assets/Scripts/Meta/InkBudget.cs:8)).

| 자원 | 제한하는 것 | 단위 |
|---|---|---|
| **마나** | 얼마나 **자주** 쓰는가 | 절대 수치 (0~100) |
| **잉크** | 얼마나 **크게** 쓰는가 | 화면 높이 배수 |

주석이 잉크가 이 게임에 잘 맞는 이유 세 가지를 든다
([InkBudget.cs:11-16](RuneCast/Assets/Scripts/Meta/InkBudget.cs:11)):

1. 효과 범위가 이미 "그린 크기"에서 나온다 — 길이를 조이면 범위가 그대로 줄어든다
2. 인식기가 크기를 정규화하므로 **작게 그려도 인식률은 그대로다.** 잉크 제한은
   "못 알아듣게" 만드는 게 아니라 "작게만 쓰게" 만든다
3. 도형마다 드는 길이가 달라서, 잉크가 적은 초반에는 복잡한 룬을 쓸 만한 크기로 그릴 수 없다
   — **룬 해금이 저절로 생긴다**

### 5.2 마나

| 값 | 수치 | 위치 |
|---|---|---|
| `max` | 100 | [ManaPool.cs:18](RuneCast/Assets/Scripts/Runes/ManaPool.cs:18) |
| `regenPerSecond` | 9 | [ManaPool.cs:19](RuneCast/Assets/Scripts/Runes/ManaPool.cs:19) |
| `unlimited` (기본값) | **true** | [ManaPool.cs:16](RuneCast/Assets/Scripts/Runes/ManaPool.cs:16) |

**룬별 비용** ([ManaPool.cs:83-99](RuneCast/Assets/Scripts/Runes/ManaPool.cs:83)) — 주석의 서열 근거를 함께 옮긴다:

| 룬 | 비용 | 초당 9 기준 충전 시간 | 코드 주석의 근거 |
|---|---|---|---|
| ~~Slash~~ | 12 | 1.3초 | "제일 빨리 그려지므로 제일 싸다" (현재 비활성) |
| Arrow | 15 | 1.7초 | — |
| Vortex | 22 | 2.4초 | "피해가 없는 대신 다른 룬을 강하게 만든다" |
| Heal | 25 | 2.8초 | — |
| Chain | 28 | 3.1초 | — |
| Shield | 30 | 3.3초 | — |
| Empower | 32 | 3.6초 | "결과를 바꾸지만 아군이 살아 있어야 값을 한다" |
| Pyre | 38 | 4.2초 | "총 피해는 제일 크되 다 들어간다는 보장이 없다" |
| Meteor | 45 | 5.0초 | — |
| Revive | 55 | 6.1초 | "판을 되돌리는 유일한 수단이라 제일 비싸다" |

*(충전 시간은 비용 ÷ 9로 계산한 파생값이며 코드에는 없다.)*

**마나는 `Time.deltaTime`으로 찬다** — `unscaledDeltaTime`이 아니다
([ManaPool.cs:58-59](RuneCast/Assets/Scripts/Runes/ManaPool.cs:58)). 주석에 이전 방식의
구멍 두 개가 기록돼 있다:

1. 일시정지 중에도 찼다 — 멈추고 10초 기다렸다 풀면 마나가 가득
2. **드래그를 누른 채 가만히 있으면** 시간은 0.2배로 흐르는데 마나는 실시간으로 찼다.
   잉크는 움직여야 닳으므로 **아무 대가 없이 게임 시간 대비 5배로 마나를 벌 수 있었다**

주석은 원래 걱정이 기우였음도 짚는다 — 슬로우는 **그리는 동안만** 걸린다. 무엇을 그릴지
정하는 시간은 원래 속도로 흐르므로 이중 처벌이 아니다.

### 5.3 잉크

**티어 표** ([InkBudget.cs:23-32](RuneCast/Assets/Scripts/Meta/InkBudget.cs:23)) — 단위는 화면 높이 배수:

| 티어 | 필요 총 별 | 한도 (화면 높이 배수) |
|---|---|---|
| 0 | 0 | 0.55 |
| 1 | 4 | 0.72 |
| 2 | 9 | 0.90 |
| 3 | 15 | 1.10 |
| 4 | 22 | 1.32 |
| 5 | 30 | 1.60 |

실제 픽셀 한도 = `CurrentScreenHeights × Screen.height` ([InkBudget.cs:56-59](RuneCast/Assets/Scripts/Meta/InkBudget.cs:56)).

**도형별 잉크 비율** — "바운딩박스 최대 변 대비 궤적 길이"
([InkBudget.cs:89-99](RuneCast/Assets/Scripts/Meta/InkBudget.cs:89)):

| 룬 | 도형 | 비율 | 주석의 산출 근거 |
|---|---|---|---|
| Arrow | 꺾쇠 | **1.8** | 두 선분 |
| Pyre | 깃발 | **2.11** | 자루 + 고리 — 꺾쇠 다음으로 싸다 |
| Empower | 무한대 | **3.03** | 고리 두 개 |
| Shield | 삼각형 | **3.0** | 정삼각형 둘레 / 외접 폭 |
| Revive | 하트 | **3.11** | 원과 거의 같다 |
| Heal | 원 | **3.14** | πd / d |
| Chain | 지그재그 | **3.4** | Z자 세 선분 |
| Vortex | 나선 | **4.4** | 2.5바퀴, 평균 반지름 기준 호 길이 |
| Meteor | 별 | **5.0** | 펜타그램 다섯 현 |

앞의 여섯 개는 기하로 손 계산한 값이고, 깃발·무한대·하트는 **실제 템플릿 점열에서 잰 값**이다
(주석에 따르면 손 계산과 오차 2% 안, [InkBudget.cs:96](RuneCast/Assets/Scripts/Meta/InkBudget.cs:96)).

`MaxShapeSize(ratio) = CurrentScreenHeights / ratio` ([InkBudget.cs:74-77](RuneCast/Assets/Scripts/Meta/InkBudget.cs:74)).
티어 0(0.55)에서 별을 그리면 화면 높이의 11%가 최대 — 이것이 "초반에 별·나선을 쓸 만한
크기로 못 그린다"의 실체다.

> ⚠️ **같은 파일 안에서 값이 어긋난다.** [InkBudget.cs:83-84](RuneCast/Assets/Scripts/Meta/InkBudget.cs:83)의
> 요약 주석은 "꺾쇠 2.0 … 지그재그 3.3 … 나선 4.8 … 별 5.1"로 적혀 있는데,
> 실제 상수는 1.8 / 3.4 / 4.4 / 5.0이다. 동작에 쓰이는 건 상수 쪽이다.

### 5.4 정확도 → 위력

| 값 | 수치 | 위치 |
|---|---|---|
| `minPower` (score 0) | **0.45** | [RuneCaster.cs:19](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:19) |
| `maxPower` (score 1) | **1.7** | [RuneCaster.cs:22](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:22) |
| 보간 | smoothstep `s²(3−2s)` | [RuneCaster.cs:182](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:182) |

```
power = lerp(0.45, 1.7, smoothstep(score)) × Loadout.PowerMultiplier(rune)
```

**선형이 아닌 이유**가 주석에 있다 ([RuneCaster.cs:180-181](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:180)) —
곡선을 위로 휘어 PERFECT 구간의 보상을 키운다. "선형이면 마지막 15%를 더 정확히 그리려는
동기가 생기지 않는다."

**등급은 위력 말고도 개수를 바꾼다:**

| 항목 | 계산 | 범위 | 위치 |
|---|---|---|---|
| 화살 발수 | `arrowCount + (int)grade` | 3 → 6 | [RuneCaster.cs:237](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:237) |
| 연쇄 홉 수 | `chainHops + (int)grade` | 4 → 7 | [RuneCaster.cs:271](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:271) |
| 화살 부채꼴 각 | `spread = 9°` | 고정 | [RuneCaster.cs:238](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:238) |

**실측 등급 분포와 평균 배율** (PLAN.md:150-158):

| 손그림 변형 | GOOD | GREAT | EXCELLENT | PERFECT | 평균 배율 |
|---|---|---|---|---|---|
| 아주 깔끔 0.02 | 2% | 6% | 20% | 73% | 1.61 |
| 깔끔 0.04 | 3% | 11% | 36% | 50% | 1.54 |
| 보통 0.07 | 16% | 43% | 32% | 9% | 1.23 |
| 대충 0.10 | 52% | 39% | 8% | 1% | 0.89 |
| 막 그림 0.14 | 82% | 15% | 2% | 0% | 0.68 |

정성껏 그렸을 때와 막 그렸을 때 위력이 **약 2.4배** 차이 난다.

#### 부호가 뒤집히는 자리 — 고양(Empower)

배율형 효과에는 `power`를 그대로 곱하면 안 된다
([RuneCaster.cs:288-292](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:288)).

```csharp
float dmg   = 1f + (empowerDamage - 1f) * power;   // 증가분에만 곱한다
float haste = 1f + (empowerHaste  - 1f) * power;
```

주석의 경고 — 배율 자체에 곱하면 못 그렸을 때 `1.55 × 0.45 = 0.7`배가 되어
**강화 룬이 아군을 약하게 만든다.**

소생도 같은 이유로 `Clamp(reviveHpFraction × power, 0.15, 0.9)`를 쓴다
([RuneCaster.cs:305](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:305)) — 1을 넘으면 최대
체력으로 잘려 잘 그린 보람이 사라진다.

### 5.5 룬별 기본 수치

전부 `RuneCaster`의 인스펙터 필드다
([RuneCaster.cs:24-55](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:24)).

| 룬 | 수치 | 값 | 위치 |
|---|---|---|---|
| Arrow | 발당 피해 / 기본 발수 | 26 / 3 | [:25-26](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:25) |
| Heal | 회복량 | 32 | [:27](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:27) |
| Shield | 보호막량 | 30 | [:28](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:28) |
| Meteor | 피해 | 70 | [:29](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:29) |
| ~~Slash~~ | 피해 / 반폭 | 30 / 0.45 | [:32-33](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:32) |
| Chain | 피해 / 홉 / 도약 거리 | 34 / 4 / 2.6 | [:36-38](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:36) |
| Vortex | 지속 / 흡인 속도 / 둔화 | 1.6 / 2.4 / 0.35 | [:41-43](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:41) |
| Empower | 공격력 / 공속 / 지속 | ×1.55 / ×1.35 / 5초 | [:46-48](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:46) |
| Revive | 부활 체력 비율 | 0.45 | [:51](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:51) |
| Pyre | 틱당 피해 / 지속 | 11 / 4.5초 | [:54-55](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:54) |

**효과 쪽 상수:**

| 값 | 수치 | 위치 |
|---|---|---|
| 연쇄 홉당 감쇠 | ×0.75 | [ChainLightning.cs:18](RuneCast/Assets/Scripts/Runes/Effects/ChainLightning.cs:18) |
| 봉화 틱 간격 | 0.4초 (→ 4.5초 동안 약 11틱) | [PyreField.cs:28](RuneCast/Assets/Scripts/Runes/Effects/PyreField.cs:28) |

**범위는 그린 크기에서 나온다.** 바운딩박스의 긴 변에 룬별 계수를 곱한다:

| 룬 | 계수 | 최소값 | 위치 |
|---|---|---|---|
| Heal / Vortex / Empower / Revive | ×0.5 | 0.4~0.55 | [RuneCaster.cs:216, 278, 286, 301](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:216) |
| Meteor | ×0.55 | 0.5 | [:251](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:251) |
| Pyre | ×0.45 | 0.5 | [:316](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:316) |

**두 룬만 바운딩박스를 쓰지 않는다:**
- **Slash** — 그은 선분 자체를 쓴다. "손이 그린 궤적과 피해 범위가 정확히 겹쳐야 직관적"
  ([RuneCaster.cs:259-260](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:259))
- **Pyre** — 획의 **시작점**(자루 끝)에 불이 붙는다. 바운딩박스 중심을 쓰면 고리 쪽으로
  치우쳐 손으로 찍은 자리와 어긋난다 ([RuneCaster.cs:313-315](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:313))

### 5.6 유닛 스탯

전부 [UnitStats.cs:26-121](RuneCast/Assets/Scripts/Battle/UnitStats.cs:26)의 단일 테이블.

| 종류 | HP | 공격력 | 사거리 | 공격 주기 | 이동 속도 | 설계 역할 (주석) |
|---|---|---|---|---|---|---|
| Skeleton | 30 | 5 | 0.95 | 0.7 | 1.55 | 다수·저체력·빠름 → 광역이 유리 |
| Bonelord | 135 | 14 | 1.2 | 1.15 | 0.85 | 소수·고체력·느림 → 단일 화력 |
| Vampire | 58 | 9 | 1.0 | 0.75 | 2.1 | 빠름 → 붙기 전에 처리 |
| Orc | 55 | 7 | 1.1 | 0.9 | 1.2 | 전부 평균 — 기준점 |
| **Wraith** | 90 | 6 | 1.1 | 1.0 | 0.95 | **평타 면역** — 반드시 룬을 써야 함 |
| **Shaman** | 45 | **0** | **3.8** | 2.0 | 0.8 | 때리지 않음. 위협은 단단함이 아니라 **위치** |
| **Gnoll** | 40 | 7 | **2.6** | 1.4 | 1.05 | 위협은 맷집이 아니라 **거리** |
| Spider | 52 | 6 | 1.0 | 0.85 | 1.35 | 죽으면 새끼 둘 |
| Spiderling | 22 | 4 | 0.9 | 0.7 | 1.9 | 빠름 |
| **Troll** (보스) | **420** | 11 | 1.4 | 1.5 | 0.7 | 체력만 큼. 위협은 예고된 강타 |
| **Warrior** (아군) | 100 | 8 | 1.15 | 0.8 | 1.5 | 기준 |
| Archer (아군) | 70 | 7 | 2.3 | 1.1 | 1.5 | ⚠️ 미사용·미검증 |
| Monk (아군) | 65 | **0** | 2.6 | 2.0 | 1.5 | ⚠️ 미사용·미검증 |

주석에 남은 **수치 결정의 연쇄**가 몇 군데 있다.

- **Wraith 90** — "화살 한 번(≈95)이면 정리된다. 룬을 써야 하지만 여러 번 써야 하는 건
  아니다" ([UnitStats.cs:42-44](RuneCast/Assets/Scripts/Battle/UnitStats.cs:42))
- **Shaman의 사거리 3.8과 회복 반경 4.4는 함께 정해졌다**
  ([UnitStats.cs:55-62](RuneCast/Assets/Scripts/Battle/UnitStats.cs:55)). 처음엔 4.6에서
  멈추게 하고 반경을 3.2로 뒀는데, 앞줄은 1.1까지 붙으므로 간격이 3.5가 되어 **회복이
  아무에게도 안 닿았다.** "시뮬레이터에서 회복을 껐다 켜도 결과가 한 자리도 안 달라져서 잡혔다."
- **Spider 52 → Spiderling 22×2 = 44** — "새끼 둘을 합쳐도 어미보다 약하다. 아니면
  죽이는 게 손해가 되고, 손해면 아무도 안 죽인다"
  ([UnitStats.cs:79-81](RuneCast/Assets/Scripts/Battle/UnitStats.cs:79))
- **Warrior는 옛 Soldier와 한 자리도 다르지 않다**
  ([UnitStats.cs:96-100](RuneCast/Assets/Scripts/Battle/UnitStats.cs:96)). 스프라이트만
  교체한 변경이라 값을 건드리면 12판 밸런스를 다시 재야 한다.
- **Archer / Monk는 `sim_player.py`로 확인하지 않은 값**이라고 주석이 명시한다
  ([UnitStats.cs:104-106](RuneCast/Assets/Scripts/Battle/UnitStats.cs:104)) — "근거 없는 숫자다."

### 5.7 전투 진행 수치

| 값 | 수치 | 위치 |
|---|---|---|
| `waveDelay` (물결 간격) | **3.4초** (이전 2.2) | [BattleManager.cs:84](RuneCast/Assets/Scripts/Battle/BattleManager.cs:84) |
| 전장 폭 | 7칸 (이전 11.2) | [BattleManager.cs:20](RuneCast/Assets/Scripts/Battle/BattleManager.cs:20) |

**물결 간격 2.2 → 3.4가 밸런스 전체를 흔들었다.** 연출(WAVE CLEAR 배너)을 위해 늘렸는데,
간격이 길어진 만큼 마나가 더 차서 1·2장이 전부 강화 없이 3별이 됐다 (5.10절).

### 5.8 장(章) 규칙 수치

7개 장에 하나씩 규칙이 걸린다 ([ChapterRules.cs](RuneCast/Assets/Scripts/Meta/ChapterRules.cs)).
설계 원칙이 클래스 주석에 있다 — **"수치만 키운 장은 장이 아니라 물결이다"**
([ChapterRules.cs:9](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:9)).

| 장 | 규칙 | 수치 | 위치 |
|---|---|---|---|
| 1 | Wraith | 평타 면역 적 등장 (적 쪽에 구현) | — |
| 2 | LimitedMana | `StageDef`가 판마다 지정 | — |
| 3 | Frost | 아군 공격 주기 **×1.30** | [:131](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:131) |
| 4 | Fog | **4.2** 칸보다 먼 적이 흐려짐 | [:137](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:137) |
| 5 | Plague | 적 사망 시 반경 **1.6** 내 아군 **6** 피해 | [:141-144](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:141) |
| 6 | Sealing | 판마다 룬 1종 잠금 | [:107-121](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:107) |
| 7 | Abyss | 시간 회복 **×0.50**, 처치당 **+7** | [:164, :168](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:164) |

**봉인은 무작위가 아니라 판 번호로 정해진다** (`stageId % n`,
[ChapterRules.cs:114](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:114)). 주석의 근거 —
"무작위면 다시 할 때마다 달라져서, 진 이유가 내 잘못인지 운인지 구분이 안 된다."
**소생은 절대 잠기지 않는다** ([:111](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:111)).

#### 7장 `ManaRegenScale` — 튜닝 과정이 기록된 값

[ChapterRules.cs:147-161](RuneCast/Assets/Scripts/Meta/ChapterRules.cs:147)의 주석에 훑어본
값이 전부 남아 있다.

| 값 | 강화 없을 때 결과 |
|---|---|
| 0.35 | **전패** (승률 0%) — "새 규칙이 어려운 게 아니라 그냥 못 지나가는 벽" |
| **0.50 (채택)** | 승률 67%, 평균 1.3명 생존 → **별 1개** |
| 0.65 | 승률 100%, 4.2명 생존 → 강화가 다시 무의미 |
| 0.80 | 승률 100%, 4.8명 생존 |

0.50을 고른 이유가 명시돼 있다 — 강화 1.6이면 전원 생존 3별이 되므로 **"강화해야 3별"이
여기서 돌아온다.** 물결 간격을 3.4초로 늘리면서 전 판이 강화 없이 3별이 되어 사라졌던
대장장이(강화 시스템)의 존재 이유다.

### 5.9 성장 곡선

**룬 레벨** ([Loadout.cs:13-44](RuneCast/Assets/Scripts/Meta/Loadout.cs:13)):

| 값 | 수식 / 수치 | 위치 |
|---|---|---|
| 위력 배율 | `1 + 0.08 × (level − 1)` | [:18](RuneCast/Assets/Scripts/Meta/Loadout.cs:18) |
| 강화 비용 | `10 + 8L + 2L²` 파편 | [:32](RuneCast/Assets/Scripts/Meta/Loadout.cs:32) |
| 최대 레벨 | 10 | [PlayerData.cs:105](RuneCast/Assets/Scripts/Meta/PlayerData.cs:105) |

레벨 10 = 배율 **1.72배**, 누적 비용 **1020 파편** (L=1..9 합).
주석에 수급과의 관계가 적혀 있다 ([Loadout.cs:25-26](RuneCast/Assets/Scripts/Meta/Loadout.cs:25)) —
전 스테이지 3별 첫 클리어 수급이 **2448**이므로 한 번 훑으면 룬 두 개를 만렙으로 올리거나
뽑기 24회를 할 수 있다. **"둘 다는 못 한다는 게 핵심이다. 다 되면 선택이 사라진다."**

**레벨은 위력만 올린다** ([Loadout.cs:9-11](RuneCast/Assets/Scripts/Meta/Loadout.cs:9)).
범위까지 올리면 잉크 제한이 무의미해지고, 마나까지 내리면 강화가 다른 모든 축을 덮어버린다.
범위·마나·잉크는 문양(뽑기)의 몫이다.

**문양** ([Loadout.cs:53-167](RuneCast/Assets/Scripts/Meta/Loadout.cs:53)):

| 효과 | 합산 방식 | 상한 |
|---|---|---|
| Power | 가산 (`1 + Σ`) | 없음 |
| Size | 가산 | 없음 |
| ManaCost | 가산 할인 | **60%** ([:55](RuneCast/Assets/Scripts/Meta/Loadout.cs:55)) |
| InkCost | 가산 | **60%** |
| Grade | `score`에 직접 가산 | `Clamp01` |

**곱이 아니라 합인 이유**가 주석에 있다 — "곱셈이면 문양을 모을수록 기하급수적으로 세져서
후반 밸런스가 통째로 무너진다."

**Grade 문양만 성질이 다르다.** 위력이 아니라 `score` 자체에 더해지므로
([RuneCaster.cs:178](RuneCast/Assets/Scripts/Runes/RuneCaster.cs:178)) 등급 연출까지 같이
올라간다 — "그래야 문양을 낀 값을 한다."

**잉크 문양은 룬을 가리지 않는다** ([Loadout.cs:154-161](RuneCast/Assets/Scripts/Meta/Loadout.cs:154)):
잉크 한도는 그리기 **시작할 때** 정해지는데 그 시점엔 무슨 룬인지 알 수 없다.

### 5.10 별 판정과 시뮬레이터 검증

**별은 생존한 아군 수로 매긴다** ([StageProgress.cs:172-178](RuneCast/Assets/Scripts/Meta/StageProgress.cs:172)):

| 조건 | 별 |
|---|---|
| 전원 생존 | 3 |
| 절반 이상 생존 | 2 |
| 1명 이상 생존 | 1 |
| 전멸 | 0 |

주석의 근거 — 점수제가 아니라 생존 기준인 이유는 "플레이어가 실제로 조절하는 건 아군을
지켰는가"이고, 처치 속도로 매기면 룬을 아끼지 않고 쏟아붓는 쪽이 유리해져 **마나 설계와
어긋나기** 때문이다.

기본 아군 구성: 4명, HP 100, 공격력 8
([StageDef.cs:111-113](RuneCast/Assets/Scripts/Meta/StageDef.cs:111)).

전체 43개 `StageDef` = 프롤로그 1 + **42판**. 프롤로그는 의도적으로 `StageDatabase.All`에서
빠져 있다 ([StageDef.cs:157-166](RuneCast/Assets/Scripts/Meta/StageDef.cs:157)) — 목록에 뜨면
안 되고, 넣으면 "별을 세는 모든 계산(잉크 단계·장착 칸)이 판 하나만큼 어긋난다."
따라서 **획득 가능한 총 별은 42 × 3 = 126개**다.

프롤로그의 수치는 밸런스가 아니라 **연출**이라고 명시돼 있다 — 넷 앞에 열둘을 세운 건
"이건 못 이긴다"가 한눈에 읽혀야 해서고, 아군 체력을 크게 준 건 대사가 끝나기 전에 누가
쓰러지면 안 되기 때문이다. 시뮬레이터 대상이 아니라 `read_stages.py`에도 잡히지 않는다.

#### 밸런스 검증 도구

`StageDef.cs:41-77`의 주석이 방법론을 밝힌다 — **"눈으로 맞추지 않았다."**

| 도구 | 하는 일 |
|---|---|
| `tools/sim_battle.py` | 개입이 없을 때 / "초당 몇 피해면 이기나" |
| `tools/sim_player.py` | **마나를 쓰는 플레이어**를 흉내 내어 42판 전부를 돈다 |
| `tools/read_stages.py` | `StageDef.cs`에서 표를 직접 파싱 (손으로 옮길 필요 없음) |

`sim_player.py`는 게임 규칙을 그대로 옮겼다 — 마나 회복, 룬별 비용·피해, 그리는 데 걸리는
시간(`DRAW_REAL`: 화살 0.7초 / 연쇄 1.1 / 별똥별 1.3 / 봉화 0.8, 그동안 시간은 0.2배),
판단 시간 `THINK = 0.55초`, 망령의 룬 전용 피해까지.
플레이어 정책은 "살 수 있는 것 중 마나당 피해가 제일 큰 룬을 쓴다."

**도구 자신의 한계를 스스로 기록한다.** `sim_player.py`의 헤더 주석:

> **하한선이라고 부르지 않는다.** 예전 주석에는 그렇게 적혀 있었는데 사실이 아니었다.
> 이 흉내내기는 도형을 틀리지 않고, 손이 미끄러지지 않고, 마나가 차자마자 그린다.
> 사람은 그러지 못한다. 그러니 여기서 이긴다고 사람도 이긴다는 보장은 없다 —
> **여기서 지면 사람은 확실히 진다**는 쪽만 믿을 수 있다.

같은 파일에 **도구가 눈금 구실을 못 하던 시기**도 기록돼 있다. 예전에는 "기대 피해" 하나로
뭉쳐 적들에게 남김없이 나눠 넣어서 초과 피해가 한 방울도 안 버려졌다 — 체력 5 남은 적을
26짜리 화살로 때려도 21이 뒷줄로 흘러갔다. "광역도 아니고 사거리도 없는 완벽 분배 삭제기"라
어떤 판이든 100% 3별이 나왔다. 지금은 한 대씩 넣고 넘치는 건 버린다.

#### 실측 결과 (2026-08-05, 판당 30회)

| 장 (판) | 강화 1.0 | 강화 1.6 | 강화 2.2 |
|---|---|---|---|
| 1~2 (1~12) | 전부 3별 | 3별 | 3별 |
| 3 (13~18) | 3별, 18판만 2별 | 3별 | 3별 |
| 4 (19~24) | 3별, 22판 2별 · 24판 1별 | 3별 | 3별 |
| 5 (25~30) | 27·28판 2별 · **30판 승률 37%** | 3별 | 3별 |
| 6 (31~36) | 3별, 36판만 2별 | 3별 | 3별 |
| 7 (37~42) | 37~39판 2별 · **40~42판 전패** | 41판 1별 | **42판 1별** |

주석의 해석:
- **1·2장은 강화 없이 전부 3별** — 물결 간격을 2.2→3.4초로 늘리면서 그렇게 됐다.
  "앞 열두 판은 이제 난이도가 아니라 배우는 구간이다."
- **강화가 필요해지는 곳은 5장부터.** 30판에서 처음으로 강화 없이 지기 시작한다.
- **42판은 만렙(2.2)에서 1별로 겨우 이긴다** — 최종 판으로 의도한 자리.

#### 이 표를 믿기 전에 볼 것

`StageDef.cs:67-75`가 시뮬레이터가 **안 세는 것**을 명시한다.

| 미반영 항목 | 방향 |
|---|---|
| 힐·보호막·고양·소생, "어디에 쓸지"의 판단 | 실제는 **더 쉽다** |
| **4장 안개** — 유닛이 흐려질 뿐이라 도구가 잴 게 없다 | 4장 숫자는 **실제보다 쉽게** 나옴 |
| **6장 봉인** — 시뮬레이터는 룬 넷만 쓰는데 게임은 아홉에서 고름 | 6장도 **실제보다 쉽게** 나옴 |

주석의 경고 — "6장 표가 3장처럼 평평한 건 그래서다. 적을 더 넣어 봤지만 별 수가 안 움직였다.
**표를 보고 6장을 더 조이면 실제로는 과해진다.** 이 장은 사람이 해 보고 정해야 한다."

---

## 6. 이후 계획

*(미작성)*

---

# 부록 A — 아직 답이 없는 질문

> **발행 전 이 부록은 삭제할 것.** 아래는 코드로 확인되지 않아 본문에 쓰지 않은 "왜"들이다.
> 답을 채운 뒤 해당 절에 녹여 넣으면 문서가 완성된다.

## A-1. 인식 임계값

1. **`RejectDistance = 0.13`을 정확히 그 값으로 고른 기준은?**
   주석의 실측 분포에서 "대충"의 90% 지점이 0.117, 낙서의 최소가 0.075다. 두 분포가 겹치는
   구간인데 0.13을 고른 판단 기준이 무엇이었나 — 통과율 목표? 오발동률 상한?
2. **`PerfectDistance = 0.035`는?** "깔끔한 입력"의 중앙값이 0.039인데 그보다 낮게 잡았다.
   PERFECT를 의도적으로 어렵게 만든 것인가?
3. **등급 컷 0.85 / 0.65 / 0.40의 근거는?**
   실측상 "깔끔 0.04"에서도 PERFECT가 50%다. 목표한 PERFECT 비율이 있었나, 아니면
   분포를 보고 사후에 맞춘 것인가?
4. **`minPower 0.45` / `maxPower 1.7`** — 코드 주석은 "최하 등급과 3배 이상 차이"라고 하는데
   (0.45→1.7은 3.8배), 실제 등급 분포상 평균 배율 차이는 2.4배(0.68↔1.61)다.
   설계 목표는 어느 쪽이었나?
5. **smoothstep을 고른 이유는?** 다른 이징 곡선을 시험해 봤나?
6. **시작점 4개** — 2개로 줄이면 1.2%p 손해에 1.8배 빠르다. 그 1.2%p를 지킨 판단 기준은?
   목표 프레임 예산이 있었나?
7. **리샘플 32** — 64는 효과가 없었다고 기록돼 있는데 16은 시험했나?
8. **낙서 오발동률의 목표선이 있나?** 깃발 회전 변형을 빼서 13.9% → 8.2%로 낮췄는데,
   8.2%는 만족스러운 값인가 아니면 아직 높은가?
9. **템플릿 37개의 상한을 생각해 둔 게 있나?** 회전 정규화 대신 변형 등록을 택했으니
   룬이 늘수록 선형으로 늘어난다. 매칭 비용과 템플릿 간 분리도 양쪽에 한계가 올 텐데.
10. **멀티스트로크는 계획인가 폐기인가?** `GPoint`에 `StrokeId`가 있고
    `StrokeMath`가 이를 처리하는데, 실제로 쓰는 템플릿이 하나도 없다.

## A-2. 밸런스

11. **마나 `max 100` / `regen 9`의 근거는?** 비용 서열의 이유는 주석에 있는데 절대값의
    근거가 없다. 초당 9면 화살 1.7초 / 소생 6.1초인데, 이 리듬을 의도한 것인가?
12. **`unlimited = true`가 기본값이다.** 42판 중 실제로 유한 마나인 판은 몇 개인가?
    2장부터라고 되어 있는데 3~7장은 전부 유한인가?
13. **잉크 티어가 30별에서 끝난다.** 획득 가능한 총 별이 126개(42판 × 3)인데 최대 티어가
    30별이면 **약 24% 지점**이다. 왜 이렇게 일찍 끝나나 — 이후 성장은 룬 레벨·문양이
    맡는 구조인가, 아니면 티어를 더 늘릴 여지를 남겨 둔 것인가?
14. **문양 할인 상한 60%의 근거는?** "공짜에 가까워지면 자원 설계가 사라진다"까지는
    적혀 있는데 왜 60인지는 없다.
15. **룬 레벨 만렙 1020 vs 첫 클리어 수급 2448** — "룬 둘 또는 뽑기 24회"라는 계산은
    있는데, 목표한 플레이 시간이나 반복 횟수는?
16. **사람 플레이테스트는 얼마나 돌렸나?** 시뮬레이터 수치는 풍부한데 사람 기록이 안 보인다.
    특히 시뮬레이터가 못 재는 4장(안개)·6장(봉인)은 어떻게 확정할 계획인가?
17. **3~7장은 확정인가?** `StageDef.cs:96-99`에 "3~7장은 아직 틀만 있는 판이라 이름을
    안 붙였다"고 적혀 있다. 밸런스 수치는 확정으로 봐도 되나?
18. **Archer / Monk를 언제 붙일 계획인가?** 스탯만 있고 소환되지 않으며 시뮬레이터로
    검증되지 않았다고 주석이 명시한다.
19. **가르기(Slash)를 다시 켤 계획이 있나?** "사거리나 대상 수에 상한을 둬야 한다"까지
    진단이 나와 있는데, 되살릴 것인지 영구 폐기인지.
20. **Empower 지속 5초 / Vortex 지속 1.6초** 등 지속시간 값들의 근거는? 다른 수치와 달리
    이쪽엔 주석이 없다.

## A-3. 문서화하며 발견한 코드-문서 불일치

셋 다 동작에는 영향이 없지만, 포트폴리오로 공개한다면 정리하는 편이 낫다.

21. **`RuneType.cs:13`** — Shield를 "네모 □"로 설명한다. 실제 템플릿은 삼각형이고
    (`RuneTemplateLibrary.cs:79-88`), 네모를 버린 이유가 바로 아래 주석에 상세히 적혀 있다.
    **주석만 옛날 것이 남았다.**
22. **`InkBudget.cs:83-84`** — 요약 주석의 비율(꺾쇠 2.0 / 지그재그 3.3 / 나선 4.8 / 별 5.1)이
    바로 아래 상수(1.8 / 3.4 / 4.4 / 5.0)와 다르다.
23. **`PLAN.md`가 여러 곳에서 낡았다** — "N=64로 리샘플"(실제 32), "룬 7종"(실제 9종 활성),
    쉴드 마나 30인데 표에는 삼각형/네모가 섞여 있다. PLAN.md를 계속 유지할 것인지,
    아니면 이 설계 문서로 대체할 것인지 정해야 한다.
