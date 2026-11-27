using System;
using System.Collections.Generic;

namespace EgressLab.Decision
{
    /// <summary>
    /// One evacuation episode: hazard, doors, people, and the active routing policy.
    /// Unity writes positions from NavMesh agents and calls <see cref="Tick"/> with integration off.
    /// The headless drill turns integration on and walks the same floor graph the router uses.
    /// </summary>
    public sealed class EvacuationSession
    {
        readonly List<AgentState> _agents;
        readonly List<ExitState> _exits;
        readonly FloorGraph _graph;
        readonly Random _hazardRng;
        float _hazardAccum;
        float _replanAccum;

        EvacuationSession(
            SimulationConfig config,
            HazardField hazard,
            List<ExitState> exits,
            FloorGraph graph,
            List<AgentState> agents)
        {
            Config = config;
            Hazard = hazard;
            _exits = exits;
            _graph = graph;
            _agents = agents;
            Tally = new EvacuationTally();
            _hazardRng = new Random(unchecked(config.Seed * 397) ^ 0x51ED123);
            UpdateBlocks();
            Recount();
            ApplyPolicy();
        }

        public SimulationConfig Config { get; }
        public HazardField Hazard { get; }
        public FloorGraph Graph { get { return _graph; } }
        public IReadOnlyList<ExitState> Exits { get { return _exits; } }
        public IReadOnlyList<AgentState> Agents { get { return _agents; } }
        public EvacuationTally Tally { get; }
        public ReplanSummary LastReplan { get; private set; }
        public float Elapsed { get; private set; }
        public float CompletionTime { get; private set; }
        public bool IsComplete { get; private set; }
        public bool HitTimeLimit { get; private set; }

        public static EvacuationSession Start(SimulationConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            SimulationConfig copy = config.Clone();
            copy.Normalize();
            HazardField hazard = copy.StartWithHazard
                ? HazardField.IgniteDefault()
                : new HazardField(BuildingLayout.CellsX, BuildingLayout.CellsZ);
            List<ExitState> exits = new List<ExitState>(BuildingLayout.CreateExits());
            FloorGraph graph = FloorGraph.Build(BuildingLayout.Walls, exits);
            List<AgentState> agents = CrowdSpawner.Spawn(copy, graph);
            return new EvacuationSession(copy, hazard, exits, graph, agents);
        }

        public static EvacuationSession Create(SimulationConfig config, HazardField hazard, IList<AgentState> agents)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (hazard == null) throw new ArgumentNullException("hazard");
            if (agents == null) throw new ArgumentNullException("agents");
            SimulationConfig copy = config.Clone();
            copy.Normalize();
            List<ExitState> exits = new List<ExitState>(BuildingLayout.CreateExits());
            FloorGraph graph = FloorGraph.Build(BuildingLayout.Walls, exits);
            var copyAgents = new List<AgentState>(agents.Count);
            for (int i = 0; i < agents.Count; i++)
            {
                AgentState source = agents[i];
                var clone = new AgentState(source.Id, source.Position, source.BaseSpeed);
                clone.ExitId = source.ExitId;
                clone.Status = source.Status;
                clone.DwellInHazard = source.DwellInHazard;
                copyAgents.Add(clone);
            }

            return new EvacuationSession(copy, hazard, exits, graph, copyAgents);
        }

        public void SetMode(RoutingMode mode)
        {
            Config.Mode = mode;
            ApplyPolicy();
        }

        public void SetSpreadRate(float spreadRate)
        {
            if (spreadRate < 0f) spreadRate = 0f;
            if (spreadRate > 3f) spreadRate = 3f;
            Config.SpreadRate = spreadRate;
        }

        public float SpeedFor(AgentState agent)
        {
            if (agent == null) return 0f;
            ExitState exit = FindExit(agent.ExitId);
            if (exit == null) return agent.BaseSpeed;
            return CrowdMotion.Adjust(agent.BaseSpeed, exit.ActiveCount, exit.SoftCapacity);
        }

