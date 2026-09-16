using System;
using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Gesture
{
    /// <summary>
    /// 각인 모드 — 원하는 도형을 3번 그려 룬에 묶는다.
    ///
    /// 인식 엔진이 템플릿 단일 방식이라 여기서 새로 만들 로직이 없다.
    /// 궤적을 받아 저장소에 넘기는 게 전부 — 기본 룬과 완전히 같은 경로를 탄다.
    /// 별도 씬을 만들지 않고 전투 화면에서 토글하는 이유는, 실제로 쓸 크기·속도로
    /// 그려봐야 등록한 템플릿이 쓸모 있기 때문이다.
    /// </summary>
    public class RuneRegistrar : MonoBehaviour
    {
        public const int SamplesNeeded = 3;

        public bool Active { get; private set; }
        public RuneType Target { get; private set; }
        public int Collected { get { return _pending.Count; } }

        /// <summary>모드 전환/샘플 수집 때마다 HUD가 갱신되도록.</summary>
        public event Action Changed;

        private readonly List<List<GPoint>> _pending = new List<List<GPoint>>();
        private Runes.RuneCaster _caster;
        private TraceCapture _capture;
        private bool _captureBeforeRegistering;
        private float _inkLimitBeforeRegistering = float.MaxValue;

        private void Awake()
        {
            Target = RuneType.Heal;
        }

        public void Bind(TraceCapture capture, Runes.RuneCaster caster)
        {
            _caster = caster;
            _capture = capture;
            capture.StrokeCompleted += OnStrokeCompleted;
        }

        private void Update()
        {
            if (Hotkeys.Down(Hotkey.Tab)) Toggle();
            if (!Active) return;

            // 활성 룬 목록을 그대로 순회한다. 예전엔 여기에 룬을 손으로 적어놨는데,
            // 룬을 셋 추가하고 이 목록을 안 고쳐서 **새 룬은 각인 자체가 불가능**했다.
            // 목록이 두 곳에 있으면 반드시 어긋난다 — HUD 범례도 같은 이유로 합쳤다.
            var runes = RuneCast.Meta.GlyphTable.Runes;
            for (int i = 0; i < runes.Length && i < NumberKeys.Length; i++)
                if (Hotkeys.Down(NumberKeys[i])) SetTarget(runes[i]);

            if (Hotkeys.Down(Hotkey.R)) DeleteAll();
        }

        /// <summary>
        /// 각인권이 없으면 각인 모드를 못 연다. 다만 **들어가는 데는 안 쓴다** —
        /// 실제로 도형을 다 가르쳤을 때 쓴다.
        ///
        /// 들어갈 때 쓰면 구경만 하고 나와도 한 장이 날아간다. 세 번 그리다
        /// 마음에 안 들어 그만두는 것도 마찬가지다. 그건 배우는 걸 벌하는 것이다.
        /// </summary>
        public static bool CanOpen { get { return RuneCast.Meta.PlayerData.InscribeTickets > 0; } }

        public void Toggle()
        {
            Active = !Active;
            _pending.Clear();

            // 각인 중에는 룬이 발동하면 안 된다 — 전투 화면 위에서 돌리기 때문
            if (_caster != null) _caster.suspended = Active;

            // 각인할 때는 잉크 제한을 푼다. 전투 중에 Tab을 누른 경우 제한이 걸린 채라
            // 큰 도형을 아예 가르칠 수 없다. 나올 때 원래 한도로 되돌린다.
            if (_capture != null)
            {
                if (Active)
                {
                    _inkLimitBeforeRegistering = _capture.InkMax;
                    _capture.SetInkLimit(float.MaxValue);

                    // 대장장이에서는 획 수집이 꺼져 있다. 각인은 그리는 게
                    // 전부이므로 여기서 다시 켠다.
                    _captureBeforeRegistering = _capture.captureEnabled;
                    _capture.captureEnabled = true;
                }
                else
                {
                    _capture.SetInkLimit(_inkLimitBeforeRegistering);
                    _capture.captureEnabled = _captureBeforeRegistering;
                }
            }

            Raise();
        }

        /// <summary>숫자키로 고를 수 있는 만큼. 룬이 이보다 많으면 나머지는 화면 버튼으로만 고른다.</summary>
        private static readonly Hotkey[] NumberKeys =
        {
            Hotkey.Num1, Hotkey.Num2, Hotkey.Num3, Hotkey.Num4,
            Hotkey.Num5, Hotkey.Num6, Hotkey.Num7,
        };

        /// <summary>등록한 커스텀 룬을 전부 지운다. 화면 버튼과 R키가 같이 쓴다.</summary>
        public void DeleteAll()
        {
            CustomRuneStore.DeleteAll();
            _pending.Clear();
            Raise();
        }

        /// <summary>화면 버튼용. 폰에는 키보드가 없어 이 경로가 유일한 선택 수단이다.</summary>
        public void SelectTarget(RuneType t)
        {
            SetTarget(t);
        }

        private void SetTarget(RuneType t)
        {
            if (Target == t) return;
            Target = t;
            _pending.Clear(); // 대상이 바뀌면 모으던 샘플은 의미가 없다
            Raise();
        }

        private void OnStrokeCompleted(List<Vector2> screenPoints)
        {
            if (!Active) return;

            var pts = TraceCapture.ToGPoints(screenPoints);
            _pending.Add(StrokeMath.Normalize(pts));

            if (_pending.Count >= SamplesNeeded) Commit();
            Raise();
        }

        /// <summary>마지막 각인이 왜 무산됐나. 아무 일도 없었으면 None.</summary>
        public enum CommitResult { None, Ok, NoTicket, TooSimilar }

        public CommitResult LastCommit { get; private set; }

        /// <summary>부딪힌 상대 룬. LastCommit이 TooSimilar일 때만 뜻이 있다.</summary>
        public RuneType Collided { get; private set; }

        /// <summary>
        /// 새 도형이 다른 룬의 도형과 이보다 가까우면 거절한다.
        ///
        /// **각인은 대체가 아니라 추가다.** RuneTemplateLibrary.Add는 기존 템플릿
        /// 옆에 새 것을 덧붙인다. 그래서 회복(원)과 비슷한 도형을 화살에 각인하면,
        /// 그 뒤로 원을 그릴 때마다 둘 중 무엇이 나갈지 알 수 없어진다.
        ///
        /// **플레이어는 자기가 무엇을 부쉈는지 모른다.** 각인은 대장장이에서 하고
        /// 그 결과는 다음 전투에서 나타나므로, 원인과 증상이 멀리 떨어져 있다.
        /// "회복이 갑자기 안 나간다"로 겪게 되고, 그러면 게임이 고장 난 것으로 읽힌다.
        ///
        /// 기준은 인식기가 도형을 받아주는 거리(RejectDistance)와 같게 뒀다.
        /// 그보다 가까우면 인식기 입장에서 사실상 같은 도형이다.
        /// </summary>
        private static float CollideDistance { get { return PointCloudRecognizer.RejectDistance; } }

        private void Commit()
        {
            // **여기서 한 장 쓴다.** 들어올 때가 아니라, 세 번을 다 그려서 실제로
            // 도형이 바뀌는 순간이다. 들어갈 때 쓰면 구경만 하고 나와도 날아가고,
            // 그리다 마음에 안 들어 그만두는 것까지 벌하게 된다.
            // **각인권을 쓰기 전에 검사한다.** 거절할 것에 값을 받으면 안 된다.
            RuneType clash;
            if (CollidesWithOtherRune(out clash))
            {
                _pending.Clear();
                LastCommit = CommitResult.TooSimilar;
                Collided = clash;
                AudioManager.Play(Sfx.UiDenied, 0.8f, 0.8f);
                Raise();
                return;
            }

            if (!RuneCast.Meta.PlayerData.SpendInscribeTicket())
            {
                // 모은 것은 비운다. 안 그러면 다음 획이 네 번째로 들어가
                // 곧바로 다시 여기로 온다.
                _pending.Clear();
                LastCommit = CommitResult.NoTicket;
                AudioManager.Play(Sfx.UiDenied, 0.7f);
                Raise();
                return;
            }

            LastCommit = CommitResult.Ok;

            string stamp = DateTime.Now.ToString("HHmmss");
            for (int i = 0; i < _pending.Count; i++)
                CustomRuneStore.Append(Target, _pending[i], Target + "_" + stamp + "_" + i);

            _pending.Clear();
            RuneCast.Meta.Achievements.OnInscribed();
            AudioManager.Play(Sfx.RuneLearned);
        }

        /// <summary>
        /// 모아둔 도형이 **다른 룬**의 템플릿과 너무 닮았는지 본다.
        ///
        /// 같은 룬의 템플릿과 닮은 건 괜찮다 — 오히려 그게 정상이다.
        /// 문제는 남의 자리를 침범할 때다.
        /// </summary>
        private bool CollidesWithOtherRune(out RuneType clash)
        {
            clash = RuneType.None;

            var templates = RuneTemplateLibrary.Templates;

            for (int i = 0; i < _pending.Count; i++)
            {
                for (int t = 0; t < templates.Count; t++)
                {
                    RuneTemplate tpl = templates[t];
                    if (tpl.Type == Target) continue;

                    if (PointCloudRecognizer.Distance(_pending[i], tpl.Points) >= CollideDistance)
                        continue;

                    clash = tpl.Type;
                    return true;
                }
            }
            return false;
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
