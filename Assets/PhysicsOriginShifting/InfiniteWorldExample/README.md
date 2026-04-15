# Infinite World Example

This folder contains a working example of the floating-origin setup used by the project.

## What it demonstrates

- A `PhysicsManager` that keeps the main body near the origin while simulating large-scale motion.
- A `Body` component that mirrors each object into a scaled representation for distant rendering.
- Player and AI control scripts that drive vessels through the same physics model.
- Projectile, thruster, and camera helpers used by the example scene.

## Main runtime pieces

- `Scripts/Physics/PhysicsManager.cs`: global simulation and time-scale management.
- `Scripts/Physics/Body.cs`: state container for physical objects and their scaled copies.
- `Scripts/Vessel.cs`: player input and vessel control glue.
- `Scripts/Mover.cs`: thrust allocation, steering, and missile interception logic.
- `Scripts/Pointers.cs`: forward/prograde/retrograde indicators.

## Notes

- The example uses a local-scene transform and a scaled proxy transform together.
- Most objects are expected to be driven through `Body`, not moved directly.
- `Scaled` and `ScaledCamera` classes keep distant content and camera framing consistent with the floating-origin system.
