using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// A purely code-driven projectile that travels from <c>origin</c> to <c>target</c>
/// and fires an on-arrive callback when it lands.
///
/// Visuals are built entirely via TrailRenderer + a tiny SpriteRenderer dot — no
/// sprite assets required.  Both arrow (yellow) and magic bolt (purple) variants
/// are supported through the static factory methods.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class VFXProjectile : MonoBehaviour
{
    // ── Static factory methods ────────────────────────────────────────────────

    /// <summary>Fires an arrow from <paramref name="origin"/> to <paramref name="target"/>.</summary>
    public static void SpawnArrow(Vector3 origin, Vector3 target)
    {
        float   dist  = Vector3.Distance(origin, target);
        float   speed = Mathf.Max(18f, dist / 0.25f);   // always arrives in ≤0.25 s

        // Arrow colour scheme: warm yellow → orange
        Color headColor  = new Color(1.0f, 0.85f, 0.2f, 1f);
        Color trailColor = new Color(1.0f, 0.55f, 0.1f, 0.6f);

        var go = BuildProjectile("VFX_Arrow", origin, target,
                                 headSize: 0.09f, headColor, trailColor,
                                 trailTime: 0.10f, speed,
                                 onArrive: pos => ParticleFactory.SpawnArrowImpact(pos));

        // Rotate the arrow to face the direction of travel
        Vector3 dir = (target - origin).normalized;
        float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>Fires a magic bolt from <paramref name="origin"/> to <paramref name="target"/>.</summary>
    public static void SpawnMageBolt(Vector3 origin, Vector3 target, float aoeRadius = 2f)
    {
        float dist  = Vector3.Distance(origin, target);
        float speed = Mathf.Max(14f, dist / 0.30f);   // arrives in ≤0.30 s

        // Magic colour scheme: bright lavender → deep violet
        Color headColor  = new Color(0.85f, 0.4f, 1.0f, 1f);
        Color trailColor = new Color(0.4f,  0.1f, 1.0f, 0.7f);

        BuildProjectile("VFX_MageBolt", origin, target,
                        headSize: 0.14f, headColor, trailColor,
                        trailTime: 0.18f, speed,
                        onArrive: pos => ParticleFactory.SpawnMageImpact(pos, aoeRadius));

        // Add a secondary sparkle trail
        SpawnMageBoltGlow(origin, target, speed);
    }

    // ── Internal builder ──────────────────────────────────────────────────────

    private static GameObject BuildProjectile(
        string name,
        Vector3 origin, Vector3 target,
        float headSize, Color headColor, Color trailColor,
        float trailTime, float speed,
        Action<Vector3> onArrive)
    {
        var go = new GameObject(name);
        go.transform.position = origin;

        // ── Head: tiny solid dot ──────────────────────────────────────────────
        var sr       = go.AddComponent<SpriteRenderer>();
        sr.sprite    = CreateCircleSprite();
        sr.color     = headColor;
        sr.sortingLayerName = "Entities";
        sr.sortingOrder     = 102;
        go.transform.localScale = Vector3.one * headSize;

        // ── Trail ─────────────────────────────────────────────────────────────
        var tr         = go.AddComponent<TrailRenderer>();
        tr.time        = trailTime;
        tr.startWidth  = headSize * 1.4f;
        tr.endWidth    = 0f;
        tr.numCapVertices = 3;
        tr.autodestruct   = false;
        tr.sortingLayerName = "Entities";
        tr.sortingOrder     = 101;

        // Simple two-stop gradient: head colour → transparent tail
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(headColor,  0f), new GradientColorKey(trailColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = grad;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite",   0);
        tr.material = mat;

        // ── Movement ──────────────────────────────────────────────────────────
        var proj = go.AddComponent<VFXProjectile>();
        proj.StartCoroutine(proj.Travel(origin, target, speed, onArrive));

        return go;
    }

    /// <summary>
    /// Extra glow particles that follow the magic bolt's path — gives it a
    /// "shedding sparks" look without any additional materials.
    /// </summary>
    private static void SpawnMageBoltGlow(Vector3 origin, Vector3 target, float speed)
    {
        var go = new GameObject("VFX_MageBoltGlow");
        go.transform.position = origin;

        var ps = go.AddComponent<ParticleSystem>();
        var mr = go.GetComponent<ParticleSystemRenderer>();

        mr.material         = new Material(Shader.Find("Sprites/Default"));
        mr.material.color   = new Color(0.6f, 0.2f, 1f);
        mr.sortingLayerName = "Entities";
        mr.sortingOrder     = 100;

        float dist     = Vector3.Distance(origin, target);
        float duration = dist / speed;

        var main = ps.main;
        main.duration        = duration;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.9f, 0.5f, 1f, 1f),
                                   new Color(0.3f, 0f,   1f, 1f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 60;

        var emit = ps.emission;
        emit.rateOverTime = 40f;

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

        // Attach to a follower that moves with the bolt
        var follower = go.AddComponent<VFXProjectile>();
        follower.StartCoroutine(follower.Travel(origin, target, speed, _ => { }));

        ps.Play();
        UnityEngine.Object.Destroy(go, duration + 0.4f);
    }

    // ── Coroutine movement ────────────────────────────────────────────────────

    private IEnumerator Travel(Vector3 from, Vector3 to, float speed, Action<Vector3> onArrive)
    {
        float dist    = Vector3.Distance(from, to);
        float elapsed = 0f;
        float dur     = dist / speed;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / dur);
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        transform.position = to;
        onArrive?.Invoke(to);

        // Let the trail fade before destroying
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        yield return new WaitForSeconds(0.2f);
        Destroy(gameObject);
    }

    // ── Sprite helper ─────────────────────────────────────────────────────────

    private static Sprite _circleSprite;

    /// <summary>
    /// Returns a shared 32×32 filled-circle sprite generated in code.
    /// Cached after first creation.
    /// </summary>
    private static Sprite CreateCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int size   = 32;
        const int radius = 14;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        float cx = size / 2f, cy = size / 2f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            tex.SetPixel(x, y, (dx * dx + dy * dy) <= radius * radius ? white : clear);
        }

        tex.Apply();
        _circleSprite = Sprite.Create(tex,
                            new Rect(0, 0, size, size),
                            new Vector2(0.5f, 0.5f),
                            size);
        return _circleSprite;
    }
}
