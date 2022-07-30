# BH3Studio
Check out the [original AssetStudio project](https://github.com/Perfare/AssetStudio) for more information.

This is the release of BH3Studio, Modded AssetStudio that should work with Honkai Impact 3.
_____________________________________________________________________________________________________________________________

Some features are:
```
- Togglable debug console.
- Build Asset List of assets inside game files (use "Option -> Export Options -> AM Format" to change between XML and JSON).
- CLI version (beta).
- Option "Option -> Export Options -> Ignore Controller Animations" to export model/aniamators without including all animations (slow).
```
_____________________________________________________________________________________________________________________________
How to use:

```
1. Build WMV Map (Misc. -> Build WMVMap).
2. Load WMV files.
```

CLI Version:
```
AssetStudioCLI input_path output_path [formats...]
```

NOTE: in case of any `MeshRenderer/SkinnedMeshRenderer` errors, make sure to enable `Disable Renderer` option in `Export Options` before loading assets.

Looking forward for feedback for issues/bugs to fix and update.
_____________________________________________________________________________________________________________________________
Special Thank to:
- Perfare: Original author.
- Ds5678: [AssetRipper](https://github.com/AssetRipper/AssetRipper)[[discord](https://discord.gg/XqXa53W2Yh) at `#genshin` channel] for information about Asset Formats & Parsing.
