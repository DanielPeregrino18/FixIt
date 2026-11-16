using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cola persistente de partidas terminadas, para enviarlas al servidor aunque no haya conexión
/// en el momento. RemoteProgressStore la vacía en su ciclo de sincronización (solo con sesión iniciada).
///
/// Cada partida se envía con POST /sessions. El servicio de sesiones suma por su cuenta las
/// estadísticas del usuario (partidas, tickets, tiempo, puntaje, estrellas), así que NO hay que
/// llamar a PATCH /users/:id/stats aquí: se contaría doble (comprobado en el servidor).
/// </summary>
public static class SessionQueue
{
    private const string KEY_COLA = "FixIT_PartidasPendientes";
    private const string KEY_COLA_ANTIGUA = "FixIT_SesionesPendientes"; // formato anterior (nunca se llegó a usar)

    [Serializable]
    private class Pendiente
    {
        public SessionCreateRequest sesion;
    }

    [Serializable]
    private class Cola
    {
        public List<Pendiente> partidas = new List<Pendiente>();
    }

    private static Cola _cola;

    public static bool HayPendientes => Cargar().partidas.Count > 0;

    public static void Agregar(SessionCreateRequest sesion)
    {
        if (sesion == null) return;
        Cargar().partidas.Add(new Pendiente { sesion = sesion });
        Guardar();
        Debug.Log($"💾 Partida en cola para enviarse al servidor: {sesion.levelId} ({sesion.starsEarned}★, {sesion.score} pts)");
    }

    /// <summary>Envía en orden hasta vaciar la cola o hasta perder la conexión.</summary>
    public static IEnumerator Enviar(FixItNetworkManager red)
    {
        Cola cola = Cargar();

        while (cola.partidas.Count > 0)
        {
            Pendiente p = cola.partidas[0];
            bool termino = false;
            bool reintentar = false;

            red.PostSessionClean(
                sessionRequest: p.sesion,
                onSuccess: (_) => termino = true,
                onError: (error) =>
                {
                    reintentar = FixItNetworkManager.EsErrorTemporal(error);
                    termino = true;
                }
            );
            yield return new WaitUntil(() => termino);
            if (reintentar) yield break;

            // Enviada, o rechazada por el servidor (reintentarla no cambiaría el resultado)
            Debug.Log($"✅ Partida enviada al servidor: {p.sesion.levelId}");
            cola.partidas.RemoveAt(0);
            Guardar();
        }
    }

    private static Cola Cargar()
    {
        if (_cola == null)
        {
            string json = PlayerPrefs.GetString(KEY_COLA, string.Empty);
            _cola = string.IsNullOrEmpty(json) ? new Cola() : JsonUtility.FromJson<Cola>(json);
            if (_cola == null || _cola.partidas == null) _cola = new Cola();

            if (PlayerPrefs.HasKey(KEY_COLA_ANTIGUA)) PlayerPrefs.DeleteKey(KEY_COLA_ANTIGUA);
        }
        return _cola;
    }

    private static void Guardar()
    {
        PlayerPrefs.SetString(KEY_COLA, JsonUtility.ToJson(_cola));
        PlayerPrefs.Save();
    }
}
