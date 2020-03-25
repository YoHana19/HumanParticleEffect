using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(Camera))]
public class HumanParticle : MonoBehaviour
{
    public RenderTexture LatestCameraFeedBuffer
    {
        get
        {
            if (_lastDeviceOrientation == DeviceOrientation.Portrait)
            {
                return _colorMapPortrait;
            }
            return _colorMapLandscape;
        }
    }
    
    [SerializeField] private AROcclusionManager _arOcclusionManager;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField] private RenderTexture _positionMapPortrait;
    [SerializeField] private RenderTexture _positionMapLandscape;
    [SerializeField] private RenderTexture _colorMapPortrait;
    [SerializeField] private RenderTexture _colorMapLandscape;
    [SerializeField] private VisualEffect _visualEffect;
    
    private RenderTexture _tempRenderTexture;
    private Camera _camera;
    private DeviceOrientation _lastDeviceOrientation;
    private uint _threadSizeX, _threadSizeY, _threadSizeZ;
    private int _portraitKernel, _landscapeKernel;
    private Matrix4x4 _viewportInv;

    // Compute Shader
    private readonly int PropertyID_CameraPos = Shader.PropertyToID("cameraPos");
    private readonly int PropertyID_Converter = Shader.PropertyToID("converter");
    private readonly int PropertyID_Target = Shader.PropertyToID("target");
    private readonly int PropertyID_Origin = Shader.PropertyToID("origin");
    private readonly int PropertyID_IsWide = Shader.PropertyToID("isWide");
    private readonly int PropertyID_UVFlip = Shader.PropertyToID("uVFlip");
    private readonly int PropertyID_UVMultiplierPortrait = Shader.PropertyToID("uVMultiplierPortrait");
    private readonly int PropertyID_UVMultiplierLandScape = Shader.PropertyToID("uVMultiplierLandScape");

    // Visual Effect Graph
    private readonly int PropertyID_PositionMap = Shader.PropertyToID("PositionMap");
    private readonly int PropertyID_ColorMap = Shader.PropertyToID("ColorMap");

    void Start()
    {
        _camera = GetComponent<Camera>();

        _portraitKernel = _computeShader.FindKernel("Portrait");
        _landscapeKernel = _computeShader.FindKernel("Landscape");
        
        // Init Portrait at first
        _lastDeviceOrientation = DeviceOrientation.Portrait;
        _computeShader.SetInt(PropertyID_IsWide, 0);
    }
    
    void Update()
    {
        var humanDepthTexture = _arOcclusionManager.humanDepthTexture;
        if (humanDepthTexture)
        {
            if (_lastDeviceOrientation != Input.deviceOrientation)
            {
                if (Input.deviceOrientation == DeviceOrientation.LandscapeRight)
                {
                    _computeShader.SetFloat(PropertyID_UVFlip, 0);
                    _computeShader.SetInt(PropertyID_IsWide, 1);
                }
                else if (Input.deviceOrientation == DeviceOrientation.LandscapeLeft)
                {
                    _computeShader.SetFloat(PropertyID_UVFlip, 1);
                    _computeShader.SetInt(PropertyID_IsWide, 1);
                }
                else
                {
                    _computeShader.SetInt(PropertyID_IsWide, 0);
                }
                _lastDeviceOrientation = Input.deviceOrientation;
                InitSetup(humanDepthTexture);
            }
            
            if (_tempRenderTexture)
            {
                _computeShader.SetVector(PropertyID_CameraPos, _camera.transform.position);
                _computeShader.SetMatrix(PropertyID_Converter, GetConverter());
                
                if (_lastDeviceOrientation == DeviceOrientation.Portrait)
                {
                    _computeShader.SetTexture(_portraitKernel, PropertyID_Origin, humanDepthTexture);
                    _computeShader.Dispatch(_portraitKernel, Screen.width / (int) _threadSizeX,
                        Screen.height / (int) _threadSizeY, (int) _threadSizeZ);
                    Graphics.CopyTexture(_tempRenderTexture, _positionMapPortrait);
                }
                else
                {
                    _computeShader.SetTexture(_landscapeKernel, PropertyID_Origin, humanDepthTexture);
                    _computeShader.Dispatch(_landscapeKernel, Screen.width / (int) _threadSizeX,
                        Screen.height / (int) _threadSizeY, (int) _threadSizeZ);
                    Graphics.CopyTexture(_tempRenderTexture, _positionMapLandscape);
                }
            }
            else
            {
                InitSetup(humanDepthTexture);
            }
        }
    }

    private void InitSetup(Texture humanDepthTexture)
    {
        if (_lastDeviceOrientation == DeviceOrientation.Portrait)
        {
            _tempRenderTexture = new RenderTexture(_positionMapPortrait.width, _positionMapPortrait.height, 0, _positionMapPortrait.format) {enableRandomWrite = true};
            _tempRenderTexture.Create();
            _computeShader.SetTexture(_portraitKernel, PropertyID_Target, _tempRenderTexture);
            _computeShader.GetKernelThreadGroupSizes(_portraitKernel, out _threadSizeX, out _threadSizeY, out _threadSizeZ);
            _computeShader.SetFloat(PropertyID_UVMultiplierPortrait, CalculateUVMultiplierPortrait(humanDepthTexture));
            _visualEffect.SetTexture(PropertyID_PositionMap, _positionMapPortrait);
            _visualEffect.SetTexture(PropertyID_ColorMap, _colorMapPortrait);
        }
        else
        {
            _tempRenderTexture = new RenderTexture(_positionMapLandscape.width, _positionMapLandscape.height, 0, _positionMapLandscape.format) {enableRandomWrite = true};
            _tempRenderTexture.Create();
            _computeShader.SetTexture(_landscapeKernel, PropertyID_Target, _tempRenderTexture);
            _computeShader.GetKernelThreadGroupSizes(_landscapeKernel, out _threadSizeX, out _threadSizeY, out _threadSizeZ);
            _computeShader.SetFloat(PropertyID_UVMultiplierLandScape, CalculateUVMultiplierLandScape(humanDepthTexture));
            _visualEffect.SetTexture(PropertyID_PositionMap, _positionMapLandscape);
            _visualEffect.SetTexture(PropertyID_ColorMap, _colorMapLandscape);
        }

        SetViewPortInv();
    }

    private float CalculateUVMultiplierLandScape(Texture textureFromAROcclusionManager)
    {
        float screenAspect = (float) Screen.width / Screen.height;
        float cameraTextureAspect = (float) textureFromAROcclusionManager.width / textureFromAROcclusionManager.height;
        return screenAspect / cameraTextureAspect;
    }
    
    private float CalculateUVMultiplierPortrait(Texture textureFromAROcclusionManager)
    {
        float screenAspect = (float) Screen.height / Screen.width;
        float cameraTextureAspect = (float) textureFromAROcclusionManager.width / textureFromAROcclusionManager.height;
        return screenAspect / cameraTextureAspect;
    }
    
    private void SetViewPortInv()
    {
        _viewportInv = Matrix4x4.identity;
        _viewportInv.m00 = _viewportInv.m03 = Screen.width / 2f;
        _viewportInv.m11 = Screen.height / 2f;
        _viewportInv.m13 = Screen.height / 2f;
        _viewportInv.m22 = (_camera.farClipPlane - _camera.nearClipPlane) / 2f;
        _viewportInv.m23 = (_camera.farClipPlane + _camera.nearClipPlane) / 2f;
        _viewportInv = _viewportInv.inverse;
    }

    private Matrix4x4 GetConverter()
    {
        Matrix4x4 viewMatInv = _camera.worldToCameraMatrix.inverse;
        Matrix4x4 projMatInv = _camera.projectionMatrix.inverse;
        return viewMatInv * projMatInv * _viewportInv;
    }
}
