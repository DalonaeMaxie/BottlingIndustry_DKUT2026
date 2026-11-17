using System;
using System.Collections.Generic;
using UnityEngine;
using realvirtual;
using Debug = UnityEngine.Debug;

public class RealVirtualPLCSimAdapter : MonoBehaviour
{
    [Serializable]
    public class BoolOutputMapping
    {
        public PLCOutputBool signal;
        public int byteOffset;
        [Range(0, 7)] public int bitIndex;
    }

    [Serializable]
    public class FloatOutputMapping
    {
        public PLCOutputFloat signal;
        public int byteOffset;
    }

    [Serializable]
    public class BoolInputMapping
    {
        public PLCInputBool signal;
        public int byteOffset;
        [Range(0, 7)] public int bitIndex;
    }

    [Header("Bridge Reference")]
    public UnityPLCConnectBridge bridge;

    [Header("PLC Outputs -> realvirtual Actuators (%Q)")]
    public List<BoolOutputMapping> plcBoolOutputs = new List<BoolOutputMapping>();
    public List<FloatOutputMapping> plcFloatOutputs = new List<FloatOutputMapping>();

    [Header("realvirtual Sensors -> PLC Inputs (%I)")]
    public List<BoolInputMapping> plcBoolInputs = new List<BoolInputMapping>();

    private bool[] _lastBoolOutputs;

    private void Awake()
    {
        if (bridge == null)
            bridge = GetComponent<UnityPLCConnectBridge>();
        if (bridge == null)
            bridge = FindAnyObjectByType<UnityPLCConnectBridge>();
    }

    private void Start()
    {
        if (bridge == null)
            bridge = FindAnyObjectByType<UnityPLCConnectBridge>();

        _lastBoolOutputs = new bool[plcBoolOutputs.Count];
        Debug.Log($"[RealVirtualPLCSimAdapter] Initialized | BridgeFound={(bridge != null)} | BoolOutputs={plcBoolOutputs.Count} | BoolInputs={plcBoolInputs.Count}");
    }

    private void FixedUpdate()
    {
        if (bridge == null)
        {
            bridge = FindAnyObjectByType<UnityPLCConnectBridge>();
            if (bridge == null) return;
        }

        if (!bridge.IsConnected)
            return;

        if (_lastBoolOutputs == null || _lastBoolOutputs.Length != plcBoolOutputs.Count)
            _lastBoolOutputs = new bool[plcBoolOutputs.Count];

        // 1. Read PLC Outputs (%Q) -> Drive realvirtual PLCOutputBool signals
        for (int i = 0; i < plcBoolOutputs.Count; i++)
        {
            var item = plcBoolOutputs[i];
            if (item == null || item.signal == null) continue;

            bool plcVal = bridge.ReadOutputBit(item.byteOffset, item.bitIndex);
            item.signal.SetValue(plcVal);

            if (plcVal != _lastBoolOutputs[i])
            {
                _lastBoolOutputs[i] = plcVal;
                Debug.Log($"[RealVirtualPLCSimAdapter] Signal '{item.signal.name}' (Q{item.byteOffset}.{item.bitIndex}) -> {plcVal}");
            }
        }

        for (int i = 0; i < plcFloatOutputs.Count; i++)
        {
            var item = plcFloatOutputs[i];
            if (item == null || item.signal == null) continue;

            float plcFloat = bridge.ReadOutputFloat(item.byteOffset);
            item.signal.SetValue(plcFloat);
        }

        // 2. Read realvirtual Sensors (PLCInputBool) -> Write PLC Inputs (%I)
        for (int i = 0; i < plcBoolInputs.Count; i++)
        {
            var item = plcBoolInputs[i];
            if (item == null || item.signal == null) continue;

            bool sensorVal = Convert.ToBoolean(item.signal.GetValue());
            bridge.WriteInputBit(item.byteOffset, item.bitIndex, sensorVal);
        }
    }
}