﻿namespace MVNet
{
    static internal class PortHelper
    {
        public static bool ValidateTcpPort(int port)
            => port >= 1 && port <= 65535;
    }
}
