using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuración de Entity Framework para PostgreSQL / Supabase
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// configuracion Kestrel para que escuche simultaneamente en el puerto publico 5005 y el puerto administrativo 8080
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5005); // Puerto Público
    options.ListenAnyIP(8080); // Puerto Administrativo
});

var app = builder.Build();

// agregamos Middleware de seguridad para bloquear el acceso al endpoint administrativo
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;

    // Si intentan ingresar al endpoint de diagnóstico
    if (!string.IsNullOrEmpty(path) && path.Equals("/api/kestrel-info", StringComparison.OrdinalIgnoreCase))
    {
        // Se verifica que la conexión provenga exclusivamente del puerto 8080
        if (context.Connection.LocalPort != 8080)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Acceso denegado: endpoint accesible por puerto administrativo (8080).");
            return; // Corta la ejecución de la petición aquí
        }
    }

    await next(); // Si pasa la validación (o es otra ruta), continúa a los endpoints
});

// 1. Endpoint para Unidad 1: Ciclo de Vida y Entornos
app.MapGet("/api/status", (IConfiguration config, IHostEnvironment env) =>
{
    var response = new
    {
        AppName = config["AppSettings:AppName"],
        Environment = env.EnvironmentName,
        EnableDetailesLogs = config.GetValue<bool>("AppSettings:FeatureFlag_EnableDetailedLogs"),
        DatabaseTimeout = config["DATABASE_TIMEOUT"] ?? "No definida"
        };
    return Results.Ok(response);
});

// 2. Endpoint para Unidad 3 y 4: Persistencia y prueba de lectura
//Metodo GET:
app.MapGet("/api/items", async (AppDbContext db) =>
{
var items = await db.Items.ToListAsync();
return Results.Ok(items);
});

// 3. Endpoint para probar escrituras / CORS desde clientes web
//Método POST:
app.MapPost("/api/items", async ([FromBody] string name, AppDbContext db) =>
{
if (string.IsNullOrWhiteSpace(name))
{return Results.BadRequest("El nombre del item no puede estar vacío.");
}
var newItem = new Item{Name = name, CreatedAt = DateTime.UtcNow};
db.Items.Add(newItem);
await db.SaveChangesAsync();
return Results.Created($"/api/items/{newItem.Id}", newItem);
});



// Endpoint de diagnostico para Kestrel
app.MapGet("/api/kestrel-info", () =>
{
    // Obtención de métricas del ThreadPool
    ThreadPool.GetAvailableThreads(out int workerAvailable, out int completionAvailable);
    ThreadPool.GetMaxThreads(out int workerMax, out int completionMax);

    // Obtención de memoria utilizada por el proceso
    using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
    double workingSetMb = Math.Round(currentProcess.WorkingSet64 / (1024.0 * 1024.0), 2);

    var response = new
    {
        ThreadPool = new
        {
            WorkerThreads = new
            {
                Available = workerAvailable,
                Max = workerMax
            },
            CompletionPortThreads = new
            {
                Available = completionAvailable,
                Max = completionMax
            }
        },
        Memory = new
        {
            WorkingSetMB = workingSetMb
        },
        System = new
        {
            LogicalProcessors = Environment.ProcessorCount
        }
    };

    return Results.Ok(response);
});

app.Run();