﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ByteFlood
{
    public class PeerInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string IP { get; set; }
        public byte[] AddressBytes { get; set; }

        private string client = null;
        public string Client
        {
            get { return this.client; }
            set 
            {
                if (value != client) 
                {
                    this.client = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("Client"));
                    }
                }
            }
        }
       
        private string pi_info = null;
        public string PieceInfo 
        {
            get { return this.pi_info; }
            set 
            {
                if (value != this.pi_info) 
                {
                    this.pi_info = value;
                    if (this.PropertyChanged != null) 
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("PieceInfo"));
                    }
                }
            }
        }

        public PeerInfo() { }
        //public void SetSelf(PeerInfo pi)
        //{
        //    this.IP = pi.IP;
        //    this.Client = pi.Client;
        //    this.PieceInfo = pi.PieceInfo;
        //    UpdateList("IP", "Client", "PieceInfo", "AddressBytes");
        //}
        public void UpdateList(params string[] columns)
        {
            if (PropertyChanged == null)
                return;
            foreach (string str in columns)
                PropertyChanged(this, new PropertyChangedEventArgs(str));
        }
    }
}
