# SplatoonInkPrototype

A Unity prototype exploring Splatoon-style ink shooting, surface painting, swimming, and wall traversal.

This is an early gameplay and rendering prototype. The goal is to validate the core loop first: fire ink projectiles, paint walkable surfaces, sample friendly ink for movement, and iterate toward a thicker, glossy, liquid-like ink presentation.

## Unity Version

- Unity `2022.3.35f1c1`
- Target scene: `Assets/Scenes/SampleScene.unity`

## Current Features

- Procedural prototype arena with configurable inkable floor and wall.
- Projectile-based ink weapon with gravity, collision detection, and inherited firing motion.
- Camera-forward firing direction with a limited projectile range instead of aim-point snapping.
- Runtime ink stamping on floor and wall surfaces.
- World-space paint-map sizing so large floors and narrow walls can use different texture dimensions.
- RenderTexture-based ink presentation using blur and metaball compositing for smoother connected splats.
- Ink height data used for thicker visual surfaces and wet highlights.
- Friendly ink sampling for ground swim and wall swim states.
- Shift-to-swim, space jump, wall jump, and near-top wall mantle behavior.
- Basic third-person camera orbit, obstruction handling, and combat HUD.

## Controls

- `WASD`: move
- `Mouse`: rotate camera
- `Mouse Wheel`: zoom camera
- `Left Mouse`: fire ink
- `Left Shift`: swim on friendly ink
- `Space`: jump, wall jump, or mantle near the top of an inked wall

Ink firing is disabled while swimming.

## Main Systems

- `Assets/Scripts/Core/PrototypeSceneBootstrap.cs`
  - Builds and configures the prototype scene, including floor and wall dimensions, player, camera, HUD, and runtime defaults.

- `Assets/Scripts/Weapons/InkWeaponEmitter.cs`
  - Spawns ballistic ink projectiles, resolves projectile impacts, and forwards hit data to inkable surfaces.

- `Assets/Scripts/Weapons/InkHitResolver.cs`
  - Converts projectile collisions into surface hits with UV, normal, incoming direction, and surface-space direction.

- `Assets/Scripts/Ink/Gameplay/InkGameplaySurface.cs`
  - Owns the runtime paint maps, applies splat stamps, stores gameplay coverage, and exposes ink sampling.

- `Assets/Scripts/Ink/Surfaces/InkSurfaceVisual.cs`
  - Presents the runtime ink map through a RenderTexture blur/metaball pipeline and updates the display material.

- `Assets/Scripts/Player/PlayerInkState.cs`
  - Detects friendly ink under the player or on nearby walls and drives ground/wall swim state.

- `Assets/Scripts/Player/PrototypePlayerController.cs`
  - Handles movement, jumping, swimming, wall swimming, wall jumping, and mantle behavior.

## Tuning In Inspector

- `PrototypeSceneBootstrap`
  - Floor size, wall size, wall placement, player spawn, and default scene setup.

- `InkGameplaySurface`
  - Paint-map density, max texture size, ink radius, splat clusters, directional trails, splatter, drips, and height behavior.

- `InkSurfaceVisual`
  - RenderTexture presentation, blur radius, metaball threshold, feathering, smoothness, height strength, and highlight strength.

- `InkWeaponEmitter`
  - Projectile speed, gravity, lifetime, fire cadence, radius, range, and visual feedback.

- `PrototypePlayerController`
  - Ground movement, jump tuning, swim speed, wall stick distance, wall climb speed, wall jump, and mantle behavior.

## Current Ink Rendering Direction

The project is moving toward the Splatoon-style approach:

1. Fire physical ink projectiles.
2. Convert projectile impacts into surface-space stamps.
3. Accumulate coverage and height in per-surface paint maps.
4. Smooth and merge nearby stamps with a RenderTexture pipeline.
5. Render the result as glossy, thick liquid with highlights and surface variation.

This is not final yet. The current visual system is meant to be a controllable base for iteration, not the finished look.

## Known Limitations

- Ink visual quality still needs polish toward thicker, smoother, more reflective liquid.
- Very large surfaces are bounded by max texture size, so future chunked or tiled paint maps will be needed.
- Floor and wall painting are currently per-surface, not a full arbitrary-mesh multi-face system.
- Wall back-face painting and separate per-face ink layers are not fully implemented.
- The prototype scene is generated and serialized heavily, so Unity scene diffs can be large.

## Roadmap

- Improve the ink material toward a glossy oil-paint/liquid look with better normals and specular response.
- Add tiled or chunked paint maps for large surfaces.
- Improve wall ink behavior, including front/back face handling and edge cases near ledges.
- Refine swim and wall traversal feel.
- Add enemy ink, damage/slowdown rules, refill behavior, and more complete player feedback.
- Replace placeholder player visuals with squid/swim proxies and weapon presentation.
