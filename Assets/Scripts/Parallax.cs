using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class Parallax : MonoBehaviour
{
    [Serializable]
    private class ParallaxLayer
    {
        public Transform layer;
        [Range(0f, 1.5f)] public float factorX = 0.5f;
        [Range(0f, 1.5f)] public float factorY = 0.15f;
        public bool infiniteX = true;
        public bool infiniteY = false;

        [NonSerialized] public float startX;
        [NonSerialized] public float startY;
        [NonSerialized] public float startZ;
        [NonSerialized] public float cameraYOffset;
        [NonSerialized] public float tileWidth;
        [NonSerialized] public float tileHeight;
        [NonSerialized] public float tileStepX;
        [NonSerialized] public float tileStepY;
        [NonSerialized] public bool initialized;

        [NonSerialized] public readonly Dictionary<Vector2Int, Transform> tiles = new Dictionary<Vector2Int, Transform>();
        [NonSerialized] public int minX;
        [NonSerialized] public int maxX;
        [NonSerialized] public int minY;
        [NonSerialized] public int maxY;
        [NonSerialized] public readonly Queue<Transform> pooledTiles = new Queue<Transform>();
        [NonSerialized] public readonly List<Vector2Int> keyBuffer = new List<Vector2Int>(64);
    }

    [Header("Referencias")]
    [SerializeField] private Transform cameraTarget;

    [Header("Capas")]
    [SerializeField] private ParallaxLayer[] layers;

    [Header("Tiling")]
    [SerializeField] private bool enableInfiniteParallax = true;
    [SerializeField] private bool useTilePooling = true;
    [SerializeField] private bool forceFillVisibleGaps = true;
    [SerializeField, Range(0f, 0.25f)] private float tileOverlapX = 0.02f;
    [SerializeField, Range(0f, 0.25f)] private float tileOverlapY = 0.02f;
    [SerializeField, Range(0f, 2f)] private float coveragePaddingTiles = 0.2f;
    [SerializeField, Range(0f, 4f)] private float cullPaddingTiles = 1f;
    [SerializeField, Min(0)] private int prewarmTilesX = 2;
    [SerializeField, Min(0)] private int prewarmTilesY = 0;
    [SerializeField, Min(1)] private int maxCreatePerFramePerLayer = 8;
    [SerializeField, Min(0)] private int maxPooledTilesPerLayer = 12;

    [Header("Ajuste Pantalla")]
    [SerializeField] private bool fitLayersToCamera = true;
    [SerializeField, Range(1f, 1.35f)] private float fitPadding = 1.03f;

    [Header("Eje Y")]
    [SerializeField] private bool followCameraY = true;

    private Camera mainCamera;
    private Vector3 cameraStartPosition;

    private void Awake()
    {
        mainCamera = Camera.main;

        if (cameraTarget == null && mainCamera != null)
        {
            cameraTarget = mainCamera.transform;
        }
    }

    private void Start()
    {
        if (cameraTarget == null) return;

        cameraStartPosition = cameraTarget.position;

        for (int i = 0; i < layers.Length; i++)
        {
            InitializeLayer(layers[i]);
        }

        // Prepara tiles antes del gameplay para reducir picos en los primeros frames.
        Vector3 camDelta = cameraTarget.position - cameraStartPosition;
        for (int i = 0; i < layers.Length; i++)
        {
            PrewarmLayer(layers[i], camDelta);
        }
    }

    private void LateUpdate()
    {
        if (cameraTarget == null) return;

        Vector3 camDelta = cameraTarget.position - cameraStartPosition;

        for (int i = 0; i < layers.Length; i++)
        {
            ParallaxLayer layer = layers[i];
            if (!layer.initialized) continue;

            bool useInfinite = enableInfiniteParallax && (layer.infiniteX || layer.infiniteY);
            if (useInfinite)
            {
                UpdateInfiniteLayer(layer, camDelta);
            }
            else
            {
                UpdateSingleLayer(layer, camDelta);
            }
        }
    }

    private void InitializeLayer(ParallaxLayer layer)
    {
        if (layer == null || layer.layer == null) return;

        SpriteRenderer sr = layer.layer.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogWarning("Parallax: la capa " + layer.layer.name + " necesita SpriteRenderer para tiling.");
            return;
        }

        TryFitLayerToCamera(layer, sr);

        layer.startX = layer.layer.position.x;
        layer.startY = layer.layer.position.y;
        layer.startZ = layer.layer.position.z;
        layer.cameraYOffset = cameraTarget != null ? layer.startY - cameraTarget.position.y : 0f;
        layer.tileWidth = Mathf.Max(0.0001f, sr.bounds.size.x);
        layer.tileHeight = Mathf.Max(0.0001f, sr.bounds.size.y);
        layer.tileStepX = Mathf.Max(0.0001f, layer.tileWidth - Mathf.Max(0f, tileOverlapX));
        layer.tileStepY = Mathf.Max(0.0001f, layer.tileHeight - Mathf.Max(0f, tileOverlapY));

        layer.tiles.Clear();
        layer.tiles[new Vector2Int(0, 0)] = layer.layer;
        layer.pooledTiles.Clear();
        layer.keyBuffer.Clear();
        layer.minX = 0;
        layer.maxX = 0;
        layer.minY = 0;
        layer.maxY = 0;
        layer.initialized = true;
    }

    private void TryFitLayerToCamera(ParallaxLayer layer, SpriteRenderer sr)
    {
        if (!fitLayersToCamera || layer == null || sr == null || mainCamera == null) return;

        GetCameraBoundsAtZ(layer.layer.position.z, out float camLeft, out float camRight, out float camBottom, out float camTop);
        float cameraWidth = Mathf.Max(0.0001f, camRight - camLeft);
        float cameraHeight = Mathf.Max(0.0001f, camTop - camBottom);
        float currentWidth = Mathf.Max(0.0001f, sr.bounds.size.x);
        float currentHeight = Mathf.Max(0.0001f, sr.bounds.size.y);

        float requiredWidth = cameraWidth * Mathf.Max(1f, fitPadding);
        float requiredHeight = cameraHeight * Mathf.Max(1f, fitPadding);

        float widthRatio = layer.infiniteX ? 1f : (requiredWidth / currentWidth);
        float heightRatio = requiredHeight / currentHeight;
        float scaleMultiplier = Mathf.Max(1f, widthRatio, heightRatio);
        if (scaleMultiplier <= 1.0001f) return;

        Vector3 localScale = layer.layer.localScale;
        float signX = Mathf.Approximately(localScale.x, 0f) ? 1f : Mathf.Sign(localScale.x);
        float signY = Mathf.Approximately(localScale.y, 0f) ? 1f : Mathf.Sign(localScale.y);

        layer.layer.localScale = new Vector3(
            signX * Mathf.Abs(localScale.x) * scaleMultiplier,
            signY * Mathf.Abs(localScale.y) * scaleMultiplier,
            localScale.z
        );
    }

    private void UpdateSingleLayer(ParallaxLayer layer, Vector3 camDelta)
    {
        float x = layer.startX + (camDelta.x * layer.factorX);
        float y = followCameraY
            ? cameraTarget.position.y + layer.cameraYOffset
            : layer.startY + (camDelta.y * layer.factorY);
        layer.layer.position = new Vector3(x, y, layer.startZ);
    }

    private void UpdateInfiniteLayer(ParallaxLayer layer, Vector3 camDelta)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        int createdThisFrame = 0;
        RemoveMissingTiles(layer);
        EnsureCoverage(layer, camDelta, ref createdThisFrame);
        CullInvisibleTiles(layer, camDelta);
        UpdateTilePositions(layer, camDelta);
    }

    private void EnsureCoverage(ParallaxLayer layer, Vector3 camDelta, ref int createdThisFrame)
    {
        GetCameraBoundsAtZ(layer.startZ, out float camLeft, out float camRight, out float camBottom, out float camTop);
        float coverPadX = layer.tileWidth * Mathf.Max(0f, coveragePaddingTiles);
        float coverPadY = layer.tileHeight * Mathf.Max(0f, coveragePaddingTiles);
        camLeft -= coverPadX;
        camRight += coverPadX;
        camBottom -= coverPadY;
        camTop += coverPadY;

        float parallaxOffsetX = camDelta.x * layer.factorX;
        float baseCenterX = layer.startX + parallaxOffsetX;

        int neededMinX = 0;
        int neededMaxX = 0;
        if (layer.infiniteX)
        {
            neededMinX = Mathf.CeilToInt((camLeft - (layer.tileWidth * 0.5f) - baseCenterX) / layer.tileStepX);
            neededMaxX = Mathf.FloorToInt((camRight + (layer.tileWidth * 0.5f) - baseCenterX) / layer.tileStepX);
        }

        float baseCenterY = GetTileCenterY(layer, 0, camDelta);
        int neededMinY = 0;
        int neededMaxY = 0;
        if (layer.infiniteY)
        {
            neededMinY = Mathf.CeilToInt((camBottom - (layer.tileHeight * 0.5f) - baseCenterY) / layer.tileStepY);
            neededMaxY = Mathf.FloorToInt((camTop + (layer.tileHeight * 0.5f) - baseCenterY) / layer.tileStepY);
        }

        for (int x = neededMinX; x <= neededMaxX; x++)
        {
            for (int y = neededMinY; y <= neededMaxY; y++)
            {
                if (!TryCreateTileLimited(layer, x, y, ref createdThisFrame, forceFillVisibleGaps))
                {
                    return;
                }
            }
        }

        RecalculateBounds(layer);
    }

    private void CullInvisibleTiles(ParallaxLayer layer, Vector3 camDelta)
    {
        GetCameraBoundsAtZ(layer.startZ, out float camLeft, out float camRight, out float camBottom, out float camTop);
        float padX = layer.tileWidth * cullPaddingTiles;
        float padY = layer.tileHeight * cullPaddingTiles;
        camLeft -= padX;
        camRight += padX;
        camBottom -= padY;
        camTop += padY;

        layer.keyBuffer.Clear();
        foreach (Vector2Int key in layer.tiles.Keys)
        {
            layer.keyBuffer.Add(key);
        }

        for (int i = 0; i < layer.keyBuffer.Count; i++)
        {
            Vector2Int key = layer.keyBuffer[i];
            if (key.x == 0 && key.y == 0) continue;

            float centerX = GetTileCenterX(layer, key.x, camDelta);
            float centerY = GetTileCenterY(layer, key.y, camDelta);

            float left = centerX - (layer.tileWidth * 0.5f);
            float right = centerX + (layer.tileWidth * 0.5f);
            float bottom = centerY - (layer.tileHeight * 0.5f);
            float top = centerY + (layer.tileHeight * 0.5f);

            const float cullEpsilon = 0.0005f;
            bool fullyOutside = right < (camLeft - cullEpsilon)
                || left > (camRight + cullEpsilon)
                || top < (camBottom - cullEpsilon)
                || bottom > (camTop + cullEpsilon);
            if (!fullyOutside) continue;

            Transform tile = layer.tiles[key];
            RecycleTile(layer, tile);
            layer.tiles.Remove(key);
        }

        RecalculateBounds(layer);
    }

    private void UpdateTilePositions(ParallaxLayer layer, Vector3 camDelta)
    {
        layer.keyBuffer.Clear();
        foreach (Vector2Int key in layer.tiles.Keys)
        {
            layer.keyBuffer.Add(key);
        }

        for (int i = 0; i < layer.keyBuffer.Count; i++)
        {
            Vector2Int key = layer.keyBuffer[i];
            Transform tile = layer.tiles[key];
            if (tile == null) continue;

            float x = GetTileCenterX(layer, key.x, camDelta);
            float y = GetTileCenterY(layer, key.y, camDelta);
            tile.position = new Vector3(x, y, layer.startZ);
        }
    }

    private void CreateTile(ParallaxLayer layer, int indexX, int indexY)
    {
        Vector2Int key = new Vector2Int(indexX, indexY);
        if (layer.tiles.ContainsKey(key)) return;

        Transform clone = GetTileFromPool(layer);
        if (clone == null)
        {
            clone = Instantiate(layer.layer, layer.layer.parent);
        }

        clone.name = layer.layer.name + "_Tile_" + indexX + "_" + indexY;
        clone.gameObject.SetActive(true);
        layer.tiles[key] = clone;
    }

    private bool TryCreateTileLimited(ParallaxLayer layer, int indexX, int indexY, ref int createdThisFrame, bool forceCreate)
    {
        Vector2Int key = new Vector2Int(indexX, indexY);
        if (layer.tiles.ContainsKey(key)) return true;
        if (createdThisFrame >= maxCreatePerFramePerLayer && !forceCreate) return false;

        CreateTile(layer, indexX, indexY);
        createdThisFrame++;
        return true;
    }

    private Transform GetTileFromPool(ParallaxLayer layer)
    {
        if (!useTilePooling || layer.pooledTiles.Count == 0) return null;
        return layer.pooledTiles.Dequeue();
    }

    private void RecycleTile(ParallaxLayer layer, Transform tile)
    {
        if (tile == null || tile == layer.layer) return;

        if (useTilePooling)
        {
            if (layer.pooledTiles.Count < maxPooledTilesPerLayer)
            {
                tile.gameObject.SetActive(false);
                layer.pooledTiles.Enqueue(tile);
            }
            else
            {
                Destroy(tile.gameObject);
            }
        }
        else
        {
            Destroy(tile.gameObject);
        }
    }

    private float GetTileCenterX(ParallaxLayer layer, int indexX, Vector3 camDelta)
    {
        float baseX = layer.startX + (indexX * layer.tileStepX);
        return baseX + (camDelta.x * layer.factorX);
    }

    private float GetTileCenterY(ParallaxLayer layer, int indexY, Vector3 camDelta)
    {
        float baseY = followCameraY
            ? (cameraTarget != null ? cameraTarget.position.y + layer.cameraYOffset : layer.startY)
            : (layer.startY + (camDelta.y * layer.factorY));
        return baseY + (indexY * layer.tileStepY);
    }

    private void RemoveMissingTiles(ParallaxLayer layer)
    {
        layer.keyBuffer.Clear();
        foreach (KeyValuePair<Vector2Int, Transform> entry in layer.tiles)
        {
            if (entry.Value != null) continue;
            layer.keyBuffer.Add(entry.Key);
        }

        if (layer.keyBuffer.Count == 0) return;

        for (int i = 0; i < layer.keyBuffer.Count; i++)
        {
            layer.tiles.Remove(layer.keyBuffer[i]);
        }

        RecalculateBounds(layer);
    }

    private void RecalculateBounds(ParallaxLayer layer)
    {
        if (layer.tiles.Count == 0)
        {
            layer.tiles[new Vector2Int(0, 0)] = layer.layer;
            layer.minX = 0;
            layer.maxX = 0;
            layer.minY = 0;
            layer.maxY = 0;
            return;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        foreach (KeyValuePair<Vector2Int, Transform> entry in layer.tiles)
        {
            Vector2Int key = entry.Key;
            if (key.x < minX) minX = key.x;
            if (key.x > maxX) maxX = key.x;
            if (key.y < minY) minY = key.y;
            if (key.y > maxY) maxY = key.y;
        }

        layer.minX = minX;
        layer.maxX = maxX;
        layer.minY = minY;
        layer.maxY = maxY;
    }

    private void PrewarmLayer(ParallaxLayer layer, Vector3 camDelta)
    {
        if (layer == null || !layer.initialized) return;
        if (!enableInfiniteParallax || (!layer.infiniteX && !layer.infiniteY)) return;

        int minX = layer.infiniteX ? -prewarmTilesX : 0;
        int maxX = layer.infiniteX ? prewarmTilesX : 0;
        int minY = layer.infiniteY ? -prewarmTilesY : 0;
        int maxY = layer.infiniteY ? prewarmTilesY : 0;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (x == 0 && y == 0) continue;
                CreateTile(layer, x, y);
            }
        }

        RecalculateBounds(layer);
        UpdateTilePositions(layer, camDelta);
    }

    private void GetCameraBoundsAtZ(float worldZ, out float left, out float right, out float bottom, out float top)
    {
        if (mainCamera == null)
        {
            left = -99999f;
            right = 99999f;
            bottom = -99999f;
            top = 99999f;
            return;
        }

        if (mainCamera.orthographic)
        {
            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = halfHeight * mainCamera.aspect;
            Vector3 camPos = mainCamera.transform.position;

            left = camPos.x - halfWidth;
            right = camPos.x + halfWidth;
            bottom = camPos.y - halfHeight;
            top = camPos.y + halfHeight;
            return;
        }

        float distance = Mathf.Abs(worldZ - mainCamera.transform.position.z);
        Vector3 bl = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 tr = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        left = bl.x;
        right = tr.x;
        bottom = bl.y;
        top = tr.y;
    }
}
