// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OrleansConnector.Algorithm
{
    public static class Util
    {
        public static void FilterQueues<K, V>(SortedDictionary<K, Queue<V>> queues, Func<V, bool> predicate, Action<K,V> action = null)
        {
            List<K> toRemove = null;
            var keep = new List<V>();
            foreach (var kvp in queues)
            {
                if (kvp.Value.Any(element => !predicate(element)))
                {
                    while (kvp.Value.TryDequeue(out V element))
                    {
                        if (predicate(element))
                        {
                            keep.Add(element);
                        }
                        else
                        {
                            action?.Invoke(kvp.Key, element);
                        }
                    }
                    if (keep.Count == 0)
                    {
                        (toRemove ?? (toRemove = new List<K>())).Add(kvp.Key);
                    }
                    else
                    {
                        foreach (var element in keep)
                        {
                            kvp.Value.Enqueue(element);
                        }
                    }
                    keep.Clear();
                }
            }
            if (toRemove != null)
            {
                foreach (var k in toRemove)
                {
                    queues.Remove(k);
                }
            }
        }

        public static List<T> FilterList<T>(List<T> list, Func<T, bool> predicate, Action<T> action = null)
        {
            if (list.Any(element => !predicate(element)))
            {
                var newlist = new List<T>();
                foreach(var element in list)
                {
                    if (predicate(element))
                    {
                        newlist.Add(element);
                    }
                    else if (action != null)
                    {
                        action(element);
                    }
                }
                return newlist;
            }
            else
            {
                return list;
            }
        }

        public static Queue<T> FilterQueue<T>(Queue<T> queue, Func<T, bool> predicate, Action<T> action = null)
        {
            if (queue.Any(element => !predicate(element)))
            {
                var newQueue = new Queue<T>();
                while (queue.TryDequeue(out T element))
                {
                    if (predicate(element))
                    {
                        newQueue.Enqueue(element);
                    }
                    else if (action != null)
                    {
                        action(element);
                    }
                }
                return newQueue;
            }
            else
            {
                return queue;
            }
        }

        public static void FilterDictionary<K, V>(Dictionary<K, V> dictionary, Func<V, bool> predicate, Action<K,V> action = null)
        {
            List<K> toRemove = null;
            foreach (var kvp in dictionary)
            {
                if (!predicate(kvp.Value))
                {
                    (toRemove ?? (toRemove = new List<K>())).Add(kvp.Key);

                    if (action != null)
                    {
                        action(kvp.Key, kvp.Value);
                    }
                }
            }
            if (toRemove != null)
            {
                foreach (var k in toRemove)
                {
                    dictionary.Remove(k);
                }
            }
        }
    }
}
