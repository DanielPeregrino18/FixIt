using UnityEngine;

/// <summary>
/// Nubecitas de polvo estilo low poly, creadas por código (no hace falta prefab).
/// Cada nubecita es un icosaedro facetado y sólido que crece y luego se encoge hasta desaparecer.
/// Todo (rastro al correr y puffs) sale de un solo sistema de partículas compartido.
/// </summary>
public static class EfectosPolvo
{
    private static ParticleSystem _sistema;
    private static Material _material;
    private static Mesh _malla;

    /// <summary>Guarda el material a usar (lo llama cada jugador al iniciar).</summary>
    public static void Registrar(Material material)
    {
        if (material != null) _material = material;
    }

    /// <summary>Una nubecita suelta (para el rastro al correr).</summary>
    public static void Nube(Vector3 posicion, Vector3 velocidad, float tamaño, Color color, float vida)
    {
        ParticleSystem ps = Sistema();
        if (ps == null) return;

        var p = new ParticleSystem.EmitParams
        {
            position = posicion,
            velocity = velocidad,
            startSize = tamaño,
            startLifetime = vida,
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            startColor = color
        };
        ps.Emit(p, 1);
    }

    /// <summary>Estallido en anillo sobre el piso (al aterrizar una pieza, al arrancar a correr).</summary>
    public static void Puff(Vector3 posicion, int cantidad, float tamaño, Color color)
    {
        for (int i = 0; i < cantidad; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            Vector3 dir = new Vector3(d.x, 0.35f, d.y);
            Nube(posicion + dir * 0.1f, dir * Random.Range(1f, 2f),
                 tamaño * Random.Range(0.7f, 1.2f), color, Random.Range(0.4f, 0.65f));
        }
    }

    /// <summary>Puff con el color por defecto (para quien no tiene uno propio, p. ej. las piezas).</summary>
    public static void Puff(Vector3 posicion, int cantidad, float tamaño)
        => Puff(posicion, cantidad, tamaño, new Color(0.93f, 0.91f, 0.87f, 1f));

    // ─────────────────────────────────────────────────────────────────────────

    static ParticleSystem Sistema()
    {
        if (_sistema != null) return _sistema;
        if (_material == null) return null; // todavía no hay jugadores que registren el material

        var go = new GameObject("PolvoLowPoly");
        Object.DontDestroyOnLoad(go);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startRotation3D = true;
        main.gravityModifier = -0.05f; // sube apenas
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        ParticleSystem.EmissionModule em = ps.emission;
        em.enabled = false; // solo se emite a mano con Emit()

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = false;

        // Frena rápido después del empujón inicial
        ParticleSystem.LimitVelocityOverLifetimeModule lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 0.3f;
        lim.dampen = 0.15f;

        // Crece de golpe y luego se encoge hasta desaparecer (en vez de volverse transparente)
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

        // Gira despacio mientras vive
        ParticleSystem.RotationOverLifetimeModule rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = true;
        rol.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rol.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rol.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = Icosaedro();
        r.alignment = ParticleSystemRenderSpace.World;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sharedMaterial = _material;

        ps.Play();
        _sistema = ps;
        return ps;
    }

    /// <summary>Icosaedro de 20 caras con normales por cara (se ve facetado, estilo low poly).</summary>
    static Mesh Icosaedro()
    {
        if (_malla != null) return _malla;

        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v =
        {
            new(-1,  t,  0), new( 1,  t,  0), new(-1, -t,  0), new( 1, -t,  0),
            new( 0, -1,  t), new( 0,  1,  t), new( 0, -1, -t), new( 0,  1, -t),
            new( t,  0, -1), new( t,  0,  1), new(-t,  0, -1), new(-t,  0,  1),
        };
        int[] caras =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };

        // Vértices duplicados por cara para que cada cara tenga su propia normal (facetado)
        var verts = new Vector3[caras.Length];
        var tris = new int[caras.Length];
        for (int i = 0; i < caras.Length; i += 3)
        {
            Vector3 a = v[caras[i]].normalized * 0.5f; // diámetro 1 = tamaño de la partícula
            Vector3 b = v[caras[i + 1]].normalized * 0.5f;
            Vector3 c = v[caras[i + 2]].normalized * 0.5f;

            // Unity dibuja la cara hacia donde apunta cross(b-a, c-a): que apunte hacia afuera
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) { Vector3 tmp = b; b = c; c = tmp; }

            verts[i] = a; verts[i + 1] = b; verts[i + 2] = c;
            tris[i] = i; tris[i + 1] = i + 1; tris[i + 2] = i + 2;
        }

        _malla = new Mesh { name = "IcosaedroPolvo", vertices = verts, triangles = tris };
        _malla.RecalculateNormals();
        _malla.RecalculateBounds();
        return _malla;
    }
}
