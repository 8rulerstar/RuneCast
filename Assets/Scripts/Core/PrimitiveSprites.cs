using System.Collections.Generic;
using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 스프라이트를 런타임에 텍스처로 찍어낸다.
    ///
    /// 이미지 에셋을 안 쓰는 이유: 프로토타입 단계에서 아트 방향이 안 정해졌고,
    /// 지금 아무 에셋이나 붙이면 나중에 전부 걷어내야 한다. 코드 도형은 공짜로 지워진다.
    /// 아트가 정해지면 SpriteRenderer.sprite만 갈아끼우면 된다.
    ///
    /// 주의: Shader.Find는 에디터/플레이모드에서만 확실히 동작한다.
    /// 실제 빌드를 뽑을 땐 Project Settings > Graphics > Always Included Shaders에
    /// "Sprites/Default"를 넣어야 한다.
    /// </summary>
    public static class PrimitiveSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Material _spriteMaterial;

        /// <summary>LineRenderer 등에 물릴 기본 스프라이트 머티리얼.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (_spriteMaterial == null)
                {
                    Shader s = Shader.Find("Sprites/Default");
                    if (s == null) s = Shader.Find("Unlit/Color");
                    if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                    _spriteMaterial = new Material(s);
                }
                return _spriteMaterial;
            }
        }

        public static Sprite Square(int size = 32)
        {
            return Get("sq" + size, size, (x, y, c, r) => 1f);
        }

        public static Sprite Circle(int size = 64)
        {
            return Get("ci" + size, size, (x, y, c, r) =>
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                // 가장자리 1px 페이드 — 계단 현상 완화
                return Mathf.Clamp01(r - d);
            });
        }

        /// <summary>
        /// 가운데가 밝고 가장자리로 갈수록 사라지는 빛무리.
        ///
        /// Circle로 대신하면 가장자리가 딱 끊겨서 빛이 아니라 **색종이 스티커**로 보인다.
        /// 뽑기 카드 뒤나 강조가 필요한 곳처럼 "번져 보여야" 하는 자리에 쓴다.
        ///
        /// 제곱으로 떨어뜨리는 이유: 선형이면 가장자리가 여전히 보인다.
        /// </summary>
        public static Sprite Glow(int size = 128)
        {
            return Get("gl" + size, size, (x, y, c, r) =>
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                float k = Mathf.Clamp01(1f - d);
                return k * k;
            });
        }

        /// <summary>속이 빈 원. thickness는 반지름 대비 비율(0~1).</summary>
        public static Sprite Ring(int size = 64, float thickness = 0.18f)
        {
            string key = "ri" + size + "_" + thickness.ToString("F2");
            float inner = 1f - thickness;
            return Get(key, size, (x, y, c, r) =>
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                if (d > 1f || d < inner) return 0f;
                return Mathf.Clamp01(Mathf.Min(1f - d, d - inner) * r * 0.5f);
            });
        }

        /// <summary>
        /// 세로 그라디언트. 폭 1px짜리라 가로로 늘려 써야 한다.
        /// 이미지 배경 대신 이걸 쓰는 이유: 픽셀아트 유닛과 화풍이 싸우지 않고,
        /// 궤적(밝은 선)의 대비를 해치지 않는다.
        /// </summary>
        public static Sprite VerticalGradient(Color top, Color bottom, int height = 256)
        {
            string key = "vg" + height + "_" + ColorKey(top) + "_" + ColorKey(bottom);
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[height];
            for (int y = 0; y < height; y++)
                pixels[y] = Color.Lerp(bottom, top, y / (float)(height - 1));

            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), height);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>가장자리로 갈수록 어두워지는 비네트. 시선을 가운데로 모은다.</summary>
        public static Sprite Vignette(int size = 256, float innerRadius = 0.35f)
        {
            return Get("vi" + size + "_" + innerRadius.ToString("F2"), size, (x, y, c, r) =>
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                return Mathf.Clamp01((d - innerRadius) / (1f - innerRadius));
            });
        }

        /// <summary>
        /// 속이 빈 정삼각형(위쪽 꼭짓점). 쉴드 룬이 삼각형이라 표시도 삼각형이어야 한다 —
        /// 삼각형을 그렸는데 동그란 게 나오면 무엇이 발동했는지 연결이 끊긴다.
        /// </summary>
        public static Sprite TriangleRing(int size = 96, float thickness = 0.1f)
        {
            string key = "tri" + size + "_" + thickness.ToString("F2");
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            // 정삼각형 꼭짓점 (0~1 정규화 좌표)
            var ax = new Vector2(0.5f, 0.96f);
            var bx = new Vector2(0.04f, 0.12f);
            var cx = new Vector2(0.96f, 0.12f);
            float band = thickness;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
                    float d = Mathf.Min(
                        DistToSegment(p, ax, bx),
                        Mathf.Min(DistToSegment(p, bx, cx), DistToSegment(p, cx, ax)));

                    float a = Mathf.Clamp01((band - d) / Mathf.Max(band * 0.5f, 1e-4f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        private static string ColorKey(Color c)
        {
            return ((int)(c.r * 255)) + "-" + ((int)(c.g * 255)) + "-" + ((int)(c.b * 255));
        }

        private delegate float PixelFunc(float x, float y, float center, float radius);

        private static Sprite Get(string key, int size, PixelFunc f)
        {
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float c = (size - 1) * 0.5f;
            float r = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(f(x, y, c, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            // pixelsPerUnit = size → 스프라이트 한 장이 정확히 월드 1유닛
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>스프라이트를 붙인 GameObject를 하나 만든다.</summary>
        public static SpriteRenderer Spawn(string name, Sprite sprite, Color color, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
