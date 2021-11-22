﻿/* 
 * Copyright (c) 2020, Norsk Helsenett SF and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the MIT license
 * available at https://raw.githubusercontent.com/helsenorge/Helsenorge.Messaging/master/LICENSE
 */

using Amqp;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Helsenorge.Messaging.ServiceBus
{
    /// <summary>
    /// Represents the connection to a Message Broker.
    /// </summary>
    public class ServiceBusConnection
    {
        private ConnectionFactory _connectionFactory;
        private readonly Address _address;
        private IConnection _connection;

        /// <summary>Initializes a new instance of the <see cref="ServiceBusConnection" /> class with the givem connection string and a <see cref="ILogger"/> object.</summary>
        /// <param name="connectionString">The connection used to connect to Message Broker.</param>
        /// <param name="logger">A <see cref="ILogger{LinkFactory}"/> which will be used to log errors and information.</param>
        public ServiceBusConnection(string connectionString, ILogger logger)
            : this(connectionString, ServiceBusSettings.DefaultMaxLinksPerSession, ServiceBusSettings.DefaultMaxSessions, logger)
        {

        }

        /// <summary>Initializes a new instance of the <see cref="ServiceBusConnection" /> class with the givem connection string and a <see cref="ILogger"/> object.</summary>
        /// <param name="connectionString">The connection used to connect to Message Broker.</param>
        /// <param name="maxLinksPerSession">The max links that will be allowed per session.</param>
        /// <param name="maxSessionsPerConnection">The max sessions that will be allowed per connection.</param>
        /// <param name="logger">A <see cref="ILogger{LinkFactory}"/> which will be used to log errors and information.</param>
        /// <exception cref="ArgumentException" />
        /// <exception cref="ArgumentNullException" />
        public ServiceBusConnection(string connectionString, int maxLinksPerSession, ushort maxSessionsPerConnection, ILogger logger)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(nameof(connectionString));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _address = new Address(connectionString);
            HttpClient = new ServiceBusHttpClient(_address, logger);

            if (!string.IsNullOrEmpty(_address.Path))
            {
                Namespace = _address.Path.Substring(1).TrimEnd('/');
                _address = new Address(connectionString.Substring(0, connectionString.Length - Namespace.Length));
            }

            _connectionFactory = new ConnectionFactory();
            if (maxLinksPerSession != ServiceBusSettings.DefaultMaxLinksPerSession)
            {
                _connectionFactory.AMQP.MaxLinksPerSession = maxLinksPerSession;
            }
            if (maxSessionsPerConnection != ServiceBusSettings.DefaultMaxSessions)
            {
                _connectionFactory.AMQP.MaxSessionsPerConnection = maxSessionsPerConnection;
            }
        }

        /// <summary>
        /// Returns the NameSpace part of the connection string.
        /// </summary>
        /// <remarks>This only works properly for Microsoft Service Bus.</remarks>
        public string Namespace { get; }

        internal ServiceBusHttpClient HttpClient { get; }

        internal IConnection GetConnection()
        {
            EnsureConnection();
            return _connection;
        }

        internal async Task<IConnection> GetConnectionAsync()
        {
            await EnsureConnectionAsync().ConfigureAwait(false);
            return _connection;
        }

        /// <summary>
        /// Ensures we are connected and will reconnect if we have been disconnected for externally reasons.
        /// Will auto-reconnect until Close() is called explicitly.
        /// </summary>
        /// <returns>Returns true if it is connected or has reconnected, otherwise returns false.</returns>
        public bool EnsureConnection()
        {
            if (IsClosedOrClosing)
            {
                throw new ObjectDisposedException("Connection is closed");
            }
            if (_connection == null || _connection.IsClosed)
            {
                _connection = _connectionFactory.CreateAsync(_address).GetAwaiter().GetResult();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ensures we are connected and will reconnect if we have been disconnected for externally reasons.
        /// Will auto-reconnect until Close() is called explicitly.
        /// </summary>
        /// <returns>Returns true if it is connected or has reconnected, otherwise returns false.</returns>
        public async Task<bool> EnsureConnectionAsync()
        {
            if (IsClosedOrClosing)
            {
                throw new ObjectDisposedException("Connection is closed");
            }
            if (_connection == null || _connection.IsClosed)
            {
                _connection = await _connectionFactory.CreateAsync(_address).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the connection is closing or has been closed, otherwise false.
        /// </summary>
        public bool IsClosedOrClosing { get; private set; }

        /// <summary>
        /// Closes the connection to the Message Broker. This is the preferred method of closing any open connection.
        /// </summary>
        public async Task CloseAsync()
        {
            if (IsClosedOrClosing)
            {
                return;
            }
            IsClosedOrClosing = true;
            if (_connection != null && !_connection.IsClosed)
            {
                await _connection.CloseAsync().ConfigureAwait(false);
            }
        }

        internal string GetEntityName(string id)
        {
            return GetEntityName(id, Namespace);
        }

        internal static string GetEntityName(string id, string ns)
        {
            return !string.IsNullOrEmpty(ns) ? $"{ns}/{id}" : id;
        }
    }
}
