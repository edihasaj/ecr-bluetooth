using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EcrBluetooth
{
    public class Ecr
    {
        private const int MaxDataSize = 218;
        private int _packetSeq = 0x20;

        public string Command(int command, string? data)
        {
            _packetSeq++;
            if (_packetSeq > 0x7F)
                _packetSeq = 0x20;

            return GetPacket(command, data);
        }

        private static string GetPacket(int command, string? arguments)
        {
            var len = (uint) (arguments?.Length ?? 0);
            var data = ToAnsi(arguments);

            for (;;)
            {
                var buf = new byte[MaxDataSize];
                uint offs = 0;
                var crc = 0;

                if (len > MaxDataSize) throw new ArgumentException("Lenght of the packet exceeds the limits!");

                // Set control symbol
                buf[offs++] = 0x01;
                buf[offs++] = (byte) (0x24 + len);
                const int mPacketSeq = 0x20;

                // Set packet sequence
                buf[offs++] = mPacketSeq;

                // Set command
                buf[offs++] = (byte) command;

                // Set data
                if (len > 0)
                    Array.Copy(data ?? throw new InvalidOperationException(), 0,
                        buf, offs, len);

                //[self toAnsi:data data:&buf[offs]];
                offs += len;
                // Set control symbol
                buf[offs++] = 0x05;
                // Calculate checksum
                for (var i = 1; i < offs; i++) crc += buf[i] & 0xff;

                // Set checksum
                buf[offs++] = (byte) (((crc >> 12) & 0xf) + 0x30);
                buf[offs++] = (byte) (((crc >> 8) & 0xf) + 0x30);
                buf[offs++] = (byte) (((crc >> 4) & 0xf) + 0x30);
                buf[offs++] = (byte) (((crc >> 0) & 0xf) + 0x30);
                // Set control symbol
                buf[offs] = 0x03;

                return Encoding.UTF8.GetString(buf, 0, buf.Length);
            }
        }

        private static byte[]? ToAnsi(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var data = new byte[text.Length];
            for (var s = 0; s < text.Length; s++)
            {
                var c = text[s];
                data[s] = (byte) c;
                if (c < 0x80)
                    continue;

                data[s] = (byte) c;
            }

            return data;
        }

        public string GetReportByteString(ReportType reportType)
        {
            return reportType switch
            {
                ReportType.XReport => Command(69, "2"),
                ReportType.ZReport => Command(69, "0"),
                _ => throw new ArgumentOutOfRangeException(nameof(reportType), reportType, null)
            };
        }

        public string GetCancelReceiptByte()
        {
            return Command(60, string.Empty);
        }

        public string GetDeleteArticlesByte()
        {
            return Command(107, "DA");
        }

        public string GetCloseReceiptByte()
        {
            return Command(56, string.Empty);
        }

        public string GetPrintDuplicateReceiptByte()
        {
            return Command(109, "1");
        }

        public string GetSetOperatorNameByte(string operatorName)
        {
            return Command(102, $"1,000000,{operatorName}");
        }

        public string GetInitializeReceiptByte()
        {
            return Command(48, "1;000000;1");
        }

        public string GetRegisterItemBytes(Item item)
        {
            item.Price = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero);
            var priceString = item.Price.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(priceString) && priceString.Contains(','))
                priceString = priceString.Replace(",", ".");

            if (item.Description.Length >= 25)
                item.Description = item.Description[..24];

            var command =
                $"p{Enum.GetName(item.TaxCategory)}{item.ItemId},1,{priceString},10000,..\\t{item.Description}\\t..";
            if (string.IsNullOrEmpty(command)) throw new Exception("Data should contain value");
            return Command(107, command);
        }

        public List<string> GetRegisterItemsBytes(List<Item> items)
        {
            var byteList = new List<string>();

            foreach (var item in items)
            {
                item.Price = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero);
                var priceString = item.Price.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(priceString) && priceString.Contains(','))
                    priceString = priceString.Replace(",", ".");

                if (item.Description.Length >= 25)
                    item.Description = item.Description[..24];

                var command =
                    $"p{Enum.GetName(item.TaxCategory)}{item.ItemId},1,{priceString},10000,..\\t{item.Description}\\t..";
                if (string.IsNullOrEmpty(command)) throw new Exception("Data should contain value");
                byteList.Add(Command(107, command));
            }

            return byteList;
        }

        public List<string> GetSellItemsBytes(List<Item> items)
        {
            var byteList = new List<string>();

            foreach (var item in items)
            {
                string isVoid;
                string amountString;

                item.Price = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero);
                item.Rebate = Math.Round(item.Rebate, 2, MidpointRounding.AwayFromZero);

                if (item.Amount < 0)
                {
                    isVoid = "D^";
                    amountString = (item.Amount * -1).ToString("0.000");
                }
                else
                {
                    isVoid = "D";
                    amountString = item.Amount.ToString("0.000");
                }

                var command = $"{isVoid}{item.ItemId}*{amountString}#{item.Price}";
                if (item.Rebate != 0)
                    command += $",-{item.Rebate}";

                if (item.Description.Length >= 25)
                    item.Description = item.Description[..24];

                if (string.IsNullOrEmpty(command)) throw new Exception("Data should contain value");
                byteList.Add(Command(58, command));
            }

            return byteList;
        }

        public List<string> GetPaymentBytes(List<Payment> payments)
        {
            var paymentList = new List<string>();

            foreach (var payment in payments)
            {
                payment.Value = Math.Round(payment.Value, 2, MidpointRounding.AwayFromZero);
                var valueString = payment.Value.ToString(CultureInfo.InvariantCulture);
                if (valueString.Contains(','))
                    valueString = valueString.Replace(",", ".");

                var command = $"{Enum.GetName(payment.PaymentMethod)}";
                if (payments.Count == 1)
                    command += valueString;

                paymentList.Add(Command(53, command));
            }

            paymentList.Add(Command(110, string.Empty));
            return paymentList;
        }

        public List<string> GetProgramLinesBytes(ProgramLine line)
        {
            var programLines = new List<string>();

            var forReturn = line.TotalCashPayed - line.TotalInvoiceValue < 0
                ? "0.00"
                : (line.TotalCashPayed -
                   line.TotalInvoiceValue).ToString("0.00");
            var totalCommand = $"Tot. paguar: {line.TotalCashPayed:0.00}; Kusuri: {forReturn}";
            programLines.Add(Command(43, totalCommand));

            programLines.Add(line.TotalPoints == 0
                ? Command(43, $"7Ju Faleminderit! / Nr: {line.ReceiptNumber}")
                : Command(43, $"7Pikë: {line.BonusPoints}/{line.TotalPoints}; Nr: {line.ReceiptNumber}"));

            return programLines;
        }

        public string GetDecodeResponse(byte[] resultData, List<int> statusResponses)
        {
            var data = Encoding.UTF8.GetString(resultData);
            var fl = statusResponses.Aggregate("", (current, listItems) =>
                current + string.Format("{0:x2}", listItems));

            return data + " / " + fl;
        }

        public string GetDecodeResultData(byte[] resultData)
        {
            return Encoding.UTF8.GetString(resultData);
        }

        public string GetDecodeResultStatus(byte[] status)
        {
            return $"{status:x2}";
        }

        public bool GetHasPaperBytes(byte[] status)
        {
            string lastBit;
            try
            {
                if (status.Length >= 1)
                {
                    var i = 0;
                    status = new byte[2];
                    foreach (var item in status)
                    {
                        if (i < 2)
                            status[i] = 0;

                        i++;
                    }
                }

                var input = status[1].ToString();
                var inputAsNumber = Convert.ToInt32(input);

                var output = Convert.ToString(inputAsNumber, 2);
                output = output.PadLeft(8, '0');

                lastBit = output.Substring(7, 1);
            }
            catch (Exception)
            {
                lastBit = "0";
            }

            return lastBit != "1";
        }

        public List<ReturnValue> GetPrintReceipt(SaleParameters parameters)
        {
            var operatorNameByteList = GetSetOperatorNameByte(parameters.OperatorName);
            var initializeReceiptByte = GetInitializeReceiptByte();
            var response = new List<ReturnValue>();

            var iterator = 1;
            response.Add(new ReturnValue
            {
                Id = iterator,
                Value = operatorNameByteList
            });
            iterator++;
            response.Add(new ReturnValue
            {
                Id = iterator,
                Value = initializeReceiptByte
            });
            iterator++;

            var programLineBytes = GetProgramLinesBytes(parameters.ProgramLine);
            foreach (var programLineByte in programLineBytes)
            {
                response.Add(new ReturnValue
                {
                    Id = iterator,
                    Value = programLineByte
                });
                iterator++;
            }

            var registerItemsBytes = GetRegisterItemsBytes(parameters.Items);
            foreach (var registerItems in registerItemsBytes)
            {
                response.Add(new ReturnValue
                {
                    Id = iterator,
                    Value = registerItems
                });
                iterator++;
            }

            var sellItemsBytes = GetSellItemsBytes(parameters.Items);
            foreach (var sellItemsByte in sellItemsBytes)
            {
                response.Add(new ReturnValue
                {
                    Id = iterator,
                    Value = sellItemsByte
                });
                iterator++;
            }

            var paymentBytes = GetPaymentBytes(parameters.Payments);
            foreach (var paymentByte in paymentBytes)
            {
                response.Add(new ReturnValue
                {
                    Id = iterator,
                    Value = paymentByte
                });
                iterator++;
            }

            var getCloseReceipt = GetCloseReceiptByte();
            response.Add(new ReturnValue
            {
                Id = iterator,
                Value = getCloseReceipt
            });

            return response;
        }
    }
}