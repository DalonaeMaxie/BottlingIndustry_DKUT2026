using System;
using UnityEngine;
using realvirtual;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(Drive))]
public class DriveSignalConnector : MonoBehaviour
{
    [Header("PLC Output Signals (%Q)")]
    public PLCOutputBool jogForwardSignal;
    public PLCOutputBool jogBackwardSignal;

    [Header("Direct Shared-Memory Fallback")]
    public UnityPLCConnectBridge bridge;
    public bool useDirectBridgeFallback = true;
    public int fallbackByteOffset = 0;
    [Range(0, 7)] public int fallbackBitIndex = 0; // 0 = Q0.0 (Conveyor_2), 1 = Q0.1 (Conveyor_1)

    private Drive _drive;
    private bool _lastForward;
    private bool _hasLoggedFirstState;

    private void Awake()
    {
        _drive = GetComponent<Drive>();
    }

    private void Start()
    {
        if (bridge == null)
            bridge = FindAnyObjectByType<UnityPLCConnectBridge>();

        TransportSurface surface = GetComponentInChildren<TransportSurface>();
        string sigName = jogForwardSignal != null ? jogForwardSignal.name : "None (using bridge fallback)";

        Debug.Log($"[DriveSignalConnector] Ready on '{name}' | Drive={(_drive != null)} | Signal={sigName} | Q{fallbackByteOffset}.{fallbackBitIndex} | TransportSurface={(surface != null)}");
    }

    private void FixedUpdate()
    {
        if (_drive == null) return;

        bool fwd = jogForwardSignal != null && Convert.ToBoolean(jogForwardSignal.GetValue());
        if (!fwd && useDirectBridgeFallback && bridge != null)
            fwd = bridge.ReadOutputBit(fallbackByteOffset, fallbackBitIndex);

        bool bwd = jogBackwardSignal != null && Convert.ToBoolean(jogBackwardSignal.GetValue());

        _drive.JogForward = fwd;
        _drive.JogBackward = bwd;

        if (!_hasLoggedFirstState || fwd != _lastForward)
        {
            _hasLoggedFirstState = true;
            _lastForward = fwd;
            Debug.Log($"[DriveSignalConnector] '{name}' JogForward = {fwd} (TargetSpeed={_drive.TargetSpeed})");

            if (fwd)
            {
                foreach (Rigidbody rb in FindObjectsByType<Rigidbody>())
                {
                    if (!rb.isKinematic)
                        rb.WakeUp();
                }
            }
        }
    }
}