// Coloca la carpeta "OfficeFloorLowPoly" dentro de Assets/ y usa el menú:
// Tools > Office Floor Low Poly > Create Materials
// Crea un material por variante (Gris, Beige, AlfombraAzul). Detecta Built-in, URP o HDRP.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class OfficeFloorLowPolyCreator
{
    const string Root = "Assets/OfficeFloorLowPoly";
    static readonly string[] Variants = { "Gris", "Beige", "AlfombraAzul" };

    [MenuItem("Tools/Office Floor Low Poly/Create Materials")]
    public static void Create()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        string rpName = rp == null ? "" : rp.GetType().Name;

        foreach (var v in Variants)
        {
            string texPath = $"{Root}/Textures/OfficeFloorLP_{v}.png";
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp == null) { Debug.LogWarning("No encontré " + texPath); continue; }
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;   // cambia a Point si quieres un look más pixelado
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.textureCompression = TextureImporterCompression.Uncompressed; // colores planos sin artefactos
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            bool carpet = v.Contains("Alfombra");
            float smooth = carpet ? 0f : 0.25f;   // low poly: casi mate
            Material mat;

            if (rpName.Contains("HDRenderPipeline"))
            {
                mat = new Material(Shader.Find("HDRP/Lit"));
                mat.SetTexture("_BaseColorMap", tex);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", smooth);
            }
            else if (rpName.Contains("Universal"))
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", smooth);
                // Sin reflejos especulares para un look más plano
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            else
            {
                mat = new Material(Shader.Find("Standard"));
                mat.SetTexture("_MainTex", tex);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Glossiness", smooth);
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }

            // Cada textura = 2x2 baldosas de 60 cm (1.2 m). En un Plane (10x10 m) usa ~8.33.
            mat.mainTextureScale = new Vector2(8.33f, 8.33f);

            string path = $"{Root}/OfficeFloorLP_{v}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Materiales low poly creados en " + Root);
    }
}
