using System.Collections.Generic;
using UnityEngine;
using static Modl.LevelCaptureUtils;


namespace Modl
{
    public class ModlDynamicLevelCapture : MonoBehaviour
    {
        private Camera _camera;
        private Camera modlCamera
        {
            get
            {
                if (_camera == null)
                {
                    var go = new GameObject("ModlDynamicLevelCaptureCamera",typeof(Camera));
                    _camera = go.GetComponent<Camera>();

                    _camera.orthographic = true;
                    _camera.cameraType = CameraType.SceneView;
                    _camera.clearFlags = CameraClearFlags.Color;
                    _camera.backgroundColor = Color.clear;
                    
                    _camera.aspect = 1;
                    _camera.orthographicSize = orthographicSize;
                    _camera.transform.position = transform.position + DirectionPositionOffset[(int)direction] * distance;
                    _camera.transform.rotation = DirectionRotations[(int) direction];    
                }

                return _camera;
            }
        }
        
        public ViewDirection direction;
        public float distance = 100;
        public float orthographicSize = 10;

        //Location based capturing
        private Vector2 _originOffset;
        private HashSet<string> _capturedPositions = new HashSet<string>();

        private void Start()
        {
            _originOffset = To2DOffset(direction, transform.position);
        }

        private bool sectionsMerged;
        
        private void Update()
        {
            LocationBasedCaptureLogic();
        }

        private void OnApplicationQuit()
        {
            if (!sectionsMerged)
            {
                MergeLevelSections(_capturedPositions);
                sectionsMerged = true;
            }
        }

        private void LocationBasedCaptureLogic()
        {
            var width = modlCamera.aspect * modlCamera.orthographicSize;
            var height =  width / modlCamera.aspect;
            
            //TODO: consider making the offsets align to a grid instead, to avoid overlap between images.
            var currentOffset = _originOffset - To2DOffset(direction, transform.position);
            var horizontalWidthOffset =  Mathf.RoundToInt(Mathf.RoundToInt(currentOffset.x) / width);
            var verticalHeightOffset = Mathf.RoundToInt(Mathf.RoundToInt(currentOffset.y) / height);

            var key = $"Location-{direction}-({horizontalWidthOffset}_{verticalHeightOffset})";
            if (!_capturedPositions.Contains(key))
            {
                CaptureImageAndCoordinates(key, modlCamera, direction,
                    To2DOffset(direction, transform.position));
                _capturedPositions.Add(key);
            }
        }

        private void LateUpdate()
        {
            modlCamera.transform.position = transform.position + DirectionPositionOffset[(int)direction] * distance;
        }
    }
}