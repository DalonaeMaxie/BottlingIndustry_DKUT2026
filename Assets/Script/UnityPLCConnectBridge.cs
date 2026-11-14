using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Application = UnityEngine.Application; // Resolves System.Net.Mime.MediaTypeNames.Application collision

public class UnityPLCConnectBridge : MonoBehaviour
{
    [System.Serializable]
    public struct TagDefinition
    {
        public string tagName;
        public enum DataType { Bit, Byte, Int16, Float32 }
        public DataType dataType;
        public enum Direction { InputToPLC, OutputFromPLC }
        public Direction direction;
        public int byteOffset;
        public int bitIndex;
    }

    [Header("PLC Connection Settings")]
    public string instanceName = "AutoDiscover";
    public string shmName = "PLCSimUnity_SharedMemory";
    [Tooltip("Optional full path to PLCSimUnity.exe if not placed inside the Unity project folder.")]
    public string customExePath = "";
    public int inputByteCount = 64;
    public int outputByteCount = 64;
    public float serviceStartDelay = 0.5f;

    [Header("Status Diagnostics")]
    public bool serviceExeFound;
    public bool serviceAlive;
    public byte statusByte;
    public byte plcOperatingState;
    public byte couplerLoopStage;
    public bool isConnected;

    [Header("Tag Mapping Configuration")]
    public List<TagDefinition> tagMappings = new List<TagDefinition>();

    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _view;
    private Process _connectProcess;

    private byte[] _inputBuffer;
    private byte[] _outputBuffer;
    private Dictionary<string, TagDefinition> _tagDictionary = new Dictionary<string, TagDefinition>();

    private byte _lastHeartbeat;
    private float _heartbeatTimer;
    private bool _loggedAccessWarning = false;

    // Buffer Memory Layout Constants
    private const int HeaderOffset = 0;
    private const int UnityHeartbeatOffset = 100;
    private const int InputBufferOffset = 128; // Header reserves 128 bytes

    private void Awake()
    {
        _inputBuffer = new byte[inputByteCount];
        _outputBuffer = new byte[outputByteCount];
        RebuildTagDictionary();
    }

    private void Start()
    {
        LaunchService();
        StartCoroutine(OpenSharedMemoryRoutine());
    }

    public void RebuildTagDictionary()
    {
        _tagDictionary.Clear();
        foreach (var tag in tagMappings)
        {
            if (!string.IsNullOrEmpty(tag.tagName) && !_tagDictionary.ContainsKey(tag.tagName))
            {
                _tagDictionary.Add(tag.tagName, tag);
            }
        }
    }

