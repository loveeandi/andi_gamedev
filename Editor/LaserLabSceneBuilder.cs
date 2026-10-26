using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using StarterAssets;

namespace LaserLab.EditorTools
{
    /// <summary>
    /// Menu: Tools > Laser Lab > Build Scene
    /// Generates the whole level (corridor + airlock + laboratory + exit), materials,
    /// lighting, post-processing, the player (Starter Assets PlayerCapsule) and all gameplay objects,
    /// then saves it to Assets/LaserLab/Scenes/LaserLab.unity and adds it to Build Settings.
    ///
    /// World layout (metres, +Z = forward):
    ///   Corridor   x -3..3    z 0..60     (lasers, bonus panels)
    ///   Airlock    x -1.6..1.6 z 60..66   (lab checkpoint)
    ///   Lab        x -15..15  z 66..102   (stealth; partition with scanner at z 82)
    ///   Exit room  x -1.6..1.6 z 102..106
    /// </summary>
    public static class LaserLabSceneBuilder
    {
        const string RootDir = "Assets/LaserLab";
        const string MatDir = RootDir + "/Materials";
        const string TexDir = RootDir + "/Textures";
        const string SceneDir = RootDir + "/Scenes";
        const string ScenePath = SceneDir + "/LaserLab.unity";
        const int IgnoreRaycastLayer = 2;

        static readonly Dictionary<string, Material> M = new Dictionary<string, Material>();
        static Font font;

        static readonly Color ColdWhite = new Color(0.86f, 0.93f, 1f);
        static readonly Color LabWhite = new Color(0.78f, 0.9f, 0.88f);
        static readonly Color HintCyan = new Color(0.35f, 1f, 0.95f);
        static readonly Color SignGreen = new Color(0.65f, 1f, 0.8f);
        static readonly Color SignRed = new Color(1f, 0.35f, 0.3f);

