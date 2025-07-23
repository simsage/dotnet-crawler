namespace Crawlers;

using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic; 

public class CachedAssetDbContext(string databasePath) : DbContext
{
    public DbSet<CachedAsset> CachedAsset { get; set; } // Represents the 'CachedAsset' table

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Configure the DbContext to use SQLite and specify the database file path
        optionsBuilder.UseSqlite($"Data Source={databasePath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ensure the Key is the primary key and indexed for fast lookups
        modelBuilder.Entity<CachedAsset>()
            .HasKey(c => c.Key);
        modelBuilder.Entity<CachedAsset>()
            .HasIndex(c => c.ExpiresAt); // Index for efficient cleanup
    }
}
