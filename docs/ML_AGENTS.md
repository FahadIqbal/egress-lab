# Optional ML-Agents routing policy

Egress Lab does not train a model, and Play mode does not need the ML-Agents package. The policy that runs is `DecisionSupportPolicy`. This note is the contract I would use if I added a learned policy later and kept that utility function as the heuristic and the baseline.

## Observation

`RoutingObservation.Write` fills a float buffer. The size is `RoutingObservation.Size(exitCount)`.

With four doors the layout is:

| Slice | Count | Meaning |
| --- | ---: | --- |
| Local hazard | 25 | 5×5 window of cell intensity around the person, row-major, out-of-range cells are 0 |
| Per door, in west, east, north, south order | 6 each | `dx / width`, `dz / depth`, walk length / diagonal (clipped to 1), path exposure, queue / capacity, 1 if the door is blocked |
| Self | 2 | speed / 3, seconds already spent in a lethal cell |

Exposure is `0.35 * mean + 0.65 * max` along the grid path, the same mix the utility score uses. `ObservationTests` checks the length, that every value is finite, and that marking the east door blocked flips the corresponding flag.

## Action

Discrete, one branch, four actions: west, east, north, south. Illegal actions are doors the grid cannot reach. I would mask those rather than let the agent learn a wall.

## Reward I would use

- `+1` on a successful exit.
- `-1` when the person is caught.
- `-0.001` per decision step, so lingering is not free.
- `-0.05` when the person moves into a cell hotter than the one they left.

I would not reward "matching the heuristic" as the only signal. The heuristic is the baseline and the `Heuristic()` fallback, not the target.

## Heuristic

A future `Agent` subclass would stay out of this repo until the package is installed. The method I would add is:

```csharp
public override void Heuristic(in ActionBuffers actions)
{
    var choice = new DecisionSupportPolicy().Choose(agentState, context);
    actions.DiscreteActions[0] = ExitIndex(choice.ExitId);
}
```

`CollectObservations` would call `RoutingObservation.Write` into `sensor.AddObservation` one float at a time. Behaviour name: `EgressRouting`, matching `ml-agents/routing_ppo.yaml`.

## Train later

1. Add the ML-Agents Unity package to this project.
2. Put the agent component on the person prefab or on the bootstrap-spawned body, behind the package's scripting define so the current scene still compiles without it.
3. Build a standalone player or use the Editor as the environment.
4. Run the YAML from the repository root with the ML-Agents `mlagents-learn` entry point, after installing that Python package yourself.

I have not run that loop. The YAML is a reasonable PPO starting point (normalised observations, two layers of 128, half a million steps), not a tuned result.
