using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public Transform cameraPosition;
    Camera camera;
    void Awake()
    {
        camera = GetComponent<Camera>();
    }

    public void setFov(float newFov)
    {
        if (camera != null)
            camera.fieldOfView = newFov;
    }

    void LateUpdate()
    {
        if (cameraPosition == null)
            return;
    }
}
