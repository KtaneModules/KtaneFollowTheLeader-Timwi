using System;
using System.Collections.Generic;
using System.Linq;

namespace FollowTheLeaderNS
{
    public static class ExtensionMethods
    {
        public static double sin(double x) { return Math.Sin(x * Math.PI / 180); }
        public static double cos(double x) { return Math.Cos(x * Math.PI / 180); }

        public static Pt[] MoveX(this Pt[] face, double x) { return face.Select(p => p.Add(x: x)).ToArray(); }
        public static Pt[] MoveY(this Pt[] face, double y) { return face.Select(p => p.Add(y: y)).ToArray(); }
        public static Pt[] MoveZ(this Pt[] face, double z) { return face.Select(p => p.Add(z: z)).ToArray(); }
        public static Pt[] Move(this Pt[] face, Pt by) { return face.Select(p => p + by).ToArray(); }

        public static IEnumerable<Pt> MoveX(this IEnumerable<Pt> face, double x) { return face.Select(p => p.Add(x: x)); }
        public static IEnumerable<Pt> MoveY(this IEnumerable<Pt> face, double y) { return face.Select(p => p.Add(y: y)); }
        public static IEnumerable<Pt> MoveZ(this IEnumerable<Pt> face, double z) { return face.Select(p => p.Add(z: z)); }
        public static IEnumerable<Pt> Move(this IEnumerable<Pt> face, Pt by) { return face.Select(p => p + by); }

        public static Pt[][] MoveX(this Pt[][] faces, double x) { return faces.Select(face => MoveX(face, x)).ToArray(); }
        public static Pt[][] MoveY(this Pt[][] faces, double y) { return faces.Select(face => MoveY(face, y)).ToArray(); }
        public static Pt[][] MoveZ(this Pt[][] faces, double z) { return faces.Select(face => MoveZ(face, z)).ToArray(); }
        public static Pt[][] Move(this Pt[][] faces, Pt by) { return faces.Select(face => Move(face, by)).ToArray(); }

        public static IEnumerable<Pt[]> MoveX(this IEnumerable<Pt[]> faces, double x) { return faces.Select(face => MoveX(face, x)); }
        public static IEnumerable<Pt[]> MoveY(this IEnumerable<Pt[]> faces, double y) { return faces.Select(face => MoveY(face, y)); }
        public static IEnumerable<Pt[]> MoveZ(this IEnumerable<Pt[]> faces, double z) { return faces.Select(face => MoveZ(face, z)); }
        public static IEnumerable<Pt[]> Move(this IEnumerable<Pt[]> faces, Pt by) { return faces.Select(face => Move(face, by)); }

        public static Pt RotateX(this Pt p, double angle) { return new Pt(p.X, p.Y * cos(angle) - p.Z * sin(angle), p.Y * sin(angle) + p.Z * cos(angle)); }
        public static Pt RotateY(this Pt p, double angle) { return new Pt(p.X * cos(angle) - p.Z * sin(angle), p.Y, p.X * sin(angle) + p.Z * cos(angle)); }
        public static Pt RotateZ(this Pt p, double angle) { return new Pt(p.X * cos(angle) - p.Y * sin(angle), p.X * sin(angle) + p.Y * cos(angle), p.Z); }

        public static Pt[] RotateX(this Pt[] face, double angle) { return face.Select(p => RotateX(p, angle)).ToArray(); }
        public static Pt[] RotateY(this Pt[] face, double angle) { return face.Select(p => RotateY(p, angle)).ToArray(); }
        public static Pt[] RotateZ(this Pt[] face, double angle) { return face.Select(p => RotateZ(p, angle)).ToArray(); }

        public static IEnumerable<Pt> RotateX(this IEnumerable<Pt> face, double angle) { return face.Select(p => RotateX(p, angle)); }
        public static IEnumerable<Pt> RotateY(this IEnumerable<Pt> face, double angle) { return face.Select(p => RotateY(p, angle)); }
        public static IEnumerable<Pt> RotateZ(this IEnumerable<Pt> face, double angle) { return face.Select(p => RotateZ(p, angle)); }

