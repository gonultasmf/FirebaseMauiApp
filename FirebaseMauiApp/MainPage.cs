using Firebase.Database;
using Firebase.Database.Query;
using FmgLib.MauiMarkup;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace FirebaseMauiApp;

public partial class MainPage : ContentPage
{
    private readonly FirebaseClient _firebase;
    private CollectionView messagesCollectionView;
    private Entry myChatMessage;
    private Entry userNameEntry;
    private IDisposable _subscription;
    private readonly ObservableCollection<ChatMessage> _messages;
    private string _currentUser = "";

    public MainPage()
    {
        _messages = new ObservableCollection<ChatMessage>();

        // Firebase bağlantısı - kendi Firebase URL'nizi buraya yazın
        _firebase = new FirebaseClient("https://message-maui-default-rtdb.firebaseio.com/");

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
                    message.IsFromCurrentUser = message.UserName == _currentUser;
                    _messages.Add(message);

                    // En alta scroll et
                    if (_messages.Count > 0)
                    {
                        messagesCollectionView.ScrollTo(_messages.Last(), position: ScrollToPosition.End, animate: true);
                    }
                });
            });
    }

    public void Build()
    {
        this
        .ShellNavBarIsVisible(false)
        .BackgroundColor(Color.FromRgb(240, 242, 247)) // WhatsApp benzeri açık gri arka plan
        .Content(
            new Grid()
            .RowDefinitions(e => e.Auto().Star().Auto())
            .Children(
                // Header
                new Frame()
                .Row(0)
                .BackgroundColor(Color.FromRgb(37, 211, 102)) // WhatsApp yeşili
                .CornerRadius(0)
                .Padding(20, 40, 20, 15)
                .Content(
                    new StackLayout()
                    .Orientation(StackOrientation.Horizontal)
                    .Children(
                        new Label()
                        .Text("💬")
                        .FontSize(28)
                        .VerticalOptions(LayoutOptions.Center),

                        new Label()
                        .Text("Firebase Chat")
                        .FontSize(22)
                        .FontAttributes(FontAttributes.Bold)
                        .TextColor(Colors.White)
                        .VerticalOptions(LayoutOptions.Center)
                        .Margin(10, 0, 0, 0),

                        new Label()
                        .Text("🔥")
                        .FontSize(20)
                        .VerticalOptions(LayoutOptions.Center)
                        .HorizontalOptions(LayoutOptions.EndAndExpand)
                    )
                ),

                // Mesajlar Alanı
                new CollectionView()
                .Row(1)
                .Assign(out messagesCollectionView)
                .ItemsSource(_messages)
                .Margin(10, 10, 10, 0)
                .ItemTemplate(() =>
                    new Grid()
                        .ColumnDefinitions(e => e.Star(count: 2))
                        .Margin(0, 5)
                        .Children(
                            new Frame()
                            .CornerRadius(15)
                            .Padding(15, 10)
                            .HasShadow(true)
                            .Content(
                                new StackLayout()
                                .Children(
                                    new Label()
                                    .Text(e => e.Getter(static (ChatMessage m) => m.UserName))
                                    .FontSize(12)
                                    .FontAttributes(FontAttributes.Bold)
                                    .TextColor(e => e.Getter(static (ChatMessage m) => m.UserNameColor)),

                                    new Label()
                                    .Text(e => e.Getter(static (ChatMessage m) => m.Message))
                                    .FontSize(16)
                                    .TextColor(Colors.Black)
                                    .Margin(0, 2, 0, 5),

                                    new Label()
                                    .Text(e => e.Getter(static (ChatMessage m) => m.TimeString))
                                    .FontSize(10)
                                    .TextColor(Colors.Gray)
                                    .HorizontalOptions(LayoutOptions.End)
                                )
                            )
                            .Triggers(
                                new DataTrigger(typeof(Frame))
                                .Binding(e => e.Path("IsFromCurrentUser"))
                                .Value(true)
                                .Setters(
                                    new Setters<Frame>(e => e.Column(1)
                                        .BackgroundColor(Color.FromRgb(220, 248, 198)) // Açık yeşil (kendi mesajımız)
                                        .HorizontalOptions(LayoutOptions.End)
                                    )
                                ),

                                new DataTrigger(typeof(Frame))
                                .Binding(new Binding("IsFromCurrentUser"))
                                .Value(false)
                                .Setters(
                                    new Setters<Frame>(e => e.Column(0)
                                    .BackgroundColor(Colors.White)
                                    .HorizontalOptions(LayoutOptions.Start)
                                )
                            )
                        )
                )),

                // Alt kısım - Mesaj gönderme
                new Frame()
                .Row(2)
                .BackgroundColor(Colors.White)
                .CornerRadius(0)
                .Padding(15)
                .HasShadow(true)
                .Content(
                    new Grid()
                    .RowDefinitions(e => e.Auto().Auto())
                    .ColumnDefinitions(e => e.Star().Auto())
                    .Children(
                        // Kullanıcı adı girişi (ilk satır)
                        new Entry()
                        .Row(0)
                        .ColumnSpan(2)
                        .Assign(out userNameEntry)
                        .Placeholder("👤 Kullanıcı adınız")
                        .FontSize(14)
                        .BackgroundColor(Color.FromRgb(245, 245, 245))
                        .Margin(0, 0, 0, 10),

                        // Mesaj girişi
                        new Frame()
                        .Row(1)
                        .Column(0)
                        .BackgroundColor(Color.FromRgb(245, 245, 245))
                        .CornerRadius(25)
                        .Padding(15, 0)
                        .Margin(0, 0, 10, 0)
                        .Content(
                            new Entry()
                            .Assign(out myChatMessage)
                            .Placeholder("💭 Mesajınızı yazın...")
                            .FontSize(16)
                            .BackgroundColor(Colors.Transparent)
                        ),

                        // Gönder butonu
                        new Frame()
                        .Row(1)
                        .Column(1)
                        .BackgroundColor(Color.FromRgb(37, 211, 102))
                        .CornerRadius(25)
                        .Padding(0)
                        .WidthRequest(50)
                        .HeightRequest(50)
                        .Content(
                            new Button()
                            .Text("📤")
                            .FontSize(18)
                            .BackgroundColor(Colors.Transparent)
                            .TextColor(Colors.White)
                            .OnClicked(async (s, e) => await SendMessage())
                        )
                    )
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
                await DisplayAlert("⚠️ Hata", "Lütfen kullanıcı adınızı girin", "Tamam");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                await DisplayAlert("⚠️ Hata", "Lütfen mesaj yazın", "Tamam");
                return;
            }

            _currentUser = userName;

            var chatMessage = new ChatMessage
            {
                UserName = userName,
                Message = message,
                Timestamp = DateTime.Now,
                IsFromCurrentUser = true
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
            await DisplayAlert("❌ Hata", $"Mesaj gönderilemedi: {ex.Message}", "Tamam");
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
    public string UserName { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsFromCurrentUser { get; set; }

    public string TimeString => Timestamp.ToString("HH:mm");

    public Color UserNameColor => IsFromCurrentUser
        ? Color.FromRgb(0, 120, 0) // Koyu yeşil (kendi mesajımız)
        : Color.FromRgb(25, 118, 210); // Mavi (diğer mesajlar)
}