        public void Tick(float dt, bool integrateStraightWalk)
        {
            if (dt < 0f) dt = 0f;
            if (IsComplete) return;

            Elapsed += dt;
            AdvanceHazard(dt);
            bool doorsChanged = UpdateBlocks();
            _replanAccum += dt;
            if (doorsChanged || _replanAccum >= Config.ReplanSeconds)
            {
                _replanAccum = 0f;
                ApplyPolicy();
            }

            if (integrateStraightWalk) Integrate(dt);
            Resolve(dt);
            Recount();

            if (Tally.Active == 0 || Elapsed >= Config.MaxSeconds)
            {
                IsComplete = true;
                CompletionTime = Elapsed;
                HitTimeLimit = Tally.Active > 0;
            }
        }

        public RunSnapshot CreateSnapshot()
        {
            return new RunSnapshot
            {
                Mode = Config.Mode,
                Seed = Config.Seed,
                Agents = _agents.Count,
                SpreadRate = Config.SpreadRate,
                Seconds = IsComplete ? CompletionTime : Elapsed,
                Evacuated = Tally.Evacuated,
                Caught = Tally.Caught,
                Remaining = Tally.Active,
                HitTimeLimit = HitTimeLimit,
                PeakCongestion = Tally.PeakCongestion
            };
        }

        void AdvanceHazard(float dt)
        {
            _hazardAccum += dt;
            int guard = 0;
            while (_hazardAccum >= Config.HazardStepSeconds && guard++ < 8)
            {
                _hazardAccum -= Config.HazardStepSeconds;
                Hazard.Step(Config.SpreadRate, _hazardRng);
            }
        }

        bool UpdateBlocks()
        {
            bool changed = false;
            for (int i = 0; i < _exits.Count; i++)
            {
                bool blocked = Hazard.Sample(_exits[i].Approach) >= Config.BlockIntensity;
                if (blocked != _exits[i].Blocked) changed = true;
                _exits[i].Blocked = blocked;
            }

            return changed;
        }

        void ApplyPolicy()
        {
            IRoutingPolicy policy = RoutingPolicies.Create(Config.Mode);
            var load = new Dictionary<string, int>();
            for (int i = 0; i < _exits.Count; i++) load[_exits[i].Id] = 0;

            int rerouted = 0;
            int held = 0;
            int hazardChoices = 0;
            int congestionChoices = 0;
            int distanceChoices = 0;
            var context = new RouteContext
            {
                Hazard = Hazard,
                Exits = _exits,
                Graph = _graph,
                LoadByExit = load,
                Weights = Config.Weights,
                LethalIntensity = Config.LethalIntensity
            };

            for (int i = 0; i < _agents.Count; i++)
            {
                AgentState agent = _agents[i];
                if (agent.Status != AgentStatus.Moving) continue;
                RouteChoice choice = policy.Choose(agent, context);
                bool hadExit = agent.ExitId != null;
                bool same = agent.ExitId == choice.ExitId;
                if (!hadExit)
                {
                    CountMotive(choice.Motive, ref hazardChoices, ref congestionChoices, ref distanceChoices);
                }
                else if (same)
                {
                    held++;
                }
                else
                {
                    rerouted++;
                    CountMotive(choice.Motive, ref hazardChoices, ref congestionChoices, ref distanceChoices);
                }

                agent.ExitId = choice.ExitId;
                if (choice.ExitId != null)
                {
                    int n;
                    load.TryGetValue(choice.ExitId, out n);
                    load[choice.ExitId] = n + 1;
                }
            }

            Recount();
            LastReplan = ReplanSummary.Compose(
                Config.Mode,
                _exits,
                rerouted,
                held,
                hazardChoices,
                congestionChoices,
                distanceChoices);
        }

        static void CountMotive(SwitchMotive motive, ref int hazardChoices, ref int congestionChoices, ref int distanceChoices)
        {
            if (motive == SwitchMotive.Hazard) hazardChoices++;
            else if (motive == SwitchMotive.Congestion) congestionChoices++;
            else if (motive == SwitchMotive.Distance) distanceChoices++;
        }

