using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Snowflakes;
using Snowflakes.Components;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Data;

public class DataContext(DbContextOptions<DataContext> options) : IdentityDbContext<SuperCoolUser, IdentityRole<long>, long>(options)
{
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={Config.values.databaseLocation}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // modelBuilder.Entity<SuperCoolUser>().ToTable("Users").HasKey(u => u.Id);
        base.OnModelCreating(modelBuilder);
    }
}