using System.Text.RegularExpressions;

using RodajeIA.Web.Dominio;

namespace RodajeIA.Web.Guion;

/// <summary>
/// Valida y divide el guion según la convención del Anexo A (RF-03, RF-04). Es determinístico y no usa IA.
/// Reporta todos los errores que encuentra; si hay alguno, no devuelve ninguna escena.
/// </summary>
public static partial class ParserGuion
{
    public const int MaxEscenas = 10;
    public const int MaxClipsPorEscena = 20;

    private const string FormatoEncabezado = "ESCENA N — INT. LUGAR — MOMENTO DEL DÍA";
    private const string MarcadorTextoEnPantalla = "TEXTO EN PANTALLA";

    private static readonly string[] MarcadoresCampo = ["LOCACIÓN:", "ILUMINACIÓN:", "PUESTA EN ESCENA:", "AUDIO:"];

    public static ResultadoGuion Procesar(string texto)
    {
        var lineas = texto.Split('\n');
        var errores = new List<ErrorGuion>();
        var escenas = new List<EscenaEnConstruccion>();
        EscenaEnConstruccion? escena = null;
        ClipEnConstruccion? clip = null;
        var primeraLineaConTexto = 0;

        for (var i = 0; i < lineas.Length; i++)
        {
            var numeroLinea = i + 1;
            var linea = lineas[i].Trim();
            if (linea.Length == 0)
            {
                continue;
            }

            if (primeraLineaConTexto == 0)
            {
                primeraLineaConTexto = numeroLinea;
            }

            if (EmpiezaEscena().IsMatch(linea))
            {
                escena = AbrirEscena(linea, numeroLinea, escenas.Count + 1, errores);
                escenas.Add(escena);
                clip = null;
                continue;
            }

            if (escena is null)
            {
                // Se reporta solo la primera línea fuera de escena para no repetir el mismo error.
                if (errores.Count == 0)
                {
                    errores.Add(new(numeroLinea, $"Línea fuera de una escena: el guion debe empezar con un encabezado `{FormatoEncabezado}`."));
                }

                continue;
            }

            if (EmpiezaClip().IsMatch(linea))
            {
                clip = AbrirClip(linea, numeroLinea, escena, errores);
                escena.Clips.Add(clip);
                continue;
            }

            var marcador = MarcadoresCampo.FirstOrDefault(m => linea.StartsWith(m, StringComparison.Ordinal));
            if (marcador is not null)
            {
                LeerCampo(marcador, linea, numeroLinea, escena, clip, errores);
                continue;
            }

            if (clip is null)
            {
                errores.Add(new(numeroLinea, $"Línea inesperada antes del primer CLIP de la escena {escena.Numero}: solo se permiten {string.Join(", ", MarcadoresCampo)}"));
                continue;
            }

            if (linea.StartsWith(MarcadorTextoEnPantalla, StringComparison.Ordinal))
            {
                LeerTextoEnPantalla(linea, numeroLinea, escena, clip, errores);
                continue;
            }

            var dialogo = MarcadorDialogo().Match(linea);
            if (dialogo.Success)
            {
                var acotacion = dialogo.Groups["acotacion"];
                clip.Dialogos.Add(new LineaDialogo(
                    $"c{clip.Numero}-l{clip.Dialogos.Count + 1}",
                    dialogo.Groups["personaje"].Value,
                    acotacion.Success ? acotacion.Value.Trim() : null,
                    dialogo.Groups["texto"].Value));
                continue;
            }

            if (PareceDialogo().IsMatch(linea))
            {
                errores.Add(new(numeroLinea, "Diálogo mal formado: se espera `PERSONAJE: \"texto\"` o `PERSONAJE (acotación): \"texto\"`, con comillas rectas (\"…\") o tipográficas (“…”)."));
                continue;
            }

            clip.Accion.Add(linea);
        }

        if (escenas.Count == 0)
        {
            var motivo = primeraLineaConTexto == 0
                ? "El guion está vacío."
                : $"El guion no contiene ningún encabezado de escena `{FormatoEncabezado}`.";
            return ResultadoGuion.Invalido([new(Math.Max(primeraLineaConTexto, 1), motivo)]);
        }

        foreach (var e in escenas)
        {
            ValidarEscenaCompleta(e, errores);
        }

        if (errores.Count > 0)
        {
            return ResultadoGuion.Invalido(errores.OrderBy(e => e.Linea).ToList());
        }

        return ResultadoGuion.Valido(escenas.Select(e => e.Construir()).ToList());
    }

