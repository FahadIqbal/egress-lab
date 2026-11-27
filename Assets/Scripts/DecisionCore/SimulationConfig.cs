namespace EgressLab.Decision
{
    public enum RoutingMode
    {
        NearestExit = 0,
        DecisionSupport = 1
    }

    /// <summary>
    /// Knobs for one run. The Unity overlay writes these, and the headless drill uses the same type,
    /// so a seed lines up a comparable pair of routes.
    /// </summary>
    public sealed class SimulationConfig
    {
        public int AgentCount { get; set; }
        public float SpreadRate { get; set; }
        public int Seed { get; set; }
        public RoutingMode Mode { get; set; }
        public float AgentSpeed { get; set; }
        public float HazardStepSeconds { get; set; }
        public float ReplanSeconds { get; set; }
        public float LethalIntensity { get; set; }
        public float LethalDwellSeconds { get; set; }
        public float InstantCatchIntensity { get; set; }
        public float BlockIntensity { get; set; }
        public float ArriveRadius { get; set; }
        public float MaxSeconds { get; set; }
        public bool StartWithHazard { get; set; }
        public RoutingWeights Weights { get; set; }

        public SimulationConfig()
        {
            AgentCount = 40;
            SpreadRate = 1f;
            Seed = 42;
            Mode = RoutingMode.NearestExit;
            AgentSpeed = 1.55f;
            HazardStepSeconds = 0.5f;
            ReplanSeconds = 1f;
            LethalIntensity = 0.65f;
            LethalDwellSeconds = 0.6f;
            InstantCatchIntensity = 0.9f;
            BlockIntensity = 0.55f;
            ArriveRadius = 1.05f;
            MaxSeconds = 120f;
            StartWithHazard = true;
            Weights = new RoutingWeights();
        }

        public SimulationConfig Clone()
        {
            return new SimulationConfig
            {
                AgentCount = AgentCount,
                SpreadRate = SpreadRate,
                Seed = Seed,
                Mode = Mode,
                AgentSpeed = AgentSpeed,
                HazardStepSeconds = HazardStepSeconds,
                ReplanSeconds = ReplanSeconds,
                LethalIntensity = LethalIntensity,
                LethalDwellSeconds = LethalDwellSeconds,
                InstantCatchIntensity = InstantCatchIntensity,
                BlockIntensity = BlockIntensity,
                ArriveRadius = ArriveRadius,
                MaxSeconds = MaxSeconds,
                StartWithHazard = StartWithHazard,
                Weights = Weights != null ? Weights.Clone() : new RoutingWeights()
            };
        }

        public void Normalize()
        {
            if (AgentCount < 1) AgentCount = 1;
            if (AgentCount > 120) AgentCount = 120;
            if (SpreadRate < 0f) SpreadRate = 0f;
            if (SpreadRate > 3f) SpreadRate = 3f;
            if (AgentSpeed < 0.2f) AgentSpeed = 0.2f;
            if (AgentSpeed > 6f) AgentSpeed = 6f;
            if (HazardStepSeconds < 0.05f) HazardStepSeconds = 0.05f;
            if (ReplanSeconds < 0.1f) ReplanSeconds = 0.1f;
            if (LethalIntensity < 0.05f) LethalIntensity = 0.05f;
            if (LethalDwellSeconds < 0f) LethalDwellSeconds = 0f;
            if (InstantCatchIntensity < LethalIntensity) InstantCatchIntensity = LethalIntensity;
            if (BlockIntensity < 0.05f) BlockIntensity = 0.05f;
            if (ArriveRadius < 0.3f) ArriveRadius = 0.3f;
            if (MaxSeconds < 5f) MaxSeconds = 5f;
            if (Weights == null) Weights = new RoutingWeights();
        }
    }

    /// <summary>
    /// Relative weights for the utility score. They are dimensionless: each term is already scaled
    /// to roughly 0..1 (or a small multiple of that) before the weight is applied.
    /// </summary>
    public sealed class RoutingWeights
    {
        public float Distance { get; set; }
        public float Exposure { get; set; }
        public float Congestion { get; set; }
        public float BlockedExit { get; set; }
        public float LethalCorridor { get; set; }
        public float Hysteresis { get; set; }

        public RoutingWeights()
        {
            Distance = 1f;
            Exposure = 2.4f;
            Congestion = 0.75f;
            BlockedExit = 4f;
            LethalCorridor = 2.2f;
            Hysteresis = 0.18f;
        }

        public RoutingWeights Clone()
        {
            return new RoutingWeights
            {
                Distance = Distance,
                Exposure = Exposure,
                Congestion = Congestion,
                BlockedExit = BlockedExit,
                LethalCorridor = LethalCorridor,
                Hysteresis = Hysteresis
            };
        }
    }
}
