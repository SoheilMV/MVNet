using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVNet
{
    internal static class ExceptionHelper
    {
        internal static ArgumentException EmptyString(string paramName)
        {
            return new ArgumentException(Constants.ArgumentException_EmptyString, paramName);
        }

        internal static ArgumentOutOfRangeException CanNotBeLess<T>(string paramName, T value) where T : struct
        {
            return new ArgumentOutOfRangeException(paramName, string.Format(Constants.ArgumentOutOfRangeException_CanNotBeLess, value));
        }

        internal static ArgumentOutOfRangeException CanNotBeGreater<T>(string paramName, T value) where T : struct
        {
            return new ArgumentOutOfRangeException(paramName, string.Format(Constants.ArgumentOutOfRangeException_CanNotBeGreater, value));
        }

        internal static ArgumentException WrongPath(string paramName, Exception innerException = null)
        {
            return new ArgumentException(Constants.ArgumentException_WrongPath, paramName, innerException);
        }

        internal static ArgumentOutOfRangeException WrongTcpPort(string paramName)
        {
            return new ArgumentOutOfRangeException(paramName, string.Format(Constants.ArgumentOutOfRangeException_CanNotBeLessOrGreater, 1, 65535));
        }

        internal static bool ValidateTcpPort(int port) => port >= 1 && port <= 65535;
    }
}
