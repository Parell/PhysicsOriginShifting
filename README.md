# Unity Physics Floating Origin

Unity project demonstrating a floating-origin setup for large-scale physics and rendering.

## What’s Included

- Floating-origin world management
- Physics-driven vessels and projectiles
- Scaled proxy rendering for distant objects
- A working example scene under `Assets/UnityPhysicsFloatingOrigin/InfiniteWorldExample`

## Example Docs

See `Assets/UnityPhysicsFloatingOrigin/InfiniteWorldExample/README.md` for a short walkthrough of the example runtime pieces.

Make 5 store images:

Hero image
“Stable physics in massive Unity worlds”
Before/after image
Left: jitter/glitching far from origin
Right: stable physics after shifting
Setup image
Show the component in Inspector with labels.
Architecture image
Target → Shift Manager → Registered Objects → Physics World
Use-case image
Spacecraft, planet, projectile, vehicle, or orbital scene.

For tools, screenshots sell more than abstract code.

5. Add a demo video

A 60–90 second video should show:

Object moving far from origin
Physics starts degrading
Enable your system
Origin shifts
Rigidbody behavior stays stable
Setup takes under 2 minutes

Good content titles:

“Fix Unity physics jitter in large worlds”
“Floating origin system for Rigidbody physics”
“How to build space-scale worlds in Unity”
“Unity large world precision problem solved”
“Origin rebasing vs floating origin in Unity”

Places to post:

YouTube Shorts + full tutorial
Unity Discussions
r/Unity3D, but frame it as a technical breakdown, not store spam
X/Twitter dev clips
Discords for space games, procgen, simulators
Devlog posts showing before/after jitter

URP compatibility/demo
Even if the asset is code-only, the listing saying URP/HDRP “Not compatible” hurts trust. If render pipeline does not matter, clarify it and provide URP/HDRP-compatible demo scenes.
Multiplayer support or guide
Floating origin gets complicated in multiplayer. A guide for Mirror/Netcode/Photon would differentiate you from generic scripts.
