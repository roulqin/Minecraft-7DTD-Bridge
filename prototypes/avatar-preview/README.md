# Phase 3.8.5.1 isolated Avatar feasibility preview

Experimental Mod, not Avatar Runtime. It does not reference the project's Entity, Equipment, Bridge or Authority implementations.

Run `./tests/launch-phase3_8_5_1.ps1` from the project root, enter the copied Navezgane test save, and normally exit the game. The launcher temporarily disables the installation-wide Bridge ModInfo and restores it in `finally`. Do not interrupt the launcher while the game runs. If interrupted, restore `Mods/MC7DTD.Bridge/ModInfo.xml.avatar-preview-disabled` to `ModInfo.xml` before normal project tests.

Resources: `assets/avatar/prototype/alex_slim_mesh.json` and `player_default.png`. Six cuboids, 144 vertices, 72 triangles, 19 Humanoid skeleton transforms, rigid per-part weights. Arms use the three-pixel Alex layout. Only the base skin layer is represented; outer clothing/hair layers are not implemented.

The actual game process builds Mesh, Humanoid Avatar, SkinnedMeshRenderer, PNG texture and material. SDCSUtils.TPAnimController clips are inspected for Humanoid retarget candidates. In this installed game they are non-Humanoid, so the working preview uses prototype procedural Idle/Walk via IAnimationJob / AnimationScriptPlayable / AnimationPlayableOutput into an Animator. Bone changes are sampled to verify actual animation. This is not native clip retargeting, a production AnimatorController, or animation synchronization. Camera.Render captures and results are saved under `work/phase3851-test/evidence`. A standalone visual root is placed beside the local player once the world is available.

`unity/` is an optional Editor bundle experiment. The installed Editor refused batch mode due to a missing valid license; this route was not executed or used as evidence. Runtime-generated mesh requires no Editor bundle.

No Entity registration, existing proxy replacement, Equipment anchors, collision, movement following, skin server access, production fallback or resource cache is implemented.
