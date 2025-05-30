using BlueMusicPlayer.Models;
using BlueMusicPlayer.ViewModels;
using BlueMusicPlayer.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using Windows.Media.Playback;
using Windows.Storage;
using WinRT.Interop;

namespace BlueMusicPlayer
{
    public sealed partial class MainMusicWindow : Window
    {
        private readonly MusicPlayerViewModel _vm;
        private readonly DispatcherQueue _uiQueue;
        private bool _isUserDragging;

        public MainMusicWindow()
        {
            InitializeComponent();

            // Initialize UI queue
            _uiQueue = DispatcherQueue.GetForCurrentThread();

            // MediaPlayer initialization
            var mp = new MediaPlayer();
            PlayerElement.SetMediaPlayer(mp);
            mp.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
            mp.MediaEnded += MediaPlayer_MediaEnded;

            // ViewModel initialization
            var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "tracks.db");
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            _vm = new MusicPlayerViewModel(
                new MusicRepository(dbPath),
                PlayerElement,
                hwnd,
                this.Content.XamlRoot
            );
            LayoutRoot.DataContext = _vm;

            // Set default navigation to Local Music
            this.DispatcherQueue.TryEnqueue(() =>
            {
                _vm.SelectedNavItem = "local"; // Set the ViewModel property instead of navigating
                foreach (var item in NavView.MenuItems)
                {
                    if (item is NavigationViewItem nvi && (string)nvi.Tag == "local")
                    {
                        NavView.SelectedItem = nvi;
                        break;
                    }
                }
            });

            // Handle ViewModel property changes
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_vm.LocalSelectedTrack) && _vm.LocalSelectedTrack == null)
                {
                    _uiQueue.TryEnqueue(() =>
                    {
                        ProgressSlider.Value = 0;
                        PositionTextBlock.Text = "00:00";
                        DurationTextBlock.Text = "00:00";
                    });
                }
            };
        }

        private void ToggleViewButton_Checked(object sender, RoutedEventArgs e)
        {
            if (LocalMusicView?.TracksRepeater != null)
            {
                var gridLayout = new UniformGridLayout
                {
                    MinRowSpacing = 12,
                    MinColumnSpacing = 12,
                    Orientation = Orientation.Horizontal
                };
                LocalMusicView.TracksRepeater.Layout = gridLayout;
                ViewIcon.Glyph = "\uE8EF";
            }
        }

        private void ToggleViewButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (LocalMusicView?.TracksRepeater != null)
            {
                LocalMusicView.TracksRepeater.Layout = new StackLayout { Spacing = 12 };
                ViewIcon.Glyph = "\uECA5";
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                // Handle settings if needed
                return;
            }

            if (args.SelectedItemContainer is NavigationViewItem nvi && nvi.Tag is string tag)
            {
                // Update the ViewModel's SelectedNavItem property to control view visibility
                _vm.SelectedNavItem = tag;
            }
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
            => _vm.NextCommand.Execute(null);

        private void PlaybackSession_PositionChanged(MediaPlaybackSession session, object args)
        {
            if (_isUserDragging) return;
            _uiQueue.TryEnqueue(() =>
            {
                PositionTextBlock.Text = session.Position.ToString(@"mm\:ss");
                DurationTextBlock.Text = session.NaturalDuration.ToString(@"mm\:ss");
                ProgressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                ProgressSlider.Value = session.Position.TotalSeconds;
            });
        }

        private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
            => _isUserDragging = true;

        private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUserDragging)
                PositionTextBlock.Text = TimeSpan.FromSeconds(e.NewValue).ToString(@"mm\:ss");
        }

        private void ProgressSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isUserDragging = false;
            _vm.MediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
        }

        // This method handles track selection from the LocalMusicView
        // You might need to wire this up in your LocalMusicView or handle it through the ViewModel
        private async void PlaylistListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Track track)
            {
                await _vm.SelectAndPlayAsync(track);
                _uiQueue.TryEnqueue(() =>
                {
                    ProgressSlider.Value = 0;
                    PositionTextBlock.Text = "00:00";
                    DurationTextBlock.Text = _vm.MediaPlayer.PlaybackSession.NaturalDuration.ToString(@"mm\:ss");
                });
            }
        }
    }
}