using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class Sample : MonoBehaviour
{

    [SerializeField] private AROcclusionManager _arOcclusionManager;
    [SerializeField] private RawImage _rawImage;

    private void Update()
    {
        _rawImage.texture = _arOcclusionManager.humanDepthTexture;
    }
}
