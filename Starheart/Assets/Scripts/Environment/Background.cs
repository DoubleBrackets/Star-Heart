using UnityEngine;

public class Background : MonoBehaviour
{
    [SerializeField]
    private Transform _tracker;

    private void LateUpdate()
    {
        transform.position = new Vector3(_tracker.position.x, _tracker.position.y, transform.position.z);
    }
}