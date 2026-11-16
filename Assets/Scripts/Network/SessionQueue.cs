using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cola persistente de partidas que no se pudieron enviar por falta de conexión.
/// RemoteProgressStore la vacía cuando vuelve a haber conexión.
/// </summary>
public static class SessionQueue
{
    private const string KEY_COLA = "FixIT_SesionesPendientes";

    [Serializable]
    private class Cola
    {
        public List<SessionCreateRequest> sesiones = new List<SessionCreateRequest>();
    }

    private static Cola _cola;

    public static bool HayPendientes => Cargar().sesiones.Count > 0;

    public static void Agregar(SessionCreateRequest sesion)
    {
        Cargar().sesiones.Add(sesion);
        Guardar();
        Debug.Log($"💾 Sesión en cola para enviarse con conexión: {sesion.levelId}");
    }

    /// <summary>Envía en orden hasta vaciar la cola o hasta perder la conexión.</summary>
    public static IEnumerator Enviar(FixItNetworkManager red)
    {
        Cola cola = Cargar();

        while (cola.sesiones.Count > 0)
        {
            bool termino = false;
            bool reintentar = false;

            red.PostSessionClean(
                sessionRequest: cola.sesiones[0],
                onSuccess: (_) => termino = true,
                onError: (error) =>
                {
                    reintentar = FixItNetworkManager.EsErrorTemporal(error);
                    termino = true;
                }
            );
            yield return new WaitUntil(() => termino);

            if (reintentar) yield break;

            // Enviada, o rechazada por el servidor: reintentarla no cambiaría el resultado
            cola.sesiones.RemoveAt(0);
            Guardar();
        }
    }

    private static Cola Cargar()
    {
        if (_cola == null)
        {
            string json = PlayerPrefs.GetString(KEY_COLA, string.Empty);
            _cola = string.IsNullOrEmpty(json) ? new Cola() : JsonUtility.FromJson<Cola>(json);
        }
        return _cola;
    }

    private static void Guardar()
    {
        PlayerPrefs.SetString(KEY_COLA, JsonUtility.ToJson(_cola));
        PlayerPrefs.Save();
    }
}
