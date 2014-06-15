using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Phone.Shell;

namespace PayNowUserControl
{
    public partial class StateSelectorControl
    {
        public event EventHandler Closed;

        public StateSelectorControl()
        {
            InitializeComponent();

            SystemTray.BackgroundColor = (Color)Application.Current.Resources["PhoneChromeColor"]; 

            var dataSource = new List<UsState>();
            dataSource.AddRange(UsState.AutoCompletions.Select(state => new UsState(state)));

            LongListSelector.ItemsSource = AlphaKeyGroup<UsState>.CreateGroups(dataSource,
                                            System.Threading.Thread.CurrentThread.CurrentUICulture,
                                            s => s.Name, true);
        }

        private void LongListSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ea = new PayNowEventArgs
            {
                IsSuccess = false,
                Response = LongListSelector.SelectedItem
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