    private static EscenaEnConstruccion AbrirEscena(string linea, int numeroLinea, int esperado, List<ErrorGuion> errores)
    {
        var escena = new EscenaEnConstruccion(numeroLinea, esperado);

        if (esperado == MaxEscenas + 1)
        {
            errores.Add(new(numeroLinea, $"Se superó el límite de {MaxEscenas} escenas por episodio."));
        }

        var encabezado = EncabezadoEscena().Match(linea);
        if (!encabezado.Success)
        {
            errores.Add(new(numeroLinea, $"Encabezado de escena mal formado: se espera `{FormatoEncabezado}` (INT. o EXT.)."));
            return escena;
        }

        var numero = int.Parse(encabezado.Groups["numero"].Value);
        if (numero != esperado)
        {
            errores.Add(new(numeroLinea, $"Numeración de escenas incorrecta: se esperaba ESCENA {esperado} y dice ESCENA {numero}."));
        }

        escena.Tipo = encabezado.Groups["tipo"].Value == "INT" ? TipoLocacion.Int : TipoLocacion.Ext;
        escena.Lugar = encabezado.Groups["lugar"].Value.Trim();
        escena.MomentoDelDia = encabezado.Groups["momento"].Value.Trim();
        return escena;
    }

    private static ClipEnConstruccion AbrirClip(string linea, int numeroLinea, EscenaEnConstruccion escena, List<ErrorGuion> errores)
    {
        var esperado = escena.Clips.Count + 1;
        var clip = new ClipEnConstruccion(numeroLinea, esperado);

        if (esperado == MaxClipsPorEscena + 1)
        {
            errores.Add(new(numeroLinea, $"Se superó el límite de {MaxClipsPorEscena} clips en la escena {escena.Numero}."));
        }

        var encabezado = MarcadorClip().Match(linea);
        if (!encabezado.Success)
        {
            errores.Add(new(numeroLinea, "Marcador de clip mal formado: se espera `CLIP N`, solo en su línea."));
            return clip;
        }

        var numero = int.Parse(encabezado.Groups["numero"].Value);
        if (numero != esperado)
        {
            errores.Add(new(numeroLinea, $"Numeración de clips incorrecta en la escena {escena.Numero}: se esperaba CLIP {esperado} y dice CLIP {numero}."));
        }

        return clip;
    }

    private static void LeerCampo(string marcador, string linea, int numeroLinea, EscenaEnConstruccion escena, ClipEnConstruccion? clip, List<ErrorGuion> errores)
    {
        if (clip is not null)
        {
            errores.Add(new(numeroLinea, $"`{marcador}` va antes del primer CLIP de la escena, no dentro de un clip."));
            return;
        }

        if (escena.Campos.ContainsKey(marcador))
        {
            errores.Add(new(numeroLinea, $"`{marcador}` está repetido en la escena {escena.Numero}."));
            return;
        }

        // Se registra aunque esté vacío, para no reportar además que falta.
        var valor = linea[marcador.Length..].Trim();
        escena.Campos[marcador] = valor;
        if (valor.Length == 0)
        {
            errores.Add(new(numeroLinea, $"`{marcador}` está vacío."));
        }
    }

    private static void LeerTextoEnPantalla(string linea, int numeroLinea, EscenaEnConstruccion escena, ClipEnConstruccion clip, List<ErrorGuion> errores)
    {
        var texto = MarcadorTextoEnPantallaRegex().Match(linea);
        if (!texto.Success)
        {
            errores.Add(new(numeroLinea, "Texto en pantalla mal formado: se espera `TEXTO EN PANTALLA: \"texto\"`, con comillas rectas (\"…\") o tipográficas (“…”)."));
            return;
        }

        if (clip.TextoEnPantalla is not null)
        {
            errores.Add(new(numeroLinea, $"El CLIP {clip.Numero} de la escena {escena.Numero} ya tiene un TEXTO EN PANTALLA; se permite uno por clip."));
            return;
        }

        clip.TextoEnPantalla = texto.Groups["texto"].Value;
    }

