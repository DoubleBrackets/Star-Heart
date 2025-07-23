using UnityEngine;

namespace Utils
{
    public static class ExtensionMethods
    {
        public static Vector2 ProjectOnPlane(this Vector2 vector, Vector2 planeNormal)
        {
            float dot = Vector2.Dot(vector, planeNormal);
            return vector - dot * planeNormal;
        }
    }
}