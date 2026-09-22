using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Divididor automatico de spritesheet: detecta faixas de conteudo (linhas e
// colunas quase transparentes) e exporta cada celula como PNG separado.
// Menu: Tools > Split Spritesheet PNG...
public class SpritesheetSplitterWindow : EditorWindow
{
    private Texture2D source;
    private string outputFolder = "Assets/Sprites/Split";
    private int overrideColumns = 0;
    private int overrideRows = 0;
    private int padding = 2;

    [MenuItem("Tools/Split Spritesheet PNG...")]
    public static void OpenWindow()
    {
        var win = GetWindow<SpritesheetSplitterWindow>("Split Spritesheet");
        win.minSize = new Vector2(380f, 220f);
        win.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Spritesheet", EditorStyles.boldLabel);
        source = (Texture2D)EditorGUILayout.ObjectField("Texto fonte", source, typeof(Texture2D), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Auto-detect faz a grade por padroes de transparencia.", EditorStyles.wordWrappedLabel);
        overrideColumns = EditorGUILayout.IntField("Colunas (0 = auto)", Mathf.Max(0, overrideColumns));
        overrideRows = EditorGUILayout.IntField("Linhas (0 = auto)", Mathf.Max(0, overrideRows));
        padding = EditorGUILayout.IntField("Padding (px)", Mathf.Max(0, padding));

        EditorGUILayout.Space();
        outputFolder = EditorGUILayout.TextField("Pasta de saida", outputFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("Dividir", GUILayout.Height(34f)))
        {
            Run();
        }
    }

    private void Run()
    {
        if (source == null)
        {
            EditorUtility.DisplayDialog("Split Spritesheet", "Selecione o PNG da spritesheet primeiro.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(source);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool loaded = tex.LoadImage(File.ReadAllBytes(
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", path)));
        if (!loaded)
        {
            EditorUtility.DisplayDialog("Split Spritesheet", "Nao foi possivel ler o arquivo de imagem.", "OK");
            return;
        }

        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        bool[,] mask = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = px[y * w + x].a > 30;

        var cells = (overrideColumns > 0 && overrideRows > 0)
            ? SliceGrid(mask, w, h, overrideColumns, overrideRows)
            : AutoSlice(mask, w, h);

        if (cells.Count == 0)
        {
            EditorUtility.DisplayDialog("Split Spritesheet", "Nenhuma celula detectada.", "OK");
            return;
        }

        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", outputFolder);
        Directory.CreateDirectory(dir);
        string baseName = Path.GetFileNameWithoutExtension(path);

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            int cw = cell.Width + padding * 2;
            int ch = cell.Height + padding * 2;
            var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            var outPx = new Color32[cw * ch];
            for (int cy = 0; cy < ch; cy++)
                for (int cx = 0; cx < cw; cx++)
                    outPx[cy * cw + cx] = Color.clear;

            for (int sy = 0; sy < cell.Height; sy++)
            {
                for (int sx = 0; sx < cell.Width; sx++)
                {
                    if (!mask[cell.X + sx, cell.Y + sy]) continue;
                    outPx[(sy + padding) * cw + (sx + padding)] = px[(cell.Y + sy) * w + (cell.X + sx)];
                }
            }

            outTex.SetPixels32(outPx);
            string file = Path.Combine(dir, string.Format("{0}_{1:00}.png", baseName, i + 1));
            File.WriteAllBytes(file, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            if (i % 25 == 24 || i == cells.Count - 1)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Split Spritesheet",
                    string.Format("{0}/{1} celulas...", i + 1, cells.Count),
                    (i + 1f) / cells.Count)) break;
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Split Spritesheet",
            string.Format("{0} celulas exportadas para '{1}'.", cells.Count, outputFolder), "OK");
    }

    private struct Cell
    {
        public int X, Y, Width, Height;
        public int Area { get { return Width * Height; } }
    }

    private static List<Cell> AutoSlice(bool[,] mask, int w, int h)
    {
        // Faixas verticais separadas por linhas quase vazias.
        var rowBands = new List<int[]>();
        int start = -1;
        for (int y = 0; y <= h; y++)
        {
            bool empty = y >= h || RowOpaqueCount(mask, w, y) <= 2;
            if (empty && start >= 0) { rowBands.Add(new[] { start, y - 1 }); start = -1; }
            else if (!empty && start < 0) start = y;
        }

        var cells = new List<Cell>();
        foreach (var band in rowBands)
        {
            int y0 = band[0], y1 = band[1];
            // Dentro da faixa, colunas quase vazias separam celulas.
            start = -1;
            for (int x = 0; x <= w; x++)
            {
                bool empty = x >= w || ColOpaqueCountInRows(mask, x, y0, y1) <= 2;
                if (empty && start >= 0)
                {
                    AddCellsForRange(mask, start, x - 1, y0, y1, cells);
                    start = -1;
                }
                else if (!empty && start < 0) start = x;
            }
        }
        return cells;
    }

    private static void AddCellsForRange(bool[,] mask, int x0, int x1, int y0, int y1, List<Cell> cells)
    {
        // Dentro de cada faixa horizontal x0..x1, linhas quase vazias separam celulas.
        int start = -1;
        for (int y = y0; y <= y1 + 1; y++)
        {
            bool empty = y > y1 || RowOpaqueCountInCols(mask, y, x0, x1) <= 2;
            if (empty && start >= 0)
            {
                var cell = new Cell { X = x0, Y = start, Width = x1 - x0 + 1, Height = y - start };
                if (cell.Width >= 4 && cell.Height >= 4) cells.Add(cell);
                start = -1;
            }
            else if (!empty && start < 0) start = y;
        }
    }

    private static List<Cell> SliceGrid(bool[,] mask, int w, int h, int cols, int rows)
    {
        var cells = new List<Cell>();
        int cw = w / cols, ch = h / rows;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int x0 = c * cw, y0 = r * ch;
                int x1 = (c == cols - 1) ? w : (c + 1) * cw;
                int y1 = (r == rows - 1) ? h : (r + 1) * ch;
                if (!HasContent(mask, x0, y0, x1, y1)) continue;
                cells.Add(new Cell { X = x0, Y = y0, Width = x1 - x0, Height = y1 - y0 });
            }
        }
        return cells;
    }

    private static bool HasContent(bool[,] mask, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (mask[x, y]) return true;
        return false;
    }

    private static int RowOpaqueCount(bool[,] mask, int w, int y)
    {
        int c = 0;
        for (int x = 0; x < w; x++) if (mask[x, y]) c++;
        return c;
    }

    private static int ColOpaqueCountInRows(bool[,] mask, int x, int y0, int y1)
    {
        int c = 0;
        for (int y = y0; y <= y1; y++) if (mask[x, y]) c++;
        return c;
    }

    private static int RowOpaqueCountInCols(bool[,] mask, int y, int x0, int x1)
    {
        int c = 0;
        for (int x = x0; x <= x1; x++) if (mask[x, y]) c++;
        return c;
    }
}