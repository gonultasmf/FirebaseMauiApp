﻿namespace FirebaseMauiApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        this
        .FlyoutBehavior(FlyoutBehavior.Disabled)
        .Items(
            new ShellContent()
            .Title("Home")
            .ContentTemplate(() => new MainPage())
            .Route(nameof(MainPage))
        );
    }
}
