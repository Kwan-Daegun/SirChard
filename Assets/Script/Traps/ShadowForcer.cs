using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class ShadowForcer : MonoBehaviour
{
    [Header("Shadow Settings")]
    public float shadowDistance = 50f;
    public float lightIntensity = 1.5f;
    public float shadowStrength = 0.85f;

    void Awake()
    {
        ForceLightShadows();
        ForceURPShadows();
        ForceRendererShadows();
    }

    // Run again after Start so runtime-built objects (Rubik cubies) are caught
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        ForceRendererShadows();
        Debug.Log("[ShadowForcer] Second pass complete — runtime objects covered.");
    }

    void ForceLightShadows()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            if (l.type != LightType.Directional) continue;
            // DO NOT change rotation — keep existing light angle
            l.shadows = LightShadows.Soft;
            l.shadowStrength = shadowStrength;
            l.shadowBias = 0.02f;
            l.shadowNormalBias = 0.2f;
            l.intensity = lightIntensity;
            l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
            Debug.Log($"[ShadowForcer] '{l.name}' shadows forced ON.");
        }
    }

    void ForceURPShadows()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) { Debug.LogWarning("[ShadowForcer] URP not found."); return; }

        var type = pipeline.GetType();
        var flags = System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Instance;

        var f1 = type.GetField("m_ShadowDistance", flags); f1?.SetValue(pipeline, shadowDistance);
        var f2 = type.GetField("m_SoftShadowsSupported", flags); f2?.SetValue(pipeline, true);
        var f3 = type.GetField("m_ShadowCascades", flags); f3?.SetValue(pipeline, 2);
        var f4 = type.GetField("m_MainLightShadowmapResolution", flags); f4?.SetValue(pipeline, 2048);

        Debug.Log("[ShadowForcer] URP shadows configured.");
    }

    void ForceRendererShadows()
    {
        var noShadow = new System.Collections.Generic.HashSet<string> {
            "S","P","Tr","Halo","AuraRing","DizzyStar","BlobShadow",
            "RubikShadow","Neon","Sticker","PeriDot","SpawnDisc",
            "FloorRing","CentreOrb","BeamA","BeamB","PanelDot",
            "BoxLight","SlicePivot","NL","Trail","VFXWorld","FaceGlow","ScanLine"
        };

        // Issue 1: LED floor should NOT receive shadows
        // so the player shadow disappears when standing on it (looks like it's lit up)
        var noReceive = new System.Collections.Generic.HashSet<string> {
            "LEDLampFloor","LEDFloor","LEDMat"
        };

        var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int castCount = 0, receiveCount = 0;

        foreach (var mr in renderers)
        {
            if (!mr || !mr.gameObject) continue;
            string n = mr.gameObject.name;

            if (noShadow.Contains(n) || n.Length <= 2) continue;

            // Issue 1: LED floor — no shadows on it
            // Checks name AND checks if it's a child of LEDLamp objects
            bool isLEDFloor = noReceive.Contains(n)
                || n == "LEDLampFloor"
                || n.ToLower().Contains("ledlamp")
                || n.ToLower().Contains("ledfloor");

            // Also check if parent is a LEDLamp
            if (!isLEDFloor && mr.transform.parent != null)
            {
                string parentName = mr.transform.parent.name.ToLower();
                isLEDFloor = parentName.Contains("ledlamp") || parentName.Contains("ledfloor");
            }

            if (isLEDFloor)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                continue;
            }

            // Ground/floor — receive shadows only
            if (n == "Ground" || n == "Floor" ||
                n.ToLower().Contains("ground") || n.ToLower().Contains("floor"))
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
                receiveCount++;
                continue;
            }

            // Issue 2: Cubie MUST cast shadows
            if (n == "Cubie")
            {
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                castCount++;
                continue;
            }

            // Everything else — cast AND receive
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            castCount++;
        }

        Debug.Log($"[ShadowForcer] {castCount} casting, {receiveCount} receive-only.");
    }
}