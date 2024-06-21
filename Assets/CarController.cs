using System;
using System.Linq;
using UnityEngine;

// TODO: everything affects tire grip even stearing out of curves
// https://youtu.be/azd287vwmQY?t=859

[Serializable]
public class Wheel
{
    [NonSerialized] public Transform transform;
    [NonSerialized] public float velocity;
    public float height;
    public float maxCompress;
    public bool grounded;
    public Vector3 groundPoint;
}

[Serializable]
public class EngineState
{
    [Header("Config")]
    public float revLimit = 8500;
    public float revRecover = 2000;
    public float revSpeed = 1000;
    public float revIdle = 850;
    public float throttleMin = 0.2f;

    [Header("Runtime")]
    public float enginePower;
    public float engineBrake;
    public float throttle;
    public float revs;

    public bool IsRevingUp()
    {
        return enginePower > engineBrake;
    }

    public bool IsRevLimiting()
    {
        bool isHittingLimiter = revs >= revLimit;
        //bool isRecovering = revs > revLimit - revRecover;

        return isHittingLimiter;
    }
}

public class CarController : MonoBehaviour
{
    public Rigidbody body;

    public Transform frt;
    public Transform flt;
    public Transform rrt;
    public Transform rlt;

    public Wheel fr;
    public Wheel fl;
    public Wheel rr;
    public Wheel rl;

    public EngineState engine;

    public int wheels;

    public float springRestDist = 0.38f;
    public float springStrength = 30;
    public float springDamper = 3;
    public float gripStrength = 0.01f;
    public float shockRate = 0.125f;
    public float shockRandom = 0.063f;
    public float compression;

    public float steer;
    public float maxsteer;
    public float steerresponse;
    public float turnamount;
    public float countersteerRate;

    [Header("System")]
    public int gear;
    public float throttle;

    [Header("Tuning")]
    public int[] ratios = { 50, 80, 120, 163, 200, 240 };
    public Wheel[] drivingWheels;

    void Start()
    {
        fr = new() { transform = frt };
        fl = new() { transform = flt };
        rr = new() { transform = rrt };
        rl = new() { transform = rlt };
    }

    void Update()
    {
        var isPressingGas = Input.GetAxisRaw("Vertical") > 0;
        var isPressingBrakes = Input.GetAxisRaw("Jump") > 0;
        var vertical = Input.GetAxisRaw("Vertical");
        var horizontal = Input.GetAxisRaw("Horizontal");

        #region Steering 
        var decay = body.velocity.z < 0 ? 0 : body.velocity.z;
        var decay2 = decay > 1 ? 1 : decay;

        decay *= decay2;

        var std = Math.Abs(steer - horizontal);
        steer -= (steer - horizontal) / (std * (10 / Time.deltaTime) + 1);
        steerresponse -= (steerresponse - horizontal) / (std * (10 / 2) + 1);
        turnamount = Math.Abs(steer);

        maxsteer = 1 / ((decay / 10) + 1);
        var turn = Math.Clamp(steerresponse / ((decay / 10) + 1), -maxsteer, maxsteer);

        turn *= turnamount;

        if (body.velocity.z < 0)
            turn *= 1.5f;

        fl.transform.localEulerAngles = fl.transform.up * -turn * 40;
        fr.transform.localEulerAngles = fr.transform.up * -turn * 40;
        #endregion

        if (isPressingBrakes && !isPressingGas && body.velocity.magnitude < 5)
        {
            gear = -1;
        }

        throttle = isPressingGas ? 1 : 0;

        ComputeEngine(throttle, engine);

        var currentPower = ComputeGearBox(gear, ratios, drivingWheels, engine);

        AnimateWheel(fl, animFlw);
        AnimateWheel(fr, animFrw);
        AnimateWheel(rl, animRlw);
        AnimateWheel(rr, animRrw);

        ComputeSounds(engine, new[] { fl, fr, rr, rl });
    }

    void FixedUpdate()
    {
        wheels = 0;
        wheels += ComputeWheel(body, fr).grounded ? 0 : 1;
        wheels += ComputeWheel(body, fl).grounded ? 0 : 1;
        wheels += ComputeWheel(body, rr).grounded ? 0 : 1;
        wheels += ComputeWheel(body, rl).grounded ? 0 : 1;        
    }

