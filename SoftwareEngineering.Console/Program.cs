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
