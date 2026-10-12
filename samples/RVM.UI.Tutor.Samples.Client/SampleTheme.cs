namespace RVM.UI.Tutor.Samples;

/// <summary>Tema do samples: força claro/escuro (data-tutor-theme) ou aplica variáveis --tutor-* próprias.</summary>
public sealed class SampleTheme
{
    public const string CustomStyle = "--tutor-accent:#15803d;--tutor-accent-fg:#ffffff;--tutor-radius:4px;--tutor-font:Georgia,serif";

    public string? Mode { get; private set; }
    public bool Custom { get; private set; }
    public string? Style => Custom ? CustomStyle : null;

    public event Action? Changed;

    public void Set(string? mode, bool custom)
    {
        Mode = mode;
        Custom = custom;
        Changed?.Invoke();
    }
}
