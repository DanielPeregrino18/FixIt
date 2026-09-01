using UnityEngine;

/// <summary>
/// Implementación local de IProgressStore usando PlayerPrefs.
/// Progreso guardado por PC, no por cuenta de usuario.
/// Cuando el proyecto defina la base de datos remota, se crea una
/// RemoteProgressStore que implemente la misma interfaz y se cambia
/// solo la línea de creación en ProgressManager — nada más.
/// </summary>
public class LocalProgressStore : IProgressStore
{
    private const string PREFIX_ESTRELLAS = "FixIT_Estrellas_Nivel_";
    private const string KEY_DESBLOQUEADO = "FixIT_NivelDesbloqueado";

    public int ObtenerEstrellas(int nivel)
    {
        return PlayerPrefs.GetInt(PREFIX_ESTRELLAS + nivel, 0);
    }

    public void GuardarEstrellas(int nivel, int estrellas)
    {
        int actual = ObtenerEstrellas(nivel);

        // Solo sobrescribe si el nuevo resultado es igual o mejor
        if (estrellas > actual)
            PlayerPrefs.SetInt(PREFIX_ESTRELLAS + nivel, estrellas);

        // Desbloquear el siguiente nivel si se completó este (>=1 estrella)
        if (estrellas >= 1)
        {
            int desbloqueado = ObtenerNivelDesbloqueado();
            if (nivel + 1 > desbloqueado)
                PlayerPrefs.SetInt(KEY_DESBLOQUEADO, nivel + 1);
        }

        GuardarCambios();
    }

    public int ObtenerNivelDesbloqueado()
    {
        // Por defecto solo el nivel 1 está desbloqueado
        return PlayerPrefs.GetInt(KEY_DESBLOQUEADO, 1);
    }

    public void GuardarCambios()
    {
        PlayerPrefs.Save();
    }
}
