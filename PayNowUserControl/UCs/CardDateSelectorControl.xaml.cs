using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Phone.Shell;

namespace PayNowUserControl
{
    public partial class CardDateSelectorControl
    {
        private readonly DateTime currentDate = DateTime.UtcNow;
        public event EventHandler Closed;
        public string ImgPrefix { get; set; }

        public CardDateSelectorControl()
        {
            InitializeComponent();

            ((NumbersDataSource) ls_month.DataSource).SelectedItem = currentDate.Month;
            ((NumbersDataSource)ls_year.DataSource).SelectedItem = currentDate.Year;

            SystemTray.BackgroundColor = (Color)Application.Current.Resources["PhoneChromeColor"];

            var darkTheme = (Visibility) Application.Current.Resources["PhoneDarkThemeVisibility"] == Visibility.Visible;
            ImgPrefix = darkTheme ? "dark_" : "light_";

            DataContext = this;
        }

        private async void NumbersDataSource_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ls_month == null || ls_year == null) return;

            var nds = sender as NumbersDataSource;
            if (nds == null) return;

            var m = (int)((NumbersDataSource) ls_month.DataSource).SelectedItem;
            var y = (int)((NumbersDataSource) ls_year.DataSource).SelectedItem;

            if (currentDate.Year == y && currentDate.Month > m)
            {
                //tb_dateError.Visibility = Visibility.Visible;
                tb_dateError.Foreground = new SolidColorBrush(Colors.Red);
                BtnDone.IsEnabled = false;
            }
            else
            {
                //tb_dateError.Visibility = Visibility.Collapsed;
                tb_dateError.Foreground = new SolidColorBrush(Colors.Transparent);
                BtnDone.IsEnabled = true;
            }

            await Task.Factory.StartNew(() => Thread.Sleep(600));

            if (nds.Maximum == 12)
                ls_month.IsExpanded = false;
            else
                ls_year.IsExpanded = false;

        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            CloseSelf();
        }

        private void DoneClick(object sender, RoutedEventArgs e)
        {
            var m = (int) ((NumbersDataSource) ls_month.DataSource).SelectedItem;
            var y = (int) ((NumbersDataSource) ls_year.DataSource).SelectedItem;

            var ea = new PayNowEventArgs
            {
                IsSuccess = false,
                Response = new List<int>{m, y}
            };

            var eventHandler = Closed;
            if (eventHandler != null)
                eventHandler(this, ea);

            CloseSelf();
        }

        private void CloseSelf()
        {
            SystemTray.BackgroundColor = (Color)Resources["PhoneBackgroundColor"];

            var parent = Parent as Popup;
            if (parent != null) parent.IsOpen = false;
        }
    }
}
