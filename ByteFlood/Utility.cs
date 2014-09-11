﻿/* 
    ByteFlood - A BitTorrent client.
    Copyright (C) 2014 ***REMOVED***

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
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
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ByteFlood
{
    public static class Utility
    {

        public static Dictionary<string, BitmapImage> IconCache = new Dictionary<string, BitmapImage>();

        const double K = 1024;
        const double M = 1048576;
        const double G = 1073741824;
        const double T = 1099511627776;

        static string OpenWith = Assembly.GetCallingAssembly().Location;

        public static string PrettifyAmount(double amount)
        {
            if (amount > T)
                return (amount / T).ToString("0.00") + " TB";
            if (amount > G)
                return (amount / G).ToString("0.00") + " GB";
            if (amount > M)
                return (amount / M).ToString("0.00") + " MB";
            if (amount > K)
                return (amount / K).ToString("0.00") + " KB";
            return amount.ToString("0.00") + " B";
        }

        //public static string PrettifyAmount(long amount)
        //{
        //    return PrettifyAmount((ulong)amount);
        //}

        public static string PrettifySpeed(long speed)
        {
            return PrettifyAmount((ulong)speed) + "/s";
        }

        public static bool IsMagnetLink(string path)
        {
            return path.StartsWith("magnet:"); // not a good criteria but it works
        }

        public static Label GenerateLabel(string text, Thickness margin)
        {
            Label l = new Label();
            l.Content = text;
            l.Width = double.NaN; // equivalent of Width="Auto"
            l.Height = double.NaN;

            l.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            l.Margin = margin;
            return l;
        }

        public static void SetDataContext<T>(T[] elements, object datacontext) where T : FrameworkElement
        {
            foreach (T element in elements)
                element.DataContext = datacontext;
        }

        public static void SetItemsSource<T>(T[] elements, object[] itemssource) where T : ItemsControl
        {
            foreach (T element in elements)
                element.ItemsSource = itemssource;
        }

        public static int QuickFind(ObservableCollection<FileInfo> list, string path)
        {
            int ret = -1;
            Parallel.For(0, list.Count, i =>
            {
                if (list[i].Name == path)
                {
                    ret = i;
                    return;
                }
            });
            return ret;
        }

        public static Color ToWPFColor(this System.Drawing.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Drawing.Color ToWinFormColor(this Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static Line GenerateLine(double x1, double y1, double x2, double y2, Brush color, int thickness = 2, int zindex = 1)
        {
            Line line = new Line();
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.StrokeThickness = thickness;
            line.Stroke = color;
            return line;
        }

        public static Thickness SizeToMargin(Thickness orig)
        {
            return new Thickness(orig.Left, orig.Top, orig.Right - orig.Left, orig.Bottom - orig.Top);
        }
        public static void SetIfLowerThan(ref double orig, double newval)
        {
            if (newval < orig)
                orig = newval;
        }
        public static void SetIfHigherThan(ref double orig, double newval)
        {
            if (newval > orig)
                orig = newval;
        }
        public static double CalculateLocation(double spp, double data)
        {
            return data * spp;
        }

        public static bool IsWindows8OrNewer
        {
            get
            {
                var os = Environment.OSVersion;
                return os.Platform == PlatformID.Win32NT &&
                       (os.Version.Major > 6 || (os.Version.Major == 6 && os.Version.Minor >= 2));
            }
        }

        public static void Serialize<T>(T t, string path)
        {
            string temp_file = path + ".b";
            try
            {
                using (XmlWriter xw = XmlWriter.Create(temp_file, new XmlWriterSettings()
                {
                    Indent = true
                }))
                {
                    new XmlSerializer(typeof(T)).Serialize(xw, t);
                    xw.Flush();
                }
                // Check if the new generated file is not corrupted.
                // The XML Serializer can file and destroy old data (such as state data)
                System.IO.FileInfo fi = new System.IO.FileInfo(temp_file);

                if (fi.Exists && fi.Length > 0)
                {
                    //Delete old file and replace with new one
                    File.Delete(path);
                    File.Move(temp_file, path);
                }
            }
            catch
            {
                if (File.Exists(temp_file)) { File.Delete(temp_file); }
                return;
            }
        }

        public static void ReloadTheme(Theme t)
        {
            var app = (ByteFlood.App)App.Current;
            app.LoadTheme(t);
            MainWindow mw = app.MainWindow as MainWindow;

            // I just hope I won't touch this code ever again.
            Style oldstyle = mw.Resources["SelectedItemCheckerButton"] as Style;
            Style newstyle = new System.Windows.Style(typeof(Button));

            if (t == Theme.Aero2)
            {
                // when on Aero2, simply setting IsEnabled to false will result in an ugly background.
                // Having ugly hacks instead of ugly UI is better IMO.
                Style newimagestyle = new Style(typeof(Image), null); // we have to create a style for the Image because the button is technically not disabled
                DataTrigger trig = new DataTrigger();                 // so we have to dim the Image to make it look disabled and sleek.
                Binding b = new Binding();
                b.ElementName = "mainlist";
                b.Path = new PropertyPath(ListView.SelectedIndexProperty); // checks if an item is selected
                trig.Binding = b;
                trig.Value = -1;
                trig.Setters.Add(new Setter(Image.OpacityProperty, 0.5d));
                newimagestyle.Triggers.Add(trig);
                mw.Resources["SelectedItemCheckerImage"] = newimagestyle;

                DataTrigger trig2 = new DataTrigger();
                trig2.Binding = b;
                trig2.Value = -1;
                trig2.Setters.Add(new Setter(Button.IsHitTestVisibleProperty, false));
                newstyle.Triggers.Add(trig2);

                //Binding b2 = new Binding();
                //b2.RelativeSource = new RelativeSource(RelativeSourceMode.Self);
                //b2.Path = new PropertyPath(Button.IsMouseOverProperty);

                //MultiDataTrigger mtrig = new MultiDataTrigger();
                //mtrig.Conditions.Add(new Condition(b, -1));
                //mtrig.Conditions.Add(new Condition(b2, true));
                //mtrig.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
                //newstyle.Triggers.Add(mtrig);
            }
            else
            {
                foreach (var trigger in oldstyle.Triggers)
                    newstyle.Triggers.Add(trigger);
                newstyle.BasedOn = Application.Current.TryFindResource(typeof(Button)) as Style;
            }
            mw.Resources["SelectedItemCheckerButton"] = newstyle;
        }

        // TODO: Tidy this up
        public static ProcessStartInfo ParseCommandLine(string command)
        {
            StringReader sr = new StringReader(command);
            string filename = "";
            string args = "";
            bool file_read = false;
            while (sr.Peek() != -1) // this makes us read the whole string
            {
                if (!file_read) // grab the filename first
                {
                    if (sr.Peek() == '"') // if there are quotes, let's parse that into one nice string
                    {
                        sr.Read(); // ignore the quotes
                        while (sr.Peek() != -1 && sr.Peek() != '"')
                            filename += (char)sr.Read(); // append to filename until there are no more chars or we find another quote
                        if (!string.IsNullOrWhiteSpace(filename))
                        {
                            sr.Read(); // ignore the end-quotes
                            file_read = true;
                        }
                    }
                    if (file_read)
                        continue; // we won't need to parse again, we already have the filename
                    while (sr.Peek() != ' ' && sr.Peek() != -1)
                        filename += (char)sr.Read(); // just append until we encounter a space
                    if (sr.Peek() == -1)
                        break;
                    file_read = true;
                }
                args += (char)sr.Read(); // the rest of the string are arguments
            }
            filename = filename.Trim(); // just to be sure
            args = args.Trim();
            return new ProcessStartInfo(filename, args);
        }

        public static object CloneObject(object source)
        {
            Type type = source.GetType();
            object target;
            if (type == Settings.DefaultSettings.GetType())
                target = Settings.DefaultSettings;
            else
                target = Activator.CreateInstance(type);

            PropertyInfo[] prop_info = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (PropertyInfo item in prop_info)
            {
                if (item.CanWrite)
                {
                    if (item.PropertyType.IsValueType || item.PropertyType.IsEnum || item.PropertyType.Equals(typeof(System.String)) || item.PropertyType.Equals(typeof(List<string>)))
                    {
                        item.SetValue(target, item.GetValue(source, null), null);
                    }
                    else
                    {
                        object prop_val = item.GetValue(source, null);
                        if (prop_val == null)
                        {
                            item.SetValue(target, null, null);
                        }
                        else
                        {
                            item.SetValue(target, CloneObject(prop_val), null);
                        }
                    }
                }
            }
            return target;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
        public static void SetAssociation(string KeyName = "ByteFlood", string Description = "TORRENT File", string Extension = ".torrent")
        {
            RegistryKey BaseKey;
            RegistryKey OpenMethod;
            RegistryKey Shell;
            RegistryKey CurrentUser;

            BaseKey = Registry.CurrentUser.OpenSubKey("Software\\Classes", true).CreateSubKey(Extension);
            BaseKey.SetValue("", KeyName);

            OpenMethod = Registry.CurrentUser.OpenSubKey("Software\\Classes", true).CreateSubKey(KeyName);
            OpenMethod.SetValue("", Description);
            OpenMethod.CreateSubKey("DefaultIcon").SetValue("", "\"" + OpenWith + "\",0");
            Shell = OpenMethod.CreateSubKey("Shell");
            Shell.CreateSubKey("open").CreateSubKey("command").SetValue("", "\"" + OpenWith + "\"" + " \"%1\"");
            BaseKey.Close();
            OpenMethod.Close();
            Shell.Close();

            CurrentUser = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\.torrent", true);
            CurrentUser.DeleteSubKey("UserChoice", false);
            CurrentUser.Close();

            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        public static bool Associated(string KeyName = "ByteFlood", string Description = "TORRENT File", string Extension = ".torrent")
        {
            RegistryKey BaseKey;
            RegistryKey OpenMethod;
            RegistryKey Shell;

            BaseKey = Registry.CurrentUser.OpenSubKey("Software", true).OpenSubKey("Classes", true);
            if (!BaseKey.GetSubKeyNames().Contains(Extension))
                return false;
            BaseKey = BaseKey.OpenSubKey(Extension);

            OpenMethod = Registry.CurrentUser.OpenSubKey("Software", true).OpenSubKey("Classes", true);
            if (!OpenMethod.GetSubKeyNames().Contains(KeyName))
                return false;
            OpenMethod = OpenMethod.OpenSubKey(KeyName);
            Shell = OpenMethod.OpenSubKey("Shell");
            if (!Shell.GetSubKeyNames().Contains("open"))
                return false;
            BaseKey.Close();
            OpenMethod.Close();
            Shell.Close();
            return true;
        }

        public static bool FileAssociated()
        {
            return Associated();
        }

        public static bool MagnetAssociated()
        {
            return Associated("ByteFlood", "Magnet link", "magnet");
        }

        public static void FileAssociate()
        {
            SetAssociation();
        }

        public static void MagnetAssociate()
        {
            SetAssociation("ByteFlood", "Magnet link", "magnet");
        }

        public static T Deserialize<T>(string path)
        {
            string s = File.ReadAllText(path);
            XmlReader x = XmlReader.Create(new StringReader(s));
            return (T)new XmlSerializer(typeof(T)).Deserialize(x);
        }

        public static byte[] DecompressGzip(byte[] gzip_data)
        {
            using (var stream = new System.IO.Compression.GZipStream(new MemoryStream(gzip_data), System.IO.Compression.CompressionMode.Decompress))
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        public static string CleanFileName(string name)
        {
            StringBuilder n = new StringBuilder();
            foreach (char c in name)
            {
                if (c == '/' ||
                    c == '\\' ||
                    c == ':' ||
                     c == '*' ||
                     c == '?' ||
                     c == '"' ||
                     c == '<' ||
                     c == '>' ||
                     c == '|')
                {
                    continue;
                }
                else
                {
                    n.Append(c);
                }

            }
            return n.ToString();
        }

        /// <summary>
        /// Please don't inlude dots. 'v0.6.2' become 062
        /// </summary>
        public static int ByteFloodVersion
        {
            get
            {
                return 62;
            }
        }
    }
}
