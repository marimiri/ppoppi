# Protocol Aer — Radar Prototype

Unity 6000.3.24f1 / Windows / single-player.

## Flow

Animated title → terminal → optional three-step tutorial → radar map with eight buildings → names 1–4 → sunlight restoration → names 5–8 → rainfall restoration → end.

Targets are generated from example solutions. Names require exact height matches, and introduced operators must change the output. Weather requires 85% volume IoU without a special effect condition. O/L/I add +1. S levels its four cells to their original minimum; T levels its four cells to their original maximum. All operators sample the same ordinary-only height map, including empty cells at zero. S/T intersections preserve the original height. Overlapping S candidates take the lower candidate, T candidates take the higher candidate. There is no chaining or placement-order dependency. T-created layers, including in empty cells, use T's yellow color; lowering removes upper layers. UI notices explain the new effects and shared-cell rule.

Unlocks are O initially, L in name 3, I in name 5, S in name 7 and T in name 8. Supply is unlimited; no save state exists. Names 1–5, tutorials and the first weather are unchanged. Waterworks now uses three O, one L and one vertical I: one more ordinary piece than the archive, preserving an I rotation while simplifying the old multiple L/I orientation combinations. Difficulty comparisons are design targets, not a substitute for playtesting.

Windway and control room each use three ordinary pieces and one operator. The final rainfall uses four ordinary pieces and S/T, all in default orientation. Both effects matter in its reference solution. Two common cells preserve original heights while exclusive S cells lower and exclusive T cells raise. The old whole-mino inversion, doubling and -2 rules have been removed.
## Controls

- Any key/click on title: connect.
- Click a module in the right tray, then click the central 5×5 board to place it. Different colors and shapes distinguish O/L/I/S/T.
- While holding a module over a valid board position, the 3D view shows the predicted combined result before clicking. Moving, rotating, mirroring and S/T effects update this temporary preview without changing the actual plan or progression. Leaving the board or hovering an invalid position removes the temporary result.
- Clicking an invalid board position or blank space outside the board returns the held module to the tray. Transform buttons, other interface controls and 3D camera interaction remain usable while holding it.
- Click an occupied cell without a module selected to pick up its most recently placed module.
- Right click / Escape: return held module.
- Q / E / R: rotate. F: mirror. These preserve the O module's square shape.
- Ctrl+Z or Undo: remove the last placement.
- Reset: clear the current puzzle.
- Left preview: drag or WASD to orbit through a full 360 degrees on both axes, including over-pole and under-board views; mouse wheel to zoom. Angles wrap without clamping, and Reset View restores the default orientation.
- Target / Current / Overlay: change the preview comparison.
- Hint: inspect the committed plan and show the next missing example placement. The correct type and orientation are selected automatically; click the gold dot to use the exact anchor. Duplicate placements are counted individually and hints recompute after moves/undo. Red cells and a red dot identify a misplaced module to move/return first; its marked cell is always actually clickable. Completed boards receive execution advice, including valid alternative solutions. Hints never place/remove a module automatically.
- Execute: animate height growth and compare. Failed attempts remain editable.

No save/load or PlayerPrefs access. Progress exists only in memory and disappears when the app closes. Starting a new game also resets progress and discovery notices. Timers and attempt limits are absent.

## Visuals

Retro pixel interface inspired by the user's blue-toned pixel city reference: deep navy backgrounds, blue panels, pale blue text, stepped borders, hard shadows, pixel-rasterized radar lines and a point-filtered 258×207 3D preview. Korean text uses NeoDunggeunmo at native bitmap multiples. Title uses the user's sky photograph with individually wobbling Rock Salt glyphs, a fixed chalk grain and a blinking start triangle. No character or decorative story copy. No numbers on the placement board. O / L / I / S / T colors are exactly #FFDBEE / #94E8E9 / #D4B4EF / #FFB2BC / #FEECB1. The tray, cursor, board and source-colored 3D layers share these colors; S affects height and remains visible on the 2D board. Weather restoration uses the same abstract code puzzle, not a physical atmospheric simulation.

Font sources and license copies are bundled under Assets/Resources/Fonts: NeoDunggeunmo (https://github.com/neodgm/neodgm, SIL OFL) and Rock Salt (https://github.com/google/fonts/tree/main/apache/rocksalt, Apache 2.0). The title grain can be regenerated with Tools/MakeChalkTitle.ps1.

All unconfirmed 3D placement results use an alpha-blended preview (70% alpha, no depth writes, back-face culling). Target shapes show bright white exterior surface creases, including target-only mode, distinct from the blue floor grid. Shared edges between coplanar faces are omitted; convex corners, concave corners and front-facing step edges remain. A separate camera uses depth-only contiguous voxels on layer 29 to hide rear edges without occluding the player's translucent preview. It follows the 3D camera while orbiting/zooming. Execution and failed attempts stay transparent. Only a successful execution switches the result to opaque colored cubes; the completed result is visible for 0.65 seconds before the success dialog. Editing/resetting/re-entering clears the restored visual state. Board colors and tray icons remain solid for readability. The boot message reads “기상 조작 시스템에 연결하는 중…”.

Restoration percentage is always visible for names and weather, based on committed geometry. Name stages require exact 100% geometry plus their introduced-mino condition; weather requires 85% similarity. The score uses volume intersection/union, so extra voxels reduce similarity.

The preview uses a 30-degree perspective camera, so near cells project larger than far cells. Zoom changes camera distance; the default focus accounts for target height. A fixed light-blue line (#8FD9FF) identifies the 3D edge corresponding to the bottom of the 2D board (row 4, world z=-0.575 outside the cells). The line remains readable from all orbit directions, and the 2D board's bottom border uses the same color. Target-edge depth bias is measured in view-space units, keeping surface lines stable in perspective.

## Unity

Add this folder in Unity Hub and open Assets/Scenes/Main.unity. Runtime bootstraps the interface when Play is pressed. Editor build command: `AerBuilder.BuildWindows`. It also creates the starter scene if needed.

Source model `EveningBoard.cs` reuses the earlier project's placement/rotation/height code. New stage definitions are in `AerStages.cs`; screen flow, interface and runtime are in `ProtocolAer.cs`.

Checks validate all 13 example solutions, unlock timing, compulsory new effects, 100-piece unlimited overlap, exact palette values, source colors, local S minimum, T maximum, original mixed intersections, new voxel geometry and order independence. Capture mode (`-aer-capture <folder>`) verifies repeat selection beyond old limits and the persistent type-only tray, then runs tutorial plus all ten main stages through the production selection, rotation, placement and execution methods, capturing radar progression and new-module notices.

The checks also verify the intended name/weather complexity envelope, transparent preview materials before/during execution and after failure, and opaque materials only after successful restoration. Before/after screenshots are captured for name regions 4 and 8.

Runtime placement checks cover live hover and moving existing pieces, noncommitting S/T effects, rotation/mirroring, invalid clicks both inside and outside the board, and preserving selection while interacting with controls.
