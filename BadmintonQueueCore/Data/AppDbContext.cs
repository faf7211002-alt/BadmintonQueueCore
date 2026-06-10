using Microsoft.EntityFrameworkCore;
using BadmintonQueueCore.Models;

namespace BadmintonQueueCore.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Player> Players { get; set; }
    }
}