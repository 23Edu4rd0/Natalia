using GestaoComercial.Core;
using Xunit;

namespace GestaoComercial.Tests;

public class UtilTests
{
    [Theory]
    [InlineData("12,50", 1250)] [InlineData("12.50", 1250)] [InlineData("1.234,56", 123456)] [InlineData("1,234.56", 123456)]
    [InlineData("R$ 3", 300)] [InlineData("1.234", 123400)] [InlineData("0,5", 50)] [InlineData("12.5", 1250)]
    [InlineData("0", 0)] [InlineData(" 7 ", 700)] [InlineData("10,005", 1001)]
    public void Dinheiro_valido(string txt, long esperado) => Assert.Equal(esperado, Util.ParseDinheiro(txt));

    [Theory]
    [InlineData("")] [InlineData("abc")] [InlineData("-5")] [InlineData("1,2,3")] [InlineData("12,5x")] [InlineData("1.2.3")]
    [InlineData("99999999999999")] [InlineData("99999999999999999999999999999999999")]
    public void Dinheiro_invalido(string txt) => Assert.Throws<ValidacaoException>(() => Util.ParseDinheiro(txt));

    [Fact]
    public void Formata_dinheiro()
    {
        Assert.Equal("R$ 1.234,56", Util.FmtDinheiro(123456));
        Assert.Equal("R$ 0,05", Util.FmtDinheiro(5));
        Assert.Equal("-R$ 2,50", Util.FmtDinheiro(-250));
        Assert.Equal("1.000.000,00", Util.FmtDinheiro(100000000, false));
    }

    [Fact]
    public void Inteiro()
    {
        Assert.Equal(5, Util.ParseInteiro("5"));
        foreach (var ruim in new[] { "", "1,5", "abc", "-1", "99999999999999999999" })
            Assert.Throws<ValidacaoException>(() => Util.ParseInteiro(ruim));
    }

    [Fact]
    public void Documentos()
    {
        Assert.True(Util.ValidaCpf("529.982.247-25"));
        Assert.False(Util.ValidaCpf("529.982.247-24"));
        Assert.False(Util.ValidaCpf("111.111.111-11"));
        Assert.True(Util.ValidaCnpj("11.222.333/0001-81"));
        Assert.False(Util.ValidaCnpj("11.222.333/0001-82"));
        Assert.Equal("529.982.247-25", Util.FmtDocumento("52998224725"));
        Assert.Equal("11.222.333/0001-81", Util.FmtDocumento("11222333000181"));
    }

    [Fact]
    public void Datas()
    {
        Assert.Equal("2024-03-05", Util.ParseDataBr("05/03/2024"));
        Assert.Equal("2024-03-05", Util.ParseDataBr("2024-03-05"));
        Assert.Throws<ValidacaoException>(() => Util.ParseDataBr("31/02/2024"));
        Assert.Throws<ValidacaoException>(() => Util.ParseDataBr(""));
        Assert.Equal("05/03/2024 14:30", Util.FmtData("2024-03-05 14:30:59"));
    }

    [Fact]
    public void Ordenacao_ignora_acentos_e_caixa()
    {
        var l = new[] { "Zebra", "água", "Abacate", "épico", "Ação" }.ToList();
        l.Sort(Util.ComparaNomes);
        Assert.Equal(new[] { "Abacate", "Ação", "água", "épico", "Zebra" }, l);
    }

    [Fact]
    public void Csv_protege_contra_formulas_e_escapa()
    {
        var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
        Util.ExportarCsv(p, new[] { "a", "b" }, new[] { new object?[] { "=1+1", "x;y" }, new object?[] { "ok", "com \"aspas\"" } });
        var txt = File.ReadAllText(p);
        File.Delete(p);
        Assert.Contains("'=1+1", txt);
        Assert.Contains("\"x;y\"", txt);
        Assert.Contains("\"com \"\"aspas\"\"\"", txt);
    }
}
