using UnityEngine;
using UnityEditor;

public class CrearSpriteBlanco : MonoBehaviour
{
    [ContextMenu("Generar Sprite Blanco")]
    void Generar()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes("Assets/SpriteBlanco.png", bytes);
        AssetDatabase.Refresh();
        Debug.Log("Sprite generado en Assets/SpriteBlanco.png");
    }
}