using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_BLL.Loggers
{
    public class ServiceLogger
    {
        public void WriteLog(string message, Guid userId, Guid? entityId = null)
        {
            //todo
            Console.WriteLine($"Message: {message}, UserId: {userId}, EntityId: {entityId}");
        }
        public void WriteError(string message, Exception ex)
        {
            //todo
            Console.WriteLine($"Error: {message}, Exception: {ex.Message}");
        }
    }
}
