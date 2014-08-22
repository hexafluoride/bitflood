﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Common;
using Microsoft.Win32;
using System.Threading;
using System.Xml.Serialization;
using System.Xml;
using System.Threading.Tasks;

namespace ByteFlood
{
    public class TorrentInfo : INotifyPropertyChanged
    {
        #region Properties and variables
        public event PropertyChangedEventHandler PropertyChanged;
        [XmlIgnore]
        public TorrentManager Torrent { get; set; }
        [XmlIgnore]
        public string Ratio { get { return RawRatio.ToString("0.000"); } }
        [XmlIgnore]
        public TimeSpan ETA { get; private set; }
        public string Path = "";
        public string SavePath = "";
        public TorrentSettings TorrentSettings { get; set; }
        public string Name { get; set; }
        public double Progress { get { return Torrent.Progress; } set { } } 
        public long Size { get { return Torrent.Torrent.Size; }  }
        public int DownloadSpeed { get { return Torrent.Monitor.DownloadSpeed; }  }
        public int MaxDownloadSpeed { get { return Torrent.Settings.MaxDownloadSpeed; } }
        public int MaxUploadSpeed { get { return Torrent.Settings.MaxUploadSpeed; } }
        public int UploadSpeed { get { return Torrent.Monitor.UploadSpeed; }  }
        public TimeSpan Elapsed { get { return DateTime.Now.Subtract(StartTime); }  }
        public DateTime StartTime { get; set; }
        public long PieceLength { get { return Torrent.Torrent.PieceLength; }  }
        public int HashFails { get { return Torrent.HashFails; }  }
        public long WastedBytes { get { return PieceLength * HashFails; }  }
        public int Seeders { get { return Torrent.Peers.Seeds; }  }
        public int Leechers { get { return Torrent.Peers.Leechs; }  }
        public long Downloaded { get { return Torrent.Monitor.DataBytesDownloaded; }  }
        public long Uploaded { get { return Torrent.Monitor.DataBytesUploaded; }  }
        public string Status { get { return Torrent.State.ToString() == "DontDownload" ? "Don't download" : Torrent.State.ToString(); } }
        public int PeerCount { get { return Seeders + Leechers; }  }
        public long SizeToBeDownloaded { get { return Torrent.Torrent.Files.Select<TorrentFile, long>(t => t.Priority != Priority.DoNotDownload ? t.Length : 0).Sum(); } }
        public bool ShowOnList
        {
            get
            {
                
                if (Torrent == null)
                    return false;
                return Invisible || ((MainWindow)App.Current.MainWindow).itemselector(this);
            }
        }
        public bool Invisible { get; set; }
        public float RawRatio { get; set; }
        public float RatioLimit { get; set; }
        [XmlIgnore]
        public float AverageDownloadSpeed { get { return downspeeds.Count == 0 ? 0 : downspeeds.Average(); } set { } }
        [XmlIgnore]
        public float AverageUploadSpeed { get { return upspeeds.Count == 0 ? 0 : upspeeds.Average(); } set { } }
        [XmlIgnore]
        public ObservableCollection<PeerInfo> Peers = new ObservableCollection<PeerInfo>();
        [XmlIgnore]
        public ObservableCollection<PieceInfo> Pieces = new ObservableCollection<PieceInfo>();
        public ObservableCollection<FileInfo> Files = new ObservableCollection<FileInfo>();
        public ObservableCollection<TrackerInfo> Trackers = new ObservableCollection<TrackerInfo>();
        [XmlIgnore]
        private bool hooked_pieces = false;
        private SynchronizationContext context;
        [XmlIgnore]
        public List<float> DownSpeeds
        {
            get
            {
                var t = downspeeds.Skip(downspeeds.Count - 50);
                if (t.Count() < 50)
                {
                    int count = 50 - t.Count();
                    var f = Enumerable.Repeat<float>(0f, count);
                    t = f.Concat(t);
                }
                return t.ToList();
            }
        }
        [XmlIgnore]
        public List<float> upspeeds = new List<float>();
        [XmlIgnore]
        public List<float> UpSpeeds
        {
            get
            {
                var t = upspeeds.Skip(downspeeds.Count - 50);
                if (t.Count() < 50)
                {
                    int count = 50 - t.Count();
                    var f = Enumerable.Repeat<float>(0f, count);
                    t = f.Concat(t);
                }
                return t.ToList();
            }
        }