    void ComputeEngine(float throttle, EngineState engine)
    {
        bool isHittingLimiter = engine.revs >= engine.revLimit;
        bool isRecovering = engine.revs > engine.revLimit - engine.revRecover;
        bool isUnderIdle = engine.revs < engine.revIdle;
        bool isPressingGas = throttle > 0;

        var targetRev = isPressingGas ? engine.revLimit : engine.revIdle;

        engine.revs += Math.Sign(targetRev - engine.revs) * engine.revSpeed;

        // add clutch feedback to simulate the wobble between gears
        // possibly with a sign value to modulate revs
        // https://youtu.be/Jj0HQw0ZDOU?t=602
    }

    float ComputeGearBox(int gear, int[] ratios, Wheel[] drivingWeels, EngineState engine)
    {
        var currentRatio = 0f;
        var currentPower = 0f;

        if (gear > -1)
        {
            currentRatio = gear > 0 ? ratios[gear - 1] : ratios[0];
            currentPower = (engine.enginePower / currentRatio * engine.throttle) - (engine.engineBrake / currentRatio * -engine.throttle);
        }
        else
        {
            currentRatio = 0;
        }

        // We do drive train load distribution here, RWD, FWD, AWD, or any.
        var wheelsVelocity = drivingWeels.Sum(wheel => wheel.velocity) / drivingWeels.Length;

        if (gear <= -1)
        {
            wheelsVelocity = -wheelsVelocity;
        }

        if (gear != 0)
        {
            var targrev = wheelsVelocity / currentRatio;

        }

        return currentPower;
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

            var upForce = (compress * springStrength) - (upVelocity * springDamper);

            // Side grip
            var rightVelocity = Vector3.Dot(wheel.transform.right, velocity);

            var rightForce = -rightVelocity * gripStrength;

            car.AddForceAtPosition(Time.fixedDeltaTime * wheel.transform.right * rightForce, wheel.transform.position, ForceMode.Impulse);
            car.AddForceAtPosition(Time.fixedDeltaTime * wheel.transform.up * upForce, wheel.transform.position, ForceMode.Impulse);

            Debug.DrawLine(wheel.transform.position, wheel.transform.position + wheel.transform.up * upForce, Color.green);
            Debug.DrawLine(wheel.transform.position, wheel.transform.position + wheel.transform.right * rightForce, Color.red);
        }

        wheel.groundPoint = grounded ? groundray.point : wheel.transform.position;
        wheel.velocity = Vector3.Dot(wheel.transform.forward, velocity);
        wheel.grounded = grounded;

        return wheel;
    }

    [Header("Animation")]
    public Transform animFlw;
    public Transform animFrw;
    public Transform animRlw;
    public Transform animRrw;

    public float offset = 0.3f;
    void AnimateWheel(Wheel wheel, Transform mesh)
    {
        mesh.position = wheel.grounded ? wheel.groundPoint + Vector3.up * offset : wheel.groundPoint;
        mesh.Rotate(wheel.velocity * 3, 0, 0);
    }

    [Header("Sounds")]
    public AudioSource highon;
    public AudioSource highoff;
    public AudioSource limiter;
    public AudioSource brake;
    public AudioSource peel;

    void ComputeSounds(EngineState engine, Wheel[] wheels)
    {
        if (engine.revs >= engine.revLimit - 1000)
        {
            highon.Stop();
            highoff.Stop();

            if (!limiter.isPlaying)
            {
                limiter.Play();
            }
        }
        else
        {
            limiter.Stop();

            if (engine.IsRevingUp())
            {
                highoff.Stop();

                highon.pitch = engine.revs * 0.15f * 1e-3f;

                if (!highon.isPlaying)
                {
                    highon.Play();
                }
            }
            else
            {
                highon.Stop();

                highoff.pitch = engine.revs / 6.53f * 1e-3f;

                if (!highoff.isPlaying)
                {
                    highoff.Play();
                }
            }
        }
    }
}
