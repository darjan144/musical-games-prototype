using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Full-screen fluid background, ported from Pavel Dobryakov's WebGL-Fluid-Simulation (MIT).
/// Dragging the mouse or a finger stirs dye into it; other scripts can call Splat or RandomSplats.
/// It draws on its own overlay canvas behind the rest of the UI, so put it on an object that is not inside a Canvas.
/// </summary>
public class FluidSimulation : MonoBehaviour
{
    [SerializeField] Shader simulationShader;
    [SerializeField] Shader displayShader;

    [Header("Quality")]
    [Tooltip("Short side of the velocity grid, in cells.")]
    [SerializeField] int simResolution = 128;
    [Tooltip("Short side of the dye texture, in pixels.")]
    [SerializeField] int dyeResolution = 1024;
    [SerializeField] int pressureIterations = 20;

    [Header("Fluid")]
    [Tooltip("How quickly the dye fades.")]
    [SerializeField] float densityDissipation = 1f;
    [Tooltip("How quickly the motion dies down.")]
    [SerializeField] float velocityDissipation = 0.2f;
    [Tooltip("How much of last frame's pressure is kept as the starting guess.")]
    [Range(0f, 1f)]
    [SerializeField] float pressure = 0.8f;
    [Tooltip("Vorticity: how strongly small swirls are kept alive.")]
    [SerializeField] float curl = 30f;

    [Header("Splats")]
    [SerializeField] float splatRadius = 0.25f;
    [SerializeField] float splatForce = 6000f;
    [Tooltip("Dragging the mouse or a finger stirs the fluid.")]
    [SerializeField] bool pointerInput = true;
    [Tooltip("How many times a second a dragging pointer changes colour.")]
    [SerializeField] float colorUpdateSpeed = 10f;
    [Tooltip("Random splats thrown in when the fluid starts.")]
    [SerializeField] Vector2Int startSplats = new Vector2Int(5, 25);

    [Header("Display")]
    [SerializeField] bool shading = true;
    [SerializeField] Color backColor = Color.black;
    [Tooltip("Sorting order of the fluid's canvas; keep it below the game's canvases.")]
    [SerializeField] int sortingOrder = -1000;

    // Must match the pass order in FluidSimulation.shader.
    const int PassSplat = 0;
    const int PassAdvection = 1;
    const int PassDivergence = 2;
    const int PassCurl = 3;
    const int PassVorticity = 4;
    const int PassPressure = 5;
    const int PassGradientSubtract = 6;
    const int PassClear = 7;

    const int MaxTouches = 10;

    static readonly int SimTexelSizeId = Shader.PropertyToID("_SimTexelSize");
    static readonly int VelocityId = Shader.PropertyToID("_Velocity");
    static readonly int CurlId = Shader.PropertyToID("_Curl");
    static readonly int DivergenceId = Shader.PropertyToID("_Divergence");
    static readonly int PressureId = Shader.PropertyToID("_Pressure");
    static readonly int AspectRatioId = Shader.PropertyToID("_AspectRatio");
    static readonly int SplatPointId = Shader.PropertyToID("_SplatPoint");
    static readonly int SplatColorId = Shader.PropertyToID("_SplatColor");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int DtId = Shader.PropertyToID("_Dt");
    static readonly int DissipationId = Shader.PropertyToID("_Dissipation");
    static readonly int CurlStrengthId = Shader.PropertyToID("_CurlStrength");
    static readonly int ValueId = Shader.PropertyToID("_Value");
    static readonly int DyeId = Shader.PropertyToID("_Dye");
    static readonly int BackColorId = Shader.PropertyToID("_BackColor");

    // A texture that is read and written in the same pass: read one copy, write the other, then swap.
    class DoubleTarget
    {
        public RenderTexture read;
        public RenderTexture write;

        public void Swap()
        {
            var previous = read;
            read = write;
            write = previous;
        }

        public void Release()
        {
            Destroy(read);
            Destroy(write);
        }
    }

    struct Pointer
    {
        public bool down;
        public Vector2 uv;
        public Color color;
    }

    Material simulation;
    Material display;
    GameObject displayObject;

    DoubleTarget velocity;
    DoubleTarget dye;
    DoubleTarget pressureTarget;
    RenderTexture divergenceTarget;
    RenderTexture curlTarget;