        // =====================================================================================
        [MenuItem("Tools/Laser Lab/Build Scene")]
        public static void BuildScene()
        {
            string prefabPath = FindPlayerPrefab();
            if (prefabPath == null)
            {
                EditorUtility.DisplayDialog("Laser Lab",
                    "Could not find the Starter Assets 'PlayerCapsule' prefab.\n\n" +
                    "Import 'Starter Assets: Character Controllers | URP' (or 'Starter Assets - FirstPerson') " +
                    "from Window > Package Manager > My Assets first, then run this again.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets", "LaserLab");
            EnsureFolder(RootDir, "Materials");
            EnsureFolder(RootDir, "Textures");
            EnsureFolder(RootDir, "Scenes");

            int playerLayer = EnsureLayer("Player");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMaterials();
            SetupEnvironment();

            var env = new GameObject("Environment").transform;
            BuildCorridor(env);
            BuildAirlock(env);
            BuildLab(env);
            BuildExitRoom(env);

            var player = CreatePlayer(prefabPath, playerLayer);

            var systems = new GameObject("Systems");
            var gm = systems.AddComponent<GameManager>();
            var respawn = new GameObject("LabRespawnPoint").transform;
            respawn.SetParent(systems.transform);
            respawn.SetPositionAndRotation(new Vector3(0f, 0.05f, 64.6f), Quaternion.identity);
            gm.labRespawnPoint = respawn;
            systems.AddComponent<HUD>();

            CreatePostProcessing();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;

            EditorUtility.DisplayDialog("Laser Lab",
                "Scene built and saved to\n" + ScenePath + "\n\nPress Play to start.", "Let's go");
        }

        // =====================================================================================
        // Setup helpers
        static string FindPlayerPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("PlayerCapsule t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == "PlayerCapsule") return p;
            }
            return null;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing != -1) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var prop = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(prop.stringValue))
                {
                    prop.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[LaserLab] Created layer '{name}' at index {i}.");
                    return i;
                }
            }
            Debug.LogError("[LaserLab] No free layer slot for 'Player'. Free one up in Project Settings > Tags and Layers.");
            return 0;
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetupEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.085f, 0.085f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.04f);
            RenderSettings.fogDensity = 0.018f;
        }

        // =====================================================================================
        // Materials (URP/Lit, saved as assets so the scene keeps them)
        static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
            {
                Debug.LogWarning("[LaserLab] URP Lit shader not found - is the project using URP? Falling back to Standard.");
                s = Shader.Find("Standard");
            }
            return s;
        }

        static void SetColor(Material m, string prop, Color c) { if (m.HasProperty(prop)) m.SetColor(prop, c); }
        static void SetFloat(Material m, string prop, float f) { if (m.HasProperty(prop)) m.SetFloat(prop, f); }

        static Material Mat(string name, Color color, float smooth = 0.5f, float metal = 0f,
                            Color? emission = null, Texture2D tex = null, Vector2? tiling = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = LitShader();

            SetColor(mat, "_BaseColor", color);
            SetColor(mat, "_Color", color);
            SetFloat(mat, "_Smoothness", smooth);
            SetFloat(mat, "_Glossiness", smooth);
            SetFloat(mat, "_Metallic", metal);

            if (tex != null)
            {
                Vector2 t = tiling ?? Vector2.one;
                foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                {
                    if (!mat.HasProperty(prop)) continue;
                    mat.SetTexture(prop, tex);
                    mat.SetTextureScale(prop, t);
                }
            }

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                SetColor(mat, "_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                SetColor(mat, "_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);
            M[name] = mat;
            return mat;
        }

        static Texture2D TileTexture()
        {
            string path = TexDir + "/LabTile.png";
            if (!File.Exists(path))
            {
                const int size = 256, grout = 5;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                var groutCol = new Color(0.52f, 0.56f, 0.56f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool g = x < grout || y < grout;
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.04f;
                    tex.SetPixel(x, y, g ? groutCol : new Color(0.93f - n, 0.95f - n, 0.94f - n));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void CreateMaterials()
        {
            var tile = TileTexture();

            Mat("FloorCorridor", Color.white, 0.82f, 0f, null, tile, new Vector2(6, 60));
            Mat("FloorLab", new Color(0.9f, 0.93f, 0.92f), 0.85f, 0f, null, tile, new Vector2(30, 36));
            Mat("FloorSmall", Color.white, 0.82f, 0f, null, tile, new Vector2(3, 6));

            Mat("WallPaint", new Color(0.80f, 0.84f, 0.82f), 0.3f);
            Mat("WallLab", new Color(0.62f, 0.69f, 0.67f), 0.3f);
            Mat("Trim", new Color(0.28f, 0.36f, 0.36f), 0.45f);
            Mat("Ceiling", new Color(0.72f, 0.74f, 0.74f), 0.2f);
            Mat("Steel", new Color(0.62f, 0.64f, 0.67f), 0.65f, 0.85f);
            Mat("DarkSteel", new Color(0.16f, 0.17f, 0.19f), 0.5f, 0.7f);
            Mat("WhitePlastic", new Color(0.92f, 0.93f, 0.94f), 0.6f);
            Mat("HazardYellow", new Color(0.9f, 0.72f, 0.08f), 0.4f);

            Mat("LightStrip", Color.white, 0.5f, 0f, new Color(0.85f, 0.92f, 1f) * 2.5f);
            Mat("LightStripDead", new Color(0.45f, 0.47f, 0.48f), 0.5f);
            Mat("Laser", new Color(1f, 0.1f, 0.1f), 0.5f, 0f, new Color(1f, 0.05f, 0.03f) * 10f);
            Mat("LaserEmitter", new Color(0.5f, 0.05f, 0.05f), 0.5f, 0f, new Color(1f, 0.05f, 0.03f) * 3f);

            Mat("BonusHeal", new Color(0.1f, 0.5f, 0.2f), 0.7f, 0f, new Color(0.1f, 1f, 0.3f) * 2.5f);
            Mat("BonusShield", new Color(0.1f, 0.3f, 0.6f), 0.7f, 0f, new Color(0.1f, 0.5f, 1f) * 2.5f);
            Mat("BonusSpeed", new Color(0.6f, 0.5f, 0.1f), 0.7f, 0f, new Color(1f, 0.8f, 0.1f) * 2.5f);

            Mat("Liquid", new Color(0.1f, 0.5f, 0.25f), 0.9f, 0f, new Color(0.15f, 0.9f, 0.4f) * 1.2f);
            Mat("EmergencyRed", new Color(0.5f, 0.05f, 0.05f), 0.5f, 0f, new Color(1f, 0.08f, 0.05f) * 3f);
            Mat("Keycard", new Color(0.1f, 0.5f, 0.9f), 0.7f, 0f, new Color(0.2f, 0.7f, 1f) * 4f);
            Mat("GuardEye", new Color(0.5f, 0.05f, 0.05f), 0.8f, 0f, new Color(1f, 0.05f, 0.03f) * 6f);
            Mat("CameraLens", new Color(0.5f, 0.1f, 0.05f), 0.9f, 0f, new Color(1f, 0.3f, 0.1f) * 4f);
            Mat("Scanner", new Color(0.2f, 0.8f, 0.7f), 0.5f, 0f, new Color(0.2f, 1f, 0.85f) * 5f);
            Mat("StatusLight", new Color(0.5f, 0.05f, 0.05f), 0.5f, 0f, new Color(1f, 0.08f, 0.05f) * 4f);
            Mat("ScannerGlow", new Color(0.2f, 0.8f, 0.7f), 0.5f, 0f, new Color(0.2f, 1f, 0.85f) * 2f);
            Mat("ArmTip", new Color(0.9f, 0.5f, 0.05f), 0.5f, 0f, new Color(1f, 0.45f, 0.05f) * 3f);

            AssetDatabase.SaveAssets();
        }

        // =====================================================================================
        // Geometry helpers
        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string mat, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, true);
            g.transform.position = center;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = M[mat];
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static GameObject Cyl(string name, Transform parent, Vector3 center, float radius, float height, string mat, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, true);
            g.transform.position = center;
            g.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            g.GetComponent<Renderer>().sharedMaterial = M[mat];
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            else
            {
                // swap the default capsule collider for a mesh-accurate one
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.AddComponent<MeshCollider>().convex = true;
            }
            return g;
        }

        static GameObject Floor(string name, Transform parent, Vector3 center, Vector2 size, string mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = name;
            g.transform.SetParent(parent, true);
            g.transform.position = center;
            g.transform.localScale = new Vector3(size.x / 10f, 1f, size.y / 10f);
            g.GetComponent<Renderer>().sharedMaterial = M[mat];
            return g;
        }

        static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, true);
            g.transform.position = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        static Light SpotLight(string name, Transform parent, Vector3 localPos, Color color, float intensity, float range, float angle)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <param name="facing">Direction the reader looks in when reading the sign (into the wall).</param>
        static GameObject Sign(string name, Transform parent, string text, Vector3 pos, Vector3 facing, float charSize, Color color, bool hiddenUV = false)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, true);
            g.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing));
            var tm = g.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            g.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            if (hiddenUV)
            {
                var h = g.AddComponent<HiddenObject>();
                h.collectible = false;
                h.spin = false;
                h.lingerTime = 0.6f;
                h.displayName = "UV message";
            }
            return g;
        }

        /// <summary>A wall across X at the given Z with a centred doorway.</summary>
        static void WallWithDoor(string name, Transform p, float z, float xMin, float xMax, float height,
                                 float doorWidth, float doorHeight, string mat, float thickness = 0.2f)
        {
            float half = doorWidth * 0.5f;
            float lw = -half - xMin;
            float rw = xMax - half;
            Box(name + "_L", p, new Vector3(xMin + lw * 0.5f, height * 0.5f, z), new Vector3(lw, height, thickness), mat);
            Box(name + "_R", p, new Vector3(half + rw * 0.5f, height * 0.5f, z), new Vector3(rw, height, thickness), mat);
            Box(name + "_Top", p, new Vector3(0f, doorHeight + (height - doorHeight) * 0.5f, z),
                new Vector3(doorWidth, height - doorHeight, thickness), mat);
        }

        static void SetLayerRecursive(GameObject g, int layer)
        {
            g.layer = layer;
            foreach (Transform c in g.transform) SetLayerRecursive(c.gameObject, layer);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // =====================================================================================
        // CORRIDOR  (x -3..3, z 0..60, height 4)
        static void BuildCorridor(Transform env)
        {
            var c = new GameObject("Corridor").transform;
            c.SetParent(env);

            Floor("Floor", c, new Vector3(0, 0, 30), new Vector2(6, 60), "FloorCorridor");
            Box("Ceiling", c, new Vector3(0, 4.1f, 30), new Vector3(6.4f, 0.2f, 60.4f), "Ceiling");
            Box("Wall_L", c, new Vector3(-3.1f, 2, 30), new Vector3(0.2f, 4, 60), "WallPaint");
            Box("Wall_R", c, new Vector3(3.1f, 2, 30), new Vector3(0.2f, 4, 60), "WallPaint");
            Box("Wall_Start", c, new Vector3(0, 2, -0.1f), new Vector3(6.4f, 4, 0.2f), "WallPaint");
            WallWithDoor("Wall_End", c, 60.1f, -3.2f, 3.2f, 4f, 2.4f, 2.5f, "WallPaint");

            // wainscot trim + hand rails
            Box("Trim_L", c, new Vector3(-2.98f, 0.5f, 30), new Vector3(0.04f, 1, 60), "Trim", false);
            Box("Trim_R", c, new Vector3(2.98f, 0.5f, 30), new Vector3(0.04f, 1, 60), "Trim", false);
            Box("Trim_Start", c, new Vector3(0, 0.5f, 0.02f), new Vector3(6, 1, 0.04f), "Trim", false);
            Box("Rail_L", c, new Vector3(-2.94f, 1.05f, 30), new Vector3(0.06f, 0.06f, 60), "Steel", false);
            Box("Rail_R", c, new Vector3(2.94f, 1.05f, 30), new Vector3(0.06f, 0.06f, 60), "Steel", false);

            // clinical lighting
            for (int i = 0; i < 12; i++)
                Box($"LightStrip_{i}", c, new Vector3(0, 3.97f, 2.5f + i * 5f), new Vector3(0.45f, 0.05f, 2.6f), "LightStrip", false);
            for (int i = 0; i < 6; i++)
                PointLight($"CeilingLight_{i}", c, new Vector3(0, 3.6f, 5 + i * 10), ColdWhite, 15f, 12f);

            // laser emitter frame at the far end
            Box("Emitter_L", c, new Vector3(-2.8f, 2, 59.85f), new Vector3(0.4f, 4, 0.3f), "DarkSteel");
            Box("Emitter_R", c, new Vector3(2.8f, 2, 59.85f), new Vector3(0.4f, 4, 0.3f), "DarkSteel");
            Box("EmitterGlow_L", c, new Vector3(-2.58f, 2, 59.7f), new Vector3(0.05f, 3.6f, 0.05f), "LaserEmitter", false);
            Box("EmitterGlow_R", c, new Vector3(2.58f, 2, 59.7f), new Vector3(0.05f, 3.6f, 0.05f), "LaserEmitter", false);
            PointLight("EmitterLight", c, new Vector3(0, 2.2f, 58.8f), new Color(1f, 0.15f, 0.1f), 6f, 8f);
            Box("HazardLine", c, new Vector3(0, 0.005f, 56.5f), new Vector3(6, 0.01f, 0.3f), "HazardYellow", false);

            // signage
            Sign("Sign_Lab", c, "B1  LABORATORY", new Vector3(0, 3.2f, 59.94f), Vector3.forward, 0.045f, SignGreen);
            Sign("Sign_Warning_L", c, "CAUTION\nLASER GRID ACTIVE", new Vector3(-2.96f, 2.4f, 8f), Vector3.left, 0.03f, SignRed);
            Sign("Sign_Warning_R", c, "JUMP  /  CROUCH  /  SIDESTEP", new Vector3(2.96f, 2.4f, 20f), Vector3.right, 0.028f, SignRed);
            Sign("Sign_Panels", c, "GREEN = FIRST AID\nBLUE = SHIELD\nYELLOW = ADRENALINE", new Vector3(-2.96f, 2.4f, 28f), Vector3.left, 0.026f, SignGreen);

            // laser spawner
            var spawnerGO = new GameObject("LaserSpawner");
            spawnerGO.transform.SetParent(c);
            spawnerGO.transform.position = new Vector3(0, 0, 58.5f);
            var spawner = spawnerGO.AddComponent<LaserSpawner>();
            spawner.laserMaterial = M["Laser"];
            spawner.destroyZ = 0.3f;

            // bonus floor panels
            BonusPanelAt(c, BonusType.Heal, new Vector3(-1.8f, 0.012f, 14f));
            BonusPanelAt(c, BonusType.Shield, new Vector3(1.8f, 0.012f, 24f));
            BonusPanelAt(c, BonusType.Speed, new Vector3(0f, 0.012f, 33f));
            BonusPanelAt(c, BonusType.Heal, new Vector3(1.8f, 0.012f, 42f));
            BonusPanelAt(c, BonusType.Shield, new Vector3(-1.8f, 0.012f, 50f));
        }

        static void BonusPanelAt(Transform parent, BonusType type, Vector3 pos)
        {
            string mat = type == BonusType.Heal ? "BonusHeal" : type == BonusType.Shield ? "BonusShield" : "BonusSpeed";
            Color col = type == BonusType.Heal ? new Color(0.2f, 1f, 0.4f) : type == BonusType.Shield ? new Color(0.2f, 0.55f, 1f) : new Color(1f, 0.8f, 0.2f);

            var frame = Box($"BonusFrame_{type}", parent, pos - new Vector3(0, 0.004f, 0), new Vector3(1.8f, 0.02f, 1.8f), "DarkSteel", false);
            frame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var panel = Box($"BonusPanel_{type}", parent, pos, new Vector3(1.6f, 0.025f, 1.6f), mat, false);
            panel.AddComponent<BonusPanel>().type = type;
            var l = PointLight("Glow", panel.transform, pos + Vector3.up * 0.4f, col, 2f, 2.5f);
            l.transform.SetParent(panel.transform, true);
        }

        // =====================================================================================
        // AIRLOCK (x -1.6..1.6, z 60..66, height 3) - lab checkpoint
        static void BuildAirlock(Transform env)
        {
            var a = new GameObject("Airlock").transform;
            a.SetParent(env);

            Floor("Floor", a, new Vector3(0, 0.002f, 63f), new Vector2(3.2f, 6.2f), "FloorSmall");
            Box("Ceiling", a, new Vector3(0, 3.1f, 63f), new Vector3(3.4f, 0.2f, 6f), "Ceiling");
            Box("Wall_L", a, new Vector3(-1.7f, 1.5f, 63f), new Vector3(0.2f, 3, 6f), "WallPaint");
            Box("Wall_R", a, new Vector3(1.7f, 1.5f, 63f), new Vector3(0.2f, 3, 6f), "WallPaint");
            Box("Vent_L", a, new Vector3(-1.58f, 2.1f, 63f), new Vector3(0.05f, 0.6f, 1.6f), "DarkSteel", false);
            Box("Vent_R", a, new Vector3(1.58f, 2.1f, 63f), new Vector3(0.05f, 0.6f, 1.6f), "DarkSteel", false);
            Box("Stripe_In", a, new Vector3(0, 0.006f, 60.5f), new Vector3(3.2f, 0.01f, 0.25f), "HazardYellow", false);
            Box("Stripe_Out", a, new Vector3(0, 0.006f, 65.7f), new Vector3(3.2f, 0.01f, 0.25f), "HazardYellow", false);
            Box("LightStrip", a, new Vector3(0, 2.97f, 63f), new Vector3(0.4f, 0.05f, 2.5f), "LightStrip", false);
            PointLight("AirlockLight", a, new Vector3(0, 2.7f, 63f), new Color(0.75f, 1f, 0.85f), 5f, 6f);

            Sign("Sign_Decon", a, "DECONTAMINATION\nCOMPLETE", new Vector3(-1.57f, 2.15f, 61.6f), Vector3.left, 0.025f, SignGreen);
            Sign("Sign_Biohazard", a, "B1 BIOHAZARD RESEARCH\nAUTHORISED STAFF ONLY", new Vector3(1.57f, 2.15f, 61.6f), Vector3.right, 0.022f, SignRed);

            var zone = new GameObject("LabEntranceZone");
            zone.transform.SetParent(a);
            zone.transform.position = new Vector3(0, 1.2f, 63.2f);
            var z = zone.AddComponent<ZoneTrigger>();
            z.type = ZoneType.LabEntrance;
            z.size = new Vector3(3.2f, 2.4f, 1.5f);
        }

        // =====================================================================================
        // LABORATORY (x -15..15, z 66..102, height 5)
        static void BuildLab(Transform env)
        {
            var lab = new GameObject("Laboratory").transform;
            lab.SetParent(env);

            // shell
            Floor("Floor", lab, new Vector3(0, 0, 84f), new Vector2(30, 36.4f), "FloorLab");
            Box("Ceiling", lab, new Vector3(0, 5.1f, 84f), new Vector3(30.4f, 0.2f, 36.4f), "Ceiling");
            Box("Wall_L", lab, new Vector3(-15.1f, 2.5f, 84f), new Vector3(0.2f, 5, 36.4f), "WallLab");
            Box("Wall_R", lab, new Vector3(15.1f, 2.5f, 84f), new Vector3(0.2f, 5, 36.4f), "WallLab");
            WallWithDoor("Wall_Front", lab, 66f, -15.2f, 15.2f, 5f, 2.4f, 2.5f, "WallLab");
            WallWithDoor("Wall_Back", lab, 102f, -15.2f, 15.2f, 5f, 2.4f, 2.5f, "WallLab");

            // trims
            Box("Trim_L", lab, new Vector3(-14.98f, 0.5f, 84f), new Vector3(0.04f, 1, 36f), "Trim", false);
            Box("Trim_R", lab, new Vector3(14.98f, 0.5f, 84f), new Vector3(0.04f, 1, 36f), "Trim", false);
            foreach (float zf in new[] { 66.12f, 101.88f })
            {
                Box("Trim_FB_L", lab, new Vector3(-8.1f, 0.5f, zf), new Vector3(13.8f, 1, 0.04f), "Trim", false);
                Box("Trim_FB_R", lab, new Vector3(8.1f, 0.5f, zf), new Vector3(13.8f, 1, 0.04f), "Trim", false);
            }

            // partition at z=82 with the scanner doorway (x -3..3)
            Box("Partition_L", lab, new Vector3(-9f, 1.5f, 82f), new Vector3(12f, 3f, 0.3f), "WallPaint");
            Box("Partition_R", lab, new Vector3(9f, 1.5f, 82f), new Vector3(12f, 3f, 0.3f), "WallPaint");
            Box("ScannerPost_L", lab, new Vector3(-3.1f, 1.6f, 82f), new Vector3(0.25f, 3.2f, 0.5f), "DarkSteel");
            Box("ScannerPost_R", lab, new Vector3(3.1f, 1.6f, 82f), new Vector3(0.25f, 3.2f, 0.5f), "DarkSteel");
            Box("ScannerBeam", lab, new Vector3(0f, 3.3f, 82f), new Vector3(6.45f, 0.25f, 0.5f), "DarkSteel");
            Box("ScannerGlow_L", lab, new Vector3(-2.96f, 1.6f, 82f), new Vector3(0.04f, 3f, 0.3f), "ScannerGlow", false);
            Box("ScannerGlow_R", lab, new Vector3(2.96f, 1.6f, 82f), new Vector3(0.04f, 3f, 0.3f), "ScannerGlow", false);
            Sign("Sign_Scanner", lab, "DECON SCANNER\nDO NOT CROSS DURING SWEEP", new Vector3(0f, 3.75f, 81.7f), Vector3.forward, 0.022f, SignRed);

            var scanner = new GameObject("ScannerGate");
            scanner.transform.SetParent(lab);
            scanner.transform.position = new Vector3(0f, 1.5f, 82f);
            var sg = scanner.AddComponent<ScannerGate>();
            sg.pointA = new Vector3(-1.7f, 1.5f, 82f);
            sg.pointB = new Vector3(1.7f, 1.5f, 82f);
            sg.halfExtents = new Vector3(1.3f, 1.5f, 0.2f);
            var sheet = Box("ScannerSheet", scanner.transform, scanner.transform.position, new Vector3(2.6f, 3f, 0.05f), "Scanner", false);
            sheet.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight("ScannerLight", scanner.transform, scanner.transform.position, new Color(0.3f, 1f, 0.9f), 4f, 4f);
            SetLayerRecursive(scanner, IgnoreRaycastLayer);

            // ---------- SECTION 1 (z 66..82)
            foreach (float tz in new[] { 70f, 74.5f, 79f }) SpecimenTank(lab, new Vector3(-13.6f, 0f, tz));

            Bench(lab, new Vector3(-4.5f, 0f, 72f), new Vector2(4f, 1.2f));
            Bench(lab, new Vector3(4.5f, 0f, 72f), new Vector2(4f, 1.2f));
            Bench(lab, new Vector3(0f, 0f, 76f), new Vector2(5f, 1.2f));
            Bench(lab, new Vector3(-6.5f, 0f, 77.2f), new Vector2(3f, 1.2f));

            Locker(lab, new Vector3(14.45f, 0f, 72f));
            Locker(lab, new Vector3(14.45f, 0f, 77f));

            Box("LightStrip_A", lab, new Vector3(-6f, 4.97f, 72f), new Vector3(0.45f, 0.05f, 2.6f), "LightStrip", false);
            Box("LightStrip_B", lab, new Vector3(6f, 4.97f, 77f), new Vector3(0.45f, 0.05f, 2.6f), "LightStrip", false);
            Box("LightStrip_Dead_A", lab, new Vector3(6f, 4.97f, 70f), new Vector3(0.45f, 0.05f, 2.6f), "LightStripDead", false);
            Box("LightStrip_Dead_B", lab, new Vector3(-6f, 4.97f, 79f), new Vector3(0.45f, 0.05f, 2.6f), "LightStripDead", false);
            PointLight("LabLight_A", lab, new Vector3(-6f, 4.6f, 72f), LabWhite, 6f, 9f, true);
            PointLight("LabLight_B", lab, new Vector3(6f, 4.6f, 77f), LabWhite, 6f, 9f).gameObject.AddComponent<FlickerLight>();

            CreateGuard("SecurityDroid_A", lab, new[]
            {
                new Vector3(-10f, 0f, 69f), new Vector3(10f, 0f, 69f),
                new Vector3(10f, 0f, 80f), new Vector3(-10f, 0f, 80f)
            });
            CreateSecurityCamera("SecurityCamera_A", lab, new Vector3(14.8f, 4.2f, 67.4f), -60f, 25f);

            Keycard(lab, 1, new Vector3(-13.8f, 0.4f, 76.75f));
            Sign("UV_Hint_1", lab, "KEYCARD 1:\nBETWEEN THE SPECIMEN TANKS", new Vector3(-9f, 2.1f, 81.83f), Vector3.forward, 0.03f, HintCyan, true);

            // ---------- SECTION 2 (z 82..102)
            CreateGuard("SecurityDroid_B", lab, new[]
            {
                new Vector3(-11f, 0f, 86f), new Vector3(-11f, 0f, 99f),
                new Vector3(-5f, 0f, 99f), new Vector3(-5f, 0f, 86f)
            });
            CreateSecurityCamera("SecurityCamera_B", lab, new Vector3(-14.8f, 4.2f, 100.8f), 126f, 25f);
            CreateSweepingArm(lab, new Vector3(8f, 0f, 92f));

            Bench(lab, new Vector3(-8f, 0f, 92.5f), new Vector2(2.4f, 1.2f));
            Shelf(lab, new Vector3(1.5f, 0f, 88f), new Vector3(3.5f, 2.2f, 0.6f));
            Shelf(lab, new Vector3(-1.5f, 0f, 94.5f), new Vector3(3.5f, 2.2f, 0.6f));
            Box("ColdStorage", lab, new Vector3(-14.3f, 0.6f, 92f), new Vector3(1.2f, 1.2f, 2.2f), "WhitePlastic");
            Box("ColdStorage_Lid", lab, new Vector3(-14.3f, 1.22f, 92f), new Vector3(1.25f, 0.04f, 2.25f), "Steel", false);

            Box("LightStrip_C", lab, new Vector3(-8f, 4.97f, 92f), new Vector3(0.45f, 0.05f, 2.6f), "LightStrip", false);
            Box("LightStrip_D", lab, new Vector3(8f, 4.97f, 92f), new Vector3(0.45f, 0.05f, 2.6f), "LightStrip", false);
            Box("LightStrip_Dead_C", lab, new Vector3(0f, 4.97f, 86f), new Vector3(0.45f, 0.05f, 2.6f), "LightStripDead", false);
            PointLight("LabLight_C", lab, new Vector3(-8f, 4.6f, 92f), LabWhite, 6f, 9f, true);
            PointLight("LabLight_D", lab, new Vector3(8f, 4.6f, 92f), LabWhite, 6f, 9f).gameObject.AddComponent<FlickerLight>();
            Box("EmergencyLamp", lab, new Vector3(0f, 4.9f, 100.5f), new Vector3(0.6f, 0.2f, 0.3f), "EmergencyRed", false);
            PointLight("EmergencyLight", lab, new Vector3(0f, 4.4f, 100.5f), new Color(1f, 0.12f, 0.08f), 6f, 10f);

            Keycard(lab, 2, new Vector3(8f, 1.25f, 92f));              // on top of the sterilizer hub
            Keycard(lab, 3, new Vector3(-14.25f, 1.4f, 92.4f));        // on the cold storage unit
            Sign("UV_Hint_2", lab, "KEYCARD 2:\nON THE STERILIZER HUB", new Vector3(14.88f, 2.2f, 95f), Vector3.right, 0.03f, HintCyan, true);
            Sign("UV_Hint_3", lab, "KEYCARD 3:\nON THE COLD STORAGE UNIT", new Vector3(-9f, 2.2f, 101.86f), Vector3.forward, 0.03f, HintCyan, true);
            Sign("Sign_Arm", lab, "AUTOMATED STERILIZER\nKEEP CLEAR", new Vector3(14.88f, 3.4f, 88f), Vector3.right, 0.024f, SignRed);

            // exit door
            Sign("Sign_Exit", lab, "EMERGENCY EXIT", new Vector3(0f, 3.4f, 101.86f), Vector3.forward, 0.035f, SignGreen);
            var door = Box("ExitDoor", lab, new Vector3(0f, 1.25f, 102f), new Vector3(2.4f, 2.5f, 0.12f), "DarkSteel");
            var status = Box("ExitStatusLamp", lab, new Vector3(0f, 2.75f, 101.86f), new Vector3(0.5f, 0.12f, 0.06f), "StatusLight", false);
            var statusLight = PointLight("ExitStatusLight", lab, new Vector3(0f, 2.7f, 101.4f), Color.red, 4f, 4f);
            var ed = door.AddComponent<ExitDoor>();
            ed.statusLight = statusLight;
            ed.statusRenderer = status.GetComponent<Renderer>();

            var hint = new GameObject("ExitLockedZone");
            hint.transform.SetParent(lab);
            hint.transform.position = new Vector3(0f, 1.2f, 100.9f);
            var hz = hint.AddComponent<ZoneTrigger>();
            hz.type = ZoneType.LockedDoorHint;
            hz.size = new Vector3(3f, 2.4f, 1.6f);
        }

        static void SpecimenTank(Transform parent, Vector3 basePos)
        {
            var t = new GameObject("SpecimenTank").transform;
            t.SetParent(parent);
            t.position = basePos;
            Cyl("Base", t, basePos + Vector3.up * 0.15f, 0.75f, 0.3f, "Steel");
            Cyl("Liquid", t, basePos + Vector3.up * 1.45f, 0.6f, 2.3f, "Liquid");
            Cyl("Cap", t, basePos + Vector3.up * 2.75f, 0.75f, 0.3f, "Steel");
            Cyl("Pipe", t, basePos + Vector3.up * 3.95f, 0.1f, 2.1f, "DarkSteel", false);
            PointLight("TankGlow", t, basePos + new Vector3(1f, 1.5f, 0f), new Color(0.3f, 1f, 0.5f), 3f, 3.5f);
        }

        static void Bench(Transform parent, Vector3 basePos, Vector2 size)
        {
            var b = new GameObject("LabBench").transform;
            b.SetParent(parent);
            b.position = basePos;
            Box("Body", b, basePos + Vector3.up * 0.46f, new Vector3(size.x, 0.92f, size.y), "WhitePlastic");
            Box("Top", b, basePos + Vector3.up * 0.96f, new Vector3(size.x + 0.1f, 0.08f, size.y + 0.1f), "Steel");
            Box("Kick", b, basePos + Vector3.up * 0.05f, new Vector3(size.x - 0.1f, 0.1f, size.y + 0.02f), "Trim", false);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * size.x * 0.3f;
                Cyl("Beaker", b, basePos + new Vector3(x, 1.09f, (i % 2 == 0 ? 0.2f : -0.2f)), 0.06f, 0.18f, "Liquid", false);
            }
            Box("Microscope", b, basePos + new Vector3(size.x * 0.35f, 1.15f, 0f), new Vector3(0.25f, 0.3f, 0.3f), "DarkSteel", false);
        }

        static void Locker(Transform parent, Vector3 basePos)
        {
            var l = new GameObject("Locker").transform;
            l.SetParent(parent);
            l.position = basePos;
            Box("Body", l, basePos + Vector3.up * 1.1f, new Vector3(1f, 2.2f, 1.6f), "WhitePlastic");
            Box("Seam", l, basePos + new Vector3(-0.505f, 1.1f, 0f), new Vector3(0.01f, 2.1f, 0.02f), "Trim", false);
            Box("Handle", l, basePos + new Vector3(-0.52f, 1.2f, 0.12f), new Vector3(0.03f, 0.3f, 0.04f), "Steel", false);
        }

        static void Shelf(Transform parent, Vector3 basePos, Vector3 size)
        {
            var s = new GameObject("Shelving").transform;
            s.SetParent(parent);
            s.position = basePos;
            Box("Frame", s, basePos + Vector3.up * size.y * 0.5f, size, "Steel");
            for (int i = 0; i < 3; i++)
            {
                float y = 0.5f + i * 0.65f;
                Box("Crate", s, basePos + new Vector3(-size.x * 0.25f + i * 0.4f, y + 0.15f, -size.z * 0.5f - 0.01f),
                    new Vector3(0.5f, 0.3f, 0.02f), i == 1 ? "HazardYellow" : "WhitePlastic", false);
            }
        }

        static void Keycard(Transform parent, int n, Vector3 pos)
        {
            var root = new GameObject($"HiddenKeycard_{n}");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            Box("Card", root.transform, pos, new Vector3(0.32f, 0.2f, 0.02f), "Keycard", false);
            Box("Stripe", root.transform, pos + new Vector3(0f, 0.05f, -0.012f), new Vector3(0.32f, 0.04f, 0.005f), "DarkSteel", false);
            PointLight("Glow", root.transform, pos, new Color(0.3f, 0.75f, 1f), 1.5f, 1.5f);
            var h = root.AddComponent<HiddenObject>();
            h.collectible = true;
            h.displayName = "Keycard";
        }

        // =====================================================================================
        // EXIT ROOM (x -1.6..1.6, z 102..106)
        static void BuildExitRoom(Transform env)
        {
            var e = new GameObject("ExitRoom").transform;
            e.SetParent(env);
            Floor("Floor", e, new Vector3(0, 0.002f, 104.1f), new Vector2(3.2f, 4.2f), "FloorSmall");
            Box("Ceiling", e, new Vector3(0, 3.1f, 104.1f), new Vector3(3.4f, 0.2f, 4.2f), "Ceiling");
            Box("Wall_L", e, new Vector3(-1.7f, 1.5f, 104.1f), new Vector3(0.2f, 3, 4f), "WallPaint");
            Box("Wall_R", e, new Vector3(1.7f, 1.5f, 104.1f), new Vector3(0.2f, 3, 4f), "WallPaint");
            Box("Wall_End", e, new Vector3(0, 1.5f, 106.2f), new Vector3(3.6f, 3.2f, 0.2f), "WallPaint");
            PointLight("ExitLight", e, new Vector3(0, 2.6f, 104f), new Color(0.6f, 1f, 0.7f), 6f, 6f);
            Sign("Sign_Elevator", e, "SURFACE ELEVATOR", new Vector3(0, 2.2f, 106.08f), Vector3.forward, 0.03f, SignGreen);

            var exit = new GameObject("ExitZone");
            exit.transform.SetParent(e);
            exit.transform.position = new Vector3(0, 1.2f, 104.5f);
            var z = exit.AddComponent<ZoneTrigger>();
            z.type = ZoneType.Exit;
            z.size = new Vector3(3f, 2.4f, 2f);
        }

        // =====================================================================================
        // HAZARDS
        static void CreateGuard(string name, Transform parent, Vector3[] points)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(points[0], Quaternion.LookRotation(points[1] - points[0]));
            Transform r = root.transform;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(r, false);
            body.transform.localPosition = new Vector3(0, 0.75f, 0);
            body.transform.localScale = new Vector3(0.7f, 0.75f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = M["WhitePlastic"];

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(r, false);
            head.transform.localPosition = new Vector3(0, 1.75f, 0);
            head.transform.localScale = Vector3.one * 0.55f;
            head.GetComponent<Renderer>().sharedMaterial = M["DarkSteel"];

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor";
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.transform.SetParent(r, false);
            visor.transform.localPosition = new Vector3(0, 1.78f, 0.24f);
            visor.transform.localScale = new Vector3(0.4f, 0.1f, 0.08f);
            visor.GetComponent<Renderer>().sharedMaterial = M["GuardEye"];

            var eye = new GameObject("Eye").transform;
            eye.SetParent(r, false);
            eye.localPosition = new Vector3(0, 1.78f, 0.32f);
            eye.localRotation = Quaternion.Euler(10f, 0f, 0f);

            var vision = SpotLight("VisionLight", eye, Vector3.zero, new Color(1f, 0.96f, 0.85f), 8f, 9f, 70f);

            var guard = root.AddComponent<PatrolGuard>();
            guard.displayName = "a security droid";
            guard.waypoints = points;
            guard.eye = eye;
            guard.visionLight = vision;

            SetLayerRecursive(root, IgnoreRaycastLayer);
        }

        static void CreateSecurityCamera(string name, Transform parent, Vector3 mountPos, float yaw, float pitch)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = mountPos;
            Transform r = root.transform;

            Box("Mount", r, mountPos, new Vector3(0.2f, 0.2f, 0.2f), "DarkSteel", false);

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(r, false);
            pivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Housing";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(pivot, false);
            body.transform.localPosition = new Vector3(0, 0, 0.3f);
            body.transform.localScale = new Vector3(0.25f, 0.22f, 0.5f);
            body.GetComponent<Renderer>().sharedMaterial = M["WhitePlastic"];

            var lensVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lensVis.name = "LensGlass";
            Object.DestroyImmediate(lensVis.GetComponent<Collider>());
            lensVis.transform.SetParent(pivot, false);
            lensVis.transform.localPosition = new Vector3(0, 0, 0.56f);
            lensVis.transform.localScale = new Vector3(0.12f, 0.12f, 0.03f);
            lensVis.GetComponent<Renderer>().sharedMaterial = M["CameraLens"];

            var lens = new GameObject("Lens").transform;
            lens.SetParent(pivot, false);
            lens.localPosition = new Vector3(0, 0, 0.62f);

            var vision = SpotLight("VisionLight", lens, Vector3.zero, new Color(1f, 0.85f, 0.6f), 10f, 14f, 38f);

            var cam = root.AddComponent<SecurityCamera>();
            cam.displayName = "a security camera";
            cam.pivot = pivot;
            cam.lens = lens;
            cam.visionLight = vision;

            SetLayerRecursive(root, IgnoreRaycastLayer);
        }

        static void CreateSweepingArm(Transform parent, Vector3 pos)
        {
            Cyl("SterilizerHazardZone", parent, pos + Vector3.up * 0.004f, 4.7f, 0.006f, "Trim", false)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cyl("SterilizerHub", parent, pos + Vector3.up * 0.5f, 0.45f, 1f, "DarkSteel");

            var root = new GameObject("SterilizerArm");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            var arm = root.AddComponent<SweepingArm>();

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Arm";
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(root.transform, false);
            float len = arm.armLength - arm.innerRadius;
            beam.transform.localPosition = new Vector3(0, arm.armHeight, arm.innerRadius + len * 0.5f);
            beam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            beam.transform.localScale = new Vector3(arm.armRadius * 2f, len * 0.5f, arm.armRadius * 2f);
            beam.GetComponent<Renderer>().sharedMaterial = M["Steel"];

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            Object.DestroyImmediate(tip.GetComponent<Collider>());
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0, arm.armHeight, arm.armLength);
            tip.transform.localScale = Vector3.one * 0.45f;
            tip.GetComponent<Renderer>().sharedMaterial = M["ArmTip"];

            SetLayerRecursive(root, IgnoreRaycastLayer);
        }

        // =====================================================================================
        // PLAYER
        static GameObject CreatePlayer(string prefabPath, int playerLayer)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Player";
            player.tag = "Player";
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 2f), Quaternion.identity);

            var camRoot = FindDeep(player.transform, "PlayerCameraRoot");
            if (camRoot == null)
            {
                Debug.LogError("[LaserLab] 'PlayerCameraRoot' not found on the PlayerCapsule prefab. Camera attached to the player root instead.");
                camRoot = player.transform;
            }

            // A plain camera parented to the camera root: the FirstPersonController already
            // pitches that root and yaws the player, so no Cinemachine rig is needed.
            var camGO = new GameObject("MainCamera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(camRoot, false);
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGO.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGO.AddComponent<Interactor>();

            var flGO = new GameObject("Flashlight");
            flGO.transform.SetParent(camGO.transform, false);
            flGO.transform.localPosition = new Vector3(0.2f, -0.15f, 0.1f);
            var spot = flGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 20f;
            spot.spotAngle = 55f;
            spot.innerSpotAngle = 28f;
            spot.intensity = 25f;
            spot.color = new Color(1f, 0.96f, 0.88f);
            spot.shadows = LightShadows.Soft;
            flGO.AddComponent<Flashlight>();

            player.AddComponent<PlayerHealth>();
            var crouch = player.AddComponent<PlayerCrouch>();
            crouch.cameraRoot = camRoot;

            SetLayerRecursive(player, playerLayer);

            // Make sure the controller's ground check never detects the player's own collider.
            var fpc = player.GetComponent<FirstPersonController>();
            if (fpc != null) fpc.GroundLayers = Physics.DefaultRaycastLayers & ~(1 << playerLayer);
            return player;
        }

        // =====================================================================================
        // POST PROCESSING (bloom for lasers/strips, cold desaturated grade, vignette)
        static void CreatePostProcessing()
        {
            string path = RootDir + "/LaserLabVolumeProfile.asset";
            AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);

            var grade = profile.Add<ColorAdjustments>();
            grade.saturation.Override(-18f);
            grade.contrast.Override(12f);
            grade.postExposure.Override(0.2f);
            grade.colorFilter.Override(new Color(0.93f, 1f, 0.97f));

            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProcessVolume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }
    }
}
