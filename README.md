# RuneCast

*English · [한국어](README.ko.md)*

A real-time gesture auto-battler. Unity 6 (6000.4.2f1) · C# · solo project.

**Play it: https://8rulerstar.itch.io/runecast**

> Your heroes fight on their own. You don't fight — you draw.
> Trace a rune on the screen — a chevron, a spiral, a five-pointed star — and it
> fires into the battle. You never move your heroes and never pick their targets.
> You draw, and that is the whole of your influence.

![](screenshots/shot2_battle.png)

---

## About this repository

**Code and design documents only.** The art and audio the game ships with are
third-party assets, most of them licensed as "bundle with your game, do not
redistribute", so they are not included here. This means the repository will
**not build** — the playable game is at the itch link above.

It is meant to be read, not cloned. The code and the documents under `docs/`
are the thing worth looking at.

> Most documents under `docs/` are written in Korean — they are the working
> notes the project was actually built with, kept as they were rather than
> rewritten for display. The code and its comments are a mix of English
> identifiers and Korean rationale comments.

| Path | What |
|---|---|
| `Assets/Scripts/` | 94 C# files. No scene file — `Core/Bootstrap.cs` assembles everything in code |
| `tools/` | 11 Python checkers and battle simulators — most things can be verified without opening the editor |
| `docs/DESIGN.md` | Design document. Every number carries the `file:line` where it is defined |
| `docs/DEVLOG.md` | Development log — what was built and why |
| `docs/WORKING-NOTES.md` | Mistakes this project actually made, and what came out of them |
| `docs/CREDITS.md` | Asset sources and how each one was processed |

## Structure

```
Assets/Scripts/
├─ Core/     (22)  Bootstrap, AudioManager, BurstFx, PrimitiveSprites …
├─ Battle/   (14)  units, AI, projectiles, boss patterns, status effects
├─ Gesture/  (11)  stroke input → shape recognition → accuracy grading
├─ Runes/    (12)  the nine runes and what they do
├─ Meta/     (18)  stages, gacha, achievements, save data
└─ UI/       (17)  every screen, IMGUI
```

### Worth a look

- **`Gesture/`** — takes a hand-drawn stroke, decides which of nine shapes it is,
  and grades how precisely it was traced (GOOD / GREAT / EXCELLENT / PERFECT).
  This is the mechanism the whole game hangs on.
- **`Core/Bootstrap.cs`** — there is no scene file. Every object is assembled in
  code, so there are no merge conflicts and every change is readable as a diff.
- **`Core/PrimitiveSprites.cs`** — health bars, shield rings and the like are not
  image files; they are painted to textures at runtime.
- **`tools/sim_battle.py`, `tools/sim_player.py`** — balance is not tuned by feel.
  These run all 12 stages and print a table. Turning a mechanic off and on should
  move the numbers; when it doesn't, that mechanic isn't actually firing. Several
  bugs found exactly that way are written up in `docs/WORKING-NOTES.md`.

## Nine runes, nine shapes

| Shape | Rune | |
|---|---|---|
| Chevron `>` | **Arrow** | a volley toward the point you drew |
| Circle `○` | **Heal** | restores everyone inside it |
| Triangle `△` | **Shield** | barriers for the allies you enclosed |
| Zigzag `Z` | **Chain Lightning** | leaps between enemies, weaker each hop |
| Spiral `◎` | **Vortex** | drags them into a pile — deals no damage |
| Star `☆` | **Meteor** | telegraphed, then everything under it |
| Banner | **Empower** | allies hit harder and faster |
| Heart | **Revive** | brings a fallen hero back |
| Infinity | **Pyre** | a ring of fire that keeps burning |

**Mana** limits how *often* you step in. **Ink** limits how *big* you can draw —
a stroke that runs out of ink does not cast at all, so a huge meteor is a
decision rather than a habit.

## License

The code (`Assets/Scripts/`, `tools/`) is MIT — see [LICENSE](LICENSE).

Assets are not in this repository. Their sources and licenses are recorded in
[docs/CREDITS.md](docs/CREDITS.md). The one asset carrying an attribution
requirement — **Complete UI Essential Pack** by Crusenho Agus Hennihuno,
CC BY 4.0 — is credited on the in-game credits screen.
