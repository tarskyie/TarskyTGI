using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TarskyTGI
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
        }

        private void NavigateToChatPage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ChatPage));
        }

        private void NavigateToInstructPage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(InstructPage));
        }
    }
}
