# Example Scene

This scene demonstrates how to keep a Unity physics simulation stable at large distances from the origin by rebasing the world around the main body.

The problem is not rendering alone. Once positions get large enough, floating-point precision starts to show up as jitter, unstable contacts, and visible error in the simulation. A simple world snap can hide the precision issue visually, but it can also create physics artifacts because Unity interprets the discontinuity as a sudden change in state.

## What This Example Shows

The example includes three rebasing modes:

- `None` - no rebasing is applied.
- `FloatingOrigin` - the world is shifted back toward the origin when the main body moves too far away.
- `PhysicsFloatingOrigin` - the world is pulled back continuously using a force-based rebasing controller.

The third mode is the important one. Instead of teleporting the world, it applies an acceleration that recenters the simulation over time. That keeps the physics frame continuous and avoids the contact artifacts caused by large discrete offsets.

## How It Works

The scene uses a single manager to track the origin offset and the reconstructed physical state of the main body.

### `ExamplePhysicsManager`

This script:

- Finds the rigidbodies in the scene and selects a main body.
- Applies standard floating-origin rebasing when the body exceeds the configured distance or velocity thresholds.
- Applies the physics rebasing controller in `PhysicsFloatingOrigin` mode.
- Reconstructs the main body's physical position, velocity, and acceleration for display.

### `ExamplePhysicsHUD`

This script provides the on-screen controls and readout:

- Switch between rebasing modes.
- Display the current physical position, velocity, and acceleration of the main body.

### `ExampleBody`

This is the simple test body used by the demo:

- Applies an initial velocity at startup.
- Applies a constant acceleration every fixed update.

## Scene Setup

Open `Assets/Scenes/Example/ExampleFloatingOrigin.unity` and press Play.

The HUD lets you switch modes at runtime so you can compare:

- the unmodified simulation,
- the snap-based floating origin approach,
- and the continuous physics-based rebasing approach.

## Notes

- The floating-origin thresholds are intentionally conservative so the behavior is easy to observe in the demo.
- `PhysicsFloatingOrigin` is designed to preserve contact stability better than a hard rebase.
- The displayed physical values are reconstructed from the simulation frame, not read directly from the shifted world frame.

## Why This Matters

If you want an infinite or very large world in Unity, a floating origin is usually necessary. The key detail is that physics cannot be treated like rendering. If the origin is moved abruptly, the solver can interpret the change as a large impulse and generate incorrect collision response.

This example shows the difference between:

- moving the world instantly, and
- moving it continuously through physics.

That distinction is what keeps the simulation numerically stable.
