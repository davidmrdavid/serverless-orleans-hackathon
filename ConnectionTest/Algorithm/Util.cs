﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConnectionTest.Algorithm
{
    public static class Util
    {
        public static void FilterQueues<K, V>(SortedDictionary<K, Queue<V>> queues, Func<V, bool> predicate)
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

        public static List<T> FilterList<T>(List<T> list, Func<T, bool> predicate)
        {
            if (list.Any(element => !predicate(element)))
            {
                return list.Where(predicate).ToList();
            }
            else
            {
                return list;
            }
        }

        public static Queue<T> FilterQueue<T>(Queue<T> queue, Func<T, bool> predicate)
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
                }
                return newQueue;
            }
            else
            {
                return queue;
            }
        }

        public static void FilterDictionary<K, V>(Dictionary<K, V> dictionary, Func<V, bool> predicate)
        {
            List<K> toRemove = null;
            foreach (var kvp in dictionary)
            {
                if (!predicate(kvp.Value))
                {
                    (toRemove ?? (toRemove = new List<K>())).Add(kvp.Key);
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