using System.Collections;
using System.Collections.Generic;
using Shapes2D;
using Unity.Mathematics;
using UnityEngine;

public class RobotBladeParticle : MonoBehaviour
{
    [SerializeField]
    private GameObject BladeObject;

    [SerializeField]
    private TrailRenderer trail;
    private float myangle = 0;

    [HideInInspector]
    public float BladeLenght = 0;
    private bool isBladeAttack = false;

    private void Start()
    {
        trail.emitting = false;
        trail.startWidth = 0.6f;
        trail.endWidth = 0.0f;
    }

    private void FixedUpdate()
    {
        isBladeAttack = BladeObject.GetComponent<Robot_blade_move>().isBladeSwinging;
        if (isBladeAttack)
        {
            trail.emitting = true;
            float Bladeangle = BladeObject.transform.eulerAngles.z;
            if (270 <= Bladeangle)
                Bladeangle = Bladeangle - 360;
            myangle = Bladeangle <= 90 ? Bladeangle * (4f / 3f) : (4f / 3f) * Bladeangle - 60;
            Vector3 offset = new Vector3(
                BladeLenght * Mathf.Cos(myangle * Mathf.Deg2Rad),
                BladeLenght * Mathf.Sin(myangle * Mathf.Deg2Rad),
                0
            );
            this.transform.position = BladeObject.transform.position + offset;
        }
        else
        {
            trail.emitting = false;
        }
    }
}
