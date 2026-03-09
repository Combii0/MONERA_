using UnityEngine;

public class ParallaxEffect : MonoBehaviour
    {
        [Header("X-Axis Parallax")]
        public float AmountOfParallaxX; 
        
        [Header("Y-Axis Parallax")]
        public bool TrackCameraY = true; 
        public float AmountOfParallaxY = 1f; 

        [Header("References")]
        public Camera MainCamera;

        private Transform[] _childSprites;
        private float _spriteWidth;
        private float _totalWidth;
        private float _startingPosX;
        private float _startingPosY;
        private float _startCamPosY;

        private void Start()
        {
            if (MainCamera == null) MainCamera = Camera.main;

            _startingPosX = transform.position.x;
            _startingPosY = transform.position.y;
            
            _startCamPosY = MainCamera.transform.position.y;

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            _childSprites = new Transform[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                _childSprites[i] = renderers[i].transform;
            }

            if (renderers.Length > 1)
            {
                // THE FIX: Instead of looking at the sprite's tiny pixel width, 
                // it measures the exact distance you placed them apart in the scene!
                _spriteWidth = Mathf.Abs(_childSprites[1].position.x - _childSprites[0].position.x);
                _totalWidth = _spriteWidth * renderers.Length;
            }
            else if (renderers.Length == 1)
            {
                _spriteWidth = renderers[0].bounds.size.x;
                _totalWidth = _spriteWidth;
            }
        }

        private void LateUpdate()
        {
            if (_childSprites == null || _childSprites.Length == 0) return;

            Vector3 camPos = MainCamera.transform.position;

            // --- 1. X-Axis Movement ---
            float distanceX = camPos.x * AmountOfParallaxX;
            float newX = _startingPosX + distanceX;

            // --- 2. Y-Axis Movement ---
            float newY = transform.position.y;
            if (TrackCameraY)
            {
                float distCamMovedY = camPos.y - _startCamPosY;
                float distanceY = distCamMovedY * AmountOfParallaxY;
                newY = _startingPosY + distanceY;
            }

            transform.position = new Vector3(newX, newY, transform.position.z);

            // --- 3. Wrap Individual Children (X-Axis only) ---
            foreach (Transform child in _childSprites)
            {
                float distanceToCam = camPos.x - child.position.x;

                if (Mathf.Abs(distanceToCam) > _totalWidth / 2f)
                {
                    float shiftAmount = Mathf.Round(distanceToCam / _totalWidth) * _totalWidth;
                    child.position += new Vector3(shiftAmount, 0, 0);
                }
            }
        }
    }