        private ParallelOptions parallel = new ParallelOptions() {
            MaxDegreeOfParallelism = 8 // 8 seems like a good ground, even for single core machines.
        };
        [XmlIgnore]
        public List<float> downspeeds = new List<float>();
        #endregion 

        public TorrentInfo() // this is reserved for the XML deserializer.
        {
        }

        public TorrentInfo(SynchronizationContext c)
        {
            context = c;
            Name = "";
            StartTime = DateTime.Now;
        }

        private void TryHookPieceHandler()
        {
            if (hooked_pieces)
                return;
            try
            {
                Torrent.PieceHashed += new EventHandler<PieceHashedEventArgs>(PieceHashed);
                hooked_pieces = true;
            }
            catch { }
        }
        public void Stop()
        {
            Torrent.Stop();
        }
        public void Start()
        {
            Torrent.Start();
        }
        public void UpdateGraphData()
        {
            downspeeds.Add(Torrent.Monitor.DownloadSpeed);
            upspeeds.Add(Torrent.Monitor.UploadSpeed);
        }
        
        public void Pause()
        {
            // TorrentSettings ts = new TorrentSettings();

            Torrent.Pause();
        }
        void PieceHashed(object sender, PieceHashedEventArgs e)
        {
            if (e.HashPassed)
            {
                try
                {
                    var results = Pieces.Where(t => t.ID == e.PieceIndex);
                    if (results.Count() != 0)
                    {
                        int index = Pieces.IndexOf(results.ToList()[0]);
                        context.Send(x => Pieces[index].Finished = true, null);
                        return;
                    }
                }
                catch (InvalidOperationException) { }
            }
            PieceInfo pi = new PieceInfo();
            pi.ID = e.PieceIndex;
            pi.Finished = e.HashPassed;
            context.Send(x => Pieces.Add(pi), null);
        }
        public void OffMyself() // Dispose
        {
            if (Torrent.State != TorrentState.Stopped)
                Torrent.Stop();
            Torrent.Dispose();
        }

        public long GetDownloadedBytes() // I have to use this because Torrent.Monitor only shows bytes downloaded in this session
        {
            long ret = 0;
            foreach (TorrentFile file in Torrent.Torrent.Files)
            {
                ret += file.BytesDownloaded;
            }
            return ret;
        }

