using UnityEngine;
using UnityEngine.UI;

public class ExtinguisherStation : InteractableTable
{
    [Header("Punto donde se coloca el extintor")]
    public Transform socket;

    [Header("Barra de Carga Flotante")]
    public GameObject barraUI;
    public Image barraRelleno;

    [Header("Configuración de Recarga")]
    public float cargaPorSegundoPorcentaje = 50f;

    [Header("Extintor Inicial")]
    public GameObject extintorPrefab;

    // ── Estado interno ────────────────────────────────────────────────────────
    private GameObject   _extintorActual   = null; // el que está físicamente en la estación
    private GameObject   _extintorGenerado = null; // referencia PERMANENTE al que creó esta estación
    private Extinguisher _extScript        = null;
    private bool         _cargando         = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (barraUI != null) barraUI.SetActive(false);

        if (extintorPrefab != null && socket != null)
        {
            GameObject nuevoExtintor = Instantiate(extintorPrefab, socket.position, socket.rotation, socket);

            _extintorActual   = nuevoExtintor;
            _extintorGenerado = nuevoExtintor; // guardar para siempre, aunque se lo lleven
            _extScript        = nuevoExtintor.GetComponent<Extinguisher>();

            nuevoExtintor.transform.localPosition = Vector3.zero;
            nuevoExtintor.transform.localRotation = Quaternion.identity;
            nuevoExtintor.transform.localScale    = Vector3.one;

            if (nuevoExtintor.GetComponent<Rigidbody>() != null)
                nuevoExtintor.GetComponent<Rigidbody>().isKinematic = true;
            if (nuevoExtintor.GetComponent<Collider>() != null)
                nuevoExtintor.GetComponent<Collider>().enabled = true;

            if (_extScript != null)
            {
                _cargando = _extScript.cargaActual < _extScript.cargaMaxima;
                if (barraUI != null) barraUI.SetActive(true);
                ActualizarBarra();
            }
        }
    }

    void Update()
    {
        if (!_cargando || _extScript == null) return;

        _extScript.Cargar(cargaPorSegundoPorcentaje * Time.deltaTime);
        ActualizarBarra();

        if (_extScript.cargaActual >= _extScript.cargaMaxima)
        {
            _cargando = false;
            Debug.Log("ExtinguisherStation: Carga completa.");
        }
    }

    // ── Interacción principal ─────────────────────────────────────────────────

    public override void Interact(PlayerInteraction player)
    {
        bool playerHasItem     = player.isHoldingItem;
        bool estacionTieneItem = _extintorActual != null;

        if (playerHasItem && !estacionTieneItem)
        {
            Extinguisher ext = player.heldItem.GetComponent<Extinguisher>();
            if (ext != null) ColocarExtintor(player, ext);
        }
        else if (!playerHasItem && estacionTieneItem)
        {
            TomarExtintor(player);
        }
    }

    // ── Reset entre niveles ───────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el extintor de esta estación a su socket con carga completa,
    /// sin importar dónde haya quedado (suelo, mesa, mano de un jugador).
    /// Llamado por WaveManager al iniciar/reiniciar un nivel.
    /// </summary>
    public void ResetearExtintor()
    {
        if (_extintorGenerado == null) return;

        _extintorActual = _extintorGenerado;
        _extScript      = _extintorGenerado.GetComponent<Extinguisher>();

        // Devolver al socket de la estación
        _extintorGenerado.transform.SetParent(socket);
        _extintorGenerado.transform.localPosition = Vector3.zero;
        _extintorGenerado.transform.localRotation = Quaternion.identity;
        _extintorGenerado.transform.localScale    = Vector3.one;

        if (_extintorGenerado.GetComponent<Rigidbody>() != null)
            _extintorGenerado.GetComponent<Rigidbody>().isKinematic = true;
        if (_extintorGenerado.GetComponent<Collider>() != null)
            _extintorGenerado.GetComponent<Collider>().enabled = true;

        if (_extScript != null)
        {
            // Apagar la espuma si quedó rociando y recargar al 100%
            if (_extScript.espumaParticles != null) _extScript.espumaParticles.Stop();
            _extScript.Recargar();
        }

        _cargando = false;
        if (barraUI != null) barraUI.SetActive(true);
        ActualizarBarra();

        Debug.Log("ExtinguisherStation: extintor devuelto a la estación con carga completa.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void ColocarExtintor(PlayerInteraction player, Extinguisher ext)
    {
        _extintorActual = player.heldItem;
        _extScript      = ext;

        _extintorActual.transform.SetParent(socket);
        _extintorActual.transform.localPosition = Vector3.zero;
        _extintorActual.transform.localRotation = Quaternion.identity;
        _extintorActual.transform.localScale    = Vector3.one;

        if (_extintorActual.GetComponent<Collider>() != null)
            _extintorActual.GetComponent<Collider>().enabled = true;

        player.isHoldingItem = false;
        player.heldItem      = null;
        if (player.anim != null) player.anim.SetBool("isHolding", false);
        player.AjustarCollider(false);

        _cargando = _extScript.cargaActual < _extScript.cargaMaxima;

        if (barraUI != null) barraUI.SetActive(true);
        ActualizarBarra();

        Debug.Log("ExtinguisherStation: Extintor colocado, cargando...");
    }

    void TomarExtintor(PlayerInteraction player)
    {
        player.isHoldingItem = true;
        player.heldItem      = _extintorActual;

        // FIX: preservar escala visual al cambiar de padre
        Vector3 escalaOriginal = _extintorActual.transform.lossyScale;
        _extintorActual.transform.SetParent(player.itemSocket);
        _extintorActual.transform.localPosition = Vector3.zero;
        _extintorActual.transform.localRotation = Quaternion.identity;
        _extintorActual.transform.localScale = new Vector3(
            escalaOriginal.x / player.itemSocket.lossyScale.x,
            escalaOriginal.y / player.itemSocket.lossyScale.y,
            escalaOriginal.z / player.itemSocket.lossyScale.z
        );

        if (_extintorActual.GetComponent<Rigidbody>() != null)
            _extintorActual.GetComponent<Rigidbody>().isKinematic = true;
        if (_extintorActual.GetComponent<Collider>() != null)
            _extintorActual.GetComponent<Collider>().enabled = false;

        if (player.anim != null) player.anim.SetBool("isHolding", true);
        player.AjustarCollider(true);

        _cargando       = false;
        _extintorActual = null;
        _extScript      = null;

        if (barraUI != null) barraUI.SetActive(false);

        Debug.Log("ExtinguisherStation: Extintor recogido.");
    }

    void ActualizarBarra()
    {
        if (barraRelleno != null && _extScript != null)
            barraRelleno.fillAmount = _extScript.PorcentajeCarga();
    }
}