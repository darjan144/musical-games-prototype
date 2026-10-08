using UnityEngine;

/// <summary>
/// Layers of seamless sprites that slide to the left, far ones slower than near ones, so the scene seems to travel past.
/// Each layer is scaled to fill the camera's height and repeated to cover its width, whatever the screen shape.
/// The sprites must be imported with Mesh Type = Full Rect and Wrap Mode = Repeat.
/// </summary>
public class ScrollingBackground : MonoBehaviour
{
    [System.Serializable]
    class Layer
    {
        public SpriteRenderer renderer;
        [Tooltip("How much of the scroll this layer follows: 0 stands still (sky), 1 moves with the ground.")]
        [Range(0f, 1f)] public float parallax = 1f;
        [System.NonSerialized] public float offset;
    }

    [SerializeField] Camera view;
    [Tooltip("Back to front.")]
    [SerializeField] Layer[] layers;

    /// <summary>Moves the scene to the left by this many world units (at the ground; farther layers move less).</summary>
    public void Scroll(float distance)
    {
        foreach (var layer in layers) layer.offset += distance * layer.parallax;
    }

    void LateUpdate()
    {
        float viewHeight = view.orthographicSize * 2f;
        float viewWidth = viewHeight * view.aspect;
        Vector3 centre = view.transform.position;

        foreach (var layer in layers)
        {
            Vector2 tile = layer.renderer.sprite.bounds.size;
            float scale = viewHeight / tile.y;
            float tileWidth = tile.x * scale;

            // One tile more than fits on screen, so there is always something to slide in from the right.
            int tiles = Mathf.CeilToInt(viewWidth / tileWidth) + 1;
            layer.offset = Mathf.Repeat(layer.offset, tileWidth);

            layer.renderer.drawMode = SpriteDrawMode.Tiled;
            layer.renderer.size = new Vector2(tile.x * tiles, tile.y);

            var t = layer.renderer.transform;
            t.localScale = new Vector3(scale, scale, 1f);
            float left = centre.x - viewWidth / 2f - layer.offset;
            t.position = new Vector3(left + tileWidth * tiles / 2f, centre.y, t.position.z);
        }
    }
}
