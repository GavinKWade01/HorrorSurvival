using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [Header("Gun Parameters")]
    [SerializeField] private float fireRate = 0.1f;
    [SerializeField] private int magSize;
    [SerializeField] private int reservedAmmo;
    [SerializeField] private float aimSmoothing = 10;

    bool canShoot;
    int currentMag;
    int currentReservedAmmo;

    public Vector3 normalLocalPos;
    public Vector3 aimingLocalPos;

    private void Start()
    {
        currentMag = magSize;
        currentReservedAmmo = reservedAmmo;
        canShoot = true;
    }

    private void Update()
    {
        DetermineAim();


        if(Input.GetMouseButton(0) && canShoot && currentMag > 0)
        {
            canShoot = false;
            currentMag--;
            StartCoroutine(Shooting());
        }
        else if (Input.GetKeyDown(KeyCode.R) && currentMag < magSize && reservedAmmo > 0)
        {
            int amountNeeded = magSize - currentMag;
            if (amountNeeded >= currentReservedAmmo)
            {
                currentMag += currentReservedAmmo;
                currentReservedAmmo -= amountNeeded;
            }
            else
            {
                currentMag = magSize;
                reservedAmmo -= amountNeeded;
            }
        }
    }

    void DetermineAim()
    {
        Vector3 target = normalLocalPos;
        if (Input.GetMouseButton(1)) target = aimingLocalPos;

        Vector3 desiredPos = Vector3.Lerp(transform.localPosition, target, Time.deltaTime * aimSmoothing);

        transform.localPosition = desiredPos;

    }

    IEnumerator Shooting()
    {
        yield return new WaitForSeconds(fireRate);
        canShoot = true;
    }

}
