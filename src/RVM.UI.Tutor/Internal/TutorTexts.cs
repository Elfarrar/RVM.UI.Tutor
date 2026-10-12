using System.Globalization;
using System.Resources;

namespace RVM.UI.Tutor.Internal;

/// <summary>Textos da biblioteca (Resources/TutorStrings*.resx): neutro em inglês, satélite <c>pt</c>.</summary>
internal static class TutorTexts
{
    private static readonly ResourceManager Resources =
        new("RVM.UI.Tutor.Resources.TutorStrings", typeof(TutorTexts).Assembly);

    public static CultureInfo CultureFor(TutorOptions options) =>
        options.Language is { Length: > 0 } language ? CultureInfo.GetCultureInfo(language) : CultureInfo.CurrentUICulture;

    public static string Get(string key, CultureInfo culture) => Resources.GetString(key, culture) ?? key;
}
