using System;
using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;

namespace RuneCast.Runes
{
    /// <summary>
    /// 궤적 → 인식 → 마나 소모 → 효과 발동을 잇는 지점.
    /// 인식 결과의 score(0~1)가 효과 강도로 이어져서, 대충 그리면 약하게 나간다.
    /// </summary>
    public class RuneCaster : MonoBehaviour
    {
        [Header("강도 매핑")]
        [Tooltip("score 0일 때의 배율. 인식은 됐으니 0으로 만들지는 않는다.")]
        public float minPower = 0.45f;

        [Tooltip("score 1일 때의 배율. 최하 등급과 3배 이상 차이가 나야 체감된다.")]
        public float maxPower = 1.7f;

        [Header("기본 수치")]
        public float arrowDamage = 26f;
        public int arrowCount = 3;
        public float healAmount = 32f;
        public float shieldAmount = 30f;
        public float meteorDamage = 70f;

        [Header("가르기")]
        public float slashDamage = 30f;
        public float slashHalfWidth = 0.45f;

        [Header("연쇄 번개")]
        public float chainDamage = 34f;
        public int chainHops = 4;
        public float chainJumpRange = 2.6f;

        [Header("소용돌이")]
        public float vortexDuration = 1.6f;
        public float vortexPullSpeed = 2.4f;
        [Range(0f, 1f)] public float vortexSlow = 0.35f;

        [Header("고양")]
        public float empowerDamage = 1.55f;
        public float empowerHaste = 1.35f;
        public float empowerDuration = 5f;

        [Header("소생")]
        [Range(0.1f, 1f)] public float reviveHpFraction = 0.45f;

        [Header("봉화")]
        public float pyreTickDamage = 11f;
        public float pyreDuration = 4.5f;

        /// <summary>HUD가 구독한다. (결과, 실제 발동 여부, 실패 사유)</summary>
        public event Action<RecognitionResult, bool, CastFail> Cast;

        /// <summary>각인 모드가 궤적을 가져갈 때 발동을 막는다.</summary>
        [NonSerialized] public bool suspended;

        /// <summary>
        /// 전투 중일 때만 true. GameFlow가 켜고 끈다.
        ///
        /// suspended와 따로 두는 이유: 둘을 한 변수로 합치면 각인 모드를 빠져나올 때
        /// 스테이지 선택 화면인데도 발동이 다시 켜진다. 끄는 주체가 둘이면 플래그도 둘이어야 한다.
        /// </summary>
        [NonSerialized] public bool castingEnabled = true;

        /// <summary>업적 집계용. 없어도 동작한다.</summary>
        [NonSerialized] public GameFlow flow;

        private ManaPool _mana;
        private Camera _cam;

        public RecognitionResult LastResult { get; private set; }
        public List<KeyValuePair<string, float>> LastDistances { get; private set; }

        private void Awake()
        {
            _mana = GetComponent<ManaPool>();
            LastDistances = new List<KeyValuePair<string, float>>();
        }

        private void Start()
        {
            _cam = Camera.main;
        }

        private TraceCapture _capture;

        public void Bind(TraceCapture capture)
        {
            _capture = capture;
            capture.StrokeCompleted += OnStrokeCompleted;
        }

