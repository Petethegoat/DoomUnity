# DoomUnity

# what?

Various tools for working with Doom WADs and levels in Unity.

# why?

Because it doesn't already exist. There are various cool applications for such a set of tools but personally I just like the idea of letting potential games of mine use doom levels.

I guess I'm kind of making a new Doom engine with this stuff now.

# how?

Drop the "DoomUnity" folder into your project and try some stuff out!

`WadFile wad = new WadFile(path_to_wad);`
`MapData map = new MapData(wad, "MAP01");`
`Texture2D impSprite = DoomGraphic.BuildPatch("TROOA1", wad);`

To run the engine stuff, you'll need to generate `nasty.wad` and put that and (at least one) IWAD in the root folder of your project (just below Assets).

For Midi, you must have a soundfont file in the root directory, and merge in a wad with MIDI files.
The IWADS use MUS files and those are not supported yet.

To build the engine wad: you need to have python 3, omgifol, and pillow

`pip install omgifol`  
`pip install pillow`

you will want to change the path to the root directory of the project or build.
then just run `python3 build_wad.py` to generate the engine wad.

And various other stuff. It's probably still gonna be a bit messy while I'm developing it!

# when?

Whenever I get to it, and am inspired to do so.
