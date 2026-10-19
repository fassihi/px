# Level 01, "The Gate Halls": plan

A 20-minute playable draft of PX built from Soheil's concept drafts (`../art/concept drafts`): the teal-and-orange
gate halls over dark water, the scaffold bazaar, the green-and-magenta face shrine, the tall-hatted sabre soldiers,
the crested brute, the flying craft. Decided with Amir on 2026-10-19 (see the Notion Decision Log).

## Decisions

- **Weapon:** a sabre, with her kicks kept as finishers and the sweep. The blade is a simple mesh seated in her right
  hand. Mixamo one-handed sword clips for the slashes; the kick clips stay.
- **Ability (Metroidvania):** the Tether, a ribbon she throws at glowing hanging lamps to pull herself up or across.
  Found mid-level in the Gate chamber. Upper routes and the way into Act 2 need it; an alcove near the start holds a
  reward for coming back with it.
- **Death loop (Souls-like):** three healing flasks, refilled only at shrines. Enemies killed give light; on death
  she drops it where she fell and can recover it once. Light buys a flask or health at a shrine. Resting at a shrine
  brings the regular enemies back.
- **Stealth:** guards patrol with a facing and a sight cone; crouching behind low cover hides her. An unaware guard
  taken from behind dies in one takedown. Fighting noise alerts guards nearby. States: unaware, suspicious, alert.
- **Boss:** the Captain, a scaled-up Enemy A with heavy swings, a charge, a slam and two phases, in front of the face.
- **Floating enemies:** Sentinels, ornate hovering craft with temp art, firing slow bolts she can dodge, block or
  parry back at them.
- **Camera:** varied but restrained angles per zone; a cinematic cut for finishers (takedowns, last kills, the boss)
  and a reveal when entering a new location.
- **Layout:** one rail that winds and climbs, layered: an upper route over the lower one, tether-gated, with shortcut
  doors back to shrines. Two acts, two palettes.
- **Process:** editor open, Unity MCP for compiling, rebuilding and tests; Mixamo clips downloaded as needed.

## The level

Rail distances are approximate and will be tuned in the builder.

| m | Zone | What happens |
| --- | --- | --- |
| 0–60 | **Threshold** | She wakes at Shrine 1. Run, jump, a gap, a ledge. The hall opens: reveal of the far gate. One guard facing away: the takedown is taught. A high alcove is visible and out of reach (tether reward). |
| 60–140 | **Flooded Court** | Broken platforms over dark water (water is death). Two sentinels over the water. Shrine 2. |
| 140–230 | **Guardhouse** | Stealth: three patrolling guards among low cover; a sealed door opened by a lever at the far end. Alerted guards converge. Shrine 3. |
| 230–300 | **The Gate** | The mandala gate chamber. The Tether lies on a plinth (cinematic). Lamps lead up to a gallery that runs back over the Guardhouse to a shortcut door above Shrine 1 and the alcove reward. Forward, lamps cross a chasm into Act 2. |
| 300–420 | **Scaffold Ascent** | Act 2, green and magenta. Climbing scaffolds with ledges and lamps; sentinels; a knight on a platform. Shrine 4. |
| 420–540 | **Bazaar Walk** | Hanging walkways under neon signs; a two-guard patrol with cover; a sentinel; a shortcut door back down to Shrine 4. Shrine 5. |
| 540–600 | **Face Shrine** | The Captain in front of the giant face. Gates close for the fight. Two phases. Finisher cinematic; the face's eyes light; end card. |

## Systems, in build order

1. **Builder kit.** Shared helpers pulled out of `GrayboxBuilder` (`BuilderKit`), a new `LevelBuilder` writing
   `Scenes/Level01.unity` (menu PX > Level 01 > Rebuild Scene). Graybox stays the test scene and keeps its tests.
2. **Environment kit and mood.** Procedural modules from primitives with generated pattern textures: pointed arches,
   pillars with gold bands, the mandala gate, hanging lamps, banners, platforms with glowing inlay, water, scaffolds,
   neon signs, the face. Dark ambient, fog, emissive accents, bloom, vignette. Two palettes.
3. **Souls layer.** Interact input; shrines (rest: checkpoint, refill, enemies return); flasks; light, dropped on
   death and recoverable; a shrine menu to spend it; water as a kill plane; HUD for flasks, light, prompts, area
   names.
4. **Stealth.** Enemy awareness (unaware, suspicious, alert), patrol routes, sight cones blocked by cover when she
   crouches, noise from fighting, takedowns from behind with a cinematic, awareness markers over heads.
5. **Sabre and combat.** Blade mesh seated in her hand; sword slash clips for the combo with kicks kept; a dash attack;
   enemy stagger that opens an execution (cinematic finisher).
6. **Cinematic camera.** `CameraDirector`: timed view overrides with letterbox for finishers and location reveals.
7. **Tether.** Pickup, anchors (lamps), the pull with a ribbon, air-jump reset at the anchor, gated geometry.
8. **Sentinels.** Hovering craft: patrol, face her, fire bolts; bolts can be blocked, dodged or parried back.
9. **The Captain.** Boss config and moves (heavy combo, charge, slam), two phases, arena gates, boss bar.
10. **Level design pass.** The whole layout with encounters, camera zones, reveals and shortcuts; tuned toward
    20 minutes.
11. **Tests and docs.** Play mode tests on Level01 for each system; README and CLAUDE.md updated; Notion decision log.