    public void LaunchService()
    {
        // 1. Check if PLCSimUnity is already running
        Process[] existing = Process.GetProcessesByName("PLCSimUnity");
        if (existing.Length > 0)
        {
            _connectProcess = existing[0];
            serviceExeFound = true;
            return;
        }

        // 2. Check explicit custom path first, then search standard project output paths
        string exePath = null;
        if (!string.IsNullOrEmpty(customExePath) && File.Exists(customExePath))
        {
            exePath = customExePath;
        }
        else
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] possiblePaths = new string[]
            {
                Path.Combine(projectRoot, "PLCSimUnity", "PLCSimUnity.exe"),
                Path.Combine(projectRoot, "PLCSimUnity", "bin", "x64", "Debug", "PLCSimUnity.exe"),
                Path.Combine(projectRoot, "PLCSimUnity", "bin", "x64", "Release", "PLCSimUnity.exe"),
                Path.Combine(projectRoot, "PLCSimUnity", "bin", "Debug", "PLCSimUnity.exe"),
                Path.Combine(projectRoot, "PLCSimUnity", "bin", "Release", "PLCSimUnity.exe"),
                Path.Combine(projectRoot, "PLCSimUnity", "bin", "Debug", "net8.0-windows", "PLCSimUnity.exe"),
                Path.Combine(projectRoot, "PLCSimUnity", "bin", "Release", "net8.0-windows", "PLCSimUnity.exe"),
                Path.Combine(projectRoot, "PLCSimUnity.exe"),
                Path.Combine(Application.dataPath, "PLCSimUnity.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    exePath = path;
                    break;
                }
            }
        }

        serviceExeFound = exePath != null;

        if (!serviceExeFound)
        {
            Debug.LogError("[UnityPLCConnectBridge] PLCSimUnity.exe not found. Start PLCSimUnity.exe manually or set 'Custom Exe Path' in the Inspector.");
            return;
        }

        string args = $"--instance \"{instanceName}\" --inputs {inputByteCount} --outputs {outputByteCount} --shm \"{shmName}\"";

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)
        };

        try
        {
            _connectProcess = Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityPLCConnectBridge] Failed to launch service process: {ex.Message}");
        }
    }

    private IEnumerator OpenSharedMemoryRoutine()
    {
        yield return new WaitForSecondsRealtime(serviceStartDelay);

        while (_mmf == null)
        {
            bool openedSuccessfully = false;
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(shmName);
                _view = _mmf.CreateViewAccessor();
                Debug.Log($"[UnityPLCConnectBridge] Shared Memory Connected: '{shmName}'");
                openedSuccessfully = true;
            }
            catch (UnauthorizedAccessException)
            {
                if (!_loggedAccessWarning)
                {
                    Debug.LogWarning($"[UnityPLCConnectBridge] Access denied opening '{shmName}'. Ensure Program.cs creates the MemoryMappedFile with WorldSid FullControl permissions.");
                    _loggedAccessWarning = true;
                }
            }
            catch
            {
                // Background service is still initializing memory mapped file
            }

            if (!openedSuccessfully)
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
    }

    private bool IsServiceProcessExited()
    {
        if (_connectProcess == null) return false;

        try
        {
            return _connectProcess.HasExited;
        }
        catch (Win32Exception)
        {
            // Expected when PLCSimUnity.exe runs as Administrator and Unity Editor runs as standard user
            return Process.GetProcessesByName("PLCSimUnity").Length == 0;
        }
        catch
        {
            return false;
        }
    }

    private void Update()
    {
        if (IsServiceProcessExited())
        {
            Debug.LogWarning("[UnityPLCConnectBridge] Service process exited. Relaunching...");
            _connectProcess = null;
            LaunchService();
        }

        if (_view == null || !_view.CanRead) return;

        try
        {
            // Write rolling Unity Heartbeat tick to Header Offset 100 for Dashboard Form
            _view.Write(UnityHeartbeatOffset, Environment.TickCount);

            // Read Telemetry Header
            statusByte = _view.ReadByte(HeaderOffset + 0);
            plcOperatingState = _view.ReadByte(HeaderOffset + 1);
            byte currentHeartbeat = _view.ReadByte(HeaderOffset + 2);
            couplerLoopStage = _view.ReadByte(HeaderOffset + 3);

            if (currentHeartbeat != _lastHeartbeat)
            {
                _lastHeartbeat = currentHeartbeat;
                _heartbeatTimer = 0f;
                serviceAlive = true;
            }
            else
            {
                _heartbeatTimer += Time.unscaledDeltaTime;
                if (_heartbeatTimer > 3.0f) serviceAlive = false;
            }

            // Status Byte 4 = Connected
            isConnected = serviceAlive && statusByte == 4;

            if (!isConnected) return;

            // Perform synchronous I/O buffer transfers automatically once connected
            int outputBufferOffset = InputBufferOffset + inputByteCount;
            _view.WriteArray(InputBufferOffset, _inputBuffer, 0, inputByteCount);
            _view.ReadArray(outputBufferOffset, _outputBuffer, 0, outputByteCount);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityPLCConnectBridge] SHM Exchange Error: {ex.Message}");
        }
    }

    // Retained for compatibility if any external UI script invokes it
    public void BeginSimulation() { }

    // --- Core Low-Level Accessor API ---

    public void SetInputBit(int byteOffset, int bitIndex, bool value)
    {
        if (byteOffset < 0 || byteOffset >= inputByteCount || bitIndex < 0 || bitIndex > 7) return;
        if (value)
            _inputBuffer[byteOffset] |= (byte)(1 << bitIndex);
        else
            _inputBuffer[byteOffset] &= (byte)~(1 << bitIndex);
    }

    public bool GetOutputBit(int byteOffset, int bitIndex)
    {
        if (byteOffset < 0 || byteOffset >= outputByteCount || bitIndex < 0 || bitIndex > 7) return false;
        return (_outputBuffer[byteOffset] & (1 << bitIndex)) != 0;
    }

    public void SetInputFloat(int byteOffset, float value)
    {
        if (byteOffset < 0 || byteOffset + 3 >= inputByteCount) return;
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, _inputBuffer, byteOffset, 4);
    }

    public float GetOutputFloat(int byteOffset)
    {
        if (byteOffset < 0 || byteOffset + 3 >= outputByteCount) return 0f;
        return BitConverter.ToSingle(_outputBuffer, byteOffset);
    }

    // --- Dynamic Tag API ---

    public void SetTagBool(string tagName, bool value)
    {
        if (_tagDictionary.TryGetValue(tagName, out var tag) && tag.direction == TagDefinition.Direction.InputToPLC)
        {
            SetInputBit(tag.byteOffset, tag.bitIndex, value);
        }
    }

    public bool GetTagBool(string tagName)
    {
        if (_tagDictionary.TryGetValue(tagName, out var tag) && tag.direction == TagDefinition.Direction.OutputFromPLC)
        {
            return GetOutputBit(tag.byteOffset, tag.bitIndex);
        }
        return false;
    }

    private void OnDestroy()
    {
        _view?.Dispose();
        _mmf?.Dispose();
    }
}