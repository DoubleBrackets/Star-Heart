using System.Collections.Generic;
using UnityEngine;

namespace SpacePhysics
{
    public class GravityManager : MonoBehaviour
    {
        public struct GravityEffect
        {
            public Vector2 TotalAcceleration;
            public Vector2 PlanetAcceleration;
            public bool InPlanetGravity;
            public bool InGravity => TotalAcceleration != Vector2.zero;
        }

        public static GravityManager Instance { get; private set; }

        private List<GravitySource> gravitySources = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void RegisterGravitySource(GravitySource source)
        {
            if (source == null || gravitySources.Contains(source))
            {
                return;
            }

            gravitySources.Add(source);
        }

        public void UnregisterGravitySource(GravitySource source)
        {
            if (source == null || !gravitySources.Contains(source))
            {
                return;
            }

            gravitySources.Remove(source);
        }

        public GravityEffect CalculateGravity(Vector2 position)
        {
            Vector2 totalGravity = Vector2.zero;
            Vector2 planetGravity = Vector2.zero;

            var inPlanetGravity = false;
            foreach (GravitySource source in gravitySources)
            {
                if (source == null || !source.IsActive)
                {
                    continue;
                }

                Vector2 direction = source.CenterPosition - position;

                float dist = direction.magnitude;
                if (dist <= source.Radius)
                {
                    Vector2 accel = direction.normalized * source.GravityAccel;
                    totalGravity += accel;

                    if (source.GravityType == GravitySource.GravityTypes.Planet)
                    {
                        inPlanetGravity = true;
                        planetGravity += accel;
                    }
                }
            }

            return new GravityEffect
            {
                TotalAcceleration = totalGravity,
                PlanetAcceleration = planetGravity,
                InPlanetGravity = inPlanetGravity
            };
        }
    }
}