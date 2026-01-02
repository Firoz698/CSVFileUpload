using Microsoft.EntityFrameworkCore;
using CSVUpload.Models;

namespace CSVUpload.DBContext;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
}
