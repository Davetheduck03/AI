using UnityEngine;

/// <summary>
/// Builds and launches one-shot particle effects entirely in code — no prefabs required.
/// All effects self-destroy after their lifetime expires.
/// </summary>
public static class ParticleFactory
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Soft green sparkles that float upward from the heal target's position.
    /// </summary>
    public static void SpawnHealEffect(Vector3 position)
    {
        GameObject go = new GameObject("VFX_Heal");
        go.transform.position = position;

        var ps  = go.AddComponent<ParticleSystem>();
        var mr  = go.GetComponent<ParticleSystemRenderer>();

        // Renderer — bright additive green
        mr.material          = CreateAdditiveMaterial(new Color(0.1f, 1f, 0.3f));
        mr.sortingLayerName  = "Entities";
        mr.sortingOrder      = 100;

        var main = ps.main;
        main.duration          = 0.6f;
        main.loop              = false;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSize         = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor        = new ParticleSystem.MinMaxGradient(
                                     new Color(0.3f, 1f, 0.4f, 1f),
                                     new Color(0.8f, 1f, 0.8f, 1f));
        main.gravityModifier   = -0.4f;           // float upward
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.maxParticles      = 30;

        var emit = ps.emission;
        emit.rateOverTime = 0f;
        emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 25) });

        var shape = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Circle;
        shape.radius     = 0.3f;

        // Fade out over lifetime
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Cross/plus flash overlay — a brief white ring
        SpawnHealRing(position);

        ps.Play();
        UnityEngine.Object.Destroy(go, main.duration + 1.2f);
    }

    /// <summary>
    /// Small yellow-orange burst at arrow impact point.
    /// </summary>
    public static void SpawnArrowImpact(Vector3 position)
    {
        GameObject go = new GameObject("VFX_ArrowImpact");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        var mr = go.GetComponent<ParticleSystemRenderer>();

        mr.material         = CreateAdditiveMaterial(new Color(1f, 0.75f, 0.1f));
        mr.sortingLayerName = "Entities";
        mr.sortingOrder     = 100;

        var main = ps.main;
        main.duration        = 0.2f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 0.9f, 0.2f, 1f),
                                   new Color(1f, 0.5f, 0.0f, 1f));
        main.gravityModifier = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 20;

        var emit = ps.emission;
        emit.rateOverTime = 0f;
        emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 16) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.08f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        ps.Play();
        UnityEngine.Object.Destroy(go, 0.8f);
    }

    /// <summary>
    /// Purple AoE burst at mage impact point. Radius scales with the attack range.
    /// </summary>
    public static void SpawnMageImpact(Vector3 position, float aoeRadius)
    {
        // Central flash
        {
            GameObject go = new GameObject("VFX_MageImpact");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            var mr = go.GetComponent<ParticleSystemRenderer>();

            mr.material         = CreateAdditiveMaterial(new Color(0.7f, 0.2f, 1f));
            mr.sortingLayerName = "Entities";
            mr.sortingOrder     = 100;

            var main = ps.main;
            main.duration        = 0.25f;
            main.loop            = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(2f, aoeRadius * 1.5f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                                       new Color(0.9f, 0.4f, 1f, 1f),
                                       new Color(0.4f, 0.1f, 1f, 1f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 40;

            var emit = ps.emission;
            emit.rateOverTime = 0f;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 28, 36) });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius    = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.5f, 0f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ps.Play();
            UnityEngine.Object.Destroy(go, 1.2f);
        }

        // Ring of sparks spreading outward to the AoE edge
        {
            GameObject ring = new GameObject("VFX_MageRing");
            ring.transform.position = position;

            var ps = ring.AddComponent<ParticleSystem>();
            var mr = ring.GetComponent<ParticleSystemRenderer>();

            mr.material         = CreateAdditiveMaterial(new Color(0.5f, 0.1f, 1f));
            mr.sortingLayerName = "Entities";
            mr.sortingOrder     = 100;

            var main = ps.main;
            main.duration        = 0.15f;
            main.loop            = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(aoeRadius * 0.8f, aoeRadius * 1.2f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.10f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                                       new Color(0.8f, 0.5f, 1f, 1f),
                                       new Color(0.3f, 0f, 0.8f, 1f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 24;

            var emit = ps.emission;
            emit.rateOverTime = 0f;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 18, 22) });

            // Emit outward from a disc (2D ring)
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius    = 0.05f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ps.Play();
            UnityEngine.Object.Destroy(ring, 1.0f);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Brief expanding white/green ring drawn at the heal target — signals "healed!".
    /// </summary>
    private static void SpawnHealRing(Vector3 position)
    {
        GameObject go = new GameObject("VFX_HealRing");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        var mr = go.GetComponent<ParticleSystemRenderer>();
        mr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        mr.material         = CreateAdditiveMaterial(new Color(0.4f, 1f, 0.5f));
        mr.sortingLayerName = "Entities";
        mr.sortingOrder     = 99;

        var main = ps.main;
        main.duration          = 0.1f;
        main.loop              = false;
        main.startLifetime     = 0.35f;
        main.startSpeed        = 0f;
        main.startSize         = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startColor        = new Color(0.6f, 1f, 0.7f, 0.9f);
        main.gravityModifier   = 0f;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.maxParticles      = 8;

        var emit = ps.emission;
        emit.rateOverTime = 0f;
        emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 8) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.25f;

        // Grow outward over lifetime
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 2.5f);
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        ps.Play();
        UnityEngine.Object.Destroy(go, 0.6f);
    }

    /// <summary>
    /// Creates a simple Sprites/Default material in Additive blend mode tinted to <paramref name="color"/>.
    /// Used so particles glow without needing any imported material asset.
    /// </summary>
    private static Material CreateAdditiveMaterial(Color color)
    {
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // additive
        mat.SetInt("_ZWrite",   0);
        mat.color = color;
        return mat;
    }
}
