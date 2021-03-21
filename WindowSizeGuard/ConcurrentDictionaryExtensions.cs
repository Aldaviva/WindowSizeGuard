using System;
using System.Collections.Concurrent;
using System.Threading;

#nullable enable

namespace WindowSizeGuard {

    public static class ConcurrentDictionaryExtensions {

        public static ConcurrentDictionary<K, ValueHolder<V>> createConcurrentDictionary<K, V>() {
            return new();
        }

        public static V exchangeEnum<K, V>(this ConcurrentDictionary<K, ValueHolder<int>> dictionary, K key, V newValue) where V: Enum {
            int newValueInt = (int) Convert.ChangeType(newValue, newValue.GetTypeCode());
            return (V) Enum.ToObject(typeof(V), Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<int>(newValueInt)).value, newValueInt));
        }

        public static V exchange<K, V>(this ConcurrentDictionary<K, ValueHolder<V>> dictionary, K key, V newValue) where V: class {
            return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<V>(newValue)).value, newValue);
        }

        public static long exchange<K>(this ConcurrentDictionary<K, ValueHolder<long>> dictionary, K key, long newValue) {
            return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<long>(newValue)).value, newValue);
        }

        public static int exchange<K>(this ConcurrentDictionary<K, ValueHolder<int>> dictionary, K key, int newValue) {
            return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<int>(newValue)).value, newValue);
        }

        public static double exchange<K>(this ConcurrentDictionary<K, ValueHolder<double>> dictionary, K key, double newValue) {
            return Interlocked.Exchange(ref dictionary.GetOrAdd(key, new ValueHolder<double>(newValue)).value, newValue);
        }

    }

    public class ValueHolder<T> {

        public T value;

        public ValueHolder(T value) {
            this.value = value;
        }

    }

}