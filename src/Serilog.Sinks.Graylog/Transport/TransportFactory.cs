﻿using System;
using Serilog.Sinks.Graylog.Helpers;
using Serilog.Sinks.Graylog.Transport.Http;
using Serilog.Sinks.Graylog.Transport.Udp;

namespace Serilog.Sinks.Graylog.Transport
{
    public static class TransportFactory
    {
        public static ITransport FromOptions(GraylogSinkOptions options)
        {
            switch (options.TransportType)
            {
                case TransportType.Udp:
                    return CreateUdpTransportFactory(options);
                case TransportType.Http:
                    return CreateHttpTransportFactory(options);
                default:
                    throw new ArgumentOutOfRangeException(nameof(options), options.TransportType, null);
            }
        }

        private static ITransport CreateHttpTransportFactory(GraylogSinkOptions options)
        {
            var url = new Uri($"{options.HostnameOrAddress}:{options.Port}/gelf");
            var httpTransport = new HttpTransport(url, options.HttpClientFactory);
            return httpTransport;
        }

        private static ITransport CreateUdpTransportFactory(GraylogSinkOptions options)
        {
            var chunkConverter = new DataToChunkConverter(new ChunkSettings
            {
                MessageIdGeneratorType = options.MessageGeneratorType
            }, new MessageIdGeneratorResolver());

            var udpTransport = new UdpTransport(
                chunkConverter,
                options.HostnameOrAddress,
                options.Port,
                options.UdpClientFactory
            );
            return udpTransport;
        }
    }

}
