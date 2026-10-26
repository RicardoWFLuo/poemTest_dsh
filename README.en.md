# Poem Adventure · PoemTest

> A Unity project that turns **learning classical Chinese poetry** into a **2D pixel-art RPG**:
> explore the world, let AI explain the poems, then recite them out loud.

**Engine**: Unity 2022.3.60f1c1 (LTS) · **Language**: C# 9.0 · **Platforms**: Windows / Android · **View**: 2D top-down pixel art

> 中文文档见 [README.md](README.md)

---

## 📖 Overview

*Poem Adventure* is a 2D pixel-art RPG aimed at primary-school Chinese-literature education. The player
explores two open maps, fights and gathers, and visits **ten poet NPCs** to study ten classic poems.

Unlike a plain flashcard app, the game wires **AI and speech recognition** into the learning loop:

- **Want to understand a poem?** Ask in-game — Alibaba's **Qwen** returns a 200–300 character objective gloss.
- **Want to see it?** **Wanxiang** generates a Chinese-style illustration from the poem's own prompt.
- **Ready to be tested?** Walk up to an examiner and **say the next line out loud** — the game grades you with
  **Baidu speech recognition plus pinyin similarity matching**.
- **Prefer written questions?** Take a four-choice quiz instead and earn a token.

Collected tokens are turned in at the quest board for experience and gold, and levels buy skill points on a
24-node skill tree. Read → understand → recite → get rewarded: a complete loop with real feedback.

All content is **ScriptableObject-driven** — poems, items, dialogue, quests, characters, locations and skills
are asset files editable directly in the Inspector, so new course material can be added without touching code.

---

## ✨ Features

| | Feature | Notes |
|---|---|---|
| 📚 | **Ten classic poems** | 咏鹅, 春晓, 池上, 静夜思, 所见, 悯农, 寻隐者不遇, 登鹳雀楼, 风, 小儿垂钓 — fully data-driven |
| 🤖 | **AI poem explanation** | Alibaba Bailian (DashScope) · Qwen `qwen-turbo`, prompted for an objective 200–300 character gloss |
| 🎨 | **AI poem illustration** | DashScope · Wanxiang `wan2.6-t2i`, generates a 1024×576 illustration from a per-poem prompt and displays it immediately |
| 🎤 | **Voice recitation exam** | Microphone → 16 kHz PCM → Baidu ASR → **Hanzi-to-Pinyin + edit-distance similarity** grading, so a single misheard character does not fail the player |
| ✅ | **Multiple-choice exams** | Poetry comprehension questions (e.g. "What was the child secretly picking in *On the Pond*?"); a correct answer yields a token |
| 📜 | **Full quest system** | Objectives track **items / locations / conversations**; includes a quest board and a quest log with accept, decline and turn-in flows |
| 🎒 | **Inventory and shop** | Stacking, use, drop and loot; the shop has item/weapon/armour tabs with buying and selling |
| ⚔️ | **Action combat** | Melee and bow with weapon switching, cooldowns, knockback and hit-stun, plus on-screen range gizmos |
| 📈 | **Progression** | Experience from kills and items, multi-level level-ups, and a 24-node skill tree (attack / health / speed) |
| 🕹️ | **Unified PC and mobile input** | One input layer: keyboard on desktop, virtual joystick and touch buttons on device |
| 🗺️ | **Two scenes with persistent state** | Travel between maps freely; player, UI, inventory and quest progress survive the transition |
| 💬 | **Data-driven dialogue** | Conditional lines, branching options, quiz logic and quest hooks, with `{PoemTitle}` `{Poet}` `{PoemContent}` `{SelectedLines}` placeholders that inject poem text automatically |

---

## 🎮 Gameplay and content

### Loop

```
        ┌───────────────── Explore ─────────────────┐
        │  combat · gather mushrooms/steaks · collect gold and EXP  │
        └────────────────────┬──────────────────────┘
                             │
      ┌──────────────────────┼───────────────────────┐
      ▼                      ▼                       ▼
  Poet NPCs             Examiner NPCs            Coloured NPCs
  (10)                  (10)                     (blue / yellow / purple)
      │                      │                       │
  poem dialogue        voice recitation        quest dialogue
      │                (say the next line)      (tutorial chain)
      ├─► Explanation: AI-generated gloss            │
      │                                    correct answer → "Bone"
      └─► Illustration: AI-generated art             │
                             │                      │
                        pass → "Pumpkin"            │
                             └───────────┬──────────┘
                                         ▼
                        Quest board turn-in (10 Bones / 10 Pumpkins)
                                         ▼
                            Reward: 100 EXP + 50 Gold
                                         ▼
                            Level up → skill points → 24-node tree
```

