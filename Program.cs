using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configuración de Entity Framework para PostgreSQL
// La cadena de conexión no está en appsettings.json: llega por variable de entorno (ConnectionStrings__DefaultConnection)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// configuracion Kestrel para que escuche simultaneamente en el puerto publico 5000 y el puerto administrativo 8080
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // Puerto Público
    options.ListenAnyIP(8080); // Puerto Administrativo
});

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
// Creación automática del esquema en la base de datos si no existe
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

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
            WorkerThreads = new { Available = workerAvailable, Max = workerMax },
            CompletionPortThreads = new { Available = completionAvailable, Max = completionMax }
        },
        Memory = new { WorkingSetMB = workingSetMb },
        System = new { LogicalProcessors = Environment.ProcessorCount }
    };

    return Results.Ok(response);
});


//metodo GET para obtener las categorias
app.MapGet("/api/categories", async (AppDbContext db) => {
    var categories = await db.Categories.ToListAsync();
    return Results.Ok(categories);
});

//metodo POST para agregar una nueva categoria
app.MapPost("/api/categories", async ([FromBody] string name, AppDbContext db) => {
    if (string.IsNullOrWhiteSpace(name)){
        return Results.BadRequest("El nombre de la categoría no puede estar vacío.");
    }
    var newCategory = new Category{Name = name};
    db.Categories.Add(newCategory);
    await db.SaveChangesAsync();
    return Results.Created($"/api/categories/{newCategory.Id}", newCategory);
});

//metodo GET para obtener los items de una categoria (consulta filtrada)
app.MapGet("/api/categories/{categoryId}/items", async (int categoryId, AppDbContext db) => {
    var items = await db.Items.Where(i => i.CategoryId == categoryId).ToListAsync();
    return Results.Ok(items);
});

// ==================== ITEMS ====================

//metodo GET para obtener los items
app.MapGet("/api/items", async (AppDbContext db) => {
    var items = await db.Items.ToListAsync();
    return Results.Ok(items);
});

//metodo GET para obtener un item por su id
app.MapGet("/api/items/{id}", async (int id, AppDbContext db) => {
    var item = await db.Items.FindAsync(id);
    if (item is null){
        return Results.NotFound($"No existe el item con id {id}.");
    }
    return Results.Ok(item);
});

//metodo POST para agregar un nuevo item
app.MapPost("/api/items", async ([FromBody] Item item, AppDbContext db) => {
    if (string.IsNullOrWhiteSpace(item.Name)){
        return Results.BadRequest("El nombre del item no puede estar vacío.");
    }
    if (await db.Categories.FindAsync(item.CategoryId) is null){
        return Results.BadRequest($"No existe la categoría con id {item.CategoryId}.");
    }
    item.CreatedAt = DateTime.UtcNow;
    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/items/{item.Id}", item);
});

//metodo PUT para modificar un item existente
app.MapPut("/api/items/{id}", async (int id, [FromBody] Item datos, AppDbContext db) => {
    var item = await db.Items.FindAsync(id);
    if (item is null){
        return Results.NotFound($"No existe el item con id {id}.");
    }
    if (string.IsNullOrWhiteSpace(datos.Name)){
        return Results.BadRequest("El nombre del item no puede estar vacío.");
    }
    if (await db.Categories.FindAsync(datos.CategoryId) is null){
        return Results.BadRequest($"No existe la categoría con id {datos.CategoryId}.");
    }
    item.Name = datos.Name;
    item.Price = datos.Price;
    item.Stock = datos.Stock;
    item.CategoryId = datos.CategoryId;
    await db.SaveChangesAsync();
    return Results.Ok(item);
});

//metodo DELETE para eliminar un item (sus movimientos se borran en cascada)
app.MapDelete("/api/items/{id}", async (int id, AppDbContext db) => {
    var item = await db.Items.FindAsync(id);
    if (item is null){
        return Results.NotFound($"No existe el item con id {id}.");
    }
    db.Items.Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
});


//metodo GET para obtener el historial de movimientos de un item
app.MapGet("/api/items/{id}/movements", async (int id, AppDbContext db) => {
    var movements = await db.StockMovements
        .Where(m => m.ItemId == id)
        .OrderByDescending(m => m.Date)
        .ToListAsync();
    return Results.Ok(movements);
});

//metodo POST para registrar una entrada (+) o salida (-) de stock
app.MapPost("/api/movements", async ([FromBody] StockMovement movement, AppDbContext db) => {
    var item = await db.Items.FindAsync(movement.ItemId);
    if (item is null){
        return Results.BadRequest($"No existe el item con id {movement.ItemId}.");
    }
    if (movement.Quantity == 0){
        return Results.BadRequest("La cantidad no puede ser 0.");
    }
    if (item.Stock + movement.Quantity < 0){
        return Results.BadRequest($"Stock insuficiente: hay {item.Stock} unidades.");
    }
    item.Stock += movement.Quantity;
    movement.Date = DateTime.UtcNow;
    db.StockMovements.Add(movement);
    await db.SaveChangesAsync();
    return Results.Created($"/api/items/{item.Id}/movements", movement);
});

app.Run();