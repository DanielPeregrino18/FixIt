using UnityEditor;
using UnityEngine;

/// <summary>
/// Agrega el logo de la UTCH a la pantalla de inicio del build, justo después del logo de Unity
/// (Player Settings → Splash Image → Draw Mode "All Sequential").
///
/// Se aplica sola una vez al abrir/compilar el proyecto. Para volver a aplicarla (por ejemplo si
/// cambias algo a mano en Player Settings y quieres regresar a esto): menú FixIt → Splash: logo UTCH.
/// </summary>
[InitializeOnLoad]
public static class ConfigurarSplashUTCH
{
    const string RUTA_LOGO = "Assets/Sprites/Splash/Logotipo_UTCH_splash.png";
    const string CLAVE_APLICADO = "FixIt_SplashUTCH_Aplicado_v1";
    const float SEGUNDOS_UNITY = 2f;
    const float SEGUNDOS_UTCH = 3f;

    static ConfigurarSplashUTCH()
    {
        // Esperar a que termine de cargar/importar el editor
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(CLAVE_APLICADO, false)) return;
            if (Aplicar(mostrarAviso: false)) EditorPrefs.SetBool(CLAVE_APLICADO, true);
        };
    }

    [MenuItem("FixIt/Splash: logo UTCH")]
    static void AplicarDesdeMenu()
    {
        if (Aplicar(mostrarAviso: true)) EditorPrefs.SetBool(CLAVE_APLICADO, true);
    }

    static bool Aplicar(bool mostrarAviso)
    {
        Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(RUTA_LOGO);
        if (logo == null)
        {
            if (mostrarAviso)
                EditorUtility.DisplayDialog("Splash UTCH", $"No se encontró el sprite en {RUTA_LOGO}.", "OK");
            return false;
        }

        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
        PlayerSettings.SplashScreen.logos = new[]
        {
            PlayerSettings.SplashScreenLogo.CreateWithUnityLogo(SEGUNDOS_UNITY), // primero Unity…
            PlayerSettings.SplashScreenLogo.Create(SEGUNDOS_UTCH, logo),         // …luego la UTCH
        };
        AssetDatabase.SaveAssets();

        Debug.Log("ConfigurarSplashUTCH: splash configurado (logo de Unity y después logo UTCH).");
        if (mostrarAviso)
            EditorUtility.DisplayDialog("Splash UTCH", "Listo: en el build sale el logo de Unity y después el de la UTCH.", "OK");
        return true;
    }
}
