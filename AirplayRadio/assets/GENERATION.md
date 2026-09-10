# Station icon

Repository storage: the current game asset is `station.svg`; the approved square raster is retained once at `../release-assets/thumbnail.png`. Historical `station.png` references below describe local artwork history. That duplicate file is ignored by Git and is not required for building or packaging.

## Current revision — circular game asset

The production game asset is now station.svg: native vector geometry matching the approved broadcast tower and concentric waves, on a circular navy disk with transparent corners. This avoids raster downscaling in the small game UI. station.png retains the approved square source artwork.

An imagegen edit was attempted first, but its exported corners contained an opaque checkerboard (alpha 255), so that output is not used in the game.

Imagegen prompt: Precise production icon edit. Keep the approved mint radio broadcast tower and three concentric arcs and their geometry. Convert the square navy background into a perfect circular navy disk, with real transparent alpha outside the disk (all four corners alpha zero, not painted checkerboard, not white, not black). Reduce the entire inner tower and arcs slightly so they fit fully inside the disk with an even generous inset. Center the whole emblem optically. Flat solid navy #102733 and mint #71dfce only. Make the curves and diagonal antenna strokes clean, smoothly antialiased, and bold enough for 48px and 64px game UI. Perfectly circular outer silhouette, smooth edges, no texture, no gradients, no glow, no shadow, no bevel, no text, no new elements. Output a transparent PNG icon.

## Previous revision — broadcast tower

Built-in imagegen edit of the approved radial icon. Saved as station.png and copied into the package and local installation.

Prompt: Edit this approved Airplay Radio station icon with one focused addition: make it clearly a radio BROADCAST TOWER emitting the existing concentric waves. Preserve the dark navy teal background, mint turquoise color, three concentric open circular arcs, centered composition, generous margins, and minimalist flat Live Radio visual style. Keep the central dot as the antenna tip. Add a simple mint broadcast tower mast descending from this dot through the existing bottom gap: two clean slightly splayed legs and two minimal crossbars, terminating neatly at the baseline of the outer arc. Make the mast clearly readable at small icon sizes with bold geometric strokes, balanced against the arcs, without touching the arc ends. Only two flat colors, no lettering, no musical note, no gradient, no glow, no shadow, no bevel, no 3D, no extra objects. Finished square game radio icon.

## Previous revision — Live Radio visual style

Built-in imagegen, using release-assets/thumbnail.png as a style reference. Replaced station.png and the installed icon.

Prompt: Create a new square Airplay Radio game station icon. The attached Live Radio image is a STYLE REFERENCE ONLY: match its absolutely flat dark blue-green background (#102733), mint turquoise (#71dfce) graphics, understated Swiss graphic design, crisp geometry and generous negative space. Replace the equalizer and ALL text with a single centered radial wireless diffusion emblem: a small solid mint central dot surrounded by three concentric circular arcs, each with a small symmetric opening at the bottom, suggesting AirDrop ripples radiating outward. Bold consistent stroke widths, balanced spacing, legible as a tiny radio station icon. Emblem fills approximately 70 percent of canvas. Background is uniform opaque dark blue-green edge to edge. Strictly two flat colors. NO lettering, no musical note, no gradients, no glow, no shadows, no bevel, no 3D, no glossy badge, no checkerboard. This is a finished standalone square icon.

## Earlier revisions

Generated with the built-in imagegen tool on 2026-09-10. Output: station.png.

Prompt: Use case: logo-brand. Create one square radio station icon for a game mod named Airplay Radio that receives phone music wirelessly. A bold simple white musical note integrated with three wireless broadcast arcs, centered on a rich teal and midnight blue circular badge, subtle luminous cyan edge. Clean polished flat graphic, highly legible at 48px, generous padding, no lettering, no watermark, no Apple logo. Transparent background outside the circle. This is a standalone production icon, not a mockup.

Final edit prompt (built-in imagegen): Edit this radio station icon: keep the white musical note, three wireless arcs, and blue teal circular badge exactly as they are. Replace ONLY the gray checkerboard outside the circle with solid deep midnight navy #071c35. No checkerboard anywhere. Opaque square production icon. No text.
