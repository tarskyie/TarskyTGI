using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TarskyTGI
{
    public sealed partial class MainWindow : Window
    {
        private readonly string[] processNames = { "python", "WebView2" };
        private readonly Dictionary<string, Type> navigationMap = new()
            {
                { "ChatApp", typeof(ChatPage) },
                { "InstructApp", typeof(InstructPage) },
                { "BaseApp", typeof(BasePage) },
                { "HostApp", typeof(HostPage) },
                { "HomeApp", typeof(HomePage) },
                { "hfPage", typeof(hfPage) },
                { "dwnlds", typeof(downloadsPage) }
            };

        private SystemBackdropConfiguration backdropConfiguration;
        private MicaController micaController;

        public MainWindow()
        {
            //TrySetMicaBackdrop();
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            ContentFrame.Navigate(typeof(HomePage));
            
        }

        private async void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            await TerminateProcessesAsync();

            if (args.SelectedItemContainer != null && navigationMap.TryGetValue(args.SelectedItemContainer.Tag.ToString(), out Type pageType))
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private async Task TerminateProcessesAsync()
        {
            foreach (string processName in processNames)
            {
                Process[] processes = Process.GetProcessesByName(processName);

                foreach (Process process in processes)
                {
                    try
                    {
                        await Task.Run(() => process.Kill());
                        Console.WriteLine($"Terminated process: {process.ProcessName}, PID: {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to terminate process: {process.ProcessName}, PID: {process.Id}, Error: {ex.Message}");
                    }
                }
            }
        }
        private void TrySetMicaBackdrop()
        {
            if (MicaController.IsSupported())
            {
                backdropConfiguration = new SystemBackdropConfiguration();
                backdropConfiguration.IsInputActive = true;
                backdropConfiguration.Theme = SystemBackdropTheme.Default;

                micaController = new MicaController();
                micaController.AddSystemBackdropTarget(Window.As<ICompositionSupportsSystemBackdrop>());
                micaController.SetSystemBackdropConfiguration(backdropConfiguration);
            }
        }
    }
}
