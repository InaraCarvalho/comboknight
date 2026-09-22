using System.IO;
using UnityEditor;
using UnityEngine;

// Exporta as fatias (sub-sprites) definidas no Sprite Editor da Spritesheet.png
// como arquivos PNG separados em Assets/Sprites/Split/, com nomes por posicao.
// Menu: Tools > Export Spritesheet Slices as PNGs
public static class SpritesheetExporter
{
    private const string SourcePath = "Assets/Sprites/Spritesheet.png";

    [MenuItem("Tools/Export Spritesheet Slices as PNGs")]
    public static void Export()
    {
        var importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
        if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            EditorUtility.DisplayDialog("Export Spritesheet", "A Spritesheet.png precisa estar em Sprite Mode = Multiple.", "OK");
            return;
        }

        string srcDir = Path.GetDirectoryName(Application.dataPath) + "/Assets/Sprites";
        string outDir = srcDir + "/Split";
        Directory.CreateDirectory(outDir);

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(srcDir + "/Spritesheet.png")))
        {
            EditorUtility.DisplayDialog("Export Spritesheet", "Nao foi possivel ler a Spritesheet.png.", "OK");
            return;
        }

        var px = tex.GetPixels32();
        var metas = importer.spritesheet;
        string[] suffixes = {
            "0_upper", "1_text", "2_middle", "3_text", "4_lower"
        };

        for (int i = 0; i < metas.Length; i++)
        {
            var r = metas[i].rect;
            int x = Mathf.RoundToInt(r.x), y = Mathf.RoundToInt(r.y);
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var outPx = new Color32[w * h];
            for (int dy = 0; dy < h; dy++)
            {
                int srcRow = tex.height - 1 - (y + dy);
                for (int dx = 0; dx < w; dx++)
                    outPx[dy * w + dx] = px[srcRow * tex.width + (x + dx)];
            }
            outTex.SetPixels32(outPx);

            string suffix = (i < suffixes.Length) ? suffixes[i] : i.ToString("00");
            string file = Path.Combine(outDir, string.Format("spritesheet_{0}.png", suffix));
            File.WriteAllBytes(file, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
        }

        Object.DestroyImmediate(tex);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Export Spritesheet",
            string.Format("{0} fatias exportadas para Assets/Sprites/Split/.", metas.Length), "OK");
    }
}