# Game Compatibility List

This is the working compatibility matrix for the full local 3DO collection. The 4DO scores and notes are transcribed from the saved 4DO wiki page; the 5DO columns are filled in by the collection sweep.

## Sources

- 4DO compatibility wiki snapshot: `References/4DO-master/4DO Compatibility List ⚈ 3DO emulator Wiki ».mhtml` (saved 2026-10-25). `References/` is local-only and ignored by git; this Markdown file is the committed reference copy.
- Local collection roots: `J:\Panasonic 3DO`, `G:\MAME\MAME Software List CHDs\3do`.
- 5DO sweep tool: `tools/compat-sweep.ps1`; results are written under `artifacts/compat/` and can be merged into the 5DO columns below. The latest automated pass is [`GameCompatibilityResults.md`](GameCompatibilityResults.md).
- BIOS sensitivity: the 2026-10-30 triage found that several titles need `panafz10-patched` or the Anvil BIOS; see [`Compatibility.md`](Compatibility.md) 13.2. Record the BIOS used in the 5DO notes.

## 4DO status scale

| Status | 4DO score | Meaning |
|---|---:|---|
| No data | (none) | No assessment recorded. |
| Not Loadable | 0 | The game cannot load or does not start. |
| Unplayable | 1-49 | Starts, but freezes or has severe graphical/functional problems. |
| Playable | 50-79 | Playable, but has significant problems or cannot be completed. |
| Good | 80-100 | Completes and runs without significant problems. |

## Automated sweep labels

The automated loadability pass uses three intermediate labels before a human assigns the final five-level status:

| Automated label | Meaning | Usual final status |
|---|---|---|
| Loadable/Rendered | Completed the frame count and the final frame is non-black. | Playable or Good after visual/input review |
| Loadable/No video | Completed the frame count but the final frame is black/blank. | Unplayable or Playable after rerun/inspection |
| Not Loadable | CLI error, timeout, invalid JSON, or short frame count. | Not Loadable unless the failure is a harness issue |

## Summary

- 4DO reference titles: **241**
- Local disc images found: **594**
  - `J:\Panasonic 3DO`: **589**
  - `G:\MAME\MAME Software List CHDs\3do`: **5**
- 4DO titles with a local match: **216**
- Local images without a 4DO reference entry: **184**

## 4DO reference and 5DO results

`Local disc(s)` is a filename match, not proof that the image is the same revision. Untested discs start as `No data`; use the same scale as 4DO.

