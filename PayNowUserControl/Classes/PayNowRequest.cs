using Newtonsoft.Json;

namespace PayNowUserControl
{
    public class PayNowRequest
    {
        [JsonProperty("id")]
        public string TokenId { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }

        [JsonProperty("itemName")]
        public string ItemName { get; set; }
    }
}
