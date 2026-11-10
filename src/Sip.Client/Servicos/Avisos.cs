namespace Sip.Client.Servicos;

public enum TipoAviso { Sucesso, Erro, Alerta, Info }

public sealed record Aviso(Guid Id, TipoAviso Tipo, string Texto);

/// <summary>Mensagens rápidas no topo da tela (equivalente ao flash() do Flask).</summary>
public sealed class Avisos
{
    private readonly List<Aviso> _lista = [];
    public IReadOnlyList<Aviso> Lista => _lista;
    public event Action? Mudou;

    public void Sucesso(string texto) => Adicionar(TipoAviso.Sucesso, texto);
    public void Erro(string texto) => Adicionar(TipoAviso.Erro, texto);
    public void Alerta(string texto) => Adicionar(TipoAviso.Alerta, texto);
    public void Info(string texto) => Adicionar(TipoAviso.Info, texto);

    public void Remover(Guid id)
    {
        _lista.RemoveAll(a => a.Id == id);
        Mudou?.Invoke();
    }

    private void Adicionar(TipoAviso tipo, string texto)
    {
        var aviso = new Aviso(Guid.NewGuid(), tipo, texto);
        _lista.Add(aviso);
        Mudou?.Invoke();
        // Some sozinho depois de alguns segundos (erros ficam mais tempo).
        _ = Task.Delay(tipo == TipoAviso.Erro ? 8000 : 4500).ContinueWith(_ => Remover(aviso.Id));
    }
}
