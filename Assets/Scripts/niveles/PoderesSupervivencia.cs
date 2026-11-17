using UnityEngine;

/// <summary>Poderes que da la laptop dorada en Supervivencia.</summary>
public enum Poder
{
    QuitarPerdidas,     // instantáneo: resta laptops perdidas
    StaminaInfinita,    // café para todos (stamina llena) + no se gasta mientras dura
    ServidoresRapidos,  // los servidores se apagan con un rocío corto
    LaptopsFaciles,     // las laptops nuevas llegan fáciles
    FuegoCongelado,     // ninguna laptop se calienta ni avanza su incendio
    Respiro,            // no llegan laptops nuevas
    ExtintorInfinito,   // el extintor no gasta carga
    ReparacionRapida    // todas las mesas trabajan mucho más rápido
}

/// <summary>
/// Estado de los poderes activos. Los tiempos se miden con el reloj del GameManager, así que
/// se detienen en la pausa y se limpian al empezar otra partida. Solo funcionan en Supervivencia.
/// </summary>
public static class PoderesSupervivencia
{
    static readonly int CANTIDAD = System.Enum.GetValues(typeof(Poder)).Length;
    static readonly float[] _hasta = new float[CANTIDAD];

    static float Reloj => GameManager.Instance != null ? GameManager.Instance.TiempoTranscurrido : 0f;

    /// <summary>¿Este poder está activo ahora mismo?</summary>
    public static bool Activo(Poder p) => ModoSupervivencia.Activo && Reloj < _hasta[(int)p];

    /// <summary>Segundos que le quedan (0 si no está activo).</summary>
    public static float Restante(Poder p) => Activo(p) ? _hasta[(int)p] - Reloj : 0f;

    /// <summary>Activa (o reinicia la duración de) un poder.</summary>
    public static void Activar(Poder p, float duracion)
    {
        _hasta[(int)p] = Reloj + Mathf.Max(0f, duracion);
    }

    /// <summary>Qué tan rápido trabajan las mesas ahora (1 = normal).</summary>
    public static float FactorReparacion =>
        Activo(Poder.ReparacionRapida) && GameManager.Instance != null
            ? Mathf.Max(1f, GameManager.Instance.factorReparacionRapida) : 1f;

    /// <summary>Pulsaciones o movimientos necesarios con el poder aplicado (mínimo 1).</summary>
    public static int Requeridos(int normales) => Mathf.Max(1, Mathf.CeilToInt(normales / FactorReparacion));

    /// <summary>Apaga todos los poderes (al iniciar o reiniciar una partida).</summary>
    public static void Reiniciar()
    {
        for (int i = 0; i < CANTIDAD; i++) _hasta[i] = 0f;
    }

    public static string Nombre(Poder p)
    {
        switch (p)
        {
            case Poder.QuitarPerdidas: return "Laptops perdidas perdonadas";
            case Poder.StaminaInfinita: return "Café para todos: stamina infinita";
            case Poder.ServidoresRapidos: return "Servidores fáciles de apagar";
            case Poder.LaptopsFaciles: return "Solo laptops fáciles";
            case Poder.FuegoCongelado: return "Fuego congelado";
            case Poder.Respiro: return "Respiro: no llegan laptops";
            case Poder.ExtintorInfinito: return "Extintor infinito";
            case Poder.ReparacionRapida: return "Reparación rápida";
            default: return p.ToString();
        }
    }

    /// <summary>Nombre corto para el contador del HUD.</summary>
    public static string NombreCorto(Poder p)
    {
        switch (p)
        {
            case Poder.StaminaInfinita: return "Stamina";
            case Poder.ServidoresRapidos: return "Servidores";
            case Poder.LaptopsFaciles: return "Fáciles";
            case Poder.FuegoCongelado: return "Fuego congelado";
            case Poder.Respiro: return "Respiro";
            case Poder.ExtintorInfinito: return "Extintor";
            case Poder.ReparacionRapida: return "Reparación x" + Mathf.RoundToInt(
                GameManager.Instance != null ? GameManager.Instance.factorReparacionRapida : 4f);
            default: return p.ToString();
        }
    }
}
