using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace BlueMusicPlayer
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            var window = new MainMusicWindow();
            window.Activate();
        }
    }
}
