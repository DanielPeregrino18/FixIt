using UnityEngine;

/// <summary>
/// Modo Supervivencia: sin límite de tiempo, se juega hasta que el área de TI colapsa
/// (6 laptops perdidas o un servidor explota). El récord se guarda solo en local y por
/// separado para 1, 2, 3 y 4 jugadores.
///
/// Activo lo ponen los botones del menú principal: "Jugar" lo apaga y "Supervivencia" lo enciende.
/// </summary>
public static class ModoSupervivencia
{
    /// <summary>¿La partida actual (o la que se va a iniciar) es de supervivencia?</summary>
    public static bool Activo;

    static string Clave(int jugadores) => $"FixIt_Supervivencia_Record_{Mathf.Clamp(jugadores, 1, 4)}J";

    /// <summary>Mejor tiempo (segundos) con esta cantidad de jugadores. 0 = todavía no hay récord.</summary>
    public static float Record(int jugadores) => PlayerPrefs.GetFloat(Clave(jugadores), 0f);

    /// <summary>Guarda el tiempo si supera el récord. Devuelve true si fue récord nuevo.</summary>
    public static bool GuardarSiEsRecord(int jugadores, float segundos)
    {
        if (segundos <= Record(jugadores)) return false;
        PlayerPrefs.SetFloat(Clave(jugadores), segundos);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Segundos → "mm:ss" (o "h:mm:ss" si pasa de una hora).</summary>
    public static string Formato(float segundos)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos));
        int h = s / 3600, m = (s / 60) % 60, seg = s % 60;
        return h > 0 ? $"{h}:{m:D2}:{seg:D2}" : $"{m:D2}:{seg:D2}";
    }
}
