using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChessTeacher.App.ViewModels;
using ChessTeacher.App.Views;
using ChessTeacher.Data;
using ChessTeacher.Engine;
using ChessTeacher.Teaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChessTeacher.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<AppPaths>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IGameRepository, GameRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IStockfishService, StockfishService>();
        services.AddSingleton<MoveClassifier>();
        services.AddSingleton<IExplanationService, RuleBasedExplanationService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        try
        {
            await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database initialization failed: {ex}");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
            desktop.Exit += async (_, _) =>
            {
                if (_services.GetService<IStockfishService>() is { } engine)
                    await engine.DisposeAsync();
                await _services.DisposeAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
