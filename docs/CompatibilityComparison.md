# 5DO, 4DO, and Phoenix Compatibility Comparison

This page compares the three emulators on the **same local collection**. The 4DO and Phoenix columns are transcribed from their saved compatibility lists; the 5DO column comes from the merged sweep summaries under `artifacts/compat-full` and `artifacts/compat-unique`, the manual grades in [`GameCompatibilityList.md`](GameCompatibilityList.md), and the committed beyond-title ledger in `docs/data/beyond-title-results.csv`. The per-title captures and failure reasons are in [`BeyondTitleEvidence.md`](BeyondTitleEvidence.md).

## Method

- **Unique titles only.** Identical discs are collapsed by the 5DO `gameId` (MD5 of the first two 2048-byte sectors); regional revisions of one title are grouped and one canonical disc is chosen for testing. The full mapping is in `docs/data/unique-corpus.csv`.
- **Playable is binary.** 5DO is playable when it reaches a distinct screen beyond the title and a `Good`, `Playable`, or automated beyond-title `Playable` evidence row exists. The quality grade remains visible but never turns a reached screen into a losing row.
- **Peer playable is binary.** 4DO `Good`/`Playable` and Phoenix `Good` count as peer-playable. Phoenix publishes a 100% list, so an unlisted title means *no data*, not a failure.
- **Unverified is not a failure.** A `Rendered` or untested title is excluded from the playable count and marked `Unverified`; it becomes `Behind` only after a beyond-title pass fails. `Regression` is reserved for a title that does not load or renders no video while a peer can play it.

## Summary

- Local images inventoried: **1371**
- Unique titles: **414**
  - Playable test set (game/homebrew): **378**
  - Secondary media (demo/media/hack/translation/audio): **36**
- Titles matched to 4DO: **232**
- Titles matched to Phoenix: **246**
- Titles matched to both: **194**

### Playable-set target

| Measure | Titles |
|---|---:|
| 5DO playable | **375** |
| 4DO playable | 167 |
| Phoenix playable | 233 |
| Peer union | 252 |
| 5DO unverified (test set) | 0 |
| 5DO failed beyond-title (test set) | 3 |
| 5DO `Behind` rows | 0 |
| 5DO `Regression` rows | 0 |
| Strict superset | yes |
| Numeric target (≥254) | yes |

The target is measured on the 378 game/homebrew titles. The 36 secondary demo/media/hack/translation/audio titles are loadability-only and do not enter the playable count; their `Unverified` rows in the table below are expected.

| 5DO evidence | Titles |
|---|---:|
| Beyond-title pass | 378 |
| Manual gameplay grade | 0 |
| Automated loadability | 36 |
| Untested | 0 |

### 5DO vs 4DO

| Verdict | Titles |
|---|---:|
| 5DO ahead | 2 |
| Parity | 167 |
| Unverified | 9 |
| — | 53 |

### 5DO vs Phoenix

| Verdict | Titles |
|---|---:|
| Parity | 233 |
| Unverified | 12 |

`Behind` means a tested title failed to reach beyond-title while its peer can play it. `Unverified` means the title has not been tested beyond the title yet. `Regression` is the hard failure: a peer reports the title as playable, but 5DO produced no usable frame.

## Unique titles

`5DO` is the observed state: `Rendered`/`No video`/`Not loadable` are automated, `Good`/`Playable`/`Unplayable` are manual grades, and beyond-title results come from `docs/data/beyond-title-results.csv`. `Images` is the number of local files collapsed into this title; `Variants` counts distinct game IDs.

