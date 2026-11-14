using System;
using System.Collections.Generic;
using UnityEngine;
using realvirtual;

public class RealVirtualPLCSimAdapter : MonoBehaviour
{
    [Serializable]
    public struct BoolOutputBinding // PLC (%Q) -> realvirtual Actuator Signal
    {
        public PLCOutputBool signal;
        public int byteOffset;
        [Range(0, 7)] public int bitIndex;
    }

    [Serializable]
    public struct BoolInputBinding // realvirtual Sensor Signal -> PLC (%I)
    {
        public PLCInputBool signal;
        public int byteOffset;
        [Range(0, 7)] public int bitIndex;
    }

    [Serializable]
    public struct FloatOutputBinding // PLC (%QD) -> realvirtual Analog Drive Signal
    {
        public PLCOutputFloat signal;
        public int byteOffset;
    }

    [Serializable]
    public struct FloatInputBinding // realvirtual Analog Sensor -> PLC (%ID)
    {
        public PLCInputFloat signal;
        public int byteOffset;
    }

    [Header("Bridge Reference")]
    public UnityPLCConnectBridge bridge;

    [Header("PLC Outputs -> realvirtual Actuators (%Q)")]
    public List<BoolOutputBinding> plcBoolOutputs = new List<BoolOutputBinding>();
    public List<FloatOutputBinding> plcFloatOutputs = new List<FloatOutputBinding>();

    [Header("realvirtual Sensors -> PLC Inputs (%I)")]
    public List<BoolInputBinding> plcBoolInputs = new List<BoolInputBinding>();
    public List<FloatInputBinding> plcFloatInputs = new List<FloatInputBinding>();

    private bool _lastConnectedState = false;

    private void Awake()
    {
        if (bridge == null)
            bridge = FindAnyObjectByType<UnityPLCConnectBridge>();
    }

    private void FixedUpdate()
    {
        if (bridge == null) return;

        // Update signal connection badges in the realvirtual Hierarchy
        if (bridge.isConnected != _lastConnectedState)
        {
            _lastConnectedState = bridge.isConnected;
            UpdateSignalConnectionStatus(_lastConnectedState);
        }

        if (!bridge.isConnected) return;

        // 1. Transfer PLC %Q Boolean Outputs -> realvirtual PLCOutputBool
        for (int i = 0; i < plcBoolOutputs.Count; i++)
        {
            var item = plcBoolOutputs[i];
            if (item.signal != null)
            {
                bool val = bridge.GetOutputBit(item.byteOffset, item.bitIndex);
                item.signal.SetValue(val);
            }
        }

        // 2. Transfer PLC %QD Float Outputs -> realvirtual PLCOutputFloat
        for (int i = 0; i < plcFloatOutputs.Count; i++)
        {
            var item = plcFloatOutputs[i];
            if (item.signal != null)
            {
                float val = bridge.GetOutputFloat(item.byteOffset);
                item.signal.SetValue(val);
            }
        }

        // 3. Transfer realvirtual PLCInputBool -> PLC %I Boolean Inputs
        for (int i = 0; i < plcBoolInputs.Count; i++)
        {
            var item = plcBoolInputs[i];
            if (item.signal != null)
            {
                bool val = Convert.ToBoolean(item.signal.GetValue());
                bridge.SetInputBit(item.byteOffset, item.bitIndex, val);
            }
        }

        // 4. Transfer realvirtual PLCInputFloat -> PLC %ID Float Inputs
        for (int i = 0; i < plcFloatInputs.Count; i++)
        {
            var item = plcFloatInputs[i];
            if (item.signal != null)
            {
                float val = Convert.ToSingle(item.signal.GetValue());
                bridge.SetInputFloat(item.byteOffset, val);
            }
        }
    }

    private void UpdateSignalConnectionStatus(bool connected)
    {
        foreach (var item in plcBoolOutputs) item.signal?.SetStatusConnected(connected);
        foreach (var item in plcFloatOutputs) item.signal?.SetStatusConnected(connected);
        foreach (var item in plcBoolInputs) item.signal?.SetStatusConnected(connected);
        foreach (var item in plcFloatInputs) item.signal?.SetStatusConnected(connected);
    }
}