        public void Update()
        {
            if (this.Torrent == null)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    MainWindow mw = Application.Current.MainWindow as MainWindow;
                    this.context = mw.uiContext;
                    
                    this.Torrent = new TorrentManager(MonoTorrent.Common.Torrent.Load(this.Path), SavePath, TorrentSettings, false);
                    mw.state.ce.Register(this.Torrent);
                    this.Start();
                }));
            }

            TryHookPieceHandler();
            try // I hate having to do this
            {
                UpdateProperties();
                //context.Send(x => Peers.Clear(), null);
                ThreadPool.QueueUserWorkItem(new WaitCallback(UpdateFileList));
                ThreadPool.QueueUserWorkItem(new WaitCallback(UpdatePeerList));
                ThreadPool.QueueUserWorkItem(new WaitCallback(UpdateTrackerList));
                if (PropertyChanged != null)
                {
                    UpdateList("DownloadSpeed",
                        "UploadSpeed",
                        "PeerCount",
                        "Seeders",
                        "Leechers",
                        "Downloaded",
                        "Uploaded",
                        "Progress",
                        "Status",
                        "Ratio", 
                        "ETA", 
                        "Size", 
                        "Elapsed", 
                        "TorrentSettings",
                        "WastedBytes",
                        "HashFails",
                        "AverageDownloadSpeed",
                        "AverageUploadSpeed",
                        "MaxDownloadSpeed",
                        "MaxUploadSpeed",
                        "ShowOnList");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
        private void UpdateTrackerList(object obj)
        {
            foreach (var tracker in Torrent.Torrent.AnnounceUrls)
            {
                foreach (string str in tracker)
                {
                    var results = Trackers.Where(t => t.URL == str);
                    int index = -1;
                    if (results.Count() != 0)
                        index = Trackers.IndexOf(results.ToList()[0]);
                    TrackerInfo ti = new TrackerInfo();
                    ti.URL = str;
                    if (index == -1)
                        context.Send(x => Trackers.Add(ti), null);
                    else
                        context.Send(x => Trackers[index].SetSelf(ti), null);
                }
            }
        }
        private void UpdatePeerList(object obj)
        {
            var peerlist = Torrent.GetPeers();
            Parallel.ForEach(peerlist, parallel, peer =>
            {
                var results = Peers.Where(t => t.IP == peer.Uri.ToString());
                int index = -1;
                if (results.Count() != 0)
                    index = Peers.IndexOf(results.ToList()[0]);
                PeerInfo pi = new PeerInfo();
                pi.IP = peer.Uri.ToString();
                pi.PieceInfo = peer.PiecesReceived + "/" + peer.PiecesSent;
                pi.Client = peer.ClientApp.Client.ToString();
                if (index == -1)
                    context.Send(x => Peers.Add(pi), null);
                else
                    context.Send(x => Peers[index].SetSelf(pi), null);
            });
            Parallel.For(0, peerlist.Count, parallel, i =>
            {
                try
                {
                    PeerInfo peer = Peers[i];
                    var results = peerlist.Where(t => t.Uri.ToString() == peer.IP);
                    if (results.Count() == 0)
                    {
                        context.Send(x => Peers.Remove(peer), null);
                    }
                }
                catch
                {

                }
            });
        }
        private void UpdateFileList(object obj)
        {
            Parallel.ForEach(Torrent.Torrent.Files, parallel, file =>
            {
                int index = Utility.QuickFind(Files, file.FullPath);
                FileInfo fi = new FileInfo();
                fi.Name = file.FullPath;
                fi.Priority = (file.Priority == Priority.DoNotDownload ? "Don't download" : file.Priority.ToString());
                fi.Progress = (int)(((float)file.BytesDownloaded / (float)file.Length) * 100);
                fi.RawSize = (uint)file.Length;
                if (index == -1)
                    context.Send(x => Files.Add(fi), null);
                else
                    context.Send(x => Files[index].SetSelf(fi), null);
            });
        }
        public void UpdateList(params string[] columns)
        {
            foreach (string str in columns)
                PropertyChanged(this, new PropertyChangedEventArgs(str));
        }

        private void UpdateProperties()
        {
            this.Path = Torrent.Torrent.TorrentPath;
            this.SavePath = Torrent.SavePath;
            this.TorrentSettings = Torrent.Settings;
            var seconds = 0;
            if (this.DownloadSpeed > 0)
            {
                seconds = Convert.ToInt32(this.Size / this.DownloadSpeed);
            }
            this.ETA = new TimeSpan(0, 0, seconds);
            if (!this.Torrent.Complete)
                this.RawRatio = ((float)Torrent.Monitor.DataBytesUploaded / (float)Torrent.Monitor.DataBytesDownloaded);
            else
                this.RawRatio = ((float)Torrent.Monitor.DataBytesUploaded / (float)GetDownloadedBytes()); // sad :(
            if (this.RawRatio >= this.RatioLimit && this.RatioLimit != 0)
            {
                this.Torrent.Settings.UploadSlots = 0;
            }
        }
    }
}
