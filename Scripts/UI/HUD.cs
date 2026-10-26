using UnityEngine;
using UnityEngine.UI;

namespace LaserLab
{
    /// <summary>
    /// Builds the whole HUD from code at runtime (no prefab or TextMeshPro needed).
    /// Resident Evil style "CONDITION: FINE / CAUTION / DANGER" vitals, flashlight battery,
    /// keycards, detection meter, messages, interaction prompt, death and escape screens.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        Font font;
        RectTransform healthFill, batteryFill, detectFill;
        Image healthFillImg, detectFillImg, damageOverlay, endOverlay;
        Text conditionText, batteryText, statusText, keycardText, messageText, promptText, detectText, endTitle, endSub;
        GameObject detectGroup;
        Interactor interactor;
        float endFade;

        static readonly Color Fine = new Color(0.25f, 0.85f, 0.4f);
        static readonly Color Caution = new Color(0.95f, 0.75f, 0.15f);
        static readonly Color Danger = new Color(0.9f, 0.15f, 0.12f);

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Start() => interactor = FindFirstObjectByType<Interactor>();

        // ------------------------------------------------------------------ build
        void Build()
        {
            var canvasGO = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            damageOverlay = MakeImage("DamageOverlay", root, new Color(0.7f, 0f, 0f, 0f));
            Stretch(damageOverlay.rectTransform);

            Vector2 topLeft = new Vector2(0, 1);
            conditionText = MakeText("Condition", root, 28, TextAnchor.LowerLeft, topLeft, new Vector2(40, -36), new Vector2(520, 36));
            healthFill = MakeBar("HealthBar", root, topLeft, new Vector2(40, -78), new Vector2(360, 16), Fine, out healthFillImg);
            batteryText = MakeText("Battery", root, 20, TextAnchor.LowerLeft, topLeft, new Vector2(40, -104), new Vector2(520, 28));
            batteryFill = MakeBar("BatteryBar", root, topLeft, new Vector2(40, -138), new Vector2(240, 8), new Color(0.95f, 0.9f, 0.65f), out _);
            statusText = MakeText("Status", root, 20, TextAnchor.UpperLeft, topLeft, new Vector2(40, -158), new Vector2(600, 120));
            statusText.color = new Color(0.55f, 0.9f, 1f);

            keycardText = MakeText("Keycards", root, 28, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -36), new Vector2(420, 40));

            detectGroup = new GameObject("Detection", typeof(RectTransform));
            detectGroup.transform.SetParent(root, false);
            Stretch((RectTransform)detectGroup.transform);
            Vector2 topCenter = new Vector2(0.5f, 1);
            detectText = MakeText("DetectText", detectGroup.transform, 26, TextAnchor.MiddleCenter, topCenter, new Vector2(0, -36), new Vector2(600, 36));
            detectFill = MakeBar("DetectBar", detectGroup.transform, topCenter, new Vector2(0, -78), new Vector2(420, 12), Caution, out detectFillImg);

            Vector2 center = new Vector2(0.5f, 0.5f);
            messageText = MakeText("Message", root, 32, TextAnchor.MiddleCenter, center, new Vector2(0, 190), new Vector2(1500, 170));
            promptText = MakeText("Prompt", root, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(900, 50));

            var cross = MakeImage("Crosshair", root, new Color(1, 1, 1, 0.55f));
            SetRect(cross.rectTransform, center, Vector2.zero, new Vector2(5, 5));

