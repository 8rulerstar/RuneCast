using System.Collections.Generic;
using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 소리를 낼 사건 목록. 이름은 "무엇이 일어났나"이지 파일명이 아니다 —
    /// 나중에 다른 팩으로 갈아끼울 때 이 목록은 그대로 두고 매핑만 바꾼다.
    /// </summary>
    public enum Sfx
    {
        // 시전
        CastArrow, CastHeal, CastShield, CastMeteor, CastChain, CastVortex,
        CastEmpower, CastRevive, CastPyre,
        // 명중·피격
        HitArrow, HitMeteor, HitChain, HitMelee, HitHero, ShieldAbsorb,
        // 판정 피드백
        GradePerfect, GradeGreat, FailShape, FailMana, DrawBegin,
        // 전투 흐름
        DeathHero, DeathEnemy, WaveClear, Defeat, RuneLearned,
        // UI
        UiClick, UiDenied, StarGain,
    }

    /// <summary>
    /// 오디오. Resources에서 이름으로 불러오므로 인스펙터 연결이 필요 없다.
    ///
    /// **소리마다 변형을 여러 개 두고 매번 무작위로 고른다.** 같은 파일이 반복되면
    /// 몇 초 만에 귀에 거슬리기 시작하는데, 이 게임은 타격이 초당 여러 번 나므로
    /// 변형이 없으면 전투가 기계음처럼 들린다. 피치도 조금씩 흔든다.
    ///
    /// 에셋 출처:
    ///  - BGM: Abstraction "Music Loop Bundle" — CC-0
    ///  - SFX: Helton Yan's Pixel Combat (원본 96kHz/24bit → 모노 44.1kHz/16bit로 변환해 반입)
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float sfxVolume = 0.65f;
        [Range(0f, 1f)] public float bgmVolume = 0.3f;

        /// <summary>어떤 화면의 음악인가.</summary>
        public enum BgmGroup { Menu, Battle }

        /// <summary>
        /// 메뉴 곡. 타이틀·스테이지 선택·대장장이에서 돈다.
        ///
        /// **전투와 같은 곡을 쓰면 화면이 바뀐 게 귀로 안 읽힌다.** 배경을 갈라 놓고도
        /// 음악이 그대로면 여전히 한 화면 위에 덮인 것처럼 느껴진다.
        /// 고르는 화면이라 잔잔한 것들로 골랐다 — 여기서 몰아붙일 이유가 없다.
        /// </summary>
        private static readonly string[] MenuBgm =
        {
            "bgm_menu", "bgm_cloak1", "bgm_cloak2", "bgm_cloak3",
        };

        /// <summary>전투 곡. 판이 시작될 때 이 중에서 하나를 고른다.</summary>
        private static readonly string[] BattleBgm =
        {
            "bgm_wave1", "bgm_wave2", "bgm_wave3", "bgm_wave4",
            "bgm_wave5", "bgm_wave6", "bgm_wave7", "bgm_wave8",
        };

        /// <summary>Sfx 이름 → 파일 접두사. 파일은 `접두사_1.wav` … `_6.wav`.</summary>
        private static string FileOf(Sfx s)
        {
            switch (s)
            {
                case Sfx.CastArrow: return "cast_arrow";
                case Sfx.CastHeal: return "cast_heal";
                case Sfx.CastShield: return "cast_shield";
                case Sfx.CastMeteor: return "cast_meteor";
                case Sfx.CastChain: return "cast_chain";
                case Sfx.CastVortex: return "cast_vortex";

                // 새 룬 셋은 전용 클립이 없다. 파일을 늘리는 대신 성격이 가장 가까운
                // 클립을 빌려 쓰고, 호출부에서 피치를 크게 벌려 다른 사건으로 들리게 한다
                // (고양 ×1.35 / 소생 ×0.78 / 봉화 ×1.15).
                // 같은 소리를 같은 높이로 두 룬에 쓰면 그때부터 피드백이 아니라 소음이 된다.
                case Sfx.CastEmpower: return "cast_shield";
                case Sfx.CastRevive: return "cast_heal";
                case Sfx.CastPyre: return "cast_meteor";

                // UI. 전용 클립이 없어서 성격이 가까운 것을 피치로 벌려 쓴다.
                // 누르는 소리는 짧고 가벼워야 해서 그리기 시작음을 높여서 쓴다.
                case Sfx.UiClick: return "draw_begin";
                case Sfx.UiDenied: return "fail_mana";
                case Sfx.StarGain: return "grade_great";
                case Sfx.HitArrow: return "hit_arrow";
                case Sfx.HitMeteor: return "hit_meteor";
                case Sfx.HitChain: return "hit_chain";
                case Sfx.HitMelee: return "hit_melee";
                case Sfx.HitHero: return "hit_hero";
                case Sfx.ShieldAbsorb: return "shield_absorb";
                case Sfx.GradePerfect: return "grade_perfect";
                case Sfx.GradeGreat: return "grade_great";
                case Sfx.FailShape: return "fail_shape";
                case Sfx.FailMana: return "fail_mana";
                case Sfx.DrawBegin: return "draw_begin";
                case Sfx.DeathHero: return "death_hero";
                case Sfx.DeathEnemy: return "death_enemy";
                case Sfx.WaveClear: return "wave_clear";
                case Sfx.Defeat: return "defeat";
                default: return "rune_learned";
            }
        }

        /// <summary>
        /// 같은 소리가 몰릴 때의 최소 간격(초). 근접 공격처럼 여러 유닛이 동시에
        /// 때리는 소리는 짧게 잡아야 하고, 판정음처럼 하나만 들려야 하는 건 길게 잡는다.
        /// </summary>
        private static float GuardOf(Sfx s)
        {
            switch (s)
            {
                case Sfx.HitMelee: return 0.05f;
                case Sfx.HitArrow:
                case Sfx.HitChain:
                case Sfx.DeathEnemy: return 0.04f;
                case Sfx.ShieldAbsorb: return 0.07f;
                case Sfx.GradePerfect:
                case Sfx.GradeGreat: return 0.25f;

                // 버튼은 연타할 수 있어야 한다. 간격을 길게 잡으면 두 번째 클릭이
                // 소리 없이 먹혀서 안 눌린 것처럼 느껴진다.
                case Sfx.UiClick: return 0.03f;

                // 별은 0.35초 간격으로 하나씩 뜬다. 그보다 짧게 잡아야 세 번 다 들린다.
                case Sfx.StarGain: return 0.05f;
                default: return 0.05f;
            }
        }

        private readonly Dictionary<Sfx, List<AudioClip>> _sfx = new Dictionary<Sfx, List<AudioClip>>();
        private readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();
        private readonly List<AudioClip> _bgm = new List<AudioClip>();
        private readonly List<AudioClip> _menuBgm = new List<AudioClip>();
        private readonly List<AudioClip> _battleBgm = new List<AudioClip>();

        private AudioSource _bgmSource;
        private AudioSource[] _sfxSources;
        private int _sfxCursor;
        private int _bgmIndex = -1;

        public string CurrentBgmName
        {
            get
            {
                if (_bgmIndex < 0 || _bgmIndex >= _bgm.Count || _bgm[_bgmIndex] == null) return "(없음)";
                return _bgm[_bgmIndex].name;
            }
        }

        private void Awake()
        {
            Instance = this;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = bgmVolume;

            // 소스를 여러 개 돌려 쓴다. PlayOneShot 하나로 몰면 피치를 바꿀 수 없다.
            _sfxSources = new AudioSource[6];
            for (int i = 0; i < _sfxSources.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _sfxSources[i] = src;
            }

            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx))) LoadVariants(s);

            for (int i = 0; i < MenuBgm.Length + BattleBgm.Length; i++)
            {
                bool menu = i < MenuBgm.Length;
                string name = menu ? MenuBgm[i] : BattleBgm[i - MenuBgm.Length];

                var clip = Resources.Load<AudioClip>("Audio/BGM/" + name);
                if (clip != null)
                {
                    _bgm.Add(clip);
                    (menu ? _menuBgm : _battleBgm).Add(clip);
                }
            }
        }

        private void Start()
        {
            PlayRandomBgm();
        }

        private void Update()
        {
            UpdateFade();

            if (Hotkeys.Down(Hotkey.M)) NextBgm();
        }

        private void LoadVariants(Sfx s)
        {
            string file = FileOf(s);
            var list = new List<AudioClip>();
            for (int i = 1; i <= 6; i++)
            {
                var clip = Resources.Load<AudioClip>("Audio/SFX/" + file + "_" + i);
                if (clip == null) break;
                list.Add(clip);
            }
            if (list.Count > 0) _sfx[s] = list;
        }

        public static void Play(Sfx key, float volumeScale = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.PlayInternal(key, volumeScale, pitch);
        }

        private void PlayInternal(Sfx key, float volumeScale, float pitch)
        {
            List<AudioClip> variants;
            if (!_sfx.TryGetValue(key, out variants) || variants.Count == 0) return;

            float last;
            if (_lastPlayed.TryGetValue(key, out last) && Time.unscaledTime - last < GuardOf(key)) return;
            _lastPlayed[key] = Time.unscaledTime;

            var src = _sfxSources[_sfxCursor];
            _sfxCursor = (_sfxCursor + 1) % _sfxSources.Length;

            src.clip = variants[Random.Range(0, variants.Count)];
            src.volume = sfxVolume * volumeScale;

            // 미세한 피치 흔들림. 변형이 있어도 이게 없으면 반복이 티가 난다.
            src.pitch = pitch * Random.Range(0.94f, 1.06f);
            src.Play();
        }

        /// <summary>설정에서 음량을 바꿨을 때. 재생 중인 곡에 바로 반영한다.</summary>
        public void RefreshBgmVolume()
        {
            // 페이드 중이면 건드리지 않는다 — 안 그러면 넘어가는 도중에 소리가 튄다
            if (_bgmSource != null && _pendingClip == null && _fade <= 0f)
                _bgmSource.volume = bgmVolume;
        }

        public void PlayRandomBgm()
        {
            if (_bgm.Count == 0) return;
            PlayBgmAt(Random.Range(0, _bgm.Count));
        }

        private BgmGroup _group = BgmGroup.Menu;
        private bool _groupSet;

        /// <summary>
        /// 화면이 바뀌면 음악도 바꾼다.
        ///
        /// **같은 무리 안에서는 곡을 안 갈아끼운다.** 스테이지 선택 → 대장장이 →
        /// 설정을 오갈 때마다 곡이 처음부터 다시 시작하면, 화면을 옮길 때마다
        /// 같은 도입부만 반복해서 듣게 된다.
        /// </summary>
        public static void SetGroup(BgmGroup group)
        {
            if (Instance != null) Instance.SetGroupInternal(group);
        }

        private void SetGroupInternal(BgmGroup group)
        {
            if (_groupSet && _group == group) return;

            _group = group;
            _groupSet = true;

            var list = group == BgmGroup.Menu ? _menuBgm : _battleBgm;
            if (list.Count == 0) return;

            // 전투는 판마다 다른 곡이 나오는 편이 낫다. 같은 판을 여러 번 다시 할 때
            // 매번 같은 곡이면 재도전이 더 지겹게 느껴진다.
            AudioClip next = list[Random.Range(0, list.Count)];
            if (next == _bgmSource.clip && list.Count > 1)
                next = list[(list.IndexOf(next) + 1) % list.Count];

            _pendingClip = next;
            _fade = FadeTime;
        }

        // 곡을 툭 끊고 바꾸면 화면 전환보다 소리가 먼저 튄다. 짧게 겹쳐 넘긴다.
        private const float FadeTime = 0.45f;
        private AudioClip _pendingClip;
        private float _fade;

        private void UpdateFade()
        {
            if (_pendingClip == null && _fade <= 0f) return;

            _fade -= Time.unscaledDeltaTime;

            if (_pendingClip != null)
            {
                // 앞 절반은 줄이고, 다 줄면 갈아끼운다
                float k = Mathf.Clamp01(_fade / FadeTime);
                _bgmSource.volume = bgmVolume * k;

                if (_fade > 0f) return;

                _bgmSource.clip = _pendingClip;
                _bgmIndex = _bgm.IndexOf(_pendingClip);
                _pendingClip = null;
                _fade = FadeTime;
                _bgmSource.volume = 0f;
                _bgmSource.Play();
                return;
            }

            // 뒤 절반은 키운다
            float up = 1f - Mathf.Clamp01(_fade / FadeTime);
            _bgmSource.volume = bgmVolume * up;
            if (_fade <= 0f) _bgmSource.volume = bgmVolume;
        }

        /// <summary>개발용 곡 넘기기(M). **지금 무리 안에서만** 넘긴다.</summary>
        public void NextBgm()
        {
            var list = _group == BgmGroup.Menu ? _menuBgm : _battleBgm;
            if (list.Count == 0) return;

            int i = list.IndexOf(_bgmSource.clip);
            AudioClip next = list[(i + 1) % list.Count];

            _bgmSource.clip = next;
            _bgmIndex = _bgm.IndexOf(next);
            _bgmSource.volume = bgmVolume;
            _bgmSource.Play();
        }

        private void PlayBgmAt(int index)
        {
            _bgmIndex = index;
            _bgmSource.clip = _bgm[index];
            _bgmSource.volume = bgmVolume;
            _bgmSource.Play();
        }
    }
}