        void Integrate(float dt)
        {
            Recount();
            for (int i = 0; i < _agents.Count; i++)
            {
                AgentState agent = _agents[i];
                if (agent.Status != AgentStatus.Moving) continue;
                ExitState exit = FindExit(agent.ExitId);
                if (exit == null) continue;
                float remaining = SpeedFor(agent) * dt;
                int guard = 0;
                while (remaining > 0.0001f && guard++ < 8)
                {
                    WorldPos way = _graph.Steer(exit.Id, agent.Position, exit.Arrival);
                    WorldPos delta = way - agent.Position;
                    float distance = delta.Length;
                    if (distance < 0.001f)
                    {
                        if (agent.Position.DistanceTo(exit.Arrival) <= Config.ArriveRadius)
                            agent.Position = exit.Arrival;
                        break;
                    }

                    if (distance <= remaining)
                    {
                        agent.Position = way;
                        remaining -= distance;
                    }
                    else
                    {
                        agent.Position = agent.Position + delta * (remaining / distance);
                        remaining = 0f;
                    }
                }
            }
        }

        void Resolve(float dt)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                AgentState agent = _agents[i];
                if (agent.Status != AgentStatus.Moving) continue;
                float here = Hazard.Sample(agent.Position);
                if (here >= Config.InstantCatchIntensity)
                {
                    agent.Status = AgentStatus.Caught;
                    continue;
                }

                if (here >= Config.LethalIntensity)
                {
                    agent.DwellInHazard += dt;
                    if (agent.DwellInHazard >= Config.LethalDwellSeconds)
                    {
                        agent.Status = AgentStatus.Caught;
                        continue;
                    }
                }
                else
                {
                    agent.DwellInHazard = 0f;
                }

                ExitState exit = FindExit(agent.ExitId);
                if (exit == null) continue;
                if (agent.Position.DistanceTo(exit.Arrival) <= Config.ArriveRadius)
                {
                    if (exit.Blocked || here >= Config.LethalIntensity)
                    {
                        agent.Status = AgentStatus.Caught;
                    }
                    else
                    {
                        agent.Status = AgentStatus.Evacuated;
                        exit.EvacuatedCount++;
                    }
                }
            }
        }

        void Recount()
        {
            for (int i = 0; i < _exits.Count; i++) _exits[i].ActiveCount = 0;
            int evacuated = 0;
            int caught = 0;
            int active = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                AgentState agent = _agents[i];
                if (agent.Status == AgentStatus.Evacuated) evacuated++;
                else if (agent.Status == AgentStatus.Caught) caught++;
                else
                {
                    active++;
                    ExitState exit = FindExit(agent.ExitId);
                    if (exit != null) exit.ActiveCount++;
                }
            }

            Tally.Total = _agents.Count;
            Tally.Evacuated = evacuated;
            Tally.Caught = caught;
            Tally.Active = active;
            for (int i = 0; i < _exits.Count; i++)
            {
                if (_exits[i].ActiveCount > Tally.PeakCongestion)
                    Tally.PeakCongestion = _exits[i].ActiveCount;
            }
        }

        ExitState FindExit(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < _exits.Count; i++)
            {
                if (_exits[i].Id == id) return _exits[i];
            }

            return null;
        }
    }

    public static class CrowdSpawner
    {
        public static List<AgentState> Spawn(SimulationConfig config, FloorGraph graph)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (graph == null) throw new ArgumentNullException("graph");
            var random = new Random(config.Seed);
            IReadOnlyList<WorldPos[]> regions = BuildingLayout.SpawnRegions;
            int count = config.AgentCount;
            var agents = new List<AgentState>(count);
            for (int i = 0; i < count; i++)
            {
                WorldPos[] region = regions[i % regions.Count];
                WorldPos point = region[(i / regions.Count) % region.Length];
                WorldPos placed = Jitter(point, random, graph);
                float speed = config.AgentSpeed * (0.88f + 0.24f * (float)random.NextDouble());
                agents.Add(new AgentState(i, placed, speed));
            }

            return agents;
        }

        static WorldPos Jitter(WorldPos point, Random random, FloorGraph graph)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float jx = ((float)random.NextDouble() - 0.5f) * 0.7f;
                float jz = ((float)random.NextDouble() - 0.5f) * 0.7f;
                var candidate = new WorldPos(point.X + jx, point.Z + jz);
                if (graph.IsWalkable(candidate)) return candidate;
            }

            if (graph.IsWalkable(point)) return point;
            return graph.Snap(point);
        }
    }
}
