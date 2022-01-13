# AssetStudio
Check out the [original AssetStudio project](https://github.com/Perfare/AssetStudio) for more information.

There's already another AssetStudio fork that can load Genshin Impact serialized files, but I didn't make it and therefore didn't want to distribute it without permission.

This one is a fork that I've written myself from scratch. Unlike the other fork(s), this one can load .blk files directly and properly load Texture2Ds.

See [genshinblkstuff](https://github.com/khang06/genshinblkstuff) for more information about .blk decryption and extraction.

_____________________________________________________________________________________________________________________________
This is the release of AssetStudio-CAB, Modded AssetStudio that should work with Genshin Impact/YuanShen.

How to use:
1- Extract blks to a specific location (File -> Extract folder).                                                             
2- Build CAB Map (Misc. -> Build CAB Map).
3- Load AssetIndex file (Misc. -> Select AI JSON).
First design used to support .blks dependencies directly, but now it's CAB- files for more reliable results with dependencies

Looking forward for feedback for issues/bugs to fix and update.
_____________________________________________________________________________________________________________________________

Some features are:
- Export options added some stuff (exportable AssetBundle/IndexObject, on-the-fly key change)
- Togglable debug console.
- Container/filename recovery for MiHoYoBinData and other Assets
- Ability to export MiHoYoBinData to .json directly if it's json format (like some stored configs in blks sometimes, need to set key to 0 to make sure it's original data).
- Fixes to some classes to be able to parse it (AnimationClip/Mesh/Renderer/etc)

_____________________________________________________________________________________________________________________________
