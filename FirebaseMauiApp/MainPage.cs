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
    private Entry messageEntry;
    private readonly ObservableCollection<ChatMessage> _messages;
    private string _currentUser = "";
    private string _otherUser = "";
    private Label statusLabel;
    private Label typingLabel;
    private IDisposable _messageSubscription;
    private IDisposable _onlineSubscription;
    private IDisposable _typingSubscription;
    private System.Timers.Timer _typingTimer;
    private DateTime _lastTypingTime;
    private bool _isOtherUserOnline = false;

    // Kullanıcılar
    private const string USER_FS = "Fs";
    private const string USER_MG = "Mg";

    private System.Timers.Timer _onlineTimer;
    
    public MainPage()
    {
        _messages = new ObservableCollection<ChatMessage>();
        _firebase = new FirebaseClient("https://your-database.firebaseio.com/");
        
        Build();
        ShowUserSelection();
    }

    private async void ShowUserSelection()
    {
        var result = await DisplayActionSheet("Kullanıcı Seçin", null, null, USER_FS, USER_MG);
        if (result == USER_FS)
        {
            _currentUser = USER_FS;
            _otherUser = USER_MG;
        }
        else if (result == USER_MG)
        {
            _currentUser = USER_MG;
            _otherUser = USER_FS;
        }
        else
        {
            // Kullanıcı seçmezse tekrar sor
            ShowUserSelection();
            return;
        }

        // UI'ı güncelle
        UpdateUIAfterUserSelection();
        await InitializeChat();
    }

    private void UpdateUIAfterUserSelection()
    {
        // Header'ı güncelle
        OnPropertyChanged(nameof(_otherUser));
        OnPropertyChanged(nameof(_currentUser));
        
        // Status label'ı başlangıç durumuna getir
        if (statusLabel != null)
        {
            statusLabel.Text = "⚫ Bağlanıyor...";
            statusLabel.TextColor = Color.FromRgb(156, 163, 175);
        }
    }

    private async Task InitializeChat()
    {
        // Kullanıcıyı online yap
        await SetUserOnlineStatus(true);
        
        // Periyodik olarak online durumunu güncelle
        _onlineTimer = new System.Timers.Timer(5000); // 5 saniyede bir
        _onlineTimer.Elapsed += async (s, e) => await SetUserOnlineStatus(true);
        _onlineTimer.Start();
        
        // Mesajları dinlemeye başla
        StartListeningToMessages();
        
        // Online durumu dinle - basitleştirilmiş
        StartListeningToOnlineStatusSimple();
        
        // Yazıyor durumunu dinle - basitleştirilmiş
        StartListeningToTypingStatusSimple();
    }

    private async Task SetUserOnlineStatus(bool isOnline)
    {
        try
        {
            var userKey = _currentUser == "Fs" ? "user2" : "user1";
            
            await _firebase
                .Child("activeUsers")
                .Child(userKey)
                .PutAsync(new
                {
                    isOnline = isOnline,
                    lastSeen = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                    name = _currentUser
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Online status update error: {ex.Message}");
        }
    }

    private void StartListeningToOnlineStatusSimple()
    {
        var otherUserKey = _otherUser == "Fs" ? "user2" : "user1";
        
        _onlineSubscription = _firebase
            .Child("activeUsers")
            .Child(otherUserKey)
            .Child("isOnline")
            .AsObservable<bool>()
            .Subscribe(isOnline =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isOtherUserOnline = isOnline.Object;
                    UpdateStatusLabel();
                });
            });
    }
    
    private void StartListeningToTypingStatusSimple()
    {
        _typingSubscription = _firebase
            .Child("typing")
            .Child($"{_otherUser}To{_currentUser}")
            .Child("isTyping")
            .AsObservable<bool>()
            .Subscribe(isTyping =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (typingLabel != null)
                    {
                        typingLabel.IsVisible = isTyping.Object;
                        if (statusLabel != null)
                        {
                            statusLabel.IsVisible = !isTyping.Object;
                        }
                    }
                });
            });
    }

    private void UpdateStatusLabel()
    {
        if (statusLabel == null) return;
        
        if (_isOtherUserOnline)
        {
            statusLabel.Text = $"🟢 çevrimiçi";
            statusLabel.TextColor = Color.FromRgb(34, 197, 94);
        }
        else
        {
            statusLabel.Text = $"⚫ çevrimdışı";
            statusLabel.TextColor = Color.FromRgb(156, 163, 175);
        }
    }

    private void StartListeningToMessages()
    {
        _messageSubscription = _firebase
            .Child("messages")
            .AsObservable<ChatMessage>()
            .Where(item => item.Object != null)
            .Where(item => 
                (item.Object.UserName == _currentUser || item.Object.UserName == _otherUser))
            .Subscribe(item =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var message = item.Object;
                    message.IsFromCurrentUser = message.UserName == _currentUser;
                    
                    // TimeString yoksa ekle
                    if (string.IsNullOrEmpty(message.TimeString))
                    {
                        message.TimeString = message.Timestamp.ToString("HH:mm");
                    }
                    
                    // Duplicate kontrolü
                    if (!_messages.Any(m => m.UserName == message.UserName && 
                                          m.Message == message.Message && 
                                          Math.Abs((m.Timestamp - message.Timestamp).TotalSeconds) < 2))
                    {
                        _messages.Add(message);
                        
                        // Sıralama
                        var sortedMessages = _messages.OrderBy(m => m.Timestamp).ToList();
                        _messages.Clear();
                        foreach (var msg in sortedMessages)
                        {
                            _messages.Add(msg);
                        }

                        // En alta scroll
                        if (_messages.Count > 0 && messagesCollectionView != null)
                        {
                            Task.Run(async () =>
                            {
                                await Task.Delay(100);
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    try
                                    {
                                        messagesCollectionView.ScrollTo(_messages.Last(), position: ScrollToPosition.End, animate: true);
                                    }
                                    catch { }
                                });
                            });
                        }
                    }
                });
            });
    }

    private async Task SendMessage()
    {
        try
        {
            var message = messageEntry?.Text?.Trim();
            if (string.IsNullOrEmpty(message))
                return;

            var chatMessage = new ChatMessage
            {
                UserName = _currentUser,
                Message = message,
                Timestamp = DateTime.Now,
                IsFromCurrentUser = true,
                TimeString = DateTime.Now.ToString("HH:mm"),
                UserNameColor = "#007800",
                MessageTextColor = "#FFFFFF",
                TimeTextColor = "#C8DCFF"
            };

            await _firebase
                .Child("messages")
                .PostAsync(chatMessage);

            messageEntry.Text = string.Empty;
            
            // Yazıyor durumunu kaldır
            await SetTypingStatus(false);
            
            try
            {
                HapticFeedback.Perform(HapticFeedbackType.Click);
            }
            catch { }
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Hata", "Mesaj gönderilemedi", "Tamam");
        }
    }

    private async Task SetTypingStatus(bool isTyping)
    {
        try
        {
            await _firebase
                .Child("typing")
                .Child($"{_currentUser}To{_otherUser}")
                .PutAsync(new 
                { 
                    isTyping = isTyping, 
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                    user = _currentUser
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Set typing status error: {ex.Message}");
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _lastTypingTime = DateTime.Now;
        
        if (!string.IsNullOrEmpty(e.NewTextValue) && e.NewTextValue != e.OldTextValue)
        {
            // Yazıyor bildirimi gönder
            Task.Run(async () => 
            {
                try
                {
                    await _firebase
                        .Child("typing")
                        .Child($"{_currentUser}To{_otherUser}")
                        .PutAsync(new 
                        { 
                            isTyping = true, 
                            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                            user = _currentUser
                        });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Typing status error: {ex.Message}");
                }
            });
            
            // Timer'ı yeniden başlat
            _typingTimer?.Stop();
            _typingTimer = new System.Timers.Timer(2000);
            _typingTimer.Elapsed += async (s, args) =>
            {
                if ((DateTime.Now - _lastTypingTime).TotalSeconds >= 2)
                {
                    await SetTypingStatus(false);
                    _typingTimer?.Stop();
                }
            };
            _typingTimer.Start();
        }
        else if (string.IsNullOrEmpty(e.NewTextValue))
        {
            Task.Run(async () => await SetTypingStatus(false));
        }
    }

    public void Build()
    {
        this
        .ShellNavBarIsVisible(false)
        .BackgroundColor(Color.FromRgb(240, 242, 245))
        .Content(
            new Grid()
            .RowDefinitions(e => e.Auto().Star().Auto())
            .Children(
                // Header
                new Frame()
                .Row(0)
                .BackgroundColor(Colors.White)
                .CornerRadius(0)
                .Padding(0)
                .HasShadow(true)
                .Content(
                    new Grid()
                    .ColumnDefinitions(e => e.Auto().Star().Auto())
                    .Padding(15, 50, 15, 15)
                    .Children(
                        // Profil resmi
                        new Frame()
                        .Column(0)
                        .BackgroundColor(Color.FromRgb(79, 70, 229))
                        .CornerRadius(25)
                        .Padding(0)
                        .WidthRequest(50)
                        .HeightRequest(50)
                        .Content(
                            new Label()
                            .BindingContext(this)
                            .Text(e => e.Path("_otherUser").Convert((string x) => 
                                !string.IsNullOrEmpty(x) ? x.Substring(0, 1).ToUpper() : "?"))
                            .FontSize(24)
                            .FontAttributes(FontAttributes.Bold)
                            .TextColor(Colors.White)
                            .HorizontalOptions(LayoutOptions.Center)
                            .VerticalOptions(LayoutOptions.Center)
                        ),

                        // Kullanıcı bilgileri
                        new StackLayout()
                        .Column(1)
                        .Spacing(2)
                        .Margin(15, 0, 0, 0)
                        .VerticalOptions(LayoutOptions.Center)
                        .Children(
                            new Label()
                            .BindingContext(this)
                            .Text(e => e.Path("_otherUser").Convert((string x) => 
                                !string.IsNullOrEmpty(x) ? x : "..."))
                            .FontSize(18)
                            .FontAttributes(FontAttributes.Bold)
                            .TextColor(Color.FromRgb(17, 24, 39)),

                            new Label()
                            .Assign(out statusLabel)
                            .Text("⚫ Bağlanıyor...")
                            .FontSize(13)
                            .TextColor(Color.FromRgb(156, 163, 175)),

                            new Label()
                            .Assign(out typingLabel)
                            .Text("yazıyor...")
                            .FontSize(12)
                            .FontAttributes(FontAttributes.Italic)
                            .TextColor(Color.FromRgb(79, 70, 229))
                            .IsVisible(false)
                        ),

                        // Menü butonu
                        new Button()
                        .Column(2)
                        .Text("⋮")
                        .FontSize(24)
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(Color.FromRgb(107, 114, 128))
                        .WidthRequest(40)
                        .HeightRequest(40)
                    )
                ),

                // Mesajlar alanı
                new CollectionView()
                .Row(1)
                .Assign(out messagesCollectionView)
                .ItemsSource(_messages)
                .BackgroundColor(Colors.Transparent)
                .Margin(10)
                .ItemTemplate(() =>
                    new Grid()
                    .Padding(5, 3)
                    .Children(
                        new Frame()
                        .CornerRadius(18)
                        .Padding(12, 8)
                        .HasShadow(false)
                        .MaximumWidthRequest(280)
                        .Content(
                            new StackLayout()
                            .Spacing(2)
                            .Children(
                                new Label()
                                .Text(e => e.Path("Message"))
                                .FontSize(15)
                                .LineBreakMode(LineBreakMode.WordWrap)
                                .TextColor(e => e.Path("IsFromCurrentUser").Convert((bool isFrom) => 
                                    isFrom ? Colors.White : Color.FromRgb(17, 24, 39))),

                                new Label()
                                .Text(e => e.Path("TimeString"))
                                .FontSize(11)
                                .HorizontalOptions(LayoutOptions.End)
                                .Opacity(0.7)
                                .TextColor(e => e.Path("IsFromCurrentUser").Convert((bool isFrom) => 
                                    isFrom ? Color.FromRgb(200, 220, 255) : Color.FromRgb(107, 114, 128)))
                            )
                        )
                        .BackgroundColor(e => e.Path("IsFromCurrentUser").Convert((bool isFrom) => 
                            isFrom ? Color.FromRgb(79, 70, 229) : Colors.White))
                        .HorizontalOptions(e => e.Path("IsFromCurrentUser").Convert((bool isFrom) => 
                            isFrom ? LayoutOptions.End : LayoutOptions.Start))
                    )
                ),

                // Mesaj gönderme alanı
                new Frame()
                .Row(2)
                .BackgroundColor(Colors.White)
                .CornerRadius(0)
                .Padding(10, 10, 10, 20)
                .HasShadow(true)
                .Content(
                    new Grid()
                    .ColumnDefinitions(e => e.Auto().Star().Auto())
                    .Spacing(10)
                    .Children(
                        // Ek butonları
                        new Button()
                        .Column(0)
                        .Text("📎")
                        .FontSize(20)
                        .BackgroundColor(Colors.Transparent)
                        .WidthRequest(44)
                        .HeightRequest(44),

                        // Mesaj giriş alanı
                        new Frame()
                        .Column(1)
                        .BackgroundColor(Color.FromRgb(249, 250, 251))
                        .CornerRadius(22)
                        .Padding(15, 0)
                        .BorderColor(Color.FromRgb(229, 231, 235))
                        .HasShadow(false)
                        .Content(
                            new Entry()
                            .Assign(out messageEntry)
                            .Placeholder("Mesaj yazın...")
                            .FontSize(15)
                            .BackgroundColor(Colors.Transparent)
                            .VerticalOptions(LayoutOptions.Center)
                            .OnTextChanged(OnTextChanged)
                            .OnCompleted(async (s, e) => await SendMessage())
                        ),

                        // Gönder butonu
                        new Frame()
                        .Column(2)
                        .BackgroundColor(Color.FromRgb(79, 70, 229))
                        .CornerRadius(22)
                        .Padding(0)
                        .WidthRequest(44)
                        .HeightRequest(44)
                        .HasShadow(false)
                        .Content(
                            new Button()
                            .Text("➤")
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Task.Run(async () => await SetUserOnlineStatus(false));
        _messageSubscription?.Dispose();
        _onlineSubscription?.Dispose();
        _typingSubscription?.Dispose();
        _typingTimer?.Dispose();
        _onlineTimer?.Dispose();
    }
}

public class ChatMessage
{
    public string UserName { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsFromCurrentUser { get; set; }
    public bool IsSystemMessage { get; set; }
    public string TimeString { get; set; }
    public string UserNameColor { get; set; }
    public string MessageTextColor { get; set; }
    public string TimeTextColor { get; set; }
}

public class UserStatus
{
    public bool isOnline { get; set; }
    public string lastSeen { get; set; }
    public string name { get; set; }
}

public class TypingStatus
{
    public bool isTyping { get; set; }
    public string timestamp { get; set; }
    public string user { get; set; }
}