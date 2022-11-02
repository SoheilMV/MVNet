﻿namespace MVNet
{
    internal class Constants
    {
        public const string ArgumentException_CanNotReadOrSeek = "Thread can not read or seek";
        public const string ArgumentException_EmptyString = "Value has not been empty string";
        public const string ArgumentException_HttpRequest_SetNotAvailableHeader = "Value of '{0}' set not available header";
        public const string ArgumentException_MultiThreading_BegIndexRangeMoreEndIndex = "Begin Index range more end index";
        public const string ArgumentException_OnlyAbsoluteUri = "Acceptable use only the absolute URI.";
        public const string ArgumentException_WrongPath = "The path is an empty string, contains only white space, or contains invalid characters.";
        public const string ArgumentOutOfRangeException_CanNotBeGreater = "The value can not be more than {0}.";
        public const string ArgumentOutOfRangeException_CanNotBeLess = "The value can not be less {0}.";
        public const string ArgumentOutOfRangeException_CanNotBeLessOrGreater = "The value can not be less than {0} or {1} longer.";
        public const string ArgumentOutOfRangeException_StringHelper_MoreLengthString = "The starting position can not be more than the length of the string.";
        public const string ArgumentOutOfRangeException_StringLengthCanNotBeMore = "String length must not be more than {0} characters.";
        public const string CookieStorage_SaveToFile_FileAlreadyExists = "Cookies file '${0}' already exists.";
        public const string DirectoryNotFoundException_DirectoryNotFound = "he path points to a nonexistent directory {0}.";
        public const string FormatException_ProxyClient_WrongPort = "Invalid port specified.";
        public const string HttpException_ClientError = "The error on the client side. Status code: {0}";
        public const string HttpException_ConnectTimeout = "It turned out wait for a connection to the HTTP-server '{0}'.";
        public const string HttpException_Default = "An error when handling HTTP protocol.";
        public const string HttpException_FailedConnect = "Unable to connect to the HTTP-server '{0}'.";
        public const string HttpException_FailedGetHostAddresses = "Failed to get the host IP-address '{0}'.";
        public const string HttpException_FailedReceiveMessageBody = "Could not receive the message body of the response HTTP-server '{0}'.";
        public const string HttpException_FailedReceiveResponse = "Failed to receive the response from the HTTP-server '{0}'.";
        public const string HttpException_FailedSendRequest = "Failed to send HTTP-request to the server '{0}'.";
        public const string HttpException_FailedSslConnect = "Unable to establish SSL-connection with HTTP-server '{0}'.";
        public const string HttpException_LimitRedirections = "Have exceeded the maximum number of consecutive redirects.";
        public const string HttpException_ReceivedEmptyResponse = "Received empty response from the HTTP-server '{0}'.";
        public const string HttpException_ReceivedWrongResponse = "Received an invalid response from the HTTP-server '{0}'.";
        public const string HttpException_SeverError = "The error on the server side. Status code: {0}";
        public const string HttpException_WaitDataTimeout = "It turned out the wait time data from the HTTP-server '{0}'.";
        public const string HttpException_WrongChunkedBlockLength = "Received invalid data block size when using Chunked: {0}";
        public const string HttpException_WrongCookie = "Received invalid cookies '{0}' from the HTTP-server '{1}'.";
        public const string HttpException_WrongHeader = "Received invalid header '{0}' from the HTTP-server '{1}'.";
        public const string InvalidOperationException_HttpResponse_HasError = "Unable to perform the method, because an error occurred while receiving a response.";
        public const string InvalidOperationException_NotSupportedEncodingFormat = "Received an unsupported encoding format: {0}";
        public const string InvalidOperationException_ProxyClient_WrongHost = "The host may be uncertain or have zero length.";
        public const string InvalidOperationException_ProxyClient_WrongPassword = "The password can not be more than 255 characters.";
        public const string InvalidOperationException_ProxyClient_WrongPort = "The port can not be less than 1 or greater than 65535.";
        public const string InvalidOperationException_ProxyClient_WrongUsername = "User name can not be more than 255 characters.";
        public const string NetException_Default = "An error occurred while working with the network.";
        public const string ProxyException_CommandError = "{0} The proxy server '{1}'.";
        public const string ProxyException_ConnectTimeout = "It turned out the wait time to connect to the proxy server '{0}'.";
        public const string ProxyException_Default = "An error occurred while working with the proxy.";
        public const string ProxyException_Error = "An error occurred while working with the proxy server '{0}'.";
        public const string ProxyException_FailedConnect = "Unable to connect to the proxy server '{0}'.";
        public const string ProxyException_FailedGetHostAddresses = "Failed to get the host IP-address '{0}'.";
        public const string ProxyException_NotSupportedAddressType = "The host '{0}' type '{1}' does not support the type Address. The following types: InterNetwork and InterNetworkV6. The proxy server '{2}'.";
        public const string ProxyException_ReceivedEmptyResponse = "Received empty response from the proxy server '{0}'.";
        public const string ProxyException_ReceivedWrongResponse = "Received an invalid response from the proxy server '{0}'.";
        public const string ProxyException_ReceivedWrongStatusCode = "Received invalid status code '{0}' on '{1}' proxy.";
        public const string ProxyException_Socks5_FailedAuthOn = "Failed to authenticate with the proxy server '{0}'.";
        public const string ProxyException_WaitDataTimeout = "It turned out the wait time data from the proxy server '{0}'.";
        public const string Socks4_CommandReplyRequestRejectedCannotConnectToIdentd = "The request failed, because things are not running idents (or not available from the server).";
        public const string Socks4_CommandReplyRequestRejectedDifferentIdentd = "The request failed because client's idents could not confirm the user ID in the query.";
        public const string Socks4_CommandReplyRequestRejectedOrFailed = "Query rejected or erroneous.";
        public const string Socks5_AuthMethodReplyNoAcceptableMethods = "The proposed authentication methods are supported.";
        public const string Socks5_CommandReplyAddressTypeNotSupported = "Address type not supported.";
        public const string Socks5_CommandReplyCommandNotSupported = "The command is not supported or protocol error.";
        public const string Socks5_CommandReplyConnectionNotAllowedByRuleset = "Connecting a set of rules is prohibited.";
        public const string Socks5_CommandReplyConnectionRefused = "Connection refused.";
        public const string Socks5_CommandReplyGeneralSocksServerFailure = "Error SOCKS-server.";
        public const string Socks5_CommandReplyHostUnreachable = "Host unreachable.";
        public const string Socks5_CommandReplyNetworkUnreachable = "The network is not available.";
        public const string Socks5_CommandReplyTTLExpired = "Expired TTL.";
        public const string Azadi_EmptySecret = "The secret cannot be empty.";
        public const string Azadi_FormatIsIncorrect = "The proxy format is incorrect.";
        public const string Azadi_CommandReplyAuthWrong = "Invalid username or password.";
        public const string Azadi_CommandReplyHostIncorrect = "The host entered is incorrect.";
        public const string Azadi_CommandReplyConnectionRefused = "Connection refused.";
        public const string StringExtensions_Substrings_Invalid_Index = "Invalid start index for substrings.";
        public const string UnknownError = "Unknown error.";
    }
}
