using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using FollowTheLeaderNS;

namespace FollowTheLeaderNS
{
    public class MeshGenerator
    {
        const double _hexFrameBoxOuterRadius = .003;
        const double _hexFrameBoxInnerRadius = .002;
        const double _hexFrameBoxHeight = .005;
        const double _hexFrameBoxLocationRatio = .6;

        const double _wireRadius = .0014;
        const double _wireMaxSegmentDeviation = .005;
        const double _wireMaxBézierDeviation = .005;

        sealed class HexFrameInfo
        {
            public double InnerRadius, OuterRadius, Bevel, Depth, StartAngle;

            public double BoxCenterRadius { get { return InnerRadius * _hexFrameBoxLocationRatio + OuterRadius * (1 - _hexFrameBoxLocationRatio); } }
        }

        private static HexFrameInfo _outerHexFrame = new HexFrameInfo { InnerRadius = .062, OuterRadius = .07, Bevel = .003, Depth = .005, StartAngle = 0 };
        private static HexFrameInfo _innerHexFrame = new HexFrameInfo { InnerRadius = .032, OuterRadius = .04, Bevel = .003, Depth = .005, StartAngle = 30 };

        public static Mesh GenerateWire(System.Random rnd, int lengthIndex)
        {
            var length = getWireLength(lengthIndex);
            var start = pt(0, 0, 0);
            var startControl = pt(length / 10, 0, .01);
            var endControl = pt(length * 9 / 10, 0, .01);
            var end = pt(length, 0, 0);
            var numSegments =
                lengthIndex == 0 ? 6 :
                lengthIndex == 1 ? 3 : 4;

            var bézierSteps = 16;
            var tubeRevSteps = 16;

            var interpolateStart = pt(0, 0, .007);
            var interpolateEnd = pt(length, 0, .007);

            var intermediatePoints = newArray(numSegments - 1, i => interpolateStart + (interpolateEnd - interpolateStart) * (i + 1) / numSegments + pt(rnd.NextDouble() - .5, rnd.NextDouble() - .5, rnd.NextDouble() - .5) * _wireMaxSegmentDeviation);
            var deviations = newArray(numSegments - 1, _ => pt(rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()) * _wireMaxBézierDeviation);

            var points =
                new[]{ new { ControlBefore = default(Pt), Point = start, ControlAfter = startControl } }
                .Concat(intermediatePoints.Select((p, i) => new { ControlBefore = p - deviations[i], Point = p, ControlAfter = p + deviations[i] }))
                .Concat(new[]{ new { ControlBefore = endControl, Point = end, ControlAfter = default(Pt) } })
                .SelectConsecutivePairs(false, (one, two) => bézier(one.Point, one.ControlAfter, two.ControlBefore, two.Point, bézierSteps))
                .SelectMany((x, i) => i == 0 ? x : x.Skip(1))
                .ToArray();
            return tubeFromCurve(points, startControl - start, end - endControl, _wireRadius, tubeRevSteps);
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
            public int VertexIndex;

            public Vector3 V { get { return new Vector3((float)Point.X, (float)Point.Y, (float)Point.Z); } }

            public Vector3 N { get { return new Vector3((float)Normal.X, (float)Normal.Y, (float)Normal.Z); } }
        }

        private static Mesh createMesh(bool closedX, bool closedY, VertexInfo[][] meshData)
        {
            var len = meshData[0].Length;
            var hashSet = new HashSet<Pt>();
            var uniqueVectors = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int i = 0; i < meshData.Length; i++)
                for (int j = 0; j < meshData[i].Length; j++)
                    if (hashSet.Add(meshData[i][j].Point))
                    {
                        meshData[i][j].VertexIndex = uniqueVectors.Count;
                        uniqueVectors.Add(meshData[i][j].V);
                        normals.Add(meshData[i][j].N);
                    }
                    else
                        meshData[i][j].VertexIndex = uniqueVectors.IndexOf(meshData[i][j].V);

            var mesh = new Mesh
            { 
                vertices = uniqueVectors.ToArray(),
                triangles = Enumerable.Range(0, meshData.Length).SelectManyConsecutivePairs(closedX, (i1, i2) =>
                    Enumerable.Range(0, len).SelectManyConsecutivePairs(closedY, (j1, j2) => new[]
                {
                    // triangle 1
                    meshData[i1][j1].VertexIndex, meshData[i2][j1].VertexIndex, meshData[i2][j2].VertexIndex,
                    // triangle 2
                    meshData[i1][j1].VertexIndex, meshData[i2][j2].VertexIndex, meshData[i1][j2].VertexIndex
                }))
                    .ToArray()
            };
            mesh.normals = normals.ToArray();
            return mesh;
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

        private static Mesh tubeFromCurve(Pt[] pts, Pt startTangent, Pt endTangent, double radius, int revSteps)
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

            return createMesh(false, true, Enumerable.Range(0, pts.Length)
                .Select(ix => new { Axis = axes[ix], Perp = pts[ix] + normals[ix], Point = pts[ix] })
                .Select(inf => Enumerable.Range(0, revSteps)
                    .Select(i => 360 * i / revSteps)
                    .Select(angle => inf.Perp.Rotate(inf.Axis.Start, inf.Axis.End, angle))
                    .Select(p => new VertexInfo { Point = p, Normal = p - inf.Point }).Reverse().ToArray())
                .ToArray());
        }

        private static IEnumerable<Pt> bézier(Pt start, Pt control1, Pt control2, Pt end, int steps)
        {
            return Enumerable.Range(0, steps)
                .Select(i => (double)i / (steps - 1))
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
