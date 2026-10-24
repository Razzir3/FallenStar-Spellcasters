# TASK-N27: The pony's mane in the game, with a random mane colour per player

- **Source:** [Issue Report #15](../Reports/2026-10-24_IssueReport-15.md) §4 (poll Q5 = d) and §5. Closes **N27**, and N60's `PlayerController.cs` item (poll P4 = b).
- **Owner decision (poll Q5, 2026-10-24):** "Create the enum pool with 5 color, different from team color. Save this mane colors as separete material. Before first spawn of characters - select random unique color for each player. Don't change it before match ends."
- **The mesh exists:** `Assets/Models/PonyMane.fbx` (copied 2026-10-24 from `C:\Lesson\ManeWork\out\`, SHA-256 `b8a6e3ce…c12c3ec`, Git LFS like `PonyBase.fbx`). It has no `.meta` yet: Unity creates it on first import.
- **Severity:** N27 🟢 Low (a warning on every spawn, and no mane). The colours are a feature.
- **Baseline:** this round's commit (TASK-N59_N61_F1, TASK-F4_F6_N36 and TASK-UI7 accepted and committed). Start only after it. Line numbers below are from the working tree on 2026-10-24.
- **Implementer:** Codex. Independent of TASK-F9_F14a_N62: no shared files. Do them one at a time, each with its own baseline snapshot.

## 1. Objective

1. Every pony shows the mane from `PonyMane.fbx`, aligned on its head and neck.
2. At the start of each match, before the first pony spawns, the server gives every player a **random mane colour from a pool of five**, a different one for each player. The pool doesn't contain the team colours (red, blue, green).
3. The colour stays the same for the whole match, including respawns, and both peers see the same colours.
4. The warning "Invalid mane material index." disappears.

## 2. Current behavior

- **`PonyBase.prefab`** (`Assets/Prefab/BoardPrefab/`) is a prefab variant of `Assets/Models/PonyBase.fbx`.
  - Its root (scale 0.3) holds the gameplay components: `CapsuleCollider`, `Rigidbody`, `NetworkObject`, `PlayerController`, `PonyColorScript` and others.
  - The body is the model's child `Vert.002` (`bodyObject`). The prefab removes the model's 8 empty nodes.
  - `PonyColorScript.maneObject` is empty (`PonyBase.prefab:368`).
- **`PonyColorScript`** (`InGame/PonyColorScript.cs`):
  - `m_materials` holds the team materials: `TestMaterial` red, `TestMaterial2` blue, `TestMaterial3` green.
  - `SetMaterials(body, mane)` (`:48-87`) puts `m_materials[body]` on the body and `m_materials[mane]` on the mane, and on the server writes `NetworkBodyColor` / `NetworkManeColor`.
  - With no mane renderer, the mane branch logs **"Invalid mane material index."** (`:85`) on every spawn, on every peer: that's N27.
  - `OnNetworkSpawn`, `OnManeColorChanged` and `SetSelected(false)` re-apply `NetworkManeColor` through `ApplyManeMaterial` (`:132-147`), which also indexes `m_materials`.
- **`PlayerController.ApplyMaterial`** (`InGame/PlayerController.cs:108-115`) runs on **every peer** one frame after spawn, once the session is known (`DelayedInit`, `:92-105`). It calls `SetMaterials(team, team)`.
- **The spawn order:** `MultiplayerManager` (`:1485-1490`) calls `GameMaster.SpawnPlayers()` (blocks and characters, `GameMaster.Spawn.cs:210-245`) **before** `GameMaster.StartNewGame()`. `RespawnCharacter` (`GameMaster.Spawn.cs:80`) spawns a new pony after a death.
- **The mane file** (`C:\Lesson\ManeWork\out\README.md`):
  - FBX 7.4 binary, Blender default axes (−Z forward, Y up), unit scale 1.0, the same header values as `PonyBase.fbx`.
  - **One mesh object, `Mane`**, 3,800 triangles, one material slot `Mane` (a purple placeholder), no armature or animation.
  - **It's modelled in `PonyBase.fbx`'s scene space.** In the file, `Mane` has rotation (−90, 0, 0) and scale 100, like `Vert.002`'s (−90, 0, 0) and 142.07. The two line up when both models' roots sit at the same transform under the same parent.
  - Renders: `C:\Lesson\ManeWork\renders\alignment_front.png`, `alignment_side.png`, `alignment_closeup.png`.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Models/PonyMane.fbx` + its new `.meta` | Import settings (§4.1) |
| `Assets/Materials/Mane/` (**new**): `ManeWhite.mat`, `ManeBlack.mat`, `ManeBrown.mat`, `ManeGold.mat`, `ManePink.mat` | Five mane materials (§4.2) |
| `Assets/Scripts/InGame/ManeColor.cs` (**new**) | The enum (§4.3) |
| `Assets/Scripts/InGame/PonyColorScript.cs` | Mane materials from their own array (§4.4) |
| `Assets/Scripts/InGame/GameMaster/GameMaster.Spawn.cs`, `GameMaster.cs` | Server-side assignment (§4.5) |
| `Assets/Scripts/InGame/PlayerController.cs` | `ApplyMaterial` passes the mane colour; the orphan `//ToDo` (N60) goes (§4.6) |
| `Assets/Prefab/BoardPrefab/PonyBase.prefab` | The mane object, `maneObject`, `m_maneMaterials` (§4.7) |
| `Docs/AI/Architecture/Architecture.md`, `GameLoop.md`, `Dependencies.md` | §4.8 |

**Dependencies:**
- **`PonyBase.prefab` is a network prefab** (`GameMasterPrefab.m_playerCharacterPrefab`). Adding a child without a `NetworkObject` doesn't change its `GlobalObjectIdHash`. Don't add a `NetworkObject`, `Collider` or `Rigidbody` to the mane (RP10).
- **No new NetworkVariable or RPC:** `PonyColorScript.NetworkManeColor` already exists and replicates. Both peers still need the same build, because the prefab changes (RP12).
- **Selection highlight:** `PonyColorScript.SetSelected(true)` turns the body and the mane white; `SetSelected(false)` re-applies the replicated colours. Keep it.

## 4. Required changes

### 4.1 Import `PonyMane.fbx`

Open the project once so Unity imports the file and writes `PonyMane.fbx.meta`. Then set, in the Inspector:

| Tab | Setting | Value | Why |
|---|---|---|---|
| Model | Scale Factor / Convert Units / Bake Axis Conversion | 1 / on / off | **The same as `PonyBase.fbx.meta`** (`globalScale: 1`, `useFileScale: 1`, `bakeAxisConversion: 0`). Different values break the alignment. |
| Model | **Preserve Hierarchy** | **on** (`preserveHierarchy: 1`) | The file has a single top node. Without this, Unity makes `Mane` the model's root and moves its (−90°, ×100) transform onto the root. With it, the root is an empty `PonyMane` at identity and `Mane` is its child, like `Vert.002` under `PonyBase`. |
| Rig | Animation Type | None | No `Animator` on the mane |
| Animation | Import Animation | off | Nothing to import |
| Materials | Material Creation Mode | keep the default | The prefab assigns a mane material anyway (§4.7) |

Don't modify, re-export or reimport `PonyBase.fbx` with other settings: `PonyBase.prefab` refers to objects inside it.

### 4.2 Five mane materials

- **Create `Assets/Materials/Mane/`** with five materials: duplicate `Assets/Materials/TestMaterial0.mat` (URP Lit, the body's white base) and change only `_BaseColor` and `_Color`:

  | Material | Base colour (RGB 0–1, as the Inspector's colour picker shows it) |
  |---|---|
  | `ManeWhite` | (0.95, 0.95, 0.92) |
  | `ManeBlack` | (0.08, 0.08, 0.09) |
  | `ManeBrown` | (0.40, 0.24, 0.12) |
  | `ManeGold` | (0.95, 0.75, 0.20) |
  | `ManePink` | (1.00, 0.55, 0.75) |

- **None of them may be red, blue or green:** the team colours are `TestMaterial` (0.97, 0, 0), `TestMaterial2` (0.05, 0, 0.92) and `TestMaterial3` (0.26, 0.99, 0.08).
- The owner can retune the colours in these materials later without touching code (report #15, poll).

### 4.3 `ManeColor` (new file `Assets/Scripts/InGame/ManeColor.cs`)

```csharp
// Mane colour pool (N27). The order is the index into PonyColorScript.m_maneMaterials.
public enum ManeColor
{
    White,
    Black,
    Brown,
    Gold,
    Pink
}
```

- The value travels as an `int` in `PonyColorScript.NetworkManeColor`. **Append** new values only.

### 4.4 `PonyColorScript`: the mane uses its own materials

- **Add** `[SerializeField] private Material[] m_maneMaterials;` right after `m_materials`. In the prefab its five entries follow the enum order (§4.7).
- **The mane branch of `SetMaterials`** (`:69-86`) and **`ApplyManeMaterial`** (`:132-147`): check the index against `m_maneMaterials.Length` and use `m_maneMaterials[index]` instead of `m_materials`. The body code stays as it is.
- **The warning:** keep "Invalid mane material index." for a real bad index. When `maneRenderer` is null, log nothing: the prefab now always has a mane, and a missing one is a setup error that `Awake` can report once with `Debug.LogWarning("Mane object is not assigned in the inspector.")`, like the body.
- **Keep `NetworkManeColor`'s name, type and permissions.** It now holds a `ManeColor` value.

### 4.5 Server: choose the colours before the first spawn

In `GameMaster` (server only):

```csharp
// N27: one mane colour per player, chosen before the first spawn of a match.
private readonly Dictionary<PlayerSession, ManeColor> m_maneColors = new();

private void AssignManeColors()
{
    m_maneColors.Clear();
    ManeColor[] _pool = (ManeColor[])System.Enum.GetValues(typeof(ManeColor));
    for (int i = _pool.Length - 1; i > 0; i--)
    {
        int j = UnityEngine.Random.Range(0, i + 1);
        (_pool[i], _pool[j]) = (_pool[j], _pool[i]);
    }

    int _next = 0;
    foreach (var _pair in SessionManager.Instance.m_playerSessions)
    {
        PlayerSession _session = _pair.Value;
        if (_session == null || _session.GetSessionRole() != SessionRole.Player)
            continue;
        m_maneColors[_session] = _pool[_next % _pool.Length];
        _next++;
    }
}

public ManeColor GetManeColor(PlayerSession session) =>
    session != null && m_maneColors.TryGetValue(session, out ManeColor _color) ? _color : ManeColor.White;
```

- **Call `AssignManeColors()` in `SpawnPlayers()`** (`GameMaster.Spawn.cs:210`), after the spawn-point checks and **before** the loop that calls `CreateBlock` / `SpawnCharacter`. That is the "before first spawn" moment; `StartNewGame` runs after it.
- **Don't clear `m_maneColors` in `StartNewGame`:** it runs after the spawn. Clear it in `ClearupAfterDisconnect` (`GameMaster.cs:344`), next to `m_players.Clear()`.
- **`RespawnCharacter` needs no change:** the new pony reads the same dictionary entry (§4.6). So the colour stays until the match ends.
- **Unique colours:** two players get two different colours. Only with more than five players would colours repeat (`% _pool.Length`); the game has two.
- Write `UnityEngine.Random` in full (no clash with `System.Random` if a `using System;` is ever added). It runs on the server only. The `Enum.GetValues` allocation happens once per match.

### 4.6 `PlayerController.ApplyMaterial`

Replace the body of `ApplyMaterial` (`PlayerController.cs:108-115`):

```csharp
private void ApplyMaterial()
{
    int _team = m_session.TeamId.Value;
    // The server picks the mane colour (N27); clients use the replicated value
    // and PonyColorScript.OnManeColorChanged applies later updates.
    int _mane = IsServer && GameMaster.Instance != null
        ? (int)GameMaster.Instance.GetManeColor(m_session)
        : m_ponyColorScript.NetworkManeColor.Value;

    if (m_debugMode) Debug.Log($"Applying materials: team {_team}, mane {_mane} for OwnerClientId: {OwnerClientId}");
    m_ponyColorScript.SetMaterials(_team, _mane);
    m_defaultColor = m_meshRenderer.material.color;
}
```

- **On the server,** `SetMaterials` writes `NetworkManeColor`. It replicates, and each client's `OnManeColorChanged` applies it.
- **On a client,** `ApplyMaterial` may run before or after the value arrives. Either way the last applied value is the replicated one. Don't add an RPC.
- **N60 leftover in the same file** (poll P4 = b: clean it with the next task that touches the file): delete the orphan `//ToDo` line and the empty line before it (`PlayerController.cs:36-37`). Nothing else.

### 4.7 `PonyBase.prefab`

1. Open the prefab. Drag `Assets/Models/PonyMane.fbx` onto the **prefab root** (the `PonyBase` object with scale 0.3), so the new `PonyMane` model instance is a direct child of the root, a sibling of `Vert.002`.
2. **`PonyMane`** (the instance root): local position (0, 0, 0), rotation (0, 0, 0), scale (1, 1, 1). **Don't change its child `Mane`:** it keeps the imported rotation (−90, 0, 0) and scale (100, 100, 100), the same pattern as `Vert.002`.
3. **No** `Collider`, `Rigidbody`, `NetworkObject` or `Animator` on `PonyMane` or `Mane`. Layer: the same as `Vert.002`.
4. **`Mane`'s `MeshRenderer`:** slot 0 = `ManeWhite` (so nothing purple shows before the first `SetMaterials`). Cast and receive shadows like `Vert.002`.
5. **The root's `PonyColorScript`:**
   - **Mane Object** = the `Mane` GameObject (the one with the `MeshRenderer`)
   - **Mane Materials** = `ManeWhite`, `ManeBlack`, `ManeBrown`, `ManeGold`, `ManePink`, in this order
   - `m_materials` stays as it is.
6. Save the prefab. `GameScene` and `GameMasterPrefab` need no change.

### 4.8 Docs

- **`Architecture.md`:** a short "Pony appearance" note: team body colour, server-chosen mane colour per match (`ManeColor`, `m_maneColors`, `NetworkManeColor`).
- **`GameLoop.md`:** §3 (match start): `SpawnPlayers` → `AssignManeColors` before the first spawn.
- **`Dependencies.md`:**
  - RP10: the mane child has no network or physics components.
  - **RP22 (new):** `ManeColor` order = `m_maneMaterials` order; append only. `PonyMane.fbx` and `PonyBase.fbx` must keep the same Model import settings.

## 5. Explicit non-goals

- **No change to `PonyBase.fbx`** or its import settings.
- **No body colour change.** Team colours stay red / blue.
- **No mane animation, physics or skinning.** The pony has no rig; the mane is a static child.
- **No player choice of colour,** no colour shown in the lobby or the HUD, no localization keys.
- **No new NetworkVariable or RPC.**
- **No change to selection, colliders or click handling.**

## 6. Constraints and compatibility requirements

- **Keep every serialized field name** (`bodyObject`, `maneObject`, `m_materials`, `NetworkBodyColor`, `NetworkManeColor`) and every existing reference in `PonyBase.prefab`.
- **Server authority:** only the server chooses colours. Clients only read `NetworkManeColor`.
- **RP12:** the prefab changes, so both peers need the same build.
- **Unity 6 APIs, no warnings of our own.** Keep baseline copies of the changed files in `build/n27-verification/baseline/`.
- Restore the dynamic font before handing over (`AGENTS.md`, N28).
- **Do not delete or overwrite `C:\Lesson\ManeWork\`.**

## 7. Acceptance criteria

1. **Import:** `PonyMane.fbx.meta` exists with `preserveHierarchy: 1`, `globalScale: 1`, `useFileScale: 1`, `bakeAxisConversion: 0`, `animationType: 0`, `importAnimation: 0`. The model's hierarchy is `PonyMane` → `Mane`.
2. **Alignment** (in the prefab, root at the origin):
   - The ratio of the mane's world-space height (`Renderer.bounds.size.y`) to the body's is **0.54 ± 0.05** (2.42 m / 4.45 m in Blender). A ratio near 54 or 0.0054, or a mane lying sideways, means the import or the transforms are wrong.
   - The mane's bounds lie inside the body's bounds expanded by 10 % of the body height on every side, and the mane's bounds centre is in the upper 40 % of the body height.
   - Screenshots of the prefab from the front, the side and ¾ match `C:\Lesson\ManeWork\renders\alignment_*.png`: the mane sits on the head and neck, the forelock between the ears.
3. **Colours:**
   - `AssignManeColors` with two player sessions gives two different values. Over 1,000 runs every one of the five values appears.
   - In a match, the host's and the client's ponies have different mane colours, both from the five, and the same on both peers.
   - After a death and respawn, the respawned pony has the same mane colour.
   - A second match on the same host chooses again (it may differ).
4. **Warnings:** no "Invalid mane material index." and no new warning at any spawn, in the Editor or in the release build's `Player.log`.
5. **No regression:** the body keeps its team colour; the selection highlight (white) and its reset still work on the body and the mane; clicking the pony and the board works as before; no new collider.
6. **Build:** 0 errors, no warnings of our own. The release player builds.

## 8. Regression tests

- **Edit Mode** (temporary, `Temp/N27Verification/`): AC1 (read the `.meta`), AC2 (load the prefab, compute the bounds), the uniqueness and coverage part of AC3 (call `AssignManeColors` through a test hook or reflection with two fake sessions).
- **Play Mode** (host fixture, as for F6 last round): spawn both characters, read `NetworkManeColor` and the mane renderer's material on both ponies; kill one pony and respawn it; start a second match. Assert AC3 and AC4 on the host. Count warnings.
- **Baseline:** on the baseline, count "Invalid mane material index." per spawn (expected: one per pony per peer) and report it.
- **Screenshots:** AC2's three prefab views, and one in-match view with both ponies, into `build/n27-verification/`.
- **Not testable by a harness:** what the remote client sees. §9 covers it.
- Remove the harness from `Assets` and `Temp`; keep a copy in `build/n27-verification/`.

## 9. Manual Unity Editor checks

**Setup:** two Editors (main = host, clone = client, `-logFile C:\Lesson\clone-editor.log`), plus a new build for 5.

1. **Prefab:** open `PonyBase.prefab`. The mane sits on the head and neck from every angle; nothing purple.
2. **Match, host window:** both ponies have a mane, in two different colours from white / black / brown / gold / pink. Note them.
3. **Client window:** the same two colours on the same ponies.
4. **Respawn:** kill a pony (Scene view: drag it into the lava, host window). After the respawn it has the same mane colour, in both windows.
5. **Second match** (Result → Main menu → Lobby → new match): the colours are chosen again. Repeat a few times: each time the two differ.
6. **Selection:** clicking a pony still highlights it (white) and the colours come back after.
7. **Logs:** no "Invalid mane material index." in either Editor log or in the build's `Player.log`; no new warning or exception.

## 10. Open questions and assumptions

- **A1, the five colours** are my proposal (white, black, brown, gold, pink): none is red, blue or green. Poll #15 asks whether you want others. Changing a colour later is a material edit; renaming an enum value is safe (only the order matters).
- **A2, `Preserve Hierarchy`.** Unity's documented behaviour for a single-root model is to reuse that node as the root. If the imported hierarchy is already `PonyMane` → `Mane` without the flag, keep the flag on anyway: it makes the result independent of the file.
- **A3, client timing.** A client may first apply the default `NetworkManeColor` (0 = white) for a frame before the server's value arrives. That's acceptable; a flash of white for one frame is not a defect.
- **A4, the mane is static.** If the pony gets a rig later, the mane must be re-parented to the head bone or skinned (README "Static mesh").
