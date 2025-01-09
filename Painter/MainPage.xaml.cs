namespace Painter
{
    public partial class MainPage : ContentPage
    {
        private const string ClientId = "";
        private const string RedirectUri = "";
        private const string OAuthUrl = "";
        public MainPage()
        {
            InitializeComponent();
        }
        private async void OnNewProjectClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new PdfColoringPage());
        }
        private async void OnDiscordLoginClicked(object sender, EventArgs e)
        {

        }
    }

}
