using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Data;

namespace Notifier
{
    public class Notification
    {
        /// <summary>
        /// Set or get notification ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Set or get notification description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Set or get notification trigger time
        /// </summary>
        public DateTime Datetime { get; set; }

        /// <summary>
        /// Set or get notification timer class
        /// </summary>
        public DispatcherTimer Timer { get; set; }

        public Notification()
        {

        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Helpers
        private MediaPlayer mp = new MediaPlayer();
        private System.Windows.Forms.NotifyIcon ni;
        private string listname = "notifier.list";
        private string dateFormat = "yyyy-MM-dd HH:mm:ss";
        private bool importing = false;
        private ObservableCollection<Notification> notifications = new ObservableCollection<Notification>();

        public MainWindow()
        {
            InitializeComponent();

            Stream iconStream = Application.GetResourceStream(new Uri(@"/Notifier;component/Notifier.ico", UriKind.Relative)).Stream;
            ni = new System.Windows.Forms.NotifyIcon();

            ni.Icon = new System.Drawing.Icon(iconStream);
            ni.Visible = true;
            ni.DoubleClick += delegate (object sender, EventArgs args) {
                Show();
                Activate();
                WindowState = WindowState.Normal;
            };

            if (Properties.Settings.Default.Location == "")
            {
                Properties.Settings.Default.Location = AppDomain.CurrentDomain.BaseDirectory;
                Properties.Settings.Default.Save();
            }

            dg_list.ItemsSource = notifications;

            CollectionView myCollectionView = (CollectionView)CollectionViewSource.GetDefaultView(dg_list.Items);
            ((INotifyCollectionChanged)myCollectionView).CollectionChanged += new NotifyCollectionChangedEventHandler(DataGrid_CollectionChanged);

            importing = true;
            LoadList(Properties.Settings.Default.Location + listname);
            importing = false;

            Hide();
        }

        private void DataGrid_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            txt_note_count.Text = notifications.Count + " " + (notifications.Count == 1 ? "notification" : "notifications");

            SaveList();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                Hide();

            base.OnStateChanged(e);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            DispatcherTimer dt = (DispatcherTimer)sender;

            dt.Stop();
            Alert(dt.Tag.ToString());
            Show();
            Activate();
            WindowState = WindowState.Normal;
        }

