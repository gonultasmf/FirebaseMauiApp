using Firebase.Database;
using Firebase.Database.Query;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace FirebaseMauiApp;

public partial class MainPage : ContentPage
{
    private readonly FirebaseClient _firebase;
    private Label chatMessages;
    private Entry myChatMessage;
    private Entry userNameEntry;
    private IDisposable _subscription;
    private readonly ObservableCollection<string> _messages;

    public MainPage()
    {
        _messages = new ObservableCollection<string>();
        
        // Firebase bağlantısı - kendi Firebase URL'nizi buraya yazın
        _firebase = new FirebaseClient("https://your-database.firebaseio.com/");
        
        Build();
        StartListeningToMessages();
    }

    private void StartListeningToMessages()
    {
        // Firebase'den mesajları dinle
        _subscription = _firebase
            .Child("chat")
            .Child("messages")
            .AsObservable<ChatMessage>()
            .Where(item => item.Object != null)
            .Subscribe(item =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var message = item.Object;
                    var displayMessage = $"[{message.Timestamp:HH:mm}] {message.UserName}: {message.Message}";
                    chatMessages.Text += $"{Environment.NewLine}{displayMessage}";
                });
            });
    }

    public void Build()
    {
        this
        .Content(
            new ScrollView()
            .Content(
                new VerticalStackLayout()
                .MinimumWidthRequest(350)
                .Spacing(10)
                .Padding(20)
                .Children(
                    new Label()
                    .Text("🔥 Firebase Chat")
                    .FontSize(24)
                    .FontAttributes(Bold)
                    .CenterHorizontal()
                    .TextColor(Colors.Orange),
                    
                    new Entry()
                    .Assign(out userNameEntry)
                    .Placeholder("Kullanıcı adınız")
                    .FontSize(16)
                    .CenterHorizontal(),
                    
                    new Frame()
                    .BackgroundColor(Colors.LightGray)
                    .HeightRequest(300)
                    .Content(
                        new ScrollView()
                        .Content(
                            new Label()
                            .Assign(out chatMessages)
                            .Text("Mesajlar burada görünecek...")
                            .FontSize(14)
                            .VerticalOptions(LayoutOptions.Start)
                        )
                    ),
                    
                    new Entry()
                    .Assign(out myChatMessage)
                    .Placeholder("Mesajınızı yazın...")
                    .FontSize(16)
                    .CenterHorizontal(),
                    
                    new Button()
                    .Text("📤 GÖNDER")
                    .FontSize(16)
                    .BackgroundColor(Colors.Orange)
                    .TextColor(Colors.White)
                    .CenterHorizontal()
                    .OnClicked(async (s, e) =>
                    {
                        await SendMessage();
                    })
                )
            )
        );
    }

    private async Task SendMessage()
    {
        try
        {
            var userName = userNameEntry.Text?.Trim();
            var message = myChatMessage.Text?.Trim();

            if (string.IsNullOrEmpty(userName))
            {
                await DisplayAlert("Hata", "Lütfen kullanıcı adınızı girin", "Tamam");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                await DisplayAlert("Hata", "Lütfen mesaj yazın", "Tamam");
                return;
            }

            var chatMessage = new ChatMessage
            {
                UserName = userName,
                Message = message,
                Timestamp = DateTime.Now
            };

            // Firebase'e mesajı gönder
            await _firebase
                .Child("chat")
                .Child("messages")
                .PostAsync(chatMessage);

            // Mesaj kutusunu temizle
            myChatMessage.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", $"Mesaj gönderilemedi: {ex.Message}", "Tamam");
            Console.WriteLine($"Send message error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _subscription?.Dispose();
    }
}

public class ChatMessage
{
    public string? UserName { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
}