using UnityEngine;

namespace RuneCast.Core
{
    public enum Vfx
    {
        HealWave,        // 힐 — 퍼지는 물결
        ShieldCharge,    // 보호막 — 모여드는 기운
        ArrowImpact,     // 화살 명중
        SlashStreak,     // 가르기 — 섬광 줄기
        ChainRing,       // 연쇄 번개 — 전기 고리
        MeteorExplosion, // 별똥별 착탄
        StarBurst,       // 완벽 등급의 추가 폭발
        VortexSwirl,     // 소용돌이 — 회전 (반복 재생)
        SmallHit,        // 잡타격

        // **새 룬 셋이 남의 이펙트를 빌려 쓰고 있었다.** 고양은 보호막 것,
        // 부활은 회복 것, 봉화는 별똥별 것. 아홉 룬 중 셋이 다른 룬처럼 보였고,
        // 그러면 무엇이 나갔는지 화면으로 구분이 안 된다.
        //
        // 셋 다 이미 쓰던 brackeys 팩에서 골랐다 — 화풍이 같아야 섞이지 않는다.
        EmpowerWave,     // 고양 — 보라 화염. 회복(파란 물결)과 같은 모양에 색만 다르다
        ReviveBurst,     // 부활 — 밝은 방사 폭발. 회복과 확실히 갈린다
        FireRing,        // 봉화 — 불의 고리. 원형 불바다라는 모양 그대로다
    }

    /// <summary>
    /// 이펙트 시트 카탈로그.
    ///
    /// 에셋: brackeys_vfx_bundle / predrawn (CC0).
    /// 파일명 뒤의 `_6x5`가 격자 크기라 그대로 파싱하지 않고 여기에 명시해 둔다 —
    /// 파일명을 바꿨을 때 조용히 깨지는 것보다 한 곳에서 틀리는 편이 낫다.
    /// </summary>
    public static class VfxLibrary
    {
        private const string Dir = "Sprites/VFX/";

        public static Sprite[] Frames(Vfx v)
        {
            switch (v)
            {
                case Vfx.HealWave: return SpriteSheet.LoadGrid(Dir + "wavy_blue_6x5", 6, 5);
                case Vfx.ShieldCharge: return SpriteSheet.LoadGrid(Dir + "charge_7x6", 7, 6);
                // **파일명이 틀려 있었다** (`_6x4`). 1505 ÷ 4 = 376.25로 나뉘지 않아
                // 행마다 조금씩 밀렸다. 아래 star_explosion과 같은 사고다.
                case Vfx.ArrowImpact: return SpriteSheet.LoadGrid(Dir + "impact_white_6x5", 6, 5);
                case Vfx.SlashStreak: return SpriteSheet.LoadGrid(Dir + "lightstreaks_6x5", 6, 5);
                case Vfx.ChainRing: return SpriteSheet.LoadGrid(Dir + "electric_ring_6x5", 6, 5);
                case Vfx.MeteorExplosion: return SpriteSheet.LoadGrid(Dir + "explosion_6x5", 6, 5);
                // **이 시트는 7×6이다. 파일명과 코드가 둘 다 6×5로 틀려 있었다.**
                //
                // 840÷6 = 140인데 진짜 프레임 폭은 120이다. 프레임을 하나 넘길
                // 때마다 창이 20px씩 어긋나므로, 폭발이 제자리에서 터지지 않고
                // **오른쪽에서 왼쪽으로 흘러갔다.** 클리어 연출·완벽 등급·싹쓸이가
                // 전부 이걸 쓴다.
                //
                // 654÷5 = 130.8로 나뉘지 않는 것이 표식이었다. 다른 시트 열 개는
                // 전부 정수로 나뉘고 경계선에 내용이 없다 — 격자를 의심할 때는
                // **나누어떨어지는지부터** 보면 된다.
                case Vfx.StarBurst: return SpriteSheet.LoadGrid(Dir + "star_explosion_7x6", 7, 6);
                case Vfx.VortexSwirl: return SpriteSheet.LoadGrid(Dir + "vortex_6x5", 6, 5);
                case Vfx.EmpowerWave: return SpriteSheet.LoadGrid(Dir + "wavy_purple_6x5", 6, 5);
                case Vfx.ReviveBurst: return SpriteSheet.LoadGrid(Dir + "big_hit_6x5", 6, 5);
                case Vfx.FireRing: return SpriteSheet.LoadGrid(Dir + "fire_ring_6x5", 6, 5);

                // **처음엔 switch에 없어서 default(fire_point)로 떨어졌다** —
                // 적이 죽을 때마다 불이 붙었다. 그다음 impact_white로 바꿨는데
                // 그건 가느다란 흰 반짝임이라 "죽었다"가 아니라 "뭔가 반짝했다"로
                // 읽혔다. 작게 쓰는 폭발이 맞다 — 별똥별 착탄과 같은 그림이지만
                // 크기가 열 배 넘게 차이 나서 헷갈리지 않는다.
                case Vfx.SmallHit: return SpriteSheet.LoadGrid(Dir + "explosion_6x5", 6, 5);
                default: return SpriteSheet.LoadGrid(Dir + "fire_point_6x5", 6, 5);
            }
        }

        /// <summary>
        /// 시트마다 프레임 수가 같아도(30장) 어울리는 속도는 다르다.
        /// 타격은 빠르게 터져야 하고, 소용돌이는 느리게 돌아야 빨려드는 것처럼 보인다.
        /// </summary>
        public static float Fps(Vfx v)
        {
            switch (v)
            {
                case Vfx.ArrowImpact: return 48f;
                case Vfx.SmallHit: return 44f;
                case Vfx.SlashStreak: return 46f;
                case Vfx.ChainRing: return 40f;
                case Vfx.MeteorExplosion: return 34f;
                case Vfx.StarBurst: return 36f;
                case Vfx.HealWave: return 30f;
                case Vfx.ShieldCharge: return 32f;
                case Vfx.VortexSwirl: return 26f;
                case Vfx.EmpowerWave: return 30f;   // 회복 물결과 같은 속도 — 같은 계열로 읽히게
                case Vfx.ReviveBurst: return 40f;
                case Vfx.FireRing: return 24f;      // 느리게 — 잠깐 터지는 게 아니라 깔려 있는 것이다
                default: return 36f;
            }
        }
    }
}
