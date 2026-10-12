using System.Globalization;
using System.Runtime.CompilerServices;

namespace RVM.UI.Tutor.Tests;

// Testes rodam em en-US (padrao-rvm §9): texto pt-BR que dependa da cultura da maquina quebra aqui,
// e nao no app do cliente.
internal static class CulturaDosTestes
{
    [ModuleInitializer]
    internal static void Fixar()
    {
        var cultura = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
    }
}
