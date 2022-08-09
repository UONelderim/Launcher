using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Models;
using Nelderim.Utility;
using static MessageBox.Avalonia.Enums.Icon;
using static MessageBox.Avalonia.MessageBoxManager;

namespace NelderimLauncher.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (ShouldSelfUpdate())
            {
                Dispatcher.UIThread.Post(() => ShowUpdateDialog(), DispatcherPriority.Background);
            }
        }
        
        public static bool ShouldSelfUpdate()
        {
            var patch = Utils.FetchPatch();
        
            using (FileStream stream = File.OpenRead(Utils.AppName()))
            {
                return Crypto.Sha1Hash(stream) != patch.Sha1;
            }
        }

        private async void ShowUpdateDialog()
        {
            var buttonResult = await GetMessageBoxCustomWindow(new MessageBoxCustomParams
            {
                ContentTitle = "Aktualizacja dostępna",
                ContentHeader = "Nowa wersja Nelderim Launcher jest dostępna ",
                ContentMessage = "Czy chcesz zaktualizować?",
                WindowIcon = new WindowIcon(Utils.GetAsset("nelderim.ico")),
                Icon = Question,
                ButtonDefinitions = new[]
                {
                    new ButtonDefinition { Name = "Tak", IsDefault = true },
                    new ButtonDefinition { Name = "Nie", IsCancel = true }
                },
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                MinWidth = 250
            }).ShowDialog(this);
            if ("Tak".Equals(buttonResult))
            {
                var nelderimApp = Utils.AppName();
                var appCopyName = $"_{nelderimApp}";
                File.Copy(nelderimApp, appCopyName, true);
            
                var process = new Process();
                process.StartInfo.FileName = appCopyName;
                process.StartInfo.Arguments = $"update {nelderimApp}";
                process.Start();
                Close();
            }
        }
    }
}