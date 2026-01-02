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
        private RectTransform tableContent;
        private TMP_Text summaryText;
        private ScrollRect scrollRect;

        // =========================
        // BUILD
        // =========================
        public void Build()
        {
            var canvasGO = new GameObject("SWT_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var root = canvasGO.transform;

            var bg = NewPanel(root, new Color(0.15f, 0.2f, 0.3f, 1f));
            Stretch(bg);

            titleText = NewText(bg.transform, "ShortWaveTrader — Python Parity", 24, TextAlignmentOptions.Left);
            Anchor(titleText.rectTransform, 16, -12, -16, -52);

            statusText = NewText(bg.transform, "Initializing…", 16, TextAlignmentOptions.Left);
            Anchor(statusText.rectTransform, 16, -48, -16, -88);

            var barBG = NewImage(bg.transform, new Color(0.25f, 0.25f, 0.25f, 1f), 18);
            Anchor(barBG.rectTransform, 16, -84, -16, -112);

            progressFill = NewImage(barBG.transform, Color.white, 22);
            progressFill.rectTransform.anchorMin = new Vector2(0, 0);
            progressFill.rectTransform.anchorMax = new Vector2(0, 1);
            progressFill.rectTransform.offsetMin = Vector2.zero;
            progressFill.rectTransform.offsetMax = Vector2.zero;

            BuildTable(bg.transform);

            var summaryPanel = NewPanel(bg.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
            Anchor(summaryPanel.GetComponent<RectTransform>(), 16, -430, -16, -16);
            summaryText = NewText(summaryPanel.transform, "", 16, TextAlignmentOptions.Left);
            Stretch(summaryText.gameObject);
            summaryText.textWrappingMode = TextWrappingModes.Normal;
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
            var row = NewText(tableContent, text, 14, TextAlignmentOptions.Left);
            row.enableWordWrapping = true;
            row.textWrappingMode = TextWrappingModes.Normal;
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 24;
            layout.minHeight = 20;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0;
        }

        public void SetSummary(string text) => summaryText.text = text;
        public string GetSummaryText() => summaryText != null ? summaryText.text : string.Empty;

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

        private TMP_Text NewText(Transform parent, string text, int size, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var go = new GameObject("TMP_Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = align;
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

        private void BuildTable(Transform parent)
        {
            var scrollGO = new GameObject("TableScroll");
            scrollGO.transform.SetParent(parent, false);
            scrollRect = scrollGO.AddComponent<ScrollRect>();
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            Anchor(scrollRT, 16, -120, -16, -440);

            var scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = new Color(0.1f, 0.12f, 0.16f, 1f);
            scrollGO.AddComponent<RectMask2D>();

            var contentGO = new GameObject("Content");
            tableContent = contentGO.AddComponent<RectTransform>();
            tableContent.SetParent(scrollGO.transform, false);
            tableContent.anchorMin = new Vector2(0, 1);
            tableContent.anchorMax = new Vector2(1, 1);
            tableContent.pivot = new Vector2(0, 1);
            tableContent.offsetMin = new Vector2(0, 0);
            tableContent.offsetMax = new Vector2(0, 0);

            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.UpperLeft;

            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = tableContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = scrollRT;
        }
    }
}
