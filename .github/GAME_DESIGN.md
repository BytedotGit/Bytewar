# ByteWar — Game Design Document

> This is the authoritative game design spec. All AI agents MUST consult this document when making gameplay, balance, economy, or systems design decisions. Engineering process rules are in `GAME_PROTOCOL.md`.

---

## 1. High-Level Vision & Aesthetic

- **Genre**: Third-Person PvPvE Extraction RPG / Hero Shooter Hybrid.
- **Core Philosophy**: High-stakes open-world risk, deep player autonomy in base building, and bracketed competitive arenas.
- **Visual Aesthetic**: Custom high-poly, detailed player models (developed via Blender) set against stylized, low-poly environments. The contrast between detailed characters and stylized worlds is key to visual readability.

### Art Direction Bible (WIP, agent-enforced)

- **Primary references**: Valheim + Enshrouded blend.
- **Environment detail rule**: Hero props can carry richer surface detail; background props stay simpler for performance and readability.
- **The Wilds mood**: Moodier/foggier atmosphere is preferred over bright daylight.
- **Wear level**: Wear/dirt is per-asset and must be explicitly specified (clean / light wear / heavy wear) to avoid inconsistent styling.

---

## 2. The Core Gameplay Triangle

The game is anchored by three distinct zones that feed into an infinite progression loop:

### A. The Wilds (PvPvE Extraction Zone)

- **Purpose**: The primary resource gathering and open-world PvP zone.
- **Mechanic**: Full PvP. Death results in dropping unbanked resources gathered during that session. Equipped gear is never lost.
- **Biomes & Resources**: Moderately deterministic. Specific biomes have a higher probability of yielding specific resource types, encouraging targeted farming without completely removing RNG.

### B. The Stronghold (Player Hub & Base Building)

- **Purpose**: A private, instanced progression hub.
- **Building System**: High autonomy (Valheim-style). Infinite/sprawling limits dictated only by the resources the player gathers. Players freely mix mechanical structures (crafting stations, storage) with aesthetic architecture.
- **The Economic Sink**: Bases are subject to PvE attacks/sieges (see Section 5). Players must actively defend their Strongholds and spend resources to build defenses and repair damage, creating an organic economic sink without forced "upkeep taxes."

### C. The Arena (Structured PvP)

- **Purpose**: The competitive endgame. 3v3 or 5v5 objective-based matchmaking.
- **Mechanic**: Gear matters. High-tier gear has better stats and unique mechanical perks compared to low-tier gear.
- **Matchmaking**: Players are matched based on a combination of Gearscore brackets and MMR (Matchmaking Rating) to ensure fair fights.
- **Reward**: Winning grants "Gladiator Tokens" (a fixed currency, zero RNG) used to buy high-tier Blueprints.

---

## 3. The Classless Loadout System

A player's role and playstyle are entirely dictated by the combination of gear they craft and equip.

### Races (Inherent Traits)

Players choose a completely original Race at character creation. Racial passives strictly focus on **out-of-combat utility, economy, or exploration** (e.g., 5% faster extraction, larger base grid) to ensure combat remains perfectly balanced and gear-dependent.

### Equipment Slots

| Slot   | Name             | Determines                                                                                             |
| ------ | ---------------- | ------------------------------------------------------------------------------------------------------ |
| Weapon | **The Catalyst** | Primary Attack (LMB), Secondary Attack/Block (RMB), and active abilities                               |
| Armor  | **The Chassis**  | Health pool, movement speed, passive mitigation, and dodge mechanics (e.g., Heavy step vs. Light roll) |
| Relic  | **The Augment**  | One highly impactful "Ultimate" or Utility ability on a long cooldown                                  |

### UI Freedom & Skill Caps

- Players have total UI customizability (e.g., multiple action bars, 1-9 defaults).
- The total number of active skills/abilities equipped at one time is **strictly capped (6-8 slots)** to maintain combat readability and prevent "piano-key" rotations.

---

## 4. Combat & Sustain Mechanics

### No Dedicated Healers

Support roles focus on **utility, mitigation, and crowd control** rather than UI-gazing health restoration.

### Hybrid Sustain Model

| Layer    | Name                             | Behavior                                                                                                                                                                                  |
| -------- | -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Outer    | **Energy Shield** (The Skirmish) | Regenerating buffer. Absorbs initial damage. Recharges after avoiding damage for a set time. Encourages aggressive poking.                                                                |
| Inner    | **Core Armor** (The Commitment)  | Once shield breaks, damage hits Core Armor. This damage is **permanent** during the firefight.                                                                                            |
| Recovery | **Out-of-Combat Preparation**    | To heal Core Armor, players must disengage, hide, and use craftable consumables (made from Wilds resources). Using these items triggers a **vulnerable animation with a loud audio cue**. |

---

## 5. Stronghold PvE Sieges (The "Heat" System)

