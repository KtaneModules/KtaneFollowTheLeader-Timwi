using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FollowTheLeader
{
    public class MeshGenerator
    {
        public const double _wireRadius = .002;
        public const double _wireRadiusHighlight = .004;

        const double _hexFrameBoxOuterRadius = .003;
        const double _hexFrameBoxInnerRadius = .002;
        const double _hexFrameBoxHeight = .005;
        const double _hexFrameBoxLocationRatio = .6;

        const double _wireMaxSegmentDeviation = .003;
        const double _wireMaxBézierDeviation = .003;

        const double _firstControlHeight = .02; // was .01
        const double _interpolateHeight = .01;  // was .007
        const double _firstControlHeightHighlight = .01; // was .01
        const double _interpolateHeightHighlight = .007;  // was .007

        sealed class HexFrameInfo
        {
            public double InnerRadius, OuterRadius, Bevel, Depth, StartAngle;

            public double BoxCenterRadius { get { return InnerRadius * _hexFrameBoxLocationRatio + OuterRadius * (1 - _hexFrameBoxLocationRatio); } }
        }

        private static HexFrameInfo _outerHexFrame = new HexFrameInfo { InnerRadius = .058, OuterRadius = .070, Bevel = .003, Depth = .005, StartAngle = 0 };
        private static HexFrameInfo _innerHexFrame = new HexFrameInfo { InnerRadius = .032, OuterRadius = .044, Bevel = .003, Depth = .005, StartAngle = 30 };

        sealed class CPC { public Pt ControlBefore, Point, ControlAfter; }

        private static Color[] _colors = Ex.NewArray(
            new Color(0xEA / 255f, 0x00 / 255f, 0x00 / 255f),
            new Color(0x00 / 255f, 0xAE / 255f, 0x00 / 255f),
            new Color(0xE8 / 255f, 0xE8 / 255f, 0xE8 / 255f),
            new Color(0xEE / 255f, 0xEE / 255f, 0x15 / 255f),
            new Color(0x16 / 255f, 0x78 / 255f, 0xFF / 255f),
            new Color(0x29 / 255f, 0x29 / 255f, 0x29 / 255f)
        );

        public enum WirePiece { Uncut, Cut, Copper }

        public static Mesh GenerateWire(System.Random rnd, int lengthIndex, int color, WirePiece piece, bool highlight)
        {
            var length = getWireLength(lengthIndex);
            var thickness = highlight ? _wireRadiusHighlight : _wireRadius;
            var firstControlHeight = highlight ? _firstControlHeightHighlight : _firstControlHeight;
            var interpolateHeight = highlight ? _interpolateHeightHighlight : _interpolateHeight;

            var start = pt(0, 0, 0);
            var startControl = pt(length / 10, 0, firstControlHeight);
            var endControl = pt(length * 9 / 10, 0, firstControlHeight);
            var end = pt(length, 0, 0);
            var numSegments = new[] { 6, 4, 4 }[lengthIndex];

            var bézierSteps = 16;
            var tubeRevSteps = 16;

            var interpolateStart = pt(0, 0, interpolateHeight);
            var interpolateEnd = pt(length, 0, interpolateHeight);

            var intermediatePoints = newArray(numSegments - 1, i => interpolateStart + (interpolateEnd - interpolateStart) * (i + 1) / numSegments + pt(rnd.NextDouble() - .5, rnd.NextDouble() - .5, rnd.NextDouble() - .5) * _wireMaxSegmentDeviation);
            var deviations = newArray(numSegments - 1, _ => pt(rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()) * _wireMaxBézierDeviation);

            if (piece == WirePiece.Uncut)
            {
                var points =
                    new[] { new { ControlBefore = default(Pt), Point = start, ControlAfter = startControl } }
                    .Concat(intermediatePoints.Select((p, i) => new { ControlBefore = p - deviations[i], Point = p, ControlAfter = p + deviations[i] }))
                    .Concat(new[] { new { ControlBefore = endControl, Point = end, ControlAfter = default(Pt) } })
                    .SelectConsecutivePairs(false, (one, two) => bézier(one.Point, one.ControlAfter, two.ControlBefore, two.Point, bézierSteps))
                    .SelectMany((x, i) => i == 0 ? x : x.Skip(1))
                    .ToArray();
                return toMesh(createFaces(false, true, tubeFromCurve(points, thickness, tubeRevSteps)), _colors[color]);
            }

            var partialWire = new Func<IEnumerable<CPC>, IEnumerable<VertexInfo[]>>(pts =>
            {
                var points = pts
                    .SelectConsecutivePairs(false, (one, two) => bézier(one.Point, one.ControlAfter, two.ControlBefore, two.Point, bézierSteps))
                    .SelectMany((x, i) => i == 0 ? x : x.Skip(1))
                    .ToArray();

                var reserveForCopper = 6;
                var discardCopper = 2;

                if (piece == WirePiece.Cut)
                {
                    var tube = tubeFromCurve(points, thickness, tubeRevSteps).SkipLast(reserveForCopper).ToArray();
                    var capCenter = points[points.Length - 1 - reserveForCopper];
                    var normal = capCenter - points[points.Length - 2 - reserveForCopper];
                    var cap = tube[tube.Length - 1].SelectConsecutivePairs(true, (v1, v2) => new[] { capCenter, v2.Point, v1.Point }.Select(p => new VertexInfo { Point = p, Normal = normal }).ToArray()).ToArray();
                    return createFaces(false, true, tube).Concat(cap);
                }
                else
                {
                    var copper = tubeFromCurve(points.TakeLast(reserveForCopper + 2).SkipLast(discardCopper).ToArray(), thickness / 2, tubeRevSteps).Skip(1).ToArray();
                    var copperCapCenter = points[points.Length - 1 - discardCopper];
                    var copperNormal = copperCapCenter - points[points.Length - 2];
                    var copperCap = copper[copper.Length - 1].SelectConsecutivePairs(true, (v1, v2) => new[] { copperCapCenter, v2.Point, v1.Point }.Select(p => new VertexInfo { Point = p, Normal = copperNormal }).ToArray()).ToArray();
                    return createFaces(false, true, copper).Concat(copperCap);
                }
            });

            var rotAngle = (rnd.NextDouble() * 7 + 5) * (rnd.Next(2) == 0 ? -1 : 1);
            var rotAxisStart = new Pt(0, 0, 0);
            var rotAxisEnd = new Pt(rnd.NextDouble() * .01, rnd.NextDouble() * .01, 1);
            Func<Pt, Pt> rot = p => p.Rotate(rotAxisStart, rotAxisEnd, rotAngle);
            var beforeCut =
                new[] { new CPC { ControlBefore = default(Pt), Point = start, ControlAfter = startControl } }
                .Concat(intermediatePoints.Take(numSegments / 2).Select((p, i) => new CPC { ControlBefore = rot(p - deviations[i]), Point = rot(p), ControlAfter = rot(p + deviations[i]) }));
            var bcTube = partialWire(beforeCut);

            var cutOffPoint = (numSegments - 1) / 2;
            rotAngle = (rnd.NextDouble() * 7 + 5) * (rnd.Next(2) == 0 ? -1 : 1);
            rotAxisStart = new Pt(length, 0, 0);
            rotAxisEnd = new Pt(length + rnd.NextDouble() * .01, rnd.NextDouble() * .01, 1);
            var afterCut =
                new[] { new CPC { ControlBefore = default(Pt), Point = end, ControlAfter = endControl } }
                .Concat(intermediatePoints.Skip(cutOffPoint).Select((p, i) => new CPC { ControlBefore = rot(p + deviations[i + cutOffPoint]), Point = rot(p), ControlAfter = rot(p - deviations[i + cutOffPoint]) }).Reverse());
            var acTube = partialWire(afterCut);

            return toMesh(bcTube.Concat(acTube).ToArray(), _colors[color]);
        }

        private static void getBoxCenter(int peg, out double x, out double z)
        {
            var rad = (peg % 2 == 0 ? _outerHexFrame : _innerHexFrame).BoxCenterRadius;
            x = rad * cos(30 * (4 - peg));
            z = rad * sin(30 * (4 - peg));
        }

        public static double GetWirePosAndAngle(int peg1, int peg2, out double x, out double z)
        {
            double x2, z2;
            getBoxCenter(peg1, out x, out z);
            getBoxCenter(peg2, out x2, out z2);
            return Math.Atan2(z2 - z, x2 - x) * 180 / Math.PI;
        }

        sealed class VertexInfo
        {
            public Pt Point;
            public Pt Normal;
            public Vector3 V { get { return new Vector3((float) Point.X, (float) Point.Y, (float) Point.Z); } }
            public Vector3 N { get { return new Vector3((float) Normal.X, (float) Normal.Y, (float) Normal.Z); } }
        }

        private static Mesh toMesh(VertexInfo[][] triangles, Color color)
        {
            return new Mesh
            {
                vertices = triangles.SelectMany(t => t).Select(v => v.V).ToArray(),
                normals = triangles.SelectMany(t => t).Select(v => v.N).ToArray(),
                triangles = triangles.SelectMany(t => t).Select((v, i) => i).ToArray()
            };
        }

        // Converts a 2D array of vertices into triangles by joining each vertex with the next in each dimension
        private static VertexInfo[][] createFaces(bool closedX, bool closedY, VertexInfo[][] meshData)
        {
            var len = meshData[0].Length;
            return Enumerable.Range(0, meshData.Length).SelectManyConsecutivePairs(closedX, (i1, i2) =>
                Enumerable.Range(0, len).SelectManyConsecutivePairs(closedY, (j1, j2) => new[]
                {
                    // triangle 1
                    new[] { meshData[i1][j1], meshData[i2][j1], meshData[i2][j2] },
                    // triangle 2
                    new[] { meshData[i1][j1], meshData[i2][j2], meshData[i1][j2] }
                }))
                    .ToArray();
        }

        private static double getWireLength(int index)
        {
            var outer = pt(_outerHexFrame.BoxCenterRadius, _outerHexFrame.Depth, 0);
            var inner = pt(_innerHexFrame.BoxCenterRadius, _innerHexFrame.Depth, 0).RotateY(30);
            if (index == 0)
                return outer.Distance(outer.RotateY(60));
            else if (index == 1)
                return inner.Distance(inner.RotateY(60));
            else
                return outer.Distance(inner);
        }

        private static VertexInfo[][] tubeFromCurve(Pt[] pts, double radius, int revSteps)
        {
            var normals = new Pt[pts.Length];
            normals[0] = ((pts[1] - pts[0]) * pt(0, 1, 0)).Normalize() * radius;
            for (int i = 1; i < pts.Length - 1; i++)
                normals[i] = normals[i - 1].ProjectOntoPlane((pts[i + 1] - pts[i]) + (pts[i] - pts[i - 1])).Normalize() * radius;
            normals[pts.Length - 1] = normals[pts.Length - 2].ProjectOntoPlane(pts[pts.Length - 1] - pts[pts.Length - 2]).Normalize() * radius;

            var axes = pts.Select((p, i) =>
                i == 0 ? new { Start = pts[0], End = pts[1] } :
                i == pts.Length - 1 ? new { Start = pts[pts.Length - 2], End = pts[pts.Length - 1] } :
                new { Start = p, End = p + (pts[i + 1] - p) + (p - pts[i - 1]) }).ToArray();

            return Enumerable.Range(0, pts.Length)
                .Select(ix => new { Axis = axes[ix], Perp = pts[ix] + normals[ix], Point = pts[ix] })
                .Select(inf => Enumerable.Range(0, revSteps)
                    .Select(i => 360 * i / revSteps)
                    .Select(angle => inf.Perp.Rotate(inf.Axis.Start, inf.Axis.End, angle))
                    .Select(p => new VertexInfo { Point = p, Normal = p - inf.Point }).Reverse().ToArray())
                .ToArray();
        }

        private static IEnumerable<Pt> bézier(Pt start, Pt control1, Pt control2, Pt end, int steps)
        {
            return Enumerable.Range(0, steps)
                .Select(i => (double) i / (steps - 1))
                .Select(t => pow(1 - t, 3) * start + 3 * pow(1 - t, 2) * t * control1 + 3 * (1 - t) * t * t * control2 + pow(t, 3) * end);
        }

        static double sin(double x)
        {
            return Math.Sin(x * Math.PI / 180);
        }

        static double cos(double x)
        {
            return Math.Cos(x * Math.PI / 180);
        }

        static double pow(double x, double y)
        {
            return Math.Pow(x, y);
        }

        static Pt pt(double x, double y, double z)
        {
            return new Pt(x, y, z);
        }

        static T[] newArray<T>(int size, Func<int, T> initialiser)
        {
            var result = new T[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = initialiser(i);
            }
            return result;
        }
    }
}
