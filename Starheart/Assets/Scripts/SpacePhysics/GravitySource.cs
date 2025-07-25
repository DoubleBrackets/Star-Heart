using FishNet.Object;
using UnityEngine;

namespace SpacePhysics
{
    public class GravitySource : NetworkBehaviour
    {
        public enum GravityTypes
        {
            Planet,
            Heartstar
        }

        [Header("Border Visual")]

        [SerializeField]
        private LineRenderer _radiusLineRenderer;

        [Header("Config")]

        [SerializeField]
        private float _radius;

        [SerializeField]
        private float _gravityAcceleration;

        [SerializeField]
        private GravityTypes _gravityType;

        public Vector2 CenterPosition => transform.position;
        public float Radius => _radius;
        public float GravityAccel => _gravityAcceleration;
        public GravityTypes GravityType => _gravityType;

        public bool IsActive { get; set; } = true;

        private void Awake()
        {
            if (_radiusLineRenderer)
            {
                CreateBorderLine();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(CenterPosition, _radius);
        }

        public override void OnStartNetwork()
        {
            GravityManager.Instance.RegisterGravitySource(this);
        }

        public override void OnStopNetwork()
        {
            GravityManager.Instance.UnregisterGravitySource(this);
        }

        private void CreateBorderLine()
        {
            var positionCount = (int)(_radius * 10);
            _radiusLineRenderer.positionCount = positionCount;
            float anglePerSegment = 360f / (positionCount - 1);

            var positions = new Vector3[positionCount];
            var angle = 0f;

            for (var i = 0; i < positionCount; i++)
            {
                positions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _radius + CenterPosition;
                angle += anglePerSegment * Mathf.Deg2Rad;
            }

            _radiusLineRenderer.SetPositions(positions);
        }
    }
}