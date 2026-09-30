namespace RodajeIA.Web.Configuracion;

public sealed record CategoriaVocabulario(bool Obligatorio, IReadOnlyList<string> Valores, string? Descripcion = null);

/// <summary>Vocabulario técnico cerrado (RN-02, Anexo E), cargado desde <c>config/vocabulario.json</c>.</summary>
public sealed record Vocabulario(string Version, IReadOnlyDictionary<string, CategoriaVocabulario> Categorias)
{
    public const string Plano = "plano";
    public const string Optica = "optica";
    public const string Iluminacion = "iluminacion";
    public const string Angulo = "angulo";
    public const string Movimiento = "movimiento";

    /// <summary>Categorías que exige el PRD y si cada una es obligatoria en la toma.</summary>
    public static readonly IReadOnlyDictionary<string, bool> CategoriasEsperadas = new Dictionary<string, bool>
    {
        [Plano] = true,
        [Optica] = true,
        [Iluminacion] = true,
        [Angulo] = false,
        [Movimiento] = false,
    };

    public bool Permite(string categoria, string valor) =>
        Categorias.TryGetValue(categoria, out var c) && c.Valores.Contains(valor);

    internal void Validar(string archivo)
    {
        if (string.IsNullOrWhiteSpace(Version))
        {
            throw new InvalidOperationException($"{archivo}: falta la versión.");
        }

        var sobrantes = Categorias.Keys.Except(CategoriasEsperadas.Keys).ToList();
        if (sobrantes.Count > 0)
        {
            throw new InvalidOperationException($"{archivo}: categorías no permitidas: {string.Join(", ", sobrantes)}.");
        }

        foreach (var (nombre, obligatorio) in CategoriasEsperadas)
        {
            if (!Categorias.TryGetValue(nombre, out var categoria))
            {
                throw new InvalidOperationException($"{archivo}: falta la categoría '{nombre}'.");
            }

            if (categoria.Obligatorio != obligatorio)
            {
                throw new InvalidOperationException(
                    $"{archivo}: la categoría '{nombre}' debe tener obligatorio = {obligatorio.ToString().ToLowerInvariant()}.");
            }

            if (categoria.Valores is null || categoria.Valores.Count == 0)
            {
                throw new InvalidOperationException($"{archivo}: la categoría '{nombre}' no tiene valores.");
            }

            if (categoria.Valores.Any(v => string.IsNullOrWhiteSpace(v) || v != v.Trim()))
            {
                throw new InvalidOperationException(
                    $"{archivo}: la categoría '{nombre}' tiene un valor vacío o con espacios en los extremos.");
            }

            var repetido = categoria.Valores.GroupBy(v => v).FirstOrDefault(g => g.Count() > 1);
            if (repetido is not null)
            {
                throw new InvalidOperationException(
                    $"{archivo}: la categoría '{nombre}' repite el valor '{repetido.Key}'.");
            }
        }
    }
}
