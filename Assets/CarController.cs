using System;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class Wheel
{
    [NonSerialized] public Transform transform;
    [NonSerialized] public Vector3 velocity;
    public float height;
    public float maxCompress;
    public bool aired;
}

public class CarController : MonoBehaviour
{
    public Rigidbody rb;

    public Transform frt;
    public Transform flt;
    public Transform rrt;
    public Transform rlt;

    public Wheel fr;
    public Wheel fl;
    public Wheel rr;
    public Wheel rl;
    
    public int wheels;

    public float springRestDist = 0.38f;
    public float springStrength = 30;
    public float springDamper = 3;
    public float gripStrength = 0.01f;
    public float shockRate = 0.125f;
    public float shockRandom = 0.063f;
    public float compression;

    void Start()
    {
        fr = new () { transform = frt };
        fl = new () { transform = flt };
        rr = new () { transform = rrt };
        rl = new () { transform = rlt };
    }

    void FixedUpdate()
    {
        wheels = 0;
        wheels += ComputeWheel(rb, fr).aired ? 0 : 1;
        wheels += ComputeWheel(rb, fl).aired ? 0 : 1;
        wheels += ComputeWheel(rb, rr).aired ? 0 : 1;
        wheels += ComputeWheel(rb, rl).aired ? 0 : 1;
    }

    Wheel ComputeWheel(Rigidbody car, Wheel wheel)
    {
        var velocity = car.GetPointVelocity(wheel.transform.position);
        var grounded = Physics.Raycast(wheel.transform.position, -wheel.transform.up, out RaycastHit groundray, springRestDist);
        var upsideDown = Vector3.Dot(groundray.normal, -wheel.transform.up) >= 0;

        if (grounded && !upsideDown)
        {
            // Suspension
            var compress = springRestDist - groundray.distance;

            var upVelocity = Vector3.Dot(wheel.transform.up, velocity);

            var upForce = (compress * springStrength) - (upVelocity * (springDamper / 4));

            // Side grip
            var rightVelocity = Vector3.Dot(wheel.transform.right, velocity);

            var rightForce = -rightVelocity * (gripStrength / 4);
            var force = wheel.transform.right * rightForce + wheel.transform.up * upForce * Time.fixedDeltaTime;

            car.AddForceAtPosition(force, wheel.transform.position, ForceMode.Impulse);

            Debug.DrawLine(wheel.transform.position, wheel.transform.position + wheel.transform.up * upForce, Color.green);
            Debug.DrawLine(wheel.transform.position, wheel.transform.position + wheel.transform.right * rightForce, Color.red);
        }

        wheel.velocity = velocity;

        return wheel;
    }
}
