using Microsoft.EntityFrameworkCore;

// Tabla 1: categorías de productos
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Tabla 2: items (productos), cada uno pertenece a una categoría
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CategoryId { get; set; }
}

// Tabla 3: movimientos de stock (entradas positivas, salidas negativas)
public class StockMovement
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

// Contexto de Base de Datos
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
}