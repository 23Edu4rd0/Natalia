using Avalonia.Headless.XUnit;
using GestaoComercial.Core;
using Xunit;

namespace GestaoComercial.Tests;

public class UiSmokeTests
{
    [AvaloniaFact]
    public void Todas_as_abas_abrem_sem_erro()
    {
        using var a = new Ambiente();
        a.Janela.Show();
        foreach (var nome in a.Janela.Telas.Keys.ToList())
        {
            a.Janela.IrPara(nome);
            Ambiente.Rodar();
        }
        Assert.Empty(a.P.Msgs);   // nenhuma aba mostrou erro
        a.Foto("smoke_painel");
    }
}