### The ten poems

| # | Title | Author |
| :---: | --- | --- |
| 1 | 咏鹅 *Ode to the Goose* | 骆宾王 Luo Binwang |
| 2 | 春晓 *Spring Dawn* | 孟浩然 Meng Haoran |
| 3 | 池上 *On the Pond* | 白居易 Bai Juyi |
| 4 | 静夜思 *Quiet Night Thought* | 李白 Li Bai |
| 5 | 所见 *What I Saw* | 袁枚 Yuan Mei |
| 6 | 悯农（其二）*Sympathy for the Peasants II* | 李绅 Li Shen |
| 7 | 寻隐者不遇 *Seeking the Hermit in Vain* | 贾岛 Jia Dao |
| 8 | 登鹳雀楼 *On the Stork Tower* | 王之涣 Wang Zhihuan |
| 9 | 风 *The Wind* | 李峤 Li Qiao |
| 10 | 小儿垂钓 *A Child Fishing* | 胡令能 Hu Lingneng |

### NPCs and quests

| Character | Role |
| --- | --- |
| Poet NPCs × 10 | One per poem; entry point for dialogue, AI explanation and AI illustration |
| Examiner NPCs × 10 | Voice recitation exam; passing awards 1 Pumpkin |
| Purple NPC | Quiz examiner; a correct answer awards 1 Bone |
| Blue NPC | Gives and receives Tutorial Quest I |
| Yellow NPC | Talk target of Tutorial Quest III |
| Knight | Combat target |

| Quest | Level | Objectives | Rewards |
| --- | :---: | --- | --- |
| Tutorial I | 1 | Reach the hills + gather 3 mushrooms + return to the Blue NPC | 10 EXP, 10 Gold, 3 Mushrooms |
| Tutorial II | 1 | Collect 2 steaks | 10 EXP, 10 Gold |
| Tutorial III | 1 | Talk to the Yellow NPC | — |
| Poem Exam | 10 | Accumulate 10 Bones from quizzes | 100 EXP, 50 Gold |
| Recitation Exam | 10 | Accumulate 10 Pumpkins from voice exams | 100 EXP, 50 Gold |

### Controls

| Key | Action | Key | Action |
| :---: | --- | :---: | --- |
| `W A S D` / arrows | Move | `Q` | Stats panel |
| `J` | Melee attack | `E` | Skill tree |
| `K` | Shoot arrow | `Z` | Start / stop recording |
| `V` | Switch melee / bow | `1` `2` `3` | Use item (sell in a shop) |
| `F` | Interact (dialogue, exam, explanation, illustration, shop, quest board) | `4` `5` `6` | Drop item |

> No code changes are needed for mobile: `MobileInputManager` enables the virtual joystick and touch buttons
> on device and hides them on desktop/editor, switching to keyboard input automatically.

---

## 🖼️ Art preview

The game uses 2D pixel art. A few assets actually used in the project:

**Buildings and scenery**

| | | | |
| :---: | :---: | :---: | :---: |
| ![Castle](Assets/Sprites/Resource/Building/Castle.png) | ![Monastery](Assets/Sprites/Resource/Building/Monastery.png) | ![Tower](Assets/Sprites/Resource/Building/Tower.png) | ![House](Assets/Sprites/Resource/Building/House_Red.png) |
| Castle | Monastery | Tower | House |

**Combat and item icons**

| | | | |
| :---: | :---: | :---: | :---: |
| ![Health](Assets/Sprites/Icons/Combat/Heart.png) | ![Attack](Assets/Sprites/Icons/Combat/Attack.png) | ![Speed](Assets/Sprites/Icons/Combat/Speed.png) | ![Poem item](Assets/Sprites/Resource/3ItemAboutPoems/ItemExplain.png) |
| Health | Attack | Speed | Poem explanation item |

**Character sprite sheets**

| | | |
| :---: | :---: | :---: |
| ![Poet NPC](Assets/Sprites/Resource/NPC/Pawn_Purple.png) | ![Examiner NPC](Assets/Sprites/Resource/NPC/Pawn_Red.png) | ![Archer](Assets/Sprites/Player/Archer_Blue.png) |
| Poet NPC | Examiner NPC | Archer (player) |

> More art lives under `Assets/Sprites/` (terrain, water, trees, houses, 9-slice UI, fonts, icons).

---

## 🛠️ Technical architecture

### Environment

