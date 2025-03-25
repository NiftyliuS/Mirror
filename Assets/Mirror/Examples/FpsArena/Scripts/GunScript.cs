using System;
using System.Collections;
using System.Collections.Generic;
using Mirror.Components.Experimental;
using UnityEngine;

public class GunScript : MonoBehaviour
{
    public new string name;

    public int maxDistance = 100;
    public int damage = 40;

    public float cooldownTime = 0.1f;

    public float recoilY = 5f;
    public float recoilX = 1f;

    public GameObject replica;

    public float reloadTime = 1;
    public byte maxAmmo = 10;

    public float lowerAmount = 0.2f;
    float rotationAngle = 30f;

    int cooldownTicks = 0;
    int reloadTicks = 0;
    int magazine = 0;

    bool isVisible = true;
    float reloadStart = 0;
    Vector3 originalPosition;
    Quaternion originalRotation;

    Vector3 replicaOriginalPosition;
    Quaternion replicaOriginalRotation;

    void Start()
    {
        cooldownTicks = Mathf.CeilToInt(cooldownTime / Time.fixedDeltaTime);
        reloadTicks = Mathf.CeilToInt(reloadTime / Time.fixedDeltaTime);
        magazine = maxAmmo;
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
        replicaOriginalPosition = replica.transform.localPosition;
        replicaOriginalRotation = replica.transform.localRotation;
    }


    // Update is called once per frame
    void Update()
    {
    }

    public bool Fire()
    {
        return false;
    }

    public RaycastHit CheckHit()
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hitInfo, maxDistance))
        {
            Debug.Log("Hit something! pew pew!");
        }

        OnGunShot();
        return hitInfo;
    }

    private void OnGunShot()
    {
    }

    public bool CanReload() => magazine < maxAmmo;
    public bool CanFire(byte ticksSinceFire, byte sinceReload) => magazine > 0 && ticksSinceFire > cooldownTicks && sinceReload > reloadTicks;
    public void ReduceMagazine() => magazine -= magazine > 0 ? 1 : 0;

    public void ReloadMagazine()
    {
        magazine = maxAmmo;
        reloadStart = NetworkTick.CurrentAbsoluteTick;
    }

    public byte GetMagazine() => (byte)magazine;


    public void SetEnabled(bool enabled, bool includeTinyGuns, byte sinceReload)
    {
        if (enabled == isVisible) return;

        reloadStart = NetworkTick.CurrentAbsoluteTick - sinceReload;

        Renderer[] renderers = replica.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers) renderer.enabled = enabled;

        if (includeTinyGuns)
        {
            renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers) renderer.enabled = enabled;
        }

        isVisible = enabled;
    }

    // Fancy reloading animation!
    void LateUpdate()
    {
        // Compute the fraction of the current fixed tick that has elapsed.
        float fractionalTick = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        // Calculate total ticks elapsed since reload start.
        float elapsedTicks = NetworkTick.CurrentAbsoluteTick - reloadStart + fractionalTick;

        if (elapsedTicks < reloadTicks)
        {
            float t = Mathf.Clamp01(elapsedTicks / reloadTicks);
            // Sine easing: at t=0 => 0; t=0.5 => maximum; t=1 => 0.
            float offsetY = -Mathf.Sin(t * Mathf.PI) * lowerAmount;
            transform.localPosition = originalPosition + new Vector3(0, offsetY, 0);
            replica.transform.localPosition = replicaOriginalPosition + new Vector3(0, -offsetY, 0);

            // Rotate around the X axis: negative angle to lower the gun.
            float angle = -Mathf.Sin(t * Mathf.PI) * rotationAngle;
            transform.localRotation = originalRotation * Quaternion.Euler(0, angle, 0);
            replica.transform.localRotation = replicaOriginalRotation * Quaternion.Euler(angle, 0, 0 );
        }
        else
        {
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            replica.transform.localPosition = replicaOriginalPosition;
            replica.transform.localRotation = replicaOriginalRotation;
        }
    }
}
