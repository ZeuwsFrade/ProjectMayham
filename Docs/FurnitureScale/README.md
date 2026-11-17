# Furniture sizing — 2026-10-30

Source: the user's live hand-tuned Shelter scene. The existing Shelter transforms
and its unsaved state were preserved; the migration recorded zero scale changes
there.

| Furniture | Multiplier |
| --- | --- |
| Small table | 2 |
| Chairs | 1.5 |
| Bookshelves and water dispenser | 1.4 |
| Filing cabinets | 1.3 |
| Desks and sofas | 1.2 |

Related appliances use the same proportions: coffee machine 1.4; vending
machine and bin 1.3; printers, round table and bathroom furniture 1.2.
Plants, wall decorations and loose paper/book pickups retain their proportions.
The policy lives in `Assets/Editor/Level/FurnitureScalePolicy.cs`.

29 source prefabs were updated, including their scaled colliders. 17 direct
objects across CombatTest and ShelterTest were also updated. Existing room
prefabs inherit the source scales automatically; validation checked 1,837
inherited props. Special barricade arrangements retain their authored sizing.

OfficeMazeBuilder uses the same policy for newly spawned furniture and reserves
the scaled collision footprint, including the space needed for chairs.
The updated generator compiled successfully. Gameplay/pathfinding through all
existing room layouts was not run; their authored positions were retained.

`applied.txt` records changes. Originals are backed up under
`Docs/ArtBackups/FurnitureScale-2026-10-30/`.
`Apply.cs` is the one-time migration, guarded by `applied.txt` against reapplying.
