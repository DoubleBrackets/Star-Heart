using System.Collections.Generic;
using UnityEngine;

namespace SpacePhysics
{
    public class GravityManager : MonoBehaviour
    {
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

        public Vector2 CalculateGravity(Vector2 position)
        {
            Vector2 totalGravity = Vector2.zero;

            foreach (GravitySource source in gravitySources)
            {
                if (source == null)
                {
                    continue;
                }

                Vector2 direction = source.CenterPosition - position;

                float dist = direction.magnitude;
                if (dist <= source.Radius)
                {
                    totalGravity += direction.normalized * source.GravityAccel;
                }
            }

            return totalGravity;
        }
    }
}