| Item | Version |
| --- | --- |
| Unity | 2022.3.60f1c1 (LTS; other 2022.3.x releases work) |
| C# | 9.0 (.NET Standard 2.1) |
| Cinemachine | 2.10.7 (2D camera confiner) |
| TextMesh Pro | 3.0.9 (UI text and Chinese fonts) |
| 2D Feature Set | 2.0.1 (Tilemap / 2D sprites) |
| Input | Legacy Input Manager (the Input System package is not used) |
| Resolution | 1920 × 1080 |

### Data-driven: everything is a ScriptableObject

| Asset type | Purpose |
| --- | --- |
| `PoemSO` | Poem: title, author, text, AI explanation prompt, AI illustration prompt |
| `ItemSO` | Item: name, icon, stack size, stat bonuses, consumable flag |
| `ActorSO` | Character: name, portrait |
| `DialogueSO` | Dialogue node: lines, options, quiz logic, quest hooks, display conditions |
| `QuestSO` | Quest: objectives (item / location / conversation), rewards |
| `LocationSO` | Location: id, display name |
| `SkillSO` | Skill: name, max level, per-level increment, icon |

### Event-driven: no hard references between systems

| Event | Publisher | Subscribers |
| --- | --- | --- |
| `QuestEvents.OnQuestOfferRequested` / `OnQuestTurnInRequested` | `DialogueManager`, `QuestBoard` | `QuestLogUI` |
| `QuestEvents.OnQuestAccepted` | `QuestLogUI` | `NPCTalk`, `PoemTalk` |
| `QuestEvents.IsQuestComplete` | `QuestManager` | `QuestBoard`, `DialogueSO` |
| `EnemyHealth.OnMonsterDefeated` | `EnemyHealth` | `ExpManager` |
| `InventoryManager.OnExperienceGained` | `InventoryManager` | `ExpManager` |
| `Loot.OnItemLooted` | `Loot` | `InventoryManager` |
| `ShopKeeper.OnShopStateChanged` | `ShopKeeper` | `InventorySlot` |
| `SkillSlot.OnAbilityPointSpent` / `OnSkillMaxed` | `SkillSlot` | `SkillManager`, `SkillTreeManager` |

### Main modules

| Module | Directory | Responsibility |
| --- | --- | --- |
| Global | `Scripts/GameManager.cs` | Singleton and cross-scene persistent object registration |
| Input | `Scripts/MobileScript/` | Unified PC / mobile input abstraction |
| Player | `Scripts/Player/` | Movement, melee, bow, health, stats, EXP, death and respawn |
| Enemy | `Scripts/Enemy/` | State-machine AI (idle / chase / attack / knockback), health, hit-stun |
| Poetry | `Scripts/PoemScripts/` | Poem data, AI explanation and illustration, recitation examiner |
| Voice | `Scripts/Voice/` | Recording, ASR requests, voice interaction UI |
| Dialogue | `Scripts/NPCScript/` | NPC state machine, dialogue system, history trackers |
| Quests | `Scripts/QuestSystem/` | Quest manager, quest log UI, quest board |
| Inventory & shop | `Scripts/Inventory&Shop/` | Inventory, items, loot, item use, shop and item info panel |
| Skill tree | `Scripts/SkillTree/` | Skill data, slots, prerequisite unlocking, point spending |
| UI | `Scripts/UIScripts/` | Menu management, centralised pause control |
| Camera / terrain / scenes | `Scripts/CameraScripts/`, `Scripts/TilemapScripts/`, `Scripts/SceneChange/` | 2D camera confiner, elevation collider switching, scene transitions |
| Pinyin | `Scripts/Plugins/NPinyin/` | Hanzi to Pinyin (third party) |

### Project layout

```
poemTest/
├── Assets/
│   ├── Prefabs/                 Prefabs (NPCs, player, UI, loot)
│   ├── Resources/               api_keys.example.json template
│   ├── Scenes/                  RPG1.unity (main) / RPG2.unity
│   ├── Scripts/                 All game scripts (see table above)
│   ├── Sprites/                 Art assets
│   └── TextMesh Pro/            Bundled TMP resources
├── Packages/manifest.json
├── ProjectSettings/
├── docs/                        Development notes
├── README.md / README.en.md
└── SECURITY.md
```

---

## 🚀 Getting started

### 1. Requirements

- Unity Hub with **Unity 2022.3.60f1c1** (or another 2022.3.x)
- 5 GB or more of free disk space (the first Library import is large)

### 2. Run the project

```bash
git clone <repository-url>
cd poemTest
```

1. Unity Hub → `Add` → select the repository root;
2. Wait for the first asset import and package restore to finish;
3. Open `Assets/Scenes/RPG1.unity`;
4. Press **Play**.

Both scenes are registered in `Build Settings`, so the project can be built directly.

### 3. Configure API keys (needed for AI and speech features)

For security the project ships **no keys at all** — supply your own:

