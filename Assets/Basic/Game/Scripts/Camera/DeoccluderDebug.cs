using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using UnityEngine;

/// Script sementara untuk mencari tahu collider apa yang bikin Deoccluder
/// menarik kamera mendekat. Pasang di GameObject ThirdPersonCamera, lalu Play.
/// Hapus lagi kalau sudah ketemu penyebabnya.
[RequireComponent(typeof(CinemachineDeoccluder))]
public class DeoccluderDebug : MonoBehaviour
{
    private CinemachineDeoccluder _deoccluder;
    private CinemachineVirtualCameraBase _camera;

    private readonly List<List<Vector3>> _paths = new();
    private readonly List<List<Collider>> _obstacles = new();
    private readonly StringBuilder _builder = new();
    private string _lastMessage;

    private void Awake()
    {
        _deoccluder = GetComponent<CinemachineDeoccluder>();
        _camera = GetComponent<CinemachineVirtualCameraBase>();
    }

    private void LateUpdate()
    {
        _deoccluder.DebugCollisionPaths(_paths, _obstacles);

        _builder.Clear();
        foreach (List<Collider> obstacles in _obstacles)
        {
            foreach (Collider obstacle in obstacles)
            {
                if (obstacle == null)
                {
                    continue;
                }

                _builder.Append(obstacle.name)
                    .Append(" [layer ")
                    .Append(LayerMask.LayerToName(obstacle.gameObject.layer))
                    .Append("]  ");
            }
        }

        float distance = Vector3.Distance(_camera.State.GetFinalPosition(), _camera.State.ReferenceLookAt);
        string message = $"Deoccluder | jarak kamera ke target: {distance:F2} m " +
                         $"| digeser: {_deoccluder.GetCameraDisplacementDistance(_camera):F2} m " +
                         $"| penghalang: {(_builder.Length == 0 ? "(tidak ada)" : _builder.ToString())}";

        if (message != _lastMessage)
        {
            _lastMessage = message;
            Debug.Log(message);
        }
    }
}
