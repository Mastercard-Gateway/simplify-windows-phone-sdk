using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Phone.Shell;
using Newtonsoft.Json;
using SimplifyCommerce.Payments;

namespace PayNowUserControl
{
    public partial class PayNowUC
    {
        private const int TIMEOUT = 5;

        private readonly string callbackUrl, publicKey;
        private CardToken cardToken;
        public event EventHandler Closed;
        private Popup dateSelectorPopup, stateSelectorPopup, parentPopup;

        public long Amount { get; set; }
        public string CardNumber { get; set; }
        public string ItemName { get; set; }
        public string CVC { get; set; }
        public string NameOnCard { get; set; }
        public string BillingAddress { get; set; }
        public string State { get; set; }
        public int ExpYear { get; set; }
        public int ExpMonth { get; set; }
        public string ZipCode { get; set; }
        public bool IsAddressRequired { get; set; }

        private string oldCardText = "";

        public PayNowUC(string publicKey, long amount, string itemName, string callbackUrl, bool isAddresRequired = false)
        {
            InitializeComponent();

            if (amount > 9999999)
                throw new Exception("Max value is 99999.99!");

            if (String.IsNullOrWhiteSpace(publicKey) || String.IsNullOrWhiteSpace(itemName) || String.IsNullOrWhiteSpace(callbackUrl))
                throw new Exception("all parameters are required");

            Amount = amount;
            ItemName = itemName.ToUpper();
            this.callbackUrl = callbackUrl;
            this.publicKey = publicKey;
            IsAddressRequired = isAddresRequired;

            Loaded += PayNowUC_Loaded;
            DataContext = this;
        }

        void PayNowUC_Loaded(object sender, RoutedEventArgs e)
        {
            parentPopup = Parent as Popup;
            if (parentPopup != null)
                parentPopup.Closed += parentPopup_Closed;
        }

        //only if closed from outside(eg back button)
        void parentPopup_Closed(object sender, EventArgs e)
        {
            var ea = new PayNowEventArgs
            {
                IsSuccess = false,
            };

            if (dateSelectorPopup != null)
                dateSelectorPopup.IsOpen = false;
            
            if (stateSelectorPopup != null)
                stateSelectorPopup.IsOpen = false;

            SystemTray.BackgroundColor = (Color)Resources["PhoneBackgroundColor"];

            gr_busy.Visibility = Visibility.Collapsed;

            var eventHandler = Closed;
            if (eventHandler != null)
                eventHandler(this, ea);
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            RaiseEventHandler(false);
        }

        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAllFieldsFilled())
            {
                MessageBox.Show(AppResources.Msg_AllFieldsRequired, AppResources.MsgHdr_AllFieldsRequired, MessageBoxButton.OK);
                return;
            }

            gr_busy.Visibility = Visibility.Visible;

            var card = new Card
            {
                Number = CardNumber.Replace(" ",""),
                ExpMonth = ExpMonth,
                ExpYear = ExpYear,
                Cvc = CVC,
                Name = NameOnCard
            };

            if (IsAddressRequired)
            {
                card.AddressLine1 = BillingAddress;
                card.AddressZip = ZipCode;
                card.AddressState = State;
            }

            var api = new PaymentsApi();

            try
            {
                cardToken = await api.CreateCardToken(card, publicKey);
            }
            catch (Exception ex)
            {
                RaiseEventHandler(false, ex);
                return;
            }

            //TODO: just for testing, remove befor rls
            MessageBox.Show(await Task.Factory.StartNew(() => JsonConvert.SerializeObject(cardToken)), "response from simplify", MessageBoxButton.OK);

            var request = new PayNowRequest
            {
                TokenId = cardToken.Id,
                Amount = Amount,
                ItemName = ItemName
            };