| Category | Game | 4DO score | 4DO status | 4DO version | 4DO notes | Local disc(s) | 5DO status | 5DO notes |
|---|---|---:|---|---|---|---|---|---|
| Adult | Blonde Justice | — | No data | — | — | Blonde Justice (USA).chd | No data | Not tested yet |
| Adult | Coven, The | 100 | Good | 1.3.2.0 | — | Coven, The (USA).chd | No data | Not tested yet |
| Adult | Endlessly | — | No data | — | — | Endlessly (USA).chd | No data | Not tested yet |
| Adult | Immortal Desire | — | No data | — | — | Immortal Desire (USA).chd | No data | Not tested yet |
| Adult | Love Bites | — | No data | — | — | Love Bites (USA).chd | No data | Not tested yet |
| Adult | Mind Teazzer | — | No data | — | — | Mind Teazzer (USA).chd | No data | Not tested yet |
| Adult | NeuroDancer - Journey Into The Neuronet | — | No data | — | — | NeuroDancer - Journey into the Neuronet! (USA).chd | No data | Not tested yet |
| Adult | Penthouse Interactive - Virtual Photo Shoot Vol. 1 | — | No data | — | — | Penthouse Interactive - Virtual Photo Shoot Vol. 1 (Japan).chd | No data | Not tested yet |
| Adult | Plumbers Don't Wear Ties | 100 | Good | 1.3.2.4 | cenes fixed in 1.3.2.4 | Plumbers Don't Wear Ties (USA).chd | No data | Not tested yet |
| Adult | Sex | — | No data | — | — | Sex (USA).chd | No data | Not tested yet |
| Adult | Super Model, The - Gail McKenna | — | No data | — | — | Super Model, The - Gail McKenna (Japan).chd | No data | Not tested yet |
| Adult | SuperModels Go Wild | — | No data | — | — | SuperModels Go Wild (USA).chd | No data | Not tested yet |
| Adult | Twinkle Knights | — | No data | — | — | Twinkle Knights (Japan).chd | No data | Not tested yet |
| Adult | Virtual Cameraman Part 1 - Naomi Sawada & Anna Juri | — | No data | — | — | Virtual Cameraman Part 1 - Naomi Sawada & Anna Juri (Japan).chd | No data | Not tested yet |
| Adult | Virtual Cameraman Part 2 - Natsumi Kawai & Kimi Tachihara | — | No data | — | — | Virtual Cameraman Part 2 - Natsumi Kawai & Kimi Tachihara (Japan).chd | No data | Not tested yet |
| Adult | Virtual Cameraman Part 3 - Yumika Sugimoto | — | No data | — | — | Virtual Cameraman Part 3 - Yumika Sugimoto (Japan).chd | No data | Not tested yet |
| Adult | Virtual Cameraman Part 4 - Shiori Fujitani | — | No data | — | — | Virtual Cameraman Part 4 - Shiori Fujitani (Japan).chd | No data | Not tested yet |
| Adult | Virtual Cameraman Part 5 - Yuri Andou | — | No data | — | — | Virtual Cameraman Part 5 - Yuri Andou (Japan).chd | No data | Not tested yet |
| Adult | Virtual Vivid | — | No data | — | — | Virtual Vivid (USA) (RE-1).chd<br>Virtual Vivid (USA).chd | No data | Not tested yet |
| Arcade | Mad Dog McCree 2: The Lost Gold | 25 | Unplayable | 1.1.1.1 Beta | Requires patched bios, no controls | — | No data | Not tested yet |
| Arcade | Orbatak | 25 | Unplayable | 1.1.1.1 Beta | Requires patched bios, no controls | Orbatak (USA) (Arcade CD-ROM).chd | Playable | panafz10-patched; input script starts a match. 4DO's no-controls note does not reproduce. |
| Arcade | Shootout at Old Tucson | 25 | Unplayable | 1.1.1.1 Beta | Requires patched bios, no controls | Shootout at Old Tucson (USA) (RE2) (Arcade CD-ROM) [b].chd<br>Shootout at Old Tucson (USA) (RE3) (Arcade CD-ROM).chd | Playable | RE2/RE3 render with panafz10-patched; input reaches the title/advisory screen. |
| Demo | 3DO Interactive Sampler CD #2, The | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | — | No data | Not tested yet |
| Demo | 3DO Interactive Sampler CD #3, The | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | — | No data | Not tested yet |
| Demo | 3DO Interactive Sampler CD #4, The | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | — | No data | Not tested yet |
| Demo | 3DO Interactive Sampler CD, The | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | — | No data | Not tested yet |
| Demo | 3DO Magazine 01 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 02 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 03 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 04 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 05 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 06 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 07 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 08 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 09 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 10 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 11 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 12 | — | No data | — | — | — | No data | Not tested yet |
| Demo | 3DO Magazine 13 | — | No data | — | — | — | No data | Not tested yet |
| Demo | Interplay 3DO Buffet, The | 100 | Good | 1.3.0.1 | — | Interplay 3DO Buffet, The (USA).chd | No data | Not tested yet |
| Demo | Killing Time alpha | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | — | No data | Not tested yet |
| Demo | Panasonic REAL 3DO Interactive Multiplayer Sampler A | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | — | No data | Not tested yet |
| Demo | Panasonic REAL 3DO Interactive Multiplayer Sampler B | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | — | No data | Not tested yet |
| Demo | Sample This! | 100 | Good | 1.3.0.1 | — | Sample This! (USA, Japan).chd | No data | Not tested yet |
| Educational | Bonogurashi | — | No data | — | — | Bonogurashi (Japan).chd | No data | Not tested yet |
| Educational | Fatty Bear's Birthday Surprise | — | No data | — | — | Fatty Bear's Birthday Surprise (Japan) (En,Ja).chd<br>Fatty Bear's Birthday Surprise (USA).chd | No data | Not tested yet |
| Educational | Fun 'n Games | — | No data | — | — | Fun 'n Games (USA).chd | No data | Not tested yet |
| Educational | Fun 'n Games - Soft no Omochabai | — | No data | — | — | Fun 'n Games (USA).chd | No data | Not tested yet |
| Educational | Gakkou no Kowai Uwasa - Hanako-san ga Kita!! | — | No data | — | — | Gakkou no Kowai Uwasa - Hanako-san ga Kita!! (Japan).chd | No data | Not tested yet |
| Educational | Mathemagics - An Interactive Learning Cube | — | No data | — | — | Mathemagics - An Interactive Learning Cube (USA).chd | No data | Not tested yet |
| Educational | Montana Jones | — | No data | — | — | Montana Jones (Japan).chd | No data | Not tested yet |
| Educational | Puppet Tale | — | No data | — | — | Puppet Tale (Japan).chd | No data | Not tested yet |
| Educational | Putt-Putt Goes to the Moon | — | No data | — | — | Putt-Putt Goes to the Moon (USA).chd | No data | Not tested yet |
| Educational | Putt-Putt Joins the Parade | — | No data | — | — | Putt-Putt Joins the Parade (Japan) (En,Ja).chd<br>Putt-Putt Joins the Parade (USA) (Alt).chd<br>Putt-Putt Joins the Parade (USA).chd | No data | Not tested yet |
| Educational | Sesame Street - Numbers | — | No data | — | — | Sesame Street - Numbers (Japan).chd<br>Sesame Street - Numbers (USA).chd | No data | Not tested yet |
| Educational | Shelley Duvall's It's A Bird's Life | — | No data | — | — | Shelley Duvall's It's A Bird's Life (USA).chd | No data | Not tested yet |
| Educational | Slopestyle - An Interactive Learning Cube | — | No data | — | — | Slopestyle - An Interactive Learning Cube (USA).chd | No data | Not tested yet |
| Educational | Toon Time...in the classroom | — | No data | — | — | ToonTime ...In the Classroom (USA).chd | No data | Not tested yet |
| Main list | Alone in the Dark | 75 | Playable | 1.3.2.0 | See Detailed Note | Alone in the Dark (Europe) (En,Fr) (NTSC Version).chd<br>Alone in the Dark (Europe) (En,Fr).chd<br>Alone in the Dark (Japan).chd<br>+1 more | No data | Not tested yet |
| Main list | Alone in the Dark 2 | 85 | Good | 1.2.0.3 | Occulusion/culling visual problems | Alone in the Dark 2 (Europe).chd<br>Alone in the Dark 2 (France).chd<br>Alone in the Dark 2 (Japan).chd<br>+1 more | No data | Not tested yet |
| Main list | AutoBahn Tokio | 100 | Good | 1.2.0.1 | — | AutoBahn Tokio (Japan).chd | No data | Not tested yet |
| Main list | Ballz - The Director's Cut | 100 | Good | 1.1.6.1 Beta | — | Ballz - The Director's Cut (World) (En,Ja).chd | No data | Not tested yet |
| Main list | Battle Chess | 100 | Good | 1.3.0.1 | — | Battle Chess (Europe).chd<br>Battle Chess (Japan).chd<br>Battle Chess (USA).chd | Playable | Europe disc is PAL-only and needs `panafz10e-anvil`; it reaches the board at 30,000 frames. USA/Japan boot with `panafz10`. |
| Main list | Battle Pinball | 100 | Good | 1.2.0.1 | — | Battle Pinball (Japan).chd | No data | Not tested yet |
| Main list | BattleSport | 100 | Good | 1.2.0.3 | — | BattleSport (Europe).chd<br>BattleSport (USA).chd | No data | Not tested yet |
| Main list | BC Racers | 100 | Good | 1.3.0.1 | — | BC Racers (USA, Europe).chd | No data | Not tested yet |
| Main list | Blade Force | 100 | Good | 1.3.0.1 | — | Blade Force (Europe).chd<br>Blade Force (Japan).chd<br>Blade Force (Korea).chd<br>+1 more | No data | Not tested yet |
| Main list | Blood Angels - Space Hulk | 100 | Good | 1.2.0.2 | — | Blood Angels (Japan).chd | No data | Not tested yet |
| Main list | Blue Forest Monogatari | 100 | Good | 1.2.0.1 | Requires rom2.rom (kanji font) | — | No data | Not tested yet |
| Main list | Brain Dead 13 | 100 | Good | 1.3.0.1 | — | BrainDead 13 (USA) (Disc 1) (RE-1).chd<br>BrainDead 13 (USA) (Disc 1).chd<br>BrainDead 13 (USA) (Disc 2).chd | No data | Not tested yet |
| Main list | Burning Soldier | 100 | Good | 1.3.0.1 | — | Burning Soldier (Canada).chd<br>Burning Soldier (Europe).chd<br>Burning Soldier (Japan).chd<br>+1 more | No data | Not tested yet |
| Main list | Bust-A-Move | 100 | Good | 1.3.0.1 | — | Bust-A-Move (USA).chd | No data | Not tested yet |
| Main list | Cannon Fodder | 100 | Good | 1.2.0.2 | — | Cannon Fodder (Europe).chd<br>Cannon Fodder (USA).chd | No data | Not tested yet |
| Main list | Captain Quazar | 100 | Good | 1.2.0.3 | — | Captain Quazar (Europe).chd<br>Captain Quazar (Japan).chd<br>Captain Quazar (Korea).chd<br>+2 more | No data | Not tested yet |
| Main list | Casper | 100 | Good | 1.2.0.2 | — | Casper (USA).chd | No data | Not tested yet |
| Main list | Chiki Chiki Machine Mou-Race (Wacky Races) | 100 | Good | 1.3.0.1 | — | Chiki Chiki Machine Mou Race - Kenken to Black Maou no Ijiwaru Daisakusen (Japan).chd<br>Chiki Chiki Machine Mou Race 2 - In Space (Japan).chd | No data | Not tested yet |
| Main list | Chiki Chiki Machine Mou-Race 2 - In Space (Wacky Races 2) | 100 | Good | 1.3.0.1 | — | Chiki Chiki Machine Mou Race 2 - In Space (Japan).chd | No data | Not tested yet |
| Main list | Club 3DO - Station Invasion | 100 | Good | 1.2.0.3 | — | Club 3DO - Station Invasion (USA).chd | No data | Not tested yet |
| Main list | Corpse Killer | 100 | Good | 1.3.0.1 | — | Corpse Killer (Japan).chd<br>Corpse Killer (USA).chd | No data | Not tested yet |
| Main list | Cowboy Casino - Interactive Poker | 100 | Good | 1.3.0.1 | — | cowboy casino (usa).chd<br>Cowboy Casino (USA).chd | No data | Not tested yet |
| Main list | Crash 'n Burn | 90 | Good | 1.3.0.1 | (Fixed in 1.3.0.0) See Detailed Note | Crash 'n Burn (Japan).chd<br>Crash 'n Burn (USA) (Not for Resale).chd<br>Crash 'n Burn (USA, Korea).chd | Playable | USA/Korea revisions render with panafz1; Japan revision still black with FZ-1J + FZ-1J Kanji font at 65,000 frames (2026-11-01 retry); needs a bootable Japanese BIOS or a better dump. |
| Main list | Crayon Shin-chan - Puzzle Daimaou no Nazo | 100 | Good | 1.3.0.1 | — | Crayon Shin-chan - Puzzle Daimaou no Nazo (Japan).chd | No data | Not tested yet |
| Main list | Creature Shock | 100 | Good | 1.3.0.1 | — | Creature Shock (USA) (Disc 1).chd<br>Creature Shock (USA) (Disc 2).chd | No data | Not tested yet |
| Main list | Crime Patrol | 100 | Good | 1.2.0.2 | — | Crime Patrol (USA) (Arcade CD-ROM).chd<br>Crime Patrol (USA).chd | Playable | Arcade CD-ROM: panafz10-patched; regular USA: panafz1. Input reaches live-action/credits. |
| Main list | Cyberdillo | 100 | Good | 1.3.0.1 | — | Cyberdillo (USA).chd | No data | Not tested yet |
| Main list | Cyberia | 100 | Good | 1.3.0.1 | — | Cyberia (Europe).chd<br>Cyberia (Japan).chd<br>Cyberia (USA).chd | No data | Not tested yet |
| Main list | D | 100 | Good | 1.3.0.1 | — | D (Germany) (Disc 1).chd<br>D (Germany) (Disc 2).chd<br>D (USA, Europe) (Disc 1).chd<br>+1 more | No data | Not tested yet |
| Main list | Daedalus Encounter, The | 100 | Good | 1.2.0.2 | (All Freezes Fixed in 1.2.0.2) | Daedalus Encounter, The (Korea) (Disc 1).chd<br>Daedalus Encounter, The (Korea) (Disc 2).chd<br>Daedalus Encounter, The (Korea) (Disc 3).chd<br>+5 more | No data | Not tested yet |
| Main list | DeathKeep | 100 | Good | 1.3.0.1 | — | DeathKeep (USA).chd | No data | Not tested yet |
| Main list | Defcon 5 | 100 | Good | 1.3.0.1 | — | Defcon 5 (Europe).chd<br>Defcon 5 (Germany).chd<br>Defcon 5 (Japan).chd<br>+1 more | No data | Not tested yet |
| Main list | Demolition Man | 100 | Good | 1.2.0.2 | — | Demolition Man (Japan).chd<br>Demolition Man (USA, Europe).chd | No data | Not tested yet |
| Main list | Devil's Course | 95 | Good | 1.3.0.1 | Menu font rendering inaccurate | T&E VR Golf - Devil's Course (Japan).chd | No data | Not tested yet |
| Main list | DinoPark Tycoon | 75 | Playable | 1.3.2.4 | Graphics fixed in 1.3.2.4, font rendering inaccurate | DinoPark Tycoon (USA).chd | No data | Not tested yet |
| Main list | Doctor Hauzer | 100 | Good | 1.2.0.3 | — | Doctor Hauzer (Japan).chd | No data | Not tested yet |
| Main list | Doom | 100 | Good | 1.3.2.4 | (Game Speed and graphics bags fixed in 1.1.4.1) See Detailed Note | Doom (USA, Japan).chd | No data | Not tested yet |
| Main list | Dragon Lore - The Legend Begins | 100 | Good | 1.3.0.1 | — | Dragon Lore - The Legend Begins (USA, Germany) (Disc 1).chd<br>Dragon Lore - The Legend Begins (USA, Germany) (Disc 2).chd<br>Dragon Lore - The Legend Begins (USA, Germany) (Disc 3).chd | No data | Not tested yet |
| Main list | Dragon Tycoon Edge | 100 | Good | 1.3.0.1 | — | Dragon Tycoon Edge (Japan).chd | No data | Not tested yet |
| Main list | Dragon's Lair | 100 | Good | 1.2.0.1 | — | Dragon's Lair (Europe) (En,Fr,De,It).chd<br>Dragon's Lair (Japan).chd<br>Dragon's Lair (USA) (RE1).chd<br>+1 more | No data | Not tested yet |
| Main list | Drug Wars | 100 | Good | 1.3.2.4 | (Video Seeking fixed in 1.1.6.2) See Detailed Note | Drug Wars (USA).chd | No data | Not tested yet |
| Main list | Escape from Monster Manor - A Terrifying Hunt for the Undead | 100 | Good | 1.3.1.0 | (Text re-fixed in 1.3.1.0) | Escape from Monster Manor (Europe).chd<br>Escape from Monster Manor (Korea) (En,Ko).chd<br>Escape from Monster Manor (USA).chd | No data | Not tested yet |
| Main list | Family Feud | 100 | Good | 1.3.0.1 | — | Family Feud (USA).chd | No data | Not tested yet |
| Main list | FIFA International Soccer | 100 | Good | 1.1.6.1 Beta | — | FIFA International Soccer (Europe).chd<br>FIFA International Soccer (USA, Korea).chd | No data | Not tested yet |
| Main list | Flashback - The Quest for Identity | 100 | Good | 1.2.0.2 | — | Flashback (Europe) (En,Fr).chd<br>Flashback (Japan) (En,Ja,Fr,De,It,Nl).chd<br>Flashback - The Quest for Identity (USA) (En,Fr).chd | No data | Not tested yet |
| Main list | Flying Nightmares | 100 | Good | 1.3.0.1 | — | Flying Nightmares (Europe).chd<br>Flying Nightmares (USA).chd | No data | Not tested yet |
| Main list | Foes of Ali | 100 | Good | 1.1.6.1 Beta | — | Foes of Ali (Europe).chd<br>Foes of Ali (USA).chd | No data | Not tested yet |
| Main list | Gex | 100 | Good | 1.3.2.4 | (Crash fixed in 1.3.0.1) | Gex (Japan, Korea).chd<br>Gex (USA, Europe).chd | No data | Not tested yet |
| Main list | Gridders | 100 | Good | 1.1.2.3 Beta | — | Gridders (Europe).chd<br>Gridders (Japan).chd<br>Gridders (USA, Korea).chd | No data | Not tested yet |
| Main list | Guardian War (Powers Kingdom) | 100 | Good | 1.3.2.4 | Use Anvil-bios to load EU version of game. | Guardian War (Canada).chd<br>Guardian War (USA, Korea).chd | Playable | EU version needs Anvil BIOS; Canada/USA render; Europe reaches a live-action intro at 60,000 frames. |
| Main list | Hell - A Cyberpunk Thriller | 100 | Good | 1.2.0.3 | — | Hell - A Cyberpunk Thriller (USA).chd | No data | Not tested yet |
| Main list | Horde, The | 95 | Good | 1.1.6.4 Beta | Minor graphical inaccuracy in bird's eye map. | Horde, The (Japan).chd<br>Horde, The (USA) (RCB).chd<br>Horde, The (USA).chd<br>+1 more | No data | Not tested yet |
| Main list | Icebreaker | 100 | Good | 1.2.0.2 | — | Icebreaker (USA).chd | No data | Not tested yet |
| Main list | Immercenary | 100 | Good | 1.2.0.2 | (freezes fixed in 1.1.6.3) | Immercenary (Europe).chd | No data | Not tested yet |
| Main list | Incredible Machine, The | 100 | Good | 1.3.0.1 | — | Incredible Machine, The (Europe).chd<br>Incredible Machine, The (USA).chd | No data | Not tested yet |
| Main list | Insect War | 100 | Good | 1.3.0.1 | — | Insector War (Japan).chd | No data | Not tested yet |
| Main list | Iron Angel of the Apocalypse | 100 | Good | 1.3.0.1 | — | Iron Angel of the Apocalypse - Tetsujin (Europe).chd<br>Iron Angel of the Apocalypse - Tetsujin (USA).chd<br>Iron Angel of the Apocalypse - The Return (USA).chd | No data | Not tested yet |
| Main list | Iron Angel of the Apocalypse - The Return | 100 | Good | 1.3.0.1 | — | Iron Angel of the Apocalypse - The Return (USA).chd | No data | Not tested yet |
| Main list | Jammit | 100 | Good | 1.3.0.1 | — | Jammit (USA).chd | No data | Not tested yet |
| Main list | Jikki Pachi Slot Simulator Vol. 1 | 100 | Good | 1.3.0.1 | — | Jikki Pachi-Slot Simulator Vol. 1 (Japan).chd | No data | Not tested yet |
| Main list | John Madden NFL Football | 100 | Good | 1.3.0.1 | — | John Madden NFL Football (Europe).chd | No data | Not tested yet |
| Main list | Johnny Bazookatone | 90 | Good | 1.2.0.3 | Loading screen remains in background | Johnny Bazookatone (Europe).chd<br>Johnny Bazookatone (USA).chd | No data | Not tested yet |
| Main list | Jurassic Park Interactive | 90 | Good | 1.3.0.1 | Driving segment plays at wrong speed | Jurassic Park Interactive (Japan).chd<br>Jurassic Park Interactive (USA).chd | No data | Not tested yet |
| Main list | Killing Time | 100 | Good | 1.2.0.3 | — | Killing Time (Europe).chd<br>Killing Time (USA) (RE1).chd<br>Killing Time (USA) (RE2).chd | No data | Not tested yet |
| Main list | Kingdom - The Far Reaches | 100 | Good | 1.3.0.1 | — | Kingdom - The Far Reaches (USA, Europe).chd | No data | Not tested yet |
| Main list | Last Bounty Hunter, The | 100 | Good | 1.3.0.1 | — | Last Bounty Hunter, The (USA).chd | No data | Not tested yet |
| Main list | Lemmings | 90 | Good | 1.3.0.1 | Sound is sometimes static | Lemmings (Japan).chd<br>Lemmings (USA).chd | No data | Not tested yet |
| Main list | Lost Eden | 100 | Good | 1.2.0.3 | Takes a long time to boot up. | Lost Eden (USA, Korea).chd | No data | Not tested yet |
| Main list | Lost Files of Sherlock Holmes, The | 100 | Good | 1.3.0.1 | — | Lost Files of Sherlock Holmes, The (Europe).chd<br>Lost Files of Sherlock Holmes, The (USA).chd | No data | Not tested yet |
| Main list | Lucienne's Quest | 100 | Good | 1.2.0.3 | — | Lucienne's Quest (USA).chd | No data | Not tested yet |
| Main list | Mad Dog II - the Lost Gold | 100 | Good | 1.3.1.0 | Sometimes takes a long time to load. | Mad Dog II - The Lost Gold (USA) (Arcade CD-ROM).chd<br>Mad Dog II - the Lost Gold (USA).chd | Playable | Arcade CD-ROM: panafz10-patched; regular USA: panafz1. Input reaches live-action. |
| Main list | Mad Dog McCree | 100 | Good | 1.2.0.2 | — | Mad Dog McCree (USA).chd | No data | Not tested yet |
| Main list | Mazer | 100 | Good | 1.3.2.4 | All freezes fixed in 1.3.2.4 | Mazer (USA).chd | No data | Not tested yet |
| Main list | MegaRace | 100 | Good | 1.3.0.1 | (Video Freeze fixed in 1.3.0.1) | MegaRace (Europe) (En,Fr,De,Es,It).chd<br>MegaRace (Japan).chd<br>MegaRace (USA, Europe) (En,Fr,De,Es,It).chd<br>+1 more | No data | Not tested yet |
| Main list | Microcosm | 100 | Good | 1.3.2.4 | start screens fixed in 1.3.2.4 | Microcosm (Japan).chd<br>Microcosm (USA).chd | No data | Not tested yet |
| Main list | Myst | 100 | Good | 1.2.0.3 | — | Myst (Germany).chd<br>Myst (Japan) (Rev A).chd<br>Myst (Japan).chd<br>+1 more | Playable | Japan/USA discs render by 9,000 frames with panafz1; Germany untested. |
| Main list | N. O. B. - Neo Organic Bioform | 100 | Good | 1.2.0.1 | — | N.O.B. (Neo Organic Bioform) (Japan).chd | No data | Not tested yet |
| Main list | Need for Speed, The | 100 | Good | 1.3.2.4 | — | Road & Track Presents - The Need for Speed (Korea).chd<br>Road & Track Presents - The Need for Speed (USA, Europe).chd | No data | Not tested yet |
| Main list | Night Trap | 100 | Good | 1.2.0.3 | — | Night Trap (Japan) (Disc 1).chd<br>Night Trap (Japan) (Disc 2).chd<br>Night Trap (USA, Europe) (Disc 1).chd<br>+1 more | No data | Not tested yet |
| Main list | Novastorm | 100 | Good | 1.3.2.4 Beta | (FZ-1 BIOS Issue Fixed in 1.1.5.0) | Novastorm (USA).chd | No data | Not tested yet |
| Main list | Off-world Interceptor | 100 | Good | 1.3.0.1 | — | Off-World Interceptor (Japan).chd<br>Off-World Interceptor (Korea).chd<br>Off-world Interceptor (USA, Europe).chd | No data | Not tested yet |
| Main list | Olympic Soccer | 100 | Good | 1.3.0.1 | — | Olympic Soccer (USA) (En,Fr,De,Es,It).chd | No data | Not tested yet |
| Main list | Olympic Summer Games | 100 | Good | 1.3.0.1 | — | Olympic Summer Games (USA).chd | Playable | Default headless pad fixes controller detection; A/P reaches the credits. |
| Main list | Out of this World / Another World | 100 | Good | 1.2.0.2 | — | Another World (Europe).chd<br>Out of This World (USA) (RE).chd<br>Out of this World (USA).chd | No data | Not tested yet |
| Main list | OverDrivin' | 100 | Good | 1.2.0.2 | — | Road & Track Presents - OverDrivin' (Japan).chd | No data | Not tested yet |
| Main list | Paddock Note '95 | 100 | Good | 1.3.0.1 | — | Paddock Note '95 (Japan) (Rev 1).chd<br>Paddock Note '95 (Japan).chd | No data | Not tested yet |
| Main list | Panzer General | 100 | Good | 1.3.0.1 | — | Panzer General (USA, Germany).chd | No data | Not tested yet |
| Main list | PaTaank | 100 | Good | 1.2.0.3 | — | PaTaank (Japan).chd<br>PaTaank (Korea).chd<br>PaTaank (USA, Europe).chd | No data | Not tested yet |
| Main list | Pebble Beach Golf Links | 95 | Good | 1.2.0.3 | Menu font rendering inaccurate | Pebble Beach Golf Links (USA, Canada) (Rev 1).chd<br>Pebble Beach Golf Links (USA, Korea).chd | No data | Not tested yet |
| Main list | Perfect General, The | 100 | Good | 1.3.0.1 | — | Perfect General, The (USA).chd | No data | Not tested yet |
| Main list | PGA Tour 96 | 100 | Good | 1.3.0.1 | — | PGA Tour 96 (Europe).chd<br>PGA Tour 96 (USA) (RE-1).chd | No data | Not tested yet |
| Main list | Phoenix 3 | 100 | Good | 1.1.6.2 Beta | (Freezes fixed in 1.1.6.2) | Phoenix 3 (Europe).chd<br>Phoenix 3 (USA).chd | No data | Not tested yet |
| Main list | PO'ed | 100 | Good | 1.3.2.4 | (Game Speed fixed in 1.1.4.1) | PO'ed (USA, Europe).chd | No data | Not tested yet |
| Main list | Policenauts | 100 | Good | 1.3.0.1 | — | Policenauts (Japan) (Disc 1).chd<br>Policenauts (Japan) (Disc 2).chd | No data | Not tested yet |
| Main list | Primal Rage v1.0 | 80 | Good | 1.1.5.0 Beta | Full playable, but incorrect sound | — | No data | Not tested yet |
| Main list | Princess Maker 2 | 100 | Good | 1.3.0.1 | — | Princess Maker 2 (Japan).chd | No data | Not tested yet |
| Main list | Pro Stadium | 100 | Good | 1.3.0.1 | — | Pro Stadium (Japan).chd | No data | Not tested yet |
| Main list | Psychic Detective | 70 | Playable | 1.3.0.1 | Sound sometimes is just static during videos | Psychic Detective (Europe) (Disc 1).chd<br>Psychic Detective (Europe) (Disc 2).chd<br>Psychic Detective (Europe) (Disc 3).chd<br>+3 more | No data | Not tested yet |
| Main list | Quarantine | 100 | Good | 1.1.2.1 Beta | — | Quarantine (USA) (En,Fr,De,Es) (RE).chd<br>Quarantine (USA) (En,Ja,Fr,De,Es) (RE2).chd<br>Quarantine (USA, Japan) (En,Ja,Fr,De,Es).chd | No data | Not tested yet |
| Main list | Quarterback Attack - The Professional Quarterback Simulator | 100 | Good | 1.3.0.1 | — | Quarterback Attack (USA).chd | No data | Not tested yet |
| Main list | Real Pinball | 100 | Good | 1.1.1.1 Beta | — | Real Pinball (Canada).chd<br>Real Pinball (Europe).chd<br>Real Pinball (USA).chd | No data | Not tested yet |
| Main list | Return Fire | 100 | Good | 1.1.6.1 Beta | — | Return Fire (Japan, Europe) (En,Ja,Fr,De,Es,It,Pt,Ko,Ru,Ar).chd<br>Return Fire (USA).chd<br>Return Fire (USA, Europe).chd | No data | Not tested yet |
| Main list | Return Fire - Maps O' Death | 100 | Good | 1.2.0.2 | — | Return Fire (Japan, Europe) (En,Ja,Fr,De,Es,It,Pt,Ko,Ru,Ar).chd<br>Return Fire (USA).chd<br>Return Fire (USA, Europe).chd<br>+2 more | No data | Not tested yet |
| Main list | Rise of the Robots | 100 | Good | 1.3.0.1 | — | Rise of the Robots (Europe).chd<br>Rise of the Robots (USA).chd | No data | Not tested yet |
| Main list | Road Rash | 100 | Good | 1.1.2.1 Beta | — | Road Rash (Europe) (Rev 1).chd<br>Road Rash (Europe).chd<br>Road Rash (Japan).chd<br>+1 more | No data | Not tested yet |
| Main list | Robinson's Requiem | 100 | Good | 1.3.0.1 | — | Robinson's Requiem (USA).chd | No data | Not tested yet |
| Main list | Samurai Shodown | 100 | Good | 1.2.0.3 | (Graphical issues fixed in 1.2.0.2) | Samurai Shodown (Japan) (En,Ja).chd<br>Samurai Shodown (USA, Europe).chd | No data | Not tested yet |
| Main list | Scramble Cobra | 100 | Good | 1.1.6.1 Beta | — | Scramble Cobra (Europe).chd<br>Scramble Cobra (Japan).chd<br>Scramble Cobra (USA).chd | No data | Not tested yet |
| Main list | Seal of the Pharaoh | 100 | Good | 1.3.0.0 | (Intro freeze fixed 1.1.6.4) See Detailed Note | Seal of the Pharaoh (USA).chd | No data | Not tested yet |
| Main list | Sewer Shark | 100 | Good | 1.2.0.3 | — | Sewer Shark (USA, Europe).chd | No data | Not tested yet |
| Main list | Shadow - War of Succession | 75 | Playable | 1.3.0.1 | Plays at wrong speed (too fast) | Shadow - War of Succession (USA).chd | No data | Not tested yet |
| Main list | Shanghai - Triple-Threat | 100 | Good | 1.3.0.1 | — | Shanghai - Triple-Threat (USA) (3DO Action Pak).chd<br>Shanghai - Triple-Threat (USA).chd | No data | Not tested yet |
| Main list | Shock Wave | 100 | Good | 1.3.0.1 | — | Shock Wave (Europe).chd<br>Shock Wave (Japan).chd<br>Shock Wave (Korea).chd<br>+2 more | No data | Not tested yet |
| Main list | Shock Wave - Operation JumpGate | 100 | Good | 1.3.0.1 | — | Shock Wave (Europe).chd<br>Shock Wave (Japan).chd<br>Shock Wave (Korea).chd<br>+4 more | No data | Not tested yet |
| Main list | Shockwave 2 - Beyond the Gate | 100 | Good | 1.3.0.1 | — | Shockwave 2 - Beyond the Gate (Europe) (Disc 1).chd<br>Shockwave 2 - Beyond the Gate (Europe) (Disc 2).chd<br>Shockwave 2 - Beyond the Gate (USA) (Disc 1).chd<br>+1 more | No data | Not tested yet |
| Main list | Slam 'N Jam '95 | 100 | Good | 1.2.0.3 | — | Slam 'n Jam '95 (Japan, Korea).chd<br>Slam 'n Jam '95 (USA) (Alt).chd<br>Slam 'n Jam '95 (USA, Europe).chd | No data | Not tested yet |
| Main list | Slayer | 100 | Good | 1.2.0.3 | — | Slayer (Europe).chd<br>Slayer (USA) (3DO Action Pak).chd<br>Slayer (USA).chd | Playable | Europe renders credits with panafz1 at 12,000; USA reaches its title menu at 60,000 with panafz10. Action Pak not individually tested. |
| Main list | Snow Job | 100 | Good | 1.3.0.1 | — | SnowJob Starring Tracy Scoggins (Europe) (Disc 1).chd<br>SnowJob Starring Tracy Scoggins (Europe) (Disc 2).chd<br>SnowJob Starring Tracy Scoggins (USA) (Disc 1).chd<br>+1 more | No data | Not tested yet |
| Main list | Soccer Kid | 100 | Good | 1.3.0.1 | — | Soccer Kid (Europe).chd<br>Soccer Kid (USA).chd | No data | Not tested yet |
| Main list | Space Ace | 100 | Good | 1.2.0.1 | — | Space Ace (USA) (RE-1).chd<br>Space Ace (USA).chd | No data | Not tested yet |
| Main list | Space Hulk - Vengeance of the Blood Angels | 100 | Good | 1.2.0.2 | (freezes fixed in 1.1.6.2) | Space Hulk - Vengeance of the Blood Angels (Europe).chd<br>Space Hulk - Vengeance of the Blood Angels (USA).chd | No data | Not tested yet |
| Main list | Space Pirates | 100 | Good | 1.3.0.1 | — | Space Pirates (USA).chd | No data | Not tested yet |
| Main list | Star Control II | 95 | Good | 1.2.0.2 | Minor visual glitches and off-tone sound effects | Star Control II (USA, Europe).chd | No data | Not tested yet |
| Main list | Star Fighter | 100 | Good | 1.2.0.3 | — | Star Fighter (USA, Europe).chd | No data | Not tested yet |
| Main list | Star Wars - Rebel Assault | 100 | Good | 1.3.0.1 | — | Star Wars - Rebel Assault (Japan).chd<br>Star Wars - Rebel Assault (USA, Europe).chd | No data | Not tested yet |
| Main list | StarBlade | 100 | Good | 1.3.0.1 | — | Starblade (Canada).chd<br>Starblade (Europe, Germany).chd<br>Starblade (Japan).chd<br>+1 more | No data | Not tested yet |
| Main list | Stellar 7 - Draxon's Revenge | 100 | Good | 1.3.0.1 | — | Stellar 7 - Draxon's Revenge (Japan).chd<br>Stellar 7 - Draxon's Revenge (USA).chd | No data | Not tested yet |
| Main list | Strahl | 100 | Good | 1.3.0.1 | — | Strahl (Japan).chd<br>Strahl (USA).chd | No data | Not tested yet |
| Main list | Striker - World Cup Special | 100 | Good | 1.3.0.1 | — | Striker - World Cup Special (Europe).chd | No data | Not tested yet |
| Main list | Super Real Mahjong P.V | 100 | Good | 1.3.0.1 | — | Super Real Mahjong P.V (Japan).chd | No data | Not tested yet |
| Main list | Super Street Fighter II Turbo | 100 | Good | 1.3.0.1 | See Detailed Note | Super Street Fighter II Turbo (Europe).chd<br>Super Street Fighter II Turbo (Germany).chd<br>Super Street Fighter II Turbo (USA) (RE2).chd<br>+2 more | No data | Not tested yet |
| Main list | Super Wing Commander | 100 | Good | 1.1.5.0 Beta | (Controls fixed in 1.1.5.0) See Detailed Note | Super Wing Commander (Europe).chd<br>Super Wing Commander (Japan).chd<br>Super Wing Commander (Korea).chd<br>+1 more | No data | Not tested yet |
| Main list | Supreme Warrior | 100 | Good | 1.1.2.1 Beta | — | Supreme Warrior (Japan) (Ja,Zh) (Disc 1) (Fire & Earth).chd<br>Supreme Warrior (Japan) (Ja,Zh) (Disc 2) (Wind & Fang Tu).chd<br>Supreme Warrior (USA) (Disc 1) (Fire & Earth).chd<br>+1 more | No data | Not tested yet |
| Main list | Syndicate | 100 | Good | 1.3.0.1 | — | Syndicate (Europe).chd<br>Syndicate (Japan).chd<br>Syndicate (USA).chd | No data | Not tested yet |
| Main list | Tetsujin Returns (J) | 60 | Playable | 1.3.0.1 | Missing graphics for obstacles in driving scene | Tetsujin Returns (Japan).chd | No data | Not tested yet |
| Main list | Theme Park | 100 | Good | 1.3.0.1 | — | Theme Park (Europe) (En,Fr).chd<br>Theme Park (Germany).chd<br>Theme Park (Japan).chd<br>+1 more | No data | Not tested yet |
| Main list | Total Eclipse | 90 | Good | 1.2.0.2 | Explosion gfx wrong. (Transparencies) | Total Eclipse (Europe) (DFJN5015ZAZ).chd<br>Total Eclipse (Europe).chd<br>Total Eclipse (Japan).chd<br>+3 more | No data | Not tested yet |
| Main list | Tower, The | 100 | Good | 1.3.0.1 | — | Tower, The (Japan).chd | No data | Not tested yet |
| Main list | Trip'd | 100 | Good | 1.2.0.3 | — | Trip'd (USA, Europe).chd | No data | Not tested yet |
| Main list | Twisted - The Game Show | 100 | Good | 1.3.2.4 | Time boot fixed in 1.3.2.4 | Twisted (Japan).chd<br>Twisted - The Game Show (Europe).chd<br>Twisted - The Game Show (USA) (Alt).chd<br>+1 more | No data | Not tested yet |
| Main list | Uchuu Seibutsu Furopon Kun | 95 | Good | 1.3.0.1 | Short lag before pieces disappear | Uchuu Seibutsu Flopon-kun (Japan).chd | No data | Not tested yet |
| Main list | Ultraman Powered | 100 | Good | 1.3.0.1 | — | Ultraman Powered (Japan).chd | No data | Not tested yet |
| Main list | V Goal Soccer '96 | 100 | Good | 1.3.0.1 | — | V Goal Soccer '96 (Japan).chd | No data | Not tested yet |
| Main list | Virtuoso | 100 | Good | 1.3.0.1 | (Video Fixed in 1.1.6.4) | Virtuoso (Europe) (En,Fr,De).chd<br>Virtuoso (Japan).chd<br>virtuoso (usa).chd<br>+1 more | No data | Not tested yet |
| Main list | VR Stalker | 100 | Good | 1.3.0.1 | — | VR Stalker (Europe) (Improved Version).chd<br>VR Stalker (USA, Korea).chd | No data | Not tested yet |
| Main list | Waialae Country Club | 100 | Good | 1.3.0.1 | — | Waialae Country Club (USA, Canada).chd | No data | Not tested yet |
| Main list | Way of the Warrior | 95 | Good | 1.2.0.3 | Occasional top-of-screen graphical oddities | Way of the Warrior (Europe).chd<br>Way of the Warrior (Japan).chd<br>Way of the Warrior (USA).chd | No data | Not tested yet |
| Main list | Who Shot Johnny Rock | 100 | Good | 1.3.0.1 | — | Who Shot Johnny Rock (USA).chd | No data | Not tested yet |
| Main list | Wicked 18 | 95 | Good | 1.3.0.1 | Menu font rendering inaccurate | Wicked 18 (USA).chd | No data | Not tested yet |
| Main list | Wing Commander III - Heart of the Tiger | 90 | Good | 1.1.6.4 Beta | (Colors fixed in 1.1.6.3) Random Freezes on Videos. | Wing Commander III - Heart of the Tiger (Germany) (Disc 1).chd<br>Wing Commander III - Heart of the Tiger (Germany) (Disc 2).chd<br>Wing Commander III - Heart of the Tiger (Germany) (Disc 3).chd<br>+9 more | No data | Not tested yet |
| Main list | Winning Post | 100 | Good | 1.3.0.1 | — | Winning Post (Japan).chd | No data | Not tested yet |
| Main list | Wolfenstein 3d | 100 | Good | 1.2.0.2 | — | Wolfenstein 3D (Europe).chd<br>Wolfenstein 3D (Japan).chd<br>Wolfenstein 3D (USA).chd | No data | Not tested yet |
| Main list | World Cup Golf - Hyatt Dorado Beach | 100 | Good | 1.3.0.1 | — | World Cup Golf - Hyatt Dorado Beach (Europe).chd<br>World Cup Golf - Hyatt Dorado Beach (USA, Europe).chd | No data | Not tested yet |
| Main list | Yuu Yuu Hakusho | 100 | Good | 1.3.0.1 | — | Yu Yu Hakusho (Japan).chd | No data | Not tested yet |
| Main list | Zhadnost - The Peoples Party | 100 | Good | 1.3.0.1 | — | Zhadnost - The People's Party (USA, Europe).chd | No data | Not tested yet |
| Miscellaneous | 3DO Game Guru | 100 | Good | 1.2.0.3 | — | 3DO Game Guru (USA, Europe).chd | No data | Not tested yet |
| Miscellaneous | 3DO Storage Manager | 100 | Good | 1.2.0.3 | — | — | No data | Not tested yet |
| Multimedia | 20th Century Video Almanac | 100 | Good | 1.3.0.1 | — | 20th Century Video Almanac (USA).chd | No data | Not tested yet |
| Multimedia | 3D Atlas | 100 | Good | 1.3.0.1 | — | 3D Atlas (Europe).chd<br>3D Atlas (USA).chd | No data | Not tested yet |
| Multimedia | Aqua World - Umibi Monogatari | — | No data | — | — | Aqua World - Umi Monogatari (Japan).chd | No data | Not tested yet |
| Multimedia | Carrier - Fortress at Sea | 100 | Good | 1.3.0.1 | — | Carrier - Fortress at Sea (USA).chd | No data | Not tested yet |
| Multimedia | Dennis Miller - That's News To Me | 75 | Playable | 1.3.0.1 | Video playback Incorrect. Drops 90% of video frames. | Dennis Miller - That's News to Me (USA).chd | No data | Not tested yet |
| Multimedia | Digital Dreamware | 100 | Good | 1.3.0.1 | — | Digital Dreamware (USA, Europe).chd | No data | Not tested yet |
| Multimedia | ESPN Baseball - Interactive Hitting | — | No data | — | — | ESPN Baseball - Hitting (USA).chd | No data | Not tested yet |
| Multimedia | ESPN Golf - Lower Your Score With Tom Kite - Shot Making | 54 | Playable | 1.3.2.4 | Disc 3 has random freezes and 4 videos total on discs 2 1 and 2 (last 2 videos on disc 2 plus the below feet video on disc one and the last video on disc one all have freezes at some point during the video | ESPN Golf - Lower Your Score with Tom Kite - Shot Making (USA) (Disc 1).chd<br>ESPN Golf - Lower Your Score with Tom Kite - Shot Making (USA) (Disc 2).chd<br>ESPN Golf - Lower Your Score with Tom Kite - Shot Making (USA) (Disc 3).chd<br>+1 more | No data | Not tested yet |
| Multimedia | ESPN Golf - Lower Your Score With Tom Kite - Shot Making - Mental Messages | — | No data | — | — | ESPN Golf - Lower Your Score with Tom Kite - Shot Making (USA) (Disc 1).chd<br>ESPN Golf - Lower Your Score with Tom Kite - Shot Making (USA) (Disc 2).chd<br>ESPN Golf - Lower Your Score with Tom Kite - Shot Making (USA) (Disc 3).chd<br>+4 more | No data | Not tested yet |
| Multimedia | ESPN Golf - Tom Kite no Kore ga Golf Da! | — | No data | — | — | ESPN Golf - Tom Kite no Kore ga Golf da! (Japan) (Disc 1) (Shot Yousei Kouza Vol. 1).chd<br>ESPN Golf - Tom Kite no Kore ga Golf da! (Japan) (Disc 2) (Shot Yousei Kouza Vol. 2).chd<br>ESPN Golf - Tom Kite no Kore ga Golf da! (Japan) (Disc 3) (Pro ni Kike!).chd | No data | Not tested yet |
| Multimedia | ESPN Let's Go Skiing with Picabo Street, A. J. Kitt & Holly Flanders | — | No data | — | — | ESPN Let's Go Skiing with Picabo Street, A. J. Kitt & Holly Flanders (USA).chd | No data | Not tested yet |
| Multimedia | ESPN Let's Play Beach Volleyball with Karch Kiraly & Karolyn Kirby | — | No data | — | — | ESPN Let's Play Beach Volleyball with Karch Kiraly and Karolyn Kirby (USA).chd | No data | Not tested yet |
| Multimedia | ESPN Let's Play Soccer with John Harkes and Mia Hamm | — | No data | — | — | ESPN Let's Play Soccer with John Harkes and Mia Hamm (USA).chd | No data | Not tested yet |
| Multimedia | ESPN Let's Play Tennis with Tracy Austin | — | No data | — | — | ESPN Let's Play Tennis with Tracy Austin (USA).chd | No data | Not tested yet |
| Multimedia | ESPN Step Aerobics | — | No data | — | — | — | No data | Not tested yet |
| Multimedia | Life Stage, The - Virtual House | 100 | Good | 1.3.0.1 | — | Life Stage, The - Virtual House (Japan) (Rev 1).chd<br>Life Stage, The - Virtual House (Japan).chd<br>Life Stage, The - Virtual House (USA, Korea).chd | No data | Not tested yet |
| Multimedia | Little House, The | — | No data | — | — | Little House, The (Japan).chd | No data | Not tested yet |
| Multimedia | Marine Tour - Diving Spot Guide | — | No data | — | — | Diving Spot Guide - Marine Tour (Japan).chd | No data | Not tested yet |
| Multimedia | Oceans Below | — | No data | — | — | Oceans Below (Japan).chd<br>Oceans Below (USA).chd | No data | Not tested yet |
| Multimedia | San Diego Zoo Presents...The Animals!, The | — | No data | — | — | San Diego Zoo Presents... The Animals!, The (USA).chd | No data | Not tested yet |
| Multimedia | Sid Meier's C.P.U. Bach | 100 | Good | 1.3.0.0 | — | Sid Meier's C.P.U. Bach (USA).chd | No data | Not tested yet |
| Multimedia | Space Shuttle | — | No data | — | — | Space Shuttle (USA).chd | No data | Not tested yet |
| Multimedia | Woody Woodpecker and Friends - Volume 1 | 100 | Good | 1.3.0.1 | — | Woody Woodpecker and Friends - Volume One (USA) (RE-1).chd<br>Woody Woodpecker and Friends - Volume Three (USA).chd<br>Woody Woodpecker and Friends - Volume Two (USA).chd | No data | Not tested yet |
| Multimedia | Woody Woodpecker and Friends - Volume 2 | 100 | Good | 1.3.0.1 | — | Woody Woodpecker and Friends - Volume One (USA) (RE-1).chd<br>Woody Woodpecker and Friends - Volume Three (USA).chd<br>Woody Woodpecker and Friends - Volume Two (USA).chd | No data | Not tested yet |
| Multimedia | Woody Woodpecker and Friends - Volume 3 | 100 | Good | 1.3.0.1 | — | Woody Woodpecker and Friends - Volume One (USA) (RE-1).chd<br>Woody Woodpecker and Friends - Volume Three (USA).chd<br>Woody Woodpecker and Friends - Volume Two (USA).chd | No data | Not tested yet |
| Multimedia | Worldcup Super Stadium | - | No data | - | - | WorldCup Super Stadium (Japan).chd | Playable | Renders the "Story of the World Cup" screen with `panafz10j` plus the FZ-10JA Kanji font ROM (BIOS2); confirmed by the 2026-11-01 extended-boot retry. |

## Local images without a 4DO reference entry

These images still belong in the loadability sweep. Add a 4DO/5DO row when the title is identified.

| Disc file | Root | 5DO status | 5DO notes |
|---|---|---|---|
| 3DO de Shiru Miru Asobu - Nakajima Miyuki (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Adventures Of Captain J, The (USA) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Agi Gongnyong Goldie (Korea).chd | J:\Panasonic 3DO | No data | Not tested yet |
| AI Shougi (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Anno Mitsumasa Pepperon-mura no Shiki (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Armageddon (Korea).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Bakushou!! All Yoshimoto Quiz-ou Kettei-sen (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Battle Blues (Korea).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Belzerion (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| BFS - Blue Forest Story (Japan) (Alt).chd | J:\Panasonic 3DO | No data | Not tested yet |
| BFS - Blue Forest Story (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| BioFury (USA) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Bodyconscious Digital Rave! Part 1 - Shinjuku & Takasaki (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| D no Shokutaku (Japan) (Disc 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| D no Shokutaku (Japan) (Disc 2).chd | J:\Panasonic 3DO | No data | Not tested yet |
| D's Diner - The Director's Cut (Japan) (Disc A).chd | J:\Panasonic 3DO | No data | Not tested yet |
| D's Diner - The Director's Cut (Japan) (Disc B).chd | J:\Panasonic 3DO | No data | Not tested yet |
| D's Diner - The Director's Cut (Japan) (Disc C).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Daedalus Encounter, The - Episode 1 - Nanpasen no Alien (Japan) (Disc 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Daedalus Encounter, The - Episode 1 - Nanpasen no Alien (Japan) (Disc 2).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Daedalus Encounter, The - Episode 1 - Nanpasen no Alien (Japan) (Disc 3).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Daedalus Encounter, The - Episode 1 - Nanpasen no Alien (Japan) (Disc 4).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Decathlon (USA) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Dennou Hyouryuu - Multimedia Cruising (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Emit Vol. 1 - Toki no Maigo (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Emit Vol. 2 - Inochigake no Tabi (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Emit Vol. 3 - Watashi ni Sayonara o (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| espn fitness pros - step aerobics (usa).chd | G:\MAME\MAME Software List CHDs\3do | No data | Not tested yet |
| ESPN Fitness Pros - Step Aerobics (USA).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Eye of Typhoon, The (Korea) (Ko) (Disc 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Eye of Typhoon, The (Korea) (Rerelease).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Eye of Typhoon, The (Korea).chd | J:\Panasonic 3DO | No data | Not tested yet |
| F1GP (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Fatty Bear's Fun Pack (Korea).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Fatty Bear's Funpack (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Fatty Bear's Funpack (USA).chd | J:\Panasonic 3DO | No data | Not tested yet |
| FB Battery Navi Ver. II (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Fire Ball!! (Japan) (Rev 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Fire Ball!! (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Firewall (Korea).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Flopon World (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Footie (USA) (Powerslide Bonus Disk) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Fun 'n Games - Soft no Omochabako (Japan) (En,Ja).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Game no Tatsujin (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Gateway to English - Eigo de Go! (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Ghost Hunter Series - Kuroki Shi no Kamen (Japan) (Rev 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Ghost Hunter Series - Kuroki Shi no Kamen (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Goal FH (Field Hunter) (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Great! Soccer Kid (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Grimm Meisaku Gekijou Vol. 1 - Bremen no Ongakutai (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Grimm Meisaku Gekijou Vol. 2 - Hansel to Gretel (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Grimm Meisaku Gekijou Vol. 3 - Akazukin (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hatsubai 1 Shuunen - 3DO REAL - Tokubetsu Premium CD (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hello Kitty - Asobi no Omochabako (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hirata Shougo Interactive Ehon - Aesop Monogatari Vol. 1 (Japan) (En,Ja,Fr).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hirata Shougo Interactive Ehon - Cinderella (Japan) (En,Ja,Fr).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hirata Shougo Interactive Ehon - Ningyo-hime (Japan) (En,Ja,Fr).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hirata Shougo Interactive Ehon - Ookami to Shichihiki no Koyagi (Japan) (En,Ja,Fr).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hirata Shougo Interactive Ehon - Sanbiki no Kobuta (Japan) (En,Ja,Fr).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hirata Shougo Interactive Ehon - Shirayuki-hime (Japan) (En,Ja,Fr).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Hitchcock ni Chousen (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Icebreaker II (USA) (Bonus Disk) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Icebreaker II (USA) (The Game) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Ide Yousuke Meijin no Shin Jissen Mahjong (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Idol Janshi Suchie-Pai Special (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Idol Mahjong - Final Romance 2 - Hyper Edition (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Igo Time Trial - Shikatsu Daihyakka (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Igo Time Trial - Tsumego I (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Immercenary - Combat in a Digital World (USA).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Incredible Machine, The - Pararancho (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Ishida Yoshio Kudan no Igo Seiha (Japan).chd | J:\Panasonic 3DO | Playable | Renders by 6,000 frames with panafz1. |
| J. B. Harold - Blue Chicago Blues (Japan) (Disc 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| J. B. Harold - Blue Chicago Blues (Japan) (Disc 2).chd | J:\Panasonic 3DO | No data | Not tested yet |
| J.League Virtual Stadium '95 (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| J.League Virtual Stadium (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Jiei Shoubou Katsudou no Gakushuu - 3DO TES (3DO Training Educational System) (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Jietai World (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| John Madden Football (USA) (Alt).chd | J:\Panasonic 3DO | No data | Not tested yet |
| John Madden Football (USA).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Kakinoki Shougi (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Keiba Saishou no Housoku (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Kero Kero Keroppi to Origami no Tabibito (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Konpeki no Kantai (Japan) (Rev 1).chd | J:\Panasonic 3DO | Playable | Renders by 6,000 frames with panafz1. |
| Konpeki no Kantai (Japan).chd | J:\Panasonic 3DO | Playable | Renders by 6,000 frames with panafz1. |
| Kyuumei Kyuukyuu Teate no ABC (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Kyuusei Senjutsu ni Yoru - Heisei Kaiun Koyomi (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Lost Dungeon (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Mahjong Gokuu Tenjiku (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Mahjong-kyou Jidai - AV Gal Seifuku-hen (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Mahjong-kyou Jidai Cogal Houkago-hen (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Makaroni Hourensou Interactive (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Memory Gwanli CD (Korea) (RE).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Mirai Shounen Conan - Digital Library (Japan).chd | J:\Panasonic 3DO | Playable | Menu renders with panafz1; the sweep CHD error was transient. |
| Monoshiri Jiyuugaku - Ogura Hyakunin Isshu-hen (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Moon Cradle - Igyou no Hanayome (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Multimedia Shinsho - Driving School - Futsuu Menkyo Gakka-hen (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Murphy da yo Zen'in Shuugou!! (Japan) (En,Ja).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nais How's 1 - Front How's ('95.8.1 Version 1.0 R2.0) (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Naoko to Hide-bou - Kanji no Tensai 1 - Bushu-hen, Bushu no Namae ga Tanoshii Chou Kioku-hou (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Naoko to Hide-bou - Sansuu no Tensai 1 - Wa to Sa no Bunshoudai Tsurukamezan Part I (Butsubutsu Koukan no Maki) (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Naoko to Hide-bou - Sansuu no Tensai 2 - Hayasa no Bunshoudai Tsuukasan Part I (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| National-Panasonic Collection (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nemurenu Yoru no Chiisana Ohanashi (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| New How's 1 - Front How's ('94.9.1 Version 1.0) (Japan) (Rev A).chd | J:\Panasonic 3DO | No data | Not tested yet |
| NFL Madden Football (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nice Body - For Professional Use (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nishimura Kyoutarou Travel Mystery - Akugyaku no Kisetsu - Tokyo Nanki Shirahama Renzoku Satsujin Jiken (Japan) (Disc 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nishimura Kyoutarou Travel Mystery - Akugyaku no Kisetsu - Tokyo Nanki Shirahama Renzoku Satsujin Jiken (Japan) (Disc 2).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nobunaga no Yabou - Haouden (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nontan to Issho - Hoshi no Okurimono (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nontan to Issho - Nohara de Asobo (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Nova-Storm (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Ogura Hyakunin Isshu (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Oilman (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Onee-san to Issho! Janken Paradise (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Onee-san to Issho! Kisekae Paradise (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| OnSide Soccer (USA) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Oukoku no Grand Chef (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Outer World (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Oyaji Hunter Mahjong (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Panasonic Special CD-ROM (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Perfect World - Combat in a Digital World (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Peter Frankl - Puzzle no Tou (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Policenauts - Pilot Disk (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Powers Kingdom (Europe).chd | J:\Panasonic 3DO | Playable | Anvil BIOS required; live-action intro at 65,000 frames (2026-11-01 retry). |
| Powers Kingdom (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Powerslide (USA) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Pretty Soldier SailorMoon S (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Primal Rage (USA, Europe).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Pro Yakyuu Virtual Stadium - Professional Baseball (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Putt-Putt's Fun Pack (Japan) (En,Ja).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Putt-Putt's Fun Pack (USA, Korea).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Puzzle Bobble (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Pyramid Intruder (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Remute 3D (Europe) (En,De) (Unl).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Royal Pro Wrestling - Jikkyou Live!! (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Sangokushi IV (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Scramble Cobra - Premium Version (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Secre Fuumin no Omochabako (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Seimei Handan (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Sento Monogatari - Sono I (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| SHADOW Warriors (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Shanghai - Banri no Choujou - The Great Wall (Japan) (Rev 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Short Warp - Warp's Short Game Collection (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Sotsugyou - Graduation - Final (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Sotsugyou II Special - Neo Generation (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Star Control II - The Ur-Quan Masters (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Storage Manager - Memory Kanri (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Super Nova - Battery Navi (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Super Real Mahjong PIV   Aishou Shindan (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Super Street Fighter II X - Grand Master Challenge (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Sword & Sorcery (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| System Catalog - Kigyou-muke System no Goannai (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| T&E VR Golf - Masters - Harukanaru Augusta 3 (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| T&E VR Golf - Pebble Beach no Hatou (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| T&E VR Golf - Waialae no Kiseki (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Taiketsu Rumiizu! (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Takeru (Japan) (Rev 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Tanjou - Debut - Pure (Japan) (Rev A).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Tarot Uranai (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Tetsujin (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Theatre Wars - Goraku no Dendou (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Toki o Koeta Tegami (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Tokimeki Mahjong Paradise Special - Koi no Tenpai Beat (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Totsugeki Karakuri! Megadas!! (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Touhaiden Akagi (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Toyopet Outdoor World (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Toyopet RV Paddock (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Toyopet RV World (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Tozasareta Yakata (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| True Golf Classics - Pebble Beach Golf Links (Europe).chd | J:\Panasonic 3DO | No data | Not tested yet |
| True Golf Classics - Waialae Country Club (Germany).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Virtual Horror - Norowareta Yakata (Japan) (En,Ja).chd | J:\Panasonic 3DO | No data | Not tested yet |
| virtual puppet reika (japan).chd | G:\MAME\MAME Software List CHDs\3do | No data | Not tested yet |
| Virtual Puppet Reika (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Virtual Quest - Pharaoh no Fuuin (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Warner Music Group (USA).chd | J:\Panasonic 3DO | No data | Not tested yet |
| We Got It (Europe).chd | J:\Panasonic 3DO | No data | Not tested yet |
| World Cup Special (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Yakyuuken Special, The (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Yamada Kamachi Bijutsukan - Kamachi's Museum (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Yamamura Misa Suspense - Kyoto Kurama Sansou Satsujin Jiken (Japan) (Disc 1).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Yamamura Misa Suspense - Kyoto Kurama Sansou Satsujin Jiken (Japan) (Disc 2).chd | J:\Panasonic 3DO | No data | Not tested yet |
| Yuujou Densetsu - The Doraemons (Japan).chd | J:\Panasonic 3DO | No data | Not tested yet |

## Sweep workflow

1. Build the CLI: `dotnet build src/FiveDo.Cli -c Release`.
2. Run the collection sweep: `pwsh -File tools/compat-sweep.ps1 -Frames 3000`.
3. Inspect `artifacts/compat/summary.csv` and the per-game JSON/PNG evidence.
4. Update the `5DO status` and `5DO notes` columns for every tested title.
5. Re-run a title with `--high-res` and record the result separately when the high-resolution CEL path is being evaluated.