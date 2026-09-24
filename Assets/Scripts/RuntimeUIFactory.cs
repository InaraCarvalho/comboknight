using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Utilitario para criar elementos de UI em tempo de execucao (sem precisar
// montar prefabs na cena). Usado pelo GameManager para o HUD de XP/nivel,
// indicador de escudo e o painel do mercador.
public static class RuntimeUIFactory
{
    private static Sprite cachedWhiteSprite;

    public static Sprite WhiteSprite
    {
        get
        {
            if (cachedWhiteSprite != null) return cachedWhiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            return cachedWhiteSprite;
        }
    }

    private static Sprite cachedCircleSprite;

    // Bolinha branca com borda suave (pixel alpha radial). Usada como icone de
    // moeda na loja do mercador; a cor final vem do tint do Image.
    public static Sprite CircleSprite
    {
        get
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - dist + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return cachedCircleSprite;
        }
    }

    public static GameObject CreateRect(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite;
        img.color = color;
        return go;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    public static Button CreateButton(string name, Transform parent, string label, Action onClick, Color bg, Color labelColor)
    {
        var go = CreateRect(name, parent, Vector2.zero, new Vector2(120f, 34f), bg);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var labelObj = CreateText("Label", go.transform, label, 16f, labelColor);
        var lrt = (RectTransform)labelObj.transform;
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(120f, 34f);
        return btn;
    }

    public static void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    public static void SetButtonState(Button btn, bool interactable, string label)
    {
        if (btn == null) return;
        btn.interactable = interactable;
        var labelTmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (labelTmp != null) labelTmp.text = label;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = interactable ? new Color(0.2f, 0.55f, 0.25f) : new Color(0.25f, 0.25f, 0.28f);
        }
    }
}