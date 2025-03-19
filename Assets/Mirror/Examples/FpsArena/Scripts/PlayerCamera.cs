using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public Transform cameraPosition;


    void Start()
    {
        // Get reference to main camera
    }

    void LateUpdate()
    {
        if (cameraPosition == null)
            return;
    }
}
