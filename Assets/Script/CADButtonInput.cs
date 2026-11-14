using UnityEngine;
using realvirtual;

[RequireComponent(typeof(Collider))]
public class CADButtonInput : MonoBehaviour
{
    [Header("PLC Input Signal (%I)")]
    public PLCInputBool stateSignal;

    [Header("Button Behavior")]
    public bool toggle = false;
    public bool activeOnStart = false;
    public float pulseDuration = 0.5f;

    private bool _currentState;
    private float _releaseTime;

    private void Start()
    {
        _currentState = activeOnStart;
        if (stateSignal != null)
            stateSignal.SetValue(_currentState);
    }

    private void Update()
    {
        if (!toggle && _currentState && Time.time >= _releaseTime)
        {
            _currentState = false;
            if (stateSignal != null)
                stateSignal.SetValue(false);
        }
    }

    private void OnMouseDown()
    {
        if (toggle)
        {
            _currentState = !_currentState;
        }
        else
        {
            _currentState = true;
            _releaseTime = Time.time + pulseDuration;
        }

        if (stateSignal != null)
            stateSignal.SetValue(_currentState);
    }
}