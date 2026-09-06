using UnityEngine;

public sealed class ClimbPlatformVisual : MonoBehaviour
{
    [SerializeField] private Renderer accent;
    [SerializeField] private Light bounce;
    private MaterialPropertyBlock properties;

    public void Configure(Renderer accentRenderer, Light bounceLight)
    {
        accent = accentRenderer;
        bounce = bounceLight;
    }

    public void SetIndex(int index)
    {
        Color color = index % 2 == 0 ? new Color(.01f, .7f, 1) : new Color(.6f, .015f, 1);
        if (accent != null)
        {
            properties ??= new MaterialPropertyBlock();
            accent.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_EmissionColor", color * 4);
            accent.SetPropertyBlock(properties);
        }
        if (bounce != null) bounce.color = color;
    }
}
