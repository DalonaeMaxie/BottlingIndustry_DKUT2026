using System;
using System.IO.MemoryMappedFiles;
using UnityEngine;
using Debug = UnityEngine.Debug; // Resolves System.Diagnostics namespace collision

public class WinCCScreenReceiver : MonoBehaviour
{
    [Header("Shared Memory Name")]
    public string shmName = "PLCSimUnity_SharedMemory";

    [Header("Target Renderer")]
    public Renderer screenRenderer;

    [Header("Screen Resolution")]
    public int textureWidth = 1920;
    public int textureHeight = 1080;

    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _view;
    private Texture2D _screenTexture;
    private byte[] _pixelBuffer;
    private int _lastFrameTimestamp = -1;

    private const int FrameDataOffset = 256;

    void Start()
    {
        if (screenRenderer == null)
            screenRenderer = GetComponent<Renderer>();

        _screenTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.BGRA32, false);
        _pixelBuffer = new byte[textureWidth * textureHeight * 4];

        if (screenRenderer != null)
        {
            screenRenderer.material.mainTexture = _screenTexture;
            screenRenderer.material.mainTextureScale = new Vector2(1, -1);
            screenRenderer.material.mainTextureOffset = new Vector2(0, 1);
        }

        ConnectSharedMemory();
    }

    private void ConnectSharedMemory()
    {
        try
        {
            _mmf = MemoryMappedFile.OpenExisting(shmName);
            _view = _mmf.CreateViewAccessor();
            Debug.Log("[WinCCScreenReceiver] Connected to Shared Memory!");
        }
        catch
        {
            // Auto-retry in Update
        }
    }

    void Update()
    {
        if (_view == null)
        {
            if (Time.frameCount % 60 == 0) ConnectSharedMemory();
            return;
        }

        try
        {
            byte status = _view.ReadByte(64);
            int currentTimestamp = _view.ReadInt32(78);

            if (status == 3 && currentTimestamp != _lastFrameTimestamp && currentTimestamp > 0)
            {
                _lastFrameTimestamp = currentTimestamp;
                _view.ReadArray(FrameDataOffset, _pixelBuffer, 0, _pixelBuffer.Length);
                _screenTexture.LoadRawTextureData(_pixelBuffer);
                _screenTexture.Apply(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WinCCScreenReceiver] Frame update error: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        _view?.Dispose();
        _mmf?.Dispose();
    }
}