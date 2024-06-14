using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarCamera : MonoBehaviour
{
    public Transform car;
    public Transform cam;
    public float followSpeed;
    public float lookAtSpeed;
    public float right;
    public float up;

    void FixedUpdate()
    {
        var position = (car.position - transform.position) + (car.forward * -right) + (car.up * up);
        transform.Translate(position * followSpeed * Time.fixedDeltaTime);
        cam.LookAt(car.position);
    }
}