```bash
cp Assets/Resources/api_keys.example.json Assets/Resources/api_keys.json
```

```json
{
  "qwenTextKey": "Alibaba Bailian text-generation API key (poem explanation)",
  "qwenImageKey": "Alibaba Bailian image-generation API key (poem illustration)",
  "baiduApiKey": "Baidu Smart Cloud ASR API key",
  "baiduSecretKey": "Baidu Smart Cloud ASR secret key"
}
```

Alternatively use environment variables, which is convenient for CI or switching environments:

| Variable | Key |
| --- | --- |
| `POEMTEST_QWEN_TEXT_KEY` | Qwen text generation |
| `POEMTEST_QWEN_IMAGE_KEY` | Wanxiang image generation |
| `POEMTEST_BAIDU_API_KEY` | Baidu ASR API key |
| `POEMTEST_BAIDU_SECRET_KEY` | Baidu ASR secret key |

> `Assets/Resources/api_keys.json` is git-ignored.
> The resolution logic lives in `Assets/Scripts/Config/ApiKeyConfig.cs`.

### 4. Build notes

- **Android**: enable microphone permission in `Player Settings` and verify recording device enumeration on a real device;
- The speech features require network access to Baidu's ASR endpoint;
- To shrink the build, delete `Assets/TextMesh Pro/Examples & Extras`.

---

## ❓ FAQ

**Can I play without API keys?**

Yes. Exploration, combat, gathering, quests, inventory, the shop, the skill tree and the
multiple-choice exams all work offline. Only **AI explanation**, **AI illustration** and
**voice grading** report that they are unconfigured.

**It says "AI key not configured" — what now?**

Follow "Getting started → Configure API keys": create `Assets/Resources/api_keys.json` and fill it in,
or set the environment variables and restart Unity.

**Speech recognition keeps failing?**

Check, in order: microphone permission granted → an input device is present →
`baiduApiKey` / `baiduSecretKey` are correct → the machine can reach `vop.baidu.com`.
Recording is fixed at 16000 Hz, matching Baidu's requirement.

**Why can't I see the virtual joystick in the editor?**

That is intentional. The joystick and touch buttons are enabled only in device builds
(non-`UNITY_STANDALONE`); the editor and desktop platforms hide them and use the keyboard.

**Chinese text or comments look garbled?**

All C# scripts have been normalised to **UTF-8 with BOM**, and `.gitignore` now follows the full Unity
rule set. If individual files still look wrong, check your editor and Git encoding settings and do not
re-save scripts as GBK.

---

## 🗺️ Roadmap

The core loop is complete. Still in progress:

- [ ] **Skill tree content**: all 24 nodes currently point at the same skill; they need to be split across attack / health / speed
- [ ] **Stats panel**: crit rate, defence, dodge, mana and block are placeholders (they display a constant)
- [ ] **Save system**: gold, inventory, quest progress and skill points are memory-only and reset on quit
- [ ] **On-device mobile testing**: multi-touch gesture ownership for the virtual joystick
- [ ] **Asset cleanup**: remove development-era test assets and drop `TextMesh Pro/Examples & Extras` from the build
- [ ] **More content**: additional poems and question types (fill-in-the-blank, matching)

---

## 🤝 Contributing

1. Fork the repository
2. Create a `Feat_xxx` branch
3. Commit your changes
4. Open a pull request

**Always check that you are not committing a key:**

```bash
git grep -nE "sk-[A-Za-z0-9]{16,}"
```

Bug reports and ideas for new poetry gameplay are welcome in Issues.

---

## 📄 License and credits

For the code, refer to the license statement in the repository root.

| Asset | Location | License |
| --- | --- | --- |
| NPinyin (Hanzi to Pinyin) | `Assets/Scripts/Plugins/NPinyin/` | See the original project |
| Tiny Speech Indicators (@rehhouari / @pigeonivy) | `Assets/Sprites/UI/Tiny/` | CC0 |
| TextMesh Pro | `Assets/TextMesh Pro/` | Unity Companion License |
| Cinemachine | `Packages/manifest.json` | Unity Companion License |
| Fonts `zpix SDF`, `unifont-17.0` | `Assets/Sprites/Fonts/` | Verify before release |
| Remaining pixel art | `Assets/Sprites/` | Confirm provenance and licensing before release |

> For commercial use, verify the licensing of every asset listed above.

---

## 📚 Related documents

- [README.md](README.md) — 中文介绍
- [SECURITY.md](SECURITY.md) — Key management and security notes (**read this first**)
- [docs/开发记录与修复清单.md](docs/开发记录与修复清单.md) — Architecture details, fix log and verification (Chinese)
