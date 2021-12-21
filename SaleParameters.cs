using System.Collections.Generic;

namespace EcrBluetooth
{
    public class SaleParameters
    {
        public SaleParameters()
        {
            Items = new List<Item>();
            Payments = new List<Payment>();
        }
        
        public string OperatorName { get; set; }
        public List<Item> Items { get; set; }
        public List<Payment> Payments { get; set; }
        public ProgramLine ProgramLine { get; set; }
    }
}