# Minecraft Avatar Resource v1 — Phase 3.8.5.2

Resource production only: no Entity spawn, proxy replacement, network messages, Equipment binding or Avatar Renderer Runtime.

- `avatar_config.json`: default Alex resource; optional skeleton field in legacy Java configuration is normalized to `minecraft_avatar_v1`.
- `resource_manifest.json`: native bundle / Prefab / Controller paths and bundle checksum.
- `skins/player_default.png`: unchanged 64x64 default skin.
- `models/minecraft_avatar_v1`: authored base/outer meshes in JSON and OBJ; source meshes require bone binding during Unity authoring.
- `skeletons/minecraft_avatar_v1.json`: seven public names plus 19 Humanoid helper transforms.
- `animations`: source curves and speed-state contract. These JSON files are authoring sources, not AnimatorController substitutes.
- `prefabs` / `materials`: source descriptions, not loadable Unity Prefabs/materials.
- `production_unity/Assets/Avatar/Generated`: actual Unity Mesh assets, Avatar assets, Materials, `.anim` clips, `.controller` and `.prefab`, with their `.meta` files.
- `bundles/windows/minecraft_avatar_v1`: actual Windows AssetBundle; Editor and current 7DTD resource-load tests passed.

The prefab uses a Generic Animator Controller targeting the internal skeleton. A separately exported valid Humanoid Avatar uses that same skeleton for later Humanoid animation work; the supplied Generic clips are not claimed to be retargetable Humanoid clips.

All body ratios come from Minecraft pixel dimensions: head 8x8x8, torso 8x12x4, slim arms 3x12x4, legs 4x12x4; one pixel = 1/16 local unit. Hat outer layer inflates each face by 0.5 pixels, other outer layers by 0.25 pixels. Outer skin uses alpha cutout; semi-transparent blending is not part of this material version.

Build from project root: `./tests/build-phase3_8_5_2.ps1`. Requires an activated Unity 2022.3 Editor with Windows build support. Optional `-UnityEditor` selects another installed Editor. The build rewrites generated resources; edit authoring sources, not Generated assets.

Verify target-engine load: `./tests/verify-phase3_8_5_2-game.ps1`. This starts a separate resource-only 7DTD process, does not enter a world, and exits automatically. The launcher temporarily disables the installation-wide Bridge ModInfo and restores it in `finally`. Do not interrupt the launcher. After an interruption, restore `ModInfo.xml.avatar-resource-disabled` to `ModInfo.xml` before normal project tests.

Source/evidence checks: `tests/validate-phase3_8_5_2.py` requires Python with Pillow; the existing Codex bundled Python includes it. Config/Renderer regressions: `./tests/run-phase3_8_4_1.ps1`.
