# Office Furniture: overhead redraw

42 individual sprites were redrawn with the built-in imagegen tool, using
`Assets/Sprites/Shelter/CoffeeMachine.png` as the style reference. The original
colors and object identities are retained, with stronger overhead views,
navy contours, cool shadows, highlights, material detail and wear. Wall-mounted
items retain readable faces. Dedicated correction prompts preserve the side
sofa and chair orientations, remove unwanted chairs from tables, and ensure
the small folders and toilet-paper holder retain their original identities.

## Files

- Runtime assets: `Assets/Sprites/Office Furniture/`.
- Originals and original Unity metadata: `Docs/ArtBackups/OfficeFurniture-2026-10-29/`.
- `generation.json`: exact prompts and selected generated sources.
- `OfficeFurniture-overview.png`: labeled overview of the imported sprites.
- `Import-Art.ps1`: transparent crop, nearest-neighbor resampling, canvas fitting,
  metadata preservation and atlas reconstruction from exact original matches.
- `Make-Preview.ps1`: builds the overview from the runtime images.

Individual canvases and pixels-per-unit are both multiplied by 16, preserving
their original world dimensions, pivots, GUIDs and sprite identifiers. Filtering
remains Point and mipmaps remain disabled. Generated silhouettes fit within
the original occupied rectangle. The atlas uses the original layout at 16x
resolution, with a 4096 import limit to avoid automatic downsampling.

The existing shelter coffee machine is only a reference and is not replaced.
