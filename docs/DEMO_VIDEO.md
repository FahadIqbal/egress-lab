# Demo video

Aim for 90 seconds to two minutes. Record the Game view at 1280×720 or 1920×1080, 16:9. Use Unity Recorder or OBS. Leave the Scene view and the Console out of the frame. Set the seed to 42, people to 40, and spread to 1 before the first run, then do not touch those three until both runs have finished.

I would say the lines below in my own voice, a bit shorter if the picture is already doing the work.

## Shot list

| Time | On screen | Say |
| --- | --- | --- |
| 0:00 | Press Play. Hold on the whole floor: four doors, the fire on the west door, amber people. | "This is Egress Lab. A single floor, a spreading fire, and a choice of exit." |
| 0:12 | Let the amber crowd commit. Zoom slightly toward the west door as people enter the red cells and fall. | "This run is the baseline. Each person takes the shortest walk. The west door is already burning, and they still go there." |
| 0:28 | Pull back so the overlay is readable. Point at Caught, and at West marked blocked. | "Caught is people the fire got. The other three doors are still open. The baseline cannot use that." |
| 0:40 | Wait until the floor clears, or until Caught has stopped climbing and only the safe side is still moving. If you cut early, say that the run was cut. | "Same crowd, same seed, next to a policy that can see the fire and the queues." |
| 0:48 | Click Decision support, then Restart. The crowd comes back cyan. Hold on the headline under the counts. | "Decision support replans from hazard along the walk, the queue at the door, and whether the door itself is blocked." |
| 1:05 | Follow a few cyan people out of the west rooms toward north, east, or south. Do not follow them into the fire; they should not go there. | "The west door stays empty. Those people take a longer walk." |
| 1:20 | Pull back to the metrics card when the second run finishes. Both lines should be visible: Nearest and Support. | "On this seed the baseline loses people at the west door. The router keeps them off it. The evacuation takes longer because the safe doors are doing all the work." |
| 1:35 | Move the spread slider up for two seconds, then set it back. Optional. | "Spread, crowd size, and seed are on the overlay. The comparison uses one seed so the fire and the spawn match." |
| 1:45 | Hold on the two result lines. | "The routing code is plain C# with tests. This scene is the part I record." |

## If something looks wrong

- People do not move: the NavMesh bake failed. The status line on the left says so, and the Console has the error. Check that the floor and wall colliders exist under `EvacuationWorld/Geometry` while the game is playing.
- The fire is on the wrong side of the building: north is the far edge, the west door is on the left. The word WEST is on the floor by that door.
- The two result lines are missing: a run only stores its summary when everyone is out or caught, or when the time limit hits. Let the first run finish before Restart.

## Export

Keep the recording under two minutes. I will put the Editor counts from the metrics card into the empty table in the README after this take. The Copy button puts the same numbers on the clipboard as CSV.