        private void OnStrokeCompleted(List<Vector2> screenPoints)
        {
            if (suspended || !castingEnabled) return;

            // 잉크가 떨어져 잘린 획은 발동시키지 않는다. 마나도 쓰지 않는다 —
            // 잃는 건 슬로우로 흘려보낸 시간뿐이고, 그게 "더 작게 그려라"를 가르치는 비용이다.
            if (_capture != null && _capture.StrokeRanOutOfInk)
            {
                AudioManager.Play(Sfx.FailShape, 0.7f);
                Fire(default(RecognitionResult), false, CastFail.Ink);
                return;
            }

            var pts = TraceCapture.ToGPoints(screenPoints);

            // 진단용 거리표를 같은 패스에서 채운다 — 따로 부르면 매칭을 통째로 두 번 돈다
            var result = PointCloudRecognizer.Recognize(pts, RuneTemplateLibrary.Templates, LastDistances);
            LastResult = result;

            if (!result.Recognized)
            {
                AudioManager.Play(Sfx.FailShape, 0.75f);
                Fire(result, false, CastFail.Shape);
                return;
            }

            // 좌표 환산을 마나 소모보다 먼저 한다. 소생처럼 "대상이 있어야 발동하는" 룬은
            // 위치를 알아야 대상 유무를 판정할 수 있는데, 그 판정이 마나를 쓴 뒤에 오면
            // 되살릴 사람이 없을 때 마나만 날아간다.
            Vector3 center;
            Vector2 worldSize;
            ToWorld(screenPoints, out center, out worldSize);

            // 소생만 사전 조건이 있다. 시체가 없으면 아무 일도 일어나지 않으므로
            // 실패로 처리하고 마나를 돌려준다 — 화면에 시체가 안 보이는데 55를 잃으면
            // 플레이어는 룬이 고장 난 줄 안다.
            if (result.Type == RuneType.Revive)
            {
                float checkRadius = Mathf.Max(worldSize.x, worldSize.y) * 0.5f
                                    * Loadout.SizeMultiplier(result.Type);
                if (!RevivePulse.HasTarget(center, Mathf.Max(checkRadius, 0.5f)))
                {
                    // 마나 부족과 같은 파일이지만 피치를 낮춰 다른 사건으로 들리게 한다.
                    // 실패 사유마다 다른 소리를 낸다는 원칙은 룬이 늘어도 유지해야 한다.
                    AudioManager.Play(Sfx.FailMana, 0.7f, 0.72f);
                    Fire(result, false, CastFail.NoCorpse);
                    return;
                }
            }

            // 봉인(6장) — 이 판에 잠긴 룬은 안 나간다.
            //
            // **마나를 쓰기 전에 막는다.** 뒤에 두면 잠긴 룬을 그릴 때마다
            // 마나가 사라진다. 잠긴 걸 잊고 그리는 일이 이 규칙의 핵심 경험인데,
            // 거기에 자원까지 뺏으면 규칙이 아니라 벌이 된다.
            if (RuneCast.Meta.ChapterRules.IsSealed(result.Type))
            {
                AudioManager.Play(Sfx.FailMana, 0.75f, 0.62f);
                Fire(result, false, CastFail.Sealed);
                return;
            }

            if (_mana != null && !_mana.TrySpend(result.Type))
            {
                // (문양의 마나 할인은 ManaPool.EffectiveCost에서 이미 반영된다)
                // 실패 사유마다 다른 소리를 낸다. 같은 소리면 "못 알아봤나 / 마나가 없나"를
                // 화면 글씨를 읽어야만 구분할 수 있는데, 그럴 여유가 있는 상황이 아니다.
                AudioManager.Play(Sfx.FailMana, 0.8f);
                Fire(result, false, CastFail.Mana);
                return;
            }

            // ── 메타 보정 (룬 레벨 + 장착 문양) ──
            // 범위 보정은 잉크 제한 뒤에 곱해진다. 잉크는 "얼마나 크게 그릴 수 있나"를 정하고
            // 이건 "그린 것이 얼마나 넓게 퍼지나"라서, 둘을 곱해도 서로를 무력화하지 않는다.
            worldSize *= Loadout.SizeMultiplier(result.Type);

            // 판정 보정은 점수 자체에 더한다 — 위력만이 아니라 등급 연출까지 같이 올라가야
            // 문양을 낀 값을 한다.
            float score = Mathf.Clamp01(result.Score + Loadout.GradeBonus(result.Type));

            // 잘 그릴수록 세진다. 곡선을 살짝 위로 휘어서 'PERFECT' 구간의 보상을 키운다 —
            // 선형이면 마지막 15%를 더 정확히 그리려는 동기가 생기지 않는다.
            float eased = score * score * (3f - 2f * score); // smoothstep
            float power = Mathf.Lerp(minPower, maxPower, eased) * Loadout.PowerMultiplier(result.Type);

            RuneGrade grade = RuneGrading.Of(score);

            // 업적은 여기 한 곳에서만 알린다. 각 효과 안에서 세면 룬을 추가할 때마다
            // 빠뜨리고, 그러면 어떤 룬은 세어지고 어떤 룬은 안 세어진다.
            Achievements.OnRuneCast(grade == RuneGrade.Perfect);
            if (flow != null) flow.NotifyRuneUsed(result.Type);

            CameraShake.Shake(GradeVisuals.ShakeOf(grade));
            float vol = GradeVisuals.VolumeOf(grade);

            // 등급 자체의 소리. 룬 소리와 겹쳐 울리되 등급이 높을 때만 난다 —
            // 매번 울리면 "잘했다"는 신호가 아니라 그냥 배경음이 된다.
            if (grade == RuneGrade.Perfect)
            {
                AudioManager.Play(Sfx.GradePerfect, 0.95f);

                // 시전 순간을 아주 짧게 끊는다. 슬로우에서 실시간으로 풀리는 전환에
                // 한 박자가 생겨서 "제대로 들어갔다"가 손끝에 남는다.
                TimeControl.HitStop(0.05f, 0.07f);
            }
            else if (grade == RuneGrade.Excellent) AudioManager.Play(Sfx.GradeGreat, 0.7f);

            // 등급을 시전 위치에도 띄운다. 화면 중앙 표시는 궤적을 보던 시선에서 멀다.
            FloatingText.Label(center + new Vector3(0f, 0.7f, 0f),
                RuneGrading.DisplayName(grade), GradeVisuals.ColorOf(grade),
                grade == RuneGrade.Perfect ? 30f : 22f);

            switch (result.Type)
            {
                case RuneType.Heal:
                {
                    float radius = Mathf.Max(worldSize.x, worldSize.y) * 0.5f;
                    HealPulse.Cast(center, Mathf.Max(radius, 0.4f), healAmount * power, grade);
                    AudioManager.Play(Sfx.CastHeal, vol);
                    break;
                }

                case RuneType.Shield:
                {
                    ShieldZone.Cast(center, worldSize, shieldAmount * power, grade);
                    AudioManager.Play(Sfx.CastShield, vol);
                    break;
                }

                case RuneType.Arrow:
                {
                    float dx, dy;
                    StrokeMath.ApexDirection(pts, out dx, out dy);
                    var dir = new Vector2(dx, dy);

                    // 여러 발을 부채꼴로 — 한 발이면 빗나갔을 때 아무 일도 안 일어난 것처럼 보인다.
                    // 발수까지 등급에 걸어서, 위력 배율보다 눈에 먼저 들어오게 한다.
                    int count = arrowCount + (int)grade;
                    float spread = 9f;
                    for (int i = 0; i < count; i++)
                    {
                        float a = (i - (count - 1) * 0.5f) * spread;
                        Vector2 d = Quaternion.Euler(0f, 0f, a) * dir;
                        Projectile.Spawn(center, d, arrowDamage * power, Team.Enemy, GradeVisuals.ColorOf(grade));
                    }
                    AudioManager.Play(Sfx.CastArrow, vol);
                    break;
                }

                case RuneType.Meteor:
                {
                    float radius = Mathf.Max(worldSize.x, worldSize.y) * 0.55f;
                    MeteorStrike.Cast(center, Mathf.Max(radius, 0.5f), meteorDamage * power, grade);
                    AudioManager.Play(Sfx.CastMeteor, vol); // 착탄음은 MeteorStrike.Detonate에서 따로
                    break;
                }

                case RuneType.Slash:
                {
                    // 이 룬만 바운딩박스가 아니라 그은 선분 자체를 쓴다.
                    // 손이 그린 궤적과 피해 범위가 정확히 겹쳐야 직관적이다.
                    Vector3 from = ToWorldPoint(screenPoints[0]);
                    Vector3 to = ToWorldPoint(screenPoints[screenPoints.Count - 1]);
                    SlashWave.Cast(from, to, slashHalfWidth * power, slashDamage * power, grade);
                    break;
                }

                case RuneType.Chain:
                {
                    // 튕기는 횟수도 등급에 건다
                    ChainLightning.Cast(center, chainJumpRange, chainDamage * power,
                        chainHops + (int)grade, grade);
                    AudioManager.Play(Sfx.CastChain, vol);
                    break;
                }

                case RuneType.Vortex:
                {
                    float radius = Mathf.Max(worldSize.x, worldSize.y) * 0.5f;
                    Vortex.Cast(center, Mathf.Max(radius, 0.55f), vortexDuration * power,
                        vortexPullSpeed, vortexSlow, grade);
                    break;
                }

                case RuneType.Empower:
                {
                    float radius = Mathf.Max(worldSize.x, worldSize.y) * 0.5f;

                    // 배율은 1을 기준으로 증가분에만 power를 건다.
                    // 배율 자체에 곱하면 못 그렸을 때 1.55×0.45 = 0.7배가 되어
                    // **강화 룬이 아군을 약하게 만든다.** 부호가 뒤집히는 실수가 나오는 자리다.
                    float dmg = 1f + (empowerDamage - 1f) * power;
                    float haste = 1f + (empowerHaste - 1f) * power;

                    EmpowerAura.Cast(center, Mathf.Max(radius, 0.5f), dmg, haste,
                        empowerDuration, grade);
                    break;
                }

                case RuneType.Revive:
                {
                    float radius = Mathf.Max(worldSize.x, worldSize.y) * 0.5f;

                    // 회복 비율에도 power를 그대로 곱하지 않는다 — 1을 넘으면 최대 체력으로
                    // 잘리므로 잘 그린 보람이 사라진다. 상한을 90%로 두고 그 안에서 벌린다.
                    float frac = Mathf.Clamp(reviveHpFraction * power, 0.15f, 0.9f);
                    Achievements.OnRevived(
                        RevivePulse.Cast(center, Mathf.Max(radius, 0.5f), frac, grade));
                    break;
                }

                case RuneType.Pyre:
                {
                    // 자루 끝(획의 시작점)에 불이 붙는다. 바운딩박스 중심을 쓰면
                    // 고리 쪽으로 치우쳐서, 손으로 찍은 자리와 불이 어긋난다.
                    Vector3 root = ToWorldPoint(screenPoints[0]);
                    float radius = Mathf.Max(worldSize.x, worldSize.y) * 0.45f;

                    PyreField.Cast(root, Mathf.Max(radius, 0.5f), pyreTickDamage * power,
                        pyreDuration, grade);
                    break;
                }
            }

            Fire(result, true, CastFail.None);
        }

