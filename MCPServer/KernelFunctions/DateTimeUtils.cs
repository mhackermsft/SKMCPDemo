using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPServer.KernelFunctions
{
    internal class DateTimeUtils
    {
        [KernelFunction, Description("Retrieves the current date time in UTC.")]
        public static string GetCurrentDateTimeInUtc()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
