// A port of earcut, the ear-slicing polygon triangulation of Mapbox (https://github.com/mapbox/earcut, version 2.2),
// to C#, as the world map's build uses it. The original's licence:
//
//   ISC License. Copyright (c) 2016, Mapbox.
//   Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby
//   granted, provided that the above copyright notice and this permission notice appear in all copies.
//   THE SOFTWARE IS PROVIDED "AS IS" AND ISC DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL
//   IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL ISC BE LIABLE FOR ANY SPECIAL, DIRECT,
//   INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER
//   IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
//   PERFORMANCE OF THIS SOFTWARE.
//
// The algorithm and its names are the original's; only the language changed.
namespace Puzzle.Tooling.WorldMap;

/// <summary>
/// Cuts a polygon with holes into triangles by slicing off its ears, with a z-order hash of its points so that large
/// rings (a continent's coast has thousands of points) stay fast, and with the original's fallbacks for the small
/// self-intersections a simplified coastline can have.
/// </summary>
public static class Earcut
{
    /// <summary>Above this many points a ring's ears are looked for through the z-order hash.</summary>
    private const int HashedFrom = 80;

    /// <param name="points">The outer ring's points, then each hole's, as x and y.</param>
    /// <param name="holeStarts">Where each hole's first point stands in <paramref name="points"/>; empty for a polygon without holes.</param>
    /// <returns>Three indices into <paramref name="points"/> per triangle.</returns>
    public static List<int> Triangulate(IReadOnlyList<(double X, double Y)> points, IReadOnlyList<int> holeStarts)
    {
        var triangles = new List<int>();
        int outerEnd = holeStarts.Count > 0 ? holeStarts[0] : points.Count;
        Node? outer = LinkedList(points, 0, outerEnd, clockwise: true);
        if (outer == null || outer.Next == outer.Prev)
        {
            return triangles;
        }

        if (holeStarts.Count > 0)
        {
            outer = EliminateHoles(points, holeStarts, outer);
        }

        double minX = 0, minY = 0, invSize = 0;
        if (points.Count > HashedFrom)
        {
            minX = points[0].X;
            minY = points[0].Y;
            double maxX = minX, maxY = minY;
            for (int i = 1; i < outerEnd; i++)
            {
                minX = Math.Min(minX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxX = Math.Max(maxX, points[i].X);
                maxY = Math.Max(maxY, points[i].Y);
            }

            invSize = Math.Max(maxX - minX, maxY - minY);
            invSize = invSize != 0 ? 32767 / invSize : 0;
        }

        EarcutLinked(outer, triangles, minX, minY, invSize, pass: 0);
        return triangles;
    }

    private static Node? LinkedList(IReadOnlyList<(double X, double Y)> points, int start, int end, bool clockwise)
    {
        Node? last = null;
        if (clockwise == (SignedArea(points, start, end) > 0))
        {
            for (int i = start; i < end; i++)
            {
                last = InsertNode(i, points[i].X, points[i].Y, last);
            }
        }
        else
        {
            for (int i = end - 1; i >= start; i--)
            {
                last = InsertNode(i, points[i].X, points[i].Y, last);
            }
        }

        if (last != null && AreEqual(last, last.Next))
        {
            RemoveNode(last);
            last = last.Next;
        }

        return last;
    }

    private static Node? FilterPoints(Node? start, Node? end = null)
    {
        if (start == null)
        {
            return start;
        }

        end ??= start;
        Node p = start;
        bool again;
        do
        {
            again = false;
            if (!p.Steiner && (AreEqual(p, p.Next) || Area(p.Prev, p, p.Next) == 0))
            {
                RemoveNode(p);
                p = end = p.Prev;
                if (p == p.Next)
                {
                    break;
                }

                again = true;
            }
            else
            {
                p = p.Next;
            }
        }
        while (again || p != end);

        return end;
    }

    private static void EarcutLinked(Node? ear, List<int> triangles, double minX, double minY, double invSize, int pass)
    {
        if (ear == null)
        {
            return;
        }

        if (pass == 0 && invSize != 0)
        {
            IndexCurve(ear, minX, minY, invSize);
        }

        Node stop = ear;
        while (ear.Prev != ear.Next)
        {
            Node prev = ear.Prev;
            Node next = ear.Next;
            if (invSize != 0 ? IsEarHashed(ear, minX, minY, invSize) : IsEar(ear))
            {
                triangles.Add(prev.I);
                triangles.Add(ear.I);
                triangles.Add(next.I);
                RemoveNode(ear);
                ear = next.Next;
                stop = next.Next;
                continue;
            }

            ear = next;
            if (ear == stop)
            {
                if (pass == 0)
                {
                    EarcutLinked(FilterPoints(ear), triangles, minX, minY, invSize, 1);
                }
                else if (pass == 1)
                {
                    Node? cured = CureLocalIntersections(FilterPoints(ear)!, triangles);
                    EarcutLinked(cured, triangles, minX, minY, invSize, 2);
                }
                else if (pass == 2)
                {
                    SplitEarcut(ear, triangles, minX, minY, invSize);
                }

                break;
            }
        }
    }

    private static bool IsEar(Node ear)
    {
        Node a = ear.Prev, b = ear, c = ear.Next;
        if (Area(a, b, c) >= 0)
        {
            return false;
        }

        (double x0, double y0, double x1, double y1) = BoxOf(a, b, c);
        Node p = c.Next;
        while (p != a)
        {
            if (p.X >= x0 && p.X <= x1 && p.Y >= y0 && p.Y <= y1 && PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0)
            {
                return false;
            }

            p = p.Next;
        }

        return true;
    }

    private static bool IsEarHashed(Node ear, double minX, double minY, double invSize)
    {
        Node a = ear.Prev, b = ear, c = ear.Next;
        if (Area(a, b, c) >= 0)
        {
            return false;
        }

        (double x0, double y0, double x1, double y1) = BoxOf(a, b, c);
        int minZ = ZOrder(x0, y0, minX, minY, invSize);
        int maxZ = ZOrder(x1, y1, minX, minY, invSize);
        Node? p = ear.PrevZ;
        Node? n = ear.NextZ;
        while (p != null && p.Z >= minZ && n != null && n.Z <= maxZ)
        {
            if (BlocksTheEar(p, a, c, x0, y0, x1, y1))
            {
                return false;
            }

            p = p.PrevZ;
            if (BlocksTheEar(n, a, c, x0, y0, x1, y1))
            {
                return false;
            }

            n = n.NextZ;
        }

        while (p != null && p.Z >= minZ)
        {
            if (BlocksTheEar(p, a, c, x0, y0, x1, y1))
            {
                return false;
            }

            p = p.PrevZ;
        }

        while (n != null && n.Z <= maxZ)
        {
            if (BlocksTheEar(n, a, c, x0, y0, x1, y1))
            {
                return false;
            }

            n = n.NextZ;
        }

        return true;
    }

    /// <summary>True when a point other than the ear's own corners lies in the ear and is no reflex corner: the ear cannot be cut.</summary>
    private static bool BlocksTheEar(Node p, Node a, Node c, double x0, double y0, double x1, double y1)
    {
        Node b = a.Next;
        return p.X >= x0 && p.X <= x1 && p.Y >= y0 && p.Y <= y1 && p != a && p != c
            && PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0;
    }

    private static (double X0, double Y0, double X1, double Y1) BoxOf(Node a, Node b, Node c) =>
        (Math.Min(a.X, Math.Min(b.X, c.X)), Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Max(a.X, Math.Max(b.X, c.X)), Math.Max(a.Y, Math.Max(b.Y, c.Y)));

    private static Node? CureLocalIntersections(Node start, List<int> triangles)
    {
        Node p = start;
        do
        {
            Node a = p.Prev, b = p.Next.Next;
            if (!AreEqual(a, b) && Intersects(a, p, p.Next, b) && LocallyInside(a, b) && LocallyInside(b, a))
            {
                triangles.Add(a.I);
                triangles.Add(p.I);
                triangles.Add(b.I);
                RemoveNode(p);
                RemoveNode(p.Next);
                p = start = b;
            }

            p = p.Next;
        }
        while (p != start);

        return FilterPoints(p);
    }

    private static void SplitEarcut(Node start, List<int> triangles, double minX, double minY, double invSize)
    {
        Node a = start;
        do
        {
            Node b = a.Next.Next;
            while (b != a.Prev)
            {
                if (a.I != b.I && IsValidDiagonal(a, b))
                {
                    Node c = SplitPolygon(a, b);
                    Node? first = FilterPoints(a, a.Next);
                    Node? second = FilterPoints(c, c.Next);
                    EarcutLinked(first, triangles, minX, minY, invSize, 0);
                    EarcutLinked(second, triangles, minX, minY, invSize, 0);
                    return;
                }

                b = b.Next;
            }

            a = a.Next;
        }
        while (a != start);
    }

    private static Node EliminateHoles(IReadOnlyList<(double X, double Y)> points, IReadOnlyList<int> holeStarts, Node outer)
    {
        var queue = new List<Node>();
        for (int hole = 0; hole < holeStarts.Count; hole++)
        {
            int start = holeStarts[hole];
            int end = hole < holeStarts.Count - 1 ? holeStarts[hole + 1] : points.Count;
            Node? list = LinkedList(points, start, end, clockwise: false);
            if (list == null)
            {
                continue;
            }

            if (list == list.Next)
            {
                list.Steiner = true;
            }

            queue.Add(LeftmostOf(list));
        }

        queue.Sort((first, second) => first.X.CompareTo(second.X));
        foreach (Node hole in queue)
        {
            outer = EliminateHole(hole, outer);
        }

        return outer;
    }

    private static Node EliminateHole(Node hole, Node outer)
    {
        Node? bridge = FindHoleBridge(hole, outer);
        if (bridge == null)
        {
            return outer;
        }

        Node bridgeReverse = SplitPolygon(bridge, hole);
        FilterPoints(bridgeReverse, bridgeReverse.Next);
        return FilterPoints(bridge, bridge.Next)!;
    }

    /// <summary>David Eberly's way to the point of the outer ring a hole is joined to.</summary>
    private static Node? FindHoleBridge(Node hole, Node outer)
    {
        Node p = outer;
        double hx = hole.X, hy = hole.Y, qx = double.NegativeInfinity;
        Node? m = null;
        do
        {
            if (hy <= p.Y && hy >= p.Next.Y && p.Next.Y != p.Y)
            {
                double x = p.X + (hy - p.Y) * (p.Next.X - p.X) / (p.Next.Y - p.Y);
                if (x <= hx && x > qx)
                {
                    qx = x;
                    m = p.X < p.Next.X ? p : p.Next;
                    if (x == hx)
                    {
                        return m;
                    }
                }
            }

            p = p.Next;
        }
        while (p != outer);

        if (m == null)
        {
            return null;
        }

        Node stop = m;
        double mx = m.X, my = m.Y, tanMin = double.PositiveInfinity;
        p = m;
        do
        {
            if (hx >= p.X && p.X >= mx && hx != p.X && PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.X, p.Y))
            {
                double tan = Math.Abs(hy - p.Y) / (hx - p.X);
                if (LocallyInside(p, hole) && (tan < tanMin || (tan == tanMin && (p.X > m.X || (p.X == m.X && SectorContainsSector(m, p))))))
                {
                    m = p;
                    tanMin = tan;
                }
            }

            p = p.Next;
        }
        while (p != stop);

        return m;
    }