        private void Fire(RecognitionResult r, bool success, CastFail fail)
        {
            if (Cast != null) Cast(r, success, fail);
        }

        /// <summary>실패 사유를 화면에 보여줄 글로. 화면에 쓸 때만 부른다.</summary>
        public static string ReasonText(CastFail fail)
        {
            switch (fail)
            {
                case CastFail.Ink: return Loc.T("hud.failInk");
                case CastFail.Shape: return Loc.T("hud.failShape");
                case CastFail.NoCorpse: return Loc.T("hud.failNoCorpse");
                case CastFail.Mana: return Loc.T("hud.failMana");
                case CastFail.Sealed: return Loc.T("hud.failSealed");
                default: return null;
            }
        }

        /// <summary>화면 좌표 한 점을 z=0 평면의 월드 좌표로.</summary>
        private Vector3 ToWorldPoint(Vector2 screen)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return Vector3.zero;

            Vector3 w = _cam.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, Mathf.Abs(_cam.transform.position.z)));
            w.z = 0f;
            return w;
        }

        /// <summary>화면 궤적의 중심과 크기를 월드 단위로 환산.</summary>
        private void ToWorld(List<Vector2> screenPoints, out Vector3 center, out Vector2 size)
        {
            if (_cam == null) _cam = Camera.main;

            center = Vector3.zero;
            size = Vector2.one;
            if (_cam == null || screenPoints.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < screenPoints.Count; i++)
            {
                if (screenPoints[i].x < minX) minX = screenPoints[i].x;
                if (screenPoints[i].y < minY) minY = screenPoints[i].y;
                if (screenPoints[i].x > maxX) maxX = screenPoints[i].x;
                if (screenPoints[i].y > maxY) maxY = screenPoints[i].y;
            }

            float depth = Mathf.Abs(_cam.transform.position.z);
            Vector3 lo = _cam.ScreenToWorldPoint(new Vector3(minX, minY, depth));
            Vector3 hi = _cam.ScreenToWorldPoint(new Vector3(maxX, maxY, depth));

            center = new Vector3((lo.x + hi.x) * 0.5f, (lo.y + hi.y) * 0.5f, 0f);
            size = new Vector2(Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.y - lo.y));
        }
    }
}
