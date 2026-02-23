using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Catalog.Infrastructure.Persistence
{
    public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
    {
        public CatalogDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=lms_catalog_db;Username=lms_user;Password=lms_password_2024"
            );

            // IMediator parametresi null geçiliyor, sadece migration için!
            return new CatalogDbContext(optionsBuilder.Options, null!);
        }
    }
}