    private static bool SectorContainsSector(Node m, Node p) => Area(m.Prev, m, p.Prev) < 0 && Area(p.Next, m, m.Next) < 0;

    private static void IndexCurve(Node start, double minX, double minY, double invSize)
    {
        Node p = start;
        do
        {
            if (p.Z == 0)
            {
                p.Z = ZOrder(p.X, p.Y, minX, minY, invSize);
            }

            p.PrevZ = p.Prev;
            p.NextZ = p.Next;
            p = p.Next;
        }
        while (p != start);

        p.PrevZ!.NextZ = null;
        p.PrevZ = null;
        SortLinked(p);
    }

    /// <summary>Simon Tatham's merge sort of a linked list, by z-order.</summary>
    private static void SortLinked(Node? list)
    {
        int inSize = 1;
        int merges;
        do
        {
            Node? p = list;
            list = null;
            Node? tail = null;
            merges = 0;
            while (p != null)
            {
                merges++;
                Node? q = p;
                int pSize = 0;
                for (int i = 0; i < inSize; i++)
                {
                    pSize++;
                    q = q.NextZ;
                    if (q == null)
                    {
                        break;
                    }
                }

                int qSize = inSize;
                while (pSize > 0 || (qSize > 0 && q != null))
                {
                    Node e;
                    if (pSize != 0 && (qSize == 0 || q == null || p!.Z <= q.Z))
                    {
                        e = p!;
                        p = p!.NextZ;
                        pSize--;
                    }
                    else
                    {
                        e = q!;
                        q = q!.NextZ;
                        qSize--;
                    }

                    if (tail != null)
                    {
                        tail.NextZ = e;
                    }
                    else
                    {
                        list = e;
                    }

                    e.PrevZ = tail;
                    tail = e;
                }

                p = q;
            }

            tail!.NextZ = null;
            inSize *= 2;
        }
        while (merges > 1);
    }

