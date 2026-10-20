# TASK-N24: Convert the three Shift-JIS scripts to UTF-8

- **Source issue:** [Issue Report #7 → N24](../Reports/2026-10-20_IssueReport-7.md#-info) (Info)
- **Severity:** ⚪ Info (Confirmed)
- **Baseline:** `b7e7b91`
- **Order:** 1 of 20. Do it first: TASK-L6 and TASK-L10 edit `CardWorldViewManager.cs` afterwards.
- **Owner decision (2026-10-20):** convert the files.
- **Implementer:** Codex

## 1. Objective

The three scripts that aren't UTF-8 become UTF-8. Their Russian comments and the two Russian `[Tooltip]` texts then display correctly in every editor and in the Unity Inspector. Nothing else changes.

## 2. Current behavior

- These files are saved in **Shift-JIS (cp932)**, the Japanese Windows code page. Their Russian text was written in that code page.
  - `Assets/Scripts/InGame/NetworkCard.cs`: 3 comment lines (`:207-209`)
  - `Assets/Scripts/InGame/PlayerCardfWorldArea.cs`: 12 lines, including two `[Tooltip("…")]` strings (`:9`, `:18`)
  - `Assets/Scripts/Manager/CardWorldViewManager.cs`: 1 comment line (`:747`)
- Verified: each file decodes as cp932 without errors and round-trips byte for byte. Decoded, `PlayerCardfWorldArea.cs:9` reads `[Tooltip("Значение должно соответствовать PlayerSession.TeamId.Value")]`.
- Every other script is UTF-8. The C# compiler reads files without a BOM as UTF-8, so the two tooltips are garbled in the Inspector today.
- All three files use CRLF line endings (NetworkCard.cs 226/226, PlayerCardfWorldArea.cs 140/140, CardWorldViewManager.cs 811/811). Earlier specs (M14) told Codex to keep the encoding. That rule ends with this task.

## 3. Relevant files and dependencies

| File | Change |
|---|---|
| `Assets/Scripts/InGame/NetworkCard.cs` | Re-encode cp932 → UTF-8 |
| `Assets/Scripts/InGame/PlayerCardfWorldArea.cs` | Re-encode cp932 → UTF-8 |
| `Assets/Scripts/Manager/CardWorldViewManager.cs` | Re-encode cp932 → UTF-8 |

No `.meta` file, scene or prefab changes. The script GUIDs stay the same.

## 4. Required changes

1. Decode each file as **cp932**, and write it back as **UTF-8 without BOM**. None of the 58 tracked scripts under `Assets/Scripts` has a BOM.
2. Keep CRLF line endings and the final newline exactly as they are.
3. Don't change any character outside the encoding step. No reformatting, no rename. Keep the file name `PlayerCardfWorldArea.cs` (the naming list is report-only).

## 5. Explicit non-goals

- No code changes. No translation of the Russian comments.
- No other files, even if they contain non-ASCII text.
- No `.editorconfig` or `.gitattributes` changes.

## 6. Constraints and compatibility requirements

- Use a tool that converts explicitly from cp932, for example `iconv -f CP932 -t UTF-8` or a short Python script. **Don't** let an editor guess the source encoding: guessing as cp1251 or cp1252 corrupts the text.
- Script GUIDs and class names stay the same, so no Inspector reference is affected.

## 7. Acceptance criteria

1. All three files are valid UTF-8 (`python -c "open(p, encoding='utf-8').read()"` succeeds) and have no BOM.
2. `git diff --stat` shows changes only on the non-ASCII lines (3, 12 and 1 lines). `git diff -w --word-diff` shows only the Cyrillic text changing bytes.
3. Decoding the old blob as cp932 and the new file as UTF-8 gives identical text (a script check; put its output in the report).
4. The project compiles with no new warnings or errors. CS0414 (N4) stays until the hygiene task.

## 8. Regression tests

- Roslyn compile of the runtime sources with the project's response file (as in earlier tasks). Exit 0, same warnings as before.

## 9. Manual Unity Editor checks

1. Open the scene with a `PlayerCardWorldArea` (`GameScene` or `PlayerWorldCardAreaPrefab`). Hover the two fields with tooltips: the text is readable Russian.
2. Let the Editor recompile. The Console shows no new warnings.

## 10. Open questions and assumptions

- None. The encoding (cp932) and the BOM convention (none) were checked on `b7e7b91`.
