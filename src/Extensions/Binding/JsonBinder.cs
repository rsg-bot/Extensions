﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Rocket.Surgery.Binding
{
    /// <summary>
    /// Class JsonBinder.
    /// </summary>
    /// TODO Edit XML Comment Template for JsonBinder
    public class JsonBinder : IJsonBinder
    {
        internal static readonly JsonSerializer DefaultSerializer = JsonSerializer.CreateDefault(new JsonSerializerSettings { ContractResolver = new PrivateSetterContractResolver() });
        private readonly JsonSerializer _serializer;
        private readonly string[] _separator;

        /// <inheritdoc />
        public JsonBinder() : this(":", DefaultSerializer) { }

        /// <inheritdoc />
        public JsonBinder(char separator) : this(separator.ToString(), DefaultSerializer) { }

        /// <inheritdoc />
        public JsonBinder(string separator) : this(separator, DefaultSerializer) { }

        /// <inheritdoc />
        public JsonBinder(JsonSerializerSettings settings) : this(":", settings) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBinder"/> class.
        /// </summary>
        /// <param name="separator">The separator.</param>
        /// <param name="settings">The serialization setings.</param>
        public JsonBinder(string separator, JsonSerializerSettings settings)
            : this(separator, JsonSerializer.CreateDefault(
                settings ?? new JsonSerializerSettings { ContractResolver = new PrivateSetterContractResolver() })
            )
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBinder"/> class.
        /// </summary>
        /// <param name="separator">The separator.</param>
        /// <param name="serializer">The serializer.</param>
        public JsonBinder(string separator, JsonSerializer serializer)
        {
            _serializer = serializer;
            _separator = new[] { separator };
        }

        /// <inheritdoc />
        public T Bind<T>(IEnumerable<KeyValuePair<string, string>> values)
            where T : class, new()
        {
            return Parse(values).ToObject<T>(_serializer);
        }

        /// <inheritdoc />
        public T Bind<T>(IEnumerable<KeyValuePair<string, string>> values, JsonSerializer serializer)
            where T : class, new()
        {
            return Parse(values).ToObject<T>(serializer);
        }

        /// <inheritdoc />
        public object Bind(Type objectType, IEnumerable<KeyValuePair<string, string>> values)
        {
            return Parse(values).ToObject(objectType, _serializer);
        }

        /// <inheritdoc />
        public object Bind(Type objectType, IEnumerable<KeyValuePair<string, string>> values, JsonSerializer serializer)
        {
            return Parse(values).ToObject(objectType, serializer);
        }

        /// <inheritdoc />
        public T Populate<T>(T value, IEnumerable<KeyValuePair<string, string>> values)
            where T : class
        {
            _serializer.Populate(Parse(values).CreateReader(), value);
            return value;
        }

        /// <inheritdoc />
        public T Populate<T>(T value, IEnumerable<KeyValuePair<string, string>> values, JsonSerializer serializer)
            where T : class
        {
            serializer.Populate(Parse(values).CreateReader(), value);
            return value;
        }

        /// <inheritdoc />
        public JObject Parse(IEnumerable<KeyValuePair<string, string>> values)
        {
            var result = new JObject();
            foreach (var item in values)
            {
                var keys = item.Key.Split(_separator, StringSplitOptions.RemoveEmptyEntries);
                var prop = keys.Last();
                JToken root = result;

                // This produces a simple look ahead
                var zippedKeys = keys
                    .Zip(keys.Skip(1), (prev, current) => (prev, current));

                foreach (var (key, next) in zippedKeys)
                {
                    if (int.TryParse(next, out var value))
                    {
                        root = SetValueToToken(root, key, new JArray());
                    }
                    else
                    {
                        root = SetValueToToken(root, key, new JObject());
                    }
                }

                SetValueToToken(root, prop, new JValue(item.Value));
            }
            return result;
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, JValue>> GetValues<T>(T value)
            where T : class
        {
            return JObject.FromObject(value, _serializer)
                .Descendants()
                .Where(p => !p.Any())
                .OfType<JValue>()
                .Select(item => new KeyValuePair<string, JValue>(GetKey(item), item));
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, JValue>> GetValues<T>(T value, JsonSerializer serializer)
            where T : class
        {
            return JObject.FromObject(value, serializer)
                .Descendants()
                .Where(p => !p.Any())
                .OfType<JValue>()
                .Select(item => new KeyValuePair<string, JValue>(GetKey(item), item));
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, string>> From<T>(T value)
            where T : class
        {
            return JObject.FromObject(value, _serializer)
                .Descendants()
                .Where(p => !p.Any())
                .OfType<JValue>()
                .Select(item => new KeyValuePair<string, string>(GetKey(item), item.ToString()));
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, string>> From<T>(T value, JsonSerializer serializer)
            where T : class
        {
            return JObject.FromObject(value, serializer)
                .Descendants()
                .Where(p => !p.Any())
                .OfType<JValue>()
                .Select(item => new KeyValuePair<string, string>(GetKey(item), item.ToString()));
        }

        private string GetKey(JToken token)
        {
            var items = new Stack<string>();
            while (token.Parent != null)
            {
                if (token.Parent is JArray arr)
                {
                    items.Push(arr.IndexOf(token).ToString());
                }
                if (token is JProperty p)
                {
                    items.Push(p.Name);
                }
                token = token.Parent;
            }
            return string.Join(_separator[0], items);
        }

        private T SetValueToToken<T>(JToken root, string key, T value)
            where T : JToken
        {
            var currentValue = GetValueFromToken(root, key);
            if (currentValue == null || currentValue.Type == JTokenType.Null)
            {
                if (root is JArray arr)
                {
                    if (int.TryParse(key, out var index))
                    {
                        if (arr.Count <= index)
                        {
                            while (arr.Count < index)
                                arr.Add(null);
                            arr.Add(value);
                        }
                        else
                        {
                            arr[index] = value;
                        }

                        return value;
                    }
                }
                else
                {
                    root[key] = value;
                    return value;
                }
            }

            if (root is JArray arr2 && int.TryParse(key, out var i))
            {
                return (T)arr2[i];
            }
            return (T)root[key];
        }

        private JToken GetValueFromToken(JToken root, string key)
        {
            if (root is JArray arr)
            {
                if (int.TryParse(key, out var index))
                {
                    if (arr.Count <= index) return null;
                    return arr[index];
                }
                throw new IndexOutOfRangeException(key);
            }
            return root[key];
        }
    }
}
