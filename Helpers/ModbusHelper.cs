using IoTGateway.Models;

namespace IoTGateway.Helpers
{
    public static class ModbusHelper
    {
        public static (string address, byte stationNumber, byte functionCode) ParseModbusAddress(string input, DataType dataType, bool isWrite = false)
        {
            string address = input;
            byte stationNumber = 1;
            byte functionCode = 3; // Default for Read

            if (isWrite)
            {
                if (dataType == DataType.Bool || dataType == DataType.Coil)
                {
                    functionCode = 5; // Write Single Coil
                }
                else
                {
                    functionCode = 16; // Write Multiple Registers
                }
            }
            else
            {
                if (dataType == DataType.Bool || dataType == DataType.Coil)
                {
                    functionCode = 1; // Read Coils
                }
                else if (dataType == DataType.Discrete)
                {
                    functionCode = 2; // Read Discrete Inputs
                }
                else
                {
                    functionCode = 3; // Read Holding Registers
                }
            }

            if (input.Contains(","))
            {
                var parts = input.Split(',');
                address = parts[0].Trim();
                if (parts.Length > 1 && byte.TryParse(parts[1].Trim(), out byte sn))
                {
                    stationNumber = sn;
                }
                if (parts.Length > 2 && byte.TryParse(parts[2].Trim(), out byte fc))
                {
                    functionCode = fc;
                }
            }
            else if (input.Contains(";"))
            {
                var parts = input.Split(';');
                address = parts[0].Trim();
                if (parts.Length > 1 && byte.TryParse(parts[1].Trim(), out byte sn))
                {
                    stationNumber = sn;
                }
                if (parts.Length > 2 && byte.TryParse(parts[2].Trim(), out byte fc))
                {
                    functionCode = fc;
                }
            }
            else
            {
                // Infer function code from PLC address if it's 5 or 6 digits
                // e.g. 40001, 30001, 10001, 00001
                if ((input.Length == 5 || input.Length == 6) && char.IsDigit(input[0]))
                {
                    char firstDigit = input[0];
                    if (!isWrite)
                    {
                        if (firstDigit == '0') functionCode = 1; // Coil
                        else if (firstDigit == '1') functionCode = 2; // Discrete Input
                        else if (firstDigit == '3') functionCode = 4; // Input Register
                        else if (firstDigit == '4') functionCode = 3; // Holding Register
                    }
                    else
                    {
                        if (firstDigit == '0') functionCode = 5; // Write Single Coil
                        else if (firstDigit == '4') functionCode = 16; // Write Multiple Registers
                    }
                }
            }

            return (address, stationNumber, functionCode);
        }
    }
}
