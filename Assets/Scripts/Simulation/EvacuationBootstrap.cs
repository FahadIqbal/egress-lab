using System.Globalization;
using System.Text;
using EgressLab.Decision;
using UnityEngine;

namespace EgressLab.Simulation
{
    /// <summary>
    /// Drop this on an empty GameObject and press Play. It builds the floor, bakes a NavMesh,
    /// starts the hazard, and spawns the crowd. The included sample scene already has one.
    /// </summary>
    public sealed class EvacuationBootstrap : MonoBehaviour
    {
        [Header("Scenario")]
        [SerializeField] int agentCount = 40;
        [SerializeField] float spreadRate = 1f;
        [SerializeField] int seed = 42;
        [SerializeField] RoutingMode mode = RoutingMode.NearestExit;

        [Header("Motion")]
        [SerializeField] float agentSpeed = 1.55f;
        [SerializeField] float replanSeconds = 1f;

        EvacuationSession _session;
        FloorBuilder.Result _floor;
        NavMeshBaker.BakeResult _nav;
        CrowdVisualizer _crowd;
        HazardVisualizer _hazard;
        SimulationHud _hud;
        CameraRig _camera;
        GameObject _world;
        readonly RunArchive _archive = new RunArchive();
        float _speed = 1f;
        bool _paused;
        bool _stored;
        bool _restartRequested;

        public int AgentCount
        {
            get { return agentCount; }
            set { agentCount = Mathf.Clamp(value, 1, 120); }
        }

        public float SpreadRate
        {
            get { return spreadRate; }
            set
            {
                spreadRate = Mathf.Clamp(value, 0f, 3f);
                if (_session != null) _session.SetSpreadRate(spreadRate);
            }
        }

        public int Seed
        {
            get { return seed; }
            set { seed = value; }
        }

        public float Speed { get { return _speed; } }
        public bool Paused { get { return _paused; } }
        public EvacuationSession Session { get { return _session; } }
        public RunArchive Archive { get { return _archive; } }
        public string BakeNote { get; private set; }

        void Start()
        {
            _camera = CameraRig.Ensure();
            EnsureLight();
            Begin();
        }

        void Update()
        {
            ReadKeys();
            if (_restartRequested)
            {
                _restartRequested = false;
                Begin();
            }

            if (_camera != null) _camera.Tick();
            if (_session == null) return;

            float dt = Mathf.Min(Time.deltaTime, 0.25f);
            _crowd.PushPositions(_session);
            _session.Tick(dt, false);
            _crowd.Pull(_session);
            _hazard.Apply(_session.Hazard);
            _floor.PaintExits(_session.Exits);
            if (_session.IsComplete && !_stored)
            {
                _stored = true;
                _archive.Store(_session.CreateSnapshot());
            }

            if (_hud != null) _hud.Refresh();
        }

        public void SetMode(RoutingMode next)
        {
            mode = next;
            if (_session == null) return;
            _session.SetMode(next);
            if (_hud != null) _hud.Refresh();
        }

        public void SetSpeed(float speed)
        {
            _speed = Mathf.Clamp(speed, 0.25f, 8f);
            if (!_paused) Time.timeScale = _speed;
        }

        public void TogglePause()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : _speed;
        }

        public void Restart()
        {
            _restartRequested = true;
        }

        public void CopySummary()
        {
            GUIUtility.systemCopyBuffer = _archive.Csv(_session);
        }

        void Begin()
        {
            ClearWorld();
            if (agentCount <= 0)
            {
                agentCount = 40;
                spreadRate = 1f;
                agentSpeed = 1.55f;
                replanSeconds = 1f;
            }

            _world = new GameObject("EvacuationWorld");
            _floor = FloorBuilder.Build(_world.transform);
            _nav = NavMeshBaker.Bake(_floor.ColliderRoot);
            BakeNote = _nav.Ok ? null : "NavMesh did not bake, so people cannot walk. See the Console.";
            _session = EvacuationSession.Start(BuildConfig());
            _crowd = CrowdVisualizer.Spawn(_world.transform, _session);
            _crowd.SnapToNavMesh(_session);
            _session.SetMode(mode);
            _hazard = HazardVisualizer.Create(_world.transform, _session.Hazard);
            _stored = false;
            if (_hud != null) _hud.DestroyHud();
            _hud = SimulationHud.Create(this);
            _hud.Refresh();
        }

        SimulationConfig BuildConfig()
        {
            return new SimulationConfig
            {
                AgentCount = agentCount,
                SpreadRate = spreadRate,
                Seed = seed,
                Mode = mode,
                AgentSpeed = agentSpeed <= 0f ? 1.55f : agentSpeed,
                ReplanSeconds = replanSeconds <= 0f ? 1f : replanSeconds,
                StartWithHazard = true
            };
        }

        void ClearWorld()
        {
            if (_floor != null) _floor.DestroyOwned();
            if (_nav.Instance.valid) NavMesh.RemoveNavMeshData(_nav.Instance);
            if (_world != null) Destroy(_world);
            _world = null;
            _session = null;
            _crowd = null;
            _hazard = null;
        }

        void ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.B)) SetMode(RoutingMode.NearestExit);
            if (Input.GetKeyDown(KeyCode.D)) SetMode(RoutingMode.DecisionSupport);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(1f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(2f);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetSpeed(4f);
        }

        static void EnsureLight()
        {
            Light light = null;
            Light[] lights = Object.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) light = lights[i];
            }

            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.color = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.12f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(56f, -32f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.50f);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_nav.Instance.valid) NavMesh.RemoveNavMeshData(_nav.Instance);
            Palette.DestroyCreated();
        }
    }

    public sealed class RunArchive
    {
        public RunSnapshot Nearest { get; private set; }
        public RunSnapshot Support { get; private set; }

        public void Store(RunSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (snapshot.Mode == RoutingMode.DecisionSupport) Support = snapshot;
            else Nearest = snapshot;
        }

        public string Describe()
        {
            if (Nearest == null && Support == null) return "Finish a run, then switch mode and restart on the same seed.";
            var builder = new StringBuilder();
            if (Nearest != null) builder.AppendLine(Line("Nearest", Nearest));
            if (Support != null) builder.Append(Line("Support", Support));
            return builder.ToString();
        }

        public string Csv(EvacuationSession live)
        {
            var builder = new StringBuilder();
            builder.AppendLine("mode,seed,agents,spread,evacuated,caught,remaining,seconds");
            Append(builder, Nearest);
            Append(builder, Support);
            if (live != null) Append(builder, live.CreateSnapshot());
            return builder.ToString();
        }

        static void Append(StringBuilder builder, RunSnapshot snapshot)
        {
            if (snapshot == null) return;
            builder.Append(snapshot.Mode);
            builder.Append(',');
            builder.Append(snapshot.Seed);
            builder.Append(',');
            builder.Append(snapshot.Agents);
            builder.Append(',');
            builder.Append(snapshot.SpreadRate.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(snapshot.Evacuated);
            builder.Append(',');
            builder.Append(snapshot.Caught);
            builder.Append(',');
            builder.Append(snapshot.Remaining);
            builder.Append(',');
            builder.AppendLine(snapshot.Seconds.ToString("0.0", CultureInfo.InvariantCulture));
        }

        static string Line(string name, RunSnapshot snapshot)
        {
            return name + "  out " + snapshot.Evacuated + "  caught " + snapshot.Caught + "  " + SimText.Clock(snapshot.Seconds);
        }
    }
}
