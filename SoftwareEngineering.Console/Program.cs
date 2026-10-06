// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
int sdf = 2354;
if (sdf == 2354) {
    Log.Information("Змінна sdf дорівнює 2354");
} else {
    Log.Warning("Змінна sdf не дорівнює 2354");
}

if (sdf == 2354) {
    Log.Information("Змінна sdf дорівнює 2354");
    Log.Information("sheet");
} else {
    Log.Warning("Змінна sdf не дорівнює 2354");
}

try
{
    Log.Information("Програму успішно запущено! Перевірка логування в Seq.");

    Console.WriteLine("Логер працює. Натисніть будь-яку клавішу для виходу...");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Програма впала з помилкою");
}
finally
{
    Log.CloseAndFlush();
}