        private void Alert(string text)
        {
            //SpeechSynthesizer synth = new SpeechSynthesizer();
            //synth.Speak("Time's up");

            try
            {
                mp.Open(new Uri("Alert.mp3", UriKind.Relative));
                mp.Volume = 1.0;
                mp.Play();

                MessageBox.Show("Nofitication: " + text, "Alert!");

                mp.Stop();
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Could not play the audio file", MessageBoxButton.OK, MessageBoxImage.Error);

                System.Media.SystemSounds.Beep.Play();
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void LoadList(string Location)
        {
            string path = Properties.Settings.Default.Location + listname;

            if (File.Exists(path) == false)
            {
                try
                {
                    using (File.Create(path)) { }
                }

                catch
                {

                }

                if (File.Exists(path) == false)
                {
                    MessageBox.Show("Could not create the file " + path + ". Make sure the application has permission write to folder.", "Could not create file", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }
            }

            using (StreamReader sr = new StreamReader(path))
            {
                while (true)
                {
                    string line = sr.ReadLine();

                    if (line == null)
                        break;

                    string[] tmpline = line.Split('|');

                    if (tmpline.Length == 3)
                    {
                        int nmb = 0;

                        Notification tmp = new Notification();

                        DateTime dt;
                        DateTime.TryParseExact(tmpline[2], dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);

                        tmp.Datetime = dt;

                        if (tmp.Datetime > DateTime.Now)
                        {
                            tmp.Timer = new DispatcherTimer();
                            tmp.Timer.Interval = new TimeSpan(tmp.Datetime.Ticks - DateTime.Now.Ticks);
                            tmp.Timer.Tick += Timer_Tick;
                            tmp.Timer.Tag = tmpline[1];
                            tmp.Timer.Start();
                            tmp.Description = tmpline[1];
                            int.TryParse(tmpline[0], out nmb);

                            if (notifications.Any(n => n.ID == nmb))
                                tmp.ID = notifications.Aggregate((l, r) => l.ID > r.ID ? l : r).ID + 1;

                            else
                                tmp.ID = nmb;

                            notifications.Add(tmp);
                        }
                    }
                }
            }
        }

        private void SaveList()
        {
            if (importing)
                return;

            string path = Properties.Settings.Default.Location + listname;

            if (Directory.Exists(Properties.Settings.Default.Location) == false)
            {
                MessageBox.Show("Folder " + Properties.Settings.Default.Location + " does not exists. Set new location.", "Folder does not exists", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            using (StreamWriter sw = new StreamWriter(path))
            {
                foreach (Notification n in notifications)
                {
                    sw.WriteLine(n.ID + "|" + n.Description + "|" + n.Datetime.ToString(dateFormat, CultureInfo.InvariantCulture));
                }
            }
        }

        private void btn_alarm_Click(object sender, RoutedEventArgs e)
        {
            int hours = int.Parse(((ComboBoxItem)combo_hours.SelectedItem).Content.ToString());
            int minutes = int.Parse(((ComboBoxItem)combo_minutes.SelectedItem).Content.ToString());

            Notification tmp = new Notification();

            tmp.Datetime = new DateTime(dp_date.SelectedDate.Value.Year, dp_date.SelectedDate.Value.Month, dp_date.SelectedDate.Value.Day, hours, minutes, 0);

            if (tb_description.Text.Trim() == "")
            {
                MessageBox.Show("Give description", "Description missing", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (tmp.Datetime <= DateTime.Now)
            {
                MessageBox.Show("Time cannot be in the past", "Incorrect time", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            tmp.Timer = new DispatcherTimer();
            tmp.Timer.Interval = new TimeSpan(tmp.Datetime.Ticks - DateTime.Now.Ticks);
            tmp.Timer.Tick += Timer_Tick;
            tmp.Timer.Tag = tb_description.Text;
            tmp.Timer.Start();
            tmp.Description = tb_description.Text;

            if (notifications.Count > 0)
                tmp.ID = notifications.Aggregate((l, r) => l.ID > r.ID ? l : r).ID + 1;

            else
                tmp.ID = 0;

            notifications.Add(tmp);

            tb_description.Text = "";
            combo_hours.SelectedIndex = 0;
            combo_minutes.SelectedIndex = 0;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool close = true;

            if (notifications.Count > 0)
            {
                if (MessageBox.Show("There are upcoming notifications on the list. Are you sure you want to close the program?", "Closing the Program", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    close = true;
                }
            }

            if (close)
                ni.Dispose();
        }

        private void mi_list_location_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            dialog.Description = "Select a folder where you want to store the notification list";
            dialog.SelectedPath = Properties.Settings.Default.Location;

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.Location = dialog.SelectedPath + "\\";
                Properties.Settings.Default.Save();
                SaveList();

                MessageBox.Show("Path set to " + Properties.Settings.Default.Location);
            }
        }

        private void mi_list_import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            dialog.DefaultExt = "list";
            dialog.Multiselect = false;
            dialog.Filter = "List files (*.list)|*.list|All files (*.*)|*.*";

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                importing = true;
                LoadList(dialog.FileName);
                importing = false;
                SaveList();
            }
        }

        private void dg_list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dg_list.SelectedIndex > -1)
                btn_remove.IsEnabled = true;

            else
                btn_remove.IsEnabled = false;
        }

        private void btn_remove_Click(object sender, RoutedEventArgs e)
        {
            if (dg_list.SelectedIndex > -1)
            {
                for (int i = 0; i < notifications.Count; i++)
                {
                    if (notifications[i].ID == ((Notification)dg_list.SelectedItem).ID)
                    {
                        notifications[i].Timer.Stop();
                        notifications[i].Timer = null;

                        notifications.RemoveAt(i);
                        i = notifications.Count;
                        btn_remove.IsEnabled = false;
                    }
                }
            }
        }

        private void dp_date_Loaded(object sender, RoutedEventArgs e)
        {
            dp_date.SelectedDate = DateTime.Now;
        }
    }
}
