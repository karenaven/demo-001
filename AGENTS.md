# AGENTS.md — RodajeIA

## Propósito
App web para productoras/es de series generadas con IA: convierte guiones en fichas técnicas (plano, cámara, iluminación) y prompts listos para copiar en la IA de video, manteniendo consistentes a los personajes entre escenas y episodios. Especificación completa: `PRD-002.md`.

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
- No usar IA para dividir el guion en escenas ni para detectar personajes (RF-04, RF-05): esos pasos deben ser determinísticos y testeables sin llamar al modelo.
- No hardcodear el vocabulario de plano/cámara/iluminación en el código ni inventar términos (RNF-04, RF-06): debe venir de un archivo de configuración versionado en el repo, que todavía no existe y se define aparte. La salida del modelo debe ser estructurada con enums de esa lista, nunca texto libre.
- No normalizar, corregir ni guardar parcialmente un guion con formato inválido (RF-03): en v1 se rechaza completo y lo corrige la/el guionista.
