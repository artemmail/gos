using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zakupki.Fetcher.Data;

public sealed class NoticeDbContextFactory : IDesignTimeDbContextFactory<NoticeDbContext>
{
    public NoticeDbContext CreateDbContext(string[] args)
    {
        
        try
        {
            var environment =  "Development";

            

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();


            

            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'Default' is not configured.");
            }

            

            var optionsBuilder = new DbContextOptionsBuilder<NoticeDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            

            var a=  new NoticeDbContext(optionsBuilder.Options);
           // throw new InvalidOperationException("-------------------");

            return a;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Ошибка при создании NoticeDbContext для design-time."+ ex.Message);
        }
    }
}
