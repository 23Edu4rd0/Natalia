namespace GestaoComercial.Core;

/// <summary>Erro "esperado": carrega uma mensagem pronta para mostrar ao usuário.</summary>
public class AppException : Exception
{
    public AppException(string mensagem) : base(mensagem) { }
}

public class ValidacaoException : AppException { public ValidacaoException(string m) : base(m) { } }
public class NaoEncontradoException : AppException { public NaoEncontradoException(string m) : base(m) { } }
public class ConflitoException : AppException { public ConflitoException(string m) : base(m) { } }
public class EstoqueException : AppException { public EstoqueException(string m) : base(m) { } }
