using System.Collections.Generic;

namespace EgressLab.Decision
{
    /// <summary>
    /// The canonical floor. One metre is one hazard cell. The routing graph uses a finer 0.5 m grid
    /// rasterised from <see cref="Walls"/>. Door gaps are deliberate openings in the outer walls.
    ///
    /// Plan (not to scale). Gaps are the exits.
    ///
    ///   z=24  +-------- NW --------+   +---------- NE ----------+
    ///         |                    |   |                        |
    ///   z=17  |                    +   |                        |
    ///         |                    opening to the centre        |
    ///   z=12  +------+    +--------+   +------+                 +
    ///         | SW   |                    | SE                   |  east gap z=8..11
    ///   z=6   |  gap |                    |                      |
    ///   z=0   +------+--------------------+---+              +---+
    ///           west gap z=6..9               south gap x=26..29
    /// </summary>
    public static class BuildingLayout
    {
        public const float Width = 40f;
        public const float Depth = 24f;
        public const int CellsX = 40;
        public const int CellsZ = 24;
        public const float WallHeight = 3f;

        public static float Diagonal
        {
            get { return (float)System.Math.Sqrt(Width * Width + Depth * Depth); }
        }

        public static IReadOnlyList<WallBox> Walls { get; } = new[]
        {
            // South wall, gap x = 26..29.
            new WallBox(13.0f, 0.25f, 26.0f, 0.5f),
            new WallBox(34.5f, 0.25f, 11.0f, 0.5f),

            // North wall, gap x = 16..19.
            new WallBox(8.0f, 23.75f, 16.0f, 0.5f),
            new WallBox(29.5f, 23.75f, 21.0f, 0.5f),

            // West wall, gap z = 6..9. Kept below the z=12 partition so the door is not walled shut.
            new WallBox(0.25f, 3.0f, 0.5f, 6.0f),
            new WallBox(0.25f, 16.5f, 0.5f, 15.0f),

            // East wall, gap z = 8..11.
            new WallBox(39.75f, 4.0f, 0.5f, 8.0f),
            new WallBox(39.75f, 17.5f, 0.5f, 13.0f),

            // Interior vertical at x = 14. Opening z = 9..17 keeps the north-west room connected.
            new WallBox(14.0f, 4.75f, 0.4f, 8.5f),
            new WallBox(14.0f, 20.25f, 0.4f, 6.5f),

            // Interior vertical at x = 27. Opening z = 7..13.
            new WallBox(27.0f, 3.75f, 0.4f, 6.5f),
            new WallBox(27.0f, 18.25f, 0.4f, 10.5f),

            // Interior horizontal at z = 12. Openings x = 8..14 and x = 22..30.
            new WallBox(4.25f, 12.0f, 7.5f, 0.4f),
            new WallBox(18.2f, 12.0f, 7.6f, 0.4f),
            new WallBox(34.75f, 12.0f, 9.5f, 0.4f)
        };

        public static IReadOnlyList<ExitState> CreateExits()
        {
            // Arrival is the point a person has to reach to be counted as out.
            // Approach is a point just inside the door, sampled to decide if the door is on fire.
            return new[]
            {
                new ExitState("west", "West", new WorldPos(1.4f, 7.5f), new WorldPos(2.4f, 7.5f), 8),
                new ExitState("east", "East", new WorldPos(38.5f, 9.5f), new WorldPos(37.6f, 9.5f), 12),
                new ExitState("north", "North", new WorldPos(17.5f, 22.4f), new WorldPos(17.5f, 21.6f), 12),
                new ExitState("south", "South", new WorldPos(27.5f, 1.3f), new WorldPos(27.5f, 2.2f), 8)
            };
        }

        /// <summary>
        /// Hand-checked standing points, one array per room. The spawner round-robins rooms so a
        /// smaller crowd still contains people on the threatened west side.
        /// </summary>
        public static IReadOnlyList<WorldPos[]> SpawnRegions { get; } = new[]
        {
            new[]
            {
                new WorldPos(3.5f, 3.0f),
                new WorldPos(6.5f, 3.0f),
                new WorldPos(9.5f, 3.5f),
                new WorldPos(4.0f, 6.0f),
                new WorldPos(7.5f, 6.5f),
                new WorldPos(10.5f, 7.0f),
                new WorldPos(5.0f, 8.5f),
                new WorldPos(8.5f, 8.8f)
            },
            new[]
            {
                new WorldPos(3.5f, 15.5f),
                new WorldPos(7.0f, 16.0f),
                new WorldPos(10.0f, 16.2f),
                new WorldPos(4.0f, 19.0f),
                new WorldPos(8.0f, 20.0f),
                new WorldPos(11.0f, 21.0f)
            },
            new[]
            {
                new WorldPos(31.0f, 2.5f),
                new WorldPos(34.5f, 3.0f),
                new WorldPos(37.0f, 4.0f),
                new WorldPos(32.0f, 6.5f),
                new WorldPos(36.0f, 7.5f),
                new WorldPos(33.0f, 9.0f)
            },
            new[]
            {
                new WorldPos(31.0f, 15.0f),
                new WorldPos(35.0f, 16.5f),
                new WorldPos(37.5f, 19.0f),
                new WorldPos(32.5f, 20.5f),
                new WorldPos(36.0f, 21.0f)
            },
            new[]
            {
                new WorldPos(17.0f, 15.0f),
                new WorldPos(21.0f, 16.0f),
                new WorldPos(24.0f, 18.0f),
                new WorldPos(18.0f, 20.0f),
                new WorldPos(22.0f, 21.0f)
            }
        };
    }
}
