using System;
using System.Collections.Generic;
using EgressLab.Decision;
using NUnit.Framework;

namespace EgressLab.Tests
{
    public class HazardFieldTests
    {
        [Test]
        public void DefaultIgnition_BlocksWestDoor_AndLeavesTheOthersClear()
        {
            HazardField hazard = HazardField.IgniteDefault();
            IReadOnlyList<ExitState> exits = BuildingLayout.CreateExits();
            Assert.That(Sample(hazard, exits, "west"), Is.GreaterThanOrEqualTo(0.55f));
            Assert.That(Sample(hazard, exits, "east"), Is.LessThan(0.05f));
            Assert.That(Sample(hazard, exits, "north"), Is.LessThan(0.05f));
            Assert.That(Sample(hazard, exits, "south"), Is.LessThan(0.05f));
        }

        [Test]
        public void Step_SameSeed_ReproducesTheGrid()
        {
            float[] a = Evolve(7, 12);
            float[] b = Evolve(7, 12);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i]);
        }

        [Test]
        public void Step_DifferentSeeds_Diverge()
        {
            float[] a = Evolve(1, 6);
            float[] b = Evolve(2, 6);
            bool same = true;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) same = false;
            }

            Assert.IsFalse(same);
        }

        [Test]
        public void Step_ZeroRate_DoesNotChangeTheGrid()
        {
            HazardField hazard = HazardField.IgniteDefault();
            float[] before = hazard.CopyCells();
            var random = new Random(1);
            hazard.Step(0f, random);
            hazard.Step(0f, random);
            float[] after = hazard.CopyCells();
            for (int i = 0; i < before.Length; i++) Assert.AreEqual(before[i], after[i]);
        }

        [Test]
        public void Step_NeverCoolsACell()
        {
            HazardField hazard = HazardField.IgniteDefault();
            var random = new Random(3);
            for (int step = 0; step < 20; step++)
            {
                float[] before = hazard.CopyCells();
                hazard.Step(1f, random);
                float[] after = hazard.CopyCells();
                for (int i = 0; i < before.Length; i++)
                    Assert.That(after[i], Is.GreaterThanOrEqualTo(before[i]));
            }
        }

        [Test]
        public void Spread_ByThirtySeconds_HasGrownLocally_WithoutReachingTheSouthDoor()
        {
            var config = new SimulationConfig
            {
                Seed = 42,
                AgentCount = 4,
                SpreadRate = 1f,
                MaxSeconds = 30f
            };
            EvacuationSession session = EvacuationSession.Start(config);
            while (session.Elapsed < 30f && !session.IsComplete) session.Tick(0.25f, true);

            HazardField hazard = session.Hazard;
            float south = hazard.Sample(Approach(session, "south"));
            float east = hazard.Sample(Approach(session, "east"));
            Assert.That(south, Is.LessThan(0.2f));
            Assert.That(east, Is.LessThan(0.2f));
            Assert.That(hazard.Sample(new WorldPos(6.2f, 7.5f)), Is.GreaterThan(0.4f),
                "The front should have walked a few metres east of the initial blob.");
        }

        static float Sample(HazardField hazard, IReadOnlyList<ExitState> exits, string id)
        {
            for (int i = 0; i < exits.Count; i++)
            {
                if (exits[i].Id == id) return hazard.Sample(exits[i].Approach);
            }

            Assert.Fail("Missing exit " + id);
            return 0f;
        }

        static WorldPos Approach(EvacuationSession session, string id)
        {
            for (int i = 0; i < session.Exits.Count; i++)
            {
                if (session.Exits[i].Id == id) return session.Exits[i].Approach;
            }

            Assert.Fail("Missing exit " + id);
            return new WorldPos();
        }

        static float[] Evolve(int seed, int steps)
        {
            HazardField hazard = HazardField.IgniteDefault();
            var random = new Random(seed);
            for (int i = 0; i < steps; i++) hazard.Step(1f, random);
            return hazard.CopyCells();
        }
    }

    public class FloorGraphTests
    {
        [Test]
        public void SpawnPointsAndExits_AreWalkable_AndEveryExitIsReachable()
        {
            IReadOnlyList<ExitState> exits = BuildingLayout.CreateExits();
            FloorGraph graph = FloorGraph.Build(BuildingLayout.Walls, exits);
            for (int i = 0; i < exits.Count; i++)
            {
                Assert.IsTrue(graph.IsWalkable(exits[i].Arrival), exits[i].Id + " arrival is inside a wall.");
                Assert.IsFalse(float.IsInfinity(graph.Distance(exits[i].Id, exits[i].Arrival)));
            }

            IReadOnlyList<WorldPos[]> regions = BuildingLayout.SpawnRegions;
            for (int r = 0; r < regions.Count; r++)
            {
                for (int p = 0; p < regions[r].Length; p++)
                {
                    WorldPos point = regions[r][p];
                    Assert.IsTrue(graph.IsWalkable(point), "Spawn " + point + " is inside a wall.");
                    for (int e = 0; e < exits.Count; e++)
                    {
                        float distance = graph.Distance(exits[e].Id, point);
                        Assert.IsFalse(float.IsInfinity(distance), point + " cannot reach " + exits[e].Id);
                    }
                }
            }
        }

        [Test]
        public void JitteredSpawns_StayWalkable()
        {
            int[] seeds = { 1, 7, 42, 99 };
            int[] counts = { 20, 40, 80 };
            for (int s = 0; s < seeds.Length; s++)
            {
                for (int c = 0; c < counts.Length; c++)
                {
                    var config = new SimulationConfig { Seed = seeds[s], AgentCount = counts[c], StartWithHazard = false };
                    EvacuationSession session = EvacuationSession.Start(config);
                    for (int i = 0; i < session.Agents.Count; i++)
                    {
                        Assert.IsTrue(session.Graph.IsWalkable(session.Agents[i].Position));
                        Assert.AreEqual(i, session.Agents[i].Id);
                    }
                }
            }
        }

        [Test]
        public void SouthWestStandingPoint_ShortestWalkIsTheWestDoor()
        {
            IReadOnlyList<ExitState> exits = BuildingLayout.CreateExits();
            FloorGraph graph = FloorGraph.Build(BuildingLayout.Walls, exits);
            var origin = new WorldPos(6.5f, 3f);
            float west = graph.Distance("west", origin);
            Assert.That(west, Is.LessThan(graph.Distance("south", origin)));
            Assert.That(west, Is.LessThan(graph.Distance("east", origin)));
            Assert.That(west, Is.LessThan(graph.Distance("north", origin)));
        }
    }

    public class RoutingPolicyTests
    {
        [Test]
        public void NearestExit_PicksTheShortestWalk_EvenWhenThatDoorIsOnFire()
        {
            RouteContext context = BuildingContext(HazardField.IgniteDefault(), EmptyLoad());
            BlockFromHazard(context);
            var agent = new AgentState(0, new WorldPos(6.5f, 3f), 1.5f);
            RouteChoice choice = new NearestExitPolicy().Choose(agent, context);
            Assert.AreEqual("west", choice.ExitId);
            Assert.AreEqual(RouteReason.ShortestWalk, choice.Reason);
        }

        [Test]
        public void DecisionSupport_DoesNotSendTheSouthWestAgentIntoTheWestFire()
        {
            RouteContext context = BuildingContext(HazardField.IgniteDefault(), EmptyLoad());
            BlockFromHazard(context);
            Assert.IsTrue(Exit(context, "west").Blocked);
            var agent = new AgentState(0, new WorldPos(6.5f, 3f), 1.5f);
            RouteChoice choice = new DecisionSupportPolicy().Choose(agent, context);
            Assert.AreNotEqual("west", choice.ExitId);
            Assert.AreEqual(SwitchMotive.Hazard, choice.Motive);
        }

        [Test]
        public void OpenFloor_HazardOnTheShortDoor_SendsPeopleToTheClearDoor()
        {
            var exits = new List<ExitState>
            {
                new ExitState("near", "Near", new WorldPos(3f, 12f), new WorldPos(4f, 12f), 8),
                new ExitState("far", "Far", new WorldPos(18f, 12f), new WorldPos(17f, 12f), 8)
            };
            var hazard = new HazardField(BuildingLayout.CellsX, BuildingLayout.CellsZ);
            for (int x = 2; x <= 5; x++) hazard.Set(x, 12, 1f);
            exits[0].Blocked = true;
            var context = new RouteContext
            {
                Hazard = hazard,
                Exits = exits,
                Graph = FloorGraph.Build(new WallBox[0], exits),
                LoadByExit = new Dictionary<string, int> { { "near", 0 }, { "far", 0 } },
                Weights = new RoutingWeights(),
                LethalIntensity = 0.65f
            };
            var agent = new AgentState(0, new WorldPos(8f, 12f), 1.5f);
            Assert.AreEqual("near", new NearestExitPolicy().Choose(agent, context).ExitId);
            RouteChoice support = new DecisionSupportPolicy().Choose(agent, context);
            Assert.AreEqual("far", support.ExitId);
            Assert.AreEqual(SwitchMotive.Hazard, support.Motive);
        }

        [Test]
        public void Congestion_DivertsAwayFromASaturatedNearestDoor()
        {
            RouteContext context = BuildingContext(new HazardField(BuildingLayout.CellsX, BuildingLayout.CellsZ), EmptyLoad());
            var agent = new AgentState(0, new WorldPos(33f, 3f), 1.5f);
            string closest = new NearestExitPolicy().Choose(agent, context).ExitId;
            var loaded = new Dictionary<string, int>(EmptyLoad());
            loaded[closest] = 20;
            context.LoadByExit = loaded;
            RouteChoice choice = new DecisionSupportPolicy().Choose(agent, context);
            Assert.AreNotEqual(closest, choice.ExitId);
            Assert.AreEqual(SwitchMotive.Congestion, choice.Motive);
        }

        [Test]
        public void Hysteresis_HoldsASmallGain_AndYieldsToALargeOne()
        {
            // B is a little closer than the door the person is already on. The gain is real, but small.
            var exits = new List<ExitState>
            {
                new ExitState("a", "A", new WorldPos(14f, 10f), new WorldPos(14f, 10f), 10),
                new ExitState("b", "B", new WorldPos(11.5f, 10f), new WorldPos(11.5f, 10f), 10)
            };
            var context = new RouteContext
            {
                Hazard = new HazardField(BuildingLayout.CellsX, BuildingLayout.CellsZ),
                Exits = exits,
                Graph = FloorGraph.Build(new WallBox[0], exits),
                LoadByExit = new Dictionary<string, int> { { "a", 0 }, { "b", 0 } },
                Weights = new RoutingWeights(),
                LethalIntensity = 0.65f
            };
            var agent = new AgentState(0, new WorldPos(8f, 10f), 1.5f);
            agent.ExitId = "a";
            RouteChoice held = new DecisionSupportPolicy().Choose(agent, context);
            Assert.AreEqual("a", held.ExitId);
            Assert.AreEqual(RouteReason.KeptByHysteresis, held.Reason);

            var farAndNear = new List<ExitState>
            {
                new ExitState("a", "A", new WorldPos(36f, 10f), new WorldPos(36f, 10f), 10),
                new ExitState("b", "B", new WorldPos(20.5f, 10f), new WorldPos(20.5f, 10f), 10)
            };
            context.Exits = farAndNear;
            context.Graph = FloorGraph.Build(new WallBox[0], farAndNear);
            var traveller = new AgentState(1, new WorldPos(20f, 10f), 1.5f);
            traveller.ExitId = "a";
            RouteChoice switched = new DecisionSupportPolicy().Choose(traveller, context);
            Assert.AreEqual("b", switched.ExitId);
            Assert.AreEqual(RouteReason.LowerCost, switched.Reason);
        }

        [Test]
        public void BlockedCurrentExit_IsNotProtectedByHysteresis()
        {
            var exits = new List<ExitState>
            {
                new ExitState("a", "A", new WorldPos(10f, 10f), new WorldPos(10f, 10f), 8),
                new ExitState("b", "B", new WorldPos(14f, 10f), new WorldPos(14f, 10f), 8)
            };
            exits[0].Blocked = true;
            var hazard = new HazardField(BuildingLayout.CellsX, BuildingLayout.CellsZ);
            hazard.Set(10, 10, 1f);
            var context = new RouteContext
            {
                Hazard = hazard,
                Exits = exits,
                Graph = FloorGraph.Build(new WallBox[0], exits),
                LoadByExit = new Dictionary<string, int> { { "a", 0 }, { "b", 0 } },
                Weights = new RoutingWeights(),
                LethalIntensity = 0.65f
            };
            var agent = new AgentState(0, new WorldPos(12f, 10f), 1.5f);
            agent.ExitId = "a";
            RouteChoice choice = new DecisionSupportPolicy().Choose(agent, context);
            Assert.AreEqual("b", choice.ExitId);
            Assert.AreNotEqual(RouteReason.KeptByHysteresis, choice.Reason);
        }

        static RouteContext BuildingContext(HazardField hazard, Dictionary<string, int> load)
        {
            IReadOnlyList<ExitState> exits = BuildingLayout.CreateExits();
            return new RouteContext
            {
                Hazard = hazard,
                Exits = exits,
                Graph = FloorGraph.Build(BuildingLayout.Walls, exits),
                LoadByExit = load,
                Weights = new RoutingWeights(),
                LethalIntensity = 0.65f
            };
        }

        static Dictionary<string, int> EmptyLoad()
        {
            return new Dictionary<string, int>
            {
                { "west", 0 },
                { "east", 0 },
                { "north", 0 },
                { "south", 0 }
            };
        }

        static void BlockFromHazard(RouteContext context)
        {
            for (int i = 0; i < context.Exits.Count; i++)
            {
                ExitState exit = context.Exits[i];
                exit.Blocked = context.Hazard.Sample(exit.Approach) >= 0.55f;
            }
        }

        static ExitState Exit(RouteContext context, string id)
        {
            for (int i = 0; i < context.Exits.Count; i++)
            {
                if (context.Exits[i].Id == id) return context.Exits[i];
            }

            return null;
        }
    }

    public class SessionDrillTests
    {
        [Test]
        public void Tally_ConservesPeople_ForBothModes()
        {
            AssertConserved(RoutingMode.NearestExit);
            AssertConserved(RoutingMode.DecisionSupport);
        }

        [Test]
        public void CanonicalSeed_IsReproducible()
        {
            RunSnapshot first = Run(42, RoutingMode.DecisionSupport);
            RunSnapshot second = Run(42, RoutingMode.DecisionSupport);
            Assert.AreEqual(first.Evacuated, second.Evacuated);
            Assert.AreEqual(first.Caught, second.Caught);
            Assert.AreEqual(first.Remaining, second.Remaining);
            Assert.AreEqual(first.Seconds, second.Seconds, 0.0001f);
        }

        [Test]
        public void CanonicalSeed_DecisionSupport_LosesFewerPeopleThanNearestExit()
        {
            RunSnapshot nearest = Run(42, RoutingMode.NearestExit);
            RunSnapshot support = Run(42, RoutingMode.DecisionSupport);
            Assert.That(nearest.Caught, Is.GreaterThan(0), "The baseline should walk people into the west fire.");
            Assert.That(support.Caught, Is.LessThan(nearest.Caught));
            Assert.That(support.Evacuated, Is.GreaterThan(nearest.Evacuated));
            Assert.AreEqual(0, support.Remaining);
            Assert.AreEqual(0, nearest.Remaining);
        }

        [Test]
        public void SeveralSeeds_KeepTheSameOrdering()
        {
            int[] seeds = { 7, 42, 99 };
            for (int i = 0; i < seeds.Length; i++)
            {
                RunSnapshot nearest = Run(seeds[i], RoutingMode.NearestExit);
                RunSnapshot support = Run(seeds[i], RoutingMode.DecisionSupport);
                Assert.That(support.Caught, Is.LessThan(nearest.Caught), "Seed " + seeds[i]);
            }
        }

        [Test]
        public void CrowdMotion_SlowsOnlyAboveCapacity()
        {
            Assert.AreEqual(1.5f, CrowdMotion.Adjust(1.5f, 8, 8));
            Assert.That(CrowdMotion.Adjust(1.5f, 16, 8), Is.LessThan(1.5f));
        }

        static void AssertConserved(RoutingMode mode)
        {
            var config = new SimulationConfig { Seed = 42, AgentCount = 24, Mode = mode };
            EvacuationSession session = EvacuationSession.Start(config);
            int guard = 0;
            while (!session.IsComplete && guard++ < 5000)
            {
                session.Tick(0.2f, true);
                Assert.IsTrue(session.Tally.ConservesPeople);
            }

            Assert.IsTrue(session.IsComplete);
            Assert.IsTrue(session.Tally.ConservesPeople);
        }

        static RunSnapshot Run(int seed, RoutingMode mode)
        {
            var config = new SimulationConfig
            {
                Seed = seed,
                AgentCount = 40,
                SpreadRate = 1f,
                Mode = mode
            };
            EvacuationSession session = EvacuationSession.Start(config);
            int guard = 0;
            while (!session.IsComplete && guard++ < 5000) session.Tick(0.1f, true);
            Assert.IsTrue(session.IsComplete, mode + " did not finish.");
            return session.CreateSnapshot();
        }
    }

    public class ObservationTests
    {
        [Test]
        public void Write_FillsTheDocumentedLayout_AndReactsToABlockedDoor()
        {
            IReadOnlyList<ExitState> exits = BuildingLayout.CreateExits();
            FloorGraph graph = FloorGraph.Build(BuildingLayout.Walls, exits);
            HazardField hazard = HazardField.IgniteDefault();
            var agent = new AgentState(0, new WorldPos(6.5f, 3f), 1.8f);
            var buffer = new float[RoutingObservation.Size(exits.Count)];
            RoutingObservation.Write(buffer, agent, hazard, exits, graph, null);
            Assert.AreEqual(25 + exits.Count * 6 + 2, buffer.Length);
            for (int i = 0; i < buffer.Length; i++) Assert.IsFalse(float.IsNaN(buffer[i]) || float.IsInfinity(buffer[i]));

            var again = new float[buffer.Length];
            Array.Copy(buffer, again, buffer.Length);
            for (int i = 0; i < exits.Count; i++)
            {
                if (exits[i].Id == "east") exits[i].Blocked = true;
            }

            RoutingObservation.Write(again, agent, hazard, exits, graph, null);
            int eastBlockedIndex = 25 + 1 * RoutingObservation.PerExit + (RoutingObservation.PerExit - 1);
            Assert.AreEqual(0f, buffer[eastBlockedIndex]);
            Assert.AreEqual(1f, again[eastBlockedIndex]);
        }
    }
}
