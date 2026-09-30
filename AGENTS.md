# AGENTS.md — RodajeIA

## Propósito
App web para productoras/es de series generadas con IA: convierte guiones en bloques de tomas (plano, óptica, iluminación, mas ángulo y movimiento como opcionales) y prompts listos para copiar en la IA de video, manteniendo consistentes a los personajes entre escenas y episodios. Especificación completa: `PRD-002.md`.

## Stack
- .NET 10 — Blazor Web App (Interactive Server)
- SQLite + EF Core
- LLM: Gemini (Google)
- Tests: xUnit v3
- Lint: `dotnet format` (reglas en `.editorconfig`)

## Cómo correr
Solución: `RodajeIA.slnx` · App: `src/RodajeIA.Web` · Tests: `tests/RodajeIA.Tests`

```bash
# Instalar dependencias
dotnet tool restore
dotnet restore

# Configuración inicial (una vez)
dotnet user-secrets set "Gemini:ApiKey" "<tu-api-key>" --project src/RodajeIA.Web

# Levantar en local
dotnet ef database update --project src/RodajeIA.Web
dotnet run --project src/RodajeIA.Web

# Tests
dotnet test

# Linter
dotnet format --verify-no-changes
```

- Antes de dar por terminado cualquier cambio, correr el linter y los tests, y corregir lo que falle.
- Al agregar un paquete NuGet nuevo, usar la última versión estable (nunca preview/beta).
- No actualizar paquetes existentes sin preguntar.

## Qué NO hacer
- No usar IA para validar ni dividir el guion (escenas, campos de escena, clips, acción/diálogo/texto en pantalla) ni para detectar personajes (RF-03, RF-04, RF-05): son pasos determinísticos, testeables sin llamar al modelo. Un guion inválido se rechaza completo con línea y motivo; nunca se normaliza, corrige ni guarda en parte.
- No hardcodear ni inventar valores técnicos (RN-02): plano, óptica, iluminación, ángulo y movimiento (los dos últimos opcionales) salen solo del vocabulario JSON versionado en el repo, y el schema de salida estructurada de Gemini se genera desde ese archivo con cada campo como enum. No existen campos de cámara ni profundidad de campo: la cámara es fija y va en el estilo global. El único texto libre de la IA es la acción de cada toma.
- No dejar que la IA escriba lo que debe ser idéntico entre escenas (RN-03, RN-04): hojas de personaje (efectivas), diálogo con su acotación y texto en pantalla se insertan literales por plantilla. La IA solo los recibe como contexto y referencia las líneas de diálogo por id.