            var baseLength = callbackUrl.LastIndexOf('/') + 1;
            var baseAddress = callbackUrl.Substring(0, baseLength);
            var requestUri = callbackUrl.Substring(baseLength);

            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var requestBody = await Task.Factory.StartNew(() => JsonConvert.SerializeObject(request, settings));

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(0, 0, TIMEOUT);
                    client.BaseAddress = new Uri(baseAddress);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Simplify-PayNowUC-for-WP");
                    using (var response = await client.PostAsync(requestUri, new StringContent(requestBody, Encoding.UTF8, "application/json")))
                    {
                        RaiseEventHandler(true, response);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                RaiseEventHandler(false, new TimeoutException());
            }
            catch (Exception ex)
            {
                RaiseEventHandler(false, ex);
            }
        }

        private void RaiseEventHandler(bool isSuccess, object response = null)
        {
            gr_busy.Visibility = Visibility.Collapsed;

            var e = new PayNowEventArgs
            {
                IsSuccess = isSuccess,
                Response = response
            };

            var eventHandler = Closed;
            if (eventHandler != null)
                eventHandler(this, e);

            CloseSelf();
        }

        private void CloseSelf()
        {
            if (parentPopup != null)
            {
                parentPopup.Closed -= parentPopup_Closed; //remove event listener from parent
                parentPopup.IsOpen = false;
            }
            else
            {
                var popup = Parent as Popup;
                if (popup != null)
                    popup.IsOpen = false;
            }
        }

        private void TbxCard_OnLostFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;

            if (tb == null) return;
            if (String.IsNullOrEmpty(tb.Text)) return;

            var str = tb.Text.Replace(" ", "");
            const string regexPattern = @"^(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|6(?:011|5[0-9][0-9])[0-9]{12}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\d{3})\d{11})$";
            var regex = new Regex(regexPattern);

            if (regex.IsMatch(str) && IsValidNumber(str))
                tb_cardError.Visibility = Visibility.Collapsed;
            else
                tb_cardError.Visibility = Visibility.Visible;

            tb.Text = tb.Text.Trim();
            scrollViewer.UpdateLayout();
        }

        private static bool IsValidNumber(string number)
        {
            var sumOfDigits = number
                              .Where(e => e >= '0' && e <= '9')
                              .Reverse()
                              .Select((e, i) => ((int)e - 48) * (i % 2 == 0 ? 1 : 2))
                              .Sum(e => e / 10 + e % 10);

            return sumOfDigits % 10 == 0;
        }

        private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            if (String.IsNullOrEmpty(tb.Text)) return;

            if (tb.MaxLength == tb.Text.Length) return;

            oldCardText = oldCardText.Replace(" ", "");
            var str = tb.Text.Replace(" ", "");

            if (str.Length > 3 && oldCardText.Length < str.Length)
            {
                if (str.Length % 4 == 1)
                {
                    tb.Text = tb.Text.Substring(0, tb.Text.Length - 1) + " " + tb.Text[tb.Text.Length - 1];
                    tb.Select(tb.Text.Length, 0);
                }
            }
            else if (str.Length > 3 && oldCardText.Length > str.Length)
            {
                if (tb.Text.EndsWith(" "))
                {
                    tb.Text = tb.Text.Trim();
                    tb.Select(tb.Text.Length, 0);
                }
            }

            oldCardText = tb.Text;
        }

        private void ScrollToControl()
        {
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.Height = Application.Current.Host.Content.ActualHeight / 2 - LayoutRoot.RowDefinitions[0].ActualHeight - 60;
            scrollViewer.UpdateLayout();

            scrollViewer.ScrollToVerticalOffset(scrollViewer.ExtentHeight);
        }

        private void UIElement_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var uiControl = sender as UIElement;
            if (uiControl == null) return;

            ScrollToControl();
        }

        private void UIElement_OnLostFocus(object sender, RoutedEventArgs e)
        {
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollViewer.Height = Double.NaN; //for auto
            scrollViewer.UpdateLayout();
        }

        private bool IsAllFieldsFilled()
        {
            if (String.IsNullOrWhiteSpace(NameOnCard) ||
                String.IsNullOrWhiteSpace(CVC) ||
                String.IsNullOrWhiteSpace(CardNumber) ||
                tb_cardError.Visibility == Visibility.Visible)
                return false;

            if (IsAddressRequired)
                if (String.IsNullOrWhiteSpace(BillingAddress) ||
                    String.IsNullOrWhiteSpace(State) ||
                    String.IsNullOrWhiteSpace(ZipCode))
                    return false;

            return true;
        }

        private void Tb_date_OnTap(object sender, GestureEventArgs e)
        {
            var dateselector = new CardDateSelectorControl
            {
                Height = ActualHeight,
                Width = ActualWidth
            };

            dateselector.Closed += (o, args) =>
            {
                var eventArgs = (PayNowEventArgs)args;
                var date = (List<int>)eventArgs.Response;
                if (date == null) return;

                ExpMonth = date[0];
                ExpYear = date[1] - 2000;

                tb_date.Text = ExpMonth.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') + " / " + ExpYear.ToString(CultureInfo.InvariantCulture);
            };

            dateSelectorPopup = new Popup
            {
                Child = dateselector,
                VerticalOffset = SystemTray.IsVisible ? Application.Current.Host.Content.ActualHeight - ActualHeight : 0,
                IsOpen = true
            };

            //popup.IsOpen = true;
        }

        private void Tb_state_OnTap(object sender, GestureEventArgs e)
        {
            var ss = new StateSelectorControl
            {
                Height = ActualHeight,
                Width = ActualWidth
            };

            ss.Closed += (o, args) =>
            {
                var eventArgs = (PayNowEventArgs)args;
                var state = (UsState)eventArgs.Response;
                if (state == null) return;

                tb_state.Text = state.Name;
                State = state.Key;
            };

            stateSelectorPopup = new Popup
            {
                Child = ss,
                VerticalOffset = SystemTray.IsVisible ? Application.Current.Host.Content.ActualHeight - ActualHeight : 0,
                IsOpen = true
            };
        }

        private void Tb_date_OnGotFocus(object sender, RoutedEventArgs e)
        {
            Focus();
        }

        private void CardTB_OnKeyDown(object sender, KeyEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            if (String.IsNullOrWhiteSpace(tb.Text)) return;

            var str = tb.Text.Trim().Replace(" ", "");

            if (str.StartsWith("4") || str.StartsWith("5"))
                if (str.Length == 16)
                    e.Handled = true;

        }

        private void CvcTB_OnKeyDown(object sender, KeyEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            if (String.IsNullOrWhiteSpace(tb.Text)) return;

            var ctb = CardTextBox;
            if (ctb == null) return;
            if (String.IsNullOrWhiteSpace(ctb.Text)) return;

            var str = tb.Text.Trim();
            var cstr = ctb.Text.Trim();

            if (cstr.StartsWith("4") || cstr.StartsWith("5"))
                if (str.Length == 3)
                    e.Handled = true;

        }
    }
}
