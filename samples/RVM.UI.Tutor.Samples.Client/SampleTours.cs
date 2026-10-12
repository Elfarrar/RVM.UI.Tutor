namespace RVM.UI.Tutor.Samples;

// Marcadores data-tour como constantes (ADR-004): o mesmo valor no .razor e no roteiro.
public static class SampleTargets
{
    public const string Menu = "menu";
    public const string NewEntry = "novo-lancamento";
    public const string EntryList = "lista-lancamentos";
    public const string Footer = "rodape";
    public const string SaveButton = "salvar";
    public const string SavedEntry = "lancamento-salvo";
    public const string Summary = "resumo";
    public const string Projects = "lista-obras";
    public const string NewProject = "nova-obra";
    public const string ProjectName = "nome-obra";
    public const string SaveProject = "salvar-obra";
    public const string Reports = "relatorios"; // nunca aparece: testa a política de alvo ausente
    public const string ThemePicker = "tema";
    public const string PresenterHelp = "ajuda-apresentador";
}

public static class SampleTours
{
    public const string User = "visitante";

    public static readonly TourDefinition Simple = new("samples-tour-simples", Version: 1,
    [
        new(Target: SampleTargets.Menu, Title: "Menu",
            Text: "Daqui você chega a todas as páginas do sistema."),
        new(Target: SampleTargets.NewEntry, Title: "Novo lançamento",
            Text: "Registre um custo da obra por aqui.", Placement: Placement.Right),
        new(Target: SampleTargets.EntryList, Title: "Lançamentos",
            Text: "Tudo o que foi registrado aparece nesta lista."),
        new(Target: SampleTargets.Footer, Title: "Ajuda",
            Text: "No fim da página ficam os contatos de suporte. O tour rolou até aqui sozinho."),
    ]);

    // Passos pulados no meio, dois seguidos (#37): enquanto espera o alvo (3 s no primeiro) aparece o
    // "Procurando o próximo passo…"; depois o tour segue para o passo seguinte.
    public static readonly TourDefinition WithSkip = new("samples-passo-pulado", Version: 1,
    [
        new(Target: null, Title: "Boas-vindas",
            Text: "O próximo passo aponta para algo que não existe e é pulado."),
        new(Target: SampleTargets.Reports, Title: "Relatórios",
            Text: "Não aparece: o alvo não existe.", WhenTargetMissing: MissingTargetPolicy.Skip),
        new(Target: SampleTargets.Reports, Title: "Relatórios de novo",
            Text: "Também não aparece: dois passos pulados seguidos.", WhenTargetMissing: MissingTargetPolicy.Skip, WaitForTargetMs: 500),
        new(Target: SampleTargets.EntryList, Title: "Lançamentos",
            Text: "O tour pulou o passo sem alvo e chegou aqui."),
    ]);

    public static readonly TourDefinition MultiPage = new("samples-multi-pagina", Version: 1,
    [
        new(Target: SampleTargets.Summary, Title: "Resumo",
            Text: "Os números do mês ficam aqui.", Route: "/multi-pagina"),
        new(Target: SampleTargets.Projects, Title: "Suas obras",
            Text: "Esta lista está em outra página: o tour veio até ela sozinho.", Route: "/multi-pagina/obras"),
        new(Target: SampleTargets.Summary, Title: "De volta",
            Text: "E pode voltar também. Fim do tour.", Route: "/multi-pagina"),
    ]);

    public static readonly TourDefinition WaitForAction = new("samples-espera-acao", Version: 1,
    [
        new(Target: SampleTargets.NewProject, Title: "Comece por aqui",
            Text: "Clique em \"Nova obra\" (ou Enter) para continuar.", Advance: AdvanceMode.ClickTarget),
        new(Target: SampleTargets.ProjectName, Title: "Dê um nome",
            Text: "Digite o nome da obra e saia do campo (Tab ou Enter).", Advance: AdvanceMode.InputFilled),
        new(Target: SampleTargets.SaveProject, Title: "Salvar",
            Text: "Clique em Salvar (ou Enter).", Advance: AdvanceMode.ClickTarget),
        new(Target: SampleTargets.Reports, Title: "Relatórios",
            Text: "Este passo aponta para algo que não existe e é pulado.",
            WhenTargetMissing: MissingTargetPolicy.Skip, WaitForTargetMs: 800),
    ]);

    public static readonly TourDefinition Theme = new("samples-tema", Version: 1,
    [
        new(Target: SampleTargets.ThemePicker, Title: "Tema",
            Text: "Troque o tema e abra o tour de novo para comparar."),
        new(Target: null, Title: "Sem alvo",
            Text: "Passo sem alvo: o balão aparece no meio da tela."),
    ]);

    // Apresentador genérico: a vitrine é do pacote neutro, a Aly mora no RVM.UI (ADR-002). As falas
    // são as de wwwroot/apresentador/roteiro.json, que gera os mp3 (tools/gerar-audio.py).
    public static readonly TourPresenter Guide = new("Guia", "apresentador/guia.svg", "apresentador/guia-animado.svg");

    public static readonly TourDefinition Presenter = new("samples-apresentador", Version: 1,
    [
        new(Target: null, Title: "Oi!",
            Text: "Eu sou o Guia. Vou te mostrar esta tela, e você me escuta enquanto lê.",
            Pose: "apresentador/guia-acena.svg", Audio: "apresentador/passo-1.mp3",
            PoseAnimated: "apresentador/guia-acena-animado.svg"),
        new(Target: SampleTargets.Menu, Title: "Menu",
            Text: "Pelo menu você chega a todas as páginas do sistema.", Audio: "apresentador/passo-2.mp3"),
        new(Target: SampleTargets.PresenterHelp, Title: "Do seu jeito",
            Text: "Se não quiser me ouvir, use Silenciar. Para seguir sem mim, Ocultar Guia.",
            Placement: Placement.Right, Audio: "apresentador/passo-3.mp3"),
        new(Target: null, Title: "Sem narração",
            Text: "Este passo não tem áudio: o Guia aparece, mas fica quieto."),
    ])
    { Presenter = Guide };
}
