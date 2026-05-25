# Splatoon Ink Prototype Plan

## Project Goal
Build a Splatoon-style ink prototype in Unity with a clear separation between:

- Gameplay Ink Layer: stores authoritative ink ownership and drives movement, swim, refill, wall interaction, and turf scoring.
- Visual Ink Layer: adds splash, edge noise, highlights, drips, particles, and drag effects without making every visual detail part of gameplay simulation.

The first milestone is not full Splatoon parity. The goal is a playable, extensible, and performant prototype.

## High-Level Targets

### 1. Stable gameplay ink writing
The player can shoot at floors and walls, and the game writes ink state onto valid surfaces.

The system must support:
- Team ownership: friendly, enemy, uninked, non-inkable
- Ink coverage queries at runtime
- Surface state checks for movement and scoring

### 2. Basic visual feedback
Ink must feel alive instead of appearing as a static circle.

The visual layer should support:
- Primary impact stamp
- Secondary splash points
- Edge breakup and noise
- Basic liquid highlight
- Hit particles
- Wall drip illusion

### 3. Gameplay integration
Ink must affect the player loop.

The system should support:
- Faster movement and refill on friendly ink
- Slowdown or resistance on enemy ink
- Swim state in friendly ink
- Wall ink state for future climb or wall swim behavior
- Turf ownership tracking for scoring

### 4. Weapon extensibility
The system must support multiple weapon types later.

That means one attack should be able to produce separate results for:
- Damage
- Gameplay ink write
- Visual splash generation

## Core Implementation Method

### Gameplay Ink Layer
Use Render Texture or mask texture driven surfaces as the authoritative ink state.

Why:
- Easy to write to at impact points
- Easy to sample from player state code
- Easy to blend in shaders for visible feedback
- Easy to expand into coverage and scoring

This layer is the real data source for:
- Swim checks
- Move speed modifiers
- Ink refill conditions
- Turf control state

### Visual Ink Layer
Build visuals on top of gameplay data using shader effects, particles, and optional decals.

Visual features may include:
- Secondary splashes
- Edge noise
- High-frequency normal detail
- Drips on walls
- Directional smear or drag

Rule:
Not every visible splash must be written into gameplay state.

## Required Systems

### 1. Weapon System
Responsible for:
- Reading weapon configuration
- Firing rate control
- Ink consumption
- Launching hit checks
- Generating primary and secondary splash points

Recommended data source:
- ScriptableObject weapon definitions

Suggested weapon parameters:
- WeaponType
- FireRate
- InkCost
- Damage
- Range
- Spread
- InkRadius
- SplashCount
- SplashRandomness

### 2. Hit Resolve System
Responsible for splitting one attack into multiple outcomes.

A single shot may:
- Damage an enemy
- Ink a floor
- Ink a wall
- Trigger visual-only splashes
- Trigger particles or drips

Recommended output categories:
- DamageResult
- InkSurfaceResult
- VisualSplashResult

### 3. Ink Gameplay Surface System
This is the core system.

Responsible for:
- Owning and updating gameplay ink textures
- Accepting stamp writes
- Determining whether a surface is inkable
- Supporting floor and wall states
- Exposing ink query APIs for player and scoring systems

### 4. Ink Render System
Responsible for displaying gameplay ink state in materials.

Should support:
- Surface color blending
- Ink edge transitions
- Basic liquid highlight response
- Shared presentation across floor and wall surfaces

### 5. Ink Visual FX System
Responsible for visual enhancement only.

Should handle:
- Splash particles
- Drip effects
- Impact feedback
- Directional smear visuals
- Optional decals

### 6. Player Ink State System
Responsible for translating surface ink state into player behavior.

Should support:
- Friendly ink movement bonus
- Enemy ink slowdown or friction change
- Swim state checks
- Refill conditions
- Future wall swim or cling checks

### 7. Ink Resource System
Responsible for the player's ink tank and recovery.

Should support:
- Shot cost
- Passive refill rules
- Swim refill bonus
- Empty tank restrictions

### 8. Turf Scoring System
Responsible for map control measurement.

Should support:
- Team coverage tracking
- Debug visualization of coverage
- Future match scoring integration

## Recommended Unity Packages

### Required
- Universal RP
- Input System
- Cinemachine
- Shader Graph

### Recommended Later
- Visual Effect Graph
- Netcode solution only when multiplayer starts

## Recommended Folder Structure

```text
Assets/
  Art/
  Materials/
  Prefabs/
  Scenes/
  Shaders/
  ScriptableObjects/
  Scripts/
    Core/
    Player/
    Weapons/
    Ink/
      Gameplay/
      Rendering/
      VisualFX/
      Surfaces/
    UI/
  Textures/
```

## Implementation Steps

### Phase 1 - Minimum gameplay loop
Goal: prove that ink can drive gameplay.

Tasks:
1. Set up URP scene with test floor and wall surfaces.
2. Create inkable surface components.
3. Implement a simple raycast weapon.
4. Write gameplay ink stamps onto valid surfaces.
5. Display ink through a material or shader.
6. Sample ink under the player.
7. Apply move speed changes based on team ink ownership.

Expected result:
- Floors and walls can be inked.
- Ink is visible.
- Friendly and enemy ink affect movement.

### Phase 2 - Weapon parameterization and splash behavior
Goal: make attacks feel closer to Splatoon weapons.

Tasks:
1. Move weapon values into ScriptableObjects.
2. Add primary impact points.
3. Add secondary splash points.
4. Separate gameplay splashes from visual-only splashes.
5. Add basic hit particles.

Expected result:
- Weapons can tune spread, radius, and splash count.
- Ink patterns no longer feel like a single perfect stamp.

### Phase 3 - Swim, refill, and wall gameplay
Goal: establish the Splatoon-style player loop.

Tasks:
1. Add ink tank consumption and refill.
2. Add swim state transitions.
3. Refill faster on friendly ink.
4. Slow the player on enemy ink.
5. Add wall ink checks for future traversal.

Expected result:
- Shoot -> ink ground -> swim -> refill -> re-engage loop works.

### Phase 4 - Visual polish
Goal: make the system feel more liquid and expressive.

Tasks:
1. Add edge breakup noise.
2. Add highlight response.
3. Add normal variation.
4. Add wall drip illusion.
5. Add drag or smear visuals.

Expected result:
- Ink feels more organic while staying gameplay-driven.

### Phase 5 - Optimization and scale-up
Goal: prepare for larger maps and future multiplayer.

Tasks:
1. Chunk or segment ink surfaces.
2. Reduce unnecessary texture updates.
3. Tune render texture resolution.
4. Add debugging and profiling tools.
5. Evaluate data flow for future network sync.

Expected result:
- The prototype remains stable as map size and ink volume grow.

## Immediate Next Build Target
The first coding pass should focus on:
- Inkable surface definition
- Weapon config data
- Basic hit resolve
- Gameplay ink writing
- Ink visualization
- Player surface sampling

Once these are working, the project will have a solid technical base for more advanced weapons and polish.
