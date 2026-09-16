using System.Collections.Generic;
using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 가로 스트립 스프라이트 시트를 런타임에 프레임 단위로 자른다.
    ///
    /// 에디터에서 Sprite Mode를 Multiple로 바꿔 슬라이스하지 않는 이유:
    /// 그 설정은 .png.meta에 들어가는데, 그건 diff로 의도가 안 읽히고
    /// 에셋을 다시 받으면 날아간다. 프레임 크기가 규칙적이라 코드로 자르는 편이
    /// 재현 가능하고, "왜 100인가"를 주석으로 남길 수 있다.
    /// </summary>
    public static class SpriteSheet
    {
        private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();

        /// <summary>
        /// Resources 경로의 시트를 frameSize 폭으로 잘라 반환. 실패하면 빈 배열.
        /// </summary>
        public static Sprite[] Load(string resourcePath, int frameSize = 100, float pixelsPerUnit = 100f)
        {
            Sprite[] cached;
            if (Cache.TryGetValue(resourcePath, out cached)) return cached;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                Debug.LogWarning("스프라이트 시트를 찾을 수 없음: " + resourcePath);
                Cache[resourcePath] = new Sprite[0];
                return Cache[resourcePath];
            }

            // 픽셀아트가 흐려지지 않게. 임포터 설정 대신 코드에서 강제한다.
            tex.filterMode = FilterMode.Point;

            int count = Mathf.Max(1, tex.width / frameSize);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Sprite.Create(
                    tex,
                    new Rect(i * frameSize, 0f, frameSize, tex.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                frames[i].name = resourcePath + "_" + i;
            }

            Cache[resourcePath] = frames;
            return frames;
        }

        /// <summary>
        /// 격자로 배열된 시트를 왼쪽 위부터 행 우선으로 잘라 반환.
        ///
        /// 프레임 크기를 상수로 받지 않고 **실제 텍스처 크기 ÷ 격자 수**로 계산하는 게 중요하다.
        /// Unity 임포터의 Max Size 기본값이 2048이라 큰 시트(3342px 등)는 자동으로 줄어드는데,
        /// 원본 기준 픽셀 값을 하드코딩하면 그때 프레임이 어긋난다.
        /// </summary>
        public static Sprite[] LoadGrid(string resourcePath, int columns, int rows, float pixelsPerUnit = 0f)
        {
            string key = resourcePath + "#" + columns + "x" + rows;
            Sprite[] cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null || columns <= 0 || rows <= 0)
            {
                Debug.LogWarning("VFX 시트를 찾을 수 없음: " + resourcePath);
                Cache[key] = new Sprite[0];
                return Cache[key];
            }

            tex.filterMode = FilterMode.Bilinear; // 픽셀아트가 아닌 이펙트라 보간이 낫다
            tex.wrapMode = TextureWrapMode.Clamp;

            float fw = tex.width / (float)columns;
            float fh = tex.height / (float)rows;
            if (pixelsPerUnit <= 0f) pixelsPerUnit = Mathf.Max(fw, fh); // 프레임 하나 = 월드 1유닛

            var frames = new Sprite[columns * rows];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    // 시트는 위에서 아래로 읽지만 텍스처 좌표는 아래가 0이다
                    float y = tex.height - (r + 1) * fh;
                    var rect = new Rect(c * fw, y, fw, fh);

                    var s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    s.name = key + "_" + (r * columns + c);
                    frames[r * columns + c] = s;
                }
            }

            Cache[key] = frames;
            return frames;
        }

        /// <summary>
        /// 반복 배치(Tiled)용 스프라이트. SpriteRenderer.drawMode = Tiled 는
        /// FullRect 메시가 아니면 동작하지 않으므로 명시적으로 지정한다.
        /// </summary>
        public static Sprite Tileable(string resourcePath)
        {
            string key = resourcePath + "#tile";
            Sprite[] cached;
            if (Cache.TryGetValue(key, out cached)) return cached.Length > 0 ? cached[0] : null;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                Cache[key] = new Sprite[0];
                return null;
            }

            tex.filterMode = FilterMode.Point;

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
            sprite.name = key;

            Cache[key] = new[] { sprite };
            return sprite;
        }

        /// <summary>단일 이미지 하나를 스프라이트로.</summary>
        public static Sprite Single(string resourcePath, float pixelsPerUnit = 100f, bool pointFilter = true)
        {
            Sprite[] cached;
            if (Cache.TryGetValue(resourcePath, out cached)) return cached.Length > 0 ? cached[0] : null;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                Cache[resourcePath] = new Sprite[0];
                return null;
            }

            if (pointFilter) tex.filterMode = FilterMode.Point;

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = resourcePath;

            Cache[resourcePath] = new[] { sprite };
            return sprite;
        }
    }
}