    private static int ZOrder(double px, double py, double minX, double minY, double invSize)
    {
        int x = (int)((px - minX) * invSize);
        int y = (int)((py - minY) * invSize);
        x = (x | (x << 8)) & 0x00FF00FF;
        x = (x | (x << 4)) & 0x0F0F0F0F;
        x = (x | (x << 2)) & 0x33333333;
        x = (x | (x << 1)) & 0x55555555;
        y = (y | (y << 8)) & 0x00FF00FF;
        y = (y | (y << 4)) & 0x0F0F0F0F;
        y = (y | (y << 2)) & 0x33333333;
        y = (y | (y << 1)) & 0x55555555;
        return x | (y << 1);
    }

    private static Node LeftmostOf(Node start)
    {
        Node p = start, leftmost = start;
        do
        {
            if (p.X < leftmost.X || (p.X == leftmost.X && p.Y < leftmost.Y))
            {
                leftmost = p;
            }

            p = p.Next;
        }
        while (p != start);

        return leftmost;
    }

    private static bool PointInTriangle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py) =>
        (cx - px) * (ay - py) >= (ax - px) * (cy - py)
        && (ax - px) * (by - py) >= (bx - px) * (ay - py)
        && (bx - px) * (cy - py) >= (cx - px) * (by - py);

    private static bool IsValidDiagonal(Node a, Node b) =>
        a.Next.I != b.I && a.Prev.I != b.I && !IntersectsPolygon(a, b)
        && ((LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b) && (Area(a.Prev, a, b.Prev) != 0 || Area(a, b.Prev, b) != 0))
            || (AreEqual(a, b) && Area(a.Prev, a, a.Next) > 0 && Area(b.Prev, b, b.Next) > 0));

    private static double Area(Node p, Node q, Node r) => (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);

    private static bool AreEqual(Node p1, Node p2) => p1.X == p2.X && p1.Y == p2.Y;

    private static bool Intersects(Node p1, Node q1, Node p2, Node q2)
    {
        int o1 = Math.Sign(Area(p1, q1, p2));
        int o2 = Math.Sign(Area(p1, q1, q2));
        int o3 = Math.Sign(Area(p2, q2, p1));
        int o4 = Math.Sign(Area(p2, q2, q1));
        return (o1 != o2 && o3 != o4)
            || (o1 == 0 && OnSegment(p1, p2, q1))
            || (o2 == 0 && OnSegment(p1, q2, q1))
            || (o3 == 0 && OnSegment(p2, p1, q2))
            || (o4 == 0 && OnSegment(p2, q1, q2));
    }

    private static bool OnSegment(Node p, Node q, Node r) =>
        q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) && q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y);

    private static bool IntersectsPolygon(Node a, Node b)
    {
        Node p = a;
        do
        {
            if (p.I != a.I && p.Next.I != a.I && p.I != b.I && p.Next.I != b.I && Intersects(p, p.Next, a, b))
            {
                return true;
            }

            p = p.Next;
        }
        while (p != a);

        return false;
    }

    private static bool LocallyInside(Node a, Node b) => Area(a.Prev, a, a.Next) < 0
        ? Area(a, b, a.Next) >= 0 && Area(a, a.Prev, b) >= 0
        : Area(a, b, a.Prev) < 0 || Area(a, a.Next, b) < 0;

    private static bool MiddleInside(Node a, Node b)
    {
        Node p = a;
        bool inside = false;
        double px = (a.X + b.X) / 2, py = (a.Y + b.Y) / 2;
        do
        {
            if ((p.Y > py) != (p.Next.Y > py) && p.Next.Y != p.Y && px < (p.Next.X - p.X) * (py - p.Y) / (p.Next.Y - p.Y) + p.X)
            {
                inside = !inside;
            }

            p = p.Next;
        }
        while (p != a);

        return inside;
    }

    private static Node SplitPolygon(Node a, Node b)
    {
        var a2 = new Node(a.I, a.X, a.Y);
        var b2 = new Node(b.I, b.X, b.Y);
        Node an = a.Next;
        Node bp = b.Prev;
        a.Next = b;
        b.Prev = a;
        a2.Next = an;
        an.Prev = a2;
        b2.Next = a2;
        a2.Prev = b2;
        bp.Next = b2;
        b2.Prev = bp;
        return b2;
    }

    private static Node InsertNode(int i, double x, double y, Node? last)
    {
        var p = new Node(i, x, y);
        if (last == null)
        {
            p.Prev = p;
            p.Next = p;
        }
        else
        {
            p.Next = last.Next;
            p.Prev = last;
            last.Next.Prev = p;
            last.Next = p;
        }

        return p;
    }

    private static void RemoveNode(Node p)
    {
        p.Next.Prev = p.Prev;
        p.Prev.Next = p.Next;
        if (p.PrevZ != null)
        {
            p.PrevZ.NextZ = p.NextZ;
        }

        if (p.NextZ != null)
        {
            p.NextZ.PrevZ = p.PrevZ;
        }
    }

    private static double SignedArea(IReadOnlyList<(double X, double Y)> points, int start, int end)
    {
        double sum = 0;
        for (int i = start, j = end - 1; i < end; i++)
        {
            sum += (points[j].X - points[i].X) * (points[i].Y + points[j].Y);
            j = i;
        }

        return sum;
    }

    /// <summary>A point of a ring in the circular list the slicing walks; a point a hole is joined by appears twice.</summary>
    private sealed class Node
    {
        public Node(int i, double x, double y)
        {
            I = i;
            X = x;
            Y = y;
            Prev = this;
            Next = this;
        }

        /// <summary>The point's index in the input.</summary>
        public int I { get; }

        public double X { get; }

        public double Y { get; }

        public Node Prev { get; set; }

        public Node Next { get; set; }

        public int Z { get; set; }

        public Node? PrevZ { get; set; }

        public Node? NextZ { get; set; }

        public bool Steiner { get; set; }
    }
}
