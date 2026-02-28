# Tech Debt

Record issues discovered during scoped work that are **not** fixed due to scope containment.

Format:

- Date:
- Area:
- Symptom:
- Impact:
- Proposed fix:
- Notes:

- Date: 2026-02-27
- Area: LargeTree Blender generation (`Tools/BlenderMCP/blender_bridge.py`)
- Symptom: Branch-trunk blending now uses segmented cone junction nubs; it is visually improved but not a true continuous manifold at all joins.
- Impact: Close-up inspection can still reveal hard transitions at some branch bases compared to fully sculpted or boolean-merged junctions.
- Proposed fix: Replace cone-chain branch construction with a curve/spline-driven branch mesh and boolean/remesh trunk union pass for seamless joins while preserving deterministic output.
- Notes: Chose segmented-cone implementation first to keep deterministic generation and iteration speed high while addressing immediate silhouette issues.

- Date: 2026-02-27
- Area: Runtime biome tree conversion (`Assets/Scripts/Building/WorldPersistence.cs`)
- Symptom: Startup terrain-tree conversion is currently hosted inside `WorldPersistence` to minimize rollout scope.
- Impact: World save/load and biome spawn concerns are now colocated, which increases class responsibility and can make future biome logic harder to isolate.
- Proposed fix: Extract startup terrain tree conversion into a dedicated server-owned `TerrainTreeSpawner` runtime component with explicit dependency injection from prefab generation.
- Notes: Chose in-place `WorldPersistence` integration as the simpler one-pass migration path for immediate gameplay enablement and to avoid adding new bootstrap wiring during this pass.
