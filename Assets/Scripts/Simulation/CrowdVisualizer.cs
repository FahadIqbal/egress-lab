using System.Collections.Generic;
using EgressLab.Decision;
using UnityEngine;
using UnityEngine.AI;

namespace EgressLab.Simulation
{
    /// <summary>
    /// One capsule and one NavMeshAgent per person. The session decides the exit; this class only
    /// walks there and reports the position back so the hazard check uses where they actually are.
    /// </summary>
    public sealed class CrowdVisualizer
    {
        readonly List<Person> _people = new List<Person>();

        public static CrowdVisualizer Spawn(Transform parent, EvacuationSession session)
        {
            var view = new CrowdVisualizer();
            var root = new GameObject("Crowd");
            root.transform.SetParent(parent, false);
            IReadOnlyList<AgentState> agents = session.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                view._people.Add(CreatePerson(root.transform, agents[i], session.Config.Mode));
            }

            return view;
        }

        public void SnapToNavMesh(EvacuationSession session)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Vector3 guessed = person.Transform.position;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(guessed, out hit, 3f, NavMesh.AllAreas))
                {
                    person.Agent.Warp(hit.position);
                }

                AgentState agent = session.Agents[person.Id];
                agent.Position = new WorldPos(person.Transform.position.x, person.Transform.position.z);
            }
        }

        public void PushPositions(EvacuationSession session)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                if (person.Gone) continue;
                AgentState agent = session.Agents[person.Id];
                if (agent.Status != AgentStatus.Moving) continue;
                agent.Position = new WorldPos(person.Transform.position.x, person.Transform.position.z);
            }
        }

        public void Pull(EvacuationSession session)
        {
            Material moving = session.Config.Mode == RoutingMode.DecisionSupport
                ? Palette.CyanMaterial
                : Palette.AmberMaterial;

            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                if (person.Gone) continue;
                AgentState agent = session.Agents[person.Id];
                if (agent.Status == AgentStatus.Evacuated)
                {
                    person.Gone = true;
                    person.Agent.enabled = false;
                    person.Transform.gameObject.SetActive(false);
                    continue;
                }

                if (agent.Status == AgentStatus.Caught)
                {
                    person.Renderer.sharedMaterial = Palette.CaughtMaterial;
                    if (person.Hat != null) person.Hat.sharedMaterial = Palette.CaughtMaterial;
                    person.Fall = Mathf.MoveTowards(person.Fall, 1f, Time.deltaTime * 2.5f);
                    Vector3 euler = person.Transform.eulerAngles;
                    person.Transform.rotation = Quaternion.Euler(Mathf.Lerp(0f, 80f, person.Fall), euler.y, 0f);
                    if (person.Agent.enabled)
                    {
                        person.Agent.ResetPath();
                        person.Agent.enabled = false;
                    }

                    continue;
                }

                person.Renderer.sharedMaterial = moving;
                if (person.Hat != null) person.Hat.sharedMaterial = moving;
                if (!person.Agent.isOnNavMesh) continue;
                person.Agent.speed = session.SpeedFor(agent);
                ExitState exit = Find(session, agent.ExitId);
                if (exit == null) continue;
                if (person.ExitId != exit.Id)
                {
                    person.ExitId = exit.Id;
                    person.Agent.SetDestination(new Vector3(exit.Arrival.X, person.Transform.position.y, exit.Arrival.Z));
                }
            }
        }

        static ExitState Find(EvacuationSession session, string id)
        {
            if (id == null) return null;
            IReadOnlyList<ExitState> exits = session.Exits;
            for (int i = 0; i < exits.Count; i++)
            {
                if (exits[i].Id == id) return exits[i];
            }

            return null;
        }

        static Person CreatePerson(Transform parent, AgentState agent, RoutingMode mode)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Person " + agent.Id;
            go.transform.SetParent(parent, false);
            // Default capsule is 2 m tall with its pivot in the middle. Scale it down to about 1 m.
            go.transform.localScale = new Vector3(0.42f, 0.52f, 0.42f);
            go.transform.position = new Vector3(agent.Position.X, 0.52f, agent.Position.Z);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var nav = go.AddComponent<NavMeshAgent>();
            nav.radius = 0.24f;
            nav.height = 1.04f;
            nav.baseOffset = 0.52f;
            nav.speed = agent.BaseSpeed;
            nav.acceleration = 16f;
            nav.angularSpeed = 480f;
            nav.stoppingDistance = 0.4f;
            nav.autoBraking = true;
            nav.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            nav.avoidancePriority = agent.Id % 50;

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mode == RoutingMode.DecisionSupport ? Palette.CyanMaterial : Palette.AmberMaterial;

            var hatGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hatGo.name = "Hat";
            Object.DestroyImmediate(hatGo.GetComponent<Collider>());
            hatGo.transform.SetParent(go.transform, false);
            hatGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            hatGo.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);
            var hat = hatGo.GetComponent<Renderer>();
            hat.sharedMaterial = renderer.sharedMaterial;

            return new Person
            {
                Id = agent.Id,
                Transform = go.transform,
                Agent = nav,
                Renderer = renderer,
                Hat = hat
            };
        }

        sealed class Person
        {
            public int Id;
            public Transform Transform;
            public NavMeshAgent Agent;
            public Renderer Renderer;
            public Renderer Hat;
            public string ExitId;
            public float Fall;
            public bool Gone;
        }
    }
}
