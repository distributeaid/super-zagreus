using DA.NA.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DA.NA.Core.Data;

/// <summary>
/// Fixed GUIDs for seed units so they are stable across database rebuilds.
/// Use these IDs when constructing test requests against the API.
/// </summary>
public static class UnitIds
{
    public static readonly Guid Item   = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Box    = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Pallet = new("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Kg     = new("44444444-4444-4444-4444-444444444444");
    public static readonly Guid Lb     = new("55555555-5555-5555-5555-555555555555");
    public static readonly Guid Litre  = new("66666666-6666-6666-6666-666666666666");
    public static readonly Guid Gallon = new("77777777-7777-7777-7777-777777777777");
}

public static class SeedData
{
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger  = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        logger.LogInformation("Running database migrations...");
        await context.Database.MigrateAsync();

        if (await context.Units.AnyAsync())
        {
            logger.LogInformation("Database already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding reference data...");
        await SeedUnitsAsync(context);
        await SeedItemTypesAsync(context);
        await SeedDaAdminAsync(context);
        await context.SaveChangesAsync();
        logger.LogInformation("Seed complete.");
    }

    private static async Task SeedUnitsAsync(AppDbContext context)
    {
        var units = new List<Unit>
        {
            new() { Id = UnitIds.Item,   Name = "item",   Dimension = UnitDimension.Count,  ToBaseFactor = 1m },
            new() { Id = UnitIds.Box,    Name = "box",    Dimension = UnitDimension.Count,  ToBaseFactor = 1m },
            new() { Id = UnitIds.Pallet, Name = "pallet", Dimension = UnitDimension.Count,  ToBaseFactor = 1m },
            new() { Id = UnitIds.Kg,     Name = "kg",     Dimension = UnitDimension.Weight, ToBaseFactor = 1000m },
            new() { Id = UnitIds.Lb,     Name = "lb",     Dimension = UnitDimension.Weight, ToBaseFactor = 453.592m },
            new() { Id = UnitIds.Litre,  Name = "litre",  Dimension = UnitDimension.Volume, ToBaseFactor = 1000m },
            new() { Id = UnitIds.Gallon, Name = "gallon", Dimension = UnitDimension.Volume, ToBaseFactor = 3785.41m },
        };
        await context.Units.AddRangeAsync(units);
    }

    private static async Task SeedItemTypesAsync(AppDbContext context)
    {
        // Item types are sourced from Strapi in production.
        // Here we seed a representative set for the prototype.
        var items = new List<ItemType>
        {
            // Food
            new() { Id = Guid.NewGuid(), Category = "Food", Name = "Halal meals",       DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Food", Name = "Vegetarian meals",  DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Food", Name = "Baby formula",      DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Food", Name = "Rice",              DefaultUnitId = UnitIds.Kg   },
            new() { Id = Guid.NewGuid(), Category = "Food", Name = "Cooking oil",       DefaultUnitId = UnitIds.Litre },
            new() { Id = Guid.NewGuid(), Category = "Food", Name = "Canned goods",      DefaultUnitId = UnitIds.Item },

            // Hygiene
            new() { Id = Guid.NewGuid(), Category = "Hygiene", Name = "Soap bars",         DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Hygiene", Name = "Shampoo",           DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Hygiene", Name = "Sanitary pads",     DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Hygiene", Name = "Nappies / diapers", DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Hygiene", Name = "Toothbrushes",      DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Hygiene", Name = "Toothpaste",        DefaultUnitId = UnitIds.Item },

            // Clothing
            new() { Id = Guid.NewGuid(), Category = "Clothing", Name = "Men's jackets",            DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Clothing", Name = "Women's jackets",          DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Clothing", Name = "Children's clothing sets", DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Clothing", Name = "Socks",                    DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Clothing", Name = "Shoes",                    DefaultUnitId = UnitIds.Item },

            // Shelter
            new() { Id = Guid.NewGuid(), Category = "Shelter", Name = "Sleeping bags", DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Shelter", Name = "Tents",         DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Shelter", Name = "Blankets",      DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Shelter", Name = "Tarpaulins",    DefaultUnitId = UnitIds.Item },

            // Household
            new() { Id = Guid.NewGuid(), Category = "Household", Name = "Cleaning supplies", DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Household", Name = "Towels",            DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Household", Name = "Bedding sets",      DefaultUnitId = UnitIds.Item },

            // Infants & Children
            new() { Id = Guid.NewGuid(), Category = "Infants & Children", Name = "Baby wipes",        DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Infants & Children", Name = "Nappies (infant)",   DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Infants & Children", Name = "Children's books",   DefaultUnitId = UnitIds.Item },

            // Infrastructure
            new() { Id = Guid.NewGuid(), Category = "Infrastructure", Name = "Generators",             DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Infrastructure", Name = "First aid kits",         DefaultUnitId = UnitIds.Item },
            new() { Id = Guid.NewGuid(), Category = "Infrastructure", Name = "Portable water filters",  DefaultUnitId = UnitIds.Item },
        };
        await context.ItemTypes.AddRangeAsync(items);
    }

    private static Task SeedDaAdminAsync(AppDbContext context)
    {
        // Default DA admin for first run. Change the password after first login.
        // Username: admin  Password: ChangeMe123!
        var admin = new User
        {
            Id = new Guid("00000000-0000-0000-0000-000000000001"),
            Username = "admin",
            FirstName = "DA",
            LastName = "Admin",
            Email = "admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
            Role = UserRole.DaAdmin,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(admin);
        return Task.CompletedTask;
    }
}