        public static Pt[][] RotateX(this Pt[][] faces, double angle) { return faces.Select(face => RotateX(face, angle)).ToArray(); }
        public static Pt[][] RotateY(this Pt[][] faces, double angle) { return faces.Select(face => RotateY(face, angle)).ToArray(); }
        public static Pt[][] RotateZ(this Pt[][] faces, double angle) { return faces.Select(face => RotateZ(face, angle)).ToArray(); }

        public static IEnumerable<Pt[]> RotateX(this IEnumerable<Pt[]> faces, double angle) { return faces.Select(face => RotateX(face, angle)); }
        public static IEnumerable<Pt[]> RotateY(this IEnumerable<Pt[]> faces, double angle) { return faces.Select(face => RotateY(face, angle)); }
        public static IEnumerable<Pt[]> RotateZ(this IEnumerable<Pt[]> faces, double angle) { return faces.Select(face => RotateZ(face, angle)); }

        public static IEnumerable<TResult> SelectConsecutivePairs<T, TResult>(this IEnumerable<T> source, bool closed, Func<T, T, TResult> selector)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (selector == null)
                throw new ArgumentNullException("selector");
            return selectConsecutivePairsIterator(source, closed, selector);
        }
        private static IEnumerable<TResult> selectConsecutivePairsIterator<T, TResult>(IEnumerable<T> source, bool closed, Func<T, T, TResult> selector)
        {
            using (var enumer = source.GetEnumerator())
            {
                bool any = enumer.MoveNext();
                if (!any)
                    yield break;
                T first = enumer.Current;
                T last = enumer.Current;
                while (enumer.MoveNext())
                {
                    yield return selector(last, enumer.Current);
                    last = enumer.Current;
                }
                if (closed)
                    yield return selector(last, first);
            }
        }

        public static IEnumerable<TResult> SelectManyConsecutivePairs<T, TResult>(this IEnumerable<T> source, bool closed, Func<T, T, IEnumerable<TResult>> selector)
        {
            return source.SelectConsecutivePairs(closed, selector).SelectMany(x => x);
        }

        public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (predicate == null)
                throw new ArgumentNullException("predicate");
            int index = 0;
            foreach (var v in source)
            {
                if (predicate(v))
                    return index;
                index++;
            }
            return -1;
        }

        public static T? FirstOrNull<T>(this IEnumerable<T> source) where T : struct
        {
            if (source == null)
                throw new ArgumentNullException("source");
            using (var e = source.GetEnumerator())
            {
                if (e.MoveNext())
                    return e.Current;
                return null;
            }
        }

        public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source, int count, bool throwIfNotEnough = false)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "count cannot be negative.");
            if (count == 0)
                return source;

            var collection = source as ICollection<T>;
            if (collection != null)
            {
                if (throwIfNotEnough && collection.Count < count)
                    throw new InvalidOperationException("The collection does not contain enough elements.");
                return collection.Take(Math.Max(0, collection.Count - count));
            }

            return skipLastIterator(source, count, throwIfNotEnough);
        }

        private static IEnumerable<T> skipLastIterator<T>(IEnumerable<T> source, int count, bool throwIfNotEnough)
        {
            var queue = new T[count];
            int headtail = 0; // tail while we're still collecting, both head & tail afterwards because the queue becomes completely full
            int collected = 0;

            foreach (var item in source)
            {
                if (collected < count)
                {
                    queue[headtail] = item;
                    headtail++;
                    collected++;
                }
                else
                {
                    if (headtail == count)
                        headtail = 0;
                    yield return queue[headtail];
                    queue[headtail] = item;
                    headtail++;
                }
            }

            if (throwIfNotEnough && collected < count)
                throw new InvalidOperationException("The collection does not contain enough elements.");
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "count cannot be negative.");
            if (count == 0)
                return new T[0];

            var collection = source as ICollection<T>;
            if (collection != null)
                return collection.Skip(Math.Max(0, collection.Count - count));

            var queue = new Queue<T>(count + 1);
            foreach (var item in source)
            {
                if (queue.Count == count)
                    queue.Dequeue();
                queue.Enqueue(item);
            }
            return queue.AsEnumerable();
        }
    }
}
