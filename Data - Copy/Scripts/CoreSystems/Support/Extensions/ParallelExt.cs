using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ParallelTasks;
using VRage.Game.ModAPI;

namespace CoreSystems.Support // Equinox's Parallel Extensions, all credit to him.
{
    public static class ParallelExtensions
    {
        public static Task ForEachNonBlocking<T>(this IMyParallelTask parallel, IEnumerable<T> enumerable,
            Action<T> act)
        {
            var work = ForEachWork<T>.Get();
            work.Prepare(act, enumerable.GetEnumerator());
            return parallel.Start(work);
        }

        public static Task ForNonBlocking(this IMyParallelTask parallel, int minInclusive, int maxExclusive,
            Action<int> act, int stride = 1)
        {
            var work = ForLoopWork.Get();
            work.Prepare(minInclusive, maxExclusive, stride, act);
            return parallel.Start(work);
        }

        private class ForEachWork<T> : IWork
        {
            public static ForEachWork<T> Get()
            {
                var res = EqObjectPool<ForEachWork<T>>.Singleton.BorrowUntracked();
                res._returned = false;
                return res;
            }

            public WorkOptions Options { get; private set; }

            public ForEachWork()
            {
                Options = new WorkOptions
                {
                    MaximumThreads = int.MaxValue
                };
            }

            public void Prepare(Action<T> act, IEnumerator<T> items)
            {
                _action = act;
                _enumerator = items;
                _notDone = true;
            }

            public void DoWork(WorkData workData = null)
            {
                T obj = default(T);
                while (_notDone)
                {
                    lock (this)
                    {
                        _notDone = _enumerator.MoveNext();
                        if (!_notDone)
                            break;
                        obj = _enumerator.Current;
                    }
                    _action(obj);
                }

                lock (this)
                {
                    if (!_returned)
                        Return();
                }
            }

            private void Return()
            {
                _enumerator?.Dispose();
                _enumerator = null;
                _action = null;
                _returned = true;
                EqObjectPool<ForEachWork<T>>.Singleton.ReturnUntracked(this);
            }

            private Action<T> _action;
            private IEnumerator<T> _enumerator;
            private volatile bool _notDone;
            private volatile bool _returned;
        }

        private class ForLoopWork : IWork
        {
            public static ForLoopWork Get()
            {
                var res = EqObjectPool<ForLoopWork>.Singleton.BorrowUntracked();
                res._returned = false;
                return res;
            }

            public WorkOptions Options { get; private set; }

            public ForLoopWork()
            {
                Options = new WorkOptions
                {
                    MaximumThreads = int.MaxValue
                };
            }

            private int _index;
            private int _max, _stride;
            private Action<int> _action;
            private bool _returned;


            public void Prepare(int min, int max, int stride, Action<int> action)
            {
                _index = min;
                _max = max;
                _stride = Math.Max(1, stride);
                _action = action;
            }

            public void DoWork(WorkData workData = null)
            {
                while (_index < _max)
                {
                    int exec;
                    lock (this)
                    {
                        exec = _index;
                        _index += _stride;
                    }

                    if (exec < _max)
                        _action(exec);
                }

                lock (this)
                {
                    if (!_returned)
                        Return();
                }
            }

            private void Return()
            {
                _returned = true;
                _action = null;
                EqObjectPool<ForLoopWork>.Singleton.ReturnUntracked(this);
            }
        }

        public class EqObjectPool<T> where T : class, new()
        {
            private static EqObjectPool<T> _singleton;

            public static EqObjectPool<T> Singleton => _singleton ?? (_singleton = new EqObjectPool<T>(5));

            private readonly ConcurrentQueue<T> _cache = new ConcurrentQueue<T>();
            private readonly int _limit;


            public EqObjectPool(int limit)
            {
                _limit = limit;
            }

            public T BorrowUntracked()
            {
                T res;
                return _cache.TryDequeue(out res) ? res : new T();
            }

            public void ReturnUntracked(T v)
            {
                if (_cache.Count > _limit)
                    return;
                _cache.Enqueue(v);
            }

            public Token BorrowTracked()
            {
                return new Token(this);
            }

            public struct Token : IDisposable
            {
                public T Value { get; private set; }

                private readonly EqObjectPool<T> _pool;

                public Token(EqObjectPool<T> pool)
                {
                    _pool = pool;
                    Value = _pool.BorrowUntracked();
                }

                public static implicit operator T(Token t)
                {
                    return t.Value;
                }

                public void Dispose()
                {
                    _pool.ReturnUntracked(Value);
                    Value = null;
                }
            }
        }
    }
}