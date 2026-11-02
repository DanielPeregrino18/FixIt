using System;
using UnityEngine;

public class NetworkTester : MonoBehaviour
{
    void Start()
    {
        Debug.Log(" 🆗🆗PRUEBA DE CONEXION FIXIT");
        Debug.Log("========================================");

        Invoke(nameof(TestHealth), 1f);
    }

    void TestHealth()
    {
        Debug.Log("Probando los endpoints GET /health de los cuatro servicios");

        if (FixItNetworkManager.Instance == null)
        {
            Debug.LogError("No se encontró FixItNetworkManager en la escena.");
            return;
        }

        FixItNetworkManager manager = FixItNetworkManager.Instance;
        RunHealthCheck("USER", manager.GetUserHealth);
        RunHealthCheck("PROGRESS", manager.GetProgressHealth);
        RunHealthCheck("SESSION", manager.GetSessionHealth);
        RunHealthCheck("NEWS", manager.GetNewsHealth);
    }

    private void RunHealthCheck(string serviceName, Action<Action<string>, Action<string>> requestHealth)
    {
        requestHealth(
            response => Debug.Log("[" + serviceName + "] /health OK:\n" + response),
            error => Debug.LogError("[" + serviceName + "] /health ERROR: " + error));
    }
}