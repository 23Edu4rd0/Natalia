using System.Text;

namespace GestaoComercial.Core;

/// <summary>Versão, pastas de dados (mesmas da versão Python: o banco antigo continua valendo) e log.</summary>
public static class Config
{
    public const string NomeApp = "Gestão Comercial";
    public const string Versao = "2.4.0";

    /// <summary>Pasta de dados. Pode ser trocada com a variável de ambiente GESTAO_DATA_DIR.</summary>
    public static string PastaDados()
    {
        string baseDir;
        var over = Environment.GetEnvironmentVariable("GESTAO_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(over)) baseDir = over;
        else if (OperatingSystem.IsWindows())
        {
            var raiz = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrEmpty(raiz)) raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
            baseDir = Path.Combine(raiz, "GestaoComercial");
        }
        else if (OperatingSystem.IsMacOS())
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "GestaoComercial");
        else
        {
            var raiz = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(raiz)) raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            baseDir = Path.Combine(raiz, "gestao-comercial");
        }
        Directory.CreateDirectory(baseDir);
        return baseDir;
    }

    public static string Subpasta(string nome)
    {
        var p = Path.Combine(PastaDados(), nome);
        Directory.CreateDirectory(p);
        return p;
    }

    public static string CaminhoBanco() => Path.Combine(PastaDados(), "gestao.db");
    public static string ArquivoLog() => Path.Combine(Subpasta("logs"), "gestao.log");
}

/// <summary>Log em arquivo simples (nunca lança exceção).</summary>
public static class Log
{
    static readonly object Trava = new();

    public static void Info(string msg) => Escrever("INFO", msg, null);
    public static void Erro(string msg, Exception? ex = null) => Escrever("ERROR", msg, ex);

    static void Escrever(string nivel, string msg, Exception? ex)
    {
        try
        {
            lock (Trava)
            {
                var arq = Config.ArquivoLog();
                if (File.Exists(arq) && new FileInfo(arq).Length > 500_000)
                {
                    var velho = arq + ".1";
                    if (File.Exists(velho)) File.Delete(velho);
                    File.Move(arq, velho);
                }
                var sb = new StringBuilder();
                sb.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{nivel}] {msg}");
                if (ex != null) sb.Append('\n').Append(ex);
                File.AppendAllText(arq, sb.Append('\n').ToString(), new UTF8Encoding(false));
            }
        }
        catch { /* sem permissão de escrita no log não pode impedir o uso do programa */ }
    }
}
