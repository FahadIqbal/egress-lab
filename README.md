# Egress Lab

I built Egress Lab as a small, inspectable evacuation sandbox. A crowd leaves a single floor while a fire spreads, and a routing policy updates which door each person should use. The point is to show the decision loop in emergency decision support: the recommendation has to change as the hazard moves and as doors get crowded, and it has to be possible to compare that recommendation with a dumb baseline.

I am putting the project forward as verifiable Unity and C# practice for an HDR application at Deakin University in that area, including the line of work supervised by Dr Bahareh Nakisa. The Editor run is the demo. The routing code also compiles and tests as plain C#, so the decision itself can be checked without trusting a screen recording.

This is a toy floor, not a model of a real building and not a claim about a real incident.

## What you see on Play

The bootstrap builds a 40 m by 24 m floor with four doors, bakes a NavMesh from the colliders, ignites a fire against the west door, and spawns 40 people. Amber people follow the nearest door. Cyan people follow the decision-support policy. The overlay shows elapsed time, people out, people caught, people still inside, and the queue at each door.

The west door starts on fire. Shortest-walk routing still sends the west-side rooms there. Decision support keeps them off that door and splits them across the doors that are still clear. Switching mode replans the people who are still moving. Restarting with the same seed repeats the same ignition and the same spawn, which is the fair comparison.

| Control | What it does |
| --- | --- |
| Nearest exit / Decision support | Live replan. Amber versus cyan. |
| People, Seed | Applied on the next restart. |
| Spread | Live. `0` freezes the fire, `1` is the default. |
| 1x 2x 4x, Space | Simulation speed and pause. |
| Restart | New run from the current people, seed, spread, and mode. |
| Copy | Copies a CSV of completed runs to the clipboard. |
| B, D, R, Space, 1, 2, 4 | Same actions from the keyboard. |
| Right-drag, scroll, arrows | Orbit, zoom, pan. |

## Architecture

```text
Assets/Scripts/DecisionCore     plain C#, no UnityEngine
Assets/Scripts/Simulation       bootstrap, NavMesh crowd, hazard sheet, overlay
Assets/Tests/EditMode           the same tests the dotnet project runs
Assets/Scenes/Evacuation.unity  camera, light, one bootstrap object
dotnet/                         class library, tests, headless drill
docs/ML_AGENTS.md               optional trainer, not required to run
```

`EvacuationSession` owns the hazard, the doors, the people, and the clock. Unity writes each person's position from their NavMeshAgent and calls `Tick` with movement integration turned off. A headless drill calls the same `Tick` with integration turned on and walks the floor graph. Both paths use `NearestExitPolicy` or `DecisionSupportPolicy`.

The floor graph is a 0.5 m grid with the walls rasterised into it. Each door has a Dijkstra field, so "nearest" means shortest walk, not straight-line distance. The NavMesh is only the execution layer in the Editor. The two can differ by a corner or a body radius; that is a limitation, not a second policy.

## Decision logic

Every second, and also when a door becomes blocked, the session assigns exits one person at a time. Later people in that pass see the queue that earlier people just filled.

The baseline cost is the walk length. Nothing else. A burning door is still eligible.

Decision support scores every door:

```text
cost = 1.00 * (walk / floor diagonal)
     + 2.40 * (0.35 * mean hazard + 0.65 * max hazard along the walk)
     + 0.75 * (people already sent there / soft capacity)
     + 4.00   if the door's approach cell is at or above 0.55
     + 2.20   if any sample on the walk is at or above 0.65
```

The person keeps their current door unless another door is better by more than 0.18. A door that has started burning does not get that margin. The weights live in `RoutingWeights` and are the ones the tests lock in.

Hazard is a 1 m grid. It starts as an ellipse on the west door and only gets hotter: burning cells push heat into their neighbours, warm cells ramp toward 1, and one seeded ember nudges the front so two seeds are not identical. A person who stands in intensity 0.65 for 0.6 s is caught. Intensity 0.9 catches them immediately. Reaching a blocked door counts as caught rather than as a successful exit.

Above a door's soft capacity, the people assigned there slow down. That is a queue proxy so congestion has a consequence. It is not a social-force model.

## Open and run in Unity

1. Install Unity 2022.3 LTS with the Windows, macOS, or Linux editor. The project file asks for 2022.3.62f1. A later 2022.3 patch is fine; let Hub upgrade the project if it offers.
2. Add the project folder in Unity Hub and open it. The first import downloads `com.unity.ugui` and the test framework.
3. Open `Assets/Scenes/Evacuation.unity`. If Hub leaves you in an empty untitled scene, use **Egress Lab → Open Sample Scene**.
4. Press Play. The floor, doors, fire, crowd, and overlay are created by `EvacuationBootstrap`.
5. Let a nearest-exit run finish, switch to Decision support, press Restart without changing the seed, and let that run finish. The metrics card keeps both summaries.

