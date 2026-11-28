"""Regenerates every sprite in Assets/Resources/Art plus manifest.json (Pillow + numpy + scipy).
    pip install pillow numpy scipy
    python tools/make_art.py            (then: python tools/make_audio.py for the sounds)
Order matters: later stages composite earlier sprites (title screen reuses the tiles)."""
import art_common
import art_tiles, art_decor, art_ui, art_scene

if __name__ == "__main__":
    art_tiles.build()
    art_decor.build()
    art_ui.build()
    art_scene.build()
    art_common.write_manifest()
    print(f"wrote {len(art_common.MANIFEST)} sprites + manifest.json")
