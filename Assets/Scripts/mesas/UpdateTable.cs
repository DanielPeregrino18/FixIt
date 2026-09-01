using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpdateTable : InteractableTable
{
    [Header("Slot de la Mesa")]
    public Transform tableSocket;

    [Header("Barra de Progreso Flotante")]
    public GameObject barraUI;
    public Image barraRelleno;
    public TextMeshProUGUI textoPorcentaje;

    [Header("Tiempos")]
    public float tiempoHasta99 = 3f;
    public float min99 = 1f;
    public float max99 = 3f;

    public GameObject currentItemOnTable;

    private enum Estado { Vacia, Actualizando, Atorado99, Lista }
    private Estado _estado = Estado.Vacia;

    private float _timer;
    private float _tiempoAtorado;
    private float _timerOcultarBarra = 0f;
    private bool  _ocultandoBarra    = false;

    void Start()
    {
        if (barraUI != null) barraUI.SetActive(false);
    }

    void Update()
    {
        if (_ocultandoBarra)
        {
            _timerOcultarBarra -= Time.deltaTime;
            if (_timerOcultarBarra <= 0f) { _ocultandoBarra = false; if (barraUI != null) barraUI.SetActive(false); }
            return;
        }

        if (_estado == Estado.Actualizando)
        {
            _timer += Time.deltaTime;
            float progreso = Mathf.Clamp01(_timer / tiempoHasta99);
            ActualizarBarra(progreso * 0.99f);

            if (_timer >= tiempoHasta99)
            {
                _timer = 0f;
                _tiempoAtorado = Random.Range(min99, max99);
                _estado = Estado.Atorado99;
                ActualizarBarra(0.99f);
            }
        }
        else if (_estado == Estado.Atorado99)
        {
            _timer += Time.deltaTime;
            if (_timer >= _tiempoAtorado) { ActualizarBarra(1f); CompletarActualizacion(); }
        }
    }

    public override void Interact(PlayerInteraction player)
    {
        bool playerHasItem = player.isHoldingItem;
        bool tableHasItem  = currentItemOnTable != null;

        if (playerHasItem && !tableHasItem)
        {
            DejarLaptop(player);
        }
        else if (!playerHasItem && tableHasItem && (_estado == Estado.Lista || _estado == Estado.Vacia))
        {
            Laptop laptopCheck = currentItemOnTable.GetComponent<Laptop>();
            if (laptopCheck != null && !laptopCheck.PuedeRecogerse())
            {
                Debug.Log("UpdateTable: ¡Está en llamas! Usa el extintor primero.");
                return;
            }
            RecogerLaptop(player);
        }
        else if (tableHasItem && (_estado == Estado.Actualizando || _estado == Estado.Atorado99))
        {
            Debug.Log("UpdateTable: La laptop se está actualizando.");
        }
    }

    void DejarLaptop(PlayerInteraction player)
    {
        currentItemOnTable = player.heldItem;

        currentItemOnTable.transform.SetParent(tableSocket);
        currentItemOnTable.transform.localPosition = Vector3.zero;
        currentItemOnTable.transform.localRotation = Quaternion.identity;
        currentItemOnTable.transform.localScale    = Vector3.one;

        if (currentItemOnTable.GetComponent<Collider>() != null)
            currentItemOnTable.GetComponent<Collider>().enabled = true;

        player.isHoldingItem = false;
        player.heldItem      = null;
        if (player.anim != null) player.anim.SetBool("isHolding", false);
        player.AjustarCollider(false);

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null) laptopScript.OnDejada();

        bool necesitaUpdate = laptopScript != null &&
                              laptopScript.problemasActivos.Contains(Laptop.Problema.Actualizacion);

        if (necesitaUpdate) { _timer = 0f; _estado = Estado.Actualizando; if (barraUI != null) barraUI.SetActive(true); ActualizarBarra(0f); }
        else                { _estado = Estado.Lista; }
    }

    void RecogerLaptop(PlayerInteraction player)
    {
        player.isHoldingItem = true;
        player.heldItem      = currentItemOnTable;

        // FIX: preservar escala visual al cambiar de padre
        Vector3 escalaOriginal = currentItemOnTable.transform.lossyScale;
        currentItemOnTable.transform.SetParent(player.itemSocket);
        currentItemOnTable.transform.localPosition = Vector3.zero;
        currentItemOnTable.transform.localRotation = Quaternion.identity;
        currentItemOnTable.transform.localScale = new Vector3(
            escalaOriginal.x / player.itemSocket.lossyScale.x,
            escalaOriginal.y / player.itemSocket.lossyScale.y,
            escalaOriginal.z / player.itemSocket.lossyScale.z
        );

        if (currentItemOnTable.GetComponent<Rigidbody>() != null)
            currentItemOnTable.GetComponent<Rigidbody>().isKinematic = true;
        if (currentItemOnTable.GetComponent<Collider>() != null)
            currentItemOnTable.GetComponent<Collider>().enabled = false;

        if (player.anim != null) player.anim.SetBool("isHolding", true);
        player.AjustarCollider(true);

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null) laptopScript.OnRecogida(player);

        currentItemOnTable = null;
        _estado = Estado.Vacia;
        if (barraUI != null) barraUI.SetActive(false);
        _ocultandoBarra = false;
    }

    void CompletarActualizacion()
    {
        _estado = Estado.Lista;

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null) laptopScript.ResolverProblema(Laptop.Problema.Actualizacion);

        _ocultandoBarra    = true;
        _timerOcultarBarra = 0.5f;
    }

    void ActualizarBarra(float valor)
    {
        if (barraRelleno != null) barraRelleno.fillAmount = valor;
        if (textoPorcentaje != null) textoPorcentaje.text = Mathf.FloorToInt(valor * 100f) + "%";
    }

    /// <summary>Resetea la mesa por completo al iniciar un nivel nuevo.</summary>
    public void LimpiarParaNuevoNivel()
    {
        _estado             = Estado.Vacia;
        _timer              = 0f;
        _ocultandoBarra     = false;
        currentItemOnTable  = null;

        if (barraUI != null) barraUI.SetActive(false);
    }
}