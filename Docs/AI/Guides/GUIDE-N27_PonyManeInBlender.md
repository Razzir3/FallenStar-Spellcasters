# GUIDE-N27: How to have another Claude agent make the pony's mane in Blender 4.4

- **Why:** poll P8 (2026-10-23). N27 stays parked "until the mane model exists". You asked for instructions to delegate the mane to another Claude agent in Blender 4.4.
- **Who it's for:** you, the owner. It's not a Codex task. When the mane exists, a small Unity step follows (§5). I can write it as a Codex task, or you can do it in five minutes.
- **Date:** 2026-10-24. Facts below are read from the project at `ff8a9dd`.

## 1. What Unity needs from the mane

The code is ready and waits only for a mesh:
- **`PonyColorScript`** (`Assets/Scripts/InGame/PonyColorScript.cs`) has a `maneObject` field, empty in `PonyBase.prefab`.
  - At spawn it replaces **material slot 0** of that object's `Renderer` with the team material, as it does for the body.
  - While the field is empty, every spawn logs "Invalid mane material index." That's N27.
- **The colours** are the team materials in `PonyColorScript.m_materials`:
  - index 0: `TestMaterial`, red (team 0, host)
  - index 1: `TestMaterial2`, blue (team 1, client)
  - index 2: `TestMaterial3`, green, unused
- **Today the mane gets the same team colour as the body:** `PlayerController.ApplyMaterial` calls `SetMaterials(team, team)`. A different mane colour (for example a darker shade) needs new materials and a small code change: new poll **Q5**.

**So the mane must be:**
1. **its own mesh object,** separate from the body, with **one material slot.** The colour you give it in Blender doesn't matter: Unity replaces it.
2. **a static mesh.** The pony has no skeleton and no skinning, so there's no rig to bind to.
3. **aligned with the pony's model.** Placed as a child of the pony model at local position, rotation and scale 0 / 0 / 1, it must sit on the head and neck.
4. **low-poly,** like the body. Aim for under about 4,000 triangles.
5. **without a collider.** The pony's `Rigidbody` and colliders stay as they are.

## 2. The source model

| Fact | Value |
|---|---|
| File | `Assets/Models/PonyBase.fbx` (3.2 MB, binary FBX, dated 2026-05-31) |
| Made with | Blender (the FBX `Creator` field) |
| Content | Static mesh, no skeleton or skin. Node names: `surprise pony` (×4), `PonyRef`, `SkeletalMeshComponent0`, `Empty.001`, `Empty.002`, `Vert.002` |
| Unity import | Scale factor 1; `PonyBase.prefab` scales the model to **0.3** |
| `.blend` source | **None in the project.** The agent starts from the FBX. |
| Blender on this PC | **4.4:** `C:\Program Files\Blender Foundation\Blender 4.4\blender.exe` (5.1 is installed too; use 4.4 as you asked) |

**Important: don't change `PonyBase.fbx`.** `PonyBase.prefab` points to objects inside it by internal IDs (the body object, its material). A re-exported FBX can change them, and the prefab would lose its body reference and material. The mane goes into a **separate file**, `PonyMane.fbx`.

## 3. Three ways to delegate, and my recommendation

| Way | How it works | Good | Watch out |
|---|---|---|---|
| **A. Claude Code + Blender's command line** ★ | A new Claude Code session in a separate folder writes a Python (`bpy`) script and runs Blender 4.4 in the background: import the FBX, build the mane, render previews, export `PonyMane.fbx`. You look at the PNG renders and give feedback, and it iterates. | No add-on to install. Everything is a script you can read, and it's repeatable. It never touches the Unity project. | You review through images, not a live viewport. Start the session in its own folder (below), not in the Unity project. |
| **B. Claude chat + Blender's Scripting tab** | Claude (claude.ai) writes the script. You paste it into Blender → *Scripting* → *Run Script*, and send back screenshots. | No setup at all | Slower: you copy every step by hand |
| **C. A "Blender MCP" add-on** | A third-party add-on lets Claude drive a running Blender live | You see the work happen in Blender | **It's community software, not Anthropic's, and it runs any Python inside Blender.** Install it only from a source you trust, after reading what it does. I don't recommend it here: A does the same job without it. |

