using System.Collections.Generic;
using EgressLab.Decision;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EgressLab.Simulation
{
    /// <summary>
    /// Overlay built entirely in code so the sample does not depend on a prefab or a font asset.
    /// </summary>
    public sealed class SimulationHud : MonoBehaviour
    {
        EvacuationBootstrap _bootstrap;
        Text _clock;
        Text _mode;
        Text _evacuated;
        Text _caught;
        Text _inside;
        Text _headline;
        Text _comparison;
        Text _status;
        Text _agentValue;
        Text _spreadValue;
        Image _modeChip;
        Button _nearestButton;
        Button _supportButton;
        readonly ExitRow[] _rows = new ExitRow[4];

        public static SimulationHud Create(EvacuationBootstrap bootstrap)
        {
            var canvasGo = new GameObject("EgressCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var hud = canvasGo.AddComponent<SimulationHud>();
            hud._bootstrap = bootstrap;
            hud.Build(canvasGo.transform);
            return hud;
        }

        public void DestroyHud()
        {
            Destroy(gameObject);
        }

        void Build(Transform root)
        {
            RectTransform top = Ui.Panel(root, "Top", Palette.Panel);
            Ui.Anchor(top, 0f, 1f, 1f, 1f, new Vector2(0f, -54f), new Vector2(0f, 0f));
            _clock = Ui.Text(top, 22, FontStyle.Bold, TextAnchor.MiddleRight, Palette.Ink);
            Ui.Place(_clock.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, -16f), new Vector2(-16f, 16f));
            Text title = Ui.Text(top, 20, FontStyle.Bold, TextAnchor.MiddleLeft, Palette.Ink);
            title.text = "EGRESS LAB";
            Ui.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -18f), new Vector2(210f, 18f));
            Text subtitle = Ui.Text(top, 14, FontStyle.Normal, TextAnchor.MiddleLeft, Palette.Muted);
            subtitle.text = "Evacuation decision support";
            Ui.Place(subtitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, -16f), new Vector2(520f, 16f));
            _modeChip = Ui.Panel(top, "Mode", Palette.Amber).GetComponent<Image>();
            Ui.Place(_modeChip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, -16f), new Vector2(90f, 16f));
            _mode = Ui.Text(_modeChip.rectTransform, 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.1f, 0.08f, 0.04f, 1f));
            Ui.Stretch(_mode.rectTransform, 6f, 6f, 2f, 2f);

            RectTransform left = Ui.Panel(root, "Metrics", Palette.Panel);
            Ui.Anchor(left, 0f, 1f, 0f, 1f, new Vector2(16f, -430f), new Vector2(360f, -70f));
            var leftLayout = left.gameObject.AddComponent<VerticalLayoutGroup>();
            leftLayout.padding = new RectOffset(16, 16, 14, 14);
            leftLayout.spacing = 4f;
            leftLayout.childAlignment = TextAnchor.UpperLeft;
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;

            Ui.Line(left, "EVACUATION", 13, FontStyle.Bold, Palette.Muted, 20f);
            _evacuated = Ui.Line(left, "Out  0", 22, FontStyle.Bold, Palette.Good, 30f);
            _caught = Ui.Line(left, "Caught  0", 22, FontStyle.Bold, Palette.Danger, 30f);
            _inside = Ui.Line(left, "Still inside  0", 18, FontStyle.Normal, Palette.Ink, 26f);
            _headline = Ui.Line(left, "", 14, FontStyle.Normal, Palette.Ink, 72f);
            _comparison = Ui.Line(left, "No completed run yet.", 13, FontStyle.Normal, Palette.Muted, 56f);
            _status = Ui.Line(left, "", 13, FontStyle.Italic, Palette.Muted, 36f);

            RectTransform right = Ui.Panel(root, "Exits", Palette.Panel);
            Ui.Anchor(right, 1f, 1f, 1f, 1f, new Vector2(-336f, -400f), new Vector2(-16f, -70f));
            var rightLayout = right.gameObject.AddComponent<VerticalLayoutGroup>();
            rightLayout.padding = new RectOffset(16, 16, 14, 14);
            rightLayout.spacing = 8f;
            rightLayout.childAlignment = TextAnchor.UpperLeft;
            rightLayout.childControlWidth = true;
            rightLayout.childControlHeight = true;
            rightLayout.childForceExpandWidth = true;
            rightLayout.childForceExpandHeight = false;
            Ui.Line(right, "EXITS", 13, FontStyle.Bold, Palette.Muted, 20f);
            string[] ids = { "west", "east", "north", "south" };
            string[] names = { "West", "East", "North", "South" };
            for (int i = 0; i < ids.Length; i++) _rows[i] = ExitRow.Create(right, ids[i], names[i]);

            RectTransform bottom = Ui.Panel(root, "Controls", Palette.Panel);
            Ui.Anchor(bottom, 0f, 0f, 1f, 0f, new Vector2(16f, 12f), new Vector2(-16f, 132f));
            var column = bottom.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(12, 12, 10, 8);
            column.spacing = 8f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            column.childForceExpandWidth = true;

            RectTransform row1 = Ui.Row(bottom);
            _nearestButton = Ui.Button(row1, "Nearest exit", Palette.Amber, 150f, () => _bootstrap.SetMode(RoutingMode.NearestExit));
            _supportButton = Ui.Button(row1, "Decision support", Palette.Cyan, 180f, () => _bootstrap.SetMode(RoutingMode.DecisionSupport));
            Ui.Button(row1, "Pause", Palette.Button, 90f, () => _bootstrap.TogglePause());
            Ui.Button(row1, "1x", Palette.Button, 56f, () => _bootstrap.SetSpeed(1f));
            Ui.Button(row1, "2x", Palette.Button, 56f, () => _bootstrap.SetSpeed(2f));
            Ui.Button(row1, "4x", Palette.Button, 56f, () => _bootstrap.SetSpeed(4f));
            Ui.Button(row1, "Restart", Palette.Button, 110f, () => _bootstrap.Restart());
            Ui.Button(row1, "Copy", Palette.Button, 80f, () => _bootstrap.CopySummary());

            RectTransform row2 = Ui.Row(bottom);
            _agentValue = Ui.SliderRow(row2, "People", 8f, 80f, _bootstrap.AgentCount, true, value =>
            {
                _bootstrap.AgentCount = Mathf.RoundToInt(value);
            });
            _spreadValue = Ui.SliderRow(row2, "Spread", 0f, 2f, _bootstrap.SpreadRate, false, value =>
            {
                _bootstrap.SpreadRate = value;
            });
            Ui.SeedField(row2, _bootstrap.Seed, value => _bootstrap.Seed = value);
            Text hint = Ui.Text(row2, 12, FontStyle.Normal, TextAnchor.MiddleLeft, Palette.Muted);
            hint.text = "People and seed apply on restart.  B nearest   D support   R restart   Space pause";
            var hintLayout = hint.gameObject.AddComponent<LayoutElement>();
            hintLayout.flexibleWidth = 1f;
            hintLayout.preferredHeight = 32f;
            Ui.Stretch(hint.rectTransform, 0f, 0f, 0f, 0f);
        }

        public void Refresh()
        {
            EvacuationSession session = _bootstrap.Session;
            if (session == null) return;
            bool support = session.Config.Mode == RoutingMode.DecisionSupport;
            _mode.text = support ? "DECISION SUPPORT" : "NEAREST EXIT";
            _modeChip.color = support ? Palette.Cyan : Palette.Amber;
            _mode.color = new Color(0.08f, 0.07f, 0.05f, 1f);
            float shown = session.IsComplete ? session.CompletionTime : session.Elapsed;
            _clock.text = SimText.Clock(shown) + (session.IsComplete ? "  clear" : "");
            _evacuated.text = "Out  " + session.Tally.Evacuated;
            _caught.text = "Caught  " + session.Tally.Caught;
            _inside.text = "Still inside  " + session.Tally.Active;
            _headline.text = session.LastReplan != null ? session.LastReplan.Headline : "";
            _comparison.text = _bootstrap.Archive.Describe();

            string state;
            if (!string.IsNullOrEmpty(_bootstrap.BakeNote)) state = _bootstrap.BakeNote;
            else if (_bootstrap.Paused) state = "Paused. Space to continue.";
            else if (session.IsComplete && session.HitTimeLimit) state = "Time limit. Some people are still inside.";
            else if (session.IsComplete) state = "Floor clear. Switch mode and restart to compare the same seed.";
            else state = "Speed " + _bootstrap.Speed.ToString("0") + "x. Amber is nearest-exit, cyan is decision support.";
            _status.text = state;

            IReadOnlyList<ExitState> exits = session.Exits;
            for (int i = 0; i < _rows.Length; i++)
            {
                ExitState exit = null;
                for (int e = 0; e < exits.Count; e++)
                {
                    if (exits[e].Id == _rows[i].Id) exit = exits[e];
                }

                if (exit == null) continue;
                float ratio = exit.SoftCapacity <= 0 ? 0f : exit.ActiveCount / (float)exit.SoftCapacity;
                _rows[i].Title.text = exit.DisplayName + (exit.Blocked ? "   blocked" : "   open");
                _rows[i].Title.color = exit.Blocked ? Palette.Danger : Palette.Ink;
                _rows[i].Detail.text = exit.ActiveCount + " heading here   ·   " + exit.EvacuatedCount + " out   ·   cap " + exit.SoftCapacity;
                _rows[i].Fill.color = Color.Lerp(Palette.Good, Palette.Danger, Mathf.Clamp01(ratio));
                RectTransform fill = _rows[i].Fill.rectTransform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }

            Tint(_nearestButton, !support, Palette.Amber);
            Tint(_supportButton, support, Palette.Cyan);
        }

        static void Tint(Button button, bool active, Color on)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null) image.color = active ? on : Palette.Button;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = active ? new Color(0.10f, 0.08f, 0.05f, 1f) : Palette.Ink;
        }

        sealed class ExitRow
        {
            public string Id;
            public Text Title;
            public Text Detail;
            public Image Fill;

            public static ExitRow Create(Transform parent, string id, string name)
            {
                var row = new ExitRow();
                row.Id = id;
                var block = new GameObject(name, typeof(RectTransform));
                block.transform.SetParent(parent, false);
                var layout = block.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 2f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                var blockElement = block.AddComponent<LayoutElement>();
                blockElement.preferredHeight = 58f;
                row.Title = Ui.Line(block.transform, name, 15, FontStyle.Bold, Palette.Ink, 20f);
                RectTransform bar = Ui.Panel(block.transform, "Bar", Palette.PanelEdge);
                var barElement = bar.gameObject.AddComponent<LayoutElement>();
                barElement.preferredHeight = 8f;
                var fillGo = Ui.Panel(bar, "Fill", Palette.Good);
                row.Fill = fillGo.GetComponent<Image>();
                Ui.Stretch(row.Fill.rectTransform, 0f, 0f, 0f, 0f);
                row.Detail = Ui.Line(block.transform, "", 12, FontStyle.Normal, Palette.Muted, 16f);
                return row;
            }
        }

        static class Ui
        {
            public static RectTransform Panel(Transform parent, string name, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                var image = go.GetComponent<Image>();
                image.sprite = Palette.White;
                image.type = Image.Type.Simple;
                image.color = color;
                return go.GetComponent<RectTransform>();
            }

            public static Text Text(Transform parent, int size, FontStyle style, TextAnchor anchor, Color color)
            {
                var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(parent, false);
                var text = go.GetComponent<Text>();
                text.font = Palette.Font;
                text.fontSize = size;
                text.fontStyle = style;
                text.alignment = anchor;
                text.color = color;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.raycastTarget = false;
                return text;
            }

            public static Text Line(Transform parent, string value, int size, FontStyle style, Color color, float height)
            {
                Text text = Text(parent, size, style, TextAnchor.MiddleLeft, color);
                text.text = value;
                var element = text.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = height;
                element.minHeight = height;
                return text;
            }

            public static RectTransform Row(Transform parent)
            {
                var go = new GameObject("Row", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var layout = go.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var element = go.AddComponent<LayoutElement>();
                element.preferredHeight = 36f;
                return go.GetComponent<RectTransform>();
            }

            public static Button Button(Transform parent, string label, Color color, float width, UnityAction onClick)
            {
                RectTransform panel = Panel(parent, label, color);
                var element = panel.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = width;
                element.preferredHeight = 34f;
                var button = panel.gameObject.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                Text text = Text(panel, 14, FontStyle.Bold, TextAnchor.MiddleCenter, Palette.Ink);
                text.text = label;
                Stretch(text.rectTransform, 4f, 4f, 2f, 2f);
                var colors = button.colors;
                colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
                colors.selectedColor = Color.white;
                colors.colorMultiplier = 1f;
                button.colors = colors;
                button.onClick.AddListener(onClick);
                return button;
            }

            public static Text SliderRow(Transform parent, string caption, float min, float max, float value, bool whole, UnityAction<float> changed)
            {
                var block = new GameObject(caption, typeof(RectTransform));
                block.transform.SetParent(parent, false);
                var element = block.AddComponent<LayoutElement>();
                element.preferredWidth = 230f;
                element.preferredHeight = 34f;
                Text label = Text(block.transform, 13, FontStyle.Bold, TextAnchor.MiddleLeft, Palette.Muted);
                label.text = caption;
                Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -14f), new Vector2(64f, 14f));
                Text readout = Text(block.transform, 14, FontStyle.Bold, TextAnchor.MiddleRight, Palette.Ink);
                readout.text = whole ? Mathf.RoundToInt(value).ToString() : value.ToString("0.00");
                Place(readout.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, -14f), new Vector2(0f, 14f));

                RectTransform track = Panel(block.transform, "Track", Palette.PanelEdge);
                Place(track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(68f, -6f), new Vector2(-54f, 6f));
                RectTransform fill = Panel(track, "Fill", Palette.Cyan);
                Stretch(fill, 0f, 0f, 0f, 0f);
                var slider = track.gameObject.AddComponent<Slider>();
                slider.minValue = min;
                slider.maxValue = max;
                slider.wholeNumbers = whole;
                slider.fillRect = fill;
                slider.targetGraphic = fill.GetComponent<Image>();
                slider.direction = Slider.Direction.LeftToRight;
                slider.onValueChanged.AddListener(valueNow =>
                {
                    readout.text = whole ? Mathf.RoundToInt(valueNow).ToString() : valueNow.ToString("0.00");
                    changed(valueNow);
                });
                slider.value = value;
                return readout;
            }

            public static void SeedField(Transform parent, int seed, UnityAction<int> changed)
            {
                var block = new GameObject("Seed", typeof(RectTransform));
                block.transform.SetParent(parent, false);
                var element = block.AddComponent<LayoutElement>();
                element.preferredWidth = 120f;
                element.preferredHeight = 34f;
                Text caption = Text(block.transform, 13, FontStyle.Bold, TextAnchor.MiddleLeft, Palette.Muted);
                caption.text = "Seed";
                Place(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -14f), new Vector2(40f, 14f));
                RectTransform field = Panel(block.transform, "Input", Palette.PanelEdge);
                Place(field, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(44f, -14f), new Vector2(0f, 14f));
                Text text = Text(field, 14, FontStyle.Bold, TextAnchor.MiddleCenter, Palette.Ink);
                text.text = seed.ToString();
                text.raycastTarget = true;
                Stretch(text.rectTransform, 4f, 4f, 2f, 2f);
                var input = field.gameObject.AddComponent<InputField>();
                input.textComponent = text;
                input.text = seed.ToString();
                input.contentType = InputField.ContentType.IntegerNumber;
                input.onEndEdit.AddListener(raw =>
                {
                    int parsed;
                    if (!int.TryParse(raw, out parsed)) parsed = seed;
                    changed(parsed);
                });
            }

            public static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
            {
                rt.anchorMin = new Vector2(minX, minY);
                rt.anchorMax = new Vector2(maxX, maxY);
                rt.offsetMin = offsetMin;
                rt.offsetMax = offsetMax;
                rt.pivot = new Vector2(minX, minY);
            }

            public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.offsetMin = offsetMin;
                rt.offsetMax = offsetMax;
            }

            public static void Stretch(RectTransform rt, float left, float right, float bottom, float top)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(left, bottom);
                rt.offsetMax = new Vector2(-right, -top);
            }
        }
    }
}
