namespace FirebaseMauiApp;

public partial class App : Application
{
    public App()
    {
        this.Resources(new ResourceDictionary().MergedDictionaries(AppStyles.Default));
    }

    protected override Window CreateWindow(IActivationState? activationState) => new Window(new AppShell());
}
