using System;

namespace PayNowUserControl
{
    public class PayNowEventArgs : EventArgs
    {
        public bool IsSuccess { get; set; }
        public object Response { get; set; }
    }
}
