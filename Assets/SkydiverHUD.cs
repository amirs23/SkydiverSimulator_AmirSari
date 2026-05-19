using UnityEngine;
using TMPro;

// Attach to Main Camera. Drag the canopy Rigidbody into the slot in the Inspector.
// Creates its own world-space canvas automatically — no manual UI setup needed.
// Shows zeros as placeholders until EOM_Solver physics are running.
public class SkydiverHUD : MonoBehaviour
{
    [Tooltip("The canopy Rigidbody — source of altitude, speed, and heading data")]
    public Rigidbody canopyRigidbody;

    TextMeshProUGUI _altText, _spdText, _hdgText;

    void Awake()
    {
        BuildHUD();
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject("SkydiverHUD");
        canvasGO.transform.SetParent(transform, false);

        // Position: top-left corner, 2m in front of camera
        canvasGO.transform.localPosition = new Vector3(-1.8f, 0.8f, 2f);
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * 0.002f;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(500, 180);

        _altText = MakeLabel(canvasGO.transform, new Vector2(0f,  60f));
        _spdText = MakeLabel(canvasGO.transform, new Vector2(0f,   0f));
        _hdgText = MakeLabel(canvasGO.transform, new Vector2(0f, -60f));

        Refresh(0f, 0f, 0f);
    }

    TextMeshProUGUI MakeLabel(Transform parent, Vector2 anchoredPos)
    {
        var go = new GameObject("HUDLabel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(500f, 55f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 42;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        return tmp;
    }

    void Update()
    {
        if (canopyRigidbody == null)
        {
            Refresh(0f, 0f, 0f);
            return;
        }

        float alt = canopyRigidbody.position.y;

        Vector3 vel = canopyRigidbody.linearVelocity;
        float spd = new Vector3(vel.x, 0f, vel.z).magnitude * 3.6f; // m/s → km/h

        float hdg = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
        if (hdg < 0f) hdg += 360f;

        Refresh(alt, spd, hdg);
    }

    void Refresh(float alt, float spd, float hdg)
    {
        _altText.text = $"ALT   {alt:F0} m";
        _spdText.text = $"SPD   {spd:F1} km/h";
        _hdgText.text = $"HDG   {(int)hdg:D3}°";
    }
}
