using System;
using System.Linq;
using System.Reflection;

namespace TDC.Tools.ProjectTimer.Tools
{
    public sealed class Singleton<T> where T : class, new()
    {
        private static readonly Lazy<T> instance = new Lazy<T>(() => new T());

        public static T Instance => instance.Value;

        private Singleton() { }
    }
}