    private static void ValidarEscenaCompleta(EscenaEnConstruccion escena, List<ErrorGuion> errores)
    {
        foreach (var marcador in MarcadoresCampo.Where(m => !escena.Campos.ContainsKey(m)))
        {
            errores.Add(new(escena.Linea, $"A la escena {escena.Numero} le falta la línea `{marcador}`."));
        }

        if (escena.Clips.Count == 0)
        {
            errores.Add(new(escena.Linea, $"La escena {escena.Numero} no tiene ningún `CLIP N`; debe tener al menos uno."));
        }

        foreach (var clip in escena.Clips.Where(c => c.EstaVacio))
        {
            errores.Add(new(clip.Linea, $"El CLIP {clip.Numero} de la escena {escena.Numero} está vacío."));
        }
    }

    [GeneratedRegex(@"^ESCENA\b")]
    private static partial Regex EmpiezaEscena();

    [GeneratedRegex(@"^ESCENA (?<numero>\d+) — (?<tipo>INT|EXT)\. (?<lugar>\S.*?) — (?<momento>\S.*)$")]
    private static partial Regex EncabezadoEscena();

    [GeneratedRegex(@"^CLIP\b")]
    private static partial Regex EmpiezaClip();

    [GeneratedRegex(@"^CLIP (?<numero>\d+)$")]
    private static partial Regex MarcadorClip();

    // Nombre en mayúsculas (puede tener varias palabras), acotación opcional entre paréntesis y texto entre comillas
    // rectas ("…") o tipográficas (“…”); las de apertura y cierre deben ser del mismo tipo.
    [GeneratedRegex(@"^(?<personaje>\p{Lu}[\p{Lu}.]*(?: [\p{Lu}.]+)*)(?: \((?<acotacion>[^()]*\S[^()]*)\))?: (?:""(?<texto>.*\S.*)""|“(?<texto>.*\S.*)”)$")]
    private static partial Regex MarcadorDialogo();

    // Línea que empieza como un marcador de diálogo (nombre en mayúsculas seguido de ":" o de comillas)
    // pero no cumple el formato completo; así no se toma por acción un diálogo mal escrito.
    [GeneratedRegex(@"^\p{Lu}[\p{Lu}.]*(?: [\p{Lu}.]+)*(?: \([^)]*\))?\s*[:""“]")]
    private static partial Regex PareceDialogo();

    [GeneratedRegex(@"^TEXTO EN PANTALLA: (?:""(?<texto>.*\S.*)""|“(?<texto>.*\S.*)”)$")]
    private static partial Regex MarcadorTextoEnPantallaRegex();

    private sealed class EscenaEnConstruccion(int linea, int numero)
    {
        public int Linea { get; } = linea;

        public int Numero { get; } = numero;

        public TipoLocacion Tipo { get; set; }

        public string Lugar { get; set; } = "";

        public string MomentoDelDia { get; set; } = "";

        public Dictionary<string, string> Campos { get; } = [];

        public List<ClipEnConstruccion> Clips { get; } = [];

        public Escena Construir() => new(
            Numero,
            Tipo,
            Lugar,
            MomentoDelDia,
            Campos["LOCACIÓN:"],
            Campos["ILUMINACIÓN:"],
            Campos["PUESTA EN ESCENA:"],
            Campos["AUDIO:"],
            Clips.Select(c => c.Construir()).ToList());
    }

    private sealed class ClipEnConstruccion(int linea, int numero)
    {
        public int Linea { get; } = linea;

        public int Numero { get; } = numero;

        public List<string> Accion { get; } = [];

        public List<LineaDialogo> Dialogos { get; } = [];

        public string? TextoEnPantalla { get; set; }

        public bool EstaVacio => Accion.Count == 0 && Dialogos.Count == 0 && TextoEnPantalla is null;

        public Clip Construir() => new(Numero, Accion, Dialogos, TextoEnPantalla);
    }
}