            endOverlay = MakeImage("EndOverlay", root, Color.clear);
            Stretch(endOverlay.rectTransform);
            endTitle = MakeText("EndTitle", endOverlay.transform, 110, TextAnchor.MiddleCenter, center, new Vector2(0, 70), new Vector2(1700, 170));
            endSub = MakeText("EndSub", endOverlay.transform, 30, TextAnchor.MiddleCenter, center, new Vector2(0, -80), new Vector2(1700, 200));
            endOverlay.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Health == null) return;
            var hp = gm.Health;

            // Vitals
            float hp01 = hp.Current / hp.maxHealth;
            healthFill.anchorMax = new Vector2(hp01, 1);
            Color c = hp01 > 0.6f ? Fine : hp01 > 0.3f ? Caution : Danger;
            string cond = hp01 > 0.6f ? "FINE" : hp01 > 0.3f ? "CAUTION" : "DANGER";
            healthFillImg.color = c;
            conditionText.text = $"CONDITION:  <color=#{ColorUtility.ToHtmlStringRGB(c)}>{cond}</color>";

            // Flashlight
            var fl = Flashlight.Instance;
            if (fl != null)
            {
                batteryFill.anchorMax = new Vector2(fl.Battery / fl.maxBattery, 1);
                batteryText.text = $"FLASHLIGHT [F]  {(fl.IsOn ? "ON" : "OFF")}";
            }

            // Status effects
            string status = "";
            if (hp.HasShield) status += $"LASER SHIELD  {hp.ShieldTimer:0.0}s\n";
            if (gm.SpeedBoostTimer > 0f) status += $"ADRENALINE  {gm.SpeedBoostTimer:0.0}s\n";
            if (gm.IsCrouching) status += "CROUCHING\n";
            else if (gm.State == GameState.Lab && gm.IsSprinting) status += "<color=#FFB040>RUNNING - NOISY</color>\n";
            statusText.text = status;

            // Keycards
            keycardText.text = gm.State == GameState.Corridor ? "" :
                $"KEYCARDS  {gm.KeycardsCollected}/{gm.keycardsRequired}";

            // Detection meter
            float s = Detector.MaxSuspicion();
            bool showDetect = gm.State == GameState.Lab && s > 0.01f;
            detectGroup.SetActive(showDetect);
            if (showDetect)
            {
                detectFill.anchorMax = new Vector2(s, 1);
                detectFillImg.color = Color.Lerp(Caution, Danger, s);
                detectText.text = s > 0.66f ? "!!  ALERT  !!" : "SUSPICIOUS";
                detectText.color = detectFillImg.color;
            }

            // Message
            messageText.text = gm.Message;
            var mc = messageText.color; mc.a = Mathf.Clamp01(gm.MessageTimer); messageText.color = mc;

            // Prompt
            promptText.text = (interactor != null && interactor.Target != null)
                ? $"[E]  Take {interactor.Target.displayName}" : "";

            // Red flash on damage / detection
            var dc = damageOverlay.color;
            dc.a = Mathf.Max(hp.DamageFlash * 0.35f, gm.CaughtFlash * 0.45f);
            damageOverlay.color = dc;

            // End screens
            bool ended = gm.State == GameState.Dead || gm.State == GameState.Won;
            endOverlay.gameObject.SetActive(ended);
            if (ended)
            {
                endFade = Mathf.MoveTowards(endFade, 1f, Time.deltaTime * 1.5f);
                endOverlay.color = new Color(0, 0, 0, 0.85f * endFade);
                if (gm.State == GameState.Dead)
                {
                    endTitle.text = "YOU DIED";
                    endTitle.color = new Color(0.75f, 0.05f, 0.05f, endFade);
                    endSub.text = "Restarting at the corridor entrance...";
                }
                else
                {
                    int t = Mathf.FloorToInt(gm.ElapsedTime);
                    endTitle.text = "YOU ESCAPED";
                    endTitle.color = new Color(0.85f, 0.95f, 0.9f, endFade);
                    endSub.text = $"Time  {t / 60:00}:{t % 60:00}     Times detected  {gm.TimesCaught}\n\nPress R to play again";
                }
                endSub.color = new Color(0.85f, 0.85f, 0.85f, endFade);
            }
        }

        // ------------------------------------------------------------------ helpers
        Text MakeText(string name, Transform parent, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = new Color(0.92f, 0.94f, 0.92f);
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            SetRect(t.rectTransform, anchor, pos, dim);
            return t;
        }

        Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        RectTransform MakeBar(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 dim, Color color, out Image fillImage)
        {
            var bg = MakeImage(name, parent, new Color(0, 0, 0, 0.55f));
            SetRect(bg.rectTransform, anchor, pos, dim);
            fillImage = MakeImage("Fill", bg.transform, color);
            var fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            return fill;
        }

        static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
