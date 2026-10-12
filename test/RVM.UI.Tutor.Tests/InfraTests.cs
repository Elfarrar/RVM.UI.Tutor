using System.Globalization;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RVM.UI.Tutor.Internal;

namespace RVM.UI.Tutor.Tests;

public class LocalStorageTutorProgressStoreTests : BunitContext
{
    private readonly LocalStorageTutorProgressStore _store;

    public LocalStorageTutorProgressStoreTests() => _store = new LocalStorageTutorProgressStore(JSInterop.JSRuntime);

    [Fact]
    public async Task Salva_em_json_na_chave_do_tour_e_usuario()
    {
        var progress = new TutorProgress("t", "u", 2, TourStatus.Completed, 3, new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        var set = JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        await _store.SaveAsync(progress);

        var call = Assert.Single(JSInterop.Invocations["localStorage.setItem"]);
        Assert.Equal("rvm-tutor:t:u", call.Arguments[0]);
        Assert.Equal(progress, JsonSerializer.Deserialize<TutorProgress>((string)call.Arguments[1]!));
    }

    [Fact]
    public async Task Le_o_que_foi_salvo()
    {
        var progress = new TutorProgress("t", "u", 1, TourStatus.InProgress, 1, DateTimeOffset.UnixEpoch);
        JSInterop.Setup<string?>("localStorage.getItem", "rvm-tutor:t:u").SetResult(JsonSerializer.Serialize(progress));
        Assert.Equal(progress, await _store.GetAsync("t", "u"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{nao e json")]
    public async Task Vazio_ou_corrompido_vale_como_nunca_visto(string? stored)
    {
        JSInterop.Setup<string?>("localStorage.getItem", "rvm-tutor:t:u").SetResult(stored);
        Assert.Null(await _store.GetAsync("t", "u"));
    }

    [Fact]
    public async Task Salvar_nulo_e_erro()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveAsync(null!));
    }
}

public class TutorTextsTests
{
    [Theory]
    [InlineData("pt-BR", "Next", "Próximo")]
    [InlineData("pt-BR", "Done", "Concluir")]
    [InlineData("pt-PT", "Skip", "Pular")]
    [InlineData("en", "Next", "Next")]
    [InlineData("en-US", "Back", "Back")]
    [InlineData("fr", "Close", "Close")]
    public void Textos_por_idioma_com_ingles_de_reserva(string culture, string key, string expected)
    {
        Assert.Equal(expected, TutorTexts.Get(key, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void Progresso_formatado_nos_dois_idiomas()
    {
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        Assert.Equal("2 de 7", string.Format(pt, TutorTexts.Get("Progress", pt), 2, 7));
        var en = CultureInfo.GetCultureInfo("en");
        Assert.Equal("2 of 7", string.Format(en, TutorTexts.Get("Progress", en), 2, 7));
    }

    [Fact]
    public void Chave_desconhecida_devolve_a_chave()
    {
        Assert.Equal("Nada", TutorTexts.Get("Nada", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Cultura_vem_da_opcao_ou_da_UI_corrente()
    {
        Assert.Equal("pt-BR", TutorTexts.CultureFor(new TutorOptions { Language = "pt-BR" }).Name);
        Assert.Equal(CultureInfo.CurrentUICulture, TutorTexts.CultureFor(new TutorOptions()));
    }
}

public class AddRvmTutorTests
{
    [Fact]
    public void Registra_servico_unico_por_escopo_opcoes_e_store_padrao()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new BunitContext().JSInterop.JSRuntime);
        services.AddRvmTutor(o => o.AllowSkip = false);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Same(scope.ServiceProvider.GetRequiredService<TutorService>(), scope.ServiceProvider.GetRequiredService<ITutor>());
        Assert.False(scope.ServiceProvider.GetRequiredService<TutorOptions>().AllowSkip);
        Assert.IsType<LocalStorageTutorProgressStore>(scope.ServiceProvider.GetRequiredService<ITutorProgressStore>());
    }

    [Fact]
    public void Store_do_app_vence_registrado_antes_ou_depois()
    {
        var before = new ServiceCollection();
        before.AddScoped<ITutorProgressStore, FakeProgressStore>();
        before.AddRvmTutor();
        using (var p = before.BuildServiceProvider())
            Assert.IsType<FakeProgressStore>(p.CreateScope().ServiceProvider.GetRequiredService<ITutorProgressStore>());

        var after = new ServiceCollection();
        after.AddRvmTutor();
        after.AddScoped<ITutorProgressStore, FakeProgressStore>();
        using (var p = after.BuildServiceProvider())
            Assert.IsType<FakeProgressStore>(p.CreateScope().ServiceProvider.GetRequiredService<ITutorProgressStore>());
    }

    [Fact]
    public void Services_nulo_e_erro()
    {
        Assert.Throws<ArgumentNullException>(() => TutorServiceCollectionExtensions.AddRvmTutor(null!));
    }
}
