
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HealthHelper.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HealthHelperDbContext>
{
    public HealthHelperDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HealthHelperDbContext>();
        // Use a dummy database name for design-time purposes.
        optionsBuilder.UseSqlite("Data Source=healthhelper_design.db3");

        return new HealthHelperDbContext(optionsBuilder.Options);
    }
}
