# Beyond-title playability evidence

This page is generated from `docs/data/beyond-title-results.csv` by `python tools/beyond-title-report.py`. It records the dated beyond-title pass for the 378-game test set: capture frame, BIOS, disc hash, screenshot, and failure reason for every row.

## Summary

| Measure | Titles |
|---|---:|
| Ledger rows | 378 |
| Playable | 375 |
| Title-only | 2 |
| No video | 1 |
| Not loadable | 0 |
| Review flagged | 0 |
| 378-game set covered | 378/378 |
| 5DO playable (ledger only) | 375 |

## BIOS and input coverage

| BIOS | Rows |
|---|---:|
| Panasonic FZ-10J | 194 |
| Panasonic FZ-10 | 174 |
| Panasonic FZ-10E (Anvil) | 6 |
| Panasonic FZ-10 (No RSA) | 4 |

| Input device | Rows |
|---|---:|
| joypad/default | 368 |
| lightgun | 7 |
| mouse | 1 |
| orbatak-trackball | 1 |
| lightgun, arcade-lightgun | 1 |

## Peer-gap rows

None: every peer-playable title has a beyond-title `Playable` row.

## Failures and triage

Every non-playable title has one triage bucket, a reproduction command, the last capture, and the failing reason. The buckets are BIOS/region, disc/multi-disc, input device, core/CD boot, per-title game fix, and harness/script gap; a row can be re-bucketed with an explicit `triageBucket` column.

| Title | Status | Bucket | PC / frame hash | Reason | Screenshot | Reproduction |
|---|---|---|---|---|---|---|
| Joypad Test | Title-only | harness/script gap |  | Non-retail pad test card. The screen is a static 'NO PRESSED KEYS' status display for pads 1-4; the generic script and control are identical. | artifacts\beyond-title\joypad-test\captures\f012000.png | `dotnet src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll run "J:\Panasonic 3DO\Joypad Test v1.0 (2011)(Sdevel).chd" --bios-auto bios --frames 12000 --audio null --json --input-script "benchmarks/diff/scripts/tierb-10min.txt"` |
| LameNES Emulator | No video | core/video |  | Non-retail emulator test image. It renders only a small 'HELLO, WORLD!' text line (0.43% non-black), below the 0.5% video threshold; no game content. | artifacts\beyond-title\lamenes-emulator\captures\f012000.png | `dotnet src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll run "J:\Panasonic 3DO\LameNES Emulator (2016)(Gameblabla - Saffron)[incl. Hello World ROM].chd" --bios-auto bios --frames 12000 --audio null --json --input-script "benchmarks/diff/scripts/tierb-10min.txt" --allow-unsigned-discs` |
| Return Fire - Maps O' Death | Title-only | harness/script gap |  | Standalone add-on data disc: only the static Return Fire box-art/skull title card renders; it ignores the scripted input and control pair. The peer claim covers the expansion content used with the base game, not this image. | artifacts\beyond-title\return-fire-maps-o-death\captures\f012000.png | `dotnet src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll run "J:\Panasonic 3DO\Return Fire - Maps O' Death v1.0 FB13.1.0 (1995)(Prolific)(US)[!][data disc].chd" --bios-auto bios --frames 12000 --audio null --json --input-script "benchmarks/diff/scripts/tierb-10min.txt"` |

## Playable (375)

| Title | Canonical disc | BIOS | Disc hash | Frame | Review | Screenshot |
|---|---|---|---|---:|---|---|
| 20th Century Video Almanac | 20th Century Video Almanac (1993)(Software Toolworks)(US)[!].chd | Panasonic FZ-10 | 3f72700d784d | 12000 | no | artifacts\beyond-title\20th-century-video-almanac\captures\f012000.png |
| 3D Atlas | 3D Atlas (1994)(Electronic Arts)(US).chd | Panasonic FZ-10 | 83d70d9c6a4a | 12000 | no | artifacts\beyond-title\3d-atlas\captures\f012000.png |
| 3DO de Shiru Miru Asobu - Nakajima Miyuki | 3DO de Shiru Miru Asobu - Nakajima Miyuki (Japan).chd | Panasonic FZ-10J | 59e7f6f04228 | 12000 | no | artifacts\beyond-title\3do-de-shiru-miru-asobu-nakajima-miyuki\captures\f012000.png |
| 3DO REAL - Tokubetsu Puremiamu CD - Hatsubai 1 Shuuren | 3DO REAL - Tokubetsu Puremiamu CD - Hatsubai 1 Shuuren (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | d4620a934c8f | 12000 | no | artifacts\beyond-title\3do-real-tokubetsu-puremiamu-cd-hatsubai-1-shuuren\captures\f012000.png |
| 3DO TES - 3DO Training Educational System | 3DO TES - 3DO Training Educational System (1995)(TG Information Network)(JP).chd | Panasonic FZ-10J | f6b24c00650c | 12000 | no | artifacts\beyond-title\3do-tes-3do-training-educational-system\captures\f012000.png |
| Adventures Of Captain J, The | Adventures Of Captain J, The (USA) (Unl).chd | Panasonic FZ-10 | 0a7231ab552c | 12000 | no | artifacts\beyond-title\adventures-of-captain-j\captures\f012000.png |
| Agi Gongnyong Goldie | Agi Gongnyong Goldie (Korea).chd | Panasonic FZ-10J | c71a0d77b8c7 | 12000 | no | artifacts\beyond-title\agi-gongnyong-goldie\captures\f012000.png |
| AI Shougi | AI Shougi (1995)(Taito)(JP).chd | Panasonic FZ-10J | a5e3c8b519f8 | 12000 | no | artifacts\beyond-title\ai-shougi\captures\f012000.png |
| Alone in the Dark | Alone in the Dark (1994)(Interplay)(US)[!].chd | Panasonic FZ-10 | 9f7d72bccaac | 12000 | no | artifacts\beyond-title\alone-in-dark\captures\f012000.png |
| Alone in the Dark 2 | Alone in the Dark 2 (1995)(Interplay)(US)[!].chd | Panasonic FZ-10 | 1d963e844356 | 12000 | no | artifacts\beyond-title\alone-in-dark-2\captures\f012000.png |
| Anno Mitsumasa - Peperon-mura no Shiki | Anno Mitsumasa - Peperon-mura no Shiki (1995)(Marubeni)(JP).chd | Panasonic FZ-10J | c881c8fbba12 | 12000 | no | artifacts\beyond-title\anno-mitsumasa-peperon-mura-no-shiki\captures\f012000.png |
| Aqua World - Umi Monogatari | Aqua World - Umi Monogatari (Japan).chd | Panasonic FZ-10J | c43b1fe8c768 | 12000 | no | artifacts\beyond-title\aqua-world-umi-monogatari\captures\f012000.png |
| Armageddon | Armageddon (1996)(LG)(KR).chd | Panasonic FZ-10J | 981ce2f03821 | 12000 | no | artifacts\beyond-title\armageddon\captures\f012000.png |
| AutoBahn Tokio | AutoBahn Tokio (1995)(Panasonic)(JP)(en)[!].chd | Panasonic FZ-10J | 7df1ad3e67c1 | 12000 | no | artifacts\beyond-title\autobahn-tokio\captures\f012000.png |
| Bakushou!! All Yoshimoto Quiz ou Ketteisen | Bakushou!! All Yoshimoto Quiz ou Ketteisen (1995)(Yoshimoto Kogyo)(JP)[!].chd | Panasonic FZ-10J | 34925fc5667c | 12000 | no | artifacts\beyond-title\bakushou-all-yoshimoto-quiz-ou-ketteisen\captures\f012000.png |
| Ballz - The Director's Cut | Ballz - The Director's Cut (1995)(Panasonic)[!].chd | Panasonic FZ-10J | 4b65a32f4091 | 12000 | no | artifacts\beyond-title\ballz-director-s-cut\captures\f012000.png |
| Battle Blues | Battle Blues (1996)(LG)(KR).chd | Panasonic FZ-10J | 616440c6f4a9 | 12000 | no | artifacts\beyond-title\battle-blues\captures\f012000.png |
| Battle Chess | Battle Chess (1993)(Interplay)(US)[!].chd | Panasonic FZ-10 | 8cc37a2391c0 | 12000 | no | artifacts\beyond-title\battle-chess\captures\f012000.png |
| Battle Pinball | Battle Pinball (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 693986f70318 | 12000 | no | artifacts\beyond-title\battle-pinball\captures\f012000.png |
| BattleSport | BattleSport (1995)(Studio 3DO)(EU)[!].chd | Panasonic FZ-10 | 1771ed98fb0a | 12000 | no | artifacts\beyond-title\battle-sport\captures\f012000.png |
| BC Racers | BC Racers (1995)(GoldStar)(EU-US)[!].chd | Panasonic FZ-10 | c8198cde66c1 | 12000 | no | artifacts\beyond-title\bc-racers\captures\f012000.png |
| Belzerion | Belzerion (1994)(Human)(JP)[!].chd | Panasonic FZ-10J | e2e9845b54e4 | 12000 | no | artifacts\beyond-title\belzerion\captures\f012000.png |
| BFS - Blue Forest Story | BFS - Blue Forest Story (Japan) (Alt).chd | Panasonic FZ-10J | 7ca7eb9db167 | 12000 | no | artifacts\beyond-title\bfs-blue-forest-story\captures\f012000.png |
| BioFury | BioFury (USA) (Unl).chd | Panasonic FZ-10 | 7d62cf1f0b93 | 12000 | no | artifacts\beyond-title\biofury\captures\f012000.png |
| Bishoujo Senshi Sailor Moon S | Bishoujo Senshi Sailor Moon S (1995)(Bandai)(JP)[!].chd | Panasonic FZ-10J | c3e4ef6873a0 | 12000 | no | artifacts\beyond-title\bishoujo-senshi-sailor-moon-s\captures\f012000.png |
| BladeForce | BladeForce (1995)(Studio 3DO)(EU)[!].chd | Panasonic FZ-10 | 759bfe498092 | 12000 | no | artifacts\beyond-title\bladeforce\captures\f012000.png |
| Blonde Justice | Blonde Justice (1995)(Vivid Interactive)(US).chd | Panasonic FZ-10 | 6488571318e9 | 12000 | no | artifacts\beyond-title\blonde-justice\captures\f012000.png |
| Blue Forest Monogatari | Blue Forest Monogatari (1996)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 42ac15557c85 | 12000 | no | artifacts\beyond-title\blue-forest-monogatari\captures\f012000.png |
| Bodyconscious Digital Rave! Part 1 - Shinjuku & Takasaki | Bodyconscious Digital Rave! Part 1 - Shinjuku & Takasaki (1994)(Transpegasus)(JP).chd | Panasonic FZ-10J | 7ce169702b5f | 12000 | no | artifacts\beyond-title\bodyconscious-digital-rave-part-1-shinjuku-and-takasaki\captures\f012000.png |
| Bonogurashi | Bonogurashi (1995)(Amuse)(JP).chd | Panasonic FZ-10J | e25c436f1904 | 12000 | no | artifacts\beyond-title\bonogurashi\captures\f012000.png |
| Brain Dead 13 Starring Fritz | Brain Dead 13 Starring Fritz (1995)(ReadySoft)(US)(Disc 1 of 2)[!].chd | Panasonic FZ-10 | ac072c5035d1 | 12000 | no | artifacts\beyond-title\brain-dead-13-starring-fritz\captures\f012000.png |
| Burning Soldier | Burning Soldier (1994)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 8a243b82d063 | 12000 | no | artifacts\beyond-title\burning-soldier\captures\f012000.png |
| Bust-A-Move | Bust-A-Move (1995)(Taito)(US).chd | Panasonic FZ-10 | caaad4dd60da | 12000 | no | artifacts\beyond-title\bust-move\captures\f012000.png |
| Cannon Fodder | Cannon Fodder (1994)(Virgin)(US)[!].chd | Panasonic FZ-10 | 285433697f2c | 12000 | no | artifacts\beyond-title\cannon-fodder\captures\f012000.png |
| Captain Quazar | Captain Quazar (1995)(Studio 3DO)(EU)[!].chd | Panasonic FZ-10 | 0a2babc6c777 | 12000 | no | artifacts\beyond-title\captain-quazar\captures\f012000.png |
| Carrier - Fortress at Sea | Carrier - Fortress at Sea (1995)(Panasonic)(US).chd | Panasonic FZ-10 | c5d29d397671 | 12000 | no | artifacts\beyond-title\carrier-fortress-at-sea\captures\f012000.png |
| Casper | Casper (1996)(Interplay)(US)[!].chd | Panasonic FZ-10 | f60fff308f82 | 12000 | no | artifacts\beyond-title\casper\captures\f012000.png |
| Chiki Chiki Machine Mou Race - Kenken to Black Maou no Ijiwaru Daisakusen | Chiki Chiki Machine Mou Race - Kenken to Black Maou no Ijiwaru Daisakusen (1994)(Future Pirates)(JP)[!].chd | Panasonic FZ-10J | 1aae20e90fc3 | 12000 | no | artifacts\beyond-title\chiki-chiki-machine-mou-race-kenken-to-black-maou-no-ijiwaru-daisakusen\captures\f012000.png |
| Chiki Chiki Machine Mou Race 2 - In Space | Chiki Chiki Machine Mou Race 2 - In Space (1995)(Future Pirates)(JP)[!].chd | Panasonic FZ-10J | 0a5baaa6e252 | 12000 | no | artifacts\beyond-title\chiki-chiki-machine-mou-race-2-in-space\captures\f012000.png |
| Chip-8 Emulator | Chip-8 Emulator (2016)(Gameblabla)[incl. Tetris ROM].chd | Panasonic FZ-10J | c2d1582441db | 12000 | no | artifacts\beyond-title\chip-8-emulator\captures\f012000.png |
| Club 3DO - Station Invasion | Club 3DO - Station Invasion (1994)(Studio 3DO)(US)[!].chd | Panasonic FZ-10 | 3549173ffb8a | 12000 | no | artifacts\beyond-title\club-3do-station-invasion\captures\f012000.png |
| Corpse Killer | Corpse Killer (1994)(Digital Pictures)(US)[!].chd | Panasonic FZ-10 | 4a39f30de6a3 | 12000 | no | artifacts\beyond-title\corpse-killer\captures\f012000.png |
| Coven, The | Coven, The (1994)(Vivid Interactive)(US).chd | Panasonic FZ-10 | 21d8d5ad3a70 | 12000 | no | artifacts\beyond-title\coven\captures\f012000.png |
| Cowboy Casino - Interactive Poker | Cowboy Casino - Interactive Poker (1994)(IntelliPlay)(US)[!].chd | Panasonic FZ-10 | 455ab8b3a179 | 12000 | no | artifacts\beyond-title\cowboy-casino-interactive-poker\captures\f012000.png |
| Crash 'n Burn | Crash 'n Burn (1993)(Crystal Dynamics)(US)[!][3DRM-1239120 1].chd | Panasonic FZ-10 | 217344b0b501 | 12000 | no | artifacts\beyond-title\crash-n-burn\captures\f012000.png |
| Crayon Shin-chan - Puzzle Daimaou no Nazo | Crayon Shin-chan - Puzzle Daimaou no Nazo (1995)(Bandai)(JP).chd | Panasonic FZ-10J | 9d12e91c0827 | 12000 | no | artifacts\beyond-title\crayon-shin-chan-puzzle-daimaou-no-nazo\captures\f012000.png |
| Creature Shock | Creature Shock (1995)(Virgin)(US)(Disc 1 of 2)[!].chd | Panasonic FZ-10 | 861bdc7de021 | 12000 | no | artifacts\beyond-title\creature-shock\captures\f012000.png |
| Crime Patrol | Crime Patrol (1994)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | e8fbe61ca223 | 12000 | no | artifacts\beyond-title\crime-patrol\captures\f012000.png |
| Crystal Borg in Hyperspace | Crystal Borg in Hyperspace (2017)(Optimus6128).chd | Panasonic FZ-10J | fe2be048be8c | 12000 | no | artifacts\beyond-title\crystal-borg-in-hyperspace\captures\f012000.png |
| Cyberdillo | Cyberdillo (1995)(Panasonic)(US)[!].chd | Panasonic FZ-10 | f29bc43c3c31 | 12000 | no | artifacts\beyond-title\cyberdillo\captures\f012000.png |
| Cyberia | Cyberia (1996)(Interplay)(US)[!].chd | Panasonic FZ-10 | 27a64cfcc9ef | 12000 | no | artifacts\beyond-title\cyberia\captures\f012000.png |
| D | D (1995)(Panasonic)(EU-US)(Disc 1 of 2)[!].chd | Panasonic FZ-10 | d6f4b4d17369 | 12000 | no | artifacts\beyond-title\d\captures\f012000.png |
| D no Shokutaku | D no Shokutaku (1995)(San-Ei Shobo Publishing)(JP)(Disc 1 of 2)[!].chd | Panasonic FZ-10J | 36a0885ac8ae | 12000 | no | artifacts\beyond-title\d-no-shokutaku\captures\f012000.png |
| D's Diner - The Director's Cut | D's Diner - The Director's Cut (1996)(Warp)(JP)(Disc 1 of 3)[!][Disk A].chd | Panasonic FZ-10J | 590f8b64b7b6 | 12000 | no | artifacts\beyond-title\d-s-diner-director-s-cut\captures\f012000.png |
| Daedalus Encounter - Episode 1 - Nanpasen no Alien | Daedalus Encounter - Episode 1 - Nanpasen no Alien (1995)(HATNET)(JP)(Disc 1 of 4)[!].chd | Panasonic FZ-10J | cb73dfbf76c9 | 12000 | no | artifacts\beyond-title\daedalus-encounter-episode-1-nanpasen-no-alien\captures\f012000.png |
| Daedalus Encounter, The | Daedalus Encounter, The (1995)(Panasonic)(US)(Disc 1 of 4)[!].chd | Panasonic FZ-10 | d1c4ab66fd42 | 12000 | no | artifacts\beyond-title\daedalus-encounter\captures\f012000.png |
| DeathKeep | DeathKeep (1995)(SSi)(US)[!].chd | Panasonic FZ-10 | fc8547d2ba7a | 12000 | no | artifacts\beyond-title\deathkeep\captures\f012000.png |
| Decathlon | Decathlon (2007)(Oldergames)[!].chd | Panasonic FZ-10 | 0e80be7a2574 | 12000 | no | artifacts\beyond-title\decathlon\captures\f012000.png |
| Defcon 5 | Defcon 5 (1995)(GoldStar)(EU)[!].chd | Panasonic FZ-10E (Anvil) | c17409b35447 | 12000 | no | artifacts\beyond-title\defcon-5\captures\f012000.png |
| Demolition Man | Demolition Man (1994)(Virgin)(US)[!].chd | Panasonic FZ-10 | cf43e6953ac0 | 12000 | no | artifacts\beyond-title\demolition-man\captures\f012000.png |
| Dennis Miller - That's News To Me | Dennis Miller - That's News To Me (1994)(Sanctuary Woods)(US)[!].chd | Panasonic FZ-10 | 8d74e0e9710e | 12000 | no | artifacts\beyond-title\dennis-miller-that-s-news-to-me\captures\f012000.png |
| Dennou Hyouryuu - Multimedia Cruising | Dennou Hyouryuu - Multimedia Cruising (1995)(Hamlet)(JP)[!].chd | Panasonic FZ-10J | ce3ea37bd40c | 12000 | no | artifacts\beyond-title\dennou-hyouryuu-multimedia-cruising\captures\f012000.png |
| Devil's Course | Devil's Course (1995)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 8d1f59b6702b | 12000 | no | artifacts\beyond-title\devil-s-course\captures\f012000.png |
| Digital Dreamware | Digital Dreamware (1995)(Virgin)(US)[!].chd | Panasonic FZ-10 | 808e705f74db | 12000 | no | artifacts\beyond-title\digital-dreamware\captures\f012000.png |
| DinoPark Tycoon | DinoPark Tycoon (1994)(MECC)(US).chd | Panasonic FZ-10 | cb8ee795fdb2 | 12000 | no | artifacts\beyond-title\dinopark-tycoon\captures\f012000.png |
| Doctor Hauzer | Doctor Hauzer (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | f99996de245b | 12000 | no | artifacts\beyond-title\doctor-hauzer\captures\f012000.png |
| Doom | Doom (1995)(Art Data)(US)[!].chd | Panasonic FZ-10 | 96be37ff5cfe | 12000 | no | artifacts\beyond-title\doom\captures\f012000.png |
| Doraemons, The - Yuujou Densetsu | Doraemons, The - Yuujou Densetsu (1995)(ShoPro)(JP)[!].chd | Panasonic FZ-10J | 467acb334f3b | 12000 | no | artifacts\beyond-title\doraemons-yuujou-densetsu\captures\f012000.png |
| Dragon Lore - The Legend Begins | Dragon Lore - The Legend Begins (1995)(Mindscape)(EU-US)(M4)(Disc 1 of 3)[!].chd | Panasonic FZ-10 | 1daebe43072d | 12000 | no | artifacts\beyond-title\dragon-lore-legend-begins\captures\f012000.png |
| Dragon Tycoon Edge | Dragon Tycoon Edge (1995)(Sanyo)(JP)[!].chd | Panasonic FZ-10J | b748306e476f | 12000 | no | artifacts\beyond-title\dragon-tycoon-edge\captures\f012000.png |
| Dragon's Lair | Dragon's Lair (1993)(ReadySoft)(US)[!][DH 65003-2].chd | Panasonic FZ-10 | b78199c41d85 | 12000 | no | artifacts\beyond-title\dragon-s-lair\captures\f012000.png |
| Drug Wars | Drug Wars (1995)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | f3bb8f835943 | 12000 | no | artifacts\beyond-title\drug-wars\captures\f012000.png |
| Eigo de Go! - Gateway to English | Eigo de Go! - Gateway to English (1996)(Gakken)(JP).chd | Panasonic FZ-10J | 4ae94cbd1ae0 | 12000 | no | artifacts\beyond-title\eigo-de-go-gateway-to-english\captures\f012000.png |
| EMIT Vol. 1 - Toki no Maigo | EMIT Vol. 1 - Toki no Maigo (1995)(KOEI)(JP)[!].chd | Panasonic FZ-10J | 194976848401 | 12000 | no | artifacts\beyond-title\emit-vol-1-toki-no-maigo\captures\f012000.png |
| EMIT Vol. 2 - Meigake no Tabi | EMIT Vol. 2 - Meigake no Tabi (1995)(KOEI)(JP)[!].chd | Panasonic FZ-10J | 38f1154bc07e | 12000 | no | artifacts\beyond-title\emit-vol-2-meigake-no-tabi\captures\f012000.png |
| EMIT Vol. 3 - Watashi ni Sayonara o | EMIT Vol. 3 - Watashi ni Sayonara o (1995)(KOEI)(JP)[!].chd | Panasonic FZ-10J | 20ff6433e741 | 12000 | no | artifacts\beyond-title\emit-vol-3-watashi-ni-sayonara-o\captures\f012000.png |
| Endlessly | Endlessly (1994)(Vivid Interactive)(EU-US)[!].chd | Panasonic FZ-10 | f7abcec1590d | 12000 | no | artifacts\beyond-title\endlessly\captures\f012000.png |
| Escape from Monster Manor | Escape from Monster Manor (Korea) (En,Ko).chd | Panasonic FZ-10J | bd2bab943077 | 12000 | no | artifacts\beyond-title\escape-from-monster-manor\captures\f012000.png |
| Escape from Monster Manor - A Terrifying Hunt for the Undead | Escape from Monster Manor - A Terrifying Hunt for the Undead (1993)(Electronic Arts)(US)[!].chd | Panasonic FZ-10 | 2e6f8eeec6f9 | 12000 | no | artifacts\beyond-title\escape-from-monster-manor-terrifying-hunt-for-undead\captures\f012000.png |
| ESPN Baseball - Interactive Hitting | ESPN Baseball - Interactive Hitting (1994)(IntelliPlay)(US)[!].chd | Panasonic FZ-10 | f77653345107 | 12000 | no | artifacts\beyond-title\espn-baseball-interactive-hitting\captures\f012000.png |
| ESPN Golf - Lower Your Score With Tom Kite - Shot Making | ESPN Golf - Lower Your Score With Tom Kite - Shot Making (1994)(IntelliPlay)(US)(Disc 1 of 3)[!][Shot Making Disc 1].chd | Panasonic FZ-10 | ae239b84cfad | 12000 | no | artifacts\beyond-title\espn-golf-lower-your-score-with-tom-kite-shot-making\captures\f012000.png |
| ESPN Golf - Tom Kite no Kore ga Golf Da! | ESPN Golf - Tom Kite no Kore ga Golf Da! (1994)(BYSE)(JP)(Disc 1 of 3)[!].chd | Panasonic FZ-10J | 0afe08d786a6 | 12000 | no | artifacts\beyond-title\espn-golf-tom-kite-no-kore-ga-golf-da\captures\f012000.png |
| ESPN Let's Go Skiing with Picabo Street, A. J. Kitt & Holly Flanders | ESPN Let's Go Skiing with Picabo Street, A. J. Kitt & Holly Flanders (1994)(IntelliPlay)(US)[!].chd | Panasonic FZ-10 | 959c2aa378a3 | 12000 | no | artifacts\beyond-title\espn-let-s-go-skiing-with-picabo-street-j-kitt-and-holly-flanders\captures\f012000.png |
| ESPN Let's Play Beach Volleyball with Karch Kiraly & Karolyn Kirby | ESPN Let's Play Beach Volleyball with Karch Kiraly & Karolyn Kirby (1993)(IntelliPlay)(US).chd | Panasonic FZ-10 | 6e24867dd516 | 12000 | no | artifacts\beyond-title\espn-let-s-play-beach-volleyball-with-karch-kiraly-and-karolyn-kirby\captures\f012000.png |
| ESPN Let's Play Soccer with John Harkes and Mia Hamm | ESPN Let's Play Soccer with John Harkes and Mia Hamm (1994)(IntelliPlay)(US)[!].chd | Panasonic FZ-10 | 16766448246e | 12000 | no | artifacts\beyond-title\espn-let-s-play-soccer-with-john-harkes-and-mia-hamm\captures\f012000.png |
| ESPN Let's Play Tennis with Tracy Austin | ESPN Let's Play Tennis with Tracy Austin (1994)(IntelliPlay)(US).chd | Panasonic FZ-10 | 4efc5a24d617 | 12000 | no | artifacts\beyond-title\espn-let-s-play-tennis-with-tracy-austin\captures\f012000.png |
| ESPN Step Aerobics | ESPN Step Aerobics (1994)(IntelliPlay)(US)[!].chd | Panasonic FZ-10 | 94d171db1ac0 | 12000 | no | artifacts\beyond-title\espn-step-aerobics\captures\f012000.png |
| Evil Australians - Rise of Stupidity | Evil Australians - Rise of Stupidity v1.1 (2017)(Gameblabla).chd | Panasonic FZ-10 (No RSA) | 2f8ce27067af | 12000 | no | artifacts\beyond-title\evil-australians-rise-of-stupidity\captures\f012000.png |
| Eye of Typhoon | Eye of Typhoon (Korea).chd | Panasonic FZ-10J | 11e49bbd8283 | 12000 | no | artifacts\beyond-title\eye-of-typhoon\captures\f012000.png |
| F1GP | F1GP (1995)(Pony Canyon)(JP).chd | Panasonic FZ-10J | 5f3bfa9e0202 | 12000 | no | artifacts\beyond-title\f1gp\captures\f012000.png |
| Family Feud | Family Feud (1994)(Gametek)(US)[!].chd | Panasonic FZ-10 | 55f8ccf1c26d | 12000 | no | artifacts\beyond-title\family-feud\captures\f012000.png |
| Fatty Bear's Birthday Surprise | Fatty Bear's Birthday Surprise (1993)(Humongous Entertainment)(US)[!].chd | Panasonic FZ-10 | 587d1337568e | 12000 | no | artifacts\beyond-title\fatty-bear-s-birthday-surprise\captures\f012000.png |
| Fatty Bear's Funpack | Fatty Bear's Funpack (1993)(Humongous Entertainment)(US)[!].chd | Panasonic FZ-10 | b3b6d74d3484 | 12000 | no | artifacts\beyond-title\fatty-bear-s-funpack\captures\f012000.png |
| FB Battery Navi Ver. II | FB Battery Navi Ver. II (Japan).chd | Panasonic FZ-10J | 95c0cf74429f | 12000 | no | artifacts\beyond-title\fb-battery-navi-ver-ii\captures\f012000.png |
| FIFA International Soccer | FIFA International Soccer (1994)(EA Sports)(EU)[!].chd | Panasonic FZ-10 | 24721bf41aec | 12000 | no | artifacts\beyond-title\fifa-international-soccer\captures\f012000.png |
| Fire Ball!! | Fire Ball!! (Japan).chd | Panasonic FZ-10J | 31a3b2e79ec3 | 12000 | no | artifacts\beyond-title\fire-ball\captures\f012000.png |
| Firewall | Firewall (Korea).chd | Panasonic FZ-10J | 7814b6b15af1 | 12000 | no | artifacts\beyond-title\firewall\captures\f012000.png |
| Flashback | Flashback (Japan) (En,Ja,Fr,De,It,Nl).chd | Panasonic FZ-10J | 9d03be869ad6 | 12000 | no | artifacts\beyond-title\flashback\captures\f012000.png |
| Flashback - The Quest for Identity | Flashback - The Quest for Identity (1994)(U.S. Gold)(US)(en-fr)[!].chd | Panasonic FZ-10 | 337813d7ecc5 | 12000 | no | artifacts\beyond-title\flashback-quest-for-identity\captures\f012000.png |
| Flopon World | Flopon World (Japan).chd | Panasonic FZ-10J | 29b5bdf985a8 | 12000 | no | artifacts\beyond-title\flopon-world\captures\f012000.png |
| Flying Nightmares | Flying Nightmares (1995)(Domark)(EU)[!].chd | Panasonic FZ-10 | 4022bbb529e7 | 12000 | no | artifacts\beyond-title\flying-nightmares\captures\f012000.png |
| Foes of Ali | Foes of Ali (1995)(EA Sports)(US)[!].chd | Panasonic FZ-10 | 65503973d966 | 12000 | no | artifacts\beyond-title\foes-of-ali\captures\f012000.png |
| Fun 'n Games | Fun 'n Games (1993)(Panasonic)(US)[!].chd | Panasonic FZ-10 | cb3a3ceacc25 | 12000 | no | artifacts\beyond-title\fun-n-games\captures\f012000.png |
| Fun 'n Games - Soft no Omochabai | Fun 'n Games - Soft no Omochabai (1994)(Virgin)(JP)(en-ja)[!].chd | Panasonic FZ-10J | 1cde5c0bc853 | 12000 | no | artifacts\beyond-title\fun-n-games-soft-no-omochabai\captures\f012000.png |
| Gakkou no Kowai Uwasa - Hanako-san ga Kita!! | Gakkou no Kowai Uwasa - Hanako-san ga Kita!! (1995)(Amuse)(JP)[!].chd | Panasonic FZ-10J | d452e2b3b2a2 | 12000 | no | artifacts\beyond-title\gakkou-no-kowai-uwasa-hanako-san-ga-kita\captures\f012000.png |
| Game no Tatsujin | Game no Tatsujin (1995)(Sunsoft)(JP)[!].chd | Panasonic FZ-10J | c780bc890092 | 12000 | no | artifacts\beyond-title\game-no-tatsujin\captures\f012000.png |
| Gameblabla's Crappy Compilation Rev 2 | Gameblabla's Crappy Compilation Rev 2 (2016)(Gameblabla).chd | Panasonic FZ-10J | c458068cb3c7 | 12000 | no | artifacts\beyond-title\gameblabla-s-crappy-compilation\captures\f012000.png |
| Gex | Gex (1995)(Crystal Dynamics)(EU-US)[!].chd | Panasonic FZ-10 | f73f7d08792c | 12000 | no | artifacts\beyond-title\gex\captures\f012000.png |
| Ghost Hunter Series - Kuroki Shi no Kamen | Ghost Hunter Series - Kuroki Shi no Kamen (Japan) (Rev 1).chd | Panasonic FZ-10J | 523b0ee18fe2 | 12000 | no | artifacts\beyond-title\ghost-hunter-series-kuroki-shi-no-kamen\captures\f012000.png |
| Goal FH | Goal FH (1995)(Carrozzeria Japan)(JP)[!].chd | Panasonic FZ-10J | 2cb7fe65aa7f | 12000 | no | artifacts\beyond-title\goal-fh\captures\f012000.png |
| Gridders | Gridders (1994)(GoldStar - Tetragon)(KR-US)(en)[!].chd | Panasonic FZ-10 | e08add77013e | 12000 | no | artifacts\beyond-title\gridders\captures\f012000.png |
| Grimm Meisaku Gekijou 1 - Bremen no Ongakutai | Grimm Meisaku Gekijou 1 - Bremen no Ongakutai (1994)(IMA)(JP).chd | Panasonic FZ-10J | 86e4ca2f0dd4 | 12000 | no | artifacts\beyond-title\grimm-meisaku-gekijou-1-bremen-no-ongakutai\captures\f012000.png |
| Grimm Meisaku Gekijou 2 - Hansel to Gretel | Grimm Meisaku Gekijou 2 - Hansel to Gretel (1994)(IMA)(JP)[!].chd | Panasonic FZ-10J | f30726e9ecf8 | 12000 | no | artifacts\beyond-title\grimm-meisaku-gekijou-2-hansel-to-gretel\captures\f012000.png |
| Grimm Meisaku Gekijou 3 - Akazukin | Grimm Meisaku Gekijou 3 - Akazukin (1994)(IMA)(JP)[!].chd | Panasonic FZ-10J | e8f5b4c35df1 | 12000 | no | artifacts\beyond-title\grimm-meisaku-gekijou-3-akazukin\captures\f012000.png |
| Guardian War | Guardian War (1994)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 169f02548967 | 12000 | no | artifacts\beyond-title\guardian-war\captures\f012000.png |
| Hell - A Cyberpunk Thriller | Hell - A Cyberpunk Thriller (1994)(Take 2)(US)[!].chd | Panasonic FZ-10 | 55a11e32392f | 12000 | no | artifacts\beyond-title\hell-cyberpunk-thriller\captures\f012000.png |
| Hello Kitty - Asobi no Omochabako | Hello Kitty - Asobi no Omochabako (Japan).chd | Panasonic FZ-10J | a4f10012746c | 12000 | no | artifacts\beyond-title\hello-kitty-asobi-no-omochabako\captures\f012000.png |
| Hirata Shougo Interactive Ehon - Aesop Monogatari Vol.1 | Hirata Shougo Interactive Ehon - Aesop Monogatari Vol.1 (1994)(ELCOM)(JP)(M3)[!].chd | Panasonic FZ-10J | 3269ce6e8b0f | 12000 | no | artifacts\beyond-title\hirata-shougo-interactive-ehon-aesop-monogatari-vol-1\captures\f012000.png |
| Hirata Shougo Interactive Ehon - Cinderella | Hirata Shougo Interactive Ehon - Cinderella (1995)(ELCOM)(JP)(M3).chd | Panasonic FZ-10J | 6cdc2543cb97 | 12000 | no | artifacts\beyond-title\hirata-shougo-interactive-ehon-cinderella\captures\f012000.png |
| Hirata Shougo Interactive Ehon - Ningyo-hime | Hirata Shougo Interactive Ehon - Ningyo-hime (Japan) (En,Ja,Fr).chd | Panasonic FZ-10J | 12b7c666f3e2 | 12000 | no | artifacts\beyond-title\hirata-shougo-interactive-ehon-ningyo-hime\captures\f012000.png |
| Hirata Shougo Interactive Ehon - Ookami to Nana Hiki no Ko Yagi | Hirata Shougo Interactive Ehon - Ookami to Nana Hiki no Ko Yagi (1995)(ELCOM)(JP)(M3).chd | Panasonic FZ-10J | cb73a647d49d | 12000 | no | artifacts\beyond-title\hirata-shougo-interactive-ehon-ookami-to-nana-hiki-no-ko-yagi\captures\f012000.png |
| Hirata Shougo Interactive Ehon - Sanbiki no Kobuta | Hirata Shougo Interactive Ehon - Sanbiki no Kobuta (1994)(ELCOM)(JP)(M3)[!].chd | Panasonic FZ-10J | 6a4e0b3a01c5 | 12000 | no | artifacts\beyond-title\hirata-shougo-interactive-ehon-sanbiki-no-kobuta\captures\f012000.png |
| Hirata Shougo Interactive Ehon - Shirayukihime | Hirata Shougo Interactive Ehon - Shirayukihime (1994)(ELCOM)(JP)(M3)[!].chd | Panasonic FZ-10J | 69c2348fcaf4 | 12000 | no | artifacts\beyond-title\hirata-shougo-interactive-ehon-shirayukihime\captures\f012000.png |
| Hitchcock ni Chousen | Hitchcock ni Chousen (Japan).chd | Panasonic FZ-10J | 5c0f50fed45d | 12000 | no | artifacts\beyond-title\hitchcock-ni-chousen\captures\f012000.png |
| Horde, The | Horde, The (1994)(Crystal Dynamics)(EU-US)[!].chd | Panasonic FZ-10 | 3d1b793df219 | 12000 | no | artifacts\beyond-title\horde\captures\f012000.png |
| Icebreaker | Icebreaker (1995)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 3cf36c7e217f | 12000 | no | artifacts\beyond-title\icebreaker\captures\f012000.png |
| Icebreaker II | Icebreaker II (USA) (Bonus Disk) (Unl).chd | Panasonic FZ-10 | 620f0b67a91f | 12000 | no | artifacts\beyond-title\icebreaker-ii\captures\f012000.png |
| Icebreaker II - The Game | Icebreaker II - The Game (2007)(Oldergames)[f 3DO Experience][AKA Icebreaker 2 2011 Edition].chd | Panasonic FZ-10J | 4cb57738ba14 | 12000 | no | artifacts\beyond-title\icebreaker-ii-game\captures\f012000.png |
| Ida Jiyouji Nightmare Interactive - Moon Cradle - Igyou no Hanayome | Ida Jiyouji Nightmare Interactive - Moon Cradle - Igyou no Hanayome (1995)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 5bc20a0aedf1 | 12000 | no | artifacts\beyond-title\ida-jiyouji-nightmare-interactive-moon-cradle-igyou-no-hanayome\captures\f012000.png |
| Ide Yousuke Meijin no Shin Jissen Mahjong | Ide Yousuke Meijin no Shin Jissen Mahjong (1996)(Capcom)(JP).chd | Panasonic FZ-10J | c5c1d9ac3597 | 12000 | no | artifacts\beyond-title\ide-yousuke-meijin-no-shin-jissen-mahjong\captures\f012000.png |
| Idol Janshi Suchie-Pai Special | Idol Janshi Suchie-Pai Special (1994)(Jaleco)(JP)[!].chd | Panasonic FZ-10J | 14ea7e0f9792 | 12000 | no | artifacts\beyond-title\idol-janshi-suchie-pai-special\captures\f012000.png |
| Idol Mahjong - Final Romance 2 - Hyper Edition | Idol Mahjong - Final Romance 2 - Hyper Edition (Japan).chd | Panasonic FZ-10J | 0a09a6496e04 | 12000 | no | artifacts\beyond-title\idol-mahjong-final-romance-2-hyper-edition\captures\f012000.png |
| Igo Time Trial - Shikatsu Daihyakka | Igo Time Trial - Shikatsu Daihyakka (Japan).chd | Panasonic FZ-10J | c7bd023b869d | 12000 | no | artifacts\beyond-title\igo-time-trial-shikatsu-daihyakka\captures\f012000.png |
| Igo Time Trial Najigo I | Igo Time Trial Najigo I (1994)(Ematec)(JP)[!].chd | Panasonic FZ-10J | f1d9293e72a9 | 12000 | no | artifacts\beyond-title\igo-time-trial-najigo-i\captures\f012000.png |
| Immercenary | Immercenary (1995)(Electronic Arts)(EU)[!].chd | Panasonic FZ-10 | 6a4523f345f6 | 12000 | no | artifacts\beyond-title\immercenary-combat-in-a-digital-world\captures\f012000.png |
| Immortal Desire | Immortal Desire (1994)(Vivid Interactive)(US).chd | Panasonic FZ-10 | 775e8e0a7813 | 12000 | no | artifacts\beyond-title\immortal-desire\captures\f012000.png |
| Incredible Machine, The | Incredible Machine, The (1994)(Dynamix)(US)[!].chd | Panasonic FZ-10 | bb4abe7ebabb | 12000 | no | artifacts\beyond-title\incredible-machine\captures\f012000.png |
| Insect War | Insect War (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 064bab53f39f | 12000 | no | artifacts\beyond-title\insect-war\captures\f012000.png |
| Interplay 3DO Buffet, The | Interplay 3DO Buffet, The (1995)(Interplay)(US)[!].chd | Panasonic FZ-10 | 5fd91ed76c50 | 12000 | no | artifacts\beyond-title\interplay-3do-buffet\captures\f012000.png |
| Iron Angel of the Apocalypse | Iron Angel of the Apocalypse (1994)(Synergy)(US)[!].chd | Panasonic FZ-10 | 127030e69392 | 12000 | no | artifacts\beyond-title\iron-angel-of-apocalypse\captures\f012000.png |
| Iron Angel of the Apocalypse - The Return | Iron Angel of the Apocalypse - The Return (1995)(Synergy)(US)[!].chd | Panasonic FZ-10 | abce4cf3f1cf | 12000 | no | artifacts\beyond-title\iron-angel-of-apocalypse-return\captures\f012000.png |
| Ishida Yoshio Kudan no Igo Seiha | Ishida Yoshio Kudan no Igo Seiha (1995)(Electronic Arts)(JP).chd | Panasonic FZ-10J | 4fde93713040 | 12000 | no | artifacts\beyond-title\ishida-yoshio-kudan-no-igo-seiha\captures\f012000.png |
| J. B. Harold - Blue Chicago Blues | J. B. Harold - Blue Chicago Blues (1995)(Riverhill Soft)(JP)(Disc 1 of 2)[!].chd | Panasonic FZ-10J | a0bd791ad0d6 | 12000 | no | artifacts\beyond-title\j-b-harold-blue-chicago-blues\captures\f012000.png |
| J.League Virtual Stadium | J.League Virtual Stadium (1994)(EA Sports)(JP)[!].chd | Panasonic FZ-10J | eb86bf87ddee | 12000 | no | artifacts\beyond-title\j-league-virtual-stadium\captures\f012000.png |
| J.League Virtual Stadium '95 | J.League Virtual Stadium '95 (1995)(EA Sports)(JP)[!].chd | Panasonic FZ-10J | 1bfe2bde3a2e | 12000 | no | artifacts\beyond-title\j-league-virtual-stadium-95\captures\f012000.png |
| Jammit | Jammit (1994)(GTE)(US)[!].chd | Panasonic FZ-10 | eca3deb4383c | 12000 | no | artifacts\beyond-title\jammit\captures\f012000.png |
| Jietai World | Jietai World (Japan).chd | Panasonic FZ-10J | 281bff088b7e | 12000 | no | artifacts\beyond-title\jietai-world\captures\f012000.png |
| Jikki Pachi Slot Simulator Vol. 1 | Jikki Pachi Slot Simulator Vol. 1 (1995)(Electronic Arts)(JP).chd | Panasonic FZ-10J | 64d8b51a2ea9 | 12000 | no | artifacts\beyond-title\jikki-pachi-slot-simulator-vol-1\captures\f012000.png |
| John Madden Football | John Madden Football (1994)(EA Sports)(US)[!][722007-2 70].chd | Panasonic FZ-10 | 58156d69b0a3 | 12000 | no | artifacts\beyond-title\john-madden-nfl-football\captures\f012000.png |
| Johnny Bazookatone | Johnny Bazookatone (1996)(U.S. Gold)(US)[!].chd | Panasonic FZ-10 | c13032f17663 | 12000 | no | artifacts\beyond-title\johnny-bazookatone\captures\f012000.png |
| Jurassic Park Interactive | Jurassic Park Interactive (1994)(Universal Interactive Studios)(US)[!].chd | Panasonic FZ-10 | 18036597b8d9 | 12000 | no | artifacts\beyond-title\jurassic-park-interactive\captures\f012000.png |
| Kakinoki Shogi | Kakinoki Shogi (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | f443f731baa3 | 12000 | no | artifacts\beyond-title\kakinoki-shogi\captures\f012000.png |
| Keiba Saishou no Housoku | Keiba Saishou no Housoku (1994)(Copya)(JP)[!].chd | Panasonic FZ-10J | a83bf8a3748b | 12000 | no | artifacts\beyond-title\keiba-saishou-no-housoku\captures\f012000.png |
| Kero Kero Keroppi to Origami no Tabibito | Kero Kero Keroppi to Origami no Tabibito (1995)(Mizuki)(JP)[!].chd | Panasonic FZ-10J | 23b3ea0560f3 | 12000 | no | artifacts\beyond-title\kero-kero-keroppi-to-origami-no-tabibito\captures\f012000.png |
| Killing Time | Killing Time (1995)(Studio 3DO)(EU)[!].chd | Panasonic FZ-10 | 2ddbb4aa783d | 12000 | no | artifacts\beyond-title\killing-time\captures\f012000.png |
| Kingdom - The Far Reaches | Kingdom - The Far Reaches (1994)(Interplay)(EU-US)[!].chd | Panasonic FZ-10 | cee431e238b8 | 12000 | no | artifacts\beyond-title\kingdom-far-reaches\captures\f012000.png |
| Konpeki no Kantai | Konpeki no Kantai (Japan) (Rev 1).chd | Panasonic FZ-10J | e29c363c6fde | 12000 | no | artifacts\beyond-title\konpeki-no-kantai\captures\f012000.png |
| Kyuumei Kyuukyuu Teate no ABC | Kyuumei Kyuukyuu Teate no ABC (Japan).chd | Panasonic FZ-10J | 5f1bcfba295a | 12000 | no | artifacts\beyond-title\kyuumei-kyuukyuu-teate-no-abc\captures\f012000.png |
| Kyuusei Senjutsu ni Yoru - Heisei Kaiun Koyomi | Kyuusei Senjutsu ni Yoru - Heisei Kaiun Koyomi (1994)(Ariadne)(JP)[!].chd | Panasonic FZ-10J | 5f3be3177abe | 12000 | no | artifacts\beyond-title\kyuusei-senjutsu-ni-yoru-heisei-kaiun-koyomi\captures\f012000.png |
| Last Bounty Hunter, The | Last Bounty Hunter, The (1994)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | f59975a5af73 | 12000 | no | artifacts\beyond-title\last-bounty-hunter\captures\f012000.png |
| Lemmings | Lemmings (1993)(Psygnosis)(US)[!].chd | Panasonic FZ-10 | 0be38b22987b | 12000 | no | artifacts\beyond-title\lemmings\captures\f012000.png |
| Life Stage, The - Virtual House | Life Stage, The - Virtual House (1993)(GoldStar - Panasonic)(KR-US)(en)[!].chd | Panasonic FZ-10 | 358149fd24d6 | 12000 | no | artifacts\beyond-title\life-stage-virtual-house\captures\f012000.png |
| Little House, The | Little House, The (1995)(MC International)(JP).chd | Panasonic FZ-10J | fec767734049 | 12000 | no | artifacts\beyond-title\little-house\captures\f012000.png |
| Lost Dungeon | Lost Dungeon (1995)(T&E Soft)(JP)[!].chd | Panasonic FZ-10J | 6d1eca0be542 | 12000 | no | artifacts\beyond-title\lost-dungeon\captures\f012000.png |
| Lost Eden | Lost Eden (1993)(Virgin)(US)(M5).chd | Panasonic FZ-10 | bd2bc6609858 | 12000 | no | artifacts\beyond-title\lost-eden\captures\f012000.png |
| Lost Files of Sherlock Holmes, The | Lost Files of Sherlock Holmes, The (1994)(Electronic Arts)(US)[!].chd | Panasonic FZ-10 | f06976ff1272 | 12000 | no | artifacts\beyond-title\lost-files-of-sherlock-holmes\captures\f012000.png |
| Love Bites | Love Bites (1994)(Vivid Interactive)(US)[!].chd | Panasonic FZ-10 | 8e99acd511e3 | 12000 | no | artifacts\beyond-title\love-bites\captures\f012000.png |
| Lucienne's Quest | Lucienne's Quest (1996)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 5b49e606319d | 12000 | no | artifacts\beyond-title\lucienne-s-quest\captures\f012000.png |
| Macaroni Houren Shou Interactive | Macaroni Houren Shou Interactive (1995)(Toshiba EMI)(JP)[!].chd | Panasonic FZ-10J | a23a837b3dd7 | 12000 | no | artifacts\beyond-title\macaroni-houren-shou-interactive\captures\f012000.png |
| Mad Dog II - The Lost Gold | Mad Dog II - The Lost Gold (1994)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | a94440520fbd | 12000 | no | artifacts\beyond-title\mad-dog-ii-lost-gold\captures\f012000.png |
| Mad Dog McCree | Mad Dog McCree (1993)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | 9b55d2822b3a | 12000 | no | artifacts\beyond-title\mad-dog-mccree\captures\f012000.png |
| Mahjong Gokuu Tenjiku | Mahjong Gokuu Tenjiku (1994)(ASCII)(JP)[!].chd | Panasonic FZ-10J | 3b203c500627 | 12000 | no | artifacts\beyond-title\mahjong-gokuu-tenjiku\captures\f012000.png |
| Mahjong Kyou Jidai - AV Gal Seifuku Hen | Mahjong Kyou Jidai - AV Gal Seifuku Hen (1994)(Micronet)(JP)[!].chd | Panasonic FZ-10J | 2bd9b14074ac | 12000 | no | artifacts\beyond-title\mahjong-kyou-jidai-av-gal-seifuku-hen\captures\f012000.png |
| Mahjong Kyou Jidai - Cogal Houkago Hen | Mahjong Kyou Jidai - Cogal Houkago Hen (1995)(Micronet)(JP).chd | Panasonic FZ-10J | 891e6a482d71 | 12000 | no | artifacts\beyond-title\mahjong-kyou-jidai-cogal-houkago-hen\captures\f012000.png |
| Marine Tour - Diving Spot Guide | Marine Tour - Diving Spot Guide (1994)(Office Create)(JP)[!].chd | Panasonic FZ-10J | 29de5aa35730 | 12000 | no | artifacts\beyond-title\marine-tour-diving-spot-guide\captures\f012000.png |
| Masters Harukanaru Augusta 3 | Masters Harukanaru Augusta 3 (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | aeb43da63d3e | 12000 | no | artifacts\beyond-title\masters-harukanaru-augusta-3\captures\f012000.png |
| Mathemagics - An Interactive Learning Cube | Mathemagics - An Interactive Learning Cube (1994)(L3)(US)[!].chd | Panasonic FZ-10 | f242913f20dc | 12000 | no | artifacts\beyond-title\mathemagics-interactive-learning-cube\captures\f012000.png |
| Mazer | Mazer (1995)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | 47165d9145eb | 12000 | no | artifacts\beyond-title\mazer\captures\f012000.png |
| MegaRace | MegaRace (1994)(Mindscape)(EU-US)(M5)[!].chd | Panasonic FZ-10 | 935adb1b3d47 | 12000 | no | artifacts\beyond-title\megarace\captures\f012000.png |
| Memory Gwanli CD | Memory Gwanli CD (Korea) (RE).chd | Panasonic FZ-10J | f846b269522b | 12000 | no | artifacts\beyond-title\memory-gwanli-cd\captures\f012000.png |
| Microcosm | Microcosm (1994)(Psygnosis)(US)[!].chd | Panasonic FZ-10 | b35c911d969f | 12000 | no | artifacts\beyond-title\microcosm\captures\f012000.png |
| Mind Teazzer | Mind Teazzer (1994)(Vivid Interactive)(US)[!].chd | Panasonic FZ-10 | a3076373f12f | 12000 | no | artifacts\beyond-title\mind-teazzer\captures\f012000.png |
| Mirai Shounen Digital Library - Conan - The Boy in Future | Mirai Shounen Digital Library - Conan - The Boy in Future (1995)(Emotional Digital Software)(JP)[!].chd | Panasonic FZ-10J | d8ac5b9ae27e | 12000 | no | artifacts\beyond-title\mirai-shounen-digital-library-conan-boy-in-future\captures\f012000.png |
| Mono Shiri Jiyuugaku - Ogura Hyakunin Isshu-hen | Mono Shiri Jiyuugaku - Ogura Hyakunin Isshu-hen (1995)(Shinko Human Create)(JP).chd | Panasonic FZ-10J | 1da319edc3e1 | 12000 | no | artifacts\beyond-title\mono-shiri-jiyuugaku-ogura-hyakunin-isshu-hen\captures\f012000.png |
| Montana Jones | Montana Jones (1995)(Future Pirates Junior)(JP)[!].chd | Panasonic FZ-10J | 1f311a641002 | 12000 | no | artifacts\beyond-title\montana-jones\captures\f012000.png |
| Multimedia Shinsho - Driving School | Multimedia Shinsho - Driving School (Futsuu Menkyo Gakka-hen) (1995)(JISC Life Support)(JP).chd | Panasonic FZ-10J | b6c5cc9cb2ee | 12000 | no | artifacts\beyond-title\multimedia-shinsho-driving-school\captures\f012000.png |
| Murphy Dayo Zenin Shuugou!! - Murphy's TV CD-ROM | Murphy Dayo Zenin Shuugou!! - Murphy's TV CD-ROM (1994)(Vantan International)(JP)(en-ja)[!].chd | Panasonic FZ-10J | 0912fdf85f4b | 12000 | no | artifacts\beyond-title\murphy-dayo-zenin-shuugou-murphy-s-tv-cd-rom\captures\f012000.png |
| Myst | Myst (1994)(Panasonic)(US)[!].chd | Panasonic FZ-10 | e907558e7d16 | 12000 | no | artifacts\beyond-title\myst\captures\f012000.png |
| N. O. B. - Neo Organic Bioform | N. O. B. - Neo Organic Bioform (1995)(Sanyo)(JP).chd | Panasonic FZ-10J | d9167cf7506b | 12000 | no | artifacts\beyond-title\n-o-b-neo-organic-bioform\captures\f012000.png |
| Nais How's 1 - Front How's '95-'96 | Nais How's 1 - Front How's '95-'96 v1.0 R2.0 (1995)(National)(JP).chd | Panasonic FZ-10J | dc569f81706c | 12000 | no | artifacts\beyond-title\nais-how-s-1-front-how-s-95-96\captures\f012000.png |
| Naoko to Hide Suekane - Sansuu no Tensai 1 | Naoko to Hide Suekane - Sansuu no Tensai 1 (1995)(Gakuman)(JP).chd | Panasonic FZ-10J | a454e7b6e1fd | 12000 | no | artifacts\beyond-title\naoko-to-hide-suekane-sansuu-no-tensai-1\captures\f012000.png |
| Naoko to Hide-bou - Sansuu no Tensai 1 - Wa to Sa no Bunshoudai Tsurukamezan Part I | Naoko to Hide-bou - Sansuu no Tensai 1 - Wa to Sa no Bunshoudai Tsurukamezan Part I (Butsubutsu Koukan no Maki) (Japan).chd | Panasonic FZ-10J | c49b671467b4 | 12000 | no | artifacts\beyond-title\naoko-to-hide-bou-sansuu-no-tensai-1-wa-to-sa-no-bunshoudai-tsurukamezan-part-i\captures\f012000.png |
| Naoko to Hide-bou - Sansuu no Tensai 2 - Hayasa no Bunshoudai Tsuukasan Part I | Naoko to Hide-bou - Sansuu no Tensai 2 - Hayasa no Bunshoudai Tsuukasan Part I (Japan).chd | Panasonic FZ-10J | 7c6af0b7396a | 12000 | no | artifacts\beyond-title\naoko-to-hide-bou-sansuu-no-tensai-2-hayasa-no-bunshoudai-tsuukasan-part-i\captures\f012000.png |
| National-Panasonic Collection | National-Panasonic Collection (Japan).chd | Panasonic FZ-10J | 0d01cd2b9ebc | 12000 | no | artifacts\beyond-title\national-panasonic-collection\captures\f012000.png |
| Need for Speed, The | Need for Speed, The (1994)(Electronic Arts)(EU-US)[!].chd | Panasonic FZ-10 | 9ec810c593e6 | 12000 | no | artifacts\beyond-title\need-for-speed\captures\f012000.png |
| Nemurenu Yoru no Chiisa Nao Hanashi | Nemurenu Yoru no Chiisa Nao Hanashi (1994)(Amuse)(JP).chd | Panasonic FZ-10J | a3ece7e9ab66 | 12000 | no | artifacts\beyond-title\nemurenu-yoru-no-chiisa-nao-hanashi\captures\f012000.png |
| NeuroDancer - Journey Into The Neuronet | NeuroDancer - Journey Into The Neuronet (1994)(Pixis Interactive)(US).chd | Panasonic FZ-10 | 26fc0c600aff | 12000 | no | artifacts\beyond-title\neurodancer-journey-into-neuronet\captures\f012000.png |
| New How's 1 - Front How's '94-'95 | New How's 1 - Front How's '94-'95 v1.0 (1994)(National)(JP)[!].chd | Panasonic FZ-10J | 0f67a977ba58 | 12000 | no | artifacts\beyond-title\new-how-s-1-front-how-s-94-95\captures\f012000.png |
| Nice Body - For Professional Use - Oar Star Suieitaikai Official CD-ROM | Nice Body - For Professional Use - Oar Star Suieitaikai Official CD-ROM (1995)(Fuji Television)(JP)[!].chd | Panasonic FZ-10J | a854d093a380 | 12000 | no | artifacts\beyond-title\nice-body-for-professional-use-oar-star-suieitaikai-official-cd-rom\captures\f012000.png |
| Night Trap | Night Trap (1993)(Digital Pictures)(US)(Disc 1 of 2)[!].chd | Panasonic FZ-10 | 7c634556655a | 12000 | no | artifacts\beyond-title\night-trap\captures\f012000.png |
| Nishimura Keitarou Travel Mystery - Akugyaku no Kisetsu | Nishimura Keitarou Travel Mystery - Akugyaku no Kisetsu (1994)(Panasonic)(JP)(Disc 1 of 2)[!].chd | Panasonic FZ-10J | f8db741f714a | 12000 | no | artifacts\beyond-title\nishimura-keitarou-travel-mystery-akugyaku-no-kisetsu\captures\f012000.png |
| Nobunaga no Yabou - Haouden | Nobunaga no Yabou - Haouden (1994)(KOEI)(JP)[!].chd | Panasonic FZ-10J | 8cd6761de811 | 12000 | no | artifacts\beyond-title\nobunaga-no-yabou-haouden\captures\f012000.png |
| Nontan to Issho - Hoshi no Okurimono | Nontan to Issho - Hoshi no Okurimono (1995)(Panasonic)(JP).chd | Panasonic FZ-10J | 6efa7f4ef5a9 | 12000 | no | artifacts\beyond-title\nontan-to-issho-hoshi-no-okurimono\captures\f012000.png |
| Nontan to Issho - Nohara de Asobo | Nontan to Issho - Nohara de Asobo (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 12126b4fade8 | 12000 | no | artifacts\beyond-title\nontan-to-issho-nohara-de-asobo\captures\f012000.png |
| Novastorm | Novastorm (1994)(Psygnosis)(US)[!].chd | Panasonic FZ-10 | ebe0915c10d5 | 12000 | no | artifacts\beyond-title\nova-storm\captures\f012000.png |
| Oceans Below | Oceans Below (1993)(Software Toolworks)(US)[!].chd | Panasonic FZ-10 | 66964470230b | 12000 | no | artifacts\beyond-title\oceans-below\captures\f012000.png |
| Off-world Interceptor | Off-world Interceptor (1994)(Crystal Dynamics)(EU-US)[!].chd | Panasonic FZ-10 | 661de8940a88 | 12000 | no | artifacts\beyond-title\off-world-interceptor\captures\f012000.png |
| Ogura Hyakunin Isshu | Ogura Hyakunin Isshu (Japan).chd | Panasonic FZ-10J | 3e72649d548a | 12000 | no | artifacts\beyond-title\ogura-hyakunin-isshu\captures\f012000.png |
| Oilman | Oilman (Japan).chd | Panasonic FZ-10J | f9b2127eeab7 | 12000 | no | artifacts\beyond-title\oilman\captures\f012000.png |
| Olympic Soccer | Olympic Soccer (1996)(Panasonic)(US)(M5)[!].chd | Panasonic FZ-10 | 8bacb7ff83b8 | 12000 | no | artifacts\beyond-title\olympic-soccer\captures\f012000.png |
| Olympic Summer Games | Olympic Summer Games (1996)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 870f95cdb834 | 12000 | no | artifacts\beyond-title\olympic-summer-games\captures\f012000.png |
| Onee-san to Issho! Janken Paradise | Onee-san to Issho! Janken Paradise (Japan).chd | Panasonic FZ-10J | ffc95fb4ee70 | 12000 | no | artifacts\beyond-title\onee-san-to-issho-janken-paradise\captures\f012000.png |
| Onee-san to Issho! Kisekae Paradise | Onee-san to Issho! Kisekae Paradise (Japan).chd | Panasonic FZ-10J | e42987e23848 | 12000 | no | artifacts\beyond-title\onee-san-to-issho-kisekae-paradise\captures\f012000.png |
| OpenLara | OpenLara (2021-12-20)(XProger)(BSD2).chd | Panasonic FZ-10J | 3d53b5c402da | 12000 | no | artifacts\beyond-title\openlara\captures\f012000.png |
| Orbatak | Orbatak (USA) (Arcade CD-ROM).chd | Panasonic FZ-10 (No RSA) | b6b3f5b2ca2b | 12000 | no | artifacts\beyond-title\orbatak\captures\f012000.png |
| Oukoku no Grand Chef | Oukoku no Grand Chef (1996)(Sala International)(JP).chd | Panasonic FZ-10J | 32e53ff30198 | 12000 | no | artifacts\beyond-title\oukoku-no-grand-chef\captures\f012000.png |
| Out of this World | Out of this World (1993)(Interplay)(US)[!][45097-1].chd | Panasonic FZ-10 | 00256a21c947 | 12000 | no | artifacts\beyond-title\another-world\captures\f012000.png |
| Outer World | Outer World (1994)(Electronic Arts)(JP)(en)[!].chd | Panasonic FZ-10J | 09ae74c3c873 | 12000 | no | artifacts\beyond-title\outer-world\captures\f012000.png |
| OverDrivin' | OverDrivin' (1994)(Electronic Arts)(JP)[!].chd | Panasonic FZ-10J | b11853ca63c2 | 12000 | no | artifacts\beyond-title\overdrivin\captures\f012000.png |
| Oyaji Hunter Mahjong | Oyaji Hunter Mahjong (1995)(Warp)(JP)[!].chd | Panasonic FZ-10J | d2e4f08fb435 | 12000 | no | artifacts\beyond-title\oyaji-hunter-mahjong\captures\f012000.png |
| Paddock Note '95 | Paddock Note '95 (1995)(Fuji Television)(JP)[!].chd | Panasonic FZ-10J | fd1dfdbec912 | 12000 | no | artifacts\beyond-title\paddock-note-95\captures\f012000.png |
| Panasonic REAL 3DO Interactive Multiplayer - Demonstration CD | Panasonic REAL 3DO Interactive Multiplayer - Demonstration CD (1993)(Panasonic)(US).chd | Panasonic FZ-10 | 58ff39c2681e | 12000 | no | artifacts\beyond-title\panasonic-real-3do-interactive-multiplayer-demonstration-cd\captures\f012000.png |
| Panasonic Special CD-ROM | Panasonic Special CD-ROM (1995)(Panasonic)(JP).chd | Panasonic FZ-10J | f4c6076e8884 | 12000 | no | artifacts\beyond-title\panasonic-special-cd-rom\captures\f012000.png |
| Panzer General | Panzer General (1995)(Mindscape - SSi)(EU-US)[!].chd | Panasonic FZ-10 | 536cdb7197c7 | 12000 | no | artifacts\beyond-title\panzer-general\captures\f012000.png |
| Pararancho - The Incredible Machine | Pararancho - The Incredible Machine (1994)(T&E Soft)(JP)[!].chd | Panasonic FZ-10J | 83b9f8610ce5 | 12000 | no | artifacts\beyond-title\pararancho-incredible-machine\captures\f012000.png |
| PaTaank | PaTaank (1994)(PF.Magic)(EU-US)[!].chd | Panasonic FZ-10 | 31ee17d5168b | 12000 | no | artifacts\beyond-title\pataank\captures\f012000.png |
| Pebble Beach Golf Links | Pebble Beach Golf Links (1993)(GoldStar - Panasonic)(KR-US)(en)[!].chd | Panasonic FZ-10 | 701eb31de9ae | 12000 | no | artifacts\beyond-title\pebble-beach-golf-links\captures\f012000.png |
| Pebble Beach no Hatou | Pebble Beach no Hatou (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | ea92f4786a5f | 12000 | no | artifacts\beyond-title\pebble-beach-no-hatou\captures\f012000.png |
| Penthouse Interactive - Virtual Photo Shoot Vol. 1 | Penthouse Interactive - Virtual Photo Shoot Vol. 1 (1994)(GAGA)(JP).chd | Panasonic FZ-10J | 944c26f2a3ae | 12000 | no | artifacts\beyond-title\penthouse-interactive-virtual-photo-shoot-vol-1\captures\f012000.png |
| Perfect General, The | Perfect General, The (1995)(Kirin Entertainment)(US)[!].chd | Panasonic FZ-10 | 6a70a098ac04 | 12000 | no | artifacts\beyond-title\perfect-general\captures\f012000.png |
| Perfect World - Combat in a Digital World | Perfect World - Combat in a Digital World (1995)(Electronic Arts)(JP)[!].chd | Panasonic FZ-10J | ee077fdd251e | 12000 | no | artifacts\beyond-title\perfect-world-combat-in-digital-world\captures\f012000.png |
| Peter Frankl - Puzzle no Tou | Peter Frankl - Puzzle no Tou (1995)(TBS Multimedia)(JP)(en-ja)[!].chd | Panasonic FZ-10J | 61bda52faf10 | 12000 | no | artifacts\beyond-title\peter-frankl-puzzle-no-tou\captures\f012000.png |
| PGA Tour 96 | PGA Tour 96 (1995)(EA Sports)(EU)[!].chd | Panasonic FZ-10 | b8f303f065ca | 12000 | no | artifacts\beyond-title\pga-tour-96\captures\f012000.png |
| Phoenix 3 | Phoenix 3 (1995)(Studio 3DO)(EU)[!].chd | Panasonic FZ-10 | c39e4193cdb0 | 12000 | no | artifacts\beyond-title\phoenix-3\captures\f012000.png |
| Plumbers Don't Wear Ties | Plumbers Don't Wear Ties (1994)(Kirin Entertainment)(US)[!].chd | Panasonic FZ-10 | 1e6d3539091f | 12000 | no | artifacts\beyond-title\plumbers-don-t-wear-ties\captures\f012000.png |
| PO'ed | PO'ed (1995)(Studio 3DO)(EU-US)[!].chd | Panasonic FZ-10 | 2fd69cad68a0 | 12000 | no | artifacts\beyond-title\po-ed\captures\f012000.png |
| Policenauts | Policenauts (1995)(Konami)(JP)(Disc 1 of 2)[!].chd | Panasonic FZ-10J | 9a293b562479 | 12000 | no | artifacts\beyond-title\policenauts\captures\f012000.png |
| Policenauts - Pilot Disk | Policenauts - Pilot Disk (1995)(Konami)(JP)[!].chd | Panasonic FZ-10J | 113842b2169f | 12000 | no | artifacts\beyond-title\policenauts-pilot-disk\captures\f012000.png |
| Powers Kingdom | Powers Kingdom (1994)(Panasonic)(EU)[!].chd | Panasonic FZ-10E (Anvil) | 14f7638d69ec | 12000 | no | artifacts\beyond-title\powers-kingdom\captures\f012000.png |
| Powerslide | Powerslide (2007)(Oldergames)(Disc 2 of 2)[!][Powerslide Bonus Disk - Footie].chd | Panasonic FZ-10J | e31e74eb2774 | 12000 | no | artifacts\beyond-title\powerslide\captures\f012000.png |
| Primal Rage | Primal Rage v1.0 (1995)(GoldStar)(EU-US)(M7)[!].chd | Panasonic FZ-10 | ac774f767560 | 12000 | no | artifacts\beyond-title\primal-rage\captures\f012000.png |
| Princess Maker 2 | Princess Maker 2 (1995)(Micro Cabin)(JP)[!].chd | Panasonic FZ-10J | f0ed0be4fac1 | 12000 | no | artifacts\beyond-title\princess-maker-2\captures\f012000.png |
| Pro Stadium | Pro Stadium (1995)(Sanyo)(JP)[!].chd | Panasonic FZ-10J | 96c99d845e48 | 12000 | no | artifacts\beyond-title\pro-stadium\captures\f012000.png |
| Pro Yakyuu Virtual Stadium - Professional Baseball | Pro Yakyuu Virtual Stadium - Professional Baseball (1995)(EA Sports)(JP)[!].chd | Panasonic FZ-10J | 478c86ff2617 | 12000 | no | artifacts\beyond-title\pro-yakyuu-virtual-stadium-professional-baseball\captures\f012000.png |
| Psychic Detective | Psychic Detective (1995)(Electronic Arts)(EU)(Disc 1 of 3)[!].chd | Panasonic FZ-10 | 193555448b04 | 12000 | no | artifacts\beyond-title\psychic-detective\captures\f012000.png |
| Puppet Tale | Puppet Tale (1994)(Microcabin - Panasonic)(JP)[!].chd | Panasonic FZ-10J | d9c961a3986b | 12000 | no | artifacts\beyond-title\puppet-tale\captures\f012000.png |
| Putt-Putt Goes to the Moon | Putt-Putt Goes to the Moon (1993)(Humongous Entertainment)(US)[!].chd | Panasonic FZ-10 | 1fec4ec73348 | 12000 | no | artifacts\beyond-title\putt-putt-goes-to-moon\captures\f012000.png |
| Putt-Putt Joins the Parade | Putt-Putt Joins the Parade (1993)(Humongous Entertainment)(US)[!][CQ 6922-2].chd | Panasonic FZ-10 | 534ae001d907 | 12000 | no | artifacts\beyond-title\putt-putt-joins-parade\captures\f012000.png |
| Putt-Putt to Parade ni Ikou! | Putt-Putt to Parade ni Ikou! (1994)(Media Vision)(JP)(en-ja)[!].chd | Panasonic FZ-10J | 754d9779752e | 12000 | no | artifacts\beyond-title\putt-putt-to-parade-ni-ikou\captures\f012000.png |
| Putt-Putt's Fun Pack | Putt-Putt's Fun Pack (1993)(Humongous Entertainment)(US)[!].chd | Panasonic FZ-10 | 8f1bb94cf06b | 12000 | no | artifacts\beyond-title\putt-putt-s-fun-pack\captures\f012000.png |
| Puzzle Bobble | Puzzle Bobble (1995)(Micro Cabin)(JP)[!].chd | Panasonic FZ-10J | 4cf3778f41ad | 12000 | no | artifacts\beyond-title\bust-a-move\captures\f012000.png |
| Pyramid Intruder | Pyramid Intruder (1995)(Taito)(JP)(en)[!].chd | Panasonic FZ-10J | 04d3fe945f34 | 12000 | no | artifacts\beyond-title\pyramid-intruder\captures\f012000.png |
| Quarantine | Quarantine (1994)(Gametek - Imagineer)(JP-US)(M5)[!][IM-TS9503, 439480068501RE3].chd | Panasonic FZ-10J | 8dad7d7e8f77 | 12000 | no | artifacts\beyond-title\quarantine\captures\f012000.png |
| Quarterback Attack - The Professional Quarterback Simulator | Quarterback Attack - The Professional Quarterback Simulator (1995)(Digital Pictures)(US)[!].chd | Panasonic FZ-10 | 9897452b9145 | 12000 | no | artifacts\beyond-title\quarterback-attack-professional-quarterback-simulator\captures\f012000.png |
| Real Pinball | Real Pinball (1994)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 1ea203a314cd | 12000 | no | artifacts\beyond-title\real-pinball\captures\f012000.png |
| Reika - Virtual Puppet | Reika - Virtual Puppet (1994)(H.A.N.D)(JP).chd | Panasonic FZ-10J | 5abd7ef062af | 12000 | no | artifacts\beyond-title\reika-virtual-puppet\captures\f012000.png |
| Remute 3D | Remute 3D (Europe) (En,De) (Unl).chd | Panasonic FZ-10E (Anvil) | 3331db41068b | 12000 | no | artifacts\beyond-title\remute-3d\captures\f012000.png |
| Return Fire | Return Fire v1.0 F306.1.2 (1994)(Prolific - Silent Software)(EU-US)[!].chd | Panasonic FZ-10 | a8c7b8ffc2af | 12000 | no | artifacts\beyond-title\return-fire\captures\f012000.png |
| Rise of the Robots | Rise of the Robots (1995)(Absolute)(US)(M3)[!].chd | Panasonic FZ-10 | a3704048f8fa | 12000 | no | artifacts\beyond-title\rise-of-robots\captures\f012000.png |
| Road Rash | Road Rash (1994)(Electronic Arts)(US)[!].chd | Panasonic FZ-10 | d76725503d3a | 12000 | no | artifacts\beyond-title\road-rash\captures\f012000.png |
| Robinson's Requiem | Robinson's Requiem (1996)(ReadySoft - Silmarils)(US)[!].chd | Panasonic FZ-10 | 6cf870fccfc8 | 12000 | no | artifacts\beyond-title\robinson-s-requiem\captures\f012000.png |
| Royal Pro Wrestling - Jikkyou Live!! | Royal Pro Wrestling - Jikkyou Live!! (1996)(Natsume)(JP)[!].chd | Panasonic FZ-10J | d55a86f2485f | 12000 | no | artifacts\beyond-title\royal-pro-wrestling-jikkyou-live\captures\f012000.png |
| Samurai Shodown | Samurai Shodown (1994)(Crystal Dynamics)(EU-US)[!].chd | Panasonic FZ-10 | 2aaba5b90f51 | 12000 | no | artifacts\beyond-title\samurai-shodown\captures\f012000.png |
| San Diego Zoo Presents... The Animals!, The | San Diego Zoo Presents... The Animals!, The (1993)(Software Toolworks)(US)[!].chd | Panasonic FZ-10 | 8057c4becf13 | 12000 | no | artifacts\beyond-title\san-diego-zoo-presents-animals\captures\f012000.png |
| Sangokushi IV | Sangokushi IV (1995)(KOEI)(JP)[!].chd | Panasonic FZ-10J | 8304078b4ba1 | 12000 | no | artifacts\beyond-title\sangokushi-iv\captures\f012000.png |
| Scramble Cobra | Scramble Cobra (1995)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 9b87e5d747b5 | 12000 | no | artifacts\beyond-title\scramble-cobra\captures\f012000.png |
| Scramble Cobra - Premium Version | Scramble Cobra - Premium Version (Japan).chd | Panasonic FZ-10J | daa3ff6cc841 | 12000 | no | artifacts\beyond-title\scramble-cobra-premium-version\captures\f012000.png |
| Seal of the Pharaoh | Seal of the Pharaoh (1994)(Panasonic)(US)[!].chd | Panasonic FZ-10 | 1757408b5d31 | 12000 | no | artifacts\beyond-title\seal-of-pharaoh\captures\f012000.png |
| Seimeihandan | Seimeihandan (1995)(Ariadne)(JP)[!].chd | Panasonic FZ-10J | 77a963a0cb36 | 12000 | no | artifacts\beyond-title\seimeihandan\captures\f012000.png |
| Sekure Fuumin no Omocha Hako | Sekure Fuumin no Omocha Hako (1994)(GLAMS)(JP)[!].chd | Panasonic FZ-10J | b988259b6158 | 12000 | no | artifacts\beyond-title\sekure-fuumin-no-omocha-hako\captures\f012000.png |
| Sento Monogatari - Sono I | Sento Monogatari - Sono I (1995)(Ariadne)(JP)[!].chd | Panasonic FZ-10J | 971d507577ca | 12000 | no | artifacts\beyond-title\sento-monogatari-sono-i\captures\f012000.png |
| Sesame Street - Numbers | Sesame Street - Numbers (1994)(EA Kids)(US)[!].chd | Panasonic FZ-10 | d0e970b0ace4 | 12000 | no | artifacts\beyond-title\sesame-street-numbers\captures\f012000.png |
| Sewer Shark | Sewer Shark (1994)(Digital Pictures)(US)[!].chd | Panasonic FZ-10 | 646467ce57dd | 12000 | no | artifacts\beyond-title\sewer-shark\captures\f012000.png |
| Sex | Sex (1994)(Vivid Interactive)(US).chd | Panasonic FZ-10 | 6fefa094487e | 12000 | no | artifacts\beyond-title\sex\captures\f012000.png |
| Shadow - War of Succession | Shadow - War of Succession (1994)(Tribeca Digital Studios)(US)[!].chd | Panasonic FZ-10 | 7ec6dceceb95 | 12000 | no | artifacts\beyond-title\shadow-war-of-succession\captures\f012000.png |
| Shadow Warriors | Shadow Warriors (1994)(T&E Soft)(JP)(en)[!].chd | Panasonic FZ-10J | 002c924f19d7 | 12000 | no | artifacts\beyond-title\shadow-warriors\captures\f012000.png |
| Shanghai - Banri no Choujou | Shanghai - Banri no Choujou (1994)(Activision)(JP)[!].chd | Panasonic FZ-10J | 28049ba35744 | 12000 | no | artifacts\beyond-title\shanghai-banri-no-choujou\captures\f012000.png |
| Shanghai - Triple-Threat | Shanghai - Triple-Threat (1994)(Activision)(US)[!][02500-2.4].chd | Panasonic FZ-10 | cca6a6594eed | 12000 | no | artifacts\beyond-title\shanghai-triple-threat\captures\f012000.png |
| Shelley Duvall's It's A Bird's Life | Shelley Duvall's It's A Bird's Life (1992)(Sanctuary Woods)(US)[!].chd | Panasonic FZ-10 | 538bc2b0f3f5 | 12000 | no | artifacts\beyond-title\shelley-duvall-s-it-s-bird-s-life\captures\f012000.png |
| Shock Wave | Shock Wave (1994)(Electronic Arts)(EU)[!].chd | Panasonic FZ-10 | ca4dc585eca6 | 12000 | no | artifacts\beyond-title\shock-wave\captures\f012000.png |
| Shock Wave - Operation JumpGate | Shock Wave - Operation JumpGate (1994)(Electronic Arts)(EU-US)[!][data disc].chd | Panasonic FZ-10 | c1a13ca8e3f2 | 12000 | no | artifacts\beyond-title\shock-wave-operation-jumpgate\captures\f012000.png |
| Shockwave 2 - Beyond the Gate | Shockwave 2 - Beyond the Gate (1995)(Electronic Arts)(EU)(Disc 1 of 2)[!].chd | Panasonic FZ-10 | 9e74feb22fce | 12000 | no | artifacts\beyond-title\shockwave-2-beyond-gate\captures\f012000.png |
| Shootout at Old Tucson | Shootout at Old Tucson (USA) (RE3) (Arcade CD-ROM).chd | Panasonic FZ-10 (No RSA) | ea0682730fe2 | 12000 | no | artifacts\beyond-title\shootout-at-old-tucson\captures\f012000.png |
| Short Warp - Warp's Short Game Colection | Short Warp - Warp's Short Game Colection (1996)(Warp)(JP)[!].chd | Panasonic FZ-10J | 6635d01c96b0 | 12000 | no | artifacts\beyond-title\short-warp-warp-s-short-game-colection\captures\f012000.png |
| Sid Meier's C.P.U. Bach | Sid Meier's C.P.U. Bach (1994)(MicroProse)(US)[!].chd | Panasonic FZ-10 | 3402900ab856 | 12000 | no | artifacts\beyond-title\sid-meier-s-c-p-u-bach\captures\f012000.png |
| Slam 'N Jam '95 | Slam 'N Jam '95 (1994)(Crystal Dynamics)(EU-US)[!].chd | Panasonic FZ-10 | 181a66f14b21 | 12000 | no | artifacts\beyond-title\slam-n-jam-95\captures\f012000.png |
| Slayer | Slayer (1994)(Mindscape)(EU)[!].chd | Panasonic FZ-10 | 6b1a7fedc32a | 12000 | no | artifacts\beyond-title\slayer\captures\f012000.png |
| Slopestyle - An Interactive Learning Cube | Slopestyle - An Interactive Learning Cube (1994)(L3)(US)[!].chd | Panasonic FZ-10 | 42ac5a01bb95 | 12000 | no | artifacts\beyond-title\slopestyle-interactive-learning-cube\captures\f012000.png |
| Snow Job Starring Tracy Scoggins | Snow Job Starring Tracy Scoggins (1995)(Studio 3DO)(US)(Disc 1 of 2)[!].chd | Panasonic FZ-10 | 8d202445c25c | 12000 | no | artifacts\beyond-title\snow-job-starring-tracy-scoggins\captures\f012000.png |
| Soccer Kid | Soccer Kid (1994)(Studio 3DO)(US)[!].chd | Panasonic FZ-10 | 0703f25551b7 | 12000 | no | artifacts\beyond-title\soccer-kid\captures\f012000.png |
| Sotsugyou Graduation Final | Sotsugyou Graduation Final (1994)(Shar Rock)(JP)[!].chd | Panasonic FZ-10J | fc91499c362c | 12000 | no | artifacts\beyond-title\sotsugyou-graduation-final\captures\f012000.png |
| Sotsugyou II - Neo Generation Special | Sotsugyou II - Neo Generation Special (1995)(Shar Rock)(JP)[!].chd | Panasonic FZ-10J | 162bb9b84ad0 | 12000 | no | artifacts\beyond-title\sotsugyou-ii-neo-generation-special\captures\f012000.png |
| Space Ace | Space Ace (1995)(ReadySoft)(US)[!][DH 65008 RE-1].chd | Panasonic FZ-10 | 112b53d0a045 | 12000 | no | artifacts\beyond-title\space-ace\captures\f012000.png |
| Space Hulk - Vengeance of the Blood Angels | Space Hulk - Vengeance of the Blood Angels (1995)(Electronic Arts)(EU)[!].chd | Panasonic FZ-10 | 813e41b197d8 | 12000 | no | artifacts\beyond-title\blood-angels-space-hulk\captures\f012000.png |
| Space Invaders Invaders | Space Invaders Invaders (2014)(Mihai, Sebastian).chd | Panasonic FZ-10J | 971b56941e45 | 12000 | no | artifacts\beyond-title\space-invaders-invaders\captures\f012000.png |
| Space Pirates | Space Pirates (1994)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | 07d402d195ec | 12000 | no | artifacts\beyond-title\space-pirates\captures\f012000.png |
| Space Shuttle | Space Shuttle (1993)(Software Toolworks)(US)[!].chd | Panasonic FZ-10 | fdd9ada0f08c | 12000 | no | artifacts\beyond-title\space-shuttle\captures\f012000.png |
| Star Control II | Star Control II (1994)(Crystal Dynamics)(EU-US)[!].chd | Panasonic FZ-10 | 18c186d94ed7 | 12000 | no | artifacts\beyond-title\star-control-ii\captures\f012000.png |
| Star Fighter | Star Fighter (1995)(Studio 3DO)(EU-US)[!].chd | Panasonic FZ-10 | 5a129b01b54e | 12000 | no | artifacts\beyond-title\star-fighter\captures\f012000.png |
| Star Wars - Rebel Assault | Star Wars - Rebel Assault v1.0 (1993)(Electronic Arts)(EU-US)[!].chd | Panasonic FZ-10 | 62b99b17a91e | 12000 | no | artifacts\beyond-title\star-wars-rebel-assault\captures\f012000.png |
| Star Wars - Rebel Assault .0J | Star Wars - Rebel Assault v1.0J (1993)(Electronic Arts)(JP)[!].chd | Panasonic FZ-10J | 5af82a460688 | 12000 | no | artifacts\beyond-title\star-wars-rebel-assault-0j\captures\f012000.png |
| StarBlade | StarBlade (1994)(Panasonic)(CA)[!].chd | Panasonic FZ-10 | a7f26d1ffd41 | 12000 | no | artifacts\beyond-title\starblade\captures\f012000.png |
| Stellar 7 - Draxon no Gyakushuu | Stellar 7 - Draxon no Gyakushuu (1994)(Dynamix)(JP)[!].chd | Panasonic FZ-10J | 7b5f394b32fa | 12000 | no | artifacts\beyond-title\stellar-7-draxon-no-gyakushuu\captures\f012000.png |
| Stellar 7 - Draxon's Revenge | Stellar 7 - Draxon's Revenge (1993)(Dynamix)(US)[!].chd | Panasonic FZ-10 | b4c72191af34 | 12000 | no | artifacts\beyond-title\stellar-7-draxon-s-revenge\captures\f012000.png |
| STNICCC2000 3DO | STNICCC2000 3DO (2019)(Optimus).chd | Panasonic FZ-10J | 67d836fbc6a0 | 12000 | no | artifacts\beyond-title\stniccc2000-3do\captures\f012000.png |
| Strahl | Strahl (1995)(Panasonic)(US)[!].chd | Panasonic FZ-10 | ca135a3493f6 | 12000 | no | artifacts\beyond-title\strahl\captures\f012000.png |
| Striker - World Cup Special | Striker - World Cup Special (1995)(Panasonic)(EU)[!].chd | Panasonic FZ-10E (Anvil) | a48456f4a1bd | 12000 | no | artifacts\beyond-title\world-cup-special\captures\f012000.png |
| Super Model, The - Gail McKenna | Super Model, The - Gail McKenna (1995)(Transpegasus)(JP).chd | Panasonic FZ-10J | 82bf275840c6 | 12000 | no | artifacts\beyond-title\super-model-gail-mckenna\captures\f012000.png |
| Super Nova - Battery Navi | Super Nova - Battery Navi (Japan).chd | Panasonic FZ-10J | cb0317d10e33 | 12000 | no | artifacts\beyond-title\super-nova-battery-navi\captures\f012000.png |
| Super Real Mahjong P.V | Super Real Mahjong P.V (1995)(Seta)(JP)[!].chd | Panasonic FZ-10J | 439cef832a58 | 12000 | no | artifacts\beyond-title\super-real-mahjong-p-v\captures\f012000.png |
| Super Real Mahjong PIV Aishou Shindan | Super Real Mahjong PIV   Aishou Shindan (Japan).chd | Panasonic FZ-10J | c8dd7a63e4a4 | 12000 | no | artifacts\beyond-title\super-real-mahjong-piv-aishou-shindan\captures\f012000.png |
| Super Street Fighter II Turbo | Super Street Fighter II Turbo (1994)(Panasonic)(CA-US)[!][SM3851-2 RE1].chd | Panasonic FZ-10 | dbb419fa0f59 | 12000 | no | artifacts\beyond-title\super-street-fighter-ii-turbo\captures\f012000.png |
| Super Wing Commander | Super Wing Commander (1994)(Origin)(US)[!].chd | Panasonic FZ-10 | c3285976afc8 | 12000 | no | artifacts\beyond-title\super-wing-commander\captures\f012000.png |
| SuperModels Go Wild | SuperModels Go Wild (1994)(Vivid Interactive)(US).chd | Panasonic FZ-10 | b7cd8729f9e3 | 12000 | no | artifacts\beyond-title\supermodels-go-wild\captures\f012000.png |
| Supreme Warrior | Supreme Warrior (USA) (Disc 1) (Fire & Earth).chd | Panasonic FZ-10 | 9d963926b5fc | 12000 | no | artifacts\beyond-title\supreme-warrior\captures\f012000.png |
| Supreme Warrior - Ying Heung | Supreme Warrior - Ying Heung (1994)(Digital Pictures)(US)(en-zh)(Disc 2 of 2)[!][Wind & Fang Tu].chd | Panasonic FZ-10 | 659789938106 | 12000 | no | artifacts\beyond-title\supreme-warrior-ying-heung\captures\f012000.png |
| Sword & Sorcery | Sword & Sorcery (1995)(Micro Cabin)(JP)[!].chd | Panasonic FZ-10J | 32af9fe09534 | 12000 | no | artifacts\beyond-title\sword-and-sorcery\captures\f012000.png |
| Syndicate | Syndicate (1995)(Electronic Arts)(EU)[!].chd | Panasonic FZ-10 | 08456921707e | 12000 | no | artifacts\beyond-title\syndicate\captures\f012000.png |
| System Catalog - Kigyou-muke System no Goannai | System Catalog - Kigyou-muke System no Goannai (Japan).chd | Panasonic FZ-10J | 67f665938c4a | 12000 | no | artifacts\beyond-title\system-catalog-kigyou-muke-system-no-goannai\captures\f012000.png |
| T&E VR Golf - Waialae no Kiseki | T&E VR Golf - Waialae no Kiseki (Japan).chd | Panasonic FZ-10J | 5c359f556b63 | 12000 | no | artifacts\beyond-title\t-and-e-vr-golf-waialae-no-kiseki\captures\f012000.png |
| Taiketsu Rumiizu! | Taiketsu Rumiizu! (Japan).chd | Panasonic FZ-10J | b3152065c66f | 12000 | no | artifacts\beyond-title\taiketsu-rumiizu\captures\f012000.png |
| Takeru | Takeru (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | c0d9b015c135 | 12000 | no | artifacts\beyond-title\takeru\captures\f012000.png |
| Tanjou Debut - Pure | Tanjou Debut - Pure (1996)(Shar Rock)(JP)[!].chd | Panasonic FZ-10J | 82127c92ebea | 12000 | no | artifacts\beyond-title\tanjou-debut-pure\captures\f012000.png |
| Tarot Uranai | Tarot Uranai (1995)(Ariadne)(JP)[!].chd | Panasonic FZ-10J | 50453a931034 | 12000 | no | artifacts\beyond-title\tarot-uranai\captures\f012000.png |
| Tetsujin Returns | Tetsujin Returns (1995)(Synergy)(JP)[!].chd | Panasonic FZ-10J | dfd1b0b66cf0 | 12000 | no | artifacts\beyond-title\tetsujin-returns\captures\f012000.png |
| Theatre Wars - Goraku no Dendou | Theatre Wars - Goraku no Dendou (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 1dd6c305604e | 12000 | no | artifacts\beyond-title\theatre-wars-goraku-no-dendou\captures\f012000.png |
| Theme Park | Theme Park (1994)(Electronic Arts)(EU)(en-fr)[!].chd | Panasonic FZ-10 | 33e5afe454a7 | 12000 | no | artifacts\beyond-title\theme-park\captures\f012000.png |
| Toki o Koeta Tegami - The Letter That Over Came Time | Toki o Koeta Tegami - The Letter That Over Came Time (1994)(Panasonic)(JP)[!].chd | Panasonic FZ-10J | 6f7fabd43267 | 12000 | no | artifacts\beyond-title\toki-o-koeta-tegami-letter-that-over-came-time\captures\f012000.png |
| Tokimeki Mahjong Paradise Special - Koi no Tenpai Beat | Tokimeki Mahjong Paradise Special - Koi no Tenpai Beat (1996)(Sonnet)(JP).chd | Panasonic FZ-10J | 56a9dfc88092 | 12000 | no | artifacts\beyond-title\tokimeki-mahjong-paradise-special-koi-no-tenpai-beat\captures\f012000.png |
| Toon Time... In the Classroom | Toon Time... In the Classroom (1994)(VideoactV)(US)[!].chd | Panasonic FZ-10 | f3ada8c1ad65 | 12000 | no | artifacts\beyond-title\toon-time-in-classroom\captures\f012000.png |
| Total Eclipse | Total Eclipse (1993)(Crystal Dynamics)(US)[!][3DRM-1214760].chd | Panasonic FZ-10 | 12ef1f8439f5 | 12000 | no | artifacts\beyond-title\total-eclipse\captures\f012000.png |
| Totsugeki Karakuri! Megadas!! | Totsugeki Karakuri! Megadas!! (Japan).chd | Panasonic FZ-10J | f2397262811d | 12000 | no | artifacts\beyond-title\totsugeki-kikan-megadasu\captures\f012000.png |
| Touhaiden Akagi | Touhaiden Akagi (1996)(Micronet)(JP).chd | Panasonic FZ-10J | 3f6c2bb49e11 | 12000 | no | artifacts\beyond-title\touhaiden-akagi\captures\f012000.png |
| Tower, The | Tower, The (1996)(OPeNBooK)(JP)[!].chd | Panasonic FZ-10J | 640563c2599a | 12000 | no | artifacts\beyond-title\tower\captures\f012000.png |
| TOYOPET OutDoor World | TOYOPET OutDoor World (1995)(Toyota)(JP).chd | Panasonic FZ-10J | 050f32bea7bf | 12000 | no | artifacts\beyond-title\toyopet-outdoor-world\captures\f012000.png |
| TOYOPET RV Paddock | TOYOPET RV Paddock (1995)(Toyota)(JP).chd | Panasonic FZ-10J | f268cf598407 | 12000 | no | artifacts\beyond-title\toyopet-rv-paddock\captures\f012000.png |
| Toyopet RV World | Toyopet RV World (Japan).chd | Panasonic FZ-10J | f9891e6c71a6 | 12000 | no | artifacts\beyond-title\toyopet-rv-world\captures\f012000.png |
| Tozasareta Yakata | Tozasareta Yakata (1995)(Studio 3DO)(JP)[!].chd | Panasonic FZ-10J | ae1c19b5c9ce | 12000 | no | artifacts\beyond-title\tozasareta-yakata\captures\f012000.png |
| Trip'd | Trip'd (1995)(Panasonic)(EU-US)[!].chd | Panasonic FZ-10 | b887f0bc61e0 | 12000 | no | artifacts\beyond-title\trip-d\captures\f012000.png |
| True Golf Classics - Waialae Country Club | True Golf Classics - Waialae Country Club (Germany).chd | Panasonic FZ-10E (Anvil) | b985cc21cf9e | 12000 | no | artifacts\beyond-title\true-golf-classics-waialae-country-club\captures\f012000.png |
| Tsuukai Gameshow - Twisted | Tsuukai Gameshow - Twisted (1994)(Electronic Arts)(JP)[!].chd | Panasonic FZ-10J | 127bf39cc199 | 12000 | no | artifacts\beyond-title\tsuukai-gameshow-twisted\captures\f012000.png |
| Twinkle Knights | Twinkle Knights (1995)(Intarus)(JP).chd | Panasonic FZ-10J | 695d4bf4c676 | 12000 | no | artifacts\beyond-title\twinkle-knights\captures\f012000.png |
| Twisted - The Game Show | Twisted - The Game Show (1993)(Electronic Arts)(US)[!][730807-2 R73].chd | Panasonic FZ-10 | a7c4ee531335 | 12000 | no | artifacts\beyond-title\twisted-game-show\captures\f012000.png |
| Uchuu Seifutsu Furopon-kun | Uchuu Seifutsu Furopon-kun (1994)(San-Ei Shobo Publishing)(JP)[!].chd | Panasonic FZ-10J | 0b879be47c7d | 12000 | no | artifacts\beyond-title\uchuu-seifutsu-furopon-kun\captures\f012000.png |
| Ultraman Powered | Ultraman Powered (1994)(Bandai)(JP)[!].chd | Panasonic FZ-10J | 645923d2eaa6 | 12000 | no | artifacts\beyond-title\ultraman-powered\captures\f012000.png |
| V Goal Soccer '96 | V Goal Soccer '96 (1996)(Tecmo)(JP)(en).chd | Panasonic FZ-10J | 5d2222b9c3fb | 12000 | no | artifacts\beyond-title\v-goal-soccer-96\captures\f012000.png |
| Virtual Cameraman Part 1 - Naomi Sawada & Anna Juri | Virtual Cameraman Part 1 - Naomi Sawada & Anna Juri (1995)(Transpegasus)(JP)[!].chd | Panasonic FZ-10J | bb76354a08a0 | 12000 | no | artifacts\beyond-title\virtual-cameraman-part-1-naomi-sawada-and-anna-juri\captures\f012000.png |
| Virtual Cameraman Part 2 - Natsumi Kawai & Kimi Tachihara | Virtual Cameraman Part 2 - Natsumi Kawai & Kimi Tachihara (1995)(Transpegasus)(JP).chd | Panasonic FZ-10J | 9ece34c339eb | 12000 | no | artifacts\beyond-title\virtual-cameraman-part-2-natsumi-kawai-and-kimi-tachihara\captures\f012000.png |
| Virtual Cameraman Part 3 - Yumika Sugimoto | Virtual Cameraman Part 3 - Yumika Sugimoto (1995)(Transpegasus)(JP).chd | Panasonic FZ-10J | e9c1f7a32271 | 12000 | no | artifacts\beyond-title\virtual-cameraman-part-3-yumika-sugimoto\captures\f012000.png |
| Virtual Cameraman Part 4 - Shiori Fujitani | Virtual Cameraman Part 4 - Shiori Fujitani (1995)(Transpegasus)(JP)[!].chd | Panasonic FZ-10J | ff2ecfc0aeed | 12000 | no | artifacts\beyond-title\virtual-cameraman-part-4-shiori-fujitani\captures\f012000.png |
| Virtual Cameraman Part 5 - Yuri Andou | Virtual Cameraman Part 5 - Yuri Andou (1995)(Transpegasus)(JP)[!].chd | Panasonic FZ-10J | df04f6e70375 | 12000 | no | artifacts\beyond-title\virtual-cameraman-part-5-yuri-andou\captures\f012000.png |
| Virtual Horror - Norowareta Tachi | Virtual Horror - Norowareta Tachi (1994)(Electronic Arts)(JP)[!].chd | Panasonic FZ-10J | 9215ea1b6263 | 12000 | no | artifacts\beyond-title\virtual-horror-norowareta-tachi\captures\f012000.png |
| Virtual Quest - Pharaoh no Fuuin | Virtual Quest - Pharaoh no Fuuin (1994)(ASK KODANSHA)(JP)[!].chd | Panasonic FZ-10J | a4b2b740df6c | 12000 | no | artifacts\beyond-title\virtual-quest-pharaoh-no-fuuin\captures\f012000.png |
| Virtual Vivid | Virtual Vivid (1993)(Vivid Interactive)(US)[!][GL VIRTUA RE-1].chd | Panasonic FZ-10 | d6dfe5f72e8d | 12000 | no | artifacts\beyond-title\virtual-vivid\captures\f012000.png |
| Virtuoso | Virtuoso (1995)(Data East)(US)[!].chd | Panasonic FZ-10 | 932a49c69553 | 12000 | no | artifacts\beyond-title\virtuoso\captures\f012000.png |
| VR Stalker | VR Stalker (1994)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | 832b2bd52f89 | 12000 | no | artifacts\beyond-title\vr-stalker\captures\f012000.png |
| Waialae Country Club | Waialae Country Club (1994)(Panasonic)(CA-US)[!].chd | Panasonic FZ-10 | 73b19fcbce67 | 12000 | no | artifacts\beyond-title\waialae-country-club\captures\f012000.png |
| Warner Music Group | Warner Music Group (USA).chd | Panasonic FZ-10 | 669c06caf2ce | 12000 | no | artifacts\beyond-title\warner-music-group\captures\f012000.png |
| Way of the Warrior | Way of the Warrior (1994)(Universal Interactive Studios)(NTSC)(US)[!].chd | Panasonic FZ-10 | 53afaf336730 | 12000 | no | artifacts\beyond-title\way-of-warrior\captures\f012000.png |
| Where is Derpy Final | Where is Derpy v1.3 Final (2014)(Gameblabla)[unencrypted, req. RSA disable].chd | Panasonic FZ-10J | 8dc7ef43b92e | 12000 | no | artifacts\beyond-title\where-is-derpy-final\captures\f012000.png |
| Who Shot Johnny Rock | Who Shot Johnny Rock (1994)(American Laser Games)(US)[!].chd | Panasonic FZ-10 | 98440c807db8 | 12000 | no | artifacts\beyond-title\who-shot-johnny-rock\captures\f012000.png |
| Wicked 18 | Wicked 18 (1994)(Panasonic)(US)[!].chd | Panasonic FZ-10 | c305a4c5357f | 12000 | no | artifacts\beyond-title\wicked-18\captures\f012000.png |
| Wing Commander III - Heart of the Tiger | Wing Commander III - Heart of the Tiger (1995)(Origin)(EU-US)(Disc 1 of 4)[!].chd | Panasonic FZ-10 | a407d519aca4 | 12000 | no | artifacts\beyond-title\wing-commander-iii-heart-of-tiger\captures\f012000.png |
| Winning Post | Winning Post (1994)(KOEI)(JP)[!].chd | Panasonic FZ-10J | 1167308b9bbb | 12000 | no | artifacts\beyond-title\winning-post\captures\f012000.png |
| Wolfenstein 3D | Wolfenstein 3D (1994)(Interplay)(US)[!].chd | Panasonic FZ-10 | 3dfcdbbff14f | 12000 | no | artifacts\beyond-title\wolfenstein-3d\captures\f012000.png |
| Woody Woodpecker and Friends - Volume One | Woody Woodpecker and Friends - Volume One (1994)(MCA Universal Home Video)(US)[!].chd | Panasonic FZ-10 | 0972ffccee21 | 12000 | no | artifacts\beyond-title\woody-woodpecker-and-friends-volume-one\captures\f012000.png |
| Woody Woodpecker and Friends - Volume Three | Woody Woodpecker and Friends - Volume Three (1994)(MCA Universal Home Video)(US).chd | Panasonic FZ-10 | 59e949e58355 | 12000 | no | artifacts\beyond-title\woody-woodpecker-and-friends-volume-three\captures\f012000.png |
| Woody Woodpecker and Friends - Volume Two | Woody Woodpecker and Friends - Volume Two (1994)(MCA Universal Home Video)(US)[!].chd | Panasonic FZ-10 | 7c290450acb2 | 12000 | no | artifacts\beyond-title\woody-woodpecker-and-friends-volume-two\captures\f012000.png |
| World Cup Golf - Hyatt Dorado Beach | World Cup Golf - Hyatt Dorado Beach (1994)(U.S. Gold)(EU)[!].chd | Panasonic FZ-10 | 2f258d361dcd | 12000 | no | artifacts\beyond-title\world-cup-golf-hyatt-dorado-beach\captures\f012000.png |
| Worldcup Super Stadium | Worldcup Super Stadium (1995)(GAGA)(JP).chd | Panasonic FZ-10J | 56927592fcf5 | 12000 | no | artifacts\beyond-title\worldcup-super-stadium\captures\f012000.png |
| Yakyuuken Special, The | Yakyuuken Special, The (1994)(Societa Daikanyama)(JP)[!].chd | Panasonic FZ-10J | e4fbb91ec341 | 12000 | no | artifacts\beyond-title\yakyuuken-special\captures\f012000.png |
| Yamada Kamachi Bijutsukan - Kamachi's Museum | Yamada Kamachi Bijutsukan - Kamachi's Museum (1995)(TV Asahi)(JP)[!].chd | Panasonic FZ-10J | 322b9ef376df | 12000 | no | artifacts\beyond-title\yamada-kamachi-bijutsukan-kamachi-s-museum\captures\f012000.png |
| Yamamura Misa Suspense - Kyoto Kurama Sansou Satsujin Jiken | Yamamura Misa Suspense - Kyoto Kurama Sansou Satsujin Jiken (1994)(Pack-In-Video - Panasonic)(JP)(Disc 1 of 2)[!].chd | Panasonic FZ-10J | 0e787ffe915d | 12000 | no | artifacts\beyond-title\yamamura-misa-suspense-kyoto-kurama-sansou-satsujin-jiken\captures\f012000.png |
| Yuu Yuu Hakusho | Yuu Yuu Hakusho (1994)(Tomy)(JP)[!].chd | Panasonic FZ-10J | 9a8187d12874 | 12000 | no | artifacts\beyond-title\yuu-yuu-hakusho\captures\f012000.png |
| Zhadnost - The Peoples Party | Zhadnost - The Peoples Party (1995)(Studio 3DO)(EU)[!].chd | Panasonic FZ-10E (Anvil) | d7801a0f20ac | 12000 | no | artifacts\beyond-title\zhadnost-people-s-party\captures\f012000.png |

## Title-only (2)

| Title | Canonical disc | BIOS | Disc hash | Frame | Review | Screenshot |
|---|---|---|---|---:|---|---|
| Joypad Test | Joypad Test v1.0 (2011)(Sdevel).chd | Panasonic FZ-10J | 8d3ef9cd11a5 | 12000 | no | artifacts\beyond-title\joypad-test\captures\f012000.png |
| Return Fire - Maps O' Death | Return Fire - Maps O' Death v1.0 FB13.1.0 (1995)(Prolific)(US)[!][data disc].chd | Panasonic FZ-10 | 3e1fee64f453 | 12000 | no | artifacts\beyond-title\return-fire-maps-o-death\captures\f012000.png |

| Title | Reason |
|---|---|
| Joypad Test | no scripted-input effect; title/attract screen is static or looping |
| Return Fire - Maps O' Death | no scripted-input effect; title/attract screen is static or looping |

## No video (1)

| Title | Canonical disc | BIOS | Disc hash | Frame | Review | Screenshot |
|---|---|---|---|---:|---|---|
| LameNES Emulator | LameNES Emulator (2016)(Gameblabla - Saffron)[incl. Hello World ROM].chd | Panasonic FZ-10 (No RSA) | 4947781167f8 | 12000 | no | artifacts\beyond-title\lamenes-emulator\captures\f012000.png |

| Title | Reason |
|---|---|
| LameNES Emulator | all captures are black or nearly blank |

## Not loadable (0)

None.

## Review queue

None.
