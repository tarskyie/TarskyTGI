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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using TarskyTGI.Pages;

namespace TarskyTGI
{
    public sealed partial class MainWindow : Window
    {
        private readonly string[] processNames = { "python", "WebView2" };
        private readonly Dictionary<string, Type> navigationMap = new()
            {
                { "ChatApp", typeof(ChatPage) },
                { "LlavaApp", typeof(LlavaPage) },
                { "InstructApp", typeof(InstructPage) },
                { "BaseApp", typeof(BasePage) },
                { "HostApp", typeof(HostPage) },
                { "HomeApp", typeof(HomePage) }
            };

        private SystemBackdropConfiguration backdropConfiguration;
        private MicaController micaController;

        public MainWindow()
        {
            //TrySetMicaBackdrop();
            this.InitializeComponent();
            this.Activated += MainWindow_Activated;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            ContentFrame.Navigate(typeof(HomePage));
        }

        // ...

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId); 
            appWindow.SetIcon(@"Assets\icon.ico");
        }

        private async void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            
            if (args.SelectedItemContainer != null && navigationMap.TryGetValue(args.SelectedItemContainer.Tag.ToString(), out Type pageType))
            {
                await TerminateProcessesAsync();
                ContentFrame.IsEnabled = false;
                ContentFrame.IsEnabled = true;
                ContentFrame.Navigate(pageType);
            }
        }

        private async Task TerminateProcessesAsync()
        {
            foreach (string processName in processNames)
            {
                Process[] processes = Process.GetProcesses();

                foreach (Process process in processes.Where(p => p.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase)))
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
    }
}
