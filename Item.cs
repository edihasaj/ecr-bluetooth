namespace EcrBluetooth
{
    public class Item
    {
        public string ItemId { get; set; }
        public double Price { get; set; }
        public double Rebate { get; set; }
        public double Amount { get; set; }
        public TaxCategory TaxCategory { get; set; }
        public string Description { get; set; }
    }
}