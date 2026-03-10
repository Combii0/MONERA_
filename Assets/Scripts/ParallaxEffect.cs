using UnityEngine;
using System.Collections; // Required for the frame delay

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
    
    // Unified vectors make the code cleaner
    private Vector2 _startingPos;
    private Vector2 _startCamPos;
    private bool _isReady = false; // Prevents parallax before setup is done

    private IEnumerator Start()
    {
        if (MainCamera == null) MainCamera = Camera.main;

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

        // WAIT 1 FRAME! This allows your camera script to snap to the player first.
        yield return new WaitForEndOfFrame();

        // Now record the true starting positions after everything has settled
        _startingPos = transform.position;
        _startCamPos = MainCamera.transform.position;
        
        _isReady = true;
    }

    private void LateUpdate()
    {
        // Don't do any math until the 1-frame delay has finished
        if (!_isReady || _childSprites == null || _childSprites.Length == 0) return;

        Vector3 camPos = MainCamera.transform.position;

        // --- 1. X-Axis Movement (Now uses relative math like Y) ---
        float distX = (camPos.x - _startCamPos.x) * AmountOfParallaxX;
        float newX = _startingPos.x + distX;

        // --- 2. Y-Axis Movement ---
        float newY = transform.position.y;
        if (TrackCameraY)
        {
            float distY = (camPos.y - _startCamPos.y) * AmountOfParallaxY;
            newY = _startingPos.y + distY;
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