# -*- coding: utf-8 -*-
"""
에디터를 안 켜고 컴파일이 되는지 확인한다.

**왜 필요하나:** 에디터를 열어 컴파일이 끝나기를 기다리는 데 1~2분이 걸린다.
고치는 중에는 그 왕복이 작업 속도를 정한다. Unity가 번들로 가진 Roslyn을
직접 부르면 몇 초에 끝난다.

**플랫폼별로 따로 돌리는 이유:** `#if UNITY_ANDROID` 안의 코드는 에디터에서
컴파일되지 않는다. 에디터에서 멀쩡하던 게 빌드에서 터지는 게 이래서다.
여기서는 에디터·안드로이드·iOS를 다 돈다.

  python tools/check_compile.py
  python tools/check_compile.py --warn 4     경고도 다 본다
"""
import io
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJ = os.path.join(ROOT, "RuneCast")
SCRIPTS = os.path.join(PROJ, "Assets", "Scripts")

# 설치 위치와 내부 구조가 OS마다 다르다. 맥에서는 에디터가 .app 번들이고
# 윈도우의 `Editor/Data`에 해당하는 것이 `Unity.app/Contents/Resources/Scripting`이다.
# 이 도구가 참조하는 네 폴더(Managed·NetStandard·DotNetSdkRoslyn·NetCoreRuntime)는
# 양쪽 다 그 아래에 같은 이름으로 있어서, 그 경로만 갈아 끼우면 나머지는 그대로 돈다.
MAC = sys.platform == "darwin"
HUB = "/Applications/Unity/Hub/Editor" if MAC else r"C:\Program Files\Unity\Hub\Editor"
DATA = ("Unity.app", "Contents", "Resources", "Scripting") if MAC else ("Editor", "Data")
DOTNET = "dotnet" if MAC else "dotnet.exe"

# (이름, 추가 define). 공통 define은 아래에서 붙인다.
TARGETS = [
    (u"에디터", ["UNITY_EDITOR", "UNITY_EDITOR_WIN", "UNITY_STANDALONE_WIN"]),
    (u"안드로이드", ["UNITY_ANDROID"]),
    (u"iOS", ["UNITY_IOS"]),
]

COMMON = ["UNITY_6000_0_OR_NEWER", "ENABLE_INPUT_SYSTEM", "UNITY_2021_1_OR_NEWER"]


def find_editor():
    """ProjectVersion.txt에 적힌 버전을 먼저 찾고, 없으면 가장 높은 걸 쓴다."""
    want = None
    pv = os.path.join(PROJ, "ProjectSettings", "ProjectVersion.txt")
    if os.path.exists(pv):
        m = re.search(r"m_EditorVersion:\s*(\S+)", io.open(pv, encoding="utf-8").read())
        if m:
            want = m.group(1)

    if not os.path.isdir(HUB):
        return None, None, u"Unity Hub 설치 폴더를 못 찾았습니다: " + HUB

    have = sorted(d for d in os.listdir(HUB) if os.path.isdir(os.path.join(HUB, d, *DATA)))
    if want and want in have:
        return os.path.join(HUB, want, *DATA), want, None
    if not have:
        return None, None, u"설치된 에디터가 없습니다."

    # 프로젝트가 원하는 버전이 없으면 알려주고 가장 높은 걸로 진행한다.
    # API가 조금 다를 수 있으니 조용히 넘어가지는 않는다.
    return os.path.join(HUB, have[-1], *DATA), have[-1], \
        u"※ %s 를 원하는데 없어서 %s 로 확인합니다." % (want, have[-1])


def sources():
    out = []
    for base, _, files in os.walk(SCRIPTS):
        for f in files:
            if f.endswith(".cs"):
                out.append(os.path.join(base, f))
    return sorted(out)


def refs(data):
    """참조할 어셈블리. 없는 건 조용히 건너뛴다 — 버전마다 구성이 다르다."""
    found = []
    managed = os.path.join(data, "Managed", "UnityEngine")
    if os.path.isdir(managed):
        for f in sorted(os.listdir(managed)):
            if f.endswith(".dll"):
                found.append(os.path.join(managed, f))

    extra = [
        os.path.join(data, "NetStandard", "ref", "2.1.0", "netstandard.dll"),
        os.path.join(PROJ, "Library", "ScriptAssemblies", "Unity.InputSystem.dll"),
    ]
    found += [p for p in extra if os.path.exists(p)]
    return found


def run(data, name, defines, warn):
    dotnet = os.path.join(data, "NetCoreRuntime", DOTNET)
    csc = os.path.join(data, "DotNetSdkRoslyn", "csc.dll")
    if not os.path.exists(dotnet) or not os.path.exists(csc):
        return None, u"Roslyn을 못 찾았습니다: " + csc

    out = os.path.join(ROOT, "obj", "check_%s.dll" % name)
    rsp = os.path.join(ROOT, "obj", "check_%s.rsp" % name)
    if not os.path.isdir(os.path.dirname(out)):
        os.makedirs(os.path.dirname(out))

    # 경로에 공백이 있으면(Program Files) 따옴표 없이는 깨진다.
    lines = [
        "-nostdlib+", "-noconfig", "-target:library", "-langversion:9.0",
        # utf8output이 없으면 한국어 오류 메시지가 시스템 코드페이지로 나와서 깨진다.
        # 오류를 읽으려고 만든 도구인데 오류가 안 읽히면 소용이 없다.
        "-nologo", "-utf8output", "-warn:%d" % warn,
        '-out:"%s"' % out,
        "-define:" + ";".join(COMMON + defines),
    ]
    lines += ['-r:"%s"' % p for p in refs(data)]
    lines += ['"%s"' % p for p in sources()]
    io.open(rsp, "w", encoding="utf-8").write(u"\n".join(lines))

    p = subprocess.Popen([dotnet, csc, "@" + rsp],
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    text = p.communicate()[0].decode("utf-8", "replace")
    return p.returncode, text


def main():
    warn = 4 if "--warn" in sys.argv else 2

    W = sys.stdout.buffer.write

    def say(s):
        W((s + u"\n").encode("utf-8"))

    data, ver, note = find_editor()
    if data is None:
        say(note)
        return 1
    if note:
        say(note)

    n = len(sources())
    say(u"%s  ·  파일 %d개" % (ver, n))
    say(u"")

    bad = 0
    for name, defines in TARGETS:
        code, text = run(data, name, defines, warn)
        if code is None:
            say(text)
            return 1

        errs = [l for l in text.splitlines() if ": error " in l]
        warns = [l for l in text.splitlines() if ": warning " in l]

        mark = u"OK  " if code == 0 else u"실패"
        say(u"  %s  %-10s  오류 %d  경고 %d" % (mark, name, len(errs), len(warns)))

        for l in errs[:25]:
            say(u"        " + l.strip())
        if warn >= 4:
            for l in warns[:25]:
                say(u"        " + l.strip())

        if code != 0:
            bad += 1

    say(u"")
    say(u"전부 통과" if bad == 0 else u"%d개 대상에서 실패" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
