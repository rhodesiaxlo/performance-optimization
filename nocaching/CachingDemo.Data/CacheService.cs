﻿using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace CachingDemo.Data
{
    public class CacheService
    {
        private static ConnectionMultiplexer connection;

        private static double defaultExpirationTimeInMinutes = 5.0d;

        private static ConnectionMultiplexer Connection
        {
            get
            {
                if ((connection == null) || (!connection.IsConnected))
                {
                    connection = ConnectionMultiplexer.Connect(CloudConfigurationManager.GetSetting("RedisConfiguration"));
                }

                return connection;
            }
        }

        public static async Task<T> GetAsync<T>(string key, Func<Task<T>> loadCache)
        {
            return await GetAsync<T>(key, loadCache, defaultExpirationTimeInMinutes)
                .ConfigureAwait(false);
        }

        public static async Task<T> GetAsync<T>(string key, Func<Task<T>> loadCache, double expirationTimeInMinutes)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key cannot be null, empty, or only whitespace.");
            }

            IDatabase cache = Connection.GetDatabase();
            T value = await GetAsync<T>(cache, key)
                .ConfigureAwait(false);
            if (value == null)
            {
                value = await loadCache()
                    .ConfigureAwait(false);
                if (value != null)
                {
                    await SetAsync(cache, key, value, expirationTimeInMinutes)
                        .ConfigureAwait(false);
                }
            }

            return value;
        }

        public static async Task FlushAsync()
        {
            // In order to flush all, we need to be in admin mode.
            var options = ConfigurationOptions.Parse(CloudConfigurationManager.GetSetting("RedisConfiguration"));
            options.AllowAdmin = true;
            using (var adminConnection = ConnectionMultiplexer.Connect(options))
            {
                foreach (var redisEndPoint in adminConnection.GetEndPoints(true))
                {
                    IServer server = adminConnection.GetServer(redisEndPoint);
                    await server.FlushAllDatabasesAsync();
                }

                await adminConnection.CloseAsync(true);
            }
        }

        private static async Task<T> GetAsync<T>(IDatabase cache, string key)
        {
            return Deserialize<T>(await cache.StringGetAsync(key).ConfigureAwait(false));
        }

        private static async Task SetAsync(IDatabase cache, string key, object value, double expirationTimeInMinutes)
        {
            await cache.StringSetAsync(key, Serialize(value))
                .ConfigureAwait(false);
            // We will default to a five minute expiration
            await cache.KeyExpireAsync(key, TimeSpan.FromMinutes(expirationTimeInMinutes));
        }

        private static string Serialize(object o)
        {
            if (o == null)
            {
                return null;
            }

            return JsonConvert.SerializeObject(o);
        }

        private static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
