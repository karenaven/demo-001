using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using RodajeIA.Web.Components;
using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Datos;
using RodajeIA.Web.Generacion;
using RodajeIA.Web.Gestion;
using RodajeIA.Web.Prompt;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Vocabulario, molde y estilo versionados en config/ (se copian a la salida al compilar).
builder.Services.AddSingleton(ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config")));
builder.Services.AddSingleton<ArmadorPrompt>();

// SQLite: una ruta relativa se resuelve desde la carpeta del proyecto, así la app y `dotnet ef` usan el mismo archivo.
var cadenaSqlite = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("Rodaje"));
cadenaSqlite.DataSource = Path.Combine(builder.Environment.ContentRootPath, cadenaSqlite.DataSource);
builder.Services.AddDbContextFactory<RodajeDbContext>(o => o.UseSqlite(cadenaSqlite.ToString()));
builder.Services.AddScoped<ServicioSeries>();
builder.Services.AddScoped<ServicioPersonajes>();

// Gemini: la API key sale de user-secrets (Gemini:ApiKey); modelo y timeout de appsettings.json.
builder.Services.Configure<OpcionesGemini>(builder.Configuration.GetSection(OpcionesGemini.Seccion));
builder.Services.AddHttpClient<IClienteGemini, ClienteGemini>();
builder.Services.AddSingleton<ConstructorInstruccion>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GeneradorEscena>();

// Una sesión por pestaña (circuito de Blazor): el guion y sus prompts viven en memoria mientras esté abierta.
builder.Services.AddScoped<SesionGeneracion>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
