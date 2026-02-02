using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CrosshairSystem : MonoBehaviour
{
    public enum CrosshairShape { Square, Circle, Triangle, Diamond }

    [System.Serializable]
    public class CrosshairPart
    {
        public string name = "Layer";
        public CrosshairShape shape = CrosshairShape.Square;
        public Vector2 offset = Vector2.zero;
        public float width = 20f, height = 20f, rotation = 0f;

        // Color
        public Color cachedColor = Color.white;
        public float hue = 0f, saturation = 0f, value = 1f;

        // Outline
        public bool isOutline = false;
        public float outlineThickness = 2f;
        public bool isVisible = true;

        // Internal Texture Data
        [System.NonSerialized] public Texture2D runtimeTexture;
        [System.NonSerialized] public bool needsRebuild = true;

        // UI Toggles
        [System.NonSerialized] public bool useSliderX, useSliderY, useSliderW, useSliderH, useSliderR, useSliderThick;
    }

    [System.Serializable]
    public class CrosshairData { public List<CrosshairPart> parts = new List<CrosshairPart>(); }

    [Header("Engine")]
    public List<CrosshairPart> parts = new List<CrosshairPart>();
    public bool showMenu = false;

    // --- PATHING & FILE SYSTEM ---
    private string _saveFileName = ""; // This acts as both Save Name and Search Query
    private List<string> _fileList = new List<string>();
    private Vector2 _fileScrollPos;

    // Cross-platform safe path for accessible files
    private string SavePath => Path.Combine(Application.streamingAssetsPath, "Crosshairs");

    // --- UI RESOURCES ---
    private Vector2 _mainScrollPos;
    private const int TEX_SIZE = 128;
    private Texture2D _hueBar, _satBar, _valBar;
    private GUIStyle _invisibleSliderStyle;
    private GUIStyle _dropdownButtonStyle;

    void Awake()
    {
        // 1. Ensure Folder Exists (Works in Editor AND Build)
        if (!Directory.Exists(SavePath)) Directory.CreateDirectory(SavePath);

        RefreshFileList();

        // 2. Load Defaults if empty
        if (parts.Count == 0) LoadStandardPreset();

        // 3. Init Color UI Textures
        _hueBar = new Texture2D(128, 1); _satBar = new Texture2D(128, 1); _valBar = new Texture2D(128, 1);
        for (int i = 0; i < 128; i++) _hueBar.SetPixel(i, 0, Color.HSVToRGB(i / 128f, 1f, 1f));
        _hueBar.Apply();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            showMenu = !showMenu;
            Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = showMenu;
            if (showMenu) RefreshFileList(); // Refresh files when opening menu
        }
    }

    // --- FILE SYSTEM LOGIC ---
    void RefreshFileList()
    {
        _fileList.Clear();
        if (Directory.Exists(SavePath))
        {
            var info = new DirectoryInfo(SavePath);
            var files = info.GetFiles("*.json");
            foreach (var f in files) _fileList.Add(Path.GetFileNameWithoutExtension(f.Name));
        }
    }

    void SaveCurrent(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        string fullPath = Path.Combine(SavePath, name + ".json");

        string json = JsonUtility.ToJson(new CrosshairData { parts = parts }, true);
        File.WriteAllText(fullPath, json);

        Debug.Log($"Saved Crosshair to: {fullPath}");
        RefreshFileList();
    }

    void LoadSelected(string name)
    {
        string fullPath = Path.Combine(SavePath, name + ".json");
        if (File.Exists(fullPath))
        {
            string json = File.ReadAllText(fullPath);
            parts = JsonUtility.FromJson<CrosshairData>(json).parts;

            // Rebuild textures immediately
            foreach (var p in parts) p.needsRebuild = true;
            _saveFileName = name;
        }
    }

    // --- DRAWING LOGIC ---
    void OnGUI()
    {
        DrawCrosshair();
        if (showMenu) DrawMenu();
    }

    void DrawCrosshair()
    {
        Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
        foreach (var p in parts)
        {
            if (!p.isVisible) continue;
            if (p.needsRebuild || p.runtimeTexture == null) RebuildPartTexture(p);

            GUI.color = p.cachedColor;
            Matrix4x4 backup = GUI.matrix;
            Vector2 pos = center + p.offset;
            GUIUtility.RotateAroundPivot(p.rotation, pos);

            if (p.shape == CrosshairShape.Square && p.isOutline)
                DrawProceduralSquare(pos, p.width, p.height, p.outlineThickness);
            else
                GUI.DrawTexture(new Rect(pos.x - p.width / 2, pos.y - p.height / 2, p.width, p.height), p.runtimeTexture);

            GUI.matrix = backup;
        }
        GUI.color = Color.white;
    }

    // --- MENU UI ---
    void DrawMenu()
    {
        if (_invisibleSliderStyle == null)
        {
            _invisibleSliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            _invisibleSliderStyle.normal.background = null;
            _dropdownButtonStyle = new GUIStyle(GUI.skin.button);
            _dropdownButtonStyle.alignment = TextAnchor.MiddleLeft;
        }

        GUI.Box(new Rect(10, 10, 420, Screen.height - 20), "CROSSHAIR ARCHITECT");
        GUILayout.BeginArea(new Rect(20, 40, 400, Screen.height - 60));

        // --- SEARCHABLE SAVE/LOAD ---
        GUILayout.Label("Preset Manager (StreamingAssets)");

        GUILayout.BeginHorizontal();
        // Text field acts as Name Input AND Search Filter
        _saveFileName = GUILayout.TextField(_saveFileName, GUILayout.Height(25));
        if (GUILayout.Button("Save", GUILayout.Width(60), GUILayout.Height(25))) SaveCurrent(_saveFileName);
        GUILayout.EndHorizontal();

        // Filter logic
        List<string> filteredFiles = _fileList.Where(x => x.ToLower().Contains(_saveFileName.ToLower())).ToList();

        // Dropdown List Area
        GUI.skin.box.margin = new RectOffset(0, 0, 0, 0);
        GUILayout.BeginVertical("box", GUILayout.Height(100));
        _fileScrollPos = GUILayout.BeginScrollView(_fileScrollPos);

        if (filteredFiles.Count == 0 && _fileList.Count > 0) GUILayout.Label("No matches found.");
        if (_fileList.Count == 0) GUILayout.Label("No saved presets.");

        foreach (string file in filteredFiles)
        {
            if (GUILayout.Button(file, _dropdownButtonStyle))
            {
                LoadSelected(file);
                // Close keyboard focus
                GUI.FocusControl(null);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.Space(10);

        // --- PARTS EDITOR ---
        if (GUILayout.Button("+ ADD LAYER", GUILayout.Height(30))) parts.Add(new CrosshairPart());

        _mainScrollPos = GUILayout.BeginScrollView(_mainScrollPos);
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            GUILayout.BeginVertical("window");

            // Header
            GUILayout.BeginHorizontal();
            p.name = GUILayout.TextField(p.name, GUILayout.Width(100));
            p.isVisible = GUILayout.Toggle(p.isVisible, "Vis");
            if (GUILayout.Button("Copy", GUILayout.Width(45)))
            {
                var copy = JsonUtility.FromJson<CrosshairPart>(JsonUtility.ToJson(p));
                copy.needsRebuild = true; parts.Insert(i + 1, copy); break;
            }
            if (GUILayout.Button("X", GUILayout.Width(25))) { parts.RemoveAt(i); break; }
            GUILayout.EndHorizontal();

            // Shape
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Shape: " + p.shape.ToString()))
            {
                p.shape = (CrosshairShape)(((int)p.shape + 1) % 4);
                p.needsRebuild = true;
            }
            bool oldOut = p.isOutline;
            p.isOutline = GUILayout.Toggle(p.isOutline, "Outline");
            if (oldOut != p.isOutline) p.needsRebuild = true;
            GUILayout.EndHorizontal();

            // Sliders
            float oldW = p.width, oldH = p.height, oldThick = p.outlineThickness;
            p.offset.x = DrawSmartField("Pos X", p.offset.x, -100, 100, ref p.useSliderX);
            p.offset.y = DrawSmartField("Pos Y", p.offset.y, -100, 100, ref p.useSliderY);
            p.width = DrawSmartField("Width", p.width, 0, 200, ref p.useSliderW);
            p.height = DrawSmartField("Height", p.height, 0, 200, ref p.useSliderH);
            p.rotation = DrawSmartField("Angle", p.rotation, 0, 360, ref p.useSliderR);
            if (p.isOutline) p.outlineThickness = DrawSmartField("Border", p.outlineThickness, 1, 50, ref p.useSliderThick);

            if (Mathf.Abs(oldThick - p.outlineThickness) > 0.01f || p.shape != CrosshairShape.Square && (Mathf.Abs(oldW - p.width) > 1f || Mathf.Abs(oldH - p.height) > 1f))
                p.needsRebuild = true;

            GUILayout.Space(5);

            // Visual Colors
            DrawColorSlider("H", ref p.hue, _hueBar);
            UpdateGradientTexture(_satBar, Color.white, Color.HSVToRGB(p.hue, 1f, 1f));
            DrawColorSlider("S", ref p.saturation, _satBar);
            UpdateGradientTexture(_valBar, Color.black, Color.HSVToRGB(p.hue, p.saturation, 1f));
            DrawColorSlider("V", ref p.value, _valBar);

            p.cachedColor = Color.HSVToRGB(p.hue, p.saturation, p.value);
            GUI.color = p.cachedColor;
            GUILayout.Box("", GUILayout.Height(6), GUILayout.ExpandWidth(true));
            GUI.color = Color.white;

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // --- GENERATION HELPERS ---
    void RebuildPartTexture(CrosshairPart p)
    {
        if (p.runtimeTexture == null) p.runtimeTexture = new Texture2D(TEX_SIZE, TEX_SIZE);
        Color[] cols = new Color[TEX_SIZE * TEX_SIZE];
        Color fill = Color.white, clear = Color.clear;

        float minDim = Mathf.Max(1, Mathf.Min(p.width, p.height));
        float texThickness = (p.outlineThickness / minDim) * TEX_SIZE;
        float center = TEX_SIZE / 2f, radius = (TEX_SIZE / 2f) - 1;

        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                bool on = false;
                Vector2 pos = new Vector2(x, y), cPos = new Vector2(center, center);
                if (p.shape == CrosshairShape.Square) on = true;
                else if (p.shape == CrosshairShape.Circle)
                {
                    float d = Vector2.Distance(pos, cPos);
                    on = p.isOutline ? (d <= radius && d > radius - texThickness) : d <= radius;
                }
                else if (p.shape == CrosshairShape.Diamond)
                {
                    float d = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    on = p.isOutline ? (d <= radius && d > radius - texThickness) : d <= radius;
                }
                else if (p.shape == CrosshairShape.Triangle)
                {
                    Vector3 b = GetBarycentric(pos, new Vector2(center, TEX_SIZE - 1), new Vector2(1, 1), new Vector2(TEX_SIZE - 1, 1));
                    bool inTri = b.x >= 0 && b.y >= 0 && b.z >= 0;
                    if (p.isOutline) { float t = texThickness / TEX_SIZE; on = inTri && (b.x < t || b.y < t || b.z < t); } else on = inTri;
                }
                cols[y * TEX_SIZE + x] = on ? fill : clear;
            }
        }
        p.runtimeTexture.SetPixels(cols); p.runtimeTexture.Apply(); p.needsRebuild = false;
    }

    void DrawProceduralSquare(Vector2 c, float w, float h, float t)
    {
        Texture2D wt = Texture2D.whiteTexture; float x = c.x - w / 2, y = c.y - h / 2, th = Mathf.Clamp(t, 0, Mathf.Min(w, h) / 2);
        GUI.DrawTexture(new Rect(x, y, w, th), wt); GUI.DrawTexture(new Rect(x, y + h - th, w, th), wt);
        GUI.DrawTexture(new Rect(x, y, th, h), wt); GUI.DrawTexture(new Rect(x + w - th, y, th, h), wt);
    }

    void DrawColorSlider(string l, ref float v, Texture2D t)
    {
        GUILayout.BeginHorizontal(); GUILayout.Label(l, GUILayout.Width(15));
        Rect r = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10), t);
        v = GUI.HorizontalSlider(r, v, 0f, 1f, _invisibleSliderStyle, GUI.skin.horizontalSliderThumb);
        GUILayout.EndHorizontal();
    }

    void UpdateGradientTexture(Texture2D t, Color s, Color e) { for (int i = 0; i < 128; i++) t.SetPixel(i, 0, Color.Lerp(s, e, i / 128f)); t.Apply(); }
    float DrawSmartField(string l, float v, float min, float max, ref bool s)
    {
        GUILayout.BeginHorizontal(); GUILayout.Label(l, GUILayout.Width(50)); float r = v;
        if (s) { r = GUILayout.HorizontalSlider(v, min, max); GUILayout.Label(r.ToString("0"), GUILayout.Width(30)); }
        else { float.TryParse(GUILayout.TextField(v.ToString("0.##")), out r); }
        s = GUILayout.Toggle(s, "S", GUILayout.Width(30)); GUILayout.EndHorizontal(); return r;
    }
    public void LoadStandardPreset() { parts.Clear(); parts.Add(new CrosshairPart { name = "Dot", shape = CrosshairShape.Circle, width = 4, height = 4, saturation = 0, value = 1 }); }
    Vector3 GetBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 v0 = b - a, v1 = c - a, v2 = p - a; float d00 = Vector2.Dot(v0, v0), d01 = Vector2.Dot(v0, v1), d11 = Vector2.Dot(v1, v1), d20 = Vector2.Dot(v2, v0), d21 = Vector2.Dot(v2, v1);
        float d = d00 * d11 - d01 * d01; float v = (d11 * d20 - d01 * d21) / d, w = (d00 * d21 - d01 * d20) / d; return new Vector3(1f - v - w, v, w);
    }
}
public static class V2Ext { public static Vector2 Rotate(this Vector2 v, float d) { float r = d * Mathf.Deg2Rad; float s = Mathf.Sin(r); float c = Mathf.Cos(r); return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y); } }