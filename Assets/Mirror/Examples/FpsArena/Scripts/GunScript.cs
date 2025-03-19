using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunScript : MonoBehaviour
{
    public new string name;

    public int maxDistance = 100;
    public int damage = 40;

    public float cooldownTime = 0.1f;

    public float reloadTime = 1;
    public int maxAmmo = 10;

    int cooldownTicks = 0;
    int reloadTicks = 0;
    int magazine = 0;

    int remainingCooldownTicks = 0;
    int remainingReloadTicks = 0;

    void Start()
    {
        cooldownTicks = Mathf.CeilToInt(cooldownTime / Time.fixedDeltaTime);
        reloadTicks = Mathf.CeilToInt(reloadTime / Time.fixedDeltaTime);
    }


    // Update is called once per frame
    void Update()
    {
    }

    public bool Fire()
    {
        if (CanFire())
        {
            magazine--;
            remainingCooldownTicks = cooldownTicks;
            return true;
        }

        if (magazine == 0)
        {
            //todo start reload
            remainingReloadTicks = reloadTicks;
        }

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
    public bool CanFire() => remainingReloadTicks <= 0 && remainingCooldownTicks <= 0 && magazine > 0;

    public void UpdateState()
    {
        remainingCooldownTicks -= 1;
        remainingReloadTicks -= 1;
    }
}
