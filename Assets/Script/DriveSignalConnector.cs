using System;
using UnityEngine;
using realvirtual;

[RequireComponent(typeof(Drive))]
public class DriveSignalConnector : MonoBehaviour
{
    [Header("PLC Output Signals (%Q)")]
    public PLCOutputBool jogForwardSignal;
    public PLCOutputBool jogBackwardSignal;

    private Drive _drive;

    private void Awake()
    {
        _drive = GetComponent<Drive>();
    }

    private void FixedUpdate()
    {
        if (_drive == null) return;

        if (jogForwardSignal != null)
            _drive.JogForward = Convert.ToBoolean(jogForwardSignal.GetValue());

        if (jogBackwardSignal != null)
            _drive.JogBackward = Convert.ToBoolean(jogBackwardSignal.GetValue());
    }
}