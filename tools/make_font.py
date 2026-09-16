# -*- coding: utf-8 -*-
"""
Galmuri11을 **고정된 글자 집합**으로 잘라낸다.

**왜 자르나:** 완성형 11,172자를 다 담으면 TTF가 5.1MB다. 오디오를 33MB에서
줄여놓고 폰트로 5MB를 도로 얹는 건 앞뒤가 안 맞는다.

**왜 "쓰는 글자만"이 아니라 고정 집합인가 — 이게 이 파일에서 제일 중요하다.**

처음에는 소스에 박힌 문자열을 훑어서 실제로 쓰는 글자만 남겼다. 580자, 72KB.
작고 정확해 보였지만, 그 방식은 **글을 한 줄 고칠 때마다 폰트의 글리프 번호를
전부 밀어버린다.** "싹쓸이"를 추가해서 583자가 584자가 되면, 그 글자보다 뒤에
오는 모든 글자의 번호가 한 칸씩 밀린다.

그리고 화면에 엉뚱한 글자가 떴다 — "이어하기"가 "이어호기"로, "있습니다"가
"족습니다"로. 폰트를 아무리 확인해도 멀쩡했다. 확인해 보니 치환된 글자는
**같은 폰트 안에서 번호가 조금 뒤에 있는 글자**였고, 옛 폰트일수록 더 뒤로
밀렸다(있→잉→작→잠→잿→존→주→죽…). 어딘가 번호를 옛 폰트 기준으로 들고
있었다는 뜻이다.

원인이 무엇이든 — 아틀라스든, 에디터의 임포트 캐시든, 예전에 만든 빌드든 —
**번호가 안 변하면 어긋날 수가 없다.** 그래서 KS X 1001 상용 한글 2,350자를
통째로 넣는다. 329KB. 글을 아무리 고쳐도 폰트 파일은 바이트 단위로 동일하다.

덤으로 "글을 고치면 이걸 다시 돌려야 한다"는 함정도 없어졌다. 한글이면 그냥 된다.

**GSUB/GPOS도 버린다.** 원본에는 vert(세로쓰기)·tnum·zero 치환 규칙이 있는데,
가로쓰기 게임에는 쓸모가 없고 숫자 모양만 바꿔놓을 수 있다.

  python tools/make_font.py

원본: Galmuri (Lee Minseo) — SIL Open Font License 1.1
      https://github.com/quiple/galmuri
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(ROOT, "RuneCast", "Assets", "Scripts")
OUT_DIR = os.path.join(ROOT, "RuneCast", "Assets", "Resources", "Fonts")
CHARSET_CS = os.path.join(SCRIPTS, "Core", "FontCharset.cs")

# 72px로 그리는 글자. 뽑기 카드 가운데의 룬 도형뿐이라 따로 둔다 —
# 이걸 전체 글자와 같이 취급하면 아틀라스가 몇 배로 뛴다.
BIG = u"\u25cb\uff1e\u25b3\u2606\uff3a\u25ce\uff0f\u221e\u2661\u00b6\u00b7\u25c8"

def ksx1001():
    """
    KS X 1001 상용 한글 2,350자.

    한국어 문서에 쓰이는 음절은 사실상 이 안에 다 있다. 완성형 11,172자를
    다 넣으면 5.1MB지만 이만큼이면 329KB다.

    euc-kr 코드 영역(0xB0A1..0xC8FE)을 그대로 디코드해서 얻는다 —
    목록을 손으로 적으면 그 목록이 또 하나의 틀릴 거리가 된다.
    """
    out = []
    for hi in range(0xB0, 0xC9):
        for lo in range(0xA1, 0xFF):
            try:
                out.append(bytes([hi, lo]).decode("euc-kr"))
            except Exception:
                pass
    return out


# 한글 밖에서 반드시 있어야 하는 것들
ALWAYS = (
    # 아스키 전체 (숫자·영문·기호, 파일 경로와 BGM 이름이 여기 들어간다)
    "".join(chr(c) for c in range(0x20, 0x7F)) +
    # 화면에 쓰는 도형 글자와 기호
    "○＞△☆Ｚ◎／∞♡¶·★☆◈♪※←→↑↓↔" +
    # ¶ 는 봉화(깃발) 룬의 표시 글자다. 원래 룬 문자 ᛝ(U+16DD)를 썼는데
    # 한글 폰트에 그 글자가 없어 화면에 네모로 떴다.

    # 문장부호
    "…—–·「」『』〈〉·　" +
    # 자주 쓰는 단위·기호
    "％×÷±≤≥"
)


def source_chars():
    """
    모든 .cs의 문자열 리터럴에서 글자를 모은다.

    **\\uXXXX 이스케이프를 반드시 풀어야 한다.** 소스에 그대로 적힌 글자만 세면
    "\\u2726" 처럼 escape로 쓴 문자는 백슬래시·u·숫자로만 보여서 검사를 통과하고,
    정작 화면에는 폰트에 없는 글자가 네모로 뜬다. 실제로 그렇게 한 번 새어 나갔다
    (뽑기 카드의 "전체 적용" 표시가 Galmuri에 없는 글자였다).
    """
    chars = set()
    lit = re.compile(r'"((?:[^"\\]|\\.)*)"')
    esc = re.compile(r'\\u([0-9a-fA-F]{4})')

    for base, _, files in os.walk(SCRIPTS):
        for f in files:
            if not f.endswith(".cs"):
                continue
            text = io.open(os.path.join(base, f), encoding="utf-8").read()
            for m in lit.finditer(text):
                body = esc.sub(lambda e: chr(int(e.group(1), 16)), m.group(1))
                chars.update(body)
    return chars


CS_TMPL = u'''// 이 파일은 tools/make_font.py 가 만든다. 직접 고치지 말 것.
// 폰트를 다시 자르면 같이 갱신된다.

namespace RuneCast.Core
{
    /// <summary>
    /// 잘라낸 글꼴에 실제로 들어 있는 글자 전부.
    ///
    /// <see cref="FontWarmup"/>가 시작할 때 이걸 통째로 아틀라스에 굽는다.
    /// </summary>
    public static class FontCharset
    {
        /// <summary>%d자.</summary>
        public const string All =
%s

        /// <summary>가장 큰 단(72px)으로 그리는 글자. 룬 도형뿐이다.</summary>
        public const string Big = "%s";
    }
}
'''


def write_charset(chars):
    """
    문자 목록을 C# 상수로 내보낸다.

    **왜 코드로 내보내나:** 게임이 시작할 때 이 글자들을 전부 아틀라스에 미리
    구워둬야 한다(FontWarmup). 그 목록이 폰트에 실제로 들어간 목록과 어긋나면
    미리 굽는 의미가 없다 — 빠진 글자가 나중에 요청되는 순간 아틀라스가
    다시 짜이고, 그때 이미 그려둔 글자들의 자리가 어긋난다.

    그래서 목록을 손으로 관리하지 않고 **자르는 쪽에서 같이 내보낸다.**
    폰트와 목록이 어긋날 방법이 없다.
    """
    def esc(text):
        return u"".join(u"\\u%04x" % ord(c) for c in text)

    body = u"".join(sorted(chars))
    lines = [u'            "%s" +' % esc(body[i:i + 40]) for i in range(0, len(body), 40)]
    lines[-1] = lines[-1][:-2] + u";"

    io.open(CHARSET_CS, "w", encoding="utf-8", newline="\n").write(
        CS_TMPL % (len(body), u"\n".join(lines), esc(BIG)))
    print("FontCharset.cs  %d자" % len(body))


def main():
    try:
        from fontTools import subset
    except ImportError:
        print("fonttools가 필요합니다:  pip install fonttools")
        return 1

    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "tools", "Galmuri11.ttf")
    if not os.path.exists(src):
        print("원본 폰트가 없습니다:", src)
        print("https://github.com/quiple/galmuri 릴리스에서 Galmuri11.ttf를 받아 tools/에 두세요.")
        return 1

    # **폰트에 넣는 집합은 소스와 무관하다.** 소스를 고쳐도 이 집합은 그대로다.
    chars = set(ksx1001())
    chars.update(ALWAYS)
    chars.update(BIG)
    chars = {c for c in chars if ord(c) >= 0x20}

    hangul = sum(1 for c in chars if 0xAC00 <= ord(c) <= 0xD7A3)
    print("폰트에 넣는 글자 %d자 (한글 %d자) — 소스를 고쳐도 안 변한다" % (len(chars), hangul))

    # 다만 소스가 이 집합을 벗어나면 알려줘야 한다. 한자나 특수 기호를 쓰면
    # 화면에 네모로 뜨는데, 그건 여기서 잡는 게 제일 싸다.
    outside = sorted(c for c in source_chars() if ord(c) >= 0x20 and c not in chars)
    if outside:
        print("※ 집합 밖의 글자가 소스에 있습니다 — 화면에 네모로 뜹니다:")
        print("   " + " ".join("%s(U+%04X)" % (c, ord(c)) for c in outside))

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    out = os.path.join(OUT_DIR, "Galmuri11.ttf")

    args = [
        src,
        "--text=" + "".join(sorted(chars)),
        "--output-file=" + out,
        # 세로쓰기·표숫자 치환은 이 게임에 쓸모가 없고 숫자 모양만 바꿔놓을 수 있다
        "--layout-features=",
        "--drop-tables+=DSIG,GSUB,GPOS",
        "--name-IDs=*",          # 폰트 이름·라이선스 표기를 남긴다 (OFL 조건)
        "--recalc-bounds",
    ]
    subset.main(args)

    # 미리 구울 목록은 **실제로 쓰는 글자**만. 2,480자를 다섯 단으로 다 구우면
    # 아틀라스가 쓸데없이 커진다. 이 목록은 폰트 파일과 무관하므로 변해도 안전하다.
    warm = {c for c in source_chars() if ord(c) >= 0x20}
    warm.update(ALWAYS)
    warm.update(BIG)
    warm &= chars
    write_charset(warm)

    print("%s  %.0f KB  (원본 %.1f MB)" % (
        out, os.path.getsize(out) / 1024.0, os.path.getsize(src) / 1024.0 / 1024.0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
