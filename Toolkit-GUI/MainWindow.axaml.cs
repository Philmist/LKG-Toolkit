using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Xml.Linq;
using Toolkit_API.Device;
using ToolkitGUI.Media;
using System.Diagnostics;
using Avalonia.Threading;
using System.Reactive.Joins;

namespace ToolkitGUI
{
    public partial class MainWindow : Window
    {
        public static MainWindow instance;

        public Playlist PlayingPlaylist;
        public bool Syncing = false;

        PlaylistManager playlistManager;
        
        public Toolkit_API.Bridge.BridgeConnectionHTTP bridgeConnection;
        public volatile bool connectionStatus = false;

        public MainWindow()
        {
            instance = this;
            InitializeComponent();
            InitializePlaylistManager();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            ConnectionStatus = this.FindControl<TextBlock>("ConnectionStatus");
            ConnectedDisplay = this.FindControl<TextBlock>("ConnectedDisplay");
            PlaylistElement = this.FindControl<PlaylistControl>("PlaylistElement");
            PlaylistsListBox = this.FindControl<StackPanel>("PlaylistsListBox");
            CreatePlaylistButton = this.FindControl<Button>("CreatePlaylistButton");
            PropertiesPane = this.FindControl <PropertiesPaneControl>("PropertiesPane");
            SeekBar = this.FindControl<ProgressBar>("SeekBar");

            CreatePlaylistButton.Click += CreatePlaylistButton_Click;

            PlaylistElement.onSelectionChanged = PropertiesPane.OnItemUpdated;

            Opened += MainWindow_Opened;
            Closing += MainWindow_Closed;
            
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if(bridgeConnection != null && PlayingPlaylist != null)
            {
                bridgeConnection.TryDeletePlaylist(PlayingPlaylist.GetBridgePlaylist());
                bridgeConnection.TryShowWindow(false);
            }

            if(bridgeConnection != null)
            {
                bridgeConnection.Dispose();
            }
        }

        private void MainWindow_Opened(object? sender, System.EventArgs e)
        {
            InitializeBridge();
        }

        private void InitializePlaylistManager()
        {
            playlistManager = new PlaylistManager();
            playlistManager.SearchForPlaylists();
            UpdatePlaylistList();
            if (playlistManager.loadedPlaylists.Count > 0)
            {
                PlaylistElement.SetPlaylist(playlistManager.loadedPlaylists.First());
            }
        }

        private void InitializeBridge()
        {
            bridgeConnection = new Toolkit_API.Bridge.BridgeConnectionHTTP();
            bridgeConnection.AddConnectionStateListener((connectionStatusChange) =>
            {
                this.connectionStatus = connectionStatusChange;
                Dispatcher.UIThread.Post(() =>
                {
                    ConnectionStatus.Text = this.connectionStatus ? "Bridge Connected" : "Bridge Disconnected";
                    if(this.connectionStatus && bridgeConnection.LKG_Displays.Count > 0)
                    {
                        ConnectedDisplay.Text = bridgeConnection.GetLKGDisplays().First().hardwareInfo.hardwareVersion;
                    }
                });
            });

            this.connectionStatus = bridgeConnection.Connect();
            if (this.connectionStatus)
            {
                Trace.WriteLine("Connected to bridge");

                if (!bridgeConnection.TryEnterOrchestration())
                {
                    Trace.WriteLine("Failed to enter orchestration");
                    return;
                }

                if (!bridgeConnection.TrySubscribeToEvents())
                {
                    Trace.WriteLine("Failed to subscribe to events");
                    return;
                }

                if (!bridgeConnection.TryUpdateDevices())
                {
                    Trace.WriteLine("Failed to update devices");
                    return;
                }
            }
            else
            {
                Trace.WriteLine("Failed to connect to bridge, ensure bridge is running");
                return;
            }

            bridgeConnection.AddListener("Sync/Play Playlist", (data) =>
            {
                Trace.WriteLine($"data: \n{data}");
            });

            bridgeConnection.AddListener("Sync/Play Playlist Complete", (data) =>
            {
                Trace.WriteLine($"data: \n{data}");
            });

            bridgeConnection.AddListener("Sync/Play Playlist Cancelled", (data) =>
            {
                Trace.WriteLine($"data: \n{data}");
            });

            bridgeConnection.AddListener("Progress Update", (string data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(data);
                    var progressValue = json.RootElement.GetProperty("progress").GetProperty("value").GetSingle();

                    // Call the update method on the UI thread
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateProgressSlider(progressValue);
                    }).Wait();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to parse JSON data: {ex.Message}");
                }
            });

        }

        private void UpdatePlaylistList()
        {
            PlaylistsListBox.Children.Clear();

            // Load the playlists into the PlaylistsListBox
            foreach (Playlist item in playlistManager.loadedPlaylists)
            {
                var playlistItemControl = new PlaylistItemNameControl(item, SelectPlaylist, null);
                PlaylistsListBox.Children.Add(playlistItemControl);
            }
        }

        private void SelectPlaylist(Playlist selectedPlaylist)
        {
            PlaylistElement.SetPlaylist(selectedPlaylist);
        }

        void UpdateProgressSlider(float progress)
        {
            SeekBar.Value = progress;
        }

        private async void CreatePlaylistButton_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Create New Playlist", "Enter the name of the new playlist:");
            string? name = await dialog.ShowDialog<string?>(this);

            if (!string.IsNullOrEmpty(name))
            {
                playlistManager.AddPlaylist(name);
                UpdatePlaylistList();
            }
        }

        private void PlayButton_Click(object? sender, RoutedEventArgs e)
        {
            if(bridgeConnection != null)
            {
                bridgeConnection.TryTransportControlsPlay();
            }
        }

        private void PauseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (bridgeConnection != null)
            {
                bridgeConnection.TryTransportControlsPause();
            }
        }

        private void StopButton_Click(object? sender, RoutedEventArgs e)
        {
            if (bridgeConnection != null && PlayingPlaylist != null)
            {
                bridgeConnection.TryDeletePlaylist(PlayingPlaylist.GetBridgePlaylist());
                bridgeConnection.TryShowWindow(false);
            }
        }

        private void PreviousButton_Click(object? sender, RoutedEventArgs e)
        {
            if (bridgeConnection != null)
            {
                bridgeConnection.TryTransportControlsPrevious();
            }
        }

        private void NextButton_Click(object? sender, RoutedEventArgs e)
        {
            if (bridgeConnection != null)
            {
                bridgeConnection.TryTransportControlsNext();
            }
        }

    }
}