    // Slot 0 is the mouse, the rest are touches.
    readonly Pointer[] pointers = new Pointer[1 + MaxTouches];
    Vector2Int screenSize;
    float colorUpdateTimer;

    void OnEnable()
    {
        simulation = new Material(simulationShader);
        display = new Material(displayShader);

        displayObject = new GameObject("Fluid Display", typeof(Canvas), typeof(RawImage));
        displayObject.transform.SetParent(transform, false);
        var canvas = displayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var image = displayObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.material = display;

        InitTargets();
        RandomSplats(Random.Range(startSplats.x, startSplats.y + 1));
    }

    void OnDisable()
    {
        velocity.Release();
        dye.Release();
        pressureTarget.Release();
        Destroy(divergenceTarget);
        Destroy(curlTarget);
        velocity = dye = pressureTarget = null;

        Destroy(displayObject);
        Destroy(simulation);
        Destroy(display);
    }

    void Update()
    {
        // The original caps the step at one 60 fps frame, so a slow frame slows the fluid rather than breaking it.
        float dt = Mathf.Min(Time.deltaTime, 0.016666f);

        if (screenSize.x != Screen.width || screenSize.y != Screen.height) InitTargets();

        if (pointerInput) ApplyPointers(dt);
        Step(dt);

        if (shading) display.EnableKeyword("SHADING");
        else display.DisableKeyword("SHADING");
        display.SetColor(BackColorId, backColor.gamma);
        display.SetTexture(DyeId, dye.read);

        RenderTexture.active = null;
    }

    /// <summary>
    /// Adds a blob of dye and a push. uv is the screen position from (0,0) bottom left to (1,1) top right.
    /// Colours are in the range the original uses: about 0.15 for a drag, 1.5 for a burst.
    /// </summary>
    public void Splat(Vector2 uv, Vector2 force, Color color)
    {
        float aspectRatio = (float)Screen.width / Screen.height;
        float radius = splatRadius / 100f;
        if (aspectRatio > 1f) radius *= aspectRatio;

        simulation.SetFloat(AspectRatioId, aspectRatio);
        simulation.SetVector(SplatPointId, uv);
        simulation.SetFloat(RadiusId, radius);

        simulation.SetVector(SplatColorId, new Vector4(force.x, force.y, 0f, 0f));
        Graphics.Blit(velocity.read, velocity.write, simulation, PassSplat);
        velocity.Swap();

        simulation.SetVector(SplatColorId, new Vector4(color.r, color.g, color.b, 0f));
        Graphics.Blit(dye.read, dye.write, simulation, PassSplat);
        dye.Swap();
    }

    /// <summary>Throws in bright splats at random places, pushed in random directions.</summary>
    public void RandomSplats(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            var uv = new Vector2(Random.value, Random.value);
            var force = new Vector2(Random.value - 0.5f, Random.value - 0.5f) * 1000f;
            Splat(uv, force, GenerateColor() * 10f);
        }
    }

    void ApplyPointers(float dt)
    {
        colorUpdateTimer += dt * colorUpdateSpeed;
        bool newColors = colorUpdateTimer >= 1f;
        if (newColors) colorUpdateTimer %= 1f;

        var mouse = Mouse.current;
        if (mouse != null)
            UpdatePointer(ref pointers[0], mouse.leftButton.isPressed, mouse.position.ReadValue(), newColors);

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            int count = Mathf.Min(touchscreen.touches.Count, MaxTouches);
            for (int i = 0; i < count; i++)
            {
                var touch = touchscreen.touches[i];
                UpdatePointer(ref pointers[i + 1], touch.press.isPressed, touch.position.ReadValue(), newColors);
            }
        }
    }

    void UpdatePointer(ref Pointer pointer, bool pressed, Vector2 screenPosition, bool newColor)
    {
        if (!pressed)
        {
            pointer.down = false;
            return;
        }

        var uv = new Vector2(screenPosition.x / Screen.width, screenPosition.y / Screen.height);
        if (!pointer.down)
        {
            pointer.down = true;
            pointer.uv = uv;
            pointer.color = GenerateColor();
            return;
        }

        if (newColor) pointer.color = GenerateColor();

        // Scaled so a drag pushes equally hard in both directions whatever the screen's shape.
        var delta = uv - pointer.uv;
        pointer.uv = uv;
        float aspectRatio = (float)Screen.width / Screen.height;
        if (aspectRatio < 1f) delta.x *= aspectRatio;
        if (aspectRatio > 1f) delta.y /= aspectRatio;

        if (delta != Vector2.zero) Splat(uv, delta * splatForce, pointer.color);
    }

    void Step(float dt)
    {
        simulation.SetFloat(DtId, dt);
        simulation.SetVector(SimTexelSizeId, velocity.read.texelSize);

        Graphics.Blit(velocity.read, curlTarget, simulation, PassCurl);

        simulation.SetTexture(CurlId, curlTarget);
        simulation.SetFloat(CurlStrengthId, curl);
        Graphics.Blit(velocity.read, velocity.write, simulation, PassVorticity);
        velocity.Swap();

        Graphics.Blit(velocity.read, divergenceTarget, simulation, PassDivergence);

        simulation.SetFloat(ValueId, pressure);
        Graphics.Blit(pressureTarget.read, pressureTarget.write, simulation, PassClear);
        pressureTarget.Swap();

        simulation.SetTexture(DivergenceId, divergenceTarget);
        for (int i = 0; i < pressureIterations; i++)
        {
            Graphics.Blit(pressureTarget.read, pressureTarget.write, simulation, PassPressure);
            pressureTarget.Swap();
        }

        simulation.SetTexture(PressureId, pressureTarget.read);
        Graphics.Blit(velocity.read, velocity.write, simulation, PassGradientSubtract);
        velocity.Swap();

        simulation.SetTexture(VelocityId, velocity.read);
        simulation.SetFloat(DissipationId, velocityDissipation);
        Graphics.Blit(velocity.read, velocity.write, simulation, PassAdvection);
        velocity.Swap();

        simulation.SetTexture(VelocityId, velocity.read);
        simulation.SetFloat(DissipationId, densityDissipation);
        Graphics.Blit(dye.read, dye.write, simulation, PassAdvection);
        dye.Swap();
    }

    // Also runs when the screen changes size; the dye and velocity are stretched over to the new textures.
    void InitTargets()
    {
        screenSize = new Vector2Int(Screen.width, Screen.height);
        var simSize = GetResolution(simResolution);
        var dyeSize = GetResolution(dyeResolution);

        velocity = Resized(velocity, simSize, RenderTextureFormat.RGHalf, FilterMode.Bilinear);
        dye = Resized(dye, dyeSize, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear);

        pressureTarget?.Release();
        Destroy(divergenceTarget);
        Destroy(curlTarget);
        pressureTarget = Resized(null, simSize, RenderTextureFormat.RHalf, FilterMode.Point);
        divergenceTarget = CreateTarget(simSize, RenderTextureFormat.RHalf, FilterMode.Point);
        curlTarget = CreateTarget(simSize, RenderTextureFormat.RHalf, FilterMode.Point);
    }

    DoubleTarget Resized(DoubleTarget old, Vector2Int size, RenderTextureFormat format, FilterMode filter)
    {
        var target = new DoubleTarget
        {
            read = CreateTarget(size, format, filter),
            write = CreateTarget(size, format, filter),
        };
        if (old != null)
        {
            Graphics.Blit(old.read, target.read);
            old.Release();
        }
        return target;
    }

    static RenderTexture CreateTarget(Vector2Int size, RenderTextureFormat format, FilterMode filter)
    {
        if (!SystemInfo.SupportsRenderTextureFormat(format)) format = RenderTextureFormat.ARGBHalf;

        var target = new RenderTexture(size.x, size.y, 0, format, RenderTextureReadWrite.Linear)
        {
            filterMode = filter,
            wrapMode = TextureWrapMode.Clamp,
        };
        target.Create();

        RenderTexture.active = target;
        GL.Clear(false, true, Color.clear);
        return target;
    }

    // The given resolution is the short side; the long side follows the screen's shape.
    static Vector2Int GetResolution(int resolution)
    {
        float aspectRatio = (float)Screen.width / Screen.height;
        if (aspectRatio < 1f) aspectRatio = 1f / aspectRatio;

        int min = resolution;
        int max = Mathf.RoundToInt(resolution * aspectRatio);
        return Screen.width > Screen.height ? new Vector2Int(max, min) : new Vector2Int(min, max);
    }

    static Color GenerateColor()
    {
        return Color.HSVToRGB(Random.value, 1f, 1f) * 0.15f;
    }
}