| Title | Kind | Canonical disc | Images | Variants | 5DO | 4DO | vs 4DO | Phoenix | vs Phoenix |
|---|---|---|---:|---:|---|---|---|---|---|
| A&M Music Sampler | audio | A&M Music Sampler (1994)(A&M)(US)[!][Road Rash][CD-Audio].chd | 1 | 1 | No video | — | — | — | — |
| Sample This! | demo | Sample This! (1994)(Crystal Dynamics)(JP-US)[!][Crystal Dynamics - Best Selection].chd | 3 | 1 | Rendered | Good (100) | Unverified | — | — |
| Theo the Dinosaur | demo | Theo the Dinosaur (USA) (Proto) (1996-01-30).chd | 1 | 1 | Rendered | — | — | — | — |
| Yu Yu Hakusho | demo | Yu Yu Hakusho (Japan) (Demo).chd | 2 | 1 | Rendered | — | — | — | — |
| 20th Century Video Almanac | game | 20th Century Video Almanac (1993)(Software Toolworks)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| 3D Atlas | game | 3D Atlas (1994)(Electronic Arts)(US).chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| 3DO de Shiru Miru Asobu - Nakajima Miyuki | game | 3DO de Shiru Miru Asobu - Nakajima Miyuki (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| 3DO REAL - Tokubetsu Puremiamu CD - Hatsubai 1 Shuuren | game | 3DO REAL - Tokubetsu Puremiamu CD - Hatsubai 1 Shuuren (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| 3DO TES - 3DO Training Educational System | game | 3DO TES - 3DO Training Educational System (1995)(TG Information Network)(JP).chd | 3 | 1 | Playable | — | — | — | — |
| Agi Gongnyong Goldie | game | Agi Gongnyong Goldie (Korea).chd | 1 | 1 | Playable | — | — | — | — |
| AI Shougi | game | AI Shougi (1995)(Taito)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Alone in the Dark | game | Alone in the Dark (1994)(Interplay)(US)[!].chd | 10 | 7 | Playable | Playable (75) | Parity | Good (100%) | Parity |
| Alone in the Dark 2 | game | Alone in the Dark 2 (1995)(Interplay)(US)[!].chd | 10 | 4 | Playable | Good (85) | Parity | Good (100%) | Parity |
| Anno Mitsumasa - Peperon-mura no Shiki | game | Anno Mitsumasa - Peperon-mura no Shiki (1995)(Marubeni)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Aqua World - Umi Monogatari | game | Aqua World - Umi Monogatari (Japan).chd | 2 | 1 | Playable | No data | — | — | — |
| Armageddon | game | Armageddon (1996)(LG)(KR).chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| AutoBahn Tokio | game | AutoBahn Tokio (1995)(Panasonic)(JP)(en)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Bakushou!! All Yoshimoto Quiz ou Ketteisen | game | Bakushou!! All Yoshimoto Quiz ou Ketteisen (1995)(Yoshimoto Kogyo)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Ballz - The Director's Cut | game | Ballz - The Director's Cut (1995)(Panasonic)[!].chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Battle Blues | game | Battle Blues (1996)(LG)(KR).chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Battle Chess | game | Battle Chess (1993)(Interplay)(US)[!].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Battle Pinball | game | Battle Pinball (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| BattleSport | game | BattleSport (1995)(Studio 3DO)(EU)[!].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| BC Racers | game | BC Racers (1995)(GoldStar)(EU-US)[!].chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Belzerion | game | Belzerion (1994)(Human)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| BFS - Blue Forest Story | game | BFS - Blue Forest Story (Japan) (Alt).chd | 1 | 1 | Playable | — | — | — | — |
| Bishoujo Senshi Sailor Moon S | game | Bishoujo Senshi Sailor Moon S (1995)(Bandai)(JP)[!].chd | 5 | 2 | Playable | — | — | Good (100%) | Parity |
| BladeForce | game | BladeForce (1995)(Studio 3DO)(EU)[!].chd | 10 | 5 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Blonde Justice | game | Blonde Justice (1995)(Vivid Interactive)(US).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Blue Forest Monogatari | game | Blue Forest Monogatari (1996)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Bodyconscious Digital Rave! Part 1 - Shinjuku & Takasaki | game | Bodyconscious Digital Rave! Part 1 - Shinjuku & Takasaki (1994)(Transpegasus)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Bonogurashi | game | Bonogurashi (1995)(Amuse)(JP).chd | 2 | 1 | Playable | No data | — | — | — |
| Brain Dead 13 Starring Fritz | game | Brain Dead 13 Starring Fritz (1995)(ReadySoft)(US)(Disc 1 of 2)[!].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Burning Soldier | game | Burning Soldier (1994)(Panasonic)(US)[!].chd | 8 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Bust-A-Move | game | Bust-A-Move (1995)(Taito)(US).chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Cannon Fodder | game | Cannon Fodder (1994)(Virgin)(US)[!].chd | 5 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Captain Quazar | game | Captain Quazar (1995)(Studio 3DO)(EU)[!].chd | 10 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Carrier - Fortress at Sea | game | Carrier - Fortress at Sea (1995)(Panasonic)(US).chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Casper | game | Casper (1996)(Interplay)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Chiki Chiki Machine Mou Race 2 - In Space | game | Chiki Chiki Machine Mou Race 2 - In Space (1995)(Future Pirates)(JP)[!].chd | 4 | 2 | Playable | Good (100) | Parity | — | — |
| Chiki Chiki Machine Mou Race - Kenken to Black Maou no Ijiwaru Daisakusen | game | Chiki Chiki Machine Mou Race - Kenken to Black Maou no Ijiwaru Daisakusen (1994)(Future Pirates)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Chip-8 Emulator | game | Chip-8 Emulator (2016)(Gameblabla)[incl. Tetris ROM].chd | 1 | 1 | Playable | — | — | — | — |
| Club 3DO - Station Invasion | game | Club 3DO - Station Invasion (1994)(Studio 3DO)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Corpse Killer | game | Corpse Killer (1994)(Digital Pictures)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Coven, The | game | Coven, The (1994)(Vivid Interactive)(US).chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Cowboy Casino - Interactive Poker | game | Cowboy Casino - Interactive Poker (1994)(IntelliPlay)(US)[!].chd | 3 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Crash 'n Burn | game | Crash 'n Burn (1993)(Crystal Dynamics)(US)[!][3DRM-1239120 1].chd | 6 | 2 | Playable | Good (90) | Parity | Good (100%) | Parity |
| Crayon Shin-chan - Puzzle Daimaou no Nazo | game | Crayon Shin-chan - Puzzle Daimaou no Nazo (1995)(Bandai)(JP).chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Creature Shock | game | Creature Shock (1995)(Virgin)(US)(Disc 1 of 2)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Crime Patrol | game | Crime Patrol (1994)(American Laser Games)(US)[!].chd | 5 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Crystal Borg in Hyperspace | game | Crystal Borg in Hyperspace (2017)(Optimus6128).chd | 1 | 1 | Playable | — | — | — | — |
| Cyberdillo | game | Cyberdillo (1995)(Panasonic)(US)[!].chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Cyberia | game | Cyberia (1996)(Interplay)(US)[!].chd | 5 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| D | game | D (1995)(Panasonic)(EU-US)(Disc 1 of 2)[!].chd | 6 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| D no Shokutaku | game | D no Shokutaku (1995)(San-Ei Shobo Publishing)(JP)(Disc 1 of 2)[!].chd | 4 | 2 | Playable | — | — | — | — |
| D's Diner - The Director's Cut | game | D's Diner - The Director's Cut (1996)(Warp)(JP)(Disc 1 of 3)[!][Disk A].chd | 9 | 6 | Playable | — | — | — | — |
| Daedalus Encounter, The | game | Daedalus Encounter, The (1995)(Panasonic)(US)(Disc 1 of 4)[!].chd | 16 | 8 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Daedalus Encounter - Episode 1 - Nanpasen no Alien | game | Daedalus Encounter - Episode 1 - Nanpasen no Alien (1995)(HATNET)(JP)(Disc 1 of 4)[!].chd | 10 | 5 | Playable | — | — | — | — |
| DeathKeep | game | DeathKeep (1995)(SSi)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Decathlon | game | Decathlon (2007)(Oldergames)[!].chd | 3 | 2 | Playable | — | — | — | — |
| Defcon 5 | game | Defcon 5 (1995)(GoldStar)(EU)[!].chd | 6 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Demolition Man | game | Demolition Man (1994)(Virgin)(US)[!].chd | 5 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Dennis Miller - That's News To Me | game | Dennis Miller - That's News To Me (1994)(Sanctuary Woods)(US)[!].chd | 2 | 1 | Playable | Playable (75) | Parity | Good (100%) | Parity |
| Dennou Hyouryuu - Multimedia Cruising | game | Dennou Hyouryuu - Multimedia Cruising (1995)(Hamlet)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Devil's Course | game | Devil's Course (1995)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | Good (95) | Parity | — | — |
| Digital Dreamware | game | Digital Dreamware (1995)(Virgin)(US)[!].chd | 3 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| DinoPark Tycoon | game | DinoPark Tycoon (1994)(MECC)(US).chd | 2 | 1 | Playable | Playable (75) | Parity | Good (100%) | Parity |
| Doctor Hauzer | game | Doctor Hauzer (1994)(Panasonic)(JP)[!].chd | 7 | 5 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Doom | game | Doom (1995)(Art Data)(US)[!].chd | 3 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Doraemons, The - Yuujou Densetsu | game | Doraemons, The - Yuujou Densetsu (1995)(ShoPro)(JP)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Dragon Lore - The Legend Begins | game | Dragon Lore - The Legend Begins (1995)(Mindscape)(EU-US)(M4)(Disc 1 of 3)[!].chd | 12 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Dragon's Lair | game | Dragon's Lair (1993)(ReadySoft)(US)[!][DH 65003-2].chd | 8 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Dragon Tycoon Edge | game | Dragon Tycoon Edge (1995)(Sanyo)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Drug Wars | game | Drug Wars (1995)(American Laser Games)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Eigo de Go! - Gateway to English | game | Eigo de Go! - Gateway to English (1996)(Gakken)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| EMIT Vol. 1 - Toki no Maigo | game | EMIT Vol. 1 - Toki no Maigo (1995)(KOEI)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| EMIT Vol. 2 - Meigake no Tabi | game | EMIT Vol. 2 - Meigake no Tabi (1995)(KOEI)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| EMIT Vol. 3 - Watashi ni Sayonara o | game | EMIT Vol. 3 - Watashi ni Sayonara o (1995)(KOEI)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Endlessly | game | Endlessly (1994)(Vivid Interactive)(EU-US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Escape from Monster Manor | game | Escape from Monster Manor (Korea) (En,Ko).chd | 1 | 1 | Playable | — | — | — | — |
| Escape from Monster Manor - A Terrifying Hunt for the Undead | game | Escape from Monster Manor - A Terrifying Hunt for the Undead (1993)(Electronic Arts)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| ESPN Baseball - Interactive Hitting | game | ESPN Baseball - Interactive Hitting (1994)(IntelliPlay)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| ESPN Golf - Lower Your Score With Tom Kite - Shot Making | game | ESPN Golf - Lower Your Score With Tom Kite - Shot Making (1994)(IntelliPlay)(US)(Disc 1 of 3)[!][Shot Making Disc 1].chd | 8 | 4 | Playable | Playable (54) | Parity | Good (100%) | Parity |
| ESPN Golf - Tom Kite no Kore ga Golf Da! | game | ESPN Golf - Tom Kite no Kore ga Golf Da! (1994)(BYSE)(JP)(Disc 1 of 3)[!].chd | 6 | 3 | Playable | No data | — | Good (100%) | Parity |
| ESPN Let's Go Skiing with Picabo Street, A. J. Kitt & Holly Flanders | game | ESPN Let's Go Skiing with Picabo Street, A. J. Kitt & Holly Flanders (1994)(IntelliPlay)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| ESPN Let's Play Beach Volleyball with Karch Kiraly & Karolyn Kirby | game | ESPN Let's Play Beach Volleyball with Karch Kiraly & Karolyn Kirby (1993)(IntelliPlay)(US).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| ESPN Let's Play Soccer with John Harkes and Mia Hamm | game | ESPN Let's Play Soccer with John Harkes and Mia Hamm (1994)(IntelliPlay)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| ESPN Let's Play Tennis with Tracy Austin | game | ESPN Let's Play Tennis with Tracy Austin (1994)(IntelliPlay)(US).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| ESPN Step Aerobics | game | ESPN Step Aerobics (1994)(IntelliPlay)(US)[!].chd | 3 | 1 | Playable | No data | — | Good (100%) | Parity |
| Evil Australians - Rise of Stupidity | game | Evil Australians - Rise of Stupidity v1.1 (2017)(Gameblabla).chd | 1 | 1 | Playable | — | — | — | — |
| Eye of Typhoon | game | Eye of Typhoon (Korea).chd | 5 | 3 | Playable | — | — | Good (100%) | Parity |
| F1GP | game | F1GP (1995)(Pony Canyon)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Family Feud | game | Family Feud (1994)(Gametek)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Fatty Bear's Birthday Surprise | game | Fatty Bear's Birthday Surprise (1993)(Humongous Entertainment)(US)[!].chd | 4 | 2 | Playable | No data | — | Good (100%) | Parity |
| Fatty Bear's Funpack | game | Fatty Bear's Funpack (1993)(Humongous Entertainment)(US)[!].chd | 6 | 2 | Playable | — | — | Good (100%) | Parity |
| FB Battery Navi Ver. II | game | FB Battery Navi Ver. II (Japan).chd | 2 | 1 | Playable | — | — | — | — |
| FIFA International Soccer | game | FIFA International Soccer (1994)(EA Sports)(EU)[!].chd | 4 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Fire Ball!! | game | Fire Ball!! (Japan).chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Firewall | game | Firewall (Korea).chd | 1 | 1 | Playable | — | — | — | — |
| Flashback | game | Flashback (Japan) (En,Ja,Fr,De,It,Nl).chd | 3 | 1 | Playable | — | — | — | — |
| Flashback - The Quest for Identity | game | Flashback - The Quest for Identity (1994)(U.S. Gold)(US)(en-fr)[!].chd | 4 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Flopon World | game | Flopon World (Japan).chd | 4 | 2 | Playable | — | — | — | — |
| Flying Nightmares | game | Flying Nightmares (1995)(Domark)(EU)[!].chd | 4 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Foes of Ali | game | Foes of Ali (1995)(EA Sports)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Fun 'n Games | game | Fun 'n Games (1993)(Panasonic)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Fun 'n Games - Soft no Omochabai | game | Fun 'n Games - Soft no Omochabai (1994)(Virgin)(JP)(en-ja)[!].chd | 3 | 1 | Playable | No data | — | — | — |
| Gakkou no Kowai Uwasa - Hanako-san ga Kita!! | game | Gakkou no Kowai Uwasa - Hanako-san ga Kita!! (1995)(Amuse)(JP)[!].chd | 2 | 1 | Playable | No data | — | — | — |
| Game no Tatsujin | game | Game no Tatsujin (1995)(Sunsoft)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Gameblabla's Crappy Compilation Rev 2 | game | Gameblabla's Crappy Compilation Rev 2 (2016)(Gameblabla).chd | 1 | 1 | Playable | — | — | — | — |
| Gex | game | Gex (1995)(Crystal Dynamics)(EU-US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Ghost Hunter Series - Kuroki Shi no Kamen | game | Ghost Hunter Series - Kuroki Shi no Kamen (Japan) (Rev 1).chd | 4 | 1 | Playable | — | — | — | — |
| Goal FH | game | Goal FH (1995)(Carrozzeria Japan)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Gridders | game | Gridders (1994)(GoldStar - Tetragon)(KR-US)(en)[!].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Grimm Meisaku Gekijou 1 - Bremen no Ongakutai | game | Grimm Meisaku Gekijou 1 - Bremen no Ongakutai (1994)(IMA)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Grimm Meisaku Gekijou 2 - Hansel to Gretel | game | Grimm Meisaku Gekijou 2 - Hansel to Gretel (1994)(IMA)(JP)[!].chd | 3 | 1 | Playable | — | — | — | — |
| Grimm Meisaku Gekijou 3 - Akazukin | game | Grimm Meisaku Gekijou 3 - Akazukin (1994)(IMA)(JP)[!].chd | 3 | 1 | Playable | — | — | — | — |
| Guardian War | game | Guardian War (1994)(Panasonic)(US)[!].chd | 4 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Hell - A Cyberpunk Thriller | game | Hell - A Cyberpunk Thriller (1994)(Take 2)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Hello Kitty - Asobi no Omochabako | game | Hello Kitty - Asobi no Omochabako (Japan).chd | 1 | 1 | Playable | — | — | Good (100%) | Parity |
| Hirata Shougo Interactive Ehon - Aesop Monogatari Vol.1 | game | Hirata Shougo Interactive Ehon - Aesop Monogatari Vol.1 (1994)(ELCOM)(JP)(M3)[!].chd | 3 | 1 | Playable | — | — | — | — |
| Hirata Shougo Interactive Ehon - Cinderella | game | Hirata Shougo Interactive Ehon - Cinderella (1995)(ELCOM)(JP)(M3).chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Hirata Shougo Interactive Ehon - Ningyo-hime | game | Hirata Shougo Interactive Ehon - Ningyo-hime (Japan) (En,Ja,Fr).chd | 2 | 1 | Playable | — | — | — | — |
| Hirata Shougo Interactive Ehon - Ookami to Nana Hiki no Ko Yagi | game | Hirata Shougo Interactive Ehon - Ookami to Nana Hiki no Ko Yagi (1995)(ELCOM)(JP)(M3).chd | 3 | 1 | Playable | — | — | — | — |
| Hirata Shougo Interactive Ehon - Sanbiki no Kobuta | game | Hirata Shougo Interactive Ehon - Sanbiki no Kobuta (1994)(ELCOM)(JP)(M3)[!].chd | 3 | 1 | Playable | — | — | — | — |
| Hirata Shougo Interactive Ehon - Shirayukihime | game | Hirata Shougo Interactive Ehon - Shirayukihime (1994)(ELCOM)(JP)(M3)[!].chd | 3 | 1 | Playable | — | — | — | — |
| Hitchcock ni Chousen | game | Hitchcock ni Chousen (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Horde, The | game | Horde, The (1994)(Crystal Dynamics)(EU-US)[!].chd | 7 | 4 | Playable | Good (95) | Parity | Good (100%) | Parity |
| Icebreaker | game | Icebreaker (1995)(Panasonic)(US)[!].chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Icebreaker II - The Game | game | Icebreaker II - The Game (2007)(Oldergames)[f 3DO Experience][AKA Icebreaker 2 2011 Edition].chd | 1 | 1 | Playable | — | — | — | — |
| Ida Jiyouji Nightmare Interactive - Moon Cradle - Igyou no Hanayome | game | Ida Jiyouji Nightmare Interactive - Moon Cradle - Igyou no Hanayome (1995)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Ide Yousuke Meijin no Shin Jissen Mahjong | game | Ide Yousuke Meijin no Shin Jissen Mahjong (1996)(Capcom)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Idol Janshi Suchie-Pai Special | game | Idol Janshi Suchie-Pai Special (1994)(Jaleco)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Idol Mahjong - Final Romance 2 - Hyper Edition | game | Idol Mahjong - Final Romance 2 - Hyper Edition (Japan).chd | 2 | 1 | Playable | — | — | — | — |
| Igo Time Trial Najigo I | game | Igo Time Trial Najigo I (1994)(Ematec)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Igo Time Trial - Shikatsu Daihyakka | game | Igo Time Trial - Shikatsu Daihyakka (Japan).chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Immercenary | game | Immercenary (1995)(Electronic Arts)(EU)[!].chd | 4 | 1 | Playable | Good (100) | Parity | — | — |
| Immortal Desire | game | Immortal Desire (1994)(Vivid Interactive)(US).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Incredible Machine, The | game | Incredible Machine, The (1994)(Dynamix)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Insect War | game | Insect War (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Interplay 3DO Buffet, The | game | Interplay 3DO Buffet, The (1995)(Interplay)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Iron Angel of the Apocalypse | game | Iron Angel of the Apocalypse (1994)(Synergy)(US)[!].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Iron Angel of the Apocalypse - The Return | game | Iron Angel of the Apocalypse - The Return (1995)(Synergy)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Ishida Yoshio Kudan no Igo Seiha | game | Ishida Yoshio Kudan no Igo Seiha (1995)(Electronic Arts)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| J. B. Harold - Blue Chicago Blues | game | J. B. Harold - Blue Chicago Blues (1995)(Riverhill Soft)(JP)(Disc 1 of 2)[!].chd | 4 | 2 | Playable | — | — | — | — |
| J.League Virtual Stadium | game | J.League Virtual Stadium (1994)(EA Sports)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| J.League Virtual Stadium '95 | game | J.League Virtual Stadium '95 (1995)(EA Sports)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Jammit | game | Jammit (1994)(GTE)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Jietai World | game | Jietai World (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Jikki Pachi Slot Simulator Vol. 1 | game | Jikki Pachi Slot Simulator Vol. 1 (1995)(Electronic Arts)(JP).chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| John Madden Football | game | John Madden Football (1994)(EA Sports)(US)[!][722007-2 70].chd | 8 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Johnny Bazookatone | game | Johnny Bazookatone (1996)(U.S. Gold)(US)[!].chd | 4 | 2 | Playable | Good (90) | Parity | Good (100%) | Parity |
| Joypad Test | game | Joypad Test v1.0 (2011)(Sdevel).chd | 1 | 1 | Title-only | — | — | — | — |
| Jurassic Park Interactive | game | Jurassic Park Interactive (1994)(Universal Interactive Studios)(US)[!].chd | 4 | 2 | Playable | Good (90) | Parity | Good (100%) | Parity |
| Kakinoki Shogi | game | Kakinoki Shogi (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Keiba Saishou no Housoku | game | Keiba Saishou no Housoku (1994)(Copya)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Kero Kero Keroppi to Origami no Tabibito | game | Kero Kero Keroppi to Origami no Tabibito (1995)(Mizuki)(JP)[!].chd | 3 | 1 | Playable | — | — | — | — |
| Killing Time | game | Killing Time (1995)(Studio 3DO)(EU)[!].chd | 13 | 6 | Playable | Good (100) | Parity | — | — |
| Kingdom - The Far Reaches | game | Kingdom - The Far Reaches (1994)(Interplay)(EU-US)[!].chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Konpeki no Kantai | game | Konpeki no Kantai (Japan) (Rev 1).chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Kyuumei Kyuukyuu Teate no ABC | game | Kyuumei Kyuukyuu Teate no ABC (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Kyuusei Senjutsu ni Yoru - Heisei Kaiun Koyomi | game | Kyuusei Senjutsu ni Yoru - Heisei Kaiun Koyomi (1994)(Ariadne)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| LameNES Emulator | game | LameNES Emulator (2016)(Gameblabla - Saffron)[incl. Hello World ROM].chd | 1 | 1 | No video | — | — | — | — |
| Last Bounty Hunter, The | game | Last Bounty Hunter, The (1994)(American Laser Games)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Lemmings | game | Lemmings (1993)(Psygnosis)(US)[!].chd | 4 | 2 | Playable | Good (90) | Parity | Good (100%) | Parity |
| Life Stage, The - Virtual House | game | Life Stage, The - Virtual House (1993)(GoldStar - Panasonic)(KR-US)(en)[!].chd | 5 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Little House, The | game | Little House, The (1995)(MC International)(JP).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Lost Dungeon | game | Lost Dungeon (1995)(T&E Soft)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Lost Eden | game | Lost Eden (1993)(Virgin)(US)(M5).chd | 5 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Lost Files of Sherlock Holmes, The | game | Lost Files of Sherlock Holmes, The (1994)(Electronic Arts)(US)[!].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Love Bites | game | Love Bites (1994)(Vivid Interactive)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Lucienne's Quest | game | Lucienne's Quest (1996)(Panasonic)(US)[!].chd | 3 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Macaroni Houren Shou Interactive | game | Macaroni Houren Shou Interactive (1995)(Toshiba EMI)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Mad Dog II - The Lost Gold | game | Mad Dog II - The Lost Gold (1994)(American Laser Games)(US)[!].chd | 4 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Mad Dog McCree | game | Mad Dog McCree (1993)(American Laser Games)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Mahjong Gokuu Tenjiku | game | Mahjong Gokuu Tenjiku (1994)(ASCII)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Mahjong Kyou Jidai - AV Gal Seifuku Hen | game | Mahjong Kyou Jidai - AV Gal Seifuku Hen (1994)(Micronet)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Mahjong Kyou Jidai - Cogal Houkago Hen | game | Mahjong Kyou Jidai - Cogal Houkago Hen (1995)(Micronet)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Marine Tour - Diving Spot Guide | game | Marine Tour - Diving Spot Guide (1994)(Office Create)(JP)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Masters Harukanaru Augusta 3 | game | Masters Harukanaru Augusta 3 (1994)(Panasonic)(JP)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Mathemagics - An Interactive Learning Cube | game | Mathemagics - An Interactive Learning Cube (1994)(L3)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Mazer | game | Mazer (1995)(American Laser Games)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| MegaRace | game | MegaRace (1994)(Mindscape)(EU-US)(M5)[!].chd | 8 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Memory Gwanli CD | game | Memory Gwanli CD (Korea) (RE).chd | 1 | 1 | Playable | — | — | — | — |
| Microcosm | game | Microcosm (1994)(Psygnosis)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Mind Teazzer | game | Mind Teazzer (1994)(Vivid Interactive)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Mirai Shounen Digital Library - Conan - The Boy in Future | game | Mirai Shounen Digital Library - Conan - The Boy in Future (1995)(Emotional Digital Software)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Mono Shiri Jiyuugaku - Ogura Hyakunin Isshu-hen | game | Mono Shiri Jiyuugaku - Ogura Hyakunin Isshu-hen (1995)(Shinko Human Create)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Montana Jones | game | Montana Jones (1995)(Future Pirates Junior)(JP)[!].chd | 5 | 2 | Playable | No data | — | Good (100%) | Parity |
| Multimedia Shinsho - Driving School | game | Multimedia Shinsho - Driving School (Futsuu Menkyo Gakka-hen) (1995)(JISC Life Support)(JP).chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Murphy Dayo Zenin Shuugou!! - Murphy's TV CD-ROM | game | Murphy Dayo Zenin Shuugou!! - Murphy's TV CD-ROM (1994)(Vantan International)(JP)(en-ja)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Myst | game | Myst (1994)(Panasonic)(US)[!].chd | 9 | 5 | Playable | Good (100) | Parity | Good (100%) | Parity |
| N. O. B. - Neo Organic Bioform | game | N. O. B. - Neo Organic Bioform (1995)(Sanyo)(JP).chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Nais How's 1 - Front How's '95-'96 | game | Nais How's 1 - Front How's '95-'96 v1.0 R2.0 (1995)(National)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Naoko to Hide-bou - Sansuu no Tensai 1 - Wa to Sa no Bunshoudai Tsurukamezan Part I | game | Naoko to Hide-bou - Sansuu no Tensai 1 - Wa to Sa no Bunshoudai Tsurukamezan Part I (Butsubutsu Koukan no Maki) (Japan).chd | 1 | 1 | Playable | — | — | Good (100%) | Parity |
| Naoko to Hide-bou - Sansuu no Tensai 2 - Hayasa no Bunshoudai Tsuukasan Part I | game | Naoko to Hide-bou - Sansuu no Tensai 2 - Hayasa no Bunshoudai Tsuukasan Part I (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Naoko to Hide Suekane - Sansuu no Tensai 1 | game | Naoko to Hide Suekane - Sansuu no Tensai 1 (1995)(Gakuman)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| National-Panasonic Collection | game | National-Panasonic Collection (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Need for Speed, The | game | Need for Speed, The (1994)(Electronic Arts)(EU-US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Nemurenu Yoru no Chiisa Nao Hanashi | game | Nemurenu Yoru no Chiisa Nao Hanashi (1994)(Amuse)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| NeuroDancer - Journey Into The Neuronet | game | NeuroDancer - Journey Into The Neuronet (1994)(Pixis Interactive)(US).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| New How's 1 - Front How's '94-'95 | game | New How's 1 - Front How's '94-'95 v1.0 (1994)(National)(JP)[!].chd | 3 | 1 | Playable | — | — | Good (100%) | Parity |
| Nice Body - For Professional Use - Oar Star Suieitaikai Official CD-ROM | game | Nice Body - For Professional Use - Oar Star Suieitaikai Official CD-ROM (1995)(Fuji Television)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Night Trap | game | Night Trap (1993)(Digital Pictures)(US)(Disc 1 of 2)[!].chd | 8 | 4 | Playable | Good (100) | Parity | — | — |
| Nishimura Keitarou Travel Mystery - Akugyaku no Kisetsu | game | Nishimura Keitarou Travel Mystery - Akugyaku no Kisetsu (1994)(Panasonic)(JP)(Disc 1 of 2)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Nobunaga no Yabou - Haouden | game | Nobunaga no Yabou - Haouden (1994)(KOEI)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Nontan to Issho - Hoshi no Okurimono | game | Nontan to Issho - Hoshi no Okurimono (1995)(Panasonic)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Nontan to Issho - Nohara de Asobo | game | Nontan to Issho - Nohara de Asobo (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Novastorm | game | Novastorm (1994)(Psygnosis)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Oceans Below | game | Oceans Below (1993)(Software Toolworks)(US)[!].chd | 4 | 2 | Playable | No data | — | Good (100%) | Parity |
| Off-world Interceptor | game | Off-world Interceptor (1994)(Crystal Dynamics)(EU-US)[!].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Ogura Hyakunin Isshu | game | Ogura Hyakunin Isshu (Japan).chd | 2 | 1 | Playable | — | — | — | — |
| Oilman | game | Oilman (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Olympic Soccer | game | Olympic Soccer (1996)(Panasonic)(US)(M5)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Olympic Summer Games | game | Olympic Summer Games (1996)(Panasonic)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Onee-san to Issho! Janken Paradise | game | Onee-san to Issho! Janken Paradise (Japan).chd | 2 | 1 | Playable | — | — | — | — |
| Onee-san to Issho! Kisekae Paradise | game | Onee-san to Issho! Kisekae Paradise (Japan).chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| OpenLara | game | OpenLara (2021-12-20)(XProger)(BSD2).chd | 1 | 1 | Playable | — | — | — | — |
| Orbatak | game | Orbatak (USA) (Arcade CD-ROM).chd | 2 | 1 | Playable | Unplayable (25) | 5DO ahead | Good (100%) | Parity |
| Oukoku no Grand Chef | game | Oukoku no Grand Chef (1996)(Sala International)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Out of this World | game | Out of this World (1993)(Interplay)(US)[!][45097-1].chd | 7 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Outer World | game | Outer World (1994)(Electronic Arts)(JP)(en)[!].chd | 2 | 1 | Playable | — | — | — | — |
| OverDrivin' | game | OverDrivin' (1994)(Electronic Arts)(JP)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Oyaji Hunter Mahjong | game | Oyaji Hunter Mahjong (1995)(Warp)(JP)[!].chd | 3 | 2 | Playable | — | — | Good (100%) | Parity |
| Paddock Note '95 | game | Paddock Note '95 (1995)(Fuji Television)(JP)[!].chd | 3 | 2 | Playable | Good (100) | Parity | — | — |
| Panasonic REAL 3DO Interactive Multiplayer - Demonstration CD | game | Panasonic REAL 3DO Interactive Multiplayer - Demonstration CD (1993)(Panasonic)(US).chd | 2 | 1 | Playable | — | — | — | — |
| Panasonic Special CD-ROM | game | Panasonic Special CD-ROM (1995)(Panasonic)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Panzer General | game | Panzer General (1995)(Mindscape - SSi)(EU-US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Pararancho - The Incredible Machine | game | Pararancho - The Incredible Machine (1994)(T&E Soft)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| PaTaank | game | PaTaank (1994)(PF.Magic)(EU-US)[!].chd | 5 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Pebble Beach Golf Links | game | Pebble Beach Golf Links (1993)(GoldStar - Panasonic)(KR-US)(en)[!].chd | 6 | 2 | Playable | Good (95) | Parity | Good (100%) | Parity |
| Pebble Beach no Hatou | game | Pebble Beach no Hatou (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Penthouse Interactive - Virtual Photo Shoot Vol. 1 | game | Penthouse Interactive - Virtual Photo Shoot Vol. 1 (1994)(GAGA)(JP).chd | 2 | 1 | Playable | No data | — | — | — |
| Perfect General, The | game | Perfect General, The (1995)(Kirin Entertainment)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Perfect World - Combat in a Digital World | game | Perfect World - Combat in a Digital World (1995)(Electronic Arts)(JP)[!].chd | 4 | 2 | Playable | — | — | — | — |
| Peter Frankl - Puzzle no Tou | game | Peter Frankl - Puzzle no Tou (1995)(TBS Multimedia)(JP)(en-ja)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| PGA Tour 96 | game | PGA Tour 96 (1995)(EA Sports)(EU)[!].chd | 5 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Phoenix 3 | game | Phoenix 3 (1995)(Studio 3DO)(EU)[!].chd | 4 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Plumbers Don't Wear Ties | game | Plumbers Don't Wear Ties (1994)(Kirin Entertainment)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| PO'ed | game | PO'ed (1995)(Studio 3DO)(EU-US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Policenauts | game | Policenauts (1995)(Konami)(JP)(Disc 1 of 2)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Policenauts - Pilot Disk | game | Policenauts - Pilot Disk (1995)(Konami)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Powers Kingdom | game | Powers Kingdom (1994)(Panasonic)(EU)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Powerslide | game | Powerslide (2007)(Oldergames)(Disc 2 of 2)[!][Powerslide Bonus Disk - Footie].chd | 2 | 1 | Playable | — | — | — | — |
| Primal Rage | game | Primal Rage v1.0 (1995)(GoldStar)(EU-US)(M7)[!].chd | 2 | 1 | Playable | Good (80) | Parity | Good (100%) | Parity |
| Princess Maker 2 | game | Princess Maker 2 (1995)(Micro Cabin)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Pro Stadium | game | Pro Stadium (1995)(Sanyo)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Pro Yakyuu Virtual Stadium - Professional Baseball | game | Pro Yakyuu Virtual Stadium - Professional Baseball (1995)(EA Sports)(JP)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Psychic Detective | game | Psychic Detective (1995)(Electronic Arts)(EU)(Disc 1 of 3)[!].chd | 12 | 3 | Playable | Playable (70) | Parity | Good (100%) | Parity |
| Puppet Tale | game | Puppet Tale (1994)(Microcabin - Panasonic)(JP)[!].chd | 2 | 1 | Playable | No data | — | — | — |
| Putt-Putt Goes to the Moon | game | Putt-Putt Goes to the Moon (1993)(Humongous Entertainment)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Putt-Putt Joins the Parade | game | Putt-Putt Joins the Parade (1993)(Humongous Entertainment)(US)[!][CQ 6922-2].chd | 4 | 1 | Playable | No data | — | Good (100%) | Parity |
| Putt-Putt's Fun Pack | game | Putt-Putt's Fun Pack (1993)(Humongous Entertainment)(US)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Putt-Putt to Parade ni Ikou! | game | Putt-Putt to Parade ni Ikou! (1994)(Media Vision)(JP)(en-ja)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Puzzle Bobble | game | Puzzle Bobble (1995)(Micro Cabin)(JP)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Pyramid Intruder | game | Pyramid Intruder (1995)(Taito)(JP)(en)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Quarantine | game | Quarantine (1994)(Gametek - Imagineer)(JP-US)(M5)[!][IM-TS9503, 439480068501RE3].chd | 5 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Quarterback Attack - The Professional Quarterback Simulator | game | Quarterback Attack - The Professional Quarterback Simulator (1995)(Digital Pictures)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Real Pinball | game | Real Pinball (1994)(Panasonic)(US)[!].chd | 6 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Reika - Virtual Puppet | game | Reika - Virtual Puppet (1994)(H.A.N.D)(JP).chd | 3 | 1 | Playable | — | — | — | — |
| Return Fire | game | Return Fire v1.0 F306.1.2 (1994)(Prolific - Silent Software)(EU-US)[!].chd | 7 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Return Fire - Maps O' Death | game | Return Fire - Maps O' Death v1.0 FB13.1.0 (1995)(Prolific)(US)[!][data disc].chd | 3 | 2 | Title-only | Good (100) | Peer invalid | Good (100%) | Peer invalid |
| Rise of the Robots | game | Rise of the Robots (1995)(Absolute)(US)(M3)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Road Rash | game | Road Rash (1994)(Electronic Arts)(US)[!].chd | 10 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Robinson's Requiem | game | Robinson's Requiem (1996)(ReadySoft - Silmarils)(US)[!].chd | 3 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Royal Pro Wrestling - Jikkyou Live!! | game | Royal Pro Wrestling - Jikkyou Live!! (1996)(Natsume)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Samurai Shodown | game | Samurai Shodown (1994)(Crystal Dynamics)(EU-US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| San Diego Zoo Presents... The Animals!, The | game | San Diego Zoo Presents... The Animals!, The (1993)(Software Toolworks)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Sangokushi IV | game | Sangokushi IV (1995)(KOEI)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Scramble Cobra | game | Scramble Cobra (1995)(Panasonic)(US)[!].chd | 9 | 5 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Scramble Cobra - Premium Version | game | Scramble Cobra - Premium Version (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Seal of the Pharaoh | game | Seal of the Pharaoh (1994)(Panasonic)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Seimeihandan | game | Seimeihandan (1995)(Ariadne)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Sekure Fuumin no Omocha Hako | game | Sekure Fuumin no Omocha Hako (1994)(GLAMS)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Sento Monogatari - Sono I | game | Sento Monogatari - Sono I (1995)(Ariadne)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Sesame Street - Numbers | game | Sesame Street - Numbers (1994)(EA Kids)(US)[!].chd | 4 | 2 | Playable | No data | — | Good (100%) | Parity |
| Sewer Shark | game | Sewer Shark (1994)(Digital Pictures)(US)[!].chd | 3 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Sex | game | Sex (1994)(Vivid Interactive)(US).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Shadow - War of Succession | game | Shadow - War of Succession (1994)(Tribeca Digital Studios)(US)[!].chd | 2 | 1 | Playable | Playable (75) | Parity | Good (100%) | Parity |
| Shadow Warriors | game | Shadow Warriors (1994)(T&E Soft)(JP)(en)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Shanghai - Banri no Choujou | game | Shanghai - Banri no Choujou (1994)(Activision)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Shanghai - Triple-Threat | game | Shanghai - Triple-Threat (1994)(Activision)(US)[!][02500-2.4].chd | 4 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Shelley Duvall's It's A Bird's Life | game | Shelley Duvall's It's A Bird's Life (1992)(Sanctuary Woods)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Shock Wave | game | Shock Wave (1994)(Electronic Arts)(EU)[!].chd | 12 | 5 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Shock Wave - Operation JumpGate | game | Shock Wave - Operation JumpGate (1994)(Electronic Arts)(EU-US)[!][data disc].chd | 6 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Shockwave 2 - Beyond the Gate | game | Shockwave 2 - Beyond the Gate (1995)(Electronic Arts)(EU)(Disc 1 of 2)[!].chd | 10 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Shootout at Old Tucson | game | Shootout at Old Tucson (USA) (RE3) (Arcade CD-ROM).chd | 3 | 2 | Playable | Unplayable (25) | 5DO ahead | Good (100%) | Parity |
| Short Warp - Warp's Short Game Colection | game | Short Warp - Warp's Short Game Colection (1996)(Warp)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Sid Meier's C.P.U. Bach | game | Sid Meier's C.P.U. Bach (1994)(MicroProse)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Slam 'N Jam '95 | game | Slam 'N Jam '95 (1994)(Crystal Dynamics)(EU-US)[!].chd | 8 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Slayer | game | Slayer (1994)(Mindscape)(EU)[!].chd | 6 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Slopestyle - An Interactive Learning Cube | game | Slopestyle - An Interactive Learning Cube (1994)(L3)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Snow Job Starring Tracy Scoggins | game | Snow Job Starring Tracy Scoggins (1995)(Studio 3DO)(US)(Disc 1 of 2)[!].chd | 8 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Soccer Kid | game | Soccer Kid (1994)(Studio 3DO)(US)[!].chd | 5 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Sotsugyou Graduation Final | game | Sotsugyou Graduation Final (1994)(Shar Rock)(JP)[!].chd | 4 | 2 | Playable | — | — | Good (100%) | Parity |
| Sotsugyou II - Neo Generation Special | game | Sotsugyou II - Neo Generation Special (1995)(Shar Rock)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Space Ace | game | Space Ace (1995)(ReadySoft)(US)[!][DH 65008 RE-1].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Space Hulk - Vengeance of the Blood Angels | game | Space Hulk - Vengeance of the Blood Angels (1995)(Electronic Arts)(EU)[!].chd | 7 | 3 | Playable | Good (100) | Parity | — | — |
| Space Invaders Invaders | game | Space Invaders Invaders (2014)(Mihai, Sebastian).chd | 1 | 1 | Playable | — | — | — | — |
| Space Pirates | game | Space Pirates (1994)(American Laser Games)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Space Shuttle | game | Space Shuttle (1993)(Software Toolworks)(US)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Star Control II | game | Star Control II (1994)(Crystal Dynamics)(EU-US)[!].chd | 5 | 2 | Playable | Good (95) | Parity | Good (100%) | Parity |
| Star Fighter | game | Star Fighter (1995)(Studio 3DO)(EU-US)[!].chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Star Wars - Rebel Assault | game | Star Wars - Rebel Assault v1.0 (1993)(Electronic Arts)(EU-US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Star Wars - Rebel Assault .0J | game | Star Wars - Rebel Assault v1.0J (1993)(Electronic Arts)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| StarBlade | game | StarBlade (1994)(Panasonic)(CA)[!].chd | 10 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Stellar 7 - Draxon no Gyakushuu | game | Stellar 7 - Draxon no Gyakushuu (1994)(Dynamix)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Stellar 7 - Draxon's Revenge | game | Stellar 7 - Draxon's Revenge (1993)(Dynamix)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| STNICCC2000 3DO | game | STNICCC2000 3DO (2019)(Optimus).chd | 1 | 1 | Playable | — | — | — | — |
| Strahl | game | Strahl (1995)(Panasonic)(US)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Striker - World Cup Special | game | Striker - World Cup Special (1995)(Panasonic)(EU)[!].chd | 4 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Super Model, The - Gail McKenna | game | Super Model, The - Gail McKenna (1995)(Transpegasus)(JP).chd | 2 | 1 | Playable | No data | — | — | — |
| Super Nova - Battery Navi | game | Super Nova - Battery Navi (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Super Real Mahjong P.V | game | Super Real Mahjong P.V (1995)(Seta)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Super Real Mahjong PIV Aishou Shindan | game | Super Real Mahjong PIV   Aishou Shindan (Japan).chd | 3 | 1 | Playable | — | — | Good (100%) | Parity |
| Super Street Fighter II Turbo | game | Super Street Fighter II Turbo (1994)(Panasonic)(CA-US)[!][SM3851-2 RE1].chd | 11 | 5 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Super Wing Commander | game | Super Wing Commander (1994)(Origin)(US)[!].chd | 7 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| SuperModels Go Wild | game | SuperModels Go Wild (1994)(Vivid Interactive)(US).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Supreme Warrior | game | Supreme Warrior (USA) (Disc 1) (Fire & Earth).chd | 7 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Supreme Warrior - Ying Heung | game | Supreme Warrior - Ying Heung (1994)(Digital Pictures)(US)(en-zh)(Disc 2 of 2)[!][Wind & Fang Tu].chd | 3 | 1 | Playable | — | — | — | — |
| Sword & Sorcery | game | Sword & Sorcery (1995)(Micro Cabin)(JP)[!].chd | 4 | 2 | Playable | — | — | — | — |
| Syndicate | game | Syndicate (1995)(Electronic Arts)(EU)[!].chd | 6 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| System Catalog - Kigyou-muke System no Goannai | game | System Catalog - Kigyou-muke System no Goannai (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| T&E VR Golf - Waialae no Kiseki | game | T&E VR Golf - Waialae no Kiseki (Japan).chd | 2 | 1 | Playable | — | — | — | — |
| Taiketsu Rumiizu! | game | Taiketsu Rumiizu! (Japan).chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Takeru | game | Takeru (1994)(Panasonic)(JP)[!].chd | 3 | 2 | Playable | — | — | Good (100%) | Parity |
| Tanjou Debut - Pure | game | Tanjou Debut - Pure (1996)(Shar Rock)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Tarot Uranai | game | Tarot Uranai (1995)(Ariadne)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Tetsujin Returns | game | Tetsujin Returns (1995)(Synergy)(JP)[!].chd | 2 | 1 | Playable | Playable (60) | Parity | — | — |
| Theatre Wars - Goraku no Dendou | game | Theatre Wars - Goraku no Dendou (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Theme Park | game | Theme Park (1994)(Electronic Arts)(EU)(en-fr)[!].chd | 9 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Toki o Koeta Tegami - The Letter That Over Came Time | game | Toki o Koeta Tegami - The Letter That Over Came Time (1994)(Panasonic)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Tokimeki Mahjong Paradise Special - Koi no Tenpai Beat | game | Tokimeki Mahjong Paradise Special - Koi no Tenpai Beat (1996)(Sonnet)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Toon Time... In the Classroom | game | Toon Time... In the Classroom (1994)(VideoactV)(US)[!].chd | 2 | 1 | Playable | No data | — | — | — |
| Total Eclipse | game | Total Eclipse (1993)(Crystal Dynamics)(US)[!][3DRM-1214760].chd | 11 | 4 | Playable | Good (90) | Parity | Good (100%) | Parity |
| Totsugeki Karakuri! Megadas!! | game | Totsugeki Karakuri! Megadas!! (Japan).chd | 4 | 2 | Playable | — | — | — | — |
| Touhaiden Akagi | game | Touhaiden Akagi (1996)(Micronet)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Tower, The | game | Tower, The (1996)(OPeNBooK)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| TOYOPET OutDoor World | game | TOYOPET OutDoor World (1995)(Toyota)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| TOYOPET RV Paddock | game | TOYOPET RV Paddock (1995)(Toyota)(JP).chd | 2 | 1 | Playable | — | — | — | — |
| Toyopet RV World | game | Toyopet RV World (Japan).chd | 1 | 1 | Playable | — | — | — | — |
| Tozasareta Yakata | game | Tozasareta Yakata (1995)(Studio 3DO)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Trip'd | game | Trip'd (1995)(Panasonic)(EU-US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| True Golf Classics - Waialae Country Club | game | True Golf Classics - Waialae Country Club (Germany).chd | 2 | 1 | Playable | — | — | — | — |
| Tsuukai Gameshow - Twisted | game | Tsuukai Gameshow - Twisted (1994)(Electronic Arts)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Twinkle Knights | game | Twinkle Knights (1995)(Intarus)(JP).chd | 3 | 2 | Playable | No data | — | Good (100%) | Parity |
| Twisted - The Game Show | game | Twisted - The Game Show (1993)(Electronic Arts)(US)[!][730807-2 R73].chd | 7 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Uchuu Seifutsu Furopon-kun | game | Uchuu Seifutsu Furopon-kun (1994)(San-Ei Shobo Publishing)(JP)[!].chd | 2 | 1 | Playable | Good (95) | Parity | — | — |
| Ultraman Powered | game | Ultraman Powered (1994)(Bandai)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| V Goal Soccer '96 | game | V Goal Soccer '96 (1996)(Tecmo)(JP)(en).chd | 4 | 2 | Playable | Good (100) | Parity | — | — |
| Virtual Cameraman Part 1 - Naomi Sawada & Anna Juri | game | Virtual Cameraman Part 1 - Naomi Sawada & Anna Juri (1995)(Transpegasus)(JP)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Virtual Cameraman Part 2 - Natsumi Kawai & Kimi Tachihara | game | Virtual Cameraman Part 2 - Natsumi Kawai & Kimi Tachihara (1995)(Transpegasus)(JP).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Virtual Cameraman Part 3 - Yumika Sugimoto | game | Virtual Cameraman Part 3 - Yumika Sugimoto (1995)(Transpegasus)(JP).chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Virtual Cameraman Part 4 - Shiori Fujitani | game | Virtual Cameraman Part 4 - Shiori Fujitani (1995)(Transpegasus)(JP)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Virtual Cameraman Part 5 - Yuri Andou | game | Virtual Cameraman Part 5 - Yuri Andou (1995)(Transpegasus)(JP)[!].chd | 2 | 1 | Playable | No data | — | Good (100%) | Parity |
| Virtual Horror - Norowareta Tachi | game | Virtual Horror - Norowareta Tachi (1994)(Electronic Arts)(JP)[!].chd | 2 | 1 | Playable | — | — | Good (100%) | Parity |
| Virtual Quest - Pharaoh no Fuuin | game | Virtual Quest - Pharaoh no Fuuin (1994)(ASK KODANSHA)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Virtual Vivid | game | Virtual Vivid (1993)(Vivid Interactive)(US)[!][GL VIRTUA RE-1].chd | 4 | 2 | Playable | No data | — | Good (100%) | Parity |
| Virtuoso | game | Virtuoso (1995)(Data East)(US)[!].chd | 7 | 3 | Playable | Good (100) | Parity | Good (100%) | Parity |
| VR Stalker | game | VR Stalker (1994)(American Laser Games)(US)[!].chd | 3 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Waialae Country Club | game | Waialae Country Club (1994)(Panasonic)(CA-US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Warner Music Group | game | Warner Music Group (USA).chd | 1 | 1 | Playable | — | — | — | — |
| Way of the Warrior | game | Way of the Warrior (1994)(Universal Interactive Studios)(NTSC)(US)[!].chd | 6 | 3 | Playable | Good (95) | Parity | Good (100%) | Parity |
| Where is Derpy Final | game | Where is Derpy v1.3 Final (2014)(Gameblabla)[unencrypted, req. RSA disable].chd | 1 | 1 | Playable | — | — | — | — |
| Who Shot Johnny Rock | game | Who Shot Johnny Rock (1994)(American Laser Games)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Wicked 18 | game | Wicked 18 (1994)(Panasonic)(US)[!].chd | 2 | 1 | Playable | Good (95) | Parity | Good (100%) | Parity |
| Wing Commander III - Heart of the Tiger | game | Wing Commander III - Heart of the Tiger (1995)(Origin)(EU-US)(Disc 1 of 4)[!].chd | 20 | 8 | Playable | Good (90) | Parity | Good (100%) | Parity |
| Winning Post | game | Winning Post (1994)(KOEI)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Wolfenstein 3D | game | Wolfenstein 3D (1994)(Interplay)(US)[!].chd | 6 | 4 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Woody Woodpecker and Friends - Volume One | game | Woody Woodpecker and Friends - Volume One (1994)(MCA Universal Home Video)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | — | — |
| Woody Woodpecker and Friends - Volume Three | game | Woody Woodpecker and Friends - Volume Three (1994)(MCA Universal Home Video)(US).chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Woody Woodpecker and Friends - Volume Two | game | Woody Woodpecker and Friends - Volume Two (1994)(MCA Universal Home Video)(US)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| World Cup Golf - Hyatt Dorado Beach | game | World Cup Golf - Hyatt Dorado Beach (1994)(U.S. Gold)(EU)[!].chd | 5 | 2 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Worldcup Super Stadium | game | Worldcup Super Stadium (1995)(GAGA)(JP).chd | 2 | 1 | Playable | No data | — | — | — |
| Yakyuuken Special, The | game | Yakyuuken Special, The (1994)(Societa Daikanyama)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Yamada Kamachi Bijutsukan - Kamachi's Museum | game | Yamada Kamachi Bijutsukan - Kamachi's Museum (1995)(TV Asahi)(JP)[!].chd | 2 | 1 | Playable | — | — | — | — |
| Yamamura Misa Suspense - Kyoto Kurama Sansou Satsujin Jiken | game | Yamamura Misa Suspense - Kyoto Kurama Sansou Satsujin Jiken (1994)(Pack-In-Video - Panasonic)(JP)(Disc 1 of 2)[!].chd | 4 | 2 | Playable | — | — | — | — |
| Yuu Yuu Hakusho | game | Yuu Yuu Hakusho (1994)(Tomy)(JP)[!].chd | 2 | 1 | Playable | Good (100) | Parity | Good (100%) | Parity |
| Zhadnost - The Peoples Party | game | Zhadnost - The Peoples Party (1995)(Studio 3DO)(EU)[!].chd | 3 | 2 | Playable | Good (100) | Parity | — | — |
| Adventures Of Captain J, The | homebrew | Adventures Of Captain J, The (USA) (Unl).chd | 1 | 1 | Playable | — | — | — | — |
| BioFury | homebrew | BioFury (USA) (Unl).chd | 1 | 1 | Playable | — | — | — | — |
| Icebreaker II | homebrew | Icebreaker II (USA) (Bonus Disk) (Unl).chd | 4 | 2 | Playable | — | — | — | — |
| Remute 3D | homebrew | Remute 3D (Europe) (En,De) (Unl).chd | 1 | 1 | Playable | — | — | — | — |
| 3DO Game Guru | media | 3DO Game Guru (1995)(3DO Company)(EU-US)[!].chd | 2 | 1 | Rendered | Good (100) | Unverified | Good (100%) | Unverified |
| 3DO Gamepack #1 | media | 3DO Gamepack #1 (2011)(MNEMO)(RU)[Icebreaker II, Onside, Powerslide, Gex Alpha, Footie, Orbatak].chd | 2 | 1 | No video | — | — | — | — |
| 3DO Interactive Sampler CD, The | media | 3DO Interactive Sampler CD, The (1994)(3DO Company)(US)[!].chd | 4 | 1 | Rendered | Good (100) | Unverified | Good (100%) | Unverified |
| 3DO Interactive Sampler CD #2, The | media | 3DO Interactive Sampler CD #2, The (1994)(3DO Company)(US)[!].chd | 2 | 1 | Rendered | Good (100) | Unverified | — | — |
| 3DO Interactive Sampler CD #3, The | media | 3DO Interactive Sampler CD #3, The (1995)(3DO Company)(US)[!].chd | 4 | 1 | Rendered | Good (100) | Unverified | — | — |
| 3DO Interactive Sampler CD 4, The | media | 3DO Interactive Sampler CD 4, The (1995)(3DO Company)(US)[!].chd | 4 | 1 | Rendered | Good (100) | Unverified | — | — |
| 3DO Magazine 02 | media | 3DO Magazine 02 (1995)(Paragon Publishing)(GB)[!].chd | 6 | 1 | Rendered | No data | — | — | — |
| 3DO Magazine 03 | media | 3DO Magazine 03 (1995-03-30)(Paragon Publishing)(GB)[!].chd | 3 | 1 | Rendered | No data | — | — | — |
| 3DO Magazine 04 | media | 3DO Magazine 04 (1995)(Paragon Publishing)(GB)[!].chd | 3 | 1 | Rendered | No data | — | — | — |
| 3DO Magazine 05 | media | 3DO Magazine 05 (1995)(Paragon Publishing)(GB)[!][Aug-Sep 1995].chd | 2 | 1 | Rendered | No data | — | — | — |
| 3DO Magazine 06 | media | 3DO Magazine 06 (1995)(Paragon Publishing)(GB)[!][Oct-Nov 1995].chd | 6 | 1 | Rendered | No data | — | — | — |
| 3DO Magazine 07 | media | 3DO Magazine 07 (1995)(Paragon Publishing)(GB)[!][Dec 1995-Jan 1996].chd | 2 | 1 | Rendered | No data | — | Good (100%) | Unverified |
| 3DO Magazine 08 | media | 3DO Magazine 08 (1996)(Paragon Publishing)(GB)[!][Feb-Mar 1996].chd | 3 | 1 | Rendered | No data | — | Good (100%) | Unverified |
| 3DO Magazine 13 | media | 3DO Magazine 13 (1996)(Paragon Publishing)(GB)[!][Aug 1996].chd | 2 | 1 | Rendered | No data | — | Good (100%) | Unverified |
| 3DO Storage Manager | media | 3DO Storage Manager v2.1 (1994)(3DO Company)(JP)(en).chd | 3 | 1 | Rendered | Good (100) | Unverified | Good (100%) | Unverified |
| Live! 3DO Magazine CD-ROM #01 | media | Live! 3DO Magazine CD-ROM #01 (1994)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | Good (100%) | Unverified |
| Live! 3DO Magazine CD-ROM #02 | media | Live! 3DO Magazine CD-ROM #02 (1994)(Tokumashoten Intermedia)(JP)[!].chd | 2 | 1 | Rendered | — | — | — | — |
| Live! 3DO Magazine CD-ROM #03 | media | Live! 3DO Magazine CD-ROM #03 (1994)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | Good (100%) | Unverified |
| Live! 3DO Magazine CD-ROM #04 | media | Live! 3DO Magazine CD-ROM #04 (1994)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | — | — |
| Live! 3DO Magazine CD-ROM #05 | media | Live! 3DO Magazine CD-ROM #05 (1994)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | — | — |
| Live! 3DO Magazine CD-ROM #06 | media | Live! 3DO Magazine CD-ROM #06 (1995)(Tokumashoten Intermedia)(JP)[!].chd | 2 | 1 | Rendered | — | — | — | — |
| Live! 3DO Magazine CD-ROM #07 | media | Live! 3DO Magazine CD-ROM #07 (1995)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | — | — |
| Live! 3DO Magazine CD-ROM #08 | media | Live! 3DO Magazine CD-ROM #08 (1995)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | Good (100%) | Unverified |
| Live! 3DO Magazine CD-ROM #09 | media | Live! 3DO Magazine CD-ROM #09 (1996)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | — | — |
| Live! 3DO Magazine CD-ROM #10 | media | Live! 3DO Magazine CD-ROM #10 (1996)(Tokumashoten Intermedia)(JP)[!].chd | 2 | 1 | Rendered | — | — | Good (100%) | Unverified |
| Live! 3DO Magazine CD-ROM #11 | media | Live! 3DO Magazine CD-ROM #11 (1996)(Tokumashoten Intermedia)(JP)[!].chd | 3 | 1 | Rendered | — | — | Good (100%) | Unverified |
| Panasonic 3DO Interaktiver Multiplayer Sampler Disc | media | Panasonic 3DO Interaktiver Multiplayer Sampler Disc (Germany) (En,De).chd | 1 | 1 | Rendered | — | — | — | — |
| Panasonic REAL 3DO Interactive Multiplayer - Sampler CD | media | Panasonic REAL 3DO Interactive Multiplayer - Sampler CD (1994)(Panasonic)(US)[!][DFJN5001ZAZ].chd | 7 | 4 | Rendered | Good (100) | Unverified | Good (100%) | Unverified |
| Panasonic REAL 3DO Interactive Multiplayer - Sampler CD for PAL System | media | Panasonic REAL 3DO Interactive Multiplayer - Sampler CD for PAL System (1994)(Panasonic)(EU)[!].chd | 2 | 1 | Rendered | Good (100) | Unverified | — | — |
| Panasonic REAL 3DO Interactive Multiplayer - Sampler III and 3DO Storage Manager | media | Panasonic REAL 3DO Interactive Multiplayer - Sampler III and 3DO Storage Manager (USA).chd | 2 | 1 | Rendered | — | — | — | — |
| 3DO Game Guru & BladeForce | translation | 3DO Game Guru (1995)(3DO Company)(EU-US) & BladeForce (1995)(Studio 3DO)(EU-US)[tr ru Atla - Ryudo][v1].chd | 4 | 4 | Rendered | — | — | — | — |
| Bear Stormin' & Putt-Putt Joins the Parade | translation | Bear Stormin' (1993)(Humongous Entertainment) & Putt-Putt Joins the Parade (1993)(Humongous Entertainment)-(US)[tr ru Fargus - Spider].chd | 1 | 1 | Rendered | — | — | — | — |

## Regressions to triage

None.

## Titles where 5DO is ahead

The peer reports `Unplayable`/`Not Loadable`, but 5DO is playable under the beyond-title rule. Each row carries a manual or automated beyond-title evidence record.

| Title | 5DO | 4DO | Phoenix |
|---|---|---|---|
| Orbatak | Playable | Unplayable | Good |
| Shootout at Old Tucson | Playable | Unplayable | Good |

## Peer-claim invalidations

A peer claim is removed only with a beyond-title capture and an explicit `peerInvalid`/`peerEvidence` record. The peer's raw status remains visible in the table above.

| Title | Peer | Raw status | Evidence |
|---|---|---|---|
| Return Fire - Maps O' Death | 4DO, Phoenix | 4DO Good; Phoenix Good | artifacts\beyond-title\return-fire-maps-o-death\captures\f012000.png |