An empty scene also works. Create an empty GameObject, add `EvacuationBootstrap` (or use **Egress Lab → Create Bootstrap In Open Scene**), and press Play. There is no prefab to wire up. `Assets/Prefabs` is reserved for later art.

The render pipeline is the built-in one. I did not add URP, and I did not add a NavMesh asset, because the bake happens at play time from the colliders.

## Record a demo

Use a 16:9 Game view, 1280×720 or 1920×1080. Record with Unity Recorder or with OBS on the Game view, not the Scene view. The shot list and the lines I would say are in [docs/DEMO_VIDEO.md](docs/DEMO_VIDEO.md). Two minutes is enough: one baseline run, one decision-support run on the same seed, and the two result lines on screen together.

## Tests

The decision project targets `netstandard2.1` and C# 9, which is what Unity 2022.3 compiles. I verified it here with the .NET 8 SDK:

```bash
dotnet test dotnet/tests/EgressLab.DecisionCore.Tests.csproj
```

In the Editor the same files run from **Window → General → Test Runner → EditMode**. They cover spread (same seed, monotone, the west door starts blocked), connectivity of the floor, nearest-exit versus hazard avoidance, congestion, hysteresis, the observation vector, and a full drill where decision support loses fewer people than nearest-exit on seeds 7, 42, and 99.

The headless drill prints the same comparison. People walk the grid, so these seconds are not the NavMesh times:

```bash
dotnet run --project dotnet/headless/EgressLab.Headless.csproj -c Release
```

### Headless drill, 40 people, spread 1

| Seed | Mode | Out | Caught | Remaining | Seconds | Peak queue |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 7 | Nearest exit | 28 | 12 | 0 | 15.0 | 12 |
| 7 | Decision support | 39 | 1 | 0 | 23.1 | 18 |
| 42 | Nearest exit | 28 | 12 | 0 | 15.5 | 12 |
| 42 | Decision support | 40 | 0 | 0 | 25.9 | 17 |
| 99 | Nearest exit | 28 | 12 | 0 | 17.2 | 12 |
| 99 | Decision support | 39 | 1 | 0 | 26.9 | 17 |

On seed 42 the first decision-support plan was: "Initial routes. 14 people kept off a burning or crowded exit. West 0 blocked, East 13, North 17, South 10."

The longer time is the detour plus the load on the safe doors. Survival is the quantity I am treating as the point of the comparison. The higher peak queue is real: once the west door is unacceptable, the other three absorb everyone, and the congestion term only stops one of them from taking the entire crowd.

### Editor runs to fill in after recording

These rows are for the NavMesh demo. I have not executed them in the Editor in this environment.

| Seed | People | Spread | Mode | Out | Caught | Time (s) | Notes |
| --- | ---: | ---: | --- | ---: | ---: | ---: | --- |
| 42 | 40 | 1.0 | Nearest exit |  |  |  | Record, then Copy |
| 42 | 40 | 1.0 | Decision support |  |  |  | Same seed, Restart |

## ML-Agents

The project runs without training. `docs/ML_AGENTS.md` specifies the observation vector (`RoutingObservation`, covered by a test), a reward I would use, and how a `Heuristic` method would call `DecisionSupportPolicy`. `ml-agents/routing_ppo.yaml` is a PPO starting point for when that package is installed. Nothing in `Assets` references `Unity.MLAgents`, so a missing package cannot break Play.

## Future work

- Replace the rectangle with a real floor plate (a traced plan, then IFC or a navmesh exported from a modelling tool).
- Drive the grid from an imported smoke field instead of the cellular rule.
- Sample hazard along the NavMesh path the agent is actually on, not only along the 0.5 m graph.
- Train the policy in ML-Agents against this heuristic, and keep the heuristic as the baseline in the same scene.
- Add stairs and a second floor, then a closed door and a warden instruction, which is closer to an incident commander's problem than a single static cost is.

## Limitations

- I could compile and test the decision core here. I could not press Play, so the NavMesh bake, the overlay layout, and the recording numbers still need one pass in the Editor.
- The crowd does not push. Congestion is a slowdown on the assigned door.
- Bodies are capsules. There is no animation and no audio.
- The overlay is laid out for a desktop 16:9 Game view.

## License

MIT. See [LICENSE](LICENSE). Copyright 2026 Fahad Iqbal.
