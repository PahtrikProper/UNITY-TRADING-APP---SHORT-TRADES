using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShortWaveTrader.UI
{
    public class RuntimeUI
    {
        private TMP_Text titleText;
        private TMP_Text statusText;
        private Image progressFill;
        private RectTransform tableRoot;
        private TMP_Text summaryText;

        // =========================
        // BUILD
        // =========================
        public void Build()
        {
            var canvasGO = new GameObject("SWT_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            var root = canvasGO.transform;

            var bg = NewPanel(root, new Color(0.15f, 0.2f, 0.3f, 1f));
            Stretch(bg);

            titleText = NewText(bg.transform, "ShortWaveTrader — Python Parity", 28);
            Anchor(titleText.rectTransform, 10, -10, -10, -50);

            statusText = NewText(bg.transform, "Initializing…", 18);
            Anchor(statusText.rectTransform, 10, -50, -10, -80);

            var barBG = NewImage(bg.transform, new Color(0.25f, 0.25f, 0.25f, 1f), 22);
            Anchor(barBG.rectTransform, 10, -90, -10, -115);

            progressFill = NewImage(barBG.transform, Color.white, 22);
            progressFill.rectTransform.anchorMin = new Vector2(0, 0);
            progressFill.rectTransform.anchorMax = new Vector2(0, 1);
            progressFill.rectTransform.offsetMin = Vector2.zero;
            progressFill.rectTransform.offsetMax = Vector2.zero;

            var tableGO = new GameObject("Table");
            tableRoot = tableGO.AddComponent<RectTransform>();
            tableRoot.SetParent(bg.transform, false);
            Anchor(tableRoot, 10, -130, -10, -420);

            summaryText = NewText(bg.transform, "", 16);
            Anchor(summaryText.rectTransform, 10, -430, -10, -600);
        }

        // =========================
        // PUBLIC API (USED BY APP)
        // =========================
        public void SetStatus(string text) => statusText.text = text;

        public void SetProgress(float pct)
        {
            progressFill.rectTransform.anchorMax =
                new Vector2(Mathf.Clamp01(pct), 1);
        }

        public void AddRow(string text)
        {
            var row = NewText(tableRoot, text, 14);
            row.textWrappingMode = TextWrappingModes.NoWrap;

            var rt = row.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(0, 20);
            rt.anchoredPosition = new Vector2(0, -tableRoot.childCount * 20);
        }

        public void SetSummary(string text) => summaryText.text = text;

        // =========================
        // HELPERS
        // =========================
        private GameObject NewPanel(Transform parent, Color c)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = c;
            return go;
        }

        private Image NewImage(Transform parent, Color c, float h)
        {
            var go = new GameObject("Image");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.rectTransform.sizeDelta = new Vector2(0, h);
            return img;
        }

        private TMP_Text NewText(Transform parent, string text, int size)
        {
            var go = new GameObject("TMP_Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Left;
            return t;
        }

        private void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Anchor(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, t);
        }
    }
}
