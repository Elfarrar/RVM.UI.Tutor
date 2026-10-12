using System.Globalization;

namespace RVM.UI.Tutor.Tests;

public class CulturaDosTestesTests
{
    [Fact]
    public void Testes_rodam_em_en_US()
    {
        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
    }
}