**Recommendation: A.** Steps:
1. Make an empty folder, e.g. `C:\Lesson\ManeWork\`, and copy `Assets\Models\PonyBase.fbx` into it. Don't open the Unity project folder in this session.
2. Open a terminal in `C:\Lesson\ManeWork\` and start Claude Code there.
3. Paste the brief in §4. Answer its questions; it should show you renders before it exports.
4. When you like the look, check the alignment render (§4, deliverable 3) and continue with §5.

## 4. The brief to paste to the agent

```text
You are making a mane for a stylized low-poly pony model in Blender 4.4, for a Unity game.

Working folder: C:\Lesson\ManeWork\  (work only here)
Blender: "C:\Program Files\Blender Foundation\Blender 4.4\blender.exe"
Run Blender headless with Python scripts, for example:
  & "C:\Program Files\Blender Foundation\Blender 4.4\blender.exe" --background --factory-startup --python make_mane.py
Keep every step as a script in this folder so it can be re-run.

Input: PonyBase.fbx (exported from Blender earlier; static mesh, no armature).
Never modify, overwrite or re-export PonyBase.fbx.

Step 1 – Inspect and report before modelling:
- Import PonyBase.fbx with the default FBX importer settings. List every object (name, type,
  vertex/triangle count, location/rotation/scale, dimensions) and the scene unit settings.
- Tell me which object is the body mesh, where the head and neck are (world coordinates),
  and which axis is "forward" for the pony.
- Render 4 preview PNGs of the plain model (front, left side, 3/4 view, top) to .\renders\.

Step 2 – Model the mane:
- A separate mesh object named "Mane": a stylized mane running from the top of the head along
  the back of the neck, matching the model's low-poly, cartoon style. Optional small forelock.
  No tail unless I ask.
- Under about 4,000 triangles. Clean manifold mesh, normals facing outwards, no loose vertices.
- Exactly ONE material slot named "Mane" (any colour; the game replaces it).
- Apply all transforms on the Mane object. Its origin must be the same point as the pony
  model's origin, so that it lines up when both sit at local position 0 under the same parent.
- It must not intersect the body visibly (small overlap at the base is fine).
- Render the same 4 views with the mane, plus 1 close-up of the head, to .\renders\.
  Show me the renders and wait for my feedback. Iterate until I approve.

Step 3 – Export (after my approval):
- Export ONLY the Mane object to .\out\PonyMane.fbx (Selected Objects, Mesh only, no armature,
  no animation). Use the same axis and unit settings that round-trip PonyBase.fbx correctly:
  verify by importing PonyBase.fbx and PonyMane.fbx into a fresh scene and rendering them
  together (front and side) to .\renders\alignment_*.png.
- Also save .\out\PonyMane_work.blend.
- Write .\out\README.md: triangle count, export settings, which object of PonyBase.fbx the
  mane was aligned to, and anything I need to know for Unity.

Do not install Blender add-ons. Do not download anything. Ask me if anything is unclear.
```

## 5. Then the Unity step (5 minutes, or a small Codex task)

1. Copy `C:\Lesson\ManeWork\out\PonyMane.fbx` to `Assets\Models\`. In its import settings, keep scale factor 1 and set *Materials* to "None" or keep the default. Unity replaces slot 0 anyway.
2. Open `Assets\Prefab\BoardPrefab\PonyBase.prefab`.
3. Drag `PonyMane` into the prefab **as a child of the pony model**, next to the body mesh object, at local position 0, rotation 0, scale 1. It inherits the model's 0.3 scale.
4. Make sure the mane object has **no collider, no Rigidbody and no NetworkObject.** A nested NetworkObject would be RP10's problem.
5. On the root's **Pony Color Script,** set **Mane Object** to the mane object that has the `MeshRenderer`.
6. Save the prefab, start a match: the host's pony has a red mane and the client's a blue one, and "Invalid mane material index." is gone from the Console.
7. Tell me. I add the check to the next checklist and close N27.

**Commit note:** `PonyMane.fbx` and its `.meta` go into the commit together. The working folder `C:\Lesson\ManeWork\` stays outside the project.