- **Unpredictable Heat**: As players refine high-tier resources or hoard wealth, their base generates "Heat." As the Heat meter fills, the probability of a PvE siege increases, preventing players from perfectly timing or farming the event.
- **The Target**: There is no central "Core." Enemies attack the base as a whole, smashing through walls and turrets to breach the **Vaults** where wealth is stored.
- **The Grace Period**: A hidden 5-minute timer prevents a siege from triggering immediately after a player returns from an extraction, ensuring they aren't punished while vulnerable.
- **Consequences of Failure**: If enemies breach the vaults, structures are **damaged (not deleted)** and must be repaired. Loot in breached vaults is diminished (e.g., losing 30% of stored resources), punishing greed without causing total player churn.

---

## 6. The "Opt-In" Eclipse Event

- **The Mechanic**: Instead of a forced server-wide base attack, the Eclipse is an **opt-in event** triggered from the Stronghold.
- **The Execution**: Activating the Eclipse spawns a highly contested, high-value node/boss in a specific area of The Wilds.
- **The PvPvE Clash**: The triggering party (and anyone else on the server who notices the event) must travel to the node, fight the PvE threats, fight each other, and successfully extract with the loot.

---

## 7. The Scavenger Economy (The Vulture Loop)

A dedicated gameplay loop for low-tier or solo players to profit from high-tier clashes.

- **Corrupted Residue**: When elite mobs/bosses die during an Eclipse, they leave behind "Corrupted Residue." High-tier players ignore this due to inventory space/value ratios.
- **Active Harvesting**: Low-tier players use cheap "Extraction Syringes" to harvest this residue. Harvesting takes **5-8 seconds** and emits a **loud audio cue**, drawing in other scavengers for balanced, low-tier PvP fights over the scraps.
- **Volatile & Dangerous**: The residue evaporates **3-5 minutes** after the mob dies, forcing scavengers to move in while the area is still hot. Carrying the residue applies a "radioactive" debuff (e.g., draining max stamina or emitting a faint glow), making the run to the extraction portal a tense survival-horror experience.

---

## 8. Inventory, Crafting & Player Economy

### Weight-Based Inventory

Loot management is dictated by weight. Players must balance heavy, high-value loot against their combat mobility.

### The Secure Pouch

A very small, weight-limited pouch that guarantees the extraction of a few crucial items (like keys or rare micro-materials) even if the player dies.

### Physical Blueprint Progression

Crafting recipes are **not unlocked via skill trees**. Players must find Physical Blueprints in The Wilds or The Arena, extract them, and decode them at a Stronghold terminal to learn the recipe.

### Hybrid Player Economy

| Category                                      | Tradeable?                     | Rule                                                                                     |
| --------------------------------------------- | ------------------------------ | ---------------------------------------------------------------------------------------- |
| Raw materials, consumables, Corrupted Residue | **Yes** — Global Auction House | Freely traded on a global market (allows Scavengers to sell to high-tier players)        |
| Finished Gear, Weapons, Armor, Blueprints     | **No** — Bind on Pickup (BoP)  | Player Found Only. Cannot buy your way to maximum power; must craft or loot it yourself. |

---

## 9. Anti-Griefing Escalation Matrix

To protect new players in low-tier Wilds without using invisible walls:

| Level | Name                                | Trigger                                        | Effect                                                                                                                                                                                      |
| ----- | ----------------------------------- | ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1     | **The Bounty System (Rogue State)** | High-tier player kills a low-tier player       | Griefer gets zero loot. Turns glowing red. Location pinged on server map. Massive bounty placed for other veterans to collect.                                                              |
| 2     | **The Reaper (AI Consequence)**     | Griefer survives Level 1 and continues camping | AI Director spawns an unkillable, teleporting, CC-immune entity (an abstract obsidian monolith). It ignores all other players and relentlessly hunts the griefer until they extract or die. |

---

## Design Invariants (Non-Negotiable Rules)

These rules MUST be respected by all agents when implementing any gameplay system:

1. **No pay-to-win**: Power comes from gameplay, never purchases.
2. **Gear defines role**: The classless system means gear choice = playstyle. No class-locked abilities.
3. **Risk-reward extraction**: Death in The Wilds always costs unbanked resources, never equipped gear.
4. **No dedicated healers**: Support = utility/mitigation/CC, never health bar refilling.
5. **Combat readability**: Max 6-8 active ability slots. No "piano-key" rotations.
6. **Damage permanence in combat**: Core Armor damage is permanent during a fight. Healing requires disengagement + consumable + vulnerable animation.
7. **Economic balance**: High-tier crafting creates Heat. Heat creates sieges. Sieges consume resources. This is the economic sink — not upkeep taxes.
8. **Fair PvP**: Racial passives are out-of-combat only. Arena matchmaking uses Gearscore + MMR.
9. **Anti-grief enforcement**: The Bounty System and The Reaper protect low-tier players without invisible walls.
10. **Blueprint-driven progression**: No skill trees for crafting. Find → Extract → Decode.
