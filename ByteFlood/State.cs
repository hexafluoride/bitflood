﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MonoTorrent;
using MonoTorrent.Client;
using System.Xml;
using System.Xml.Serialization;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Common;
using Microsoft.Win32;
using System.Threading;
using System.Net;
using System.IO;

namespace ByteFlood
{
    public class State : INotifyPropertyChanged
    {
        public ObservableCollection<TorrentInfo> Torrents = new ObservableCollection<TorrentInfo>();

        public event PropertyChangedEventHandler PropertyChanged;
        [XmlIgnore]
        public MainWindow window = (MainWindow)App.Current.MainWindow;
        [XmlIgnore]
        public ClientEngine ce;
        [XmlIgnore]
        public SynchronizationContext uiContext;
        public int DownloadingTorrentCount { get { return Torrents.Count(window.Downloading); } set { } }
        public int SeedingTorrentCount { get { return Torrents.Count(window.Seeding); } set { } }
        public int ActiveTorrentCount { get { return Torrents.Count(window.Active); } set { } }
        public int InactiveTorrentCount { get { return TorrentCount - ActiveTorrentCount; } set { } }
        public int FinishedTorrentCount { get { return Torrents.Count(window.Finished); } set { } }
        public int TorrentCount { get { return Torrents.Count; } set { } }
        [XmlIgnore]
        public Thread mainthread;
        [XmlIgnore]
        public DhtListener dhtl;
        [XmlIgnore]
        public Listener listener;

        public State()
        {
            this.Initialize();
        }

        public void Initialize()
        {
            ce = new ClientEngine(new EngineSettings());
            dhtl = new DhtListener(new IPEndPoint(IPAddress.Any, App.Settings.ListeningPort));
            DhtEngine dht = new DhtEngine(dhtl);

            ce.RegisterDht(dht);
            ce.DhtEngine.Start();

            bool assoc = Utility.Associated();
            if (!assoc &&
                MessageBox.Show("Do you want to associate ByteFlood with .torrent files?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                Utility.SetAssociation();
            //MessageBox.Show(assoc.ToString());
            listener = new Listener();
            listener.State = this;
        }

        public static void Save(State s, string path)
        {
            Utility.Serialize<State>(s, path);
        }

        public void Shutdown()
        {
            SaveSettings();
            mainthread.Abort();
            ce.DiskManager.Flush();
            ce.PauseAll();
            listener.Shutdown();
        }

        public void SaveSettings()
        {
            Settings.Save(App.Settings, "./config.xml");
            State.Save(this, "./state.xml");
        }

        public void AddTorrentByPath(string path)
        {
            uiContext.Send(x =>
            {
                App.Current.MainWindow.Activate();
                AddTorrentDialog atd = new AddTorrentDialog(path);
                atd.ShowDialog();
                if (atd.userselected)
                {
                    TorrentInfo ti = CreateTorrentInfo(atd.tm);
                    ti.Name = atd.torrentname;
                    if (!atd.start)
                        ti.Stop();
                    ti.RatioLimit = atd.limit;
                    TorrentProperties.Apply(ti.Torrent, App.Settings.DefaultTorrentProperties);
                    ti.Torrent.Settings.InitialSeedingEnabled = atd.initial.IsChecked == true;
                    Torrents.Add(ti);
                }
            }, null);
        }

        public TorrentInfo CreateTorrentInfo(TorrentManager tm)
        {
            ce.Register(tm);
            tm.Start();
            TorrentInfo t = new TorrentInfo(uiContext);
            t.Torrent = tm;
            t.Update();
            return t;
        }

        public static State Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new State();
                return Utility.Deserialize<State>(path);
            }
            catch
            {
                MessageBox.Show("An error occurred while loading the program state. You may need to re-add your torrents.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new State();
            }
        }

        public void NotifyChanged(params string[] props)
        {
            if (PropertyChanged == null)
                return;
            foreach (string str in props)
                PropertyChanged(this, new PropertyChangedEventArgs(str));
        }
    }
}
