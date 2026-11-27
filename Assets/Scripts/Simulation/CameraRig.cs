using UnityEngine;

namespace EgressLab.Simulation
{
    /// <summary>
    /// Frames the whole floor, then lets a recording session orbit, zoom, and pan without leaving play mode.
    /// Right-drag orbits, scroll zooms, arrow keys pan.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        Vector3 _pivot = new Vector3(20f, 0f, 12f);
        float _yaw = 8f;
        float _pitch = 58f;
        float _distance = 42f;

        public static CameraRig Ensure()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 250f;
            camera.fieldOfView = 40f;

            CameraRig rig = camera.GetComponent<CameraRig>();
            if (rig == null) rig = camera.gameObject.AddComponent<CameraRig>();
            rig.Apply();
            return rig;
        }

        public void Tick()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * 4.5f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 3f, 25f, 80f);
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) _distance = Mathf.Clamp(_distance - scroll * 2.2f, 18f, 70f);

            Vector3 pan = Vector3.zero;
            if (Input.GetKey(KeyCode.LeftArrow)) pan.x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) pan.x += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) pan.z -= 1f;
            if (Input.GetKey(KeyCode.UpArrow)) pan.z += 1f;
            if (pan.sqrMagnitude > 0f)
            {
                Quaternion flat = Quaternion.Euler(0f, _yaw, 0f);
                _pivot += flat * pan.normalized * (12f * Time.unscaledDeltaTime);
            }

            Apply();
        }

        void Apply()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _pivot + rotation * new Vector3(0f, 0f, -_distance);
            transform.rotation = rotation;
        }
    }
}
