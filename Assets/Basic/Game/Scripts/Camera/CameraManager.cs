using System;
using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] public CameraState CameraState;
    [SerializeField] private CinemachineCamera _fpscamera;
    [SerializeField] private float _clampedHorizontalAngle = 45f;
    [SerializeField] private Vector2 _clampedVerticalRange = new Vector2(-45f, 45f);
    [SerializeField] private CinemachineCamera _tpsCamera;
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private bool _instantPerspectiveSwitch = true;

    private CinemachinePanTilt _panTilt;
    private Vector2 _defaultPanRange;
    private Vector2 _defaultTiltRange;
    private bool _defaultPanWrap;
    public Action OnChangePerspective;

    private void Start()
    {
        _inputManager.OnChangePOV += SwitchCamera;
    }
    private void OnDestroy()
    {
        _inputManager.OnChangePOV -= SwitchCamera;
    }
    public void SetTPSFieldOfView(float fov)
    {
        _tpsCamera.Lens.FieldOfView = fov;
    }

    private void Awake()
    {
        if (_fpscamera != null)
        {
            _panTilt = _fpscamera.GetComponent<CinemachinePanTilt>();
        }

        if (_panTilt != null)
        {
            _defaultPanRange = _panTilt.PanAxis.Range;
            _defaultPanWrap = _panTilt.PanAxis.Wrap;
            _defaultTiltRange = _panTilt.TiltAxis.Range;
        }

        if (_instantPerspectiveSwitch)
        {
            ApplyInstantBlend();
        }
    }

    private void ApplyInstantBlend()
    {
        CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
        if (brain == null)
        {
            return;
        }
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
    }

    public void SetFPSClampedCamera(bool isClamped, Vector3 playerRotation)
    {
        if (_panTilt == null)
        {
            return;
        }

        if (isClamped)
        {
            float center = _panTilt.PanAxis.Value + Mathf.DeltaAngle(_panTilt.PanAxis.Value, playerRotation.y);
            float min = center - _clampedHorizontalAngle;
            float max = center + _clampedHorizontalAngle;

            _panTilt.PanAxis.Wrap = false;
            _panTilt.PanAxis.Center = center;
            _panTilt.PanAxis.Range = new Vector2(min, max);
            _panTilt.PanAxis.Value = Mathf.Clamp(_panTilt.PanAxis.Value, min, max);

            _panTilt.TiltAxis.Range = _clampedVerticalRange;
            _panTilt.TiltAxis.Value = Mathf.Clamp(_panTilt.TiltAxis.Value, _clampedVerticalRange.x, _clampedVerticalRange.y);
        }
        else
        {
            _panTilt.PanAxis.Wrap = _defaultPanWrap;
            _panTilt.PanAxis.Center = 0f;
            _panTilt.PanAxis.Range = _defaultPanRange;
            _panTilt.PanAxis.Value = Mathf.DeltaAngle(0f, _panTilt.PanAxis.Value);

            _panTilt.TiltAxis.Range = _defaultTiltRange;
        }
    }
    private void SwitchCamera()
    {
        if (CameraState == CameraState.ThirdPerson)
        {
            CameraState = CameraState.FirstPerson;
            _fpscamera.gameObject.SetActive(true);
            _tpsCamera.gameObject.SetActive(false);
        }
        else
        {
            CameraState = CameraState.ThirdPerson;
            _fpscamera.gameObject.SetActive(false);
            _tpsCamera.gameObject.SetActive(true);
        }
        OnChangePerspective();